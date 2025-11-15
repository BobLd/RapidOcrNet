// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/OcrUtils.cs

using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidOcrNet
{
    internal static class OcrUtils
    {
        public static Tensor<float> SubtractMeanNormalize(SKBitmap src, float[] meanVals, float[] normVals)
        {
            int cols = src.Width;
            int rows = src.Height;
            int channels = src.BytesPerPixel;

            const int expChannels = 3; // Size of meanVals, we ignore alpha channel

            Tensor<float> inputTensor = new DenseTensor<float>([1, expChannels, rows, cols]);

            ReadOnlySpan<byte> span = src.GetPixelSpan();

            if (src.Info.ColorType == SKColorType.Gray8)
            {
                for (int r = 0; r < rows; ++r)
                {
                    for (int c = 0; c < cols; ++c)
                    {
                        int i = r * cols + c;
                        byte value = span[i * channels];
                        inputTensor[0, 0, r, c] = (value - meanVals[0]) * normVals[0];
                        inputTensor[0, 1, r, c] = (value - meanVals[1]) * normVals[1];
                        inputTensor[0, 2, r, c] = (value - meanVals[2]) * normVals[2];
                    }
                }
            }
            else if (src.Info.ColorType == SKColorType.Bgra8888)
            {
                for (int r = 0; r < rows; ++r)
                {
                    for (int c = 0; c < cols; ++c)
                    {
                        int i = r * cols + c;
                        for (int ch = 0; ch < expChannels; ++ch)
                        {
                            byte value = span[i * channels + ch];
                            inputTensor[0, ch, r, c] = (value - meanVals[ch]) * normVals[ch];
                        }
                    }
                }
            }
            else
            {
                throw new ArgumentException($"This image needs to be '{SKColorType.Bgra8888}' or '{SKColorType.Gray8}', but got '{src.Info.ColorType}'.");
            }


            return inputTensor;
        }

        public static SKBitmap MakePadding(SKBitmap src, int padding)
        {
            if (padding <= 0)
            {
                return src;
            }

            SKImageInfo info = src.Info;

            info.Width += 2 * padding;
            info.Height += 2 * padding;

            SKBitmap newBmp = new SKBitmap(info);
            using (var canvas = new SKCanvas(newBmp))
            using (var paint = new SKPaint())
            using (var image = SKImage.FromBitmap(src))
            {
                paint.IsAntialias = false;

                canvas.Clear(SKColors.White);
                canvas.DrawImage(image, padding, padding, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
            }

#if DEBUG
            using (var fs = new FileStream($"Padding_{Guid.NewGuid()}.png", FileMode.Create))
            {
                newBmp.Encode(fs, SKEncodedImageFormat.Png, 100);
            }
#endif

            return newBmp;
        }

        public static int GetThickness(SKBitmap boxImg)
        {
            int minSize = boxImg.Width > boxImg.Height ? boxImg.Height : boxImg.Width;
            return minSize / 1000 + 2;
        }

        public static IEnumerable<SKBitmap> GetPartImages(SKBitmap src, IReadOnlyList<TextBox> textBoxes)
        {
            for (int i = 0; i < textBoxes.Count; ++i)
            {
                yield return GetRotateCropImage(src, textBoxes[i].Points);
            }
        }

        public static SKMatrix GetPerspectiveTransform(SKPoint topLeft, SKPoint topRight, SKPoint botRight, SKPoint botLeft,
            float width, float height)
        {
            // https://stackoverflow.com/questions/48416118/perspective-transform-in-skia

            float x1 = topLeft.X;
            float y1 = topLeft.Y;
            float x2 = topRight.X;
            float y2 = topRight.Y;
            float x3 = botRight.X;
            float y3 = botRight.Y;
            float x4 = botLeft.X;
            float y4 = botLeft.Y;

            float w = width;
            float h = height;

            float scaleX = (y1 * x2 * x4 - x1 * y2 * x4 + x1 * y3 * x4 - x2 * y3 * x4 - y1 * x2 * x3 + x1 * y2 * x3 - x1 * y4 * x3 + x2 * y4 * x3) / (x2 * y3 * w + y2 * x4 * w - y3 * x4 * w - x2 * y4 * w - y2 * w * x3 + y4 * w * x3);
            float skewX = (-x1 * x2 * y3 - y1 * x2 * x4 + x2 * y3 * x4 + x1 * x2 * y4 + x1 * y2 * x3 + y1 * x4 * x3 - y2 * x4 * x3 - x1 * y4 * x3) / (x2 * y3 * h + y2 * x4 * h - y3 * x4 * h - x2 * y4 * h - y2 * h * x3 + y4 * h * x3);
            float transX = x1;
            float skewY = (-y1 * x2 * y3 + x1 * y2 * y3 + y1 * y3 * x4 - y2 * y3 * x4 + y1 * x2 * y4 - x1 * y2 * y4 - y1 * y4 * x3 + y2 * y4 * x3) / (x2 * y3 * w + y2 * x4 * w - y3 * x4 * w - x2 * y4 * w - y2 * w * x3 + y4 * w * x3);
            float scaleY = (-y1 * x2 * y3 - y1 * y2 * x4 + y1 * y3 * x4 + x1 * y2 * y4 - x1 * y3 * y4 + x2 * y3 * y4 + y1 * y2 * x3 - y2 * y4 * x3) / (x2 * y3 * h + y2 * x4 * h - y3 * x4 * h - x2 * y4 * h - y2 * h * x3 + y4 * h * x3);
            float transY = y1;
            float persp0 = (x1 * y3 - x2 * y3 + y1 * x4 - y2 * x4 - x1 * y4 + x2 * y4 - y1 * x3 + y2 * x3) / (x2 * y3 * w + y2 * x4 * w - y3 * x4 * w - x2 * y4 * w - y2 * w * x3 + y4 * w * x3);
            float persp1 = (-y1 * x2 + x1 * y2 - x1 * y3 - y2 * x4 + y3 * x4 + x2 * y4 + y1 * x3 - y4 * x3) / (x2 * y3 * h + y2 * x4 * h - y3 * x4 * h - x2 * y4 * h - y2 * h * x3 + y4 * h * x3);
            float persp2 = 1;

            var persp = new SKMatrix(scaleX, skewX, transX, skewY, scaleY, transY, persp0, persp1, persp2);
            return persp.TryInvert(out SKMatrix perspInv) ? perspInv : SKMatrix.Identity; // TODO - Check what's best to return when not inv
        }

        public static SKBitmap GetRotateCropImage(SKBitmap src, SKPointI[] box)
        {
            System.Diagnostics.Debug.Assert(box.Length == 4);
            Span<SKPointI> points = stackalloc SKPointI[] { box[0], box[1], box[2], box[3] }; // Clone points
            ReadOnlySpan<int> collectX = stackalloc int[] { box[0].X, box[1].X, box[2].X, box[3].X };
            int left = int.MaxValue;
            int right = int.MinValue;
            foreach (var v in collectX)
            {
                if (v < left)
                {
                    left = v;
                }
                else if (v > right)
                {
                    right = v;
                }
            }

            ReadOnlySpan<int> collectY = stackalloc int[] { box[0].Y, box[1].Y, box[2].Y, box[3].Y };
            int top = int.MaxValue;
            int bottom = int.MinValue;
            foreach (var v in collectY)
            {
                if (v < top)
                {
                    top = v;
                }
                else if (v > bottom)
                {
                    bottom = v;
                }
            }

            SKRectI rect = new SKRectI(left, top, right, bottom);

            var info = src.Info;
            info.Width = rect.Width;
            info.Height = rect.Height;

            SKBitmap imgCrop = new SKBitmap(info);
            if (!src.ExtractSubset(imgCrop, rect))
            {
                throw new Exception("Could not extract image subset.");
            }

            ref SKPointI p0 = ref points[0];
            p0.X -= left;
            p0.Y -= top;

            ref SKPointI p1 = ref points[1];
            p1.X -= left;
            p1.Y -= top;

            ref SKPointI p2 = ref points[2];
            p2.X -= left;
            p2.Y -= top;

            ref SKPointI p3 = ref points[3];
            p3.X -= left;
            p3.Y -= top;

            int imgCropWidth = (int)Math.Sqrt((p0.X - p1.X) * (p0.X - p1.X) + (p0.Y - p1.Y) * (p0.Y - p1.Y));
            int imgCropHeight = (int)Math.Sqrt((p0.X - p3.X) * (p0.X - p3.X) + (p0.Y - p3.Y) * (p0.Y - p3.Y));

            var ptsSrc0Sk = new SKPoint(p0.X, p0.Y);
            var ptsSrc1Sk = new SKPoint(p1.X, p1.Y);
            var ptsSrc2Sk = new SKPoint(p2.X, p2.Y);
            var ptsSrc3Sk = new SKPoint(p3.X, p3.Y);

            var m = GetPerspectiveTransform(ptsSrc0Sk, ptsSrc1Sk, ptsSrc2Sk, ptsSrc3Sk, imgCropWidth, imgCropHeight);

            if (m.IsIdentity)
            {
                if (imgCrop.Height >= imgCrop.Width * 1.5)
                {
                    return BitmapRotateClockWise90(imgCrop);
                }

                return imgCrop;
            }

            var info2 = imgCrop.Info;
            info2.Width = imgCropWidth;
            info2.Height = imgCropHeight;

            var partImg = new SKBitmap(info2);
            using (var canvas = new SKCanvas(partImg))
            using (var paint = new SKPaint())
            using (var image = SKImage.FromBitmap(imgCrop))
            {
                paint.IsAntialias = false;

                canvas.SetMatrix(m);
                canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
                canvas.Restore();
            }

            //#if DEBUG
            //            using (var fs = new FileStream($"perspective_{Guid.NewGuid()}.png", FileMode.Create))
            //            {
            //                partImg.Encode(fs, SKEncodedImageFormat.Png, 100);
            //            }
            //#endif

            if (partImg.Height >= partImg.Width * 1.5)
            {
                return BitmapRotateClockWise90(partImg);
            }

            return partImg;
        }

        public static SKBitmap BitmapRotateClockWise180(SKBitmap src)
        {
            var rotated = new SKBitmap(src.Info);
            
            using (var canvas = new SKCanvas(rotated))
            using (var paint = new SKPaint())
            using (var image = SKImage.FromBitmap(src))
            {
                paint.IsAntialias = false;

                canvas.Translate(rotated.Width, rotated.Height);
                canvas.RotateDegrees(180);
                canvas.DrawImage(image,0,0, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
                canvas.Restore();
            }

            return rotated;
        }

        public static SKBitmap BitmapRotateClockWise90(SKBitmap src)
        {
            var info = src.Info;
            (info.Width, info.Height) = (info.Height, info.Width);

            var rotated = new SKBitmap(info);

            using (var canvas = new SKCanvas(rotated))
            using (var paint = new SKPaint())
            using (var image = SKImage.FromBitmap(src))
            {
                paint.IsAntialias = false;

                canvas.Translate(rotated.Width, 0);
                canvas.RotateDegrees(90);
                canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
                canvas.Restore();
            }
            /*
            using (var fs = new FileStream("rotated.png", FileMode.Create))
            {
                rotated.Encode(fs, SKEncodedImageFormat.Png, 100);
            }
            */
            return rotated;
        }
    }
}
