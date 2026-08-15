using SkiaSharp;

namespace RapidOcrNet;

public partial class RapidOcr
{
    /// <inheritdoc cref="DetectAsync(SKBitmap, RapidOcrOptions, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
    public Task<OcrResult> DetectAsync(SKBitmap originSrc, RapidOcrOptions options, CancellationToken cancellationToken = default)
    {
        return DetectAsync(originSrc, options, null, cancellationToken);
    }
    
    /// <summary>
    /// Awaits a <see cref="Detect(SKBitmap, RapidOcrOptions, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
    /// on the thread pool. Use it to keep a UI thread responsive; the token and
    /// <paramref name="progress"/> behave exactly as they do on the synchronous call.
    /// </summary>
    /// <remarks>
    /// This offloads the work rather than freeing a thread — OCR here is CPU-bound from end to
    /// end, through ONNX inference, Skia resampling, contour finding and CTC decoding alike, so
    /// there is no thread to give back and no throughput to gain on a server.
    /// <para>
    /// ONNX Runtime's <c>InferenceSession.RunAsync</c> would not change that. It schedules the
    /// same blocking <c>Run</c> onto the intra-op thread pool
    /// (<c>InferenceSession::RunAsync</c> in <c>inference_session.cc</c>) — <c>Task.Run</c> over a
    /// pool that is meanwhile trying to parallelize the inference, which is why it rejects a
    /// session whose intra-op degree of parallelism is below 2. Its C# binding also requires
    /// every output to be preallocated at exactly the right shape, and the recognizer's time
    /// dimension is not known before the run. So it is deliberately not used here.
    /// </para>
    /// </remarks>
    // progress is deliberately not optional: with a default it would make
    // DetectAsync(originSrc, options) ambiguous against the token-only overload above.
    public Task<OcrResult> DetectAsync(SKBitmap originSrc, RapidOcrOptions options, IProgress<(int Completed, int Total)>? progress, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Detect(originSrc, options, progress, cancellationToken), cancellationToken);
    }

    /// <inheritdoc cref="DetectAsync(SKBitmap, RapidOcrOptions, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
    public Task<OcrResult> DetectAsync(string path, RapidOcrOptions options, CancellationToken cancellationToken = default)
    {
        return DetectAsync(path, options, null, cancellationToken);
    }

    /// <inheritdoc cref="DetectAsync(SKBitmap, RapidOcrOptions, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
    // progress is deliberately not optional: see the note on the SKBitmap overload above.
    public Task<OcrResult> DetectAsync(string path, RapidOcrOptions options, IProgress<(int Completed, int Total)>? progress, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Detect(path, options, progress, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Awaits a <see cref="DetectBoxes(SKBitmap, RapidOcrOptions, CancellationToken)"/> on the
    /// thread pool. See the remarks on
    /// <see cref="DetectAsync(SKBitmap, RapidOcrOptions, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
    /// for what awaiting does and does not buy you.
    /// </summary>
    public Task<IReadOnlyList<TextBox>> DetectBoxesAsync(SKBitmap originSrc, RapidOcrOptions options, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => DetectBoxes(originSrc, options, cancellationToken), cancellationToken);
    }

    /// <inheritdoc cref="DetectBoxesAsync(SKBitmap, RapidOcrOptions, CancellationToken)"/>
    public Task<IReadOnlyList<TextBox>> DetectBoxesAsync(string path, RapidOcrOptions options, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => DetectBoxes(path, options, cancellationToken), cancellationToken);
    }
}
