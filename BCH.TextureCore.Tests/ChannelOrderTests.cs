using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BCH.TextureCore.Tests;

/// <summary>
/// Regression tests for the PICA200 RGBA8 raw byte order, which must be [A, B, G, R]
/// to match SPICA's codec (and therefore the game). An earlier port used [A, R, G, B],
/// which round-tripped inside this tool but swapped red/blue in-game.
/// </summary>
public class ChannelOrderTests
{
    static byte[] SolidPng(byte r, byte g, byte b, byte a)
    {
        using var img = new Image<Rgba32>(8, 8, new Rgba32(r, g, b, a));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Fact]
    public void PngToRgba8_EncodesRawAs_A_B_G_R()
    {
        // A pure-red, fully-opaque image must encode each raw pixel as
        // [A=255, B=0, G=0, R=255] — i.e. red lands in raw slot 3, not slot 1.
        var (raw, _, _) = ImageHelpers.PngToRgba8(SolidPng(255, 0, 0, 255));

        Assert.Equal(255, raw[0]); // A
        Assert.Equal(0, raw[1]);   // B
        Assert.Equal(0, raw[2]);   // G
        Assert.Equal(255, raw[3]); // R  (the bug put R here = 0)
    }

    [Fact]
    public void PngToRgba8_BluePixel_PutsBlueInRawSlot1()
    {
        var (raw, _, _) = ImageHelpers.PngToRgba8(SolidPng(0, 0, 255, 255));

        Assert.Equal(255, raw[0]); // A
        Assert.Equal(255, raw[1]); // B
        Assert.Equal(0, raw[2]);   // G
        Assert.Equal(0, raw[3]);   // R
    }

    [Fact]
    public void Rgba8_RoundTrip_PreservesDistinctChannels()
    {
        // Encode a non-grey colour and decode it back; R, G, B must survive in place.
        var (raw, w, h) = ImageHelpers.PngToRgba8(SolidPng(200, 120, 40, 255));
        byte[] png = ImageHelpers.Rgba8ToPng(raw, w, h);

        using var img = Image.Load<Rgba32>(png);
        var p = img[0, 0];
        Assert.Equal(200, p.R);
        Assert.Equal(120, p.G);
        Assert.Equal(40, p.B);
        Assert.Equal(255, p.A);
    }
}
