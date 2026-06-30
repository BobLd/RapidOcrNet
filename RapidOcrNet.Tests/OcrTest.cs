using SkiaSharp;

namespace RapidOcrNet.Tests;

public class OcrTest
{
    public static IEnumerable<object[]> ImagesWords => new[]
    {
            new object[]
            {
                "empty_black.jpg",
                Array.Empty<string>()
            },
            new object[]
            {
                // Status: Pass, expected text is 100% correct
                "en_rec.jpg",
                new string[]
                {
                    "To",
                    "facilitate",
                    "the",
                    "shot",
                    "type",
                    "analysis",
                    "in",
                    "videos,",
                    "we",
                    "collect",
                    "MovieShots,",
                    "a",
                    "large-scale"
                }
            },
            new object[]
            {
                // Status: Pass, expected text is 100% correct
                "latin.jpg",
                new string[]
                {
                    "Alphabetum",
                    "in",
                    "mundo",
                    "hodie",
                    "frequentissime",
                    "adhibitum",
                    "est",
                    "alphabetum",
                    "Latinum."
                }
            },
            new object[]
            {
                // Status: Pass, expected text is 100% correct
                "rotated.PNG",
                new string[]
                {
                    "This",
                    "is",
                    "some",
                    "angled",
                    "text"
                }
            },
            new object[]
            {
                // Status: Pass, expected text is 100% correct
                "img_10.jpg",
                new string[]
                {
                    "Please",
                    "lower",
                    "your",
                    "volume",
                    "when",
                    "you",
                    "pass",
                    "by",
                    "residential",
                    "areas."
                }
            },
            new object[]
            {
                // Status: Pass, expected text is not 100% correct, it's missing "VEHICLES"
                "img_11.jpg",
                new string[]
                {
                    "BEWARE",
                    "OF",
                    "MAINTENANCE",
                    "VEHICLES"
                }
            },
            new object[]
            {
                // Status: Pass, expected text is 100% correct
                "GHOSTSCRIPT-693073-1_2.png",
                new string[]
                {
                    "This",
                    "is",
                    "test",
                    "sample",
                }
            }
        };

    public static IEnumerable<object[]> Images => new[]
    {
        /*
        new object[]
        {
            // Status: currently failing, should pass - expected text is 100% correct
            "issue_170.png", // Gray8
            new string[]
            {
                "TEST"
            }
        },
        */
        new object[]
        {
            "empty_black.jpg",
            Array.Empty<string>()
        },
        new object[]
        {
            "254.jpg",
            new string[]
            {
                "PHO", // OK
                "CAPITAL", // OK
                "107 State Street", // OK
                "Montpelier Vermont", // OK
                "802 225 6183", // OK
                "REG", // OK
                "07-24-2017 06:59 PM", // OK
                "045555", // OK
                "CT", // OK
                "1", // OK
                "T1", // OK
                "$7.95", // OK
                "1 FO0D", // Incorrect: 0 instead of O
                "1 FOOD", // OK
                "T1", // OK
                "$3.95", // OK
                "1 FOOD", // OK
                "T1", // OK
                "$9.50", // OK
                "3 No", // OK
                "$21.40", // OK
                "TA1", // OK
                "$1.92", // OK
                "TX1", // OK
                ".32", // OK
                "$23", // OK
                "TL", // OK
                "$23.32", // OK
                "CASH", // OK
                "THANK YOU", // OK
                "FOR YOUR BUSINESS", // OK
            }
        },
        new object[]
        {
            // Status: Pass, expected text is not 100% correct (this is a complex scene)
            "img623.jpg",
            new string[]
            {
                "HAR", // OK-ish
                "RIBS", // OK
                "1966", // OK
                "BARBECUES", // OK
                "FILINO DISHE", // // Incorrect: FILIPINO DISHES
                "www.flavoursofiloilo.blogspot.com" // OK
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
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
            // Status: Pass, expected text is 100% correct
            "en_rec.jpg",
            new string[]
            {
                "To facilitate the shot type analysis in videos, we collect MovieShots, a large-scale"
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "latin.jpg",
            new string[]
            {
                "Alphabetum in mundo hodie frequentissime adhibitum est alphabetum Latinum."
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "1997.png",
            new string[]
            {
                "1997"
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
            "rotated.PNG",
            new string[]
            {
                "This is some angled text"
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct (order is not, but can ignore)
            "rotated2.PNG",
            new string[]
            {
                "This is some further text continuing to write",
                "Hello World!"
            }
        },
        new object[]
        {
            // Status: Pass, expected text is 100% correct
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
            // Status: Pass, expected text is 100% correct
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
            // Status: Pass, expected text is not 100% correct, it's missing "VEHICLES"
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
            // Status: Pass, expected text is 100% correct
            "img_195.jpg",
            new string[]
            {
                "EXPERIENCE",
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
            // Status: Pass, expected text is 100% correct
            "GHOSTSCRIPT-693073-1_2.png",
            new string[]
            {
                "This is test sample"
            }
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
            // Status: Pass, expected text is not 100% correct (upper / lower case mismatch)
            "Fonts.png",
            new string[]
            {
                "Bold Italic Fixed Serif CaPitAl 123 x² y3" // not exact but good enough
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

    private static readonly Lazy<RapidOcr> V5Engine = new(() =>
    {
        var ocr = new RapidOcr();
        ocr.InitModels();
        return ocr;
    });

    // PP-OCRv6 small engine, loaded once and shared across all v6 test cases (xUnit
    // constructs a fresh test-class instance per case, so a static lazy avoids re-loading
    // the v6 models for every theory row). InferenceSession.Run is thread-safe and the
    // engine holds no mutable per-call state, so sharing is safe.
    private static readonly Lazy<RapidOcr> V6SmallEngine = new(() =>
    {
        var ocr = new RapidOcr();
        ocr.InitModels(RapidOcrModelSet.PPOCRv6Small);
        return ocr;
    });

    [Theory]
    [MemberData(nameof(TesseractImages))]
    public void TesseractOcrTextBlock(string path, string[] expected)
    {
        path = Path.Combine("images_tesseract", path);

        Assert.True(File.Exists(path));

        using (SKBitmap originSrc = SKBitmap.Decode(path))
        {
            OcrResult ocrResult = V5Engine.Value.Detect(originSrc, RapidOcrOptions.Default);

            VisualDebugBbox(Path.ChangeExtension(path, "_ocr.png"), originSrc, ocrResult);

            var actual = ocrResult.TextBlocks.Select(b => b.Text).ToArray();
            Assert.NotNull(actual);
            Assert.Equal(expected.Length, actual.Length);

            for (int s = 0; s < expected.Length; s++)
            {
                Assert.Equal(expected[s], actual[s]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Images))]
    public void OcrTextBlock(string path, string[] expected)
    {
        path = Path.Combine("images", path);

        Assert.True(File.Exists(path));

        using (SKBitmap originSrc = SKBitmap.Decode(path))
        {
            OcrResult ocrResult = V5Engine.Value.Detect(originSrc, RapidOcrOptions.Default);

            VisualDebugBbox(Path.ChangeExtension(path, "_ocr.png"), originSrc, ocrResult);

            var actual = ocrResult.TextBlocks.Select(b => b.Text).ToArray();
            Assert.NotNull(actual);
            Assert.Equal(expected.Length, actual.Length);

            for (int s = 0; s < expected.Length; s++)
            {
                Assert.Equal(expected[s], actual[s]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(ImagesWords))]
    public void OcrWordBox(string path, string[] expected)
    {
        path = Path.Combine("images", path);

        Assert.True(File.Exists(path));

        using (SKBitmap originSrc = SKBitmap.Decode(path))
        {
            OcrResult ocrResult = V5Engine.Value.Detect(originSrc, RapidOcrOptions.Default with { ReturnWordBox = true });

            VisualDebugBbox(Path.ChangeExtension(path, "_ocr_word.png"), originSrc, ocrResult);

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

    /// <summary>
    /// img_11_large.jpg is img_11.jpg upscaled to 2500x1406. Its longest side (2500) exceeds
    /// <see cref="RapidOcrOptions.MaxSideLen"/> (2000), so under the Python-style pipeline
    /// (<see cref="RapidOcrOptions.PythonCompat"/>, ImgResize == 0) Detect() downscales it via
    /// ResizeImageWithinBounds before detection. This exercises the map-back-to-original
    /// rescale: the returned boxes must be expressed in the original (2500x1406) coordinate
    /// space, not the ~2000px bounded space. We verify that by checking the normalized box
    /// centroids match the in-bounds detection of the small image (which needs no rescale).
    /// Without the rescale, the large-image centroids would be off by the bound ratio (~0.8).
    /// </summary>
    [Fact]
    public void OcrLargeImageRescalesBoxesToOriginalSpace()
    {
        string smallPath = Path.Combine("images", "img_11.jpg");
        string largePath = Path.Combine("images", "img_11_large.jpg");
        Assert.True(File.Exists(smallPath));
        Assert.True(File.Exists(largePath));

        string[] expected = ["BEWARE OF", "MAINTENANCE", "VEHICLES"];

        using SKBitmap smallSrc = SKBitmap.Decode(smallPath);
        using SKBitmap largeSrc = SKBitmap.Decode(largePath);

        var options = RapidOcrOptions.PythonCompat;

        // Sanity: the large image actually triggers the downscale path under test, while the
        // small one stays within bounds (so its boxes need no rescale and act as the oracle).
        Assert.True(Math.Max(largeSrc.Width, largeSrc.Height) > options.MaxSideLen);
        Assert.True(Math.Max(smallSrc.Width, smallSrc.Height) <= options.MaxSideLen);

        OcrResult smallResult = V5Engine.Value.Detect(smallSrc, options);
        OcrResult largeResult = V5Engine.Value.Detect(largeSrc, options);

        Assert.Equal(expected, smallResult.TextBlocks.Select(b => b.Text).ToArray());
        Assert.Equal(expected, largeResult.TextBlocks.Select(b => b.Text).ToArray());

        for (int i = 0; i < expected.Length; i++)
        {
            SKPointI[] largePoints = largeResult.TextBlocks[i].BoxPoints;

            // Clamp invariant: every mapped point lands inside the original image bounds.
            foreach (var p in largePoints)
            {
                Assert.InRange(p.X, 0, largeSrc.Width);
                Assert.InRange(p.Y, 0, largeSrc.Height);
            }

            // Coordinates are in the large image's space: normalized centroids match the
            // small-image detection. A missing rescale would shift these by ~0.07.
            var (sx, sy) = NormalizedCentroid(smallResult.TextBlocks[i].BoxPoints, smallSrc.Width, smallSrc.Height);
            var (lx, ly) = NormalizedCentroid(largePoints, largeSrc.Width, largeSrc.Height);

            Assert.True(Math.Abs(sx - lx) < 0.03, $"Block {i} ('{expected[i]}') normalized X off: small={sx:F3}, large={lx:F3}");
            Assert.True(Math.Abs(sy - ly) < 0.03, $"Block {i} ('{expected[i]}') normalized Y off: small={sy:F3}, large={ly:F3}");
        }
    }

    private static (double X, double Y) NormalizedCentroid(SKPointI[] points, int width, int height)
    {
        double cx = 0, cy = 0;
        foreach (var p in points)
        {
            cx += p.X;
            cy += p.Y;
        }

        return (cx / points.Length / width, cy / points.Length / height);
    }

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
    public static IEnumerable<object[]> ImagesV6 => new[]
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
    public static IEnumerable<object[]> ImagesWordsV6 => new[]
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

    public static IEnumerable<object[]> TesseractImagesV6 => new[]
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
    [MemberData(nameof(ImagesV6))]
    public void OcrTextBlockV6(string path, string[] expected)
    {
        path = Path.Combine("images", path);

        Assert.True(File.Exists(path));

        using (SKBitmap originSrc = SKBitmap.Decode(path))
        {
            OcrResult ocrResult = V6SmallEngine.Value.Detect(originSrc, RapidOcrOptions.PPOCRv6);

            VisualDebugBbox(Path.ChangeExtension(path, "_ocr_v6.png"), originSrc, ocrResult);

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
    [MemberData(nameof(ImagesWordsV6))]
    public void OcrWordBoxV6(string path, string[] expected)
    {
        path = Path.Combine("images", path);

        Assert.True(File.Exists(path));

        using (SKBitmap originSrc = SKBitmap.Decode(path))
        {
            OcrResult ocrResult = V6SmallEngine.Value.Detect(originSrc, RapidOcrOptions.PPOCRv6 with { ReturnWordBox = true });

            VisualDebugBbox(Path.ChangeExtension(path, "_ocr_word_v6.png"), originSrc, ocrResult);

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
    [MemberData(nameof(TesseractImagesV6))]
    public void TesseractOcrTextBlockV6(string path, string[] expected)
    {
        path = Path.Combine("images_tesseract", path);

        Assert.True(File.Exists(path));

        using (SKBitmap originSrc = SKBitmap.Decode(path))
        {
            OcrResult ocrResult = V6SmallEngine.Value.Detect(originSrc, RapidOcrOptions.PPOCRv6);

            VisualDebugBbox(Path.ChangeExtension(path, "_ocr_v6.png"), originSrc, ocrResult);

            var actual = ocrResult.TextBlocks.Select(b => b.Text).ToArray();
            Assert.NotNull(actual);
            Assert.Equal(expected.Length, actual.Length);

            for (int s = 0; s < expected.Length; s++)
            {
                Assert.Equal(expected[s], actual[s]);
            }
        }
    }

    private static void VisualDebugBbox(string output, SKBitmap image, OcrResult ocrResult)
    {
        // Visual bounding boxes check
        foreach (var block in ocrResult.TextBlocks)
        {
            var points = block.BoxPoints;
            using (var canvas = new SKCanvas(image))
            using (var redPaint = new SKPaint() { Color = SKColors.Red })
            using (var greenPaint = new SKPaint() { Color = SKColors.LimeGreen })
            {
                canvas.DrawLine(points[0], points[1], redPaint);
                canvas.DrawLine(points[1], points[2], redPaint);
                canvas.DrawLine(points[2], points[3], redPaint);
                canvas.DrawLine(points[3], points[0], redPaint);

                if (block.WordResults is not null)
                {
                    foreach (var word in block.WordResults)
                    {
                        Console.WriteLine($"   {word}");
                        var wp = word.BoxPoints;
                        canvas.DrawLine(wp[0], wp[1], greenPaint);
                        canvas.DrawLine(wp[1], wp[2], greenPaint);
                        canvas.DrawLine(wp[2], wp[3], greenPaint);
                        canvas.DrawLine(wp[3], wp[0], greenPaint);
                    }
                }
            }
        }

        using (var fs = new FileStream(output, FileMode.Create))
        {
            image.Encode(fs, SKEncodedImageFormat.Png, 100);
        }
    }
}
