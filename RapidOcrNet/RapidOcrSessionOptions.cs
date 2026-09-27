// Apache-2.0 license

using Microsoft.ML.OnnxRuntime;

namespace RapidOcrNet;

/// <summary>
/// Per-model ONNX Runtime <see cref="SessionOptions"/> used when loading the detector,
/// classifier and recognizer. Lets each model run on the execution provider that suits it,
/// e.g. CoreML for detection and recognition but plain CPU for the small classifier, where
/// the provider's overhead outweighs its gain.
/// </summary>
/// <remarks>
/// A <see langword="null"/> slot falls back to <see cref="RapidOcr.GetDefaultSessionOptions"/>
/// with <see cref="NumThread"/>; that fallback is created and disposed by
/// <see cref="RapidOcr"/>. Options supplied here stay owned by the caller, who disposes them
/// once <c>InitModels</c> has returned (the sessions do not keep a reference to them). The same
/// instance may be used for several slots.
/// <para>
/// This is load-time configuration, unrelated to <see cref="RapidOcrOptions"/>, which is passed
/// to each <c>Detect</c> call.
/// </para>
/// </remarks>
public sealed record RapidOcrSessionOptions
{
    /// <summary>Options for the detector session, or <see langword="null"/> for the default.</summary>
    public SessionOptions? Det { get; init; }

    /// <summary>Options for the classifier session, or <see langword="null"/> for the default.</summary>
    public SessionOptions? Cls { get; init; }

    /// <summary>Options for the recognizer session, or <see langword="null"/> for the default.</summary>
    public SessionOptions? Rec { get; init; }

    /// <summary>
    /// Thread count for the default options built for any <see langword="null"/> slot.
    /// 0 lets ONNX Runtime choose. Ignored for slots with explicit options.
    /// </summary>
    public int NumThread { get; init; }
}
