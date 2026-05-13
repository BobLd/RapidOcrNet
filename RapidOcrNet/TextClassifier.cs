// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/AngleNet.cs

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidOcrNet;

public sealed class TextClassifier : IDisposable
{
    private const int AngleDstWidth = 192;
    private const int AngleDstHeight = 48;
    private const int AngleCols = 2;

    private static readonly float[] MeanValues = [127.5F, 127.5F, 127.5F];
    private static readonly float[] NormValues = [1.0F / 127.5F, 1.0F / 127.5F, 1.0F / 127.5F];

    private InferenceSession _angleNet;
    private string _inputName;

    public void InitModel(string path, SessionOptions op)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Classifier model file does not exist: '{path}'.");
        }

        _angleNet = new InferenceSession(path, op);
        _inputName = _angleNet.InputMetadata.Keys.First();
    }

    public void InitModel(string path, int numThread)
    {
        using var sessionOptions = RapidOcr.GetDefaultSessionOptions(numThread);
        InitModel(path, sessionOptions);
    }

    public Angle[] GetAngles(SKBitmap[] partImgs, bool doAngle, bool mostAngle, bool preserveAspectRatio = false)
    {
        var angles = new Angle[partImgs.Length];
        if (doAngle)
        {
            for (int i = 0; i < partImgs.Length; i++)
            {
                angles[i] = GetAngle(partImgs[i], preserveAspectRatio);
            }

            // Most Possible AngleIndex
            if (mostAngle)
            {
                int sum = 0;
                foreach (var a in angles)
                {
                    sum += a.Index;
                }
                double halfPercent = angles.Length / 2.0f;

                int mostAngleIndex = sum < halfPercent ? 0 : 1; // All angles set to 0 or 1
                System.Diagnostics.Debug.WriteLine($"Set All Angle to mostAngleIndex({mostAngleIndex})");
                foreach (var angle in angles)
                {
                    angle.Index = mostAngleIndex;
                }
            }
        }
        else
        {
            for (int i = 0; i < partImgs.Length; i++)
            {
                angles[i] = new Angle
                {
                    Index = -1,
                    Score = 0F
                };
            }
        }

        return angles;
    }

    public Angle GetAngle(SKBitmap src) => GetAngle(src, preserveAspectRatio: false);

    public Angle GetAngle(SKBitmap src, bool preserveAspectRatio)
    {
        var sw = ValueStopwatch.StartNew();
        Tensor<float> inputTensors;

        if (preserveAspectRatio)
        {
            // PP-OCR cls preprocessing (Python ch_ppocr_cls/main.py:83-106):
            //   1. resize preserving aspect to (resized_w, AngleDstHeight) where
            //      resized_w = min(AngleDstWidth, ceil(AngleDstHeight * w/h))
            //   2. zero-pad in normalized space (right side stays 0 in the [-1,1] tensor)
            //
            // In raw pixel space, "normalized 0" corresponds to midgray (127.5). So we
            // clear the canvas to midgray BEFORE drawing the resized strip, then run
            // the standard (pixel - 127.5) / 127.5 normalization on the whole image.
            float ratio = src.Width / (float)src.Height;
            int resizedW = Math.Min(AngleDstWidth, (int)Math.Ceiling(AngleDstHeight * ratio));
            resizedW = Math.Max(resizedW, 1);

            var angleInfo = new SKImageInfo(AngleDstWidth, AngleDstHeight, SKColorType.Bgra8888, SKAlphaType.Opaque);
            using (var resized = src.Resize(new SKSizeI(resizedW, AngleDstHeight), OcrUtils.NetworkSampling))
            using (var angleImg = new SKBitmap(angleInfo))
            {
                using (var canvas = new SKCanvas(angleImg))
                {
                    canvas.Clear(new SKColor(128, 128, 128));
                    canvas.DrawBitmap(resized, 0, 0);
                }
                inputTensors = OcrUtils.SubtractMeanNormalize(angleImg, MeanValues, NormValues);
            }
        }
        else
        {
            // Legacy: non-uniform stretch to (AngleDstWidth, AngleDstHeight) with
            // Mitchell cubic. The bundled PP-OCRv5 cls ONNX is tuned for this.
            using (var angleImg = src.Resize(new SKSizeI(AngleDstWidth, AngleDstHeight), new SKSamplingOptions(SKCubicResampler.Mitchell)))
            {
                inputTensors = OcrUtils.SubtractMeanNormalize(angleImg, MeanValues, NormValues);
            }
        }

        IReadOnlyCollection<NamedOnnxValue> inputs =
        [
            NamedOnnxValue.CreateFromTensor(_inputName, inputTensors)
        ];

        try
        {
            using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _angleNet.Run(inputs))
            {
                var outputTensor = results[0];

                ReadOnlySpan<float> outputData;
                if (outputTensor.AsTensor<float>() is DenseTensor<float> dt)
                {
                    outputData = dt.Buffer.Span;
                }
                else
                {
                    outputData = outputTensor.AsEnumerable<float>().ToArray();
                }

                var angle = ScoreToAngle(outputData, AngleCols);
                angle.Time = (float)sw.ElapsedMilliseconds;
                return angle;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message + ex.StackTrace);
            //throw;
        }

        return new Angle() { Time = (float)sw.ElapsedMilliseconds };
    }

    private static Angle ScoreToAngle(ReadOnlySpan<float> srcData, int angleColumns)
    {
        int angleIndex = 0;
        float maxValue = srcData[0];

        for (int i = 1; i < angleColumns; ++i)
        {
            float current = srcData[i];
            if (current > maxValue)
            {
                angleIndex = i;
                maxValue = current;
            }
        }

        return new Angle
        {
            Index = angleIndex,
            Score = maxValue
        };
    }

    public void Dispose()
    {
        _angleNet.Dispose();
    }
}
