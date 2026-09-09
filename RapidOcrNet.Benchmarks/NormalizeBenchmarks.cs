// Apache-2.0 license

using BenchmarkDotNet.Attributes;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidOcrNet.Benchmarks;

/// <summary>
/// Same question as <see cref="CtcDecodingBenchmarks"/> (GitHub issue #49), applied to
/// <see cref="OcrUtils.SubtractMeanNormalize"/>: that method builds the input tensor for
/// every single detector, classifier and recognizer inference, one <c>inputTensor[index, ch,
/// r, c] = value</c> multi-dimensional <see cref="Tensor{T}"/> write per pixel per channel -
/// the write-side mirror of the read-side problem the issue describes. Unlike the CTC decode
/// loop, this path runs on <i>every</i> inference regardless of dictionary size, so a large
/// detector input (the pipeline benchmarks' "en.jpg" case feeds the detector ~3956x736) can
/// touch this loop ~8.7M times for one page.
/// </summary>
/// <remarks>
/// <c>Cols</c>/<c>Rows</c> cover a recognizer-crop-sized input (320x48, matching
/// <c>CrnnDstHeight</c>) and a detector-page-sized input (3956x736, from
/// <see cref="OcrPipelineBenchmarks"/>'s "en.jpg" case comment). Only <c>Bgra8888</c> is
/// benchmarked - the color type <c>SKBitmap.Resize</c> actually produces on this platform for
/// the images this library decodes - since all three color-type branches share the same
/// indexer-vs-buffer difference.
/// </remarks>
[MemoryDiagnoser]
public class NormalizeBenchmarks
{
    [ParamsSource(nameof(Sizes))]
    public (int Cols, int Rows) Size { get; set; }

    public static IEnumerable<(int Cols, int Rows)> Sizes() => [(320, 48), (3956, 736)];

    private SKBitmap? _bitmap;
    private static readonly float[] MeanValues = [127.5F, 127.5F, 127.5F];
    private static readonly float[] NormValues = [1.0F / 127.5F, 1.0F / 127.5F, 1.0F / 127.5F];

    [GlobalSetup]
    public void Setup()
    {
        var info = new SKImageInfo(Size.Cols, Size.Rows, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var bitmap = new SKBitmap(info);

        var rng = new Random(42);
        Span<byte> pixels = bitmap.GetPixelSpan();
        rng.NextBytes(pixels);

        _bitmap = bitmap;

        if (!ResultsMatch())
        {
            throw new InvalidOperationException("The two normalize arms disagree - benchmark would not be comparing like for like.");
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _bitmap?.Dispose();

    [Benchmark(Baseline = true, Description = "Tensor indexer (current)")]
    public float TensorIndexer()
    {
        var t = SubtractMeanNormalizeTensorIndexer(_bitmap!, MeanValues, NormValues);
        return t[0, 0, 0, 0];
    }

    [Benchmark(Description = "Buffer span, linear index")]
    public float BufferSpan()
    {
        var t = SubtractMeanNormalizeBufferSpan(_bitmap!, MeanValues, NormValues);
        return t[0, 0, 0, 0];
    }

    private bool ResultsMatch()
    {
        var a = (DenseTensor<float>)SubtractMeanNormalizeTensorIndexer(_bitmap!, MeanValues, NormValues);
        var b = (DenseTensor<float>)SubtractMeanNormalizeBufferSpan(_bitmap!, MeanValues, NormValues);
        return a.Buffer.Span.SequenceEqual(b.Buffer.Span);
    }

    /// <summary>Line-for-line copy of the current <see cref="OcrUtils.SubtractMeanNormalize"/> body (Bgra8888 branch only).</summary>
    private static Tensor<float> SubtractMeanNormalizeTensorIndexer(SKBitmap src, float[] meanVals, float[] normVals)
    {
        const int index = 0;
        const int batchSize = 1;

        int cols = src.Width;
        int rows = src.Height;
        int channels = src.BytesPerPixel;
        int rowBytes = src.RowBytes;

        const int expChannels = 3;

        Tensor<float> inputTensor = new DenseTensor<float>([batchSize, expChannels, rows, cols]);

        ReadOnlySpan<byte> span = src.GetPixelSpan();

        for (int r = 0; r < rows; ++r)
        {
            int rowBase = r * rowBytes;
            for (int c = 0; c < cols; ++c)
            {
                int pixelBase = rowBase + c * channels;
                for (int ch = 0; ch < expChannels; ++ch)
                {
                    byte value = span[pixelBase + ch];
                    inputTensor[index, ch, r, c] = (value - meanVals[ch]) * normVals[ch];
                }
            }
        }

        return inputTensor;
    }

    /// <summary>Same math, writing straight to the DenseTensor's contiguous buffer with computed NCHW offsets.</summary>
    private static Tensor<float> SubtractMeanNormalizeBufferSpan(SKBitmap src, float[] meanVals, float[] normVals)
    {
        const int batchSize = 1;

        int cols = src.Width;
        int rows = src.Height;
        int channels = src.BytesPerPixel;
        int rowBytes = src.RowBytes;

        const int expChannels = 3;

        var inputTensor = new DenseTensor<float>([batchSize, expChannels, rows, cols]);
        Span<float> dst = inputTensor.Buffer.Span;
        int channelStride = rows * cols;

        ReadOnlySpan<byte> span = src.GetPixelSpan();

        for (int r = 0; r < rows; ++r)
        {
            int rowBase = r * rowBytes;
            int pixelRowBase = r * cols;
            for (int c = 0; c < cols; ++c)
            {
                int pixelBase = rowBase + c * channels;
                int p = pixelRowBase + c;
                for (int ch = 0; ch < expChannels; ++ch)
                {
                    byte value = span[pixelBase + ch];
                    dst[ch * channelStride + p] = (value - meanVals[ch]) * normVals[ch];
                }
            }
        }

        return inputTensor;
    }
}
