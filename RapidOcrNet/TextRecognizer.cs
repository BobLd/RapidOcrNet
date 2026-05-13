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

    public TextLine[] GetTextLines(SKBitmap[] partImgs)
    {
        // NOTE: Python's pipeline batches crops by aspect ratio and zero-right-pads
        // each crop to 48 * max(w/h, 320/48) so the recognizer sees its training
        // distribution. Empirically the bundled PP-OCRv5 latin ONNX model in this
        // repo does NOT cope well with that right-side padding, it produces wrong
        // characters and 1-char substitutions on a few inputs. So we keep the legacy
        // per-image, tight-fit recognizer call (which the model evidently was
        // re-tuned for) while still recording CTC column indices.
        var textLines = new TextLine[partImgs.Length];
        for (int i = 0; i < partImgs.Length; i++)
        {
            textLines[i] = GetTextLine(partImgs[i]);
        }
        return textLines;
    }

    public TextLine GetTextLine(SKBitmap src)
    {
        var sw = ValueStopwatch.StartNew();
        float scale = CrnnDstHeight / (float)src.Height;
        int dstWidth = (int)(src.Width * scale);

        Tensor<float> inputTensors;
        using (SKBitmap srcResize = src.Resize(new SKSizeI(dstWidth, CrnnDstHeight), OcrUtils.NetworkSampling))
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
            using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _crnnNet.Run(inputs))
            {
                var result = results[0];
                var tl = ScoreToTextLine(result.AsTensor<float>());
                tl.Time = (float)sw.ElapsedMilliseconds;
                return tl;
            }
        }
        catch (Exception ex)
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

    private static void WriteImageIntoBatch(SKBitmap src, Tensor<float> batch, int batchIdx, int batchW)
    {
        int rows = src.Height;
        int cols = src.Width;
        int rowBytes = src.RowBytes;
        int channels = src.BytesPerPixel;
        ReadOnlySpan<byte> span = src.GetPixelSpan();

        if (src.Info.ColorType == SKColorType.Gray8)
        {
            for (int r = 0; r < rows; r++)
            {
                int rowBase = r * rowBytes;
                for (int c = 0; c < cols; c++)
                {
                    float v = (span[rowBase + c] - 127.5F) / 127.5F;
                    batch[batchIdx, 0, r, c] = v;
                    batch[batchIdx, 1, r, c] = v;
                    batch[batchIdx, 2, r, c] = v;
                }
                // remaining cols are zero-padded (DenseTensor default value)
            }
        }
        else if (src.Info.ColorType == SKColorType.Bgra8888)
        {
            for (int r = 0; r < rows; r++)
            {
                int rowBase = r * rowBytes;
                for (int c = 0; c < cols; c++)
                {
                    int pixelBase = rowBase + c * channels;
                    batch[batchIdx, 0, r, c] = (span[pixelBase + 0] - 127.5F) / 127.5F;
                    batch[batchIdx, 1, r, c] = (span[pixelBase + 1] - 127.5F) / 127.5F;
                    batch[batchIdx, 2, r, c] = (span[pixelBase + 2] - 127.5F) / 127.5F;
                }
                // remaining cols are zero-padded (already 0 in DenseTensor)
            }
        }
        else
        {
            throw new ArgumentException($"Recognizer crop must be '{SKColorType.Bgra8888}' or '{SKColorType.Gray8}'.");
        }
    }

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
        _crnnNet.Dispose();
    }
}
