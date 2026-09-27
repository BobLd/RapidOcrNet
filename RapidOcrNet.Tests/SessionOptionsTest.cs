using Microsoft.ML.OnnxRuntime;
using SkiaSharp;

namespace RapidOcrNet.Tests;

/// <summary>
/// Covers <see cref="RapidOcrSessionOptions"/> — loading the detector, classifier and recognizer
/// each with its own <see cref="SessionOptions"/> (issue #51).
/// </summary>
/// <remarks>
/// The tests run on CPU, so they cannot show a provider actually being picked per model. What
/// they pin is the part that can go wrong on any machine: a pipeline built from mixed explicit and
/// defaulted slots reads the same as the single-options one, and ownership is right — the caller's
/// options survive <c>InitModels</c>, and the defaults we build for null slots are not leaked into
/// the caller's hands.
/// </remarks>
public class SessionOptionsTest
{
    private static readonly Lazy<RapidOcr> Reference = new(() =>
    {
        var ocr = new RapidOcr();
        ocr.InitModels();
        return ocr;
    });

    [Fact]
    public void MixedSlotsMatchSingleOptions()
    {
        using var cls = new SessionOptions();
        using var rec = RapidOcr.GetDefaultSessionOptions();

        // Det left null on purpose, so one model goes through the defaulting path.
        using var ocr = new RapidOcr();
        ocr.InitModels(RapidOcrModelSet.PPOCRv5Latin, new RapidOcrSessionOptions { Cls = cls, Rec = rec });

        using SKBitmap originSrc = SKBitmap.Decode(Path.Combine("images", "en_rec.jpg"));

        var options = RapidOcrOptions.Default with { DoAngle = true };
        OcrResult expected = Reference.Value.Detect(originSrc, options);
        OcrResult actual = ocr.Detect(originSrc, options);

        Assert.NotEmpty(expected.TextBlocks);
        Assert.Equal(expected.StrRes, actual.StrRes);
    }

    [Fact]
    public void CallerOptionsAreNotDisposed()
    {
        using var shared = RapidOcr.GetDefaultSessionOptions();

        using (var ocr = new RapidOcr())
        {
            ocr.InitModels(new RapidOcrSessionOptions { Det = shared, Cls = shared, Rec = shared });
        }

        // Still ours after the load and after the engine is gone; a second load proves it is
        // usable, not merely unclosed.
        Assert.False(shared.IsClosed);

        using var again = new RapidOcr();
        again.InitModels(RapidOcrModelSet.PPOCRv5Latin, shared);
    }

    [Fact]
    public void AllSlotsNullUsesDefaults()
    {
        using var ocr = new RapidOcr();
        ocr.InitModels(new RapidOcrSessionOptions { NumThread = 1 });

        using SKBitmap originSrc = SKBitmap.Decode(Path.Combine("images", "en_rec.jpg"));

        OcrResult expected = Reference.Value.Detect(originSrc, RapidOcrOptions.Default);
        OcrResult actual = ocr.Detect(originSrc, RapidOcrOptions.Default);

        Assert.Equal(expected.StrRes, actual.StrRes);
    }

    [Fact]
    public void NullArgumentsThrow()
    {
        using var ocr = new RapidOcr();

        Assert.Throws<ArgumentNullException>(() => ocr.InitModels((RapidOcrSessionOptions)null!));
        Assert.Throws<ArgumentNullException>(() =>
            ocr.InitModels(RapidOcrModelSet.PPOCRv5Latin, (RapidOcrSessionOptions)null!));

        // The single-options overloads never meant "defaults" for null; routing them through the
        // per-model path must not quietly change that.
        Assert.Throws<ArgumentNullException>(() => ocr.InitModels((SessionOptions)null!));
        Assert.Throws<ArgumentNullException>(() =>
            ocr.InitModels(RapidOcrModelSet.PPOCRv5Latin, (SessionOptions)null!));
    }
}
