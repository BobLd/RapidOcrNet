// Apache-2.0 license

using BenchmarkDotNet.Attributes;
using Microsoft.ML.OnnxRuntime;
using SkiaSharp;

namespace RapidOcrNet.Benchmarks;

/// <summary>
/// This checkout (<c>Local</c>) versus the last published NuGet release before the
/// <see cref="TextRecognizer"/>/<see cref="OcrUtils"/> allocation fixes (GitHub issue #49 and
/// its <c>SubtractMeanNormalize</c> follow-up) - see <see cref="NuGetPackageConfig"/> for how
/// the two builds are produced. Only the public <see cref="RapidOcr.Detect(SKBitmap,
/// RapidOcrOptions, CancellationToken)"/> API is used, so this class compiles unchanged
/// against either reference.
/// </summary>
/// <remarks>
/// Run with <c>dotnet run -c Release -- --filter "*PackageVersionBenchmarks*"</c>. CPU
/// execution provider only - the point here is the RapidOcrNet version, not the execution
/// provider (see <see cref="OcrPipelineBenchmarks"/> for that axis).
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(NuGetPackageConfig))]
public class PackageVersionBenchmarks
{
    [ParamsSource(nameof(ImageNames))]
    public string Image { get; set; } = string.Empty;

    public static IEnumerable<string> ImageNames() => BenchmarkAssets.Images.Keys;

    private SKBitmap? _bitmap;
    private RapidOcr? _ocr;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkAssets.EnsureAvailable();
        _bitmap = BenchmarkAssets.LoadImage(Image);

        var ocr = new RapidOcr();
        using (SessionOptions sessionOptions = ExecutionProviders.Create(ExecutionProviderKind.Cpu))
        {
            ocr.InitModels(BenchmarkAssets.PPOCRv6Small, sessionOptions);
        }

        _ocr = ocr;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _ocr?.Dispose();
        _bitmap?.Dispose();
    }

    [Benchmark]
    public int Detect() => _ocr!.Detect(_bitmap!, RapidOcrOptions.PPOCRv6).TextBlocks.Length;
}
