// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/CrnnNet.cs

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
    private const int CrnnDefaultWidth = 320; // matches PP-OCR rec_img_shape [3, 48, 320]

    /// <summary>
    /// Width/height ratio every batch is padded out to at minimum, i.e. the shape the PP-OCR
    /// recognizers were exported for. Python's pipeline uses the same floor, so a batch is
    /// never narrower than the model's nominal 320px input even when every crop in it is short.
    /// </summary>
    private const float DefaultWhRatio = CrnnDefaultWidth / (float)CrnnDstHeight;

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
    /// Recognizes every crop one inference at a time. Equivalent to
    /// <see cref="GetTextLines(SKBitmap[], int, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
    /// with a batch size of 1.
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
    /// Recognizes every crop, optionally several per inference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With <paramref name="batchSize"/> of 1 each crop is resized to a tight fit (height 48,
    /// width scaled to preserve its aspect ratio) and run on its own. That is the legacy path
    /// and is bit-for-bit what this class has always done.
    /// </para>
    /// <para>
    /// Above 1, crops are sorted by aspect ratio, chunked, and each chunk is padded on the
    /// right to a common width of <c>48 * max(320/48, widest w/h in the chunk)</c> - the
    /// preprocessing Python's <c>rapidocr</c> applies, and the shape the PP-OCR models were
    /// exported for. Sorting first keeps crops of similar width together so the padding stays
    /// small. Each line records a <see cref="TextLine.LineTxtLen"/> covering only its
    /// un-padded portion, which is what <see cref="CalRecBoxes"/> needs to keep word boxes
    /// aligned.
    /// </para>
    /// <para>
    /// Batching is not free of consequence: right-padding changes what the network sees, and
    /// none of the bundled models is indifferent to it - a few percent of lines come back
    /// different, in both directions. Nor is it reliably faster, since the padding is real
    /// compute the tight-fit path never does. It is off by default and callers opt in per
    /// application through <see cref="RapidOcrOptions.RecBatchSize"/>, which documents the
    /// measurements.
    /// </para>
    /// </remarks>
    /// <param name="partImages">Cropped text-line images, in detection order.</param>
    /// <param name="batchSize">Crops per inference. Values below 1 are treated as 1.</param>
    /// <param name="progress">Reported as (recognised, total) - after each crop when unbatched,
    /// after each chunk when batched.</param>
    /// <param name="cancellationToken">Observed between crops/chunks and, via
    /// <see cref="RunOptions.Terminate"/>, within each inference.</param>
    public TextLine[] GetTextLines(SKBitmap[] partImages, int batchSize,
        IProgress<(int Completed, int Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (batchSize > 1 && partImages.Length > 1)
        {
            return GetTextLinesBatched(partImages, batchSize, progress, cancellationToken);
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

    private TextLine[] GetTextLinesBatched(SKBitmap[] partImages, int batchSize,
        IProgress<(int Completed, int Total)>? progress,
        CancellationToken cancellationToken)
    {
        var textLines = new TextLine[partImages.Length];

        // Sorting by aspect ratio is what keeps the padding cheap: neighbours in this order
        // have similar widths, so a chunk's common width stays close to its members' own.
        var order = new int[partImages.Length];
        var ratios = new float[partImages.Length];
        var sortKeys = new float[partImages.Length];
        for (int i = 0; i < partImages.Length; i++)
        {
            order[i] = i;
            ratios[i] = partImages[i].Width / (float)partImages[i].Height;
            sortKeys[i] = ratios[i];
        }

        Array.Sort(sortKeys, order);

        int completed = 0;
        for (int start = 0; start < order.Length; start += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int count = Math.Min(batchSize, order.Length - start);

            float maxWhRatio = DefaultWhRatio;
            for (int k = 0; k < count; k++)
            {
                maxWhRatio = Math.Max(maxWhRatio, ratios[order[start + k]]);
            }

            RecognizeChunk(partImages, order, ratios, start, count, maxWhRatio,
                (int)(CrnnDstHeight * maxWhRatio), textLines, cancellationToken);

            completed += count;
            progress?.Report((completed, partImages.Length));
        }

        return textLines;
    }

    private void RecognizeChunk(SKBitmap[] partImages, int[] order, float[] ratios,
        int start, int count, float maxWhRatio, int batchWidth,
        TextLine[] textLines, CancellationToken cancellationToken)
    {
        var sw = ValueStopwatch.StartNew();

        // Fresh tensor, so every column past a crop's own width is already 0 - the
        // zero-right-padding the models expect, applied after normalization as in Python.
        var batch = new DenseTensor<float>([count, 3, CrnnDstHeight, batchWidth]);

        for (int k = 0; k < count; k++)
        {
            int index = order[start + k];

            // Python clamps the resized width to the batch width rather than letting a crop
            // overflow it. Only reachable through rounding here, since maxWhRatio is taken
            // over this very chunk.
            int width = Math.Min((int)Math.Ceiling(CrnnDstHeight * ratios[index]), batchWidth);

            using SKBitmap resized = partImages[index].Resize(
                new SKSizeI(Math.Max(1, width), CrnnDstHeight), OcrUtils.NetworkSampling);
            OcrUtils.WriteIntoBatch(resized, batch, k, MeanValues, NormValues);
        }

        IReadOnlyCollection<NamedOnnxValue> inputs =
        [
            NamedOnnxValue.CreateFromTensor(_inputName, batch)
        ];

        try
        {
            using var results = OrtRun.Run(_crnnNet, inputs, cancellationToken);
            Tensor<float> scores = results[0].AsTensor<float>();

            int steps = scores.Dimensions[1];
            int classes = scores.Dimensions[2];

            // The whole chunk cost one inference, so there is no per-line time to report.
            // Amortising keeps the sum over a page equal to what recognition actually took,
            // which is what callers do with TextLine.Time.
            float perLine = (float)sw.ElapsedMilliseconds / count;

            for (int k = 0; k < count; k++)
            {
                int index = order[start + k];
                TextLine line = ScoreToTextLineFromBatch(scores, k, steps, classes);

                // Only the leading ratios[index]/maxWhRatio of the time axis corresponds to
                // real pixels, the rest is decoded padding. CalRecBoxes divides the crop's
                // width by this to get a per-column pixel width.
                line.LineTxtLen = steps * (ratios[index] / maxWhRatio);
                line.Time = perLine;
                textLines[index] = line;
            }

            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message + ex.StackTrace);
        }

        // The chunk failed as a unit, so every crop in it comes back empty rather than the
        // array keeping nulls that would NullReference further down the pipeline.
        float failedTime = (float)sw.ElapsedMilliseconds / count;
        for (int k = 0; k < count; k++)
        {
            textLines[order[start + k]] = new TextLine { Time = failedTime };
        }
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

    /// <summary>
    /// CTC-decodes one slot of a batched score tensor. Identical to <see cref="ScoreToTextLine"/>
    /// apart from indexing a batch slot rather than assuming slot 0; the caller sets
    /// <see cref="TextLine.LineTxtLen"/> afterwards, since only it knows how much of the time
    /// axis was padding.
    /// </summary>
    /// <param name="srcData">Score tensor shaped [N, timesteps, classes].</param>
    /// <param name="batchIdx">Slot to decode.</param>
    /// <param name="h">Number of CTC timesteps.</param>
    /// <param name="w">Number of classes.</param>
    private TextLine ScoreToTextLineFromBatch(Tensor<float> srcData, int batchIdx, int h, int w)
    {
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
                float v = srcData[batchIdx, i, j];
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
            ColCount = h
        };
    }

    public void Dispose()
    {
        // Null when InitModel was never reached: the models are loaded separately from
        // construction, so a caller whose load failed still disposes a half-built instance.
        _crnnNet?.Dispose();
    }
}
