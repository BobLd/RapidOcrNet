using System.Security.Cryptography;
using SkiaSharp;

namespace RapidOcrNet.Tests;

/// <summary>
/// Loading models that never exist as a file: the bytes are handed over directly, as a model read
/// from an embedded resource, decrypted from an encrypted blob or decompressed in memory would be.
/// The route has to land on the same engine as loading by path.
/// </summary>
public class ModelBytesTest
{
    private const string Image = "images/en_rec.jpg";

    [Fact]
    public void BytesRecogniseExactlyAsPathsDo()
    {
        var models = RapidOcrModelSet.PPOCRv5Latin;

        using var ocr = new RapidOcr();
        ocr.InitModels(Read(models.DetModelPath), Read(models.ClsModelPath),
            Read(models.RecModelPath), Read(models.KeysPath));

        AssertSameAsPaths(ocr);
    }

    /// <summary>
    /// The shape of an encrypted-model load: decrypt into buffers, hand the plaintext over, then
    /// wipe it so it does not sit on the heap for the life of the process. ONNX Runtime parses and
    /// copies each model as its session is built, so wiping the arrays afterwards has to leave the
    /// engine working.
    /// </summary>
    [Fact]
    public void ModelBytesCanBeWipedRightAfterLoading()
    {
        var models = RapidOcrModelSet.PPOCRv5Latin;

        byte[] det = Read(models.DetModelPath);
        byte[] cls = Read(models.ClsModelPath);
        byte[] rec = Read(models.RecModelPath);
        byte[] keys = Read(models.KeysPath);

        using var ocr = new RapidOcr();
        ocr.InitModels(det, cls, rec, keys);

        CryptographicOperations.ZeroMemory(det);
        CryptographicOperations.ZeroMemory(cls);
        CryptographicOperations.ZeroMemory(rec);
        CryptographicOperations.ZeroMemory(keys);

        using var bmp = SKBitmap.Decode(Resolve(Image));
        Assert.NotEmpty(ocr.Detect(bmp, RapidOcrOptions.Default).TextBlocks);
    }

    [V6Fact(V6Size.Small)]
    public void V6ModelsLoadFromBytesWithTheirOwnNormalization()
    {
        var models = RapidOcrModelSet.PPOCRv6Small;

        using var ocr = new RapidOcr();
        ocr.InitModels(new RapidOcrModelByteSet
        {
            DetModelBytes = Read(models.DetModelPath),
            ClsModelBytes = Read(models.ClsModelPath),
            RecModelBytes = Read(models.RecModelPath),
            KeysBytes = Read(models.KeysPath),
            DetMean = models.DetMean,
            DetStd = models.DetStd,
        });

        using var bmp = SKBitmap.Decode(Resolve(Image));
        OcrResult result = ocr.Detect(bmp, RapidOcrOptions.PPOCRv6);

        // The v5 normalization leaves a v6 detector finding nothing at all, so a non-empty result
        // is what says the set's DetMean/DetStd reached it.
        Assert.NotEmpty(result.TextBlocks);
    }

    [Fact]
    public void NullModelBytesAreRejectedByArgumentName()
    {
        // No models needed: the check happens before anything is read.
        using var ocr = new RapidOcr();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            ocr.InitModels(null!, [], [], []));

        Assert.Equal("detModel", ex.ParamName);
    }

    /// <summary>
    /// Recognizes the sample image with <paramref name="fromBytes"/> and with an engine whose models
    /// were loaded by path, and requires the two texts to be identical.
    /// </summary>
    private static void AssertSameAsPaths(RapidOcr fromBytes)
    {
        var models = RapidOcrModelSet.PPOCRv5Latin;

        using var fromPaths = new RapidOcr();
        fromPaths.InitModels(Resolve(models.DetModelPath), Resolve(models.ClsModelPath),
            Resolve(models.RecModelPath), Resolve(models.KeysPath));

        using var bmp = SKBitmap.Decode(Resolve(Image));

        string expected = fromPaths.Detect(bmp, RapidOcrOptions.Default).StrRes;
        string actual = fromBytes.Detect(bmp, RapidOcrOptions.Default).StrRes;

        Assert.False(string.IsNullOrWhiteSpace(expected));
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Model paths are relative to the output directory, which is not guaranteed to be the current
    /// directory at run time; images resolve relative to the test sources by convention.
    /// </summary>
    private static string Resolve(string relativePath)
    {
        if (File.Exists(relativePath))
        {
            return relativePath;
        }

        string fromBaseDirectory = Path.Combine(AppContext.BaseDirectory, relativePath);
        Assert.True(File.Exists(fromBaseDirectory),
            $"Test asset is missing: neither '{relativePath}' nor '{fromBaseDirectory}' exists.");

        return fromBaseDirectory;
    }

    private static byte[] Read(string relativePath) => File.ReadAllBytes(Resolve(relativePath));
}
