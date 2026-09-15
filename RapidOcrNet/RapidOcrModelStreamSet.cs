// Apache-2.0 license

namespace RapidOcrNet;

/// <summary>
/// Describes a complete set of OCR models (detector, classifier, recognizer and the recognizer's
/// character dictionary) supplied as streams, plus the detector's pixel-space normalization. The
/// in-memory counterpart of <see cref="RapidOcrModelSet"/>, for models that never exist as a file
/// this process can open: embedded resources, zip entries, blobs fetched over the network.
/// </summary>
/// <remarks>
/// <para>
/// The streams are read once, when the set is handed to
/// <see cref="RapidOcr.InitModels(RapidOcrModelStreamSet, Microsoft.ML.OnnxRuntime.SessionOptions)"/>,
/// and are not disposed there — the caller keeps ownership of them, and every stream must still be
/// open and positioned at its start at that point. Reading the same set twice therefore means
/// rewinding the streams first; a stream that cannot seek cannot be loaded twice.
/// </para>
/// <para>
/// Normalization defaults to what the bundled PP-OCRv5 detectors expect. A PP-OCRv6 detector needs
/// the other values stated explicitly (see <see cref="RapidOcrModelSet.PPOCRv6Small"/>), because a
/// detector sees nothing but pixels and cannot tell the two families apart.
/// </para>
/// </remarks>
public sealed record RapidOcrModelStreamSet
{
    /// <summary>Stream over the detector (DBNet) ONNX model.</summary>
    public required Stream DetModelStream { get; init; }

    /// <summary>Stream over the angle classifier ONNX model.</summary>
    public required Stream ClsModelStream { get; init; }

    /// <summary>Stream over the recognizer (CRNN) ONNX model.</summary>
    public required Stream RecModelStream { get; init; }

    /// <summary>Stream over the recognizer's character dictionary (keys) file.</summary>
    public required Stream KeysStream { get; init; }

    /// <summary>
    /// Detector normalization mean, in pixel space (e.g. 0.5 maps to 127.5). Defaults to the
    /// PP-OCRv5 ImageNet means; PP-OCRv6 uses 127.5.
    /// </summary>
    public float[] DetMean { get; init; } = RapidOcrModelSet.PPOCRv5Latin.DetMean;

    /// <summary>
    /// Detector normalization std, in pixel space (NOT pre-inverted). Defaults to the PP-OCRv5
    /// ImageNet stds; PP-OCRv6 uses 127.5.
    /// </summary>
    public float[] DetStd { get; init; } = RapidOcrModelSet.PPOCRv5Latin.DetStd;
}
