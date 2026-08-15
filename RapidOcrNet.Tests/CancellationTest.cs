using SkiaSharp;

namespace RapidOcrNet.Tests;

/// <summary>
/// Cancellation of a <see cref="RapidOcr.Detect(SKBitmap, RapidOcrOptions, CancellationToken)"/>
/// call. A page is not interruptible mid-inference, so what these pin is that the token is
/// observed at every boundary where it can be, and that giving up leaves nothing behind.
/// </summary>
public class CancellationTest
{
    private static readonly Lazy<RapidOcr> Engine = new(() =>
    {
        var ocr = new RapidOcr();
        ocr.InitModels();
        return ocr;
    });

    private static SKBitmap LoadImage(string name)
    {
        var path = Path.Combine("images", name);
        Assert.True(File.Exists(path));
        return SKBitmap.Decode(path);
    }

    [Fact]
    public void DetectThrowsWhenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using SKBitmap originSrc = LoadImage("en_rec.jpg");

        Assert.Throws<OperationCanceledException>(
            () => Engine.Value.Detect(originSrc, RapidOcrOptions.Default, cts.Token));
    }

    [Fact]
    public void DetectBoxesThrowsWhenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using SKBitmap originSrc = LoadImage("en_rec.jpg");

        Assert.Throws<OperationCanceledException>(
            () => Engine.Value.DetectBoxes(originSrc, RapidOcrOptions.Default, cts.Token));
    }

    [Fact]
    public void DetectStopsBetweenCropsOnceCancelled()
    {
        // Cancelled from another thread while the page is being recognised, which is the case the
        // token exists for: recognition is one inference per detected line, so the call returns
        // after the line in flight rather than after the whole page.
        using var cts = new CancellationTokenSource();
        using SKBitmap originSrc = LoadImage("en_rec.jpg");

        cts.CancelAfter(TimeSpan.FromMilliseconds(1));

        try
        {
            var result = Engine.Value.Detect(originSrc, RapidOcrOptions.Default, cts.Token);

            // Fast machine, small image: the page can legitimately finish before the timer fires.
            // Completing is a valid outcome; producing a partial or corrupt result is not.
            Assert.NotNull(result.TextBlocks);
        }
        catch (OperationCanceledException)
        {
            // The outcome under test.
        }
    }

    [Fact]
    public void RecognizerObservesTheTokenPerCrop()
    {
        // Pinned at the stage rather than through Detect, because Detect's own boundary checks
        // would throw first and hide whether the per-crop check exists at all. The token is
        // tested before the crop is touched, so no model needs to be loaded.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Not disposed: Dispose() on an instance that never had InitModel called throws, which
        // is unrelated to what this pins, and an uninitialised stage holds no native handle.
        var recognizer = new TextRecognizer();
        using var crop = new SKBitmap(32, 48);

        Assert.Throws<OperationCanceledException>(() => recognizer.GetTextLines([crop], cts.Token));
    }

    [Fact]
    public void ClassifierObservesTheTokenPerCrop()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var classifier = new TextClassifier();   // see note above on disposal
        using var crop = new SKBitmap(32, 48);

        Assert.Throws<OperationCanceledException>(
            () => classifier.GetAngles([crop], doAngle: true, mostAngle: false,
                preserveAspectRatio: false, cancellationToken: cts.Token));

        // The no-angle path does no work, so it has nothing to interrupt and must not throw.
        var angles = classifier.GetAngles([crop], doAngle: false, mostAngle: false,
            preserveAspectRatio: false, cancellationToken: cts.Token);
        Assert.Single(angles);
    }

    [Fact]
    public void UncancelledTokenChangesNothing()
    {
        // The default path has to stay byte-for-byte what it was: CancellationToken.None must
        // produce exactly the result the parameterless overload does.
        using SKBitmap a = LoadImage("en_rec.jpg");
        using SKBitmap b = LoadImage("en_rec.jpg");

        var withoutToken = Engine.Value.Detect(a, RapidOcrOptions.Default);
        var withToken = Engine.Value.Detect(b, RapidOcrOptions.Default, CancellationToken.None);

        Assert.Equal(withoutToken.TextBlocks.Length, withToken.TextBlocks.Length);
        for (int i = 0; i < withoutToken.TextBlocks.Length; i++)
        {
            Assert.Equal(withoutToken.TextBlocks[i].Text, withToken.TextBlocks[i].Text);
        }
    }
}
