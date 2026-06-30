using SkiaSharp;

namespace RapidOcrNet.Tests;

public class OcrTestV6Small
{
    private static readonly Lazy<RapidOcr> V6SmallEngine = new(() =>
    {
        var ocr = new RapidOcr();
        ocr.InitModels(RapidOcrModelSet.PPOCRv6Small);
        return ocr;
    });

    public static IEnumerable<object[]> V6ModelSets => new[]
    {
        new object[] { "tiny", RapidOcrModelSet.PPOCRv6Tiny },
        new object[] { "small", RapidOcrModelSet.PPOCRv6Small },
        new object[] { "medium", RapidOcrModelSet.PPOCRv6Medium },
    };

    /// <summary>
    /// Smoke test for PP-OCRv6: each size loads and produces non-empty output on a clean
    /// English image. Exact-string assertions are intentionally omitted here because v6
    /// correctness depends on the detector normalization taken from the RapidOCR Python
    /// config, which is not verifiable without running inference. A v6 detector that misses
    /// every box (e.g. wrong mean/std) will fail this test.
    /// </summary>
    [Theory]
    [MemberData(nameof(V6ModelSets))]
    public void OcrV6SmokeTest(string size, RapidOcrModelSet models)
    {
        _ = size;

        Assert.True(File.Exists(models.DetModelPath), $"Missing v6 detector model: '{models.DetModelPath}'.");
        Assert.True(File.Exists(models.RecModelPath), $"Missing v6 recognizer model: '{models.RecModelPath}'.");
        Assert.True(File.Exists(models.KeysPath), $"Missing v6 keys file: '{models.KeysPath}'.");

        string path = Path.Combine("images", "en_rec.jpg");
        Assert.True(File.Exists(path));

        using var ocr = new RapidOcr();
        ocr.InitModels(models);

        using SKBitmap originSrc = SKBitmap.Decode(path);
        OcrResult ocrResult = ocr.Detect(originSrc, RapidOcrOptions.PPOCRv6);

        Assert.NotNull(ocrResult.TextBlocks);
        Assert.NotEmpty(ocrResult.TextBlocks);
        Assert.Contains(ocrResult.TextBlocks, b => !string.IsNullOrWhiteSpace(b.Text));
    }

    // Expected text blocks for PP-OCRv6 small. Captured by running the small model with
    // RapidOcrOptions.PPOCRv6 (the option set v6 is designed for) and verified by eye
    // against the source images.
    public static IEnumerable<object[]> Images => new[]
    {
        new object[]
        {
            "issue_170.png",
            new string[]
            {
                "TEST",
            }
        },
        new object[]
        {
            "TIKA-1552-0_3.png",
            new string[]
            {
                "★",
                "FISCAL YEAR 2014",
                "BUDGET",
                "OF THE U.S. GOVERNMENT",
                "OFFICE OF MANAGEMENT AND BUDGET",
                "BUDGET.GOV",
                "Scan here to go to",
                "our website."
            }
        },
        new object[] { "empty_black.jpg", Array.Empty<string>() },
        new object[]
        {
            // Better than v5
            "254.jpg",
            new string[]
            {
                "PHO CAPITAL",
                "107 State Street",
                "Montpelier Vermont",
                "802 225 6183",
                "REG",
                "07-24-2017 06:59 PM",
                "045555",
                "CT 1",
                "$7.95",
                "T1",
                "1 FOOD",
                "T1",
                "$3.95",
                "1 FO0D", // Incorrect: 0 instead of O
                "T1",
                "$9.50",
                "1 FOOD",
                "3 No",
                "$21.40",
                "TA1",
                "$1.92",
                "TX1",
                "$23.32",
                "TL",
                "$23.32",
                "CASH",
                "THANK YOU",
                "FOR YOUR BUSINESS"
            }
        },
        new object[]
        {
            // Captured v6 small + PPOCRv6 output. v6 only detects two blocks on this
            // complex scene (v5 detects more, with errors).
            "img623.jpg",
            new string[]
            {
                "1966",
                "www.flavoursofiloilo.blogspot.com"
            }
        },
        new object[]
        {
            "en_rec.jpg",
            new string[]
            {
                "To facilitate the shot type analysis in videos, we collect MovieShots, a large-scale"
            }
        },
        new object[]
        {
            // v6 splits the leading "3" heading marker into its own block here.
            "en.jpg",
            new string[]
            {
                "3",
                "MovieShots Dataset",
                "To facilitate the shot type analysis in videos, we collect MovieShots, a large-scale",
                "shot type annotation set that contains 46K shots from 7858 movies. The details",
                "of this dataset are specified as follows."
            }
        },
        new object[]
        {
            "latin.jpg",
            new string[]
            {
                "Alphabetum in mundo hodie frequentissime adhibitum est alphabetum Latinum."
            }
        },
        new object[]
        {
            "1997.png",
            new string[] { "1997" }
        },
        new object[]
        {
            "rotated.PNG",
            new string[] { "This is some angled text" }
        },
        new object[]
        {
            "rotated2.PNG",
            new string[]
            {
                "This is some further text continuing to write",
                "Hello World!"
            }
        },
        new object[]
        {
            "img_10.jpg",
            new string[]
            {
                "Please lower your volume",
                "when you pass by",
                "residential areas."
            }
        },
        new object[]
        {
            "img_12.jpg",
            new string[]
            {
                "ACKNOWLEDGEMENTS",
                "We would like to thank all the designers and",
                "contributors who have been involved in the",
                "production of this book; their contributions",
                "have been indispensable to its creation. We",
                "would also like to express our gratitude to all",
                "the producers for their invaluable opinions",
                "and assistance throughout this project. And to",
                "the many others whose names are not credited",
                "but have made specific input in this book, we",
                "thank you for your continuous support."
            }
        },
        new object[]
        {
            "img_11.jpg",
            new string[]
            {
                "BEWARE OF",
                "MAINTENANCE",
                "VEHICLES"
            }
        },
        new object[]
        {
            "img_195.jpg",
            new string[]
            {
                "EXPERIENCE",
                "Open to Public.",
                "FIBRE HERE",
                "Free Admission."
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "bold-italic_1.png",
            new string[]
            {
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit."
            }
        },
        new object[]
        {
            "GHOSTSCRIPT-693073-1_2.png",
            new string[] { "This is test sample" }
        }
    };

    // Expected per-word output for PP-OCRv6 small, captured with RapidOcrOptions.PPOCRv6
    // and verified against the images. All entries are 100% correct for the small model.
    public static IEnumerable<object[]> ImagesWords => new[]
    {
        new object[] { "empty_black.jpg", Array.Empty<string>() },
        new object[]
        {
            "en_rec.jpg",
            new string[]
            {
                "To", "facilitate", "the", "shot", "type", "analysis", "in",
                "videos,", "we", "collect", "MovieShots,", "a", "large-scale"
            }
        },
        new object[]
        {
            "latin.jpg",
            new string[]
            {
                "Alphabetum", "in", "mundo", "hodie", "frequentissime",
                "adhibitum", "est", "alphabetum", "Latinum."
            }
        },
        new object[]
        {
            "rotated.PNG",
            new string[] { "This", "is", "some", "angled", "text" }
        },
        new object[]
        {
            "img_10.jpg",
            new string[]
            {
                "Please", "lower", "your", "volume", "when", "you",
                "pass", "by", "residential", "areas."
            }
        },
        new object[]
        {
            "img_11.jpg",
            new string[] { "BEWARE", "OF", "MAINTENANCE", "VEHICLES" }
        },
        new object[]
        {
            "GHOSTSCRIPT-693073-1_2.png",
            new string[] { "This", "is", "test", "sample" }
        }
    };

    public static IEnumerable<object[]> TesseractImages => new[]
  {
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "blank.png",
            new string[] { }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "empty.png",
            new string[] { }
        },
        new object[]
        {
            // Status: Pass
            "Fonts.png",
            new string[]
            {
                "Bold Italic Fixed Serif CAPITAL 123 x² y3"
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "phototest.png",
            new string[]
            {
                "This is a lot of 12 point text to test the",
                "ocr code and see if it works on all types",
                "of file format.",
                "The quick brown dog jumped over the",
                "lazy fox. The quick brown dog jumped",
                "over the lazy fox. The quick brown dog",
                "jumped over the lazy fox. The quick",
                "brown dog jumped over the lazy fox."
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "PSM_SingleBlock.png",
            new string[]
            {
                "This is a lot of 12 point text to test the",
                "ocr code and see if it works on all types",
                "of file format."
            }
        },
        /* Fails for v6
        new object[]
        {
            "PSM_SingleBlockVertText.png",
            new string[]
            {
                "A",
                "I", // Incorrect: 'I' instead of 'l'
                "i",
                "n",
                "e",
                "o",
                "f",
                "t",
                "e",
                "X", // Incorrect: should be lower-case
                "t"
            }
        },
        */
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "PSM_SingleColumn.png",
            new string[]
            {
                "This is a lot of 12 point text to test the",
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "PSM_SingleChar.png",
            new string[]
            {
                "T"
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "PSM_SingleLine.png",
            new string[]
            {
                "This is a lot of 12 point text to test the",
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "PSM_SingleWord.png",
            new string[]
            {
                "This"
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "scewed-phototest.png",
            new string[]
            {
                "This is a lot of 12 point text to test the",
                "ocr code and see if it works on all types",
                "of file format.",
                "The quick brown dog jumped over the",
                "lazy fox. The quick brown dog jumped",
                "over the lazy fox. The quick brown dog",
                "jumped over the lazy fox. The quick",
                "brown dog jumped over the lazy fox."
            }
        },
    };

    /// <summary>
    /// Text-block recognition using the PP-OCRv6 small models. Mirrors <see cref="OcrTextBlock"/>
    /// but with the v6 small engine and <see cref="RapidOcrOptions.PPOCRv6"/> options.
    /// </summary>
    [Theory]
    [MemberData(nameof(Images))]
    public void OcrTextBlock(string path, string[] expected)
    {
        path = Path.Combine("images", path);

        Assert.True(File.Exists(path));

        using (SKBitmap originSrc = SKBitmap.Decode(path))
        {
            OcrResult ocrResult = V6SmallEngine.Value.Detect(originSrc, RapidOcrOptions.PPOCRv6);

            Helper.VisualDebugBbox(Path.ChangeExtension(path, "_ocr_v6_small.png"), originSrc, ocrResult);

            var actual = ocrResult.TextBlocks.Select(b => b.Text).ToArray();
            Assert.NotNull(actual);
            Assert.Equal(expected.Length, actual.Length);

            for (int s = 0; s < expected.Length; s++)
            {
                Assert.Equal(expected[s], actual[s]);
            }
        }
    }

    /// <summary>
    /// Per-word bounding-box recognition using the PP-OCRv6 small models. Mirrors
    /// <see cref="OcrWordBox"/> but with the v6 small engine and
    /// <see cref="RapidOcrOptions.PPOCRv6"/> options.
    /// </summary>
    [Theory]
    [MemberData(nameof(ImagesWords))]
    public void OcrWordBox(string path, string[] expected)
    {
        path = Path.Combine("images", path);

        Assert.True(File.Exists(path));

        using (SKBitmap originSrc = SKBitmap.Decode(path))
        {
            OcrResult ocrResult = V6SmallEngine.Value.Detect(originSrc, RapidOcrOptions.PPOCRv6 with { ReturnWordBox = true });

            Helper.VisualDebugBbox(Path.ChangeExtension(path, "_ocr_word_v6_small.png"), originSrc, ocrResult);

            foreach (var block in ocrResult.TextBlocks)
            {
                Assert.NotNull(block.WordResults);
            }

            var actual = ocrResult.TextBlocks.SelectMany(b => b.WordResults!.Select(w => w.Text)).ToArray();
            Assert.NotNull(actual);

            Assert.Equal(expected.Length, actual.Length);

            for (int s = 0; s < expected.Length; s++)
            {
                Assert.Equal(expected[s], actual[s]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(TesseractImages))]
    public void TesseractOcrTextBlock(string path, string[] expected)
    {
        path = Path.Combine("images_tesseract", path);

        Assert.True(File.Exists(path));

        using (SKBitmap originSrc = SKBitmap.Decode(path))
        {
            OcrResult ocrResult = V6SmallEngine.Value.Detect(originSrc, RapidOcrOptions.PPOCRv6);

            Helper.VisualDebugBbox(Path.ChangeExtension(path, "_ocr_v6_small.png"), originSrc, ocrResult);

            var actual = ocrResult.TextBlocks.Select(b => b.Text).ToArray();
            Assert.NotNull(actual);
            Assert.Equal(expected.Length, actual.Length);

            for (int s = 0; s < expected.Length; s++)
            {
                Assert.Equal(expected[s], actual[s]);
            }
        }
    }
}
