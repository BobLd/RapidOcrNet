using SkiaSharp;

namespace RapidOcrNet.Tests;

public class OcrTest : IDisposable
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
                    "CAPITAL", // OK`
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
                // Status: currently failing, should pass - expected text is 100% correct
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
                // Status: currently failing, should pass - expected text is 100% correct
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


    private readonly RapidOcr _ocrEngin;

    public OcrTest()
    {
        _ocrEngin = new RapidOcr();
        _ocrEngin.InitModels();
    }

    [Theory]
    [MemberData(nameof(TesseractImages))]
    public void TesseractOcrTextBlock(string path, string[] expected)
    {
        path = Path.Combine("images_tesseract", path);

        Assert.True(File.Exists(path));

        using (SKBitmap originSrc = SKBitmap.Decode(path))
        {
            OcrResult ocrResult = _ocrEngin.Detect(originSrc, RapidOcrOptions.Default);

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
            OcrResult ocrResult = _ocrEngin.Detect(originSrc, RapidOcrOptions.Default);

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
            OcrResult ocrResult = _ocrEngin.Detect(originSrc, RapidOcrOptions.Default with { ReturnWordBox = true });

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

    public void Dispose()
    {
        _ocrEngin.Dispose();
    }
}
