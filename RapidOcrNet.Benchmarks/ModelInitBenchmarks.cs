// Apache-2.0 license

using BenchmarkDotNet.Attributes;
using Microsoft.ML.OnnxRuntime;

namespace RapidOcrNet.Benchmarks;

/// <summary>
/// One-off cost of standing up the three PP-OCRv6 <i>small</i> sessions, CPU execution
/// provider versus the WebGPU plugin EP.
/// </summary>
/// <remarks>
/// This is the half of the comparison that <see cref="OcrPipelineBenchmarks"/> deliberately
/// excludes. A GPU provider has to compile shaders for every kernel in the graph the first
/// time it sees it, which can cost seconds - so an application that OCRs a handful of images
/// per process can lose overall even when steady-state inference gets faster. Read the two
/// tables together: WebGPU only pays off once
/// <c>(images per process) x (per-image saving) &gt; (extra init cost)</c>.
/// <para>
/// <see cref="RapidOcr.Dispose"/> is inside the measured region. Tearing the sessions down
/// is part of the lifecycle being priced, and it is small next to loading ~31 MB of models.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class ModelInitBenchmarks
{
    [GlobalSetup]
    public void Setup()
    {
        BenchmarkAssets.EnsureAvailable();

        // Pay the plugin registration and adapter discovery once, in setup, so it is not
        // attributed to the first measured iteration.
        if (!ExecutionProviders.IsWebGpuAvailable(out string? error))
        {
            throw new InvalidOperationException($"WebGPU execution provider unavailable: {error}");
        }
    }

    [Benchmark(Baseline = true, Description = "CPU EP")]
    public int Cpu() => InitModels(ExecutionProviderKind.Cpu);

    [Benchmark(Description = "WebGPU EP")]
    public int WebGpu() => InitModels(ExecutionProviderKind.WebGpu);

    private static int InitModels(ExecutionProviderKind kind)
    {
        using SessionOptions sessionOptions = ExecutionProviders.Create(kind);
        using var ocr = new RapidOcr();
        ocr.InitModels(BenchmarkAssets.PPOCRv6Small, sessionOptions);
        return ocr.GetHashCode();
    }
}
