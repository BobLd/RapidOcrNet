// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/DbNet.cs

using System.Runtime.InteropServices;
using Clipper2Lib;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidOcrNet
{
    public sealed class TextDetector : IDisposable
    {
        private readonly float[] MeanValues = [0.485F * 255F, 0.456F * 255F, 0.406F * 255F];
        private readonly float[] NormValues = [1.0F / 0.229F / 255.0F, 1.0F / 0.224F / 255.0F, 1.0F / 0.225F / 255.0F];

        private InferenceSession _dbNet;
        private string _inputName;

        public void InitModel(string path, int numThread)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Detector model file does not exist: '{path}'.");
            }

            var op = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
                InterOpNumThreads = numThread,
                IntraOpNumThreads = numThread
            };

            _dbNet = new InferenceSession(path, op);
            _inputName = _dbNet.InputMetadata.Keys.First();
        }

        public IReadOnlyList<TextBox> GetTextBoxes(SKBitmap src, ScaleParam scale, float boxScoreThresh, float boxThresh,
            float unClipRatio)
        {
            Tensor<float> inputTensors;
            using (var srcResize = src.Resize(new SKSizeI(scale.DstWidth, scale.DstHeight), SKFilterQuality.High))
            {
#if DEBUG
                using (var fs = new FileStream($"Detector_{Guid.NewGuid()}.png", FileMode.Create))
                {
                    srcResize.Encode(fs, SKEncodedImageFormat.Png, 100);
                }
#endif

                inputTensors = OcrUtils.SubtractMeanNormalize(srcResize, MeanValues, NormValues);
            }

            IReadOnlyCollection<NamedOnnxValue> inputs = new NamedOnnxValue[]
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensors)
            };

            try
            {
                using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _dbNet.Run(inputs))
                {
                    return GetTextBoxes(results[0], scale.DstHeight, scale.DstWidth, scale, boxScoreThresh,
                        boxThresh, unClipRatio);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + ex.StackTrace);
            }

            return null;
        }

        private static SKPoint[][] FindContours(byte[] array, int rows, int cols)
        {
            Span<int> v = Array.ConvertAll(array, c => (int)c);
            var contours = PContour.FindContours(v, cols, rows);
            return contours
                .Where(c => !c.isHole)
                .Select(c => PContour.ApproxPolyDP(c.GetSpan(), 1).ToArray())
                .ToArray();
        }

        private static bool TryFindIndex(Dictionary<int, int> link, int offset, out int index)
        {
            bool found = false;
            index = offset;
            while (link.TryGetValue(index, out int newIndex))
            {
                found = true;
                if (index == newIndex) break;
                index = newIndex;
            }
            return found;
        }

        private static IReadOnlyList<TextBox> GetTextBoxes(DisposableNamedOnnxValue outputTensor, int rows, int cols, ScaleParam s, float boxScoreThresh, float boxThresh, float unClipRatio)
        {
            const float maxSideThresh = 3.0f; // Long Edge Threshold
            var rsBoxes = new List<TextBox>();

            // Data preparation
            ReadOnlySpan<float> predData = outputTensor.AsEnumerable<float>().ToArray();

            var gray8 = new SKImageInfo()
            {
                Height = rows,
                Width = cols,
                AlphaType = SKAlphaType.Opaque,
                ColorType = SKColorType.Gray8
            };

            var crop = new SKRectI(0, 0, cols, rows);

            Span<byte> thresholdMat = new byte[predData.Length];
            Span<byte> cbufMat = new byte[predData.Length];

            for (int i = 0; i < predData.Length; i++)
            {
                var f = predData[i];
                cbufMat[i] = Convert.ToByte(f * 255);
                thresholdMat[i] = f > boxThresh ? (byte)1 : (byte)0; // Thresholding
            }

            const float dilateRadius = 1f;

            SKPoint[][] contours;
            using (var skImage = SKImage.FromPixelCopy(gray8, thresholdMat))
            using (var dilateFilter = SKImageFilter.CreateDilate(dilateRadius, dilateRadius))
            using (var dilated = skImage.ApplyImageFilter(dilateFilter, crop, crop, out SKRectI _, out SKPointI _)) // Dilate
            using (var dilateMat = dilated.Subset(crop)) // Trim image due to dilate
            {
//#if DEBUG
//                using (var skImage2 = SKImage.FromPixelCopy(gray8, cbufMat))
//                using (var bmp = SKBitmap.FromImage(skImage2))
//                using (var fs = new FileStream($"result_{Guid.NewGuid()}.png", FileMode.Create))
//                {
//                    bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
//                }
//#endif

                nint buffer = Marshal.AllocHGlobal(gray8.BytesSize);
                try
                {
                    dilateMat.ReadPixels(gray8, buffer);
                    byte[] bytes = new byte[thresholdMat.Length];

                    Marshal.Copy(buffer, bytes, 0, thresholdMat.Length);

                    contours = FindContours(bytes, rows, cols);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            using (SKImage predImage = SKImage.FromPixelCopy(gray8, cbufMat))
            {
                for (int i = 0; i < contours.Length; i++)
                {
                    var contour = contours[i];
                    if (contour.Length <= 2)
                    {
                        continue;
                    }

                    SKPoint[] minBox = GetMiniBox(contour, out float maxSide);
                    if (maxSide < maxSideThresh)
                    {
                        continue;
                    }

                    double score = GetScore(contour, predImage);
                    if (score < boxScoreThresh)
                    {
                        continue;
                    }

                    SKPoint[]? clipBox = Unclip(minBox, unClipRatio);
                    if (clipBox is null)
                    {
                        continue;
                    }

                    ReadOnlySpan<SKPoint> clipMinBox = GetMiniBox(clipBox, out maxSide);
                    if (maxSide < maxSideThresh + 2)
                    {
                        continue;
                    }

                    var finalPoints = new SKPointI[clipMinBox.Length];
                    for (int j = 0; j < clipMinBox.Length; j++)
                    {
                        var item = clipMinBox[j];
                        int x = (int)(item.X / s.ScaleWidth);
                        int ptx = Math.Min(Math.Max(x, 0), s.SrcWidth);

                        int y = (int)(item.Y / s.ScaleHeight);
                        int pty = Math.Min(Math.Max(y, 0), s.SrcHeight);

                        finalPoints[j] = new SKPointI(ptx, pty);
                    }

                    var textBox = new TextBox
                    {
                        Score = (float)score,
                        Points = finalPoints
                    };
                    rsBoxes.Add(textBox);
                }
            }

            //rsBoxes.Reverse();
            return rsBoxes;
        }

        private static SKPoint[] GetMiniBox(SKPoint[] contours, out float minEdgeSize)
        {
            SKPoint[] points = GeometryExtensions.MinimumAreaRectangle(contours);

            GeometryExtensions.GetSize(points, out float width, out float height);
            minEdgeSize = MathF.Min(width, height);

            Array.Sort(points, CompareByX);

            int index1 = 0;
            int index2 = 1;
            int index3 = 2;
            int index4 = 3;

            if (points[1].Y > points[0].Y)
            {
                index1 = 0;
                index4 = 1;
            }
            else
            {
                index1 = 1;
                index4 = 0;
            }

            if (points[3].Y > points[2].Y)
            {
                index2 = 2;
                index3 = 3;
            }
            else
            {
                index2 = 3;
                index3 = 2;
            }

            return new SKPoint[] { points[index1], points[index2], points[index3], points[index4] };
        }

        public static int CompareByX(SKPoint left, SKPoint right)
        {
            if (left.X > right.X)
            {
                return 1;
            }

            if (left.X == right.X)
            {
                return 0;
            }

            return -1;
        }

        private static double GetScore(SKPoint[] contours, SKImage fMapMat)
        {
            short xmin = 9999;
            short xmax = 0;
            short ymin = 9999;
            short ymax = 0;

            try
            {
                foreach (SKPoint point in contours)
                {
                    if (point.X < xmin)
                    {
                        xmin = (short)point.X;
                    }

                    if (point.X > xmax)
                    {
                        xmax = (short)point.X;
                    }

                    if (point.Y < ymin)
                    {
                        ymin = (short)point.Y;
                    }

                    if (point.Y > ymax)
                    {
                        ymax = (short)point.Y;
                    }
                }

                int roiWidth = xmax - xmin + 1;
                int roiHeight = ymax - ymin + 1;

                var gray8 = new SKImageInfo()
                {
                    Height = roiHeight,
                    Width = roiWidth,
                    AlphaType = SKAlphaType.Opaque,
                    ColorType = SKColorType.Gray8
                };

                byte[] roiBitmapBytes = new byte[gray8.BytesSize];

                using (SKImage roiBitmap = fMapMat.Subset(new SKRectI(xmin - 1, ymin - 1, xmax, ymax)))
                {
                    System.Diagnostics.Debug.Assert(roiBitmap.Width.Equals(roiWidth));
                    System.Diagnostics.Debug.Assert(roiBitmap.Height.Equals(roiHeight));

                    nint buffer = Marshal.AllocHGlobal(gray8.BytesSize);
                    try
                    {
                        roiBitmap.ReadPixels(gray8, buffer);
                        Marshal.Copy(buffer, roiBitmapBytes, 0, gray8.BytesSize);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }

                double sum = 0;
                int count = 0;

                using (SKBitmap mask = new SKBitmap(gray8))
                using (SKCanvas canvas = new SKCanvas(mask))
                using (SKPaint maskPaint = new SKPaint())
                {
                    maskPaint.Color = SKColors.White;
                    maskPaint.Style = SKPaintStyle.Fill;

                    canvas.Clear(SKColors.Black);

                    using (var path = new SKPath())
                    {
                        SKPoint first = contours[0];
                        path.MoveTo(first.X - xmin, first.Y - ymin);
                        for (int p = 1; p < contours.Length; p++)
                        {
                            SKPoint point = contours[p];
                            path.LineTo(point.X - xmin, point.Y - ymin);
                        }
                        path.Close();

                        canvas.DrawPath(path, maskPaint);
                    }

//#if DEBUG
//                    using (var fs = new FileStream($"mask_{Guid.NewGuid()}.png", FileMode.Create))
//                    {
//                        mask.Encode(fs, SKEncodedImageFormat.Png, 100);
//                    }
//#endif

                    ReadOnlySpan<byte> maskSpan = mask.GetPixelSpan();
                    
                    System.Diagnostics.Debug.WriteLine(maskSpan.Length.Equals(roiBitmapBytes.Length));

                    for (int i = 0; i < maskSpan.Length; i++)
                    {
                        if (maskSpan[i] == 0)
                        {
                            continue;
                        }
                        sum += roiBitmapBytes[i];
                        count++;
                    }
                }

                if (count == 0)
                {
                    return 0;
                }
                return sum / count / byte.MaxValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + ex.StackTrace);
            }

            return 0;
        }

        private static SKPoint[]? Unclip(SKPoint[] box, float unclipRatio)
        {
            SKPoint[] points = GeometryExtensions.MinimumAreaRectangle(box);
            GeometryExtensions.GetSize(points, out float width, out float height);

            if (height < 1.001 && width < 1.001)
            {
                return null;
            }

            var theClipperPts = new Path64(box.Select(pt => new Point64(pt.X, pt.Y)));

            float area = MathF.Abs(SignedPolygonArea(box));
            double length = LengthOfPoints(box);
            double distance = area * unclipRatio / length;

            var co = new ClipperOffset();
            co.AddPath(theClipperPts, JoinType.Round, EndType.Polygon);
            var solution = new Paths64();
            co.Execute(distance, solution);
            if (solution.Count == 0)
            {
                return null;
            }

            var unclipped = solution[0];

            var retPts = new SKPoint[unclipped.Count];
            for (int i = 0; i < unclipped.Count; ++i)
            {
                var ip = unclipped[i];
                retPts[i] = new SKPoint((int)ip.X, (int)ip.Y);
            }

            return retPts;
        }

        private static float SignedPolygonArea(SKPoint[] points)
        {
            // Get the areas.
            float area = 0;
            for (int i = 0; i < points.Length - 1; i++)
            {
                area +=
                    (points[i + 1].X - points[i].X) *
                    (points[i + 1].Y + points[i].Y) / 2;
            }

            area +=
                (points[0].X - points[points.Length - 1].X) *
                (points[0].Y + points[points.Length - 1].Y) / 2;

            return area;
        }

        private static double LengthOfPoints(SKPoint[] box)
        {
            double length = 0;

            SKPoint pt = box[0];
            double x0 = pt.X;
            double y0 = pt.Y;

            for (int idx = 1; idx < box.Length; idx++)
            {
                SKPoint pts = box[idx];
                double x1 = pts.X;
                double y1 = pts.Y;
                double dx = x1 - x0;
                double dy = y1 - y0;

                length += Math.Sqrt(dx * dx + dy * dy);

                x0 = x1;
                y0 = y1;
            }

            // Compute distance from last point to first point (closed loop)
            var dxL = pt.X - x0;
            var dyL = pt.Y - y0;
            length += Math.Sqrt(dxL * dxL + dyL * dyL);

            return length;
        }

        public void Dispose()
        {
            _dbNet.Dispose();
        }
    }
}
