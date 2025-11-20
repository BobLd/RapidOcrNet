// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/CrnnNet.cs

using System.Diagnostics;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidOcrNet
{
    public sealed class TextRecognizer : IDisposable
    {
        private static readonly float[] MeanValues = [127.5F, 127.5F, 127.5F];
        private static readonly float[] NormValues = [1.0F / 127.5F, 1.0F / 127.5F, 1.0F / 127.5F];
        private const int CrnnDstHeight = 48;
        //private const int CrnnCols = 6625;

        private InferenceSession _crnnNet;
        private string[] _keys;
        private string _inputName;

        public void InitModel(string path, string keysPath, int numThread)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Recognizer model file does not exist: '{path}'.");
            }

            if (!File.Exists(keysPath))
            {
                throw new FileNotFoundException($"Recognizer keys file does not exist: '{keysPath}'.");
            }

            var op = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
                InterOpNumThreads = numThread,
                IntraOpNumThreads = numThread
            };

            _crnnNet = new InferenceSession(path, op);
            _inputName = _crnnNet.InputMetadata.Keys.First();
            _keys = InitKeys(keysPath);
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
            var textLines = new TextLine[partImgs.Length];
            for (int i = 0; i < partImgs.Length; i++)
            {
                textLines[i] = GetTextLine(partImgs[i]);
            }

            return textLines;
        }

        public TextLine GetTextLine(SKBitmap src)
        {
            var sw = Stopwatch.StartNew();
            float scale = CrnnDstHeight / (float)src.Height;
            int dstWidth = (int)(src.Width * scale);

            Tensor<float> inputTensors;
            using (SKBitmap srcResize = src.Resize(new SKSizeI(dstWidth, CrnnDstHeight), new SKSamplingOptions(SKCubicResampler.Mitchell)))
            {
#if DEBUG
                using (var fs = new FileStream($"Recognizer_{Guid.NewGuid()}.png", FileMode.Create))
                {
                    srcResize.Encode(fs, SKEncodedImageFormat.Png, 100);
                }
#endif

                inputTensors = OcrUtils.SubtractMeanNormalize(srcResize, MeanValues, NormValues);
            }

            IReadOnlyCollection<NamedOnnxValue> inputs = new NamedOnnxValue[]
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensors)
            };

            try
            {
                using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _crnnNet.Run(inputs))
                {
                    var result = results[0];
                    var tl = ScoreToTextLine(result.AsTensor<float>());
                    tl.Time = sw.ElapsedMilliseconds;
                    return tl;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + ex.StackTrace);
                //throw ex;
            }

            return new TextLine() { Time = sw.ElapsedMilliseconds };
        }

        private TextLine ScoreToTextLine(Tensor<float> srcData)
        {
            var dimensions = srcData.Dimensions;
            int h = dimensions[1];
            int w = dimensions[2];

            int lastIndex = 0;
            var scores = new List<float>();
            var chars = new List<string>();

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
                }

                lastIndex = maxIndex;
            }

            return new TextLine
            {
                Chars = chars.ToArray(),
                CharScores = scores.ToArray()
            };
        }

        private TextLine ScoreToTextLine(ReadOnlySpan<float> srcData, int h, int w)
        {
            int lastIndex = 0;
            var scores = new List<float>();
            var chars = new List<string>();

            for (int i = 0; i < h; i++)
            {
                int maxIndex = 0;
                float maxValue = -1000F;
                for (int j = 0; j < w; j++)
                {
                    int idx = i * w + j;
                    if (srcData[idx] > maxValue)
                    {
                        maxIndex = j;
                        maxValue = srcData[idx];
                    }
                }

                if (maxIndex > 0 && maxIndex < _keys.Length && !(i > 0 && maxIndex == lastIndex))
                {
                    scores.Add(maxValue);
                    chars.Add(_keys[maxIndex]);
                }

                lastIndex = maxIndex;
            }

            return new TextLine
            {
                Chars = chars.ToArray(),
                CharScores = scores.ToArray()
            };
        }

        public void Dispose()
        {
            _crnnNet.Dispose();
        }
    }
}