// Apache-2.0 license

using BenchmarkDotNet.Running;
using Microsoft.ML.OnnxRuntime;
using RapidOcrNet;
using RapidOcrNet.Benchmarks;
using SkiaSharp;

// "verify" is a cheap pre-flight: it proves the WebGPU EP actually loads, shows which
// adapter was picked, and checks the two providers agree on the text before anyone spends
// ten minutes measuring them. Everything else goes to BenchmarkDotNet.
if (args.Length > 0 && args[0].Equals("verify", StringComparison.OrdinalIgnoreCase))
{
    return Verify();
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return 0;

static int Verify()
{
    BenchmarkAssets.EnsureAvailable();
    Console.WriteLine($"Repository root : {BenchmarkAssets.RepoRoot}");
    Console.WriteLine("Model set       : PP-OCRv6 small");

    if (!ExecutionProviders.IsWebGpuAvailable(out string? error))
    {
        Console.Error.WriteLine($"WebGPU EP unavailable: {error}");
        Console.Error.WriteLine("The CPU arm can still be measured (--filter \"*.Cpu*\"), but the comparison cannot.");
        return 1;
    }

    string selected = ExecutionProviders.DescribeWebGpuDevice();
    Console.WriteLine("WebGPU adapters : (* = in use)");
    foreach (OrtEpDevice candidate in ExecutionProviders.WebGpuDevices())
    {
        string description = ExecutionProviders.Describe(candidate);
        Console.WriteLine($"  {(description == selected ? '*' : ' ')} {description}");
    }

    Console.WriteLine($"  select with {ExecutionProviders.AdapterEnvironmentVariable}=<substring>");
    Console.WriteLine();

    using RapidOcr cpu = CreateOcr(ExecutionProviderKind.Cpu);
    using RapidOcr webGpu = CreateOcr(ExecutionProviderKind.WebGpu);

    var options = RapidOcrOptions.PPOCRv6;
    int mismatches = 0;

    foreach (string image in BenchmarkAssets.Images.Keys)
    {
        using SKBitmap bitmap = BenchmarkAssets.LoadImage(image);

        // One untimed pass each: the first inference on a session pays lazy allocation and,
        // on WebGPU, shader compilation. The reported numbers come from the second pass.
        _ = cpu.Detect(bitmap, options);
        _ = webGpu.Detect(bitmap, options);

        OcrResult cpuResult = cpu.Detect(bitmap, options);
        OcrResult webGpuResult = webGpu.Detect(bitmap, options);

        Console.WriteLine($"{image}  ({bitmap.Width}x{bitmap.Height})");
        Report("  CPU   ", cpuResult);
        Report("  WebGPU", webGpuResult);

        bool same = string.Equals(cpuResult.StrRes, webGpuResult.StrRes, StringComparison.Ordinal);
        Console.WriteLine($"  text  : {(same ? "identical" : "DIFFERENT - a speed-up here would not be free")}");
        Console.WriteLine();

        if (!same)
        {
            mismatches++;
        }
    }

    if (mismatches > 0)
    {
        Console.Error.WriteLine($"{mismatches} image(s) produced different text between the two providers.");
        return 2;
    }

    return 0;
}

static void Report(string label, OcrResult result)
{
    float crnn = 0f;
    float angle = 0f;
    foreach (TextBlock block in result.TextBlocks)
    {
        crnn += block.CrnnTime;
        angle += block.AngleTime;
    }

    Console.WriteLine($"{label}: total {result.DetectTime,8:F1} ms | detector {result.DbNetTime,7:F1} ms | " +
                      $"cls {angle,7:F1} ms | rec {crnn,8:F1} ms | {result.TextBlocks.Length} blocks");
}

static RapidOcr CreateOcr(ExecutionProviderKind kind)
{
    var ocr = new RapidOcr();
    using SessionOptions sessionOptions = ExecutionProviders.Create(kind);
    ocr.InitModels(BenchmarkAssets.PPOCRv6Small, sessionOptions);
    return ocr;
}
