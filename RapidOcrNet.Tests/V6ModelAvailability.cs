namespace RapidOcrNet.Tests;

/// <summary>Which PP-OCRv6 model set a test needs on disk to run.</summary>
public enum V6Size
{
    Tiny,
    Small,
    Medium
}

/// <summary>
/// Whether a PP-OCRv6 model set is present. Only the tiny and small sets are committed; the
/// medium detector and recognizer are around 176MB together and are left out of the repository
/// and the NuGet alike, so a clean checkout genuinely cannot run the medium tests. Absent models
/// are a missing prerequisite rather than a defect, so the tests that need them report as skipped.
/// </summary>
internal static class V6Models
{
    public static RapidOcrModelSet For(V6Size size) => size switch
    {
        V6Size.Tiny => RapidOcrModelSet.PPOCRv6Tiny,
        V6Size.Small => RapidOcrModelSet.PPOCRv6Small,
        V6Size.Medium => RapidOcrModelSet.PPOCRv6Medium,
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown PP-OCRv6 model size.")
    };

    /// <summary>
    /// Null when every file the set needs is present, otherwise the reason to skip. Model paths
    /// are relative to the output directory, and this runs at discovery time where the current
    /// directory is not guaranteed to be that, so the assembly's own location is tried as well.
    /// </summary>
    public static string? SkipReason(V6Size size)
    {
        var models = For(size);

        foreach (string path in new[] { models.DetModelPath, models.ClsModelPath, models.RecModelPath, models.KeysPath })
        {
            if (!File.Exists(path) && !File.Exists(Path.Combine(AppContext.BaseDirectory, path)))
            {
                return $"PP-OCRv6 {size} models are not available: '{path}' is missing. " +
                       "Supply the file to run this test.";
            }
        }

        return null;
    }
}

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself when the PP-OCRv6 models it names are not on
/// disk. The condition is evaluated at discovery, which is the only point xUnit v2 offers —
/// <c>Assert.Skip</c> arrived in v3, and this project is on 2.9.
/// </summary>
internal sealed class V6FactAttribute : FactAttribute
{
    public V6FactAttribute(V6Size size)
    {
        Skip = V6Models.SkipReason(size);
    }
}

/// <summary>
/// <see cref="TheoryAttribute"/> counterpart of <see cref="V6FactAttribute"/>. Skips the whole
/// theory, every row of it: xUnit v2 has no way to skip an individual <c>MemberData</c> row, so a
/// theory whose rows differ in what they need has to be split into one test per model set.
/// </summary>
internal sealed class V6TheoryAttribute : TheoryAttribute
{
    public V6TheoryAttribute(V6Size size)
    {
        Skip = V6Models.SkipReason(size);
    }
}
