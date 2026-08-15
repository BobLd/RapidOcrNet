using SkiaSharp;

namespace RapidOcrNet.Tests;

/// <summary>
/// The awaitable surface added for issue #27. These pin that awaiting produces exactly what the
/// synchronous call produces, and that the token and progress callback survive the hop onto the
/// thread pool. What they deliberately do not pin is any throughput claim: OCR here is CPU-bound
/// end to end, so <c>DetectAsync</c> moves the work to a pool thread rather than freeing one.
/// </summary>
public class AsyncTest
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
    public async Task DetectAsyncMatchesDetect()
    {
        using SKBitmap a = LoadImage("en_rec.jpg");
        using SKBitmap b = LoadImage("en_rec.jpg");

        var sync = Engine.Value.Detect(a, RapidOcrOptions.Default);
        var async = await Engine.Value.DetectAsync(b, RapidOcrOptions.Default);

        Assert.Equal(sync.TextBlocks.Length, async.TextBlocks.Length);
        for (int i = 0; i < sync.TextBlocks.Length; i++)
        {
            Assert.Equal(sync.TextBlocks[i].Text, async.TextBlocks[i].Text);
        }
    }

    [Fact]
    public async Task DetectAsyncFromPathMatchesDetectFromPath()
    {
        string path = Path.Combine("images", "en_rec.jpg");

        var sync = Engine.Value.Detect(path, RapidOcrOptions.Default);
        var async = await Engine.Value.DetectAsync(path, RapidOcrOptions.Default);

        Assert.Equal(sync.StrRes, async.StrRes);
    }

    [Fact]
    public async Task DetectBoxesAsyncMatchesDetectBoxes()
    {
        using SKBitmap a = LoadImage("en_rec.jpg");
        using SKBitmap b = LoadImage("en_rec.jpg");

        var sync = Engine.Value.DetectBoxes(a, RapidOcrOptions.Default);
        var async = await Engine.Value.DetectBoxesAsync(b, RapidOcrOptions.Default);

        Assert.Equal(sync.Count, async.Count);
        for (int i = 0; i < sync.Count; i++)
        {
            Assert.Equal(sync[i].BoxPoints, async[i].BoxPoints);
        }
    }

    [Fact]
    public async Task DetectBoxesAsyncFromPathMatchesDetectBoxesFromPath()
    {
        string path = Path.Combine("images", "en_rec.jpg");

        var sync = Engine.Value.DetectBoxes(path, RapidOcrOptions.Default);
        var async = await Engine.Value.DetectBoxesAsync(path, RapidOcrOptions.Default);

        Assert.Equal(sync.Count, async.Count);
    }

    [Fact]
    public async Task DetectAsyncThrowsWhenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using SKBitmap originSrc = LoadImage("en_rec.jpg");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Engine.Value.DetectAsync(originSrc, RapidOcrOptions.Default, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task DetectBoxesAsyncThrowsWhenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using SKBitmap originSrc = LoadImage("en_rec.jpg");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Engine.Value.DetectBoxesAsync(originSrc, RapidOcrOptions.Default, cts.Token));
    }

    [Fact]
    public async Task DetectAsyncFromMissingPathThrowsFileNotFound()
    {
        // The path overload validates before any work is scheduled; awaiting must surface that
        // as a faulted task rather than swallowing it or throwing synchronously out of the call.
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => Engine.Value.DetectAsync("images/does-not-exist.png", RapidOcrOptions.Default));
    }

    [Fact]
    public async Task DetectAsyncReportsProgress()
    {
        var reports = new List<(int Completed, int Total)>();
        using SKBitmap originSrc = LoadImage("en_rec.jpg");

        var result = await Engine.Value.DetectAsync(originSrc, RapidOcrOptions.Default,
            new SyncProgress<(int Completed, int Total)>(r => { lock (reports) { reports.Add(r); } }));

        Assert.NotEmpty(reports);
        Assert.Equal(reports[0].Total, reports.Count);
        Assert.True(reports[0].Total >= result.TextBlocks.Length);
    }

    /// <summary>
    /// Invokes the handler inline, so reports are observed on the thread that produced them
    /// rather than posted elsewhere and possibly missed. Same reasoning as in CancellationTest.
    /// </summary>
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
