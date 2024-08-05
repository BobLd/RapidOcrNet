# RapidOcrNet
Cross-platform OCR processing library using PaddleOCR ONNX models, and based on original code from RapidAI's [RapidOCR](https://github.com/RapidAI/RapidOCR).

The code was optimised to remove dependencies on `System.Drawing` and `OpenCV`. The image processing is now done only using `SkiaSharp` and `PContourNet`.

## Usage
```csharp
string targetImg = "image.png";

using (var ocrEngin = new RapidOcr())
{
	ocrEngin.InitModels();
	using (SKBitmap originSrc = SKBitmap.Decode(targetImg))
	{
		OcrResult ocrResult = ocrEngin.Detect(originSrc, RapidOcrOptions.Default);
		Console.WriteLine(ocrResult.ToString());
		Console.WriteLine(ocrResult.StrRes);
		Console.WriteLine();

		// Draw bounding boxes
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
```
## Notice
Based on source code originally developed in the RapidOCR project (Apache-2.0 license).
- https://github.com/RapidAI/RapidOCR

Uses parts of source code originally developed in the PdfPig project (Apache-2.0 license).
- https://github.com/UglyToad/PdfPig

The dependency on OpenCV was removed thanks to the PContour library and its C# port.
- https://github.com/LingDong-/PContour
- https://github.com/BobLd/PContourNet

The models made available are from the PaddleOCR project (Apache-2.0 license) and were converted to ONNX using the Paddle2ONNX tool.
- https://paddlepaddle.github.io/PaddleOCR/en/ppocr/model_list.html
- https://github.com/PaddlePaddle/Paddle2ONNX
