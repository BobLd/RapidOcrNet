// Apache-2.0 license

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.ML.OnnxRuntime;
using SkiaSharp;

namespace RapidOcrNet.Benchmarks;

/// <summary>
/// Steady-state inference cost of the PP-OCRv6 <i>small</i> models, CPU execution provider
/// versus the WebGPU plugin EP.
/// </summary>
/// <remarks>
/// <para>
/// Two categories, each with its own CPU baseline so the <c>Ratio</c> column is meaningful:
/// </para>
/// <list type="bullet">
/// <item><description><c>FullPipeline</c> - detection, angle classification and recognition,
/// i.e. what <see cref="RapidOcr.Detect(SKBitmap, RapidOcrOptions, CancellationToken)"/> costs a caller.</description></item>
/// <item><description><c>DetectorOnly</c> - the DBNet detector alone. It is one large
/// convolutional graph over the whole page, so it is where GPU offload has the best chance
/// of paying off; subtracting it from the full pipeline attributes the rest to the
/// classifier and recognizer.</description></item>
/// </list>
/// <para>
/// Session creation happens in <c>[GlobalSetup]</c>, so these numbers exclude model load and
/// WebGPU shader compilation - see <see cref="ModelInitBenchmarks"/> for that one-off cost.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class OcrPipelineBenchmarks
{
    internal const string FullPipeline = "FullPipeline";
    internal const string DetectorOnly = "DetectorOnly";

    // PPOCRv6 == PythonCompat: no white border, short-side adaptive resize. Using Default
    // here would starve the v6 detector of resolution and change what is being measured.
    private static readonly RapidOcrOptions s_options = RapidOcrOptions.PPOCRv6;

    private SKBitmap? _bitmap;
    private RapidOcr? _ocr;

    /// <summary>Which test image the case runs over. Set by BenchmarkDotNet.</summary>
    [ParamsSource(nameof(ImageNames))]
    public string Image { get; set; } = string.Empty;

    public static IEnumerable<string> ImageNames() => BenchmarkAssets.Images.Keys;

    [GlobalSetup(Targets = [nameof(Cpu_FullPipeline), nameof(Cpu_DetectorOnly)])]
    public void SetupCpu() => Setup(ExecutionProviderKind.Cpu);

    [GlobalSetup(Targets = [nameof(WebGpu_FullPipeline), nameof(WebGpu_DetectorOnly)])]
    public void SetupWebGpu() => Setup(ExecutionProviderKind.WebGpu);

    [GlobalCleanup]
    public void Cleanup()
    {
        _ocr?.Dispose();
        _bitmap?.Dispose();
    }

    [BenchmarkCategory(FullPipeline)]
    [Benchmark(Baseline = true, Description = "CPU EP")]
    public int Cpu_FullPipeline() => _ocr!.Detect(_bitmap!, s_options).TextBlocks.Length;

    [BenchmarkCategory(FullPipeline)]
    [Benchmark(Description = "WebGPU EP")]
    public int WebGpu_FullPipeline() => _ocr!.Detect(_bitmap!, s_options).TextBlocks.Length;

    [BenchmarkCategory(DetectorOnly)]
    [Benchmark(Baseline = true, Description = "CPU EP")]
    public int Cpu_DetectorOnly() => _ocr!.DetectBoxes(_bitmap!, s_options).Count;

    [BenchmarkCategory(DetectorOnly)]
    [Benchmark(Description = "WebGPU EP")]
    public int WebGpu_DetectorOnly() => _ocr!.DetectBoxes(_bitmap!, s_options).Count;

    private void Setup(ExecutionProviderKind kind)
    {
        BenchmarkAssets.EnsureAvailable();

        if (kind == ExecutionProviderKind.WebGpu && !ExecutionProviders.IsWebGpuAvailable(out string? error))
        {
            throw new InvalidOperationException(
                $"WebGPU execution provider unavailable, refusing to report it as a CPU-speed result: {error}");
        }

        _bitmap = BenchmarkAssets.LoadImage(Image);

        var ocr = new RapidOcr();
        using (SessionOptions sessionOptions = ExecutionProviders.Create(kind))
        {
            ocr.InitModels(BenchmarkAssets.PPOCRv6Small, sessionOptions);
        }

        _ocr = ocr;
    }
}
