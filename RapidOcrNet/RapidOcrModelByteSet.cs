// Apache-2.0 license

namespace RapidOcrNet;

/// <summary>
/// Describes a complete set of OCR models (detector, classifier, recognizer and the recognizer's
/// character dictionary) supplied as byte arrays, plus the detector's pixel-space normalization.
/// </summary>
/// <remarks>
/// <para>
/// Use this when the models are already bytes: decrypted from an encrypted resource, decompressed
/// from a blob, or handed over by something that reads them itself. A model given as a
/// <c>byte[]</c> is not copied by this library — it goes straight to the runtime, which parses it
/// and takes its own copy while the session is built. A model that arrives as a stream has to be
/// read into an array by the caller before it can be passed here.
/// </para>
/// <para>
/// Normalization defaults to what the bundled PP-OCRv5 detectors expect. A PP-OCRv6 detector needs
/// the other values stated explicitly (see <see cref="RapidOcrModelSet.PPOCRv6Small"/>), because a
/// detector sees nothing but pixels and cannot tell the two families apart.
/// </para>
/// </remarks>
public sealed record RapidOcrModelByteSet
{
    /// <summary>Detector (DBNet) ONNX model.</summary>
    public required byte[] DetModelBytes { get; init; }

    /// <summary>Angle classifier ONNX model.</summary>
    public required byte[] ClsModelBytes { get; init; }

    /// <summary>Recognizer (CRNN) ONNX model.</summary>
    public required byte[] RecModelBytes { get; init; }

    /// <summary>Recognizer's character dictionary (keys) file.</summary>
    public required byte[] KeysBytes { get; init; }

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
