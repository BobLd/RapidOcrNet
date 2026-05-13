// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/OcrLite.cs

using Microsoft.ML.OnnxRuntime;
using SkiaSharp;
using System.Text;

namespace RapidOcrNet;

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
        int outerPadding = Math.Max(0, options.Padding);
        SKBitmap outerPadded = originSrc;
        SKBitmap? ownedOuter = null;
        if (outerPadding > 0)
        {
            ownedOuter = OcrUtils.MakePadding(originSrc, outerPadding);
            outerPadded = ownedOuter;
        }

        // PP-OCR resize_image_within_bounds: bring the input within [MinSideLen, MaxSideLen]
        // before any further processing. Skipped when caller forces legacy behavior with
        // ImgResize > 0 (so existing callers keep their pixel-for-pixel detector input).
        SKBitmap bounded = outerPadded;
        SKBitmap? ownedBounded = null;
        if (options.ImgResize <= 0)
        {
            bounded = OcrUtils.ResizeImageWithinBounds(outerPadded, options.MinSideLen, options.MaxSideLen, out bool boundOwned);
            if (boundOwned)
            {
                ownedBounded = bounded;
            }
        }

        SKBitmap letterboxed = OcrUtils.ApplyVerticalLetterbox(bounded, options.WidthHeightRatio, options.MinHeight, out int letterboxTop);
        SKBitmap? ownedLetterbox = !ReferenceEquals(letterboxed, bounded) ? letterboxed : null;

        try
        {
            ScaleParam scale;
            if (options.ImgResize > 0)
            {
                // Legacy path: explicit max-side cap. Caps at source size for tiny
                // images so 23x36 single-char crops aren't upscaled into giant inputs.
                int originMaxSide = Math.Max(originSrc.Width, originSrc.Height);
                int resize = options.ImgResize > originMaxSide ? originMaxSide : options.ImgResize;
                resize += 2 * outerPadding;
                scale = ScaleParam.GetScaleParam(letterboxed, resize);
            }
            else
            {
                // Python-style: scale short side up to LimitSideLen (default 736),
                // matching rapidocr-python's Det.limit_type="min" config.
                scale = ScaleParam.GetAdaptiveScaleParam(letterboxed, options.LimitSideLen);
            }

            int totalLeftPad = outerPadding;
            int totalTopPad = outerPadding + letterboxTop;
            var paddingRect = new SKRectI(totalLeftPad, totalTopPad,
                originSrc.Width + totalLeftPad, originSrc.Height + totalTopPad);

            // NOTE: when ResizeImageWithinBounds rescales (only when source max-side
            // exceeds MaxSideLen or post-pad min-side is below MinSideLen, neither
            // condition triggers for typical inputs), returned box/word coordinates
            // will be in the bounded image's space, not the original's. Acceptable for
            // current tests; can be fixed by scaling output coords by the bound ratio.
            return DetectOnce(letterboxed, paddingRect, scale,
                options.BoxScoreThresh, options.BoxThresh, options.UnClipRatio,
                options.DoAngle, options.MostAngle,
                options.ReturnWordBox, options.ReturnSingleCharBox,
                options.TextScore, options.ClsThresh,
                options.ClsPreserveAspectRatio);
        }
        finally
        {
            ownedLetterbox?.Dispose();
            ownedBounded?.Dispose();
            ownedOuter?.Dispose();
        }
    }

    private OcrResult DetectOnce(SKBitmap src, SKRectI originRect, ScaleParam scale, float boxScoreThresh,
        float boxThresh, float unClipRatio, bool doAngle, bool mostAngle,
        bool returnWordBox, bool returnSingleCharBox, float textScore, float clsThresh,
        bool clsPreserveAspectRatio)
    {
        // Start detect
        var sw = ValueStopwatch.StartNew();

        // step: dbNet getTextBoxes
        var textBoxes = _textDetector.GetTextBoxes(src, scale, boxScoreThresh, boxThresh, unClipRatio) ?? [];
        var dbNetTime = sw.ElapsedMilliseconds;

        // getPartImages: capture crop bookkeeping when word boxes are requested.
        // Both overloads now dispose partial results internally if a crop throws midway.
        SKBitmap[] partImages;
        CropContext[] cropContexts;
        if (returnWordBox)
        {
            (partImages, cropContexts) = OcrUtils.GetPartImagesWithContext(src, textBoxes);
        }
        else
        {
            partImages = OcrUtils.GetPartImages(src, textBoxes);
            cropContexts = [];
        }

        // step: angleNet getAngles
        Angle[] angles = _textClassifier.GetAngles(partImages, doAngle, mostAngle, clsPreserveAspectRatio);

        // Rotate partImgs only if the classifier is confident enough (Python <c>cls_thresh</c>).
        // Without this gate, low-confidence flips wrongly invert clean upright text and the
        // recognizer produces garbage like "1997" → "L66" or "This" → "s".
        for (int i = 0; i < partImages.Length; ++i)
        {
            if (angles[i].Index == 1 && angles[i].Score >= clsThresh)
            {
                var original = partImages[i];
                partImages[i] = OcrUtils.BitmapRotateClockWise180(original);
                original.Dispose();
            }
            else if (angles[i].Index == 1)
            {
                // Below threshold, treat as no-flip for downstream consumers / word-box mapping.
                angles[i].Index = 0;
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

            WordBox[]? wordResults = null;
            if (returnWordBox)
            {
                wordResults = CalRecBoxes.Build(
                    textLine,
                    cropContexts[i],
                    cls180: angle.Index == 1,
                    returnSingleCharBox: returnSingleCharBox);

                if (wordResults is not null)
                {
                    // Translate word polygons by the same origin offset applied to BoxPoints below.
                    for (int w = 0; w < wordResults.Length; w++)
                    {
                        var pts = wordResults[w].BoxPoints;
                        for (int p = 0; p < pts.Length; p++)
                        {
                            pts[p].X -= originRect.Left;
                            pts[p].Y -= originRect.Top;
                        }
                    }
                }
            }

            for (int p = 0; p < textBox.BoxPoints.Length; ++p)
            {
                ref SKPointI point = ref textBox.BoxPoints[p];
                point.X -= originRect.Left;
                point.Y -= originRect.Top;
            }

            textBlocks[i] = new TextBlock
            {
                BoxPoints = textBox.BoxPoints,
                BoxScore = textBox.Score,
                AngleIndex = angle.Index,
                AngleScore = angle.Score,
                AngleTime = angle.Time,
                Chars = textLine.Chars,
                CharScores = textLine.CharScores,
                WordResults = wordResults,
                CrnnTime = textLine.Time,
                BlockTime = angle.Time + textLine.Time,
                Text = GetText(textLine.Chars)
            };
        }

        // PP-OCR-style filtering: drop blocks with empty recognized text or
        // average char score below `textScore`.
        var filteredBlocks = new List<TextBlock>(textBlocks.Length);
        foreach (var block in textBlocks)
        {
            if (block.Chars is null || block.Chars.Length == 0)
            {
                continue;
            }

            string text = block.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (textScore > 0 && block.CharScores is { Length: > 0 })
            {
                float sum = 0;
                for (int s = 0; s < block.CharScores.Length; s++)
                {
                    sum += block.CharScores[s];
                }

                float avg = sum / block.CharScores.Length;
                if (avg < textScore)
                {
                    continue;
                }
            }

            filteredBlocks.Add(block);
        }

        textBlocks = filteredBlocks.ToArray();

        var fullDetectTime = sw.ElapsedMilliseconds;

        var strRes = new StringBuilder();
        foreach (var x in textBlocks)
        {
            strRes.AppendLine(x.Text);
        }

        return new OcrResult
        {
            TextBlocks = textBlocks,
            DbNetTime = (float)dbNetTime,
            DetectTime = (float)fullDetectTime,
            StrRes = strRes.ToString()
        };
    }

    private static string GetText(string[]? chars)
    {
        if (chars is null || chars.Length == 0)
        {
            return string.Empty;
        }

        return string.Concat(chars);
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

