using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using SkiaSharp;

namespace RapidOcrNet.Tests;

/// <summary>
/// Cancellation of a
/// <see cref="RapidOcr.Detect(SKBitmap, RapidOcrOptions, IProgress{ValueTuple{int, int}}, CancellationToken)"/>
/// call. These pin that the token is observed at every boundary where it can be, that it is
/// observed inside the ONNX runs themselves, and that giving up leaves nothing behind.
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

    private const string TimingSkip =
        "Relies on a delay landing at a particular point inside the call, which is measured on " +
        "one machine and does not transfer to another. Remove the Skip to run it locally.";

    private const string ElapsedSkip =
        "Asserts a cancelled call returns before doing its work, using a wall-clock threshold " +
        "measured on one machine. Remove the Skip to run it locally.";

    [Fact]
    public void DetectThrowsWhenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using SKBitmap originSrc = LoadImage("en_rec.jpg");

        Assert.Throws<OperationCanceledException>(
            () => Engine.Value.Detect(originSrc, RapidOcrOptions.Default, cancellationToken: cts.Token));
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
            var result = Engine.Value.Detect(originSrc, RapidOcrOptions.Default, cancellationToken: cts.Token);

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

        Assert.Throws<OperationCanceledException>(
            () => recognizer.GetTextLines([crop], cancellationToken: cts.Token));
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

    /// <summary>
    /// Invokes the handler inline. <see cref="Progress{T}"/> posts to a synchronization context,
    /// so with none installed its reports land on the thread pool and a test could assert against
    /// a list that has not filled yet — passing whether or not anything was ever reported.
    /// </summary>
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    [Fact]
    public void ProgressIsReportedOncePerRecognisedLine()
    {
        var reports = new List<(int Completed, int Total)>();
        using SKBitmap originSrc = LoadImage("en_rec.jpg");

        var result = Engine.Value.Detect(originSrc, RapidOcrOptions.Default,
            new SyncProgress<(int Completed, int Total)>(reports.Add),
            CancellationToken.None);

        Assert.NotEmpty(reports);

        // One report per detected line, counting up, with a total that never moves.
        int total = reports[0].Total;
        Assert.Equal(total, reports.Count);
        Assert.Equal(Enumerable.Range(1, total), reports.Select(r => r.Completed));
        Assert.All(reports, r => Assert.Equal(total, r.Total));

        // Detected lines are what progress counts; blocks can be fewer, since recognition drops
        // those below the text-score threshold.
        Assert.True(total >= result.TextBlocks.Length);
    }

    [Fact(Skip = TimingSkip)]
    public async Task DetectBoxesCancelsInsideTheDetectorRun()
    {
        // The case that used to be impossible. Detection is a single ONNX run over the whole
        // page, and the token was read on the way in and then not again until the run had
        // returned. ORT polls RunOptions.Terminate once per kernel, so the run now gives up where
        // it stands.
        //
        // Run against the few-box image, where the run is most of the detector stage, and sweep
        // rather than aim at one point: the ORT failure carried inside the cancellation is proof
        // the delay reached the running inference, and one of these lands there.
        //
        // That carried failure is the second thing pinned here. OrtRun translates any ORT
        // exception into a cancellation whenever the token happens to be cancelled, so a genuine
        // model or shape error coinciding with a timeout would otherwise disappear without trace —
        // the stages' own `when (ex is not OperationCanceledException)` filters rethrow before
        // their diagnostic write. Keeping it as InnerException is what leaves it diagnosable.
        var engine = Engine.Value;
        using SKBitmap originSrc = LoadImage(FewBoxImage);

        long warmMs = MeasureWarm(engine, originSrc);

        var inners = await SweepCancellations(engine, originSrc,
            f => TimeSpan.FromMilliseconds(warmMs * f),
            [0.4, 0.5, 0.6, 0.7, 0.8, 0.9]);

        AssertReachedTerminatePath(inners, "Detection");
    }

    [Fact(Skip = TimingSkip)]
    public async Task DetectBoxesCancelsInsideTheDetectorRunOnASingleThreadedSession()
    {
        // The configuration that decides the whole design. ORT's own RunAsync refuses a session
        // whose intra-op pool has fewer than two threads, so had cancellation been built on it,
        // InitModels(numThread: 1) would have lost cancellation altogether — silently, since
        // nothing else about the call changes. RunOptions.Terminate has no such requirement: the
        // executor checks the flag as it walks the graph, on whichever thread is walking it, so a
        // single-threaded session cancels exactly like a parallel one.
        using var ocr = new RapidOcr();
        ocr.InitModels(numThread: 1);

        // Same few-box image as above, and doubly suited here: at one thread the inference loses
        // its parallelism while the Skia preprocessing largely keeps its own, so the run takes an
        // even larger share of the call than it does on the shared engine.
        using SKBitmap originSrc = LoadImage(FewBoxImage);

        long warmMs = MeasureWarm(ocr, originSrc);

        var inners = await SweepCancellations(ocr, originSrc,
            f => TimeSpan.FromMilliseconds(warmMs * f),
            [0.4, 0.5, 0.6, 0.7, 0.8, 0.9]);

        AssertReachedTerminatePath(inners, "A single-threaded session");
    }

    /// <summary>
    /// For the test that pins cancellation reaching the ONNX run. 2500x1406, and — the part that
    /// matters — only about three text boxes, so the contour work after the run costs almost
    /// nothing and the run is most of the detector stage. A timer aimed at the stage lands in the
    /// run nearly every time.
    /// </summary>
    private const string FewBoxImage = "img_11_large.jpg";

    /// <summary>
    /// For the test that pins the check after the detector stage. 5184x6708 and around a hundred
    /// text boxes, so the post-run contour work — a Skia mask render per box in
    /// <c>GetScore</c> — is long, which is exactly the window that check protects.
    /// </summary>
    /// <remarks>
    /// Under <see cref="RapidOcrOptions.Default"/> its size does not reach the model:
    /// <c>ImgResize = 1024</c> caps the detector's long side, so the source is scaled down before
    /// inference and the stage grows only 172ms to 260ms against the 2500x1406 image, while
    /// <c>PrepareDetectorInput</c> — which is what pays for the extra pixels — grows 96ms to
    /// 922ms. Here that buys a long protected tail and a stable measurement.
    /// <para>
    /// The cap is doing that, not the detector. Inference cost tracks the input area almost
    /// linearly: raising <c>ImgResize</c> past this image's long side feeds the model the full
    /// 5184x6656 and the stage goes to about 8.7 seconds, some forty times the default. Any test
    /// timing the stage is timing the capped input, and would need rewriting if these options
    /// changed.
    /// </para>
    /// </remarks>
    private const string TimedImage = "2108.11480_1_very_large.png";

    /// <summary>
    /// Where the two halves of a <see cref="RapidOcr.DetectBoxes(SKBitmap, RapidOcrOptions, CancellationToken)"/>
    /// call fall on this machine. The split matters: <c>PrepareDetectorInput</c> runs first and
    /// has no cancellation check of its own, and on a large image it is most of the wall time —
    /// about 900ms of 1150ms here. A delay expressed as a fraction of the total would land there
    /// almost every time and never reach the inference it was meant to interrupt.
    /// </summary>
    private readonly record struct DetectorTiming(long TotalMs, long DetectorStageMs)
    {
        /// <summary>Preprocessing, before the detector stage begins.</summary>
        public long PreprocessMs => TotalMs - DetectorStageMs;

        /// <summary>
        /// A delay landing <paramref name="fraction"/> of the way through the detector stage —
        /// the ONNX run and the contour work after it, which is the only interruptible part.
        /// </summary>
        public TimeSpan IntoDetectorStage(double fraction)
            => TimeSpan.FromMilliseconds(PreprocessMs + (DetectorStageMs * fraction));
    }

    /// <summary>
    /// Measured once for the class: several warm calls plus a full pipeline pass are not cheap on
    /// an image this size, and every test placing a delay wants the same two numbers.
    /// </summary>
    private static readonly Lazy<DetectorTiming> Timing = new(() => MeasureDetector(Engine.Value));

    /// <summary>
    /// Warms <paramref name="engine"/> on <paramref name="image"/> and returns the fastest of
    /// several detections. The fastest, not one sample: deadlines are derived from this figure, and
    /// an unlucky slow sample would push them past the end of the call so they never fire at all.
    /// Biasing low errs towards cancelling early, which is still inside the call.
    /// </summary>
    private static long MeasureWarm(RapidOcr engine, SKBitmap image)
    {
        engine.DetectBoxes(image, RapidOcrOptions.Default);   // warm

        long totalMs = long.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            var sw = Stopwatch.StartNew();
            engine.DetectBoxes(image, RapidOcrOptions.Default);
            totalMs = Math.Min(totalMs, sw.ElapsedMilliseconds);
        }

        return totalMs;
    }

    private static DetectorTiming MeasureDetector(RapidOcr engine)
    {
        using SKBitmap image = LoadImage(TimedImage);

        long totalMs = MeasureWarm(engine, image);

        // DbNetTime is timed from inside DetectOnce, after PrepareDetectorInput has returned, so
        // it is exactly the detector stage and the remainder is the preprocessing.
        var result = engine.Detect(image, RapidOcrOptions.Default);

        return new DetectorTiming(totalMs, (long)result.DbNetTime);
    }

    /// <summary>
    /// Cancels a <see cref="RapidOcr.DetectBoxesAsync(SKBitmap, RapidOcrOptions, CancellationToken)"/>
    /// at each of <paramref name="fractions"/> and collects the inner exception of every
    /// cancellation observed. An <see cref="OnnxRuntimeException"/> inside one is the terminate
    /// path's signature — the delay reached the running inference. A null means the cancellation
    /// was seen at a plain check either side of it, and no entry at all means the call finished
    /// before its deadline.
    /// </summary>
    private static async Task<List<Exception?>> SweepCancellations(
        RapidOcr engine, SKBitmap image, Func<double, TimeSpan> deadlineFor, double[] fractions)
    {
        var inners = new List<Exception?>();
        foreach (double fraction in fractions)
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(deadlineFor(fraction));

            try
            {
                await engine.DetectBoxesAsync(image, RapidOcrOptions.Default, cts.Token);
            }
            catch (OperationCanceledException ex)
            {
                inners.Add(ex.InnerException);
            }
        }

        return inners;
    }

    private static void AssertReachedTerminatePath(List<Exception?> inners, string what)
    {
        Assert.True(inners.Any(i => i is OnnxRuntimeException),
            $"{what} never reached the terminate path, so nothing here shows it cancels " +
            "mid-inference. Inner exceptions seen: " +
            $"[{string.Join(", ", inners.Select(i => i?.GetType().Name ?? "null"))}]");
    }

    [Fact(Skip = TimingSkip)]
    public async Task DetectBoxesDoesNotReturnAResultProducedAfterCancellation()
    {
        // Detection is an ONNX run followed by contour finding, per-contour scoring, unclipping
        // and sorting — all managed, all after the inference, and none of it cheap on a large
        // page. Terminate only reaches the inference, so cancelling anywhere in that tail used to
        // yield a complete, successful result.
        //
        // Terminate reaches the inference but not the contour work after it, so the tail of the
        // stage is where a cancellation used to be ignored outright. The delays below sit in that
        // tail.
        //
        // Asserting "must throw" there would be asserting that the tail has not already finished
        // when the timer fires, which is a race against a ~50ms measurement spread. The invariant
        // that does not race: a call may finish normally only if it beat its own deadline.
        // Overrunning it means the work carried on after the caller gave up, which is the defect —
        // and the defect overran by the whole length of the contour work, far outside the
        // tolerance below.
        var engine = Engine.Value;
        var timing = Timing.Value;
        using SKBitmap originSrc = LoadImage(TimedImage);

        Assert.True(timing.DetectorStageMs >= 40,
            $"The detector stage runs for {timing.DetectorStageMs}ms here, too short to place a delay inside it.");

        // CancelAfter rides the system timer, whose granularity is around 15ms on Windows, so a
        // result landing a little past the nominal deadline is not evidence of anything. The
        // allowance is wider than that granularity because a loaded machine stretches the box
        // mapping that follows the last check, and the defect this guards overran by the whole
        // length of the contour work — far more than either.
        var granularity = TimeSpan.FromMilliseconds(50);

        foreach (double fraction in new[] { 0.6, 0.7, 0.8 })
        {
            var deadline = timing.IntoDetectorStage(fraction);
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(deadline);

            var sw = Stopwatch.StartNew();
            try
            {
                await engine.DetectBoxesAsync(originSrc, RapidOcrOptions.Default, cts.Token);
                sw.Stop();

                Assert.True(sw.Elapsed < deadline + granularity,
                    $"Cancelled at {deadline.TotalMilliseconds:F0}ms but a result came back " +
                    $"{sw.ElapsedMilliseconds}ms in, so the detector stage carried on past the cancellation.");
            }
            catch (OperationCanceledException)
            {
                // The outcome under test.
            }
        }
    }

    /// <summary>
    /// Comfortably below the cost of the work these tests require to be skipped — preprocessing
    /// is around 900ms on the timed image and decoding it around 250ms — and comfortably above
    /// the nothing a cancelled call should actually do.
    /// </summary>
    private static readonly TimeSpan ImmediatelyEnough = TimeSpan.FromMilliseconds(200);

    [Fact(Skip = ElapsedSkip)]
    public void DetectWithAnAlreadyCancelledTokenSkipsPreprocessing()
    {
        // Preprocessing runs before the first stage and used to observe nothing, so a caller who
        // had already given up still paid for it: on this image MakePadding alone allocates and
        // blits a bitmap of some 140MB, about 900ms, before the token was so much as looked at.
        var engine = Engine.Value;
        using SKBitmap originSrc = LoadImage(TimedImage);

        engine.DetectBoxes(originSrc, RapidOcrOptions.Default);   // warm, so this times work and not JIT

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sw = Stopwatch.StartNew();
        Assert.Throws<OperationCanceledException>(
            () => engine.Detect(originSrc, RapidOcrOptions.Default, cancellationToken: cts.Token));
        sw.Stop();

        Assert.True(sw.Elapsed < ImmediatelyEnough,
            $"Took {sw.ElapsedMilliseconds}ms to refuse a cancelled call, so the preprocessing ran first.");
    }

    [Fact(Skip = ElapsedSkip)]
    public void DetectFromPathWithAnAlreadyCancelledTokenSkipsDecoding()
    {
        // The path overloads decode before they preprocess, which on this image is another
        // quarter of a second of work owed to a caller who is no longer waiting for it.
        var engine = Engine.Value;
        string path = Path.Combine("images", TimedImage);
        Assert.True(File.Exists(path));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sw = Stopwatch.StartNew();
        Assert.Throws<OperationCanceledException>(
            () => engine.Detect(path, RapidOcrOptions.Default, cancellationToken: cts.Token));
        sw.Stop();

        Assert.True(sw.Elapsed < ImmediatelyEnough,
            $"Took {sw.ElapsedMilliseconds}ms to refuse a cancelled call, so the image was decoded first.");
    }

    [Fact(Skip = ElapsedSkip)]
    public void DetectBoxesFromPathWithAnAlreadyCancelledTokenSkipsDecoding()
    {
        var engine = Engine.Value;
        string path = Path.Combine("images", TimedImage);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sw = Stopwatch.StartNew();
        Assert.Throws<OperationCanceledException>(
            () => engine.DetectBoxes(path, RapidOcrOptions.Default, cts.Token));
        sw.Stop();

        Assert.True(sw.Elapsed < ImmediatelyEnough,
            $"Took {sw.ElapsedMilliseconds}ms to refuse a cancelled call, so the image was decoded first.");
    }

    [Fact]
    public void CancellableButUncancelledTokenChangesNoBoxes()
    {
        // A token that can be cancelled takes the RunOptions path, where an uncancellable one
        // still goes straight to InferenceSession.Run. Both must produce the same boxes: the
        // overload carries the session's own output names, so it is meant to be a pure
        // pass-through, and this is what would catch it if it ever stopped being one.
        using SKBitmap a = LoadImage("en_rec.jpg");
        using SKBitmap b = LoadImage("en_rec.jpg");
        using var cts = new CancellationTokenSource();

        var fastPath = Engine.Value.DetectBoxes(a, RapidOcrOptions.Default, CancellationToken.None);
        var runOptionsPath = Engine.Value.DetectBoxes(b, RapidOcrOptions.Default, cts.Token);

        Assert.Equal(fastPath.Count, runOptionsPath.Count);
        for (int i = 0; i < fastPath.Count; i++)
        {
            Assert.Equal(fastPath[i].BoxPoints, runOptionsPath[i].BoxPoints);
            Assert.Equal(fastPath[i].Score, runOptionsPath[i].Score);
        }
    }

    [Fact]
    public void CancellableButUncancelledTokenChangesNoText()
    {
        // Same guard across the full pipeline, so the classifier and recognizer runs are covered
        // too and not just the detector's.
        using SKBitmap a = LoadImage("en_rec.jpg");
        using SKBitmap b = LoadImage("en_rec.jpg");
        using var cts = new CancellationTokenSource();

        var fastPath = Engine.Value.Detect(a, RapidOcrOptions.Default, cancellationToken: CancellationToken.None);
        var runOptionsPath = Engine.Value.Detect(b, RapidOcrOptions.Default, cancellationToken: cts.Token);

        Assert.Equal(fastPath.StrRes, runOptionsPath.StrRes);
    }

    [Fact]
    public void UncancelledTokenChangesNothing()
    {
        // The default path has to stay byte-for-byte what it was: CancellationToken.None must
        // produce exactly the result the parameterless overload does.
        using SKBitmap a = LoadImage("en_rec.jpg");
        using SKBitmap b = LoadImage("en_rec.jpg");

        var withoutToken = Engine.Value.Detect(a, RapidOcrOptions.Default);
        var withToken = Engine.Value.Detect(b, RapidOcrOptions.Default, cancellationToken: CancellationToken.None);

        Assert.Equal(withoutToken.TextBlocks.Length, withToken.TextBlocks.Length);
        for (int i = 0; i < withoutToken.TextBlocks.Length; i++)
        {
            Assert.Equal(withoutToken.TextBlocks[i].Text, withToken.TextBlocks[i].Text);
        }
    }
}
