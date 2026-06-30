using SkiaSharp;

namespace RapidOcrNet.Tests;

public class OcrTestV6Tiny
{
    private static readonly Lazy<RapidOcr> V6SmallEngine = new(() =>
    {
        var ocr = new RapidOcr();
        ocr.InitModels(RapidOcrModelSet.PPOCRv6Tiny);
        return ocr;
    });

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
                "IHHO", // Wrong
                "OF MANAGE",
                "FISCAL YEAR 2014",
                "BUDGET",
                "OF THE U.S.GOVERNMENT", // Missing space
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
                "PHO",
                "CAPITAL",
                "107",
                "State Street",
                "Montpelier Vermont",
                "802 225 6183",
                "REG",
                "07-24-2017 06:59 PM",
                "045555",
                "CT",
                "1",
                "T1",
                "$7.95",
                "1 F00D",
                "1 F00D",
                "T1",
                "$3.95",
                "T1",
                "$9.50",
                "F0OD",
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
                "FOR YOUR",
                "BUSINESS"
            }
        },
        new object[]
        {
            // Captured v6 small + PPOCRv6 output. v6 only detects two blocks on this
            // complex scene (v5 detects more, with errors).
            "img623.jpg",
            new string[]
            {
                "PARE RIBS", // Wrong
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
                "3 MovieShots Dataset",
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
                "Hello World!",
                "This is some further text continuing to write"
            }
        },
        new object[]
        {
            "img_10.jpg",
            new string[]
            {
                "Please lower your yolume", // Wrong 'yolume'
                "when you pass by",
                "residential areas" // Should be 'areas.'
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
                "would also like to express our gratitude to al", // Wrong, should be 'all'
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
                "Please", "lower", "your", "yolume", "when", "you", // Wrong 'yolume'
                "pass", "by", "residential", "areas" // Should be 'areas.'
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
                "Bold Italic Fixed Serif CAPITAL 123 x² y₃" // Better than small!
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
                "ovér the lazy fox. The quick brown dog", // Wrong 'é'
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
        /* Fails
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "PSM_SingleChar.png",
            new string[]
            {
                "T"
            }
        },
        */
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "PSM_SingleLine.png",
            new string[]
            {
                "This is a lot of 12 point text to test the",
            }
        },
        /* Fails
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "PSM_SingleWord.png",
            new string[]
            {
                "This"
            }
        },
        */
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
                "ovér the lazy fox. The quick brown dog", // Wrong 'é'
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

            Helper.VisualDebugBbox(Path.ChangeExtension(path, "_ocr_v6_tiny.png"), originSrc, ocrResult);

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

            Helper.VisualDebugBbox(Path.ChangeExtension(path, "_ocr_word_v6_tiny.png"), originSrc, ocrResult);

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

            Helper.VisualDebugBbox(Path.ChangeExtension(path, "_ocr_v6_tiny.png"), originSrc, ocrResult);

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
