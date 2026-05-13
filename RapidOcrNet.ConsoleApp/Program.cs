using SkiaSharp;

namespace RapidOcrNet.ConsoleApp;

internal class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            args = new string[]
            {
                    "img_10.jpg",
                    "rotated.PNG",
                    "rotated2.PNG",
                    "1997.png",
                    "5090.FontNameList.1_raw.png",
                    "5090.FontNameList.2_raw.png"
            };
        }

        using var ocrEngin = new RapidOcr();
        ocrEngin.InitModels();

        foreach (var path in args)
        {
            ProcessImage(ocrEngin, path);
        }

        Console.WriteLine("Bye, RapidOcrNet!");
    }

    static void ProcessImage(RapidOcr ocrEngin, string targetImg)
    {
        Console.WriteLine($"Processing {targetImg}");
        using (SKBitmap originSrc = SKBitmap.Decode(targetImg))
        {
            var options = RapidOcrOptions.Default with { ReturnWordBox = true };
            OcrResult ocrResult = ocrEngin.Detect(originSrc, options);
            Console.WriteLine(ocrResult.ToString());
            Console.WriteLine(ocrResult.StrRes);

            foreach (var block in ocrResult.TextBlocks)
            {
                var points = block.BoxPoints;
                using (var canvas = new SKCanvas(originSrc))
                using (var redPaint = new SKPaint() { Color = SKColors.Red })
                using (var greenPaint = new SKPaint() { Color = SKColors.Green })
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

            Console.WriteLine();

            using (var fs = new FileStream(Path.ChangeExtension(targetImg, "_ocr.png"), FileMode.Create))
            {
                originSrc.Encode(fs, SKEncodedImageFormat.Png, 100);
            }
        }
    }
}

