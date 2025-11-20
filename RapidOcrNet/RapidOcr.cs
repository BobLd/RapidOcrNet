// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/OcrLite.cs

using System.Text;
using SkiaSharp;

namespace RapidOcrNet
{
    public sealed class RapidOcr : IDisposable
    {
        private readonly TextDetector _textDetector = new TextDetector();
        private readonly TextClassifier _textClassifier = new TextClassifier();
        private readonly TextRecognizer _textRecognizer = new TextRecognizer();

        /// <summary>
        /// Initialize using default models (latin).
        /// </summary>
        public void InitModels(int numThread = 0)
        {
            const string modelsFolderName = "models";
            const string modelsVersion = "v5";

            string detPath = Path.Combine(modelsFolderName, modelsVersion, "ch_PP-OCRv5_mobile_det.onnx");
            string clsPath = Path.Combine(modelsFolderName, modelsVersion, "ch_ppocr_mobile_v2.0_cls_infer.onnx");
            string recPath = Path.Combine(modelsFolderName, modelsVersion, "latin_PP-OCRv5_rec_mobile_infer.onnx");
            string keysPath = Path.Combine(modelsFolderName, modelsVersion, "ppocrv5_latin_dict.txt");

            InitModels(detPath, clsPath, recPath, keysPath, numThread);
        }

        public void InitModels(string detPath, string clsPath, string recPath, string keysPath, int numThread)
        {
            _textDetector.InitModel(detPath, numThread);
            _textClassifier.InitModel(clsPath, numThread);
            _textRecognizer.InitModel(recPath, keysPath, numThread);
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
                    partImages[i] = OcrUtils.BitmapRotateClockWise180(partImages[i]);
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
    }
}
