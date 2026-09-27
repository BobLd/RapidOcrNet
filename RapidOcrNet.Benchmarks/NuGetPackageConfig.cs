// Apache-2.0 license

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace RapidOcrNet.Benchmarks;

/// <summary>
/// Runs a benchmark class twice: once against this checkout (<c>ProjectReference</c>) and
/// once against a published NuGet version (<c>PackageReference</c>), by passing
/// <c>/p:RapidOcrNetVersion=&lt;value&gt;</c> through to each generated job's build - see the
/// conditional <c>ItemGroup</c>s in RapidOcrNet.Benchmarks.csproj. Mirrors the
/// Local/Latest setup in PdfPig.Rendering.Skia2's NuGetPackageConfig.
/// </summary>
/// <remarks>
/// The comparison version defaults to 4.1.0 - the last package published before the
/// ScoreToTextLine (GitHub issue #49) and SubtractMeanNormalize fixes - and can be overridden
/// with the <c>RAPIDOCR_BENCH_COMPARE_VERSION</c> environment variable, e.g. to check a
/// different historical release without editing this file.
/// </remarks>
internal class NuGetPackageConfig : ManualConfig
{
    public NuGetPackageConfig()
    {
        string compareVersion = Environment.GetEnvironmentVariable("RAPIDOCR_BENCH_COMPARE_VERSION") ?? "4.1.0";

        var baseJob = Job.Default;

        // The historical package is the baseline: Ratio > 1 for the other job means Local
        // regressed, Ratio < 1 means it improved - the direction issue #49 predicts.
        var nugetJob = baseJob
            .WithMsBuildArguments($"/p:RapidOcrNetVersion={compareVersion}")
            .WithId(compareVersion)
            .AsBaseline();

        var localJob = baseJob
            .WithMsBuildArguments("/p:RapidOcrNetVersion=Local")
            .WithId("Local");

        AddJob(nugetJob);
        AddJob(localJob);
    }
}
