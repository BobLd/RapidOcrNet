// Apache-2.0 license

using BenchmarkDotNet.Attributes;
using SkiaSharp;

namespace RapidOcrNet.Benchmarks;

/// <summary>
/// Steady-state inference cost when the three models do not all share one execution provider,
/// using <see cref="RapidOcrSessionOptions"/> (issue #51).
/// </summary>
/// <remarks>
/// <para>
/// The issue reports that on Apple Silicon the best layout was CoreML for the detector and
/// recognizer but CPU for the classifier: the classifier is a tiny graph run once per crop, so
/// an accelerator's per-dispatch overhead can cost more than it saves. The same question
/// applies to WebGPU, which is the accelerator these benchmarks have — hence
/// <see cref="WebGpu_CpuCls"/>, the issue's layout, against the two uniform ones.
/// </para>
/// <para>
/// <see cref="WebGpu_DetOnly"/> isolates the detector's share: it is the one large graph per
/// page, and the uniform-provider suite already showed it speeding up on its own.
/// </para>
/// <para>
/// Every arm fills all three slots from <see cref="ExecutionProviders.Create"/>, so graph
/// optimization and thread counts are the same everywhere and only the layout differs. Uses
/// the full pipeline with <see cref="RapidOcrOptions.PPOCRv6"/>, which enables the classifier;
/// a run with <c>DoAngle = false</c> would make the classifier's placement moot.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class PerModelProviderBenchmarks
{
    private static readonly RapidOcrOptions s_options = RapidOcrOptions.PPOCRv6;

    private static readonly ProviderLayout s_allCpu = ProviderLayout.All(ExecutionProviderKind.Cpu);
    private static readonly ProviderLayout s_allWebGpu = ProviderLayout.All(ExecutionProviderKind.WebGpu);

    private static readonly ProviderLayout s_webGpuCpuCls = new(
        ExecutionProviderKind.WebGpu, ExecutionProviderKind.Cpu, ExecutionProviderKind.WebGpu);

    private static readonly ProviderLayout s_webGpuDetOnly = new(
        ExecutionProviderKind.WebGpu, ExecutionProviderKind.Cpu, ExecutionProviderKind.Cpu);

    private SKBitmap? _bitmap;
    private RapidOcr? _ocr;

    /// <summary>Which test image the case runs over. Set by BenchmarkDotNet.</summary>
    [ParamsSource(nameof(ImageNames))]
    public string Image { get; set; } = string.Empty;

    public static IEnumerable<string> ImageNames() => BenchmarkAssets.Images.Keys;

    [GlobalSetup(Target = nameof(AllCpu))]
    public void SetupAllCpu() => Setup(s_allCpu);

    [GlobalSetup(Target = nameof(AllWebGpu))]
    public void SetupAllWebGpu() => Setup(s_allWebGpu);

    [GlobalSetup(Target = nameof(WebGpu_CpuCls))]
    public void SetupWebGpuCpuCls() => Setup(s_webGpuCpuCls);

    [GlobalSetup(Target = nameof(WebGpu_DetOnly))]
    public void SetupWebGpuDetOnly() => Setup(s_webGpuDetOnly);

    [GlobalCleanup]
    public void Cleanup()
    {
        _ocr?.Dispose();
        _bitmap?.Dispose();
    }

    [Benchmark(Baseline = true, Description = "all CPU")]
    public int AllCpu() => _ocr!.Detect(_bitmap!, s_options).TextBlocks.Length;

    [Benchmark(Description = "all WebGPU")]
    public int AllWebGpu() => _ocr!.Detect(_bitmap!, s_options).TextBlocks.Length;

    [Benchmark(Description = "WebGPU det+rec, CPU cls")]
    public int WebGpu_CpuCls() => _ocr!.Detect(_bitmap!, s_options).TextBlocks.Length;

    [Benchmark(Description = "WebGPU det, CPU cls+rec")]
    public int WebGpu_DetOnly() => _ocr!.Detect(_bitmap!, s_options).TextBlocks.Length;

    private void Setup(ProviderLayout layout)
    {
        BenchmarkAssets.EnsureAvailable();

        if (layout.Uses(ExecutionProviderKind.WebGpu) && !ExecutionProviders.IsWebGpuAvailable(out string? error))
        {
            throw new InvalidOperationException(
                $"WebGPU execution provider unavailable, refusing to report {layout} as a CPU-speed result: {error}");
        }

        _bitmap = BenchmarkAssets.LoadImage(Image);

        var ocr = new RapidOcr();
        using (PerModelSessionOptions sessionOptions = ExecutionProviders.CreatePerModel(layout))
        {
            ocr.InitModels(BenchmarkAssets.PPOCRv6Small, sessionOptions.Options);
        }

        _ocr = ocr;
    }
}
