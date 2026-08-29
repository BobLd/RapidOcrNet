using SkiaSharp;

namespace RapidOcrNet.Tests;

/// <summary>
/// Covers <see cref="RapidOcrOptions.RecBatchSize"/> — the recognizer's optional
/// several-crops-per-inference path.
/// </summary>
/// <remarks>
/// The batched path right-pads crops to a common width, which changes the recognizer's input
/// and so can change what it reads. These tests therefore assert what is actually contractual
/// — that 1 means the legacy path exactly, that batching keeps results in detection order and
/// keeps the geometry consistent, and that it reads clean single-line images the same way —
/// rather than pinning batched text on hard inputs, which would be asserting a property the
/// models do not have.
/// </remarks>
public class RecBatchTest
{
    private static readonly Lazy<RapidOcr> V6SmallEngine = new(() =>
    {
        var ocr = new RapidOcr();
        ocr.InitModels(RapidOcrModelSet.PPOCRv6Small);
        return ocr;
    });

    /// <summary>The default must stay on the legacy per-crop path.</summary>
    [Fact]
    public void DefaultOptionsDoNotBatch()
    {
        Assert.Equal(1, RapidOcrOptions.Default.RecBatchSize);
        Assert.Equal(1, RapidOcrOptions.PythonCompat.RecBatchSize);
        Assert.Equal(1, RapidOcrOptions.PPOCRv6.RecBatchSize);
        Assert.Equal(1, new RapidOcrOptions().RecBatchSize);
    }

    /// <summary>
    /// A batch size of 1 must be the legacy path, not merely a batch that happens to hold one
    /// crop: the batched path pads to a 320px floor even for a single crop, so if the two ever
    /// converged the default would silently change what every existing caller reads.
    /// </summary>
    [V6Theory(V6Size.Small)]
    [InlineData(0)]
    [InlineData(1)]
    public void BatchSizeOfOneMatchesTheDefault(int batchSize)
    {
        string path = Path.Combine("images", "254.jpg");
        Assert.True(File.Exists(path));

        using SKBitmap originSrc = SKBitmap.Decode(path);

        string[] expected = V6SmallEngine.Value
            .Detect(originSrc, RapidOcrOptions.PPOCRv6)
            .TextBlocks.Select(b => b.Text).ToArray();

        string[] actual = V6SmallEngine.Value
            .Detect(originSrc, RapidOcrOptions.PPOCRv6 with { RecBatchSize = batchSize })
            .TextBlocks.Select(b => b.Text).ToArray();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Batching sorts crops by aspect ratio before chunking, so results have to be scattered
    /// back into detection order. A regression here would silently attach every line's text to
    /// the wrong box, which no smoke test would catch.
    /// </summary>
    [V6Theory(V6Size.Small)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void BatchingPreservesDetectionOrder(int batchSize)
    {
        // 11 blocks of visibly different widths, so the aspect-ratio sort genuinely reorders
        // them, and stable enough under padding that the texts themselves do not move.
        string path = Path.Combine("images", "img_12.jpg");
        Assert.True(File.Exists(path));

        using SKBitmap originSrc = SKBitmap.Decode(path);

        OcrResult unbatched = V6SmallEngine.Value.Detect(originSrc, RapidOcrOptions.PPOCRv6);
        OcrResult batched = V6SmallEngine.Value
            .Detect(originSrc, RapidOcrOptions.PPOCRv6 with { RecBatchSize = batchSize });

        Assert.Equal(unbatched.TextBlocks.Length, batched.TextBlocks.Length);

        // Boxes come from detection, which batching does not touch, so they line up
        // one-for-one whatever happens. Pairing each box with its text is what a broken
        // scatter-back would get wrong: the text would move, the boxes would not.
        for (int i = 0; i < unbatched.TextBlocks.Length; i++)
        {
            Assert.Equal(unbatched.TextBlocks[i].BoxPoints, batched.TextBlocks[i].BoxPoints);
            Assert.Equal(unbatched.TextBlocks[i].Text, batched.TextBlocks[i].Text);
        }
    }

    /// <summary>
    /// Every crop must come back, whatever the chunk boundaries fall on. Sizes are chosen
    /// around the block count so the last chunk is short, exact, and oversized in turn.
    /// </summary>
    [V6Theory(V6Size.Small)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(1000)]
    public void EveryCropIsRecognizedWhateverTheChunking(int batchSize)
    {
        string path = Path.Combine("images", "img_12.jpg");
        Assert.True(File.Exists(path));

        using SKBitmap originSrc = SKBitmap.Decode(path);

        OcrResult result = V6SmallEngine.Value
            .Detect(originSrc, RapidOcrOptions.PPOCRv6 with { RecBatchSize = batchSize });

        Assert.NotEmpty(result.TextBlocks);
        Assert.All(result.TextBlocks, b => Assert.NotNull(b.Text));
        Assert.Contains(result.TextBlocks, b => !string.IsNullOrWhiteSpace(b.Text));
    }

    /// <summary>
    /// On a clean single-line image the padding has nothing to hide behind, so batched and
    /// unbatched must agree. This is the case where a batching bug — wrong slot decoded, wrong
    /// normalization, padding written over real pixels — would show up as garbage text.
    /// </summary>
    [V6Fact(V6Size.Small)]
    public void BatchingReadsACleanLineIdentically()
    {
        string path = Path.Combine("images", "issue_170.png");
        Assert.True(File.Exists(path));

        using SKBitmap originSrc = SKBitmap.Decode(path);

        string[] unbatched = V6SmallEngine.Value
            .Detect(originSrc, RapidOcrOptions.PPOCRv6)
            .TextBlocks.Select(b => b.Text).ToArray();

        string[] batched = V6SmallEngine.Value
            .Detect(originSrc, RapidOcrOptions.PPOCRv6 with { RecBatchSize = 8 })
            .TextBlocks.Select(b => b.Text).ToArray();

        Assert.Equal(new[] { "TEST" }, unbatched);
        Assert.Equal(unbatched, batched);
    }

    /// <summary>
    /// <see cref="TextLine.LineTxtLen"/> is how <see cref="CalRecBoxes"/> converts CTC column
    /// indices into pixel positions. Under batching it must describe only the crop's own
    /// un-padded portion, so it has to come out at or below the raw timestep count and stay
    /// positive — otherwise word boxes stretch across the padding.
    /// </summary>
    [V6Fact(V6Size.Small)]
    public void BatchedWordBoxesStayInsideTheirBlock()
    {
        string path = Path.Combine("images", "en_rec.jpg");
        Assert.True(File.Exists(path));

        using SKBitmap originSrc = SKBitmap.Decode(path);

        OcrResult result = V6SmallEngine.Value.Detect(originSrc,
            RapidOcrOptions.PPOCRv6 with { RecBatchSize = 8, ReturnWordBox = true });

        Assert.NotEmpty(result.TextBlocks);

        foreach (TextBlock block in result.TextBlocks)
        {
            if (block.WordResults is null)
            {
                continue;
            }

            int minX = block.BoxPoints.Min(p => p.X);
            int maxX = block.BoxPoints.Max(p => p.X);
            int minY = block.BoxPoints.Min(p => p.Y);
            int maxY = block.BoxPoints.Max(p => p.Y);

            // A LineTxtLen that ignored the padding would place words beyond the line they
            // came from, so bounding the word boxes by their block is what actually tests it.
            // One pixel of slack absorbs the rounding in the column-to-pixel mapping.
            foreach (WordBox word in block.WordResults)
            {
                Assert.All(word.BoxPoints, p =>
                {
                    Assert.InRange(p.X, minX - 1, maxX + 1);
                    Assert.InRange(p.Y, minY - 1, maxY + 1);
                });
            }
        }
    }
}
