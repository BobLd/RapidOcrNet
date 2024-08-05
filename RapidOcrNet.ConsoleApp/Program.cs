using SkiaSharp;

namespace RapidOcrNet.ConsoleApp
{
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
                OcrResult ocrResult = ocrEngin.Detect(originSrc, RapidOcrOptions.Default);
                Console.WriteLine(ocrResult.ToString());
                Console.WriteLine(ocrResult.StrRes);
                Console.WriteLine();

                foreach (var block in ocrResult.TextBlocks)
                {
                    var points = block.BoxPoints;
                    using (var canvas = new SKCanvas(originSrc))
                    using (var paint = new SKPaint() { Color = SKColors.Red })
                    {
                        canvas.DrawLine(points[0], points[1], paint);
                        canvas.DrawLine(points[1], points[2], paint);
                        canvas.DrawLine(points[2], points[3], paint);
                        canvas.DrawLine(points[3], points[0], paint);
                    }
                }

                using (var fs = new FileStream(Path.ChangeExtension(targetImg, "_ocr.png"), FileMode.Create))
                {
                    originSrc.Encode(fs, SKEncodedImageFormat.Png, 100);
                }
            }
        }
    }
}
