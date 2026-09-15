using System.Security.Cryptography;
using SkiaSharp;

namespace RapidOcrNet.Tests;

/// <summary>
/// Getting models into the engine when they do not come from a file: from a stream (an embedded
/// resource, a zip entry, a network response) or from bytes already in hand (a model decrypted or
/// decompressed in memory). Every route has to land on the same engine — the point of the overloads
/// is to let a caller use whichever form the model arrives in, not to get different recognition out.
/// </summary>
public class ModelLoadingTest
{
    private const string Image = "images/en_rec.jpg";

    [Fact]
    public void StreamsRecogniseExactlyAsPathsDo()
    {
        var models = RapidOcrModelSet.PPOCRv5Latin;

        using var ocr = new RapidOcr();
        using (var det = Open(models.DetModelPath))
        using (var cls = Open(models.ClsModelPath))
        using (var rec = Open(models.RecModelPath))
        using (var keys = Open(models.KeysPath))
        {
            ocr.InitModels(det, cls, rec, keys);
        }

        AssertSameAsPaths(ocr);
    }

    [Fact]
    public void BytesRecogniseExactlyAsPathsDo()
    {
        var models = RapidOcrModelSet.PPOCRv5Latin;

        using var ocr = new RapidOcr();
        ocr.InitModels(Read(models.DetModelPath), Read(models.ClsModelPath),
            Read(models.RecModelPath), Read(models.KeysPath));

        AssertSameAsPaths(ocr);
    }

    [Fact]
    public void StreamsAreLeftOpenAndCanBeRewound()
    {
        var models = RapidOcrModelSet.PPOCRv5Latin;

        using var det = Open(models.DetModelPath);
        using var cls = Open(models.ClsModelPath);
        using var rec = Open(models.RecModelPath);
        using var keys = Open(models.KeysPath);

        using (var first = new RapidOcr())
        {
            first.InitModels(det, cls, rec, keys);
        }

        // Loading does not take ownership: the streams are still open, and because they are
        // seekable they can be rewound and handed over a second time.
        det.Position = 0;
        cls.Position = 0;
        rec.Position = 0;
        keys.Position = 0;

        using var second = new RapidOcr();
        second.InitModels(det, cls, rec, keys);

        using var bmp = SKBitmap.Decode(Resolve(Image));
        Assert.NotEmpty(second.Detect(bmp, RapidOcrOptions.Default).TextBlocks);
    }

    [Fact]
    public void NonSeekableStreamsLoadToo()
    {
        // A stream with no length to size from takes the other read path (grow a buffer, then hand
        // back what it holds). A network response is the realistic shape of one, so a wrapper over
        // a file that hides its length stands in for it.
        var models = RapidOcrModelSet.PPOCRv5Latin;

        using var ocr = new RapidOcr();
        using (var det = new NonSeekableStream(Open(models.DetModelPath)))
        using (var cls = new NonSeekableStream(Open(models.ClsModelPath)))
        using (var rec = new NonSeekableStream(Open(models.RecModelPath)))
        using (var keys = new NonSeekableStream(Open(models.KeysPath)))
        {
            ocr.InitModels(det, cls, rec, keys);
        }

        using var bmp = SKBitmap.Decode(Resolve(Image));
        Assert.NotEmpty(ocr.Detect(bmp, RapidOcrOptions.Default).TextBlocks);
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
    public void V6ModelsLoadFromStreamsWithTheirOwnNormalization()
    {
        var models = RapidOcrModelSet.PPOCRv6Small;

        using var det = Open(models.DetModelPath);
        using var cls = Open(models.ClsModelPath);
        using var rec = Open(models.RecModelPath);
        using var keys = Open(models.KeysPath);

        using var ocr = new RapidOcr();
        ocr.InitModels(new RapidOcrModelStreamSet
        {
            DetModelStream = det,
            ClsModelStream = cls,
            RecModelStream = rec,
            KeysStream = keys,
            DetMean = models.DetMean,
            DetStd = models.DetStd,
        });

        AssertV6Detects(ocr);
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

        AssertV6Detects(ocr);
    }

    [Fact]
    public void NullModelStreamIsRejectedByArgumentName()
    {
        // No models needed: the check happens before anything is opened or read.
        using var ocr = new RapidOcr();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            ocr.InitModels(null!, Stream.Null, Stream.Null, Stream.Null));

        Assert.Equal("detStream", ex.ParamName);
    }

    [Fact]
    public void NullModelBytesAreRejectedByArgumentName()
    {
        using var ocr = new RapidOcr();

        var ex = Assert.Throws<ArgumentNullException>(() =>
            ocr.InitModels(null!, [], [], []));

        Assert.Equal("detModel", ex.ParamName);
    }

    /// <summary>
    /// Recognizes the sample image with <paramref name="inMemory"/> and with an engine whose models
    /// were loaded by path, and requires the two texts to be identical.
    /// </summary>
    private static void AssertSameAsPaths(RapidOcr inMemory)
    {
        var models = RapidOcrModelSet.PPOCRv5Latin;

        using var fromPaths = new RapidOcr();
        fromPaths.InitModels(Resolve(models.DetModelPath), Resolve(models.ClsModelPath),
            Resolve(models.RecModelPath), Resolve(models.KeysPath));

        using var bmp = SKBitmap.Decode(Resolve(Image));

        string expected = fromPaths.Detect(bmp, RapidOcrOptions.Default).StrRes;
        string actual = inMemory.Detect(bmp, RapidOcrOptions.Default).StrRes;

        Assert.False(string.IsNullOrWhiteSpace(expected));
        Assert.Equal(expected, actual);
    }

    private static void AssertV6Detects(RapidOcr ocr)
    {
        using var bmp = SKBitmap.Decode(Resolve(Image));
        OcrResult result = ocr.Detect(bmp, RapidOcrOptions.PPOCRv6);

        // The v5 normalization leaves a v6 detector finding nothing at all, so a non-empty result
        // is what says the set's DetMean/DetStd reached it.
        Assert.NotEmpty(result.TextBlocks);
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

    private static FileStream Open(string relativePath) => File.OpenRead(Resolve(relativePath));

    /// <summary>
    /// A readable stream that refuses to say how long it is, which is what a model arriving over
    /// the wire looks like to the loading code.
    /// </summary>
    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => inner.Read(buffer);

        public override void Flush() => inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
