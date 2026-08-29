// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/OcrResult.cs

using System.Text;
using SkiaSharp;

namespace RapidOcrNet;

public interface IBoxPoints
{
    SKPointI[] BoxPoints { get; }
}

public interface ITextBox : IBoxPoints
{
    string Text { get; }
}

public sealed class TextBox : IBoxPoints
{
    public required SKPointI[] BoxPoints { get; init; }
    public float Score { get; init; }

    public override string ToString()
    {
        return $"TextBox[score({Score}),[x: {BoxPoints[0].X}, y: {BoxPoints[0].Y}], [x: {BoxPoints[1].X}, y: {BoxPoints[1].Y}], [x: {BoxPoints[2].X}, y: {BoxPoints[2].Y}], [x: {BoxPoints[3].X}, y: {BoxPoints[3].Y}]]";
    }
}

public sealed class Angle
{
    public int Index { get; internal set; }
    public float Score { get; internal init; }
    public float Time { get; internal set; }

    public override string ToString()
    {
        string header = Index >= 0 ? "Angle" : "AngleDisabled";
        return $"{header}[Index({Index}), Score({Score}), Time({Time}ms)]";
    }
}

public sealed class TextLine
{
    public string[]? Chars { get; init; }
    public float[]? CharScores { get; init; }
    public int[]? CharCols { get; init; }

    /// <summary>
    /// Raw CTC time dimension (number of timesteps the recognizer emitted).
    /// </summary>
    public int ColCount { get; init; }

    /// <summary>
    /// Effective time-column count for the actual (non-padded) portion of this crop,
    /// computed as <c>ColCount * wh_ratio / max_wh_ratio</c>. Used by <see cref="CalRecBoxes"/>
    /// to map CTC column indices to pixel positions when batched with right-padding.
    /// Defaults to <see cref="ColCount"/>.
    /// </summary>
    public float LineTxtLen { get; internal set; }

    /// <summary>
    /// Wall-clock milliseconds spent recognizing this crop.
    /// </summary>
    /// <remarks>
    /// Wall clock, not work done: with
    /// <see cref="RapidOcrOptions.RecMaxDegreeOfParallelism"/> above 1 this includes whatever
    /// the crop spent sharing the intra-op thread pool with the others in flight, so it grows
    /// roughly with the degree of parallelism even as the page as a whole gets faster. Summing
    /// it across crops therefore overstates the page; only <see cref="OcrResult.DetectTime"/>
    /// stays comparable between the serial and parallel paths.
    /// </remarks>
    public float Time { get; internal set; }

    public override string ToString()
    {
        if (Chars is null || CharScores is null)
        {
            return "TextLine[No Data]";
        }

        return $"TextLine[Text({string.Concat(Chars)}),CharScores({string.Join(",", CharScores)}),Time({Time}ms)]";
    }
}

public sealed class WordBox : ITextBox
{
    public required string Text { get; init; }

    public required SKPointI[] BoxPoints { get; init; }

    public required float Score { get; init; }

    public override string ToString()
    {
        return $"WordBox[Text({Text}),Score({Score}),[x: {BoxPoints[0].X}, y: {BoxPoints[0].Y}], [x: {BoxPoints[1].X}, y: {BoxPoints[1].Y}], [x: {BoxPoints[2].X}, y: {BoxPoints[2].Y}], [x: {BoxPoints[3].X}, y: {BoxPoints[3].Y}]]";
    }
}

public sealed class TextBlock : ITextBox
{
    public required string Text { get; init; }
    public required SKPointI[] BoxPoints { get; init; }
    public float BoxScore { get; init; }
    public int AngleIndex { get; init; }
    public float AngleScore { get; init; }
    public float AngleTime { get; init; }
    public required string[]? Chars { get; init; }
    public required float[]? CharScores { get; init; }
    public WordBox[]? WordResults { get; init; }
    /// <summary>
    /// Wall-clock milliseconds spent recognizing this block's text line. Inflated by
    /// concurrency — see <see cref="TextLine.Time"/>, which it is taken from.
    /// </summary>
    public float CrnnTime { get; init; }

    /// <summary>
    /// <see cref="AngleTime"/> plus <see cref="CrnnTime"/>, so it carries the same caveat:
    /// under <see cref="RapidOcrOptions.RecMaxDegreeOfParallelism"/> above 1 these are
    /// overlapping wall-clock spans, and adding them up across blocks does not give the time
    /// the page took.
    /// </summary>
    public float BlockTime { get; init; }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("├─TextBlock");
        string textBox =
            $"│   ├──TextBox[score({BoxScore}),[x: {BoxPoints[0].X}, y: {BoxPoints[0].Y}], [x: {BoxPoints[1].X}, y: {BoxPoints[1].Y}], [x: {BoxPoints[2].X}, y: {BoxPoints[2].Y}], [x: {BoxPoints[3].X}, y: {BoxPoints[3].Y}]]";
        sb.AppendLine(textBox);
        string header = AngleIndex >= 0 ? "Angle" : "AngleDisabled";
        string angle = $"│   ├──{header}[Index({AngleIndex}), Score({AngleScore}), Time({AngleTime}ms)]";
        sb.AppendLine(angle);

        string textLine = $"│   ├──TextLine[Text({Text}),CharScores({string.Join(",", CharScores ?? [])}),Time({CrnnTime}ms)]";
        sb.AppendLine(textLine);
        sb.AppendLine($"│   └──BlockTime({BlockTime}ms)");
        return sb.ToString();
    }
}

public sealed class OcrResult
{
    public required TextBlock[] TextBlocks { get; init; }
    public float DbNetTime { get; init; }
    public float DetectTime { get; init; }
    public required string StrRes { get; init; }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("OcrResult");
        foreach (var x in TextBlocks)
        {
            sb.Append(x);
        }

        sb.AppendLine($"├─DbNetTime({DbNetTime}ms)");
        sb.AppendLine($"├─DetectTime({DetectTime}ms)");
        sb.AppendLine($"└─StrRes({StrRes})");
        return sb.ToString();
    }
}
