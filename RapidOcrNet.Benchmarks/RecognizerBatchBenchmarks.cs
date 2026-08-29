// Apache-2.0 license

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.ML.OnnxRuntime;
using SkiaSharp;

namespace RapidOcrNet.Benchmarks;

/// <summary>
/// Effect of <see cref="RapidOcrOptions.RecBatchSize"/> on a text-dense page, under both
/// execution providers.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline benchmarks showed the dense page gaining least from WebGPU (2.6x, against 8x
/// on a page with three lines) because <see cref="TextRecognizer"/> ran one inference per
/// crop — 106 tiny dispatches, where per-dispatch overhead dominates. This suite measures
/// what batching those crops recovers.
/// </para>
/// <para>
/// BenchmarkDotNet puts each <c>[Params]</c> value in its own logical group, so there is no
/// baseline to ratio against across the batch axis - read the absolute <c>Mean</c> column
/// down each category and compare rows yourself. The categories keep CPU and WebGPU apart.
/// </para>
/// <para>
/// Speed is only half the question. Batching right-pads crops to a common width, which
/// changes what the network sees and therefore what it reads — see the accuracy table in
/// README.md. Do not read a good ratio here as a free win.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RecognizerBatchBenchmarks
{
    internal const string Cpu = "CPU";
    internal const string WebGpu = "WebGPU";

    // The dense page: ~106 detected blocks, so recognition dominates and the batch axis has
    // something to act on. A three-line image would spend the whole run in one chunk.
    private const string DenseImage = "2108.11480_1.png";

    private SKBitmap? _bitmap;
    private RapidOcr? _ocr;
    private RapidOcrOptions _options = RapidOcrOptions.PPOCRv6;

    /// <summary>Crops per recognizer inference. 1 is the legacy per-crop path.</summary>
    [Params(1, 4, 8, 16)]
    public int RecBatchSize { get; set; }

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

        _options = RapidOcrOptions.PPOCRv6 with { RecBatchSize = RecBatchSize };
        _bitmap = BenchmarkAssets.LoadImage(DenseImage);

        var ocr = new RapidOcr();
        using (SessionOptions sessionOptions = ExecutionProviders.Create(kind))
        {
            ocr.InitModels(BenchmarkAssets.PPOCRv6Small, sessionOptions);
        }

        _ocr = ocr;
    }
}
