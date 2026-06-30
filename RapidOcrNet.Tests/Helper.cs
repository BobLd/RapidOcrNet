using SkiaSharp;

namespace RapidOcrNet.Tests;

internal static class Helper
{
    public static void VisualDebugBbox(string output, SKBitmap image, OcrResult ocrResult)
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