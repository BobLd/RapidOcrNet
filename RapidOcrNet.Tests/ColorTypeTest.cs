using SkiaSharp;

namespace RapidOcrNet.Tests;

/// <summary>
/// A bitmap's colour type is a storage detail: <c>Rgba8888</c> and <c>Bgra8888</c> hold the same
/// pixels in a different byte order, so the same image must reach the models — and come back out
/// of them — identically whichever one it arrives in.
/// </summary>
public class ColorTypeTest
{
    private static readonly Lazy<RapidOcr> Engine = new(() =>
    {
        var ocr = new RapidOcr();
        ocr.InitModels();
        return ocr;
    });

    private static SKBitmap LoadImageBgra8888(string name)
    {
        // Base color type depends on platform. Rgba8888 is default on macOS, Bgra8888 is default on Windows.
        // We force Bgra8888 for the purpose of testing - images are re-converted to Rgba8888 later on.

        var path = Path.Combine("images", name);
        Assert.True(File.Exists(path));
        
        var image = SKBitmap.Decode(path);
        if (image.ColorType == SKColorType.Bgra8888)
        {
            return image;
        }

        var bgra = image.Copy(SKColorType.Bgra8888);
        image.Dispose();
        return bgra;
    }

    /// <summary>
    /// Images chosen for colour, not for text. Channel order cannot be pinned on a page of black
    /// text on white, where R, G and B are equal and reversing them changes nothing — these carry
    /// real colour, so a branch that read the bytes in the wrong order would recognise something
    /// different.
    /// </summary>
    public static IEnumerable<object[]> ColourfulImages => new[]
    {
        new object[] { "img623.jpg" },
        new object[] { "img_195.jpg" },
        new object[] { "img_10.jpg" },
        new object[] { "254.jpg" },
        new object[] { "en_rec.jpg" },
    };

    [Theory]
    [MemberData(nameof(ColourfulImages))]
    public void DetectReadsRgba8888TheSameAsBgra8888(string name)
    {
        // Base color type depends on platform. Rgba8888 is default on macOS, Bgra8888 is default on windows

        using SKBitmap bgra = LoadImageBgra8888(name);
        Assert.Equal(SKColorType.Bgra8888, bgra.ColorType);

        // Copy, not reinterpret: this rewrites the bytes into the other order, leaving an image
        // that looks identical and is laid out differently. Exactly the case the new branch is for.
        using SKBitmap rgba = bgra.Copy(SKColorType.Rgba8888);
        Assert.Equal(SKColorType.Rgba8888, rgba.ColorType);

        var fromBgra = Engine.Value.Detect(bgra, RapidOcrOptions.Default);
        var fromRgba = Engine.Value.Detect(rgba, RapidOcrOptions.Default);

        Assert.Equal(fromBgra.TextBlocks.Length, fromRgba.TextBlocks.Length);
        Assert.Equal(fromBgra.StrRes, fromRgba.StrRes);
    }

    [Fact]
    public void DetectBoxesReadsRgba8888TheSameAsBgra8888()
    {
        // The detection stage on its own, so a difference in the boxes is not masked by
        // recognition happening to produce the same text from slightly different crops.
        using SKBitmap bgra = LoadImageBgra8888("img623.jpg");
        using SKBitmap rgba = bgra.Copy(SKColorType.Rgba8888);

        var fromBgra = Engine.Value.DetectBoxes(bgra, RapidOcrOptions.Default);
        var fromRgba = Engine.Value.DetectBoxes(rgba, RapidOcrOptions.Default);

        Assert.Equal(fromBgra.Count, fromRgba.Count);
        for (int i = 0; i < fromBgra.Count; i++)
        {
            Assert.Equal(fromBgra[i].BoxPoints, fromRgba[i].BoxPoints);
            Assert.Equal(fromBgra[i].Score, fromRgba[i].Score);
        }
    }

    [Fact]
    public void UnsupportedColourTypeIsRejectedWithAUsefulMessage()
    {
        // Rgb565 drops to a different layout entirely rather than a reordering of the same bytes,
        // so it is still refused — but the message has to name it and list what is accepted, or
        // the caller is left guessing what to convert to.
        using SKBitmap bgra = LoadImageBgra8888("en_rec.jpg");
        using SKBitmap rgb565 = bgra.Copy(SKColorType.Rgb565);
        Assert.Equal(SKColorType.Rgb565, rgb565.ColorType);

        var ex = Assert.Throws<ArgumentException>(
            () => Engine.Value.DetectBoxes(rgb565, RapidOcrOptions.Default));

        Assert.Contains(nameof(SKColorType.Rgb565), ex.Message);
        Assert.Contains(nameof(SKColorType.Rgba8888), ex.Message);
    }
}
