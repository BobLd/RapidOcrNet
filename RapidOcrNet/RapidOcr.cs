// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/OcrLite.cs

using System.Text;
using Microsoft.ML.OnnxRuntime;
using SkiaSharp;

namespace RapidOcrNet
{
    public sealed class RapidOcr : IDisposable
    {
        public const string ModelsFolderName = "models";
        public const string ModelsVersion = "v5";
        public const string DefaultDetModelPath = "ch_PP-OCRv5_mobile_det.onnx";
        public const string DefaultClsModelPath = "ch_ppocr_mobile_v2.0_cls_infer.onnx";
        public const string DefaultRecModelPath = "latin_PP-OCRv5_rec_mobile_infer.onnx";
        public const string DefaultKeysFilePath = "ppocrv5_latin_dict.txt";

        private readonly TextDetector _textDetector = new TextDetector();
        private readonly TextClassifier _textClassifier = new TextClassifier();
        private readonly TextRecognizer _textRecognizer = new TextRecognizer();

        /// <summary>
        /// Initialize using default models (latin) and default options.
        /// </summary>
        public void InitModels(int numThread = 0)
        {
            using var sessionOptions = GetDefaultSessionOptions(numThread);
            InitModels(sessionOptions);
        }

        /// <summary>
        /// Initialize using default models (latin) and custom options.
        /// </summary>
        public void InitModels(SessionOptions op)
        {
            string detPath = Path.Combine(ModelsFolderName, ModelsVersion, DefaultDetModelPath);
            string clsPath = Path.Combine(ModelsFolderName, ModelsVersion, DefaultClsModelPath);
            string recPath = Path.Combine(ModelsFolderName, ModelsVersion, DefaultRecModelPath);
            string keysPath = Path.Combine(ModelsFolderName, ModelsVersion, DefaultKeysFilePath);

            InitModels(detPath, clsPath, recPath, keysPath, op);
        }

        /// <summary>
        /// Initialize using custom models and default options.
        /// </summary>
        public void InitModels(string detPath, string clsPath, string recPath, string keysPath, int numThread = 0)
        {
            using var sessionOptions = GetDefaultSessionOptions(numThread);
            InitModels(detPath, clsPath, recPath, keysPath, sessionOptions);
        }

        /// <summary>
        /// Initialize using custom models and custom options.
        /// </summary>
        public void InitModels(string detPath, string clsPath, string recPath, string keysPath, SessionOptions op)
        {
            _textDetector.InitModel(detPath, op);
            _textClassifier.InitModel(clsPath, op);
            _textRecognizer.InitModel(recPath, keysPath, op);
        }

        public OcrResult Detect(string path, RapidOcrOptions options)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Could not find image to process: '{path}'.", path);
            }

            using (var originSrc = SKBitmap.Decode(path))
            {
                return Detect(originSrc, options);
            }
        }

        public OcrResult Detect(SKBitmap originSrc, RapidOcrOptions options)
        {
            int originMaxSide = Math.Max(originSrc.Width, originSrc.Height);

            int resize;
            if (options.ImgResize <= 0 || options.ImgResize > originMaxSide)
            {
                resize = originMaxSide;
            }
            else
            {
                resize = options.ImgResize;
            }

            resize += 2 * options.Padding;
            var paddingRect = new SKRectI(options.Padding, options.Padding, originSrc.Width + options.Padding, originSrc.Height + options.Padding);
            using (SKBitmap paddingSrc = OcrUtils.MakePadding(originSrc, options.Padding))
            {
                return DetectOnce(paddingSrc, paddingRect, ScaleParam.GetScaleParam(paddingSrc, resize),
                    options.BoxScoreThresh, options.BoxThresh, options.UnClipRatio, options.DoAngle, options.MostAngle);
            }
        }

        private OcrResult DetectOnce(SKBitmap src, SKRectI originRect, ScaleParam scale, float boxScoreThresh,
            float boxThresh, float unClipRatio, bool doAngle, bool mostAngle)
        {
            // Start detect
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // step: dbNet getTextBoxes
            var textBoxes = _textDetector.GetTextBoxes(src, scale, boxScoreThresh, boxThresh, unClipRatio) ?? [];
            var dbNetTime = sw.ElapsedMilliseconds;

            // getPartImages
            SKBitmap[] partImages = OcrUtils.GetPartImages(src, textBoxes).ToArray();

            // step: angleNet getAngles
            Angle[] angles = _textClassifier.GetAngles(partImages, doAngle, mostAngle);

            // Rotate partImgs
            for (int i = 0; i < partImages.Length; ++i)
            {
                if (angles[i].Index == 1)
                {
                    var original = partImages[i];
                    partImages[i] = OcrUtils.BitmapRotateClockWise180(original);
                    original.Dispose();
                }
            }

            // step: crnnNet getTextLines
            TextLine[] textLines = _textRecognizer.GetTextLines(partImages);

            foreach (var bmp in partImages)
            {
                bmp.Dispose();
            }

            var textBlocks = new TextBlock[textLines.Length];
            for (int i = 0; i < textLines.Length; ++i)
            {
                var textBox = textBoxes[i];
                var angle = angles[i];
                var textLine = textLines[i];

                for (int p = 0; p < textBox.Points.Length; ++p)
                {
                    ref SKPointI point = ref textBox.Points[p];
                    point.X -= originRect.Left;
                    point.Y -= originRect.Top;
                }

                textBlocks[i] = new TextBlock
                {
                    BoxPoints = textBox.Points,
                    BoxScore = textBox.Score,
                    AngleIndex = angle.Index,
                    AngleScore = angle.Score,
                    AngleTime = angle.Time,
                    Chars = textLine.Chars,
                    CharScores = textLine.CharScores,
                    CrnnTime = textLine.Time,
                    BlockTime = angle.Time + textLine.Time
                };
            }

            var fullDetectTime = sw.ElapsedMilliseconds;

            var strRes = new StringBuilder();
            foreach (var x in textBlocks)
            {
                strRes.AppendLine(x.GetText());
            }

            return new OcrResult
            {
                TextBlocks = textBlocks,
                DbNetTime = dbNetTime,
                DetectTime = fullDetectTime,
                StrRes = strRes.ToString()
            };
        }

        public void Dispose()
        {
            _textClassifier.Dispose();
            _textRecognizer.Dispose();
            _textDetector.Dispose();
        }

        /// <summary>
        /// Creates a new instance of SessionOptions configured with extended graph optimization and the specified
        /// number of threads.
        /// </summary>
        /// <remarks>The returned SessionOptions object has GraphOptimizationLevel set to
        /// ORT_ENABLE_EXTENDED. Both InterOpNumThreads and IntraOpNumThreads are set to the value of
        /// numThread.</remarks>
        /// <param name="numThread">The number of threads to use for both inter- and intra-operation parallelism. If set to 0, the default
        /// thread count is used.</param>
        /// <returns>A SessionOptions instance with extended graph optimization enabled and thread counts set according to the
        /// specified value.</returns>
        public static SessionOptions GetDefaultSessionOptions(int numThread = 0)
        {
            var op = new SessionOptions();
            op.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
            op.InterOpNumThreads = numThread;
            op.IntraOpNumThreads = numThread;
            return op;
        }
    }
}
