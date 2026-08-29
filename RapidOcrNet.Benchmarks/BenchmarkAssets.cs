// Apache-2.0 license

using SkiaSharp;

namespace RapidOcrNet.Benchmarks;

/// <summary>
/// Resolves models and test images from the repository working tree.
/// </summary>
/// <remarks>
/// BenchmarkDotNet compiles and runs each benchmark from a <i>generated</i> project whose
/// output folder is not this project's output folder, so anything relying on
/// <c>CopyToOutputDirectory</c> plus a relative path (which is how
/// <see cref="RapidOcrModelSet.PPOCRv6Small"/> and friends are written) would not be found
/// at run time. Everything is therefore addressed by absolute path, anchored on the
/// solution file found by walking up from the assembly location.
/// </remarks>
internal static class BenchmarkAssets
{
    private const string SolutionFileName = "RapidOcrNet.sln";

    /// <summary>Root of the checkout, i.e. the folder holding <c>RapidOcrNet.sln</c>.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    private static string ModelsRoot => Path.Combine(RepoRoot, "RapidOcrNet", "models");

    private static string ImagesRoot => Path.Combine(RepoRoot, "RapidOcrNet.Tests", "images");

    /// <summary>
    /// The PP-OCRv6 <i>small</i> model set, re-pointed at absolute paths. Normalization
    /// constants (and the PP-OCRv5 classifier, which v6 has no replacement for) come
    /// straight from the library preset so the benchmark cannot drift from it.
    /// </summary>
    public static RapidOcrModelSet PPOCRv6Small { get; } = RapidOcrModelSet.PPOCRv6Small with
    {
        DetModelPath = Path.Combine(ModelsRoot, "v6", "PP-OCRv6_det_small.onnx"),
        ClsModelPath = Path.Combine(ModelsRoot, "v5", "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx"),
        RecModelPath = Path.Combine(ModelsRoot, "v6", "PP-OCRv6_rec_small.onnx"),
        KeysPath = Path.Combine(ModelsRoot, "v6", "ppocrv6_dict.txt")
    };

    /// <summary>
    /// Images the pipeline benchmarks run over. The names double as the <c>Image</c> column
    /// in the BenchmarkDotNet report, so they are kept short.
    /// </summary>
    /// <remarks>
    /// The three cover the shapes that matter for a CPU/GPU comparison:
    /// <list type="bullet">
    /// <item><description><c>en.jpg</c> (709x132) - detector-dominated. Note that source
    /// size is not detector cost: <see cref="RapidOcrOptions.PPOCRv6"/> resizes the
    /// <i>short</i> side up to <see cref="RapidOcrOptions.LimitSideLen"/>, so this strip is
    /// fed to the detector at ~3956x736 and is the heaviest detector input of the three.</description></item>
    /// <item><description><c>img_11.jpg</c> (1280x720) - a mid-size photo with few lines.</description></item>
    /// <item><description><c>2108.11480_1.png</c> (1224x1584) - a dense document page, ~106
    /// blocks, so recognizer-dominated. Its short side already exceeds the limit, so the
    /// detector sees it at native size.</description></item>
    /// </list>
    /// The recognizer matters disproportionately here because <see cref="TextRecognizer"/>
    /// runs one crop per inference rather than batching them.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> Images { get; } = new Dictionary<string, string>
    {
        ["en.jpg"] = Path.Combine(ImagesRoot, "en.jpg"),
        ["img_11.jpg"] = Path.Combine(ImagesRoot, "img_11.jpg"),
        ["2108.11480_1.png"] = Path.Combine(ImagesRoot, "2108.11480_1.png")
    };

    /// <summary>Decodes one of the <see cref="Images"/> entries by key.</summary>
    public static SKBitmap LoadImage(string key)
    {
        string path = Images[key];
        return SKBitmap.Decode(path)
               ?? throw new InvalidOperationException($"Could not decode image '{path}'.");
    }

    /// <summary>
    /// Throws with an actionable message if any model or image is missing, rather than
    /// letting ONNX Runtime fail deep inside a benchmark iteration.
    /// </summary>
    public static void EnsureAvailable()
    {
        var models = PPOCRv6Small;
        foreach (string path in new[] { models.DetModelPath, models.ClsModelPath, models.RecModelPath, models.KeysPath })
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Missing model file '{path}'. The PP-OCRv6 models are not in the NuGet package; " +
                    "see the README for where to download them into RapidOcrNet/models/v6.", path);
            }
        }

        foreach (string path in Images.Values)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Missing benchmark image '{path}'.", path);
            }
        }
    }

    private static string FindRepoRoot()
    {
        // AppContext.BaseDirectory points at whichever bin folder is executing - this
        // project's, or the nested one BenchmarkDotNet generates. Both sit under the repo.
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
                {
                    return dir.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate '{SolutionFileName}' above '{AppContext.BaseDirectory}' or " +
            $"'{Environment.CurrentDirectory}'. Run the benchmarks from inside the repository.");
    }
}
