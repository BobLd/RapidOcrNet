using SkiaSharp;

namespace RapidOcrNet.Tests;

/// <summary>
/// Covers <see cref="RapidOcrOptions.RecMaxDegreeOfParallelism"/> — recognizing several crops
/// concurrently.
/// </summary>
/// <remarks>
/// The property that makes this approach worth having is that it is <i>output-neutral</i>:
/// every crop is preprocessed and run exactly as on the serial path, so concurrency can only
/// change timing. These tests pin that, rather than settling for a smoke test — an
/// implementation that raced on a shared buffer, or scattered results by completion order
/// instead of index, would still produce plausible-looking text.
/// </remarks>
public class RecParallelTest
{
    private static readonly Lazy<RapidOcr> V6SmallEngine = new(() =>
    {
        var ocr = new RapidOcr();
        ocr.InitModels(RapidOcrModelSet.PPOCRv6Small);
        return ocr;
    });

    /// <summary>The default must stay on the serial path.</summary>
    [Fact]
    public void DefaultOptionsRunSerially()
    {
        Assert.Equal(1, RapidOcrOptions.Default.RecMaxDegreeOfParallelism);
        Assert.Equal(1, RapidOcrOptions.PythonCompat.RecMaxDegreeOfParallelism);
        Assert.Equal(1, RapidOcrOptions.PPOCRv6.RecMaxDegreeOfParallelism);
        Assert.Equal(1, new RapidOcrOptions().RecMaxDegreeOfParallelism);
    }

    /// <summary>
    /// The whole claim, on the page with the most crops to race: text, per-character scores and
    /// the word polygons derived from CTC columns must all come back identical. Scores are
    /// compared exactly, not approximately — the same input through the same session has no
    /// reason to drift, and a tolerance here would hide exactly the bug worth catching.
    /// </summary>
    [V6Theory(V6Size.Small)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(-1)]
    public void ParallelRecognitionIsOutputNeutral(int dop)
    {
        string path = Path.Combine("images", "2108.11480_1.png");
        Assert.True(File.Exists(path));

        using SKBitmap originSrc = SKBitmap.Decode(path);

        var serialOptions = RapidOcrOptions.PPOCRv6 with { ReturnWordBox = true };

        OcrResult expected = V6SmallEngine.Value.Detect(originSrc, serialOptions);
        OcrResult actual = V6SmallEngine.Value.Detect(originSrc,
            serialOptions with { RecMaxDegreeOfParallelism = dop });

        Assert.Equal(expected.TextBlocks.Length, actual.TextBlocks.Length);
        Assert.NotEmpty(expected.TextBlocks);

        for (int i = 0; i < expected.TextBlocks.Length; i++)
        {
            TextBlock want = expected.TextBlocks[i];
            TextBlock got = actual.TextBlocks[i];

            Assert.Equal(want.Text, got.Text);
            Assert.Equal(want.BoxPoints, got.BoxPoints);
            Assert.Equal(want.CharScores, got.CharScores);
            Assert.Equal(want.Chars, got.Chars);

            if (want.WordResults is null)
            {
                Assert.Null(got.WordResults);
                continue;
            }

            Assert.NotNull(got.WordResults);
            Assert.Equal(want.WordResults.Length, got.WordResults.Length);

            for (int w = 0; w < want.WordResults.Length; w++)
            {
                Assert.Equal(want.WordResults[w].Text, got.WordResults[w].Text);
                Assert.Equal(want.WordResults[w].BoxPoints, got.WordResults[w].BoxPoints);
            }
        }
    }

    /// <summary>
    /// Results are scattered into their own index, so detection order has to survive whatever
    /// order the crops actually finish in. A completion-ordered implementation would pass a
    /// text-only check on a page whose lines happen to be similar, so this pins text against
    /// box together.
    /// </summary>
    [V6Fact(V6Size.Small)]
    public void ParallelRecognitionPreservesDetectionOrder()
    {
        string path = Path.Combine("images", "img_12.jpg");
        Assert.True(File.Exists(path));

        using SKBitmap originSrc = SKBitmap.Decode(path);

        OcrResult serial = V6SmallEngine.Value.Detect(originSrc, RapidOcrOptions.PPOCRv6);
        OcrResult parallel = V6SmallEngine.Value.Detect(originSrc,
            RapidOcrOptions.PPOCRv6 with { RecMaxDegreeOfParallelism = 8 });

        Assert.NotEmpty(serial.TextBlocks);
        Assert.Equal(serial.TextBlocks.Length, parallel.TextBlocks.Length);

        for (int i = 0; i < serial.TextBlocks.Length; i++)
        {
            Assert.Equal(serial.TextBlocks[i].BoxPoints, parallel.TextBlocks[i].BoxPoints);
            Assert.Equal(serial.TextBlocks[i].Text, parallel.TextBlocks[i].Text);
        }
    }

    /// <summary>
    /// Values <see cref="ParallelOptions"/> would reject are refused at the caller rather than
    /// coerced into something it accepts. Silently substituting a degree nobody asked for would
    /// bury the miscomputation that produced it — <c>ProcessorCount - 1</c> on a single core is
    /// 0, and <c>ProcessorCount - 4</c> on a two-core container is -2, which is not "serial" but
    /// the sentinel next to unbounded. Both surfaces reject them: the option, and the recognizer
    /// overload a caller bypassing <see cref="RapidOcrOptions"/> reaches directly.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(int.MinValue)]
    public void OutOfRangeParallelismThrows(int dop)
    {
        var fromOptions = Assert.Throws<ArgumentOutOfRangeException>(
            () => RapidOcrOptions.PPOCRv6 with { RecMaxDegreeOfParallelism = dop });
        Assert.Equal(nameof(RapidOcrOptions.RecMaxDegreeOfParallelism), fromOptions.ParamName);

        // Validated before any crop is touched, so an uninitialised recognizer is enough to
        // prove the value never reaches Parallel.For.
        using var recognizer = new TextRecognizer();
        using var first = new SKBitmap(32, 48);
        using var second = new SKBitmap(64, 48);

        var fromRecognizer = Assert.Throws<ArgumentOutOfRangeException>(
            () => recognizer.GetTextLines([first, second], dop));
        Assert.Equal("maxDegreeOfParallelism", fromRecognizer.ParamName);
    }

    /// <summary>
    /// Progress must account for every crop exactly once <i>and</i> arrive in ascending order,
    /// even though the reports now come from several threads. The counter behind it is shared
    /// mutable state, which is the one thing concurrency here can genuinely corrupt — and an
    /// atomic counter alone would not be enough, since a worker can take a ticket, be
    /// pre-empted, and report it after a later one has already gone out.
    /// </summary>
    /// <remarks>
    /// The exactly-once half of this has teeth; the ordering half is opportunistic. Measured
    /// against a build that counted atomically but reported outside the critical section, this
    /// passed 10 runs out of 10: the window between taking the ticket and reporting it is a few
    /// nanoseconds against milliseconds of inference, so the interleaving that breaks ordering
    /// is not reachable from an end-to-end test. It is asserted because it is the contract, not
    /// because failing it is likely — the guarantee itself rests on the critical section in
    /// <see cref="TextRecognizer.GetTextLines(SKBitmap[], int, IProgress{ValueTuple{int, int}}, CancellationToken)"/>.
    /// </remarks>
    [V6Fact(V6Size.Small)]
    public void ParallelProgressCountsEveryCropOnceInOrder()
    {
        string path = Path.Combine("images", "2108.11480_1.png");
        Assert.True(File.Exists(path));

        using SKBitmap originSrc = SKBitmap.Decode(path);

        // A queue, not a bag: the assertion below is about the order the reports arrived in, so
        // the collection recording them has to preserve it.
        var reports = new System.Collections.Concurrent.ConcurrentQueue<(int Completed, int Total)>();
        var progress = new SynchronousProgress<(int Completed, int Total)>(reports.Enqueue);

        OcrResult result = V6SmallEngine.Value.Detect(originSrc,
            RapidOcrOptions.PPOCRv6 with { RecMaxDegreeOfParallelism = 8 }, progress);

        Assert.NotEmpty(result.TextBlocks);

        int total = reports.First().Total;
        Assert.All(reports, r => Assert.Equal(total, r.Total));

        // One report per crop, and as reported they are exactly 1, 2, ... total: no duplicate or
        // gap (which a lost update on the shared counter would cause) and no step backwards
        // (which reporting outside the counter's critical section would).
        int[] completed = reports.Select(r => r.Completed).ToArray();
        Assert.Equal(total, completed.Length);
        Assert.Equal(Enumerable.Range(1, total), completed);
    }

    /// <summary>
    /// Cancelling must still surface as <see cref="OperationCanceledException"/> from the
    /// parallel path, not as the <see cref="AggregateException"/> a naive
    /// <see cref="Parallel.For(int, int, Action{int})"/> would let escape.
    /// </summary>
    [Fact]
    public void ParallelRecognitionSurfacesCancellationDirectly()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Cancellation is observed before any crop is touched, so no model needs loading — and
        // disposing an instance that never had InitModel called is fine since eb95f49.
        using var recognizer = new TextRecognizer();
        using var first = new SKBitmap(32, 48);
        using var second = new SKBitmap(64, 48);

        Assert.Throws<OperationCanceledException>(
            () => recognizer.GetTextLines([first, second], 4, null, cts.Token));
    }

    /// <summary>
    /// A degree of parallelism is a performance knob, so it must not change what a caller has to
    /// catch. A progress handler that throws is the one failure a caller can inject without a
    /// broken model, and it has to surface as itself rather than inside the
    /// <see cref="AggregateException"/> <see cref="Parallel.For(int, int, Action{int})"/> raises
    /// — a caller who wrote <c>catch (InvalidOperationException)</c> around a serial
    /// <see cref="RapidOcr.Detect(SKBitmap, RapidOcrOptions, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
    /// must not stop catching it when they raise the degree.
    /// </summary>
    [V6Fact(V6Size.Small)]
    public void ParallelRecognitionDoesNotWrapExceptions()
    {
        string path = Path.Combine("images", "img_12.jpg");
        Assert.True(File.Exists(path));

        using SKBitmap originSrc = SKBitmap.Decode(path);

        var progress = new SynchronousProgress<(int Completed, int Total)>(
            _ => throw new InvalidOperationException("thrown by the progress handler"));

        var ex = Assert.Throws<InvalidOperationException>(() => V6SmallEngine.Value.Detect(
            originSrc, RapidOcrOptions.PPOCRv6 with { RecMaxDegreeOfParallelism = 8 }, progress));

        Assert.Equal("thrown by the progress handler", ex.Message);
    }

    /// <summary>
    /// <see cref="Progress{T}"/> posts to a synchronization context, which a test has none of,
    /// so its callbacks would arrive on the thread pool after the assertions had run. This
    /// invokes the handler inline instead, on whichever worker reported.
    /// </summary>
    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
