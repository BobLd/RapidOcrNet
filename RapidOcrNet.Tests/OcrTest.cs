using SkiaSharp;

namespace RapidOcrNet.Tests
{
    public class OcrTest : IDisposable
    {
        // Most tests that are failing are due to wrong detected angle classification

        public static IEnumerable<object[]> Images => new[]
        {
            /*
            new object[]
            {
                "issue_170.png", // Gray8
                new string[]
                {
                    "TEST"
                }
            },
            new object[]
            {
                "1997.png",
                new string[]
                {
                    "1997"
                }
            },
            */
            new object[]
            {
                "rotated.PNG",
                new string[]
                {
                    "This is some angled text"
                }
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
                    "" // Not correct, should be "VEHICLES"
                }
            },
            new object[]
            {
                "img_195.jpg",
                new string[]
                {
                    "",
                    "EXPERIENCE",
                    "Open to Public.",
                    "FIBRE HERE",
                    "Free Admission."
                }
            },
            new object[]
            {
                "bold-italic_1.png",
                new string[]
                {
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit."
                }
            },
            new object[]
            {
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
                "blank.png",
                new string[] { }
            },
            new object[]
            {
                "empty.png",
                new string[] { }
            },
            new object[]
            {
                "Fonts.png",
                new string[]
                {
                    "Bold Italic Fixed Serif CaPitAl 123 x² y3" // not exact but good enough
                }
            },
            new object[]
            {
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
                "PSM_SingleBlock.png",
                new string[]
                {
                    "This is a lot of 12 point text to test the",
                    "ocr code and see if it works on all types",
                    "of file format."
                }
            },
            /*
            new object[]
            {
                "PSM_SingleBlockVertText.png",
                new string[]
                {
                    "A", "l", "i", "n", "e", "o", "f", "t", "e", "x", "t"
                }
            },
            */
            new object[]
            {
                "PSM_SingleColumn.png",
                new string[]
                {
                    "This is a lot of 12 point text to test the",
                }
            },
            new object[]
            {
                "PSM_SingleChar.png",
                new string[]
                {
                    "T"
                }
            },
            new object[]
            {
                "PSM_SingleLine.png",
                new string[]
                {
                    "This is a lot of 12 point text to test the",
                }
            },
            /*
            new object[]
            {
                "PSM_SingleWord.png",
                new string[]
                {
                    "This"
                }
            },
            */
            new object[]
            {
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
        public void TesseractOcrText(string path, string[] expected)
        {
            path = Path.Combine("images_tesseract", path);

            Assert.True(File.Exists(path));

            using (SKBitmap originSrc = SKBitmap.Decode(path))
            {
                OcrResult ocrResult = _ocrEngin.Detect(originSrc, RapidOcrOptions.Default);

                VisualDebugBbox(Path.ChangeExtension(path, "_ocr.png"), originSrc, ocrResult);

                var actual = ocrResult.TextBlocks.Select(b => b.Chars).ToArray();
                Assert.NotNull(actual);
                Assert.Equal(expected.Length, actual.Length);

                for (int s = 0; s < expected.Length; s++)
                {
                    string expectedSentence = expected[s];
                    string[]? actualSentence = actual[s];
                    Assert.NotNull(actualSentence);
                    Assert.Equal(expectedSentence.Length, actualSentence.Length);

                    for (int c = 0; c < expectedSentence.Length; c++)
                    {
                        Assert.Equal(expectedSentence[c].ToString(), actualSentence[c]);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(Images))]
        public void OcrText(string path, string[] expected)
        {
            path = Path.Combine("images", path);

            Assert.True(File.Exists(path));

            using (SKBitmap originSrc = SKBitmap.Decode(path))
            {
                OcrResult ocrResult = _ocrEngin.Detect(originSrc, RapidOcrOptions.Default);

                VisualDebugBbox(Path.ChangeExtension(path, "_ocr.png"), originSrc, ocrResult);

                var actual = ocrResult.TextBlocks.Select(b => b.Chars).ToArray();
                Assert.NotNull(actual);

                Assert.Equal(expected.Length, actual.Length);

                for (int s = 0; s < expected.Length; s++)
                {
                    string expectedSentence = expected[s];
                    
                    string[]? actualSentence = actual[s];
                    Assert.NotNull(actualSentence);
                    Assert.Equal(expectedSentence.Length, actualSentence.Length);

                    for (int c = 0; c < expectedSentence.Length; c++)
                    {
                        Assert.Equal(expectedSentence[c].ToString(), actualSentence[c]);
                    }
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
                using (var paint = new SKPaint() { Color = SKColors.Red })
                {
                    canvas.DrawLine(points[0], points[1], paint);
                    canvas.DrawLine(points[1], points[2], paint);
                    canvas.DrawLine(points[2], points[3], paint);
                    canvas.DrawLine(points[3], points[0], paint);
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
}