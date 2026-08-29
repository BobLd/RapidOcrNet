// Apache-2.0 license

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.ML.OnnxRuntime;
using SkiaSharp;

namespace RapidOcrNet.Benchmarks;

/// <summary>
/// Effect of <see cref="RapidOcrOptions.RecMaxDegreeOfParallelism"/> on a text-dense page,
/// under both execution providers.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline benchmarks showed the dense page gaining least from WebGPU (2.6x, against 8x
/// on a page with three lines) because the recognizer ran one crop per inference — 106 tiny
/// dispatches, where per-dispatch overhead and un-overlapped CPU work dominate. This suite
/// measures what running those inferences concurrently recovers.
/// </para>
/// <para>
/// Unlike batching crops into one padded inference, concurrency does not change the model
/// input, so the recognized text, character scores and word boxes are identical at every
/// value here. The only axis is time.
/// </para>
/// <para>
/// BenchmarkDotNet puts each <c>[Params]</c> value in its own logical group, so there is no
/// baseline to ratio against across the parallelism axis — read the absolute <c>Mean</c>
/// down each category and compare rows. The categories keep CPU and WebGPU apart.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RecognizerParallelBenchmarks
{
    internal const string Cpu = "CPU";
    internal const string WebGpu = "WebGPU";

    // ~106 detected blocks, so recognition dominates and there is something to overlap. A
    // three-line image would finish before the thread pool had spun up.
    private const string DenseImage = "2108.11480_1.png";

    private SKBitmap? _bitmap;
    private RapidOcr? _ocr;
    private RapidOcrOptions _options = RapidOcrOptions.PPOCRv6;

    /// <summary>Concurrent recognizer inferences. 1 is the legacy serial path.</summary>
    [Params(1, 2, 4, 8, 16)]
    public int Dop { get; set; }

    [GlobalSetup(Target = nameof(Cpu_Recognize))]
    public void SetupCpu() => Setup(ExecutionProviderKind.Cpu);

    [GlobalSetup(Target = nameof(WebGpu_Recognize))]
    public void SetupWebGpu() => Setup(ExecutionProviderKind.WebGpu);

    [GlobalCleanup]
    public void Cleanup()
    {
        _ocr?.Dispose();
        _bitmap?.Dispose();
    }

    [BenchmarkCategory(Cpu)]
    [Benchmark]
    public int Cpu_Recognize() => _ocr!.Detect(_bitmap!, _options).TextBlocks.Length;

    [BenchmarkCategory(WebGpu)]
    [Benchmark]
    public int WebGpu_Recognize() => _ocr!.Detect(_bitmap!, _options).TextBlocks.Length;

    private void Setup(ExecutionProviderKind kind)
    {
        BenchmarkAssets.EnsureAvailable();

        if (kind == ExecutionProviderKind.WebGpu && !ExecutionProviders.IsWebGpuAvailable(out string? error))
        {
            throw new InvalidOperationException($"WebGPU execution provider unavailable: {error}");
        }

        _options = RapidOcrOptions.PPOCRv6 with { RecMaxDegreeOfParallelism = Dop };
        _bitmap = BenchmarkAssets.LoadImage(DenseImage);

        var ocr = new RapidOcr();
        using (SessionOptions sessionOptions = ExecutionProviders.Create(kind))
        {
            ocr.InitModels(BenchmarkAssets.PPOCRv6Small, sessionOptions);
        }

        _ocr = ocr;
    }
}
