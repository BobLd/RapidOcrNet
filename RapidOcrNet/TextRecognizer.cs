// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/CrnnNet.cs

using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidOcrNet;

public sealed class TextRecognizer : IDisposable
{
    private static readonly float[] MeanValues = [127.5F, 127.5F, 127.5F];
    private static readonly float[] NormValues = [1.0F / 127.5F, 1.0F / 127.5F, 1.0F / 127.5F];
    private const int CrnnDstHeight = 48;
    //private const int CrnnDefaultWidth = 320; // matches PP-OCR rec_img_shape [3, 48, 320]
    //private const int RecBatchNum = 6;

    private InferenceSession _crnnNet;
    private string[] _keys;
    private string _inputName;

    public void InitModel(string path, string keysPath, SessionOptions op)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Recognizer model file does not exist: '{path}'.");
        }

        if (!File.Exists(keysPath))
        {
            throw new FileNotFoundException($"Recognizer keys file does not exist: '{keysPath}'.");
        }

        _crnnNet = new InferenceSession(path, op);
        _inputName = _crnnNet.InputMetadata.Keys.First();
        _keys = InitKeys(keysPath);
    }

    public void InitModel(string path, string keysPath, int numThread)
    {
        using var sessionOptions = RapidOcr.GetDefaultSessionOptions(numThread);
        InitModel(path, keysPath, sessionOptions);
    }

    private static string[] InitKeys(string path)
    {
        using (var sr = new StreamReader(path, Encoding.UTF8))
        {
            List<string> keys = ["#"];

            while (sr.ReadLine() is { } line)
            {
                keys.Add(line);
            }

            keys.Add(" ");
            System.Diagnostics.Debug.WriteLine($"keys Size = {keys.Count}");

            return keys.ToArray();
        }
    }

    /// <summary>
    /// Recognizes every crop on the calling thread, one after another. Equivalent to
    /// <see cref="GetTextLines(SKBitmap[], int, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
    /// with a degree of parallelism of 1.
    /// </summary>
    /// <param name="partImages">Cropped text-line images, in detection order.</param>
    /// <param name="progress">Reported after each crop as (recognised, total). Recognition is the long pole of a page and
    /// its cost is per line, so this is the only stage where a caller can show real movement.</param>
    /// <param name="cancellationToken">Observed between crops and, via <see cref="RunOptions.Terminate"/>, within each crop's own
    /// inference. A caller abandoning a page stops inside the line being recognised rather than after it.</param>
    public TextLine[] GetTextLines(SKBitmap[] partImages,
        IProgress<(int Completed, int Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return GetTextLines(partImages, 1, progress, cancellationToken);
    }

    /// <summary>
    /// Recognizes every crop, optionally running several inferences concurrently.
    /// </summary>
    /// <remarks>
    /// Concurrency changes timing only, never what a caller sees: results stay in detection
    /// order, and a crop that fails throws as itself rather than inside the
    /// <see cref="AggregateException"/> <see cref="Parallel.For(int, int, Action{int})"/> would
    /// otherwise raise. Where several crops fail, the first one observed is the one that
    /// surfaces, as on the serial path — which never reaches the crops after the first throw.
    /// </remarks>
    /// <param name="partImages">Cropped text-line images, in detection order. Results come back
    /// in that order however the work is scheduled.</param>
    /// <param name="maxDegreeOfParallelism">Concurrent inferences. 1 runs everything on the
    /// calling thread, as this class always has, and is what the overload without this
    /// parameter does. -1 lets the thread pool decide. Every other value — 0, and anything
    /// below -1 — throws rather than being coerced, so a miscomputed degree surfaces here
    /// instead of silently running unbounded.</param>
    /// <param name="progress">Reported as (recognised, total), once per crop, counting 1..total
    /// in order. With concurrency the crops behind those numbers do not complete in order, so
    /// treat the count as a count rather than an index. Reports are serialized to keep them
    /// ordered, so a handler that blocks holds up the crops queued behind it; and they arrive on
    /// worker threads, so one that touches UI state directly must marshal —
    /// <see cref="Progress{T}"/> already does both cheaply.</param>
    /// <param name="cancellationToken">Observed between crops and, via
    /// <see cref="RunOptions.Terminate"/>, within each crop's own inference.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxDegreeOfParallelism"/> is 0 or less than -1.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    public TextLine[] GetTextLines(SKBitmap[] partImages, int maxDegreeOfParallelism,
        IProgress<(int Completed, int Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(maxDegreeOfParallelism, nameof(maxDegreeOfParallelism));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDegreeOfParallelism, -1, nameof(maxDegreeOfParallelism));

        if (maxDegreeOfParallelism != 1 && partImages.Length > 1)
        {
            var textLinesP = new TextLine[partImages.Length];
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            int completed = 0;
            object progressLock = new();

            try
            {
                // Each iteration writes its own slot, so the results need no synchronization and stay
                // in detection order no matter which thread finishes when.
                Parallel.For(0, partImages.Length, parallelOptions, i =>
                {
                    textLinesP[i] = GetTextLine(partImages[i], cancellationToken);

                    if (progress is not null)
                    {
                        lock (progressLock)
                        {
                            progress.Report((++completed, partImages.Length));
                        }
                    }
                });
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Count > 0)
            {
                // A degree of parallelism is a performance knob, so it must not change what a
                // caller has to catch. Serially the first failing crop throws as itself; here
                // Parallel.For would box it in an AggregateException, so unbox it and rethrow
                // with its original stack intact. Later failures are dropped, which is what the
                // serial path does too - it never reaches the crops after the first throw.
                ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                throw; // Unreachable: Throw() above does not return.
            }

            return textLinesP;
        }

        var textLines = new TextLine[partImages.Length];
        for (int i = 0; i < partImages.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            textLines[i] = GetTextLine(partImages[i], cancellationToken);
            progress?.Report((i + 1, partImages.Length));
        }
        return textLines;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="partImage"></param>
    /// <param name="cancellationToken">Observed between crops and, via <see cref="RunOptions.Terminate"/>, within each crop's own
    /// inference. A caller abandoning a page stops inside the line being recognised rather than after it.</param>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled before or during the inference.
    /// </exception>
    public TextLine GetTextLine(SKBitmap partImage, CancellationToken cancellationToken = default)
    {
        var sw = ValueStopwatch.StartNew();
        float scale = CrnnDstHeight / (float)partImage.Height;
        int dstWidth = (int)(partImage.Width * scale);

        Tensor<float> inputTensors;
        using (SKBitmap srcResize = partImage.Resize(new SKSizeI(dstWidth, CrnnDstHeight), OcrUtils.NetworkSampling))
        {
//#if DEBUG
//            using (var fs = new FileStream($"Recognizer_{Guid.NewGuid()}.png", FileMode.Create))
//            {
//                srcResize.Encode(fs, SKEncodedImageFormat.Png, 100);
//            }
//#endif

            inputTensors = OcrUtils.SubtractMeanNormalize(srcResize, MeanValues, NormValues);
        }

        IReadOnlyCollection<NamedOnnxValue> inputs =
        [
            NamedOnnxValue.CreateFromTensor(_inputName, inputTensors)
        ];

        try
        {
            using var results = OrtRun.Run(_crnnNet, inputs, cancellationToken);
            var result = results[0];
            var tl = ScoreToTextLine(result.AsTensor<float>());
            tl.Time = (float)sw.ElapsedMilliseconds;
            return tl;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message + ex.StackTrace);
        }

        return new TextLine() { Time = (float)sw.ElapsedMilliseconds };
    }

    private TextLine ScoreToTextLine(Tensor<float> srcData)
    {
        var dimensions = srcData.Dimensions;
        int h = dimensions[1];
        int w = dimensions[2];

        int lastIndex = 0;
        var scores = new List<float>();
        var chars = new List<string>();
        var cols = new List<int>();

        for (int i = 0; i < h; i++)
        {
            int maxIndex = 0;
            float maxValue = -1000F;

            for (int j = 0; j < w; j++)
            {
                float v = srcData[0, i, j];
                if (v > maxValue)
                {
                    maxIndex = j;
                    maxValue = v;
                }
            }

            if (maxIndex > 0 && maxIndex < _keys.Length && !(i > 0 && maxIndex == lastIndex))
            {
                scores.Add(maxValue);
                chars.Add(_keys[maxIndex]);
                cols.Add(i);
            }

            lastIndex = maxIndex;
        }

        return new TextLine
        {
            Chars = chars.ToArray(),
            CharScores = scores.ToArray(),
            CharCols = cols.ToArray(),
            ColCount = h,
            LineTxtLen = h
        };
    }

    public void Dispose()
    {
        // Null when InitModel was never reached: the models are loaded separately from
        // construction, so a caller whose load failed still disposes a half-built instance.
        _crnnNet?.Dispose();
    }
}
