using SkiaSharp;

namespace RapidOcrNet.Tests;

public class OcrTestV5Latin
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

    [Theory]
    [MemberData(nameof(TesseractImages))]
    public void TesseractOcrTextBlock(string path, string[] expected)
    {
        path = Path.Combine("images_tesseract", path);

        Assert.True(File.Exists(path));

        using (SKBitmap originSrc = SKBitmap.Decode(path))
        {
            OcrResult ocrResult = V5Engine.Value.Detect(originSrc, RapidOcrOptions.Default);

            Helper.VisualDebugBbox(Path.ChangeExtension(path, "_ocr_v5.png"), originSrc, ocrResult);

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

            Helper.VisualDebugBbox(Path.ChangeExtension(path, "_ocr_v5.png"), originSrc, ocrResult);

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

            Helper.VisualDebugBbox(Path.ChangeExtension(path, "_ocr_word_v5.png"), originSrc, ocrResult);

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
}
