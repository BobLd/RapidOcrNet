// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/AngleNet.cs

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidOcrNet
{
    public sealed class TextClassifier : IDisposable
    {
        private const int AngleDstWidth = 192;
        private const int AngleDstHeight = 48;
        private const int AngleCols = 2;

        private static readonly float[] MeanValues = [127.5F, 127.5F, 127.5F];
        private static readonly float[] NormValues = [1.0F / 127.5F, 1.0F / 127.5F, 1.0F / 127.5F];

        private InferenceSession _angleNet;
        private string _inputName;

        public void InitModel(string path, int numThread)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Classifier model file does not exist: '{path}'.");
            }

            var op = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
                InterOpNumThreads = numThread,
                IntraOpNumThreads = numThread
            };
            _angleNet = new InferenceSession(path, op);
            _inputName = _angleNet.InputMetadata.Keys.First();
        }

        public Angle[] GetAngles(SKBitmap[] partImgs, bool doAngle, bool mostAngle)
        {
            var angles = new Angle[partImgs.Length];
            if (doAngle)
            {
                for (int i = 0; i < partImgs.Length; i++)
                {
                    angles[i] = GetAngle(partImgs[i]);
                }

                // Most Possible AngleIndex
                if (mostAngle)
                {
                    double sum = angles.Sum(x => x.Index);
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

        public Angle GetAngle(SKBitmap src)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Tensor<float> inputTensors;
            using (var angleImg = src.Resize(new SKSizeI(AngleDstWidth, AngleDstHeight), new SKSamplingOptions(SKCubicResampler.Mitchell)))
            {
#if DEBUG
                using (var fs = new FileStream($"Classifier_{Guid.NewGuid()}.png", FileMode.Create))
                {
                    angleImg.Encode(fs, SKEncodedImageFormat.Png, 100);
                }
#endif

                inputTensors = OcrUtils.SubtractMeanNormalize(angleImg, MeanValues, NormValues);
            }

            IReadOnlyCollection<NamedOnnxValue> inputs = new NamedOnnxValue[]
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensors)
            };

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
                    angle.Time = sw.ElapsedMilliseconds;
                    return angle;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + ex.StackTrace);
                //throw;
            }

            return new Angle() { Time = sw.ElapsedMilliseconds };
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
}
