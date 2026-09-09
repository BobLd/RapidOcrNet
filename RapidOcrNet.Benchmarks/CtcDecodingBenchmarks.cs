// Apache-2.0 license

using BenchmarkDotNet.Attributes;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace RapidOcrNet.Benchmarks;

/// <summary>
/// Isolates the CTC-decoding loop in <see cref="TextRecognizer.ScoreToTextLine"/> (GitHub
/// issue #49): does replacing repeated <c>Tensor&lt;float&gt;[0, i, j]</c> indexing with a
/// one-time copy to a flat array plus linear indexing (<c>flat[i * w + j]</c>) pay off, and at
/// what dictionary size?
/// </summary>
/// <remarks>
/// <para>
/// <c>W</c> (character classes) spans the dictionaries this repo actually ships or that the
/// issue names: the PP-OCRv5 Latin dict (502 keys, <c>models/v5/ppocrv5_latin_dict.txt</c>),
/// the Tibetan model the issue describes (6625 keys), the PP-OCRv6 <i>tiny</i> dict (6904
/// keys, <c>models/v6/ppocrv6_tiny_dict.txt</c>) and the PP-OCRv6 <i>default</i> dict this
/// repo's own <see cref="OcrPipelineBenchmarks"/> loads (18708 keys,
/// <c>models/v6/ppocrv6_dict.txt</c>) - so the issue's "only matters for exotic
/// large-dictionary models" framing can be checked against the model this library ships and
/// benchmarks by default.
/// </para>
/// <para>
/// <c>T</c> (time steps, i.e. <c>h</c> in <see cref="TextRecognizer.ScoreToTextLine"/>) covers
/// short and long crops. PP-OCR's CRNN backbone fixes crop height at 48 and downsamples the
/// resized width by ~8x, so <c>T ~ width / 8</c>: 40 is a short word-length crop, 160 a long
/// line.
/// </para>
/// <para>
/// A third arm goes further than the issue: <c>result.AsTensor&lt;float&gt;()</c> already
/// returns a <see cref="DenseTensor{T}"/>, which stores its data as one contiguous, row-major
/// <see cref="DenseTensor{T}.Buffer"/> - the exact layout the issue's <c>ToArray()</c> copies
/// into a new array. Reading that buffer directly via <see cref="ScoreToTextLineBufferSpan"/>
/// gets the same linear-indexing win with none of the copy.
/// </para>
/// <para>
/// All three arms decode the same random scores into the same characters (verified once in
/// <see cref="ResultsMatch"/>), so <c>Ratio</c> here is a clean speed comparison, not a
/// correctness trade-off.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class CtcDecodingBenchmarks
{
    [Params(502, 6625, 6904, 18708)]
    public int W { get; set; }

    [Params(40, 160)]
    public int T { get; set; }

    private DenseTensor<float>? _tensor;
    private string[] _keys = [];

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        var tensor = new DenseTensor<float>([1, T, W]);
        for (int i = 0; i < T; i++)
        {
            for (int j = 0; j < W; j++)
            {
                tensor[0, i, j] = (float)rng.NextDouble();
            }
        }
        _tensor = tensor;

        _keys = new string[W];
        for (int i = 0; i < W; i++)
        {
            _keys[i] = i.ToString();
        }

        if (!ResultsMatch())
        {
            throw new InvalidOperationException("The two decoding arms disagree - benchmark would not be comparing like for like.");
        }
    }

    [Benchmark(Baseline = true, Description = "Tensor indexer (current)")]
    public int TensorIndexer() => ScoreToTextLineTensorIndexer(_tensor!, _keys).Length;

    [Benchmark(Description = "Flatten + linear index (issue #49)")]
    public int LinearIndex() => ScoreToTextLineLinearIndex(_tensor!, _keys).Length;

    [Benchmark(Description = "Buffer span, no copy (further improvement)")]
    public int BufferSpan() => ScoreToTextLineBufferSpan(_tensor!, _keys).Length;

    private bool ResultsMatch()
    {
        string[] a = ScoreToTextLineTensorIndexer(_tensor!, _keys);
        string[] b = ScoreToTextLineLinearIndex(_tensor!, _keys);
        string[] c = ScoreToTextLineBufferSpan(_tensor!, _keys);
        return a.SequenceEqual(b) && a.SequenceEqual(c);
    }

    /// <summary>Line-for-line copy of the current <see cref="TextRecognizer.ScoreToTextLine"/> body.</summary>
    private static string[] ScoreToTextLineTensorIndexer(Tensor<float> srcData, string[] keys)
    {
        var dimensions = srcData.Dimensions;
        int h = dimensions[1];
        int w = dimensions[2];

        int lastIndex = 0;
        var chars = new List<string>();

        for (int i = 0; i < h; i++)
        {
            int maxIndex = 0;
            float maxValue = -1000F;

            for (int j = 0; j < w; j++)
            {
                float v = srcData[0, i, j];
                if (v > maxValue)
                {
                    maxIndex = j;
                    maxValue = v;
                }
            }

            if (maxIndex > 0 && maxIndex < keys.Length && !(i > 0 && maxIndex == lastIndex))
            {
                chars.Add(keys[maxIndex]);
            }

            lastIndex = maxIndex;
        }

        return chars.ToArray();
    }

    /// <summary>The issue's suggested replacement: flatten once, then index with <c>i * w + j</c>.</summary>
    private static string[] ScoreToTextLineLinearIndex(Tensor<float> srcData, string[] keys)
    {
        var dimensions = srcData.Dimensions;
        int h = dimensions[1];
        int w = dimensions[2];

        float[] flat = srcData.ToArray(); // the copy the issue proposes to pay once
        ReadOnlySpan<float> span = flat;

        int lastIndex = 0;
        var chars = new List<string>();

        for (int i = 0; i < h; i++)
        {
            int maxIndex = 0;
            float maxValue = float.NegativeInfinity;
            int offset = i * w;

            for (int j = 0; j < w; j++)
            {
                float v = span[offset + j];
                if (v > maxValue)
                {
                    maxIndex = j;
                    maxValue = v;
                }
            }

            if (maxIndex > 0 && maxIndex < keys.Length && !(i > 0 && maxIndex == lastIndex))
            {
                chars.Add(keys[maxIndex]);
            }

            lastIndex = maxIndex;
        }

        return chars.ToArray();
    }

    /// <summary>
    /// Skips the copy entirely: <paramref name="srcData"/> is already a
    /// <see cref="DenseTensor{T}"/>, whose backing <see cref="DenseTensor{T}.Buffer"/> is
    /// already the contiguous, row-major layout the issue's <c>ToArray()</c> would recreate.
    /// </summary>
    private static string[] ScoreToTextLineBufferSpan(DenseTensor<float> srcData, string[] keys)
    {
        var dimensions = srcData.Dimensions;
        int h = dimensions[1];
        int w = dimensions[2];

        ReadOnlySpan<float> span = srcData.Buffer.Span;

        int lastIndex = 0;
        var chars = new List<string>();

        for (int i = 0; i < h; i++)
        {
            int maxIndex = 0;
            float maxValue = float.NegativeInfinity;
            int offset = i * w;

            for (int j = 0; j < w; j++)
            {
                float v = span[offset + j];
                if (v > maxValue)
                {
                    maxIndex = j;
                    maxValue = v;
                }
            }

            if (maxIndex > 0 && maxIndex < keys.Length && !(i > 0 && maxIndex == lastIndex))
            {
                chars.Add(keys[maxIndex]);
            }

            lastIndex = maxIndex;
        }

        return chars.ToArray();
    }
}
