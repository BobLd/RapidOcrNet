// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/OcrLite.cs

using Microsoft.ML.OnnxRuntime;
using SkiaSharp;
using System.Text;

namespace RapidOcrNet;

public sealed partial class RapidOcr : IDisposable
{
    public const string ModelsFolderName = "models";
    public const string ModelsVersion = "v5";
    public const string DefaultDetModelPath = "ch_PP-OCRv5_mobile_det.onnx";
    public const string DefaultClsModelPath = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx";
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

    /// <summary>
    /// Initialize using a model set (e.g. <see cref="RapidOcrModelSet.PPOCRv5Latin"/> or
    /// <see cref="RapidOcrModelSet.PPOCRv6Small"/>) and default options.
    /// </summary>
    public void InitModels(RapidOcrModelSet models, int numThread = 0)
    {
        using var sessionOptions = GetDefaultSessionOptions(numThread);
        InitModels(models, sessionOptions);
    }

    /// <summary>
    /// Initialize using a model set (e.g. <see cref="RapidOcrModelSet.PPOCRv5Latin"/> or
    /// <see cref="RapidOcrModelSet.PPOCRv6Small"/>) and custom options. The model set carries
    /// the detector's per-version normalization, so v6 detectors are wired up correctly.
    /// </summary>
    public void InitModels(RapidOcrModelSet models, SessionOptions op)
    {
        ArgumentNullException.ThrowIfNull(models);

        _textDetector.InitModel(models.DetModelPath, models.DetMean, models.DetStd, op);
        _textClassifier.InitModel(models.ClsModelPath, op);
        _textRecognizer.InitModel(models.RecModelPath, models.KeysPath, op);
    }

    /// <inheritdoc cref="Detect(SKBitmap, RapidOcrOptions, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
    public OcrResult Detect(string path, RapidOcrOptions options, CancellationToken cancellationToken = default)
    {
        return Detect(path, options, null, cancellationToken);
    }

    /// <inheritdoc cref="Detect(SKBitmap, RapidOcrOptions, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
    // progress is deliberately not optional: with a default it would make Detect(path, options)
    // ambiguous against the overload above, which is the same call with everything defaulted.
    public OcrResult Detect(string path, RapidOcrOptions options, IProgress<(int Completed, int Total)>? progress, CancellationToken cancellationToken = default)
    {
        // Decoding comes before any stage and is not cheap — a quarter of a second for a large
        // page — so it is worth not starting it for a caller who has already given up.
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Could not find image to process: '{path}'.", path);
        }

        using var originSrc = SKBitmap.Decode(path);
        return Detect(originSrc, options, progress, cancellationToken);
    }

    /// <inheritdoc cref="Detect(SKBitmap, RapidOcrOptions, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
    public OcrResult Detect(SKBitmap originSrc, RapidOcrOptions options, CancellationToken cancellationToken = default)
    {
        return Detect(originSrc, options, null, cancellationToken);
    }

    /// <summary>
    /// Runs the detection.
    /// </summary>
    /// <param name="originSrc"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken">Observed at every stage boundary, between crops within the angle-classification and
    /// recognition stages, and inside each ONNX run itself: every inference carries a
    /// <see cref="RunOptions"/> whose terminate flag the runtime reads once per kernel.
    /// </param>
    /// <param name="progress">Reported during recognition as (lines recognised, lines detected). Nothing is reported
    /// before detection finishes, because the line count is not known until then.</param>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled before the next stage or crop.
    /// </exception>
    // progress is deliberately not optional: see the note on the path overload above.
    public OcrResult Detect(SKBitmap originSrc, RapidOcrOptions options, IProgress<(int Completed, int Total)>? progress, CancellationToken cancellationToken = default)
    {
        using var input = PrepareDetectorInput(originSrc, options, cancellationToken);
        return DetectOnce(input,
            options.BoxScoreThresh, options.BoxThresh, options.UnClipRatio,
            options.DoAngle, options.MostAngle,
            options.ReturnWordBox, options.ReturnSingleCharBox,
            options.TextScore, options.ClsThresh,
            options.ClsPreserveAspectRatio,
            options.RecMaxDegreeOfParallelism,
            progress,
            cancellationToken);
    }
    
    /// <summary>
    /// Runs the detection stage only and returns the raw text boxes, skipping angle
    /// classification and recognition. Mirrors Python rapidocr's
    /// <c>ocr(image, use_det=True, use_cls=False, use_rec=False)</c> call. Useful when
    /// you need layout boxes before deciding how to crop and OCR the image (e.g. split
    /// a scan into columns or per-region passes).
    /// </summary>
    /// <param name="path">Path to the source image.</param>
    /// <param name="options">Detection options. Recognition-only fields (TextScore,
    /// ReturnWordBox, ClsThresh, etc.) are ignored on this path.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Boxes in source-image coordinates, sorted in reading order.</returns>
    public IReadOnlyList<TextBox> DetectBoxes(string path, RapidOcrOptions options, CancellationToken cancellationToken = default)
    {
        // As in Detect(string, ...): do not decode on behalf of a caller who has already given up.
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Could not find image to process: '{path}'.", path);
        }

        using var originSrc = SKBitmap.Decode(path);
        return DetectBoxes(originSrc, options, cancellationToken);
    }

    /// <summary>
    /// Runs the detection stage only and returns the raw text boxes, skipping angle
    /// classification and recognition.
    /// </summary>
    public IReadOnlyList<TextBox> DetectBoxes(SKBitmap originSrc, RapidOcrOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var input = PrepareDetectorInput(originSrc, options, cancellationToken);
        var textBoxes = _textDetector.GetTextBoxes(input.Bitmap, input.Scale,
            options.BoxScoreThresh, options.BoxThresh, options.UnClipRatio, cancellationToken) ?? [];

        // Terminate only reaches the inference, and the stage does not end there: contour
        // finding, per-contour scoring, unclipping and sorting all run afterwards in managed
        // code. Without this the boxes would still be mapped and returned as a complete result
        // to a caller who has already given up. DetectOnce checks at the same point.
        cancellationToken.ThrowIfCancellationRequested();

        // Map from letterboxed-image space back into the original image's space, the
        // same transform Detect applies to TextBlock.BoxPoints. Boxes own fresh
        // point arrays, so in-place mutation is safe.
        foreach (var box in textBoxes)
        {
            input.MapToOriginal(box.BoxPoints);
        }

        return textBoxes;
    }

    /// <param name="cancellationToken">
    /// Observed between the steps below. These run before the first stage and can dominate the
    /// call on a large image — padding a 5184x6708 page allocates and blits some 140MB, around
    /// 900ms — so a caller who has already given up should not be made to wait the whole of it
    /// out. Each step is a single Skia operation and so is not itself interruptible; a step
    /// boundary is the finest granularity available here.
    /// </param>
    private static DetectorInput PrepareDetectorInput(SKBitmap originSrc, RapidOcrOptions options, CancellationToken cancellationToken = default)
    {
        // Before anything is allocated, which is the whole point on a large page.
        cancellationToken.ThrowIfCancellationRequested();

        int outerPadding = Math.Max(0, options.Padding);
        SKBitmap outerPadded = originSrc;
        SKBitmap? ownedOuter = null;
        SKBitmap? ownedBounded = null;
        SKBitmap? ownedLetterbox = null;

        // Everything that can allocate is inside, so an exception from any step — cancellation or
        // otherwise — takes the bitmaps already allocated with it. Previously only the scale
        // computation was guarded, and a throw from either resize leaked whatever came before it.
        try
        {
            if (outerPadding > 0)
            {
                ownedOuter = OcrUtils.MakePadding(originSrc, outerPadding);
                outerPadded = ownedOuter;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // PP-OCR resize_image_within_bounds: bring the input within [MinSideLen, MaxSideLen]
            // before any further processing. Skipped when caller forces legacy behavior with
            // ImgResize > 0 (so existing callers keep their pixel-for-pixel detector input).
            SKBitmap bounded = outerPadded;
            if (options.ImgResize <= 0)
            {
                bounded = OcrUtils.ResizeImageWithinBounds(outerPadded, options.MinSideLen, options.MaxSideLen, out bool boundOwned);
                if (boundOwned)
                {
                    ownedBounded = bounded;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            SKBitmap letterboxed = OcrUtils.ApplyVerticalLetterbox(bounded, options.WidthHeightRatio, options.MinHeight, out int letterboxTop);
            ownedLetterbox = !ReferenceEquals(letterboxed, bounded) ? letterboxed : null;

            cancellationToken.ThrowIfCancellationRequested();

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

            // Bound ratio = pre-resize size / bounded size, per axis (the two sides are
            // rounded to /32 independently, so they can differ). This is Python rapidocr's
            // ratio_w / ratio_h from resize_image_within_bounds, used to map detector-space
            // coordinates back up into the original image. When ResizeImageWithinBounds was a
            // no-op (typical inputs, or the legacy ImgResize path), bounded == outerPadded so
            // both ratios are exactly 1.
            float boundRatioW = outerPadded.Width / (float)bounded.Width;
            float boundRatioH = outerPadded.Height / (float)bounded.Height;

            return new DetectorInput(letterboxed, scale, outerPadding, letterboxTop,
                boundRatioW, boundRatioH, originSrc.Width, originSrc.Height,
                ownedOuter, ownedBounded, ownedLetterbox);
        }
        catch
        {
            ownedLetterbox?.Dispose();
            ownedBounded?.Dispose();
            ownedOuter?.Dispose();
            throw;
        }
    }

    private readonly struct DetectorInput : IDisposable
    {
        public readonly SKBitmap Bitmap;
        public readonly ScaleParam Scale;
        private readonly int _outerPadding;
        private readonly int _letterboxTop;
        private readonly float _boundRatioW;
        private readonly float _boundRatioH;
        private readonly int _originWidth;
        private readonly int _originHeight;
        private readonly SKBitmap? _ownedOuter;
        private readonly SKBitmap? _ownedBounded;
        private readonly SKBitmap? _ownedLetterbox;

        public DetectorInput(SKBitmap bitmap, ScaleParam scale, int outerPadding, int letterboxTop,
            float boundRatioW, float boundRatioH, int originWidth, int originHeight,
            SKBitmap? ownedOuter, SKBitmap? ownedBounded, SKBitmap? ownedLetterbox)
        {
            Bitmap = bitmap;
            Scale = scale;
            _outerPadding = outerPadding;
            _letterboxTop = letterboxTop;
            _boundRatioW = boundRatioW;
            _boundRatioH = boundRatioH;
            _originWidth = originWidth;
            _originHeight = originHeight;
            _ownedOuter = ownedOuter;
            _ownedBounded = ownedBounded;
            _ownedLetterbox = ownedLetterbox;
        }

        // Map detector (letterboxed) coordinates back into the original image space,
        // undoing the vertical letterbox, bound-ratio rescale and outer padding. Mirrors
        // Python rapidocr's map_boxes_to_original. Points are mutated in place.
        public void MapToOriginal(SKPointI[] points)
        {
            for (int p = 0; p < points.Length; p++)
            {
                MapPointToOriginal(ref points[p], _outerPadding, _letterboxTop,
                    _boundRatioW, _boundRatioH, _originWidth, _originHeight);
            }
        }

        public void Dispose()
        {
            _ownedLetterbox?.Dispose();
            _ownedBounded?.Dispose();
            _ownedOuter?.Dispose();
        }
    }

    private OcrResult DetectOnce(in DetectorInput input, float boxScoreThresh,
        float boxThresh, float unClipRatio, bool doAngle, bool mostAngle,
        bool returnWordBox, bool returnSingleCharBox, float textScore, float clsThresh,
        bool clsPreserveAspectRatio, int recMaxDegreeOfParallelism,
        IProgress<(int Completed, int Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        SKBitmap src = input.Bitmap;

        // Start detect
        var sw = ValueStopwatch.StartNew();

        cancellationToken.ThrowIfCancellationRequested();

        // step: dbNet getTextBoxes
        var textBoxes = _textDetector.GetTextBoxes(src, input.Scale, boxScoreThresh, boxThresh, unClipRatio,
            cancellationToken) ?? [];
        var dbNetTime = sw.ElapsedMilliseconds;

        // Cheapest place to abandon a page: detection is done, but the per-line stages that
        // dominate the cost have not started, and no crops have been allocated yet.
        cancellationToken.ThrowIfCancellationRequested();

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
        Angle[] angles;
        TextLine[] textLines;
        try
        {
            angles = _textClassifier.GetAngles(partImages, doAngle, mostAngle, clsPreserveAspectRatio,
                cancellationToken);

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
            textLines = _textRecognizer.GetTextLines(partImages, recMaxDegreeOfParallelism, progress, cancellationToken);
        }
        finally
        {
            // Cancellation unwinds through here as well. The crops are native Skia bitmaps, so
            // leaking them on an abandoned page would be the one lasting cost of giving up.
            foreach (var bmp in partImages)
            {
                bmp?.Dispose();
            }
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
                    // Map word polygons back to original space, same as BoxPoints below.
                    for (int w = 0; w < wordResults.Length; w++)
                    {
                        input.MapToOriginal(wordResults[w].BoxPoints);
                    }
                }
            }

            input.MapToOriginal(textBox.BoxPoints);

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

    /// <summary>
    /// Map a single detector-space point back into the original image's pixel space,
    /// undoing the preprocessing transforms in reverse order. Mirrors Python rapidocr's
    /// <c>map_boxes_to_original</c> (utils/process_img.py): remove the vertical letterbox,
    /// rescale by the <see cref="OcrUtils.ResizeImageWithinBounds"/> bound ratio, then
    /// remove the outer padding. The detector returns coordinates in letterboxed (bounded)
    /// space, so without the rescale step boxes for images outside [MinSideLen, MaxSideLen]
    /// come back in the wrong scale.
    /// </summary>
    /// <remarks>
    /// Left-side letterbox padding is always 0 (only vertical letterboxing is applied), so
    /// only Y is offset by <paramref name="letterboxTop"/>. The mapped point is finally clamped
    /// to <c>[0, originWidth] x [0, originHeight]</c>, matching the Python reference.
    /// </remarks>
    private static void MapPointToOriginal(ref SKPointI point, int outerPadding, int letterboxTop,
        float boundRatioW, float boundRatioH, int originWidth, int originHeight)
    {
        // letterboxed space -> bounded space: remove the vertical letterbox (left pad is 0).
        float x = point.X;
        float y = point.Y - letterboxTop;

        // bounded space -> outer-padded space: scale back up by the bound ratio.
        x *= boundRatioW;
        y *= boundRatioH;

        // outer-padded space -> original space: remove the outer padding.
        x -= outerPadding;
        y -= outerPadding;

        // Clamp to the original image bounds (Python map_boxes_to_original).
        point.X = Math.Clamp((int)MathF.Round(x), 0, originWidth);
        point.Y = Math.Clamp((int)MathF.Round(y), 0, originHeight);
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
    /// numThread.
    /// <para>
    /// No value of <paramref name="numThread"/> weakens cancellation: it is built on
    /// <see cref="RunOptions.Terminate"/>, which the executor reads on whichever thread walks the
    /// graph, so even <c>numThread: 1</c> stays interruptible mid-inference.
    /// </para></remarks>
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

