# RapidOcrNet
Cross-platform OCR processing library using PaddleOCR ONNX models, and based on original code from RapidAI's [RapidOCR](https://github.com/RapidAI/RapidOCR).

Available as NuGet package here https://www.nuget.org/packages/RapidOcrNet/

The code was optimised to remove dependencies on `System.Drawing` and `OpenCV`. The image processing is now done only using `SkiaSharp` and `PContourNet`.

The project now uses PP-OCR v5 models, but v4 and v3 models are also supported (see [here](https://github.com/BobLd/RapidOcrNet/issues/3)).

All ONNX models and files and can be downloaded from: https://github.com/RapidAI/RapidOCR/blob/main/python/rapidocr/default_models.yaml
You will need 4 different files for the code to work. Example below for PP-OCR v5 with latin language:
- Detection: `ch_PP-OCRv5_mobile_det.onnx`
- Classification: `ch_ppocr_mobile_v2.0_cls_infer.onnx`
- Recognition: `latin_PP-OCRv5_rec_mobile_infer.onnx`
- Model dictionary: `ppocrv5_latin_dict.txt`

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

The models made available are from the PaddleOCR project (Apache-2.0 license) and were downloaded from https://github.com/RapidAI/RapidOCR/blob/main/python/rapidocr/default_models.yaml
