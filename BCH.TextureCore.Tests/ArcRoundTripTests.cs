using System.Text;
using Xunit;

namespace BCH.TextureCore.Tests;

/// <summary>
/// Regression tests for ARC save/open round-tripping.
///
/// These guard three real bugs found in <see cref="FileOperations.SaveArc"/>:
///  1. Entries were compressed with FE3D's LZ13 compressor, which truncates data
///     containing long runs of repeated bytes (e.g. transparent portrait regions),
///     making the saved .arc unreadable (NotEnoughDataException on reopen).
///  2. SaveArc mutated the live session — every open texture was renamed to "tmp",
///     so a second save corrupted the archive.
///  3. (Covered by the round-trip assertions) names and pixel data must survive a
///     save/open cycle unchanged.
/// </summary>
public class ArcRoundTripTests
{
    static ArcRoundTripTests()
    {
        // ARC texture names are Shift-JIS (cp932); register the provider for tests.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "Sample_st.arc");

    static TextureSession OpenSample() =>
        FileOperations.OpenArc(File.ReadAllBytes(SamplePath), SamplePath);

    [Fact]
    public void Fixture_SampleArc_OpensWithSevenKanjiNamedTextures()
    {
        var session = OpenSample();
        Assert.Equal(7, session.Count);
        Assert.Equal(
            new[] { "キメ", "怒", "汗", "照", "笑", "苦", "通常" },
            session.TextureNames);
    }

    [Fact]
    public void SaveArc_RoundTrip_PreservesTextureNames()
    {
        var orig = OpenSample();
        var names = orig.TextureNames.ToArray();

        byte[] saved = FileOperations.SaveArc(orig);
        var reopened = FileOperations.OpenArc(saved, "roundtrip.arc");

        Assert.Equal(names, reopened.TextureNames);
    }

    [Fact]
    public void SaveArc_RoundTrip_PreservesPixelData()
    {
        var orig = OpenSample();
        byte[] before = orig.Scene.Textures[0].RawBuffer.ToArray();

        var reopened = FileOperations.OpenArc(FileOperations.SaveArc(orig), "roundtrip.arc");

        Assert.Equal(before, reopened.Scene.Textures[0].RawBuffer);
    }

    [Fact]
    public void SaveArc_DoesNotMutate_LiveSessionTextureNames()
    {
        var session = OpenSample();
        var before = session.TextureNames.ToArray();

        FileOperations.SaveArc(session);

        // Bug #2: this used to turn every name into "tmp".
        Assert.Equal(before, session.TextureNames);
    }

    [Fact]
    public void SaveArc_DoubleSave_StaysStable()
    {
        var s = OpenSample();
        var names = s.TextureNames.ToArray();

        var open1 = FileOperations.OpenArc(FileOperations.SaveArc(s), "first.arc");
        var open2 = FileOperations.OpenArc(FileOperations.SaveArc(open1), "second.arc");

        Assert.Equal(names, open2.TextureNames);
    }

    [Fact]
    public void SaveArc_FlatTexture_RoundTripsWithoutCorruption()
    {
        // Bug #1: a texture that is one long run of identical bytes (a fully
        // transparent / flat region) corrupted under the LZ13 compressor and threw
        // on reopen. With the LZ11-based fix it must round-trip cleanly.
        var session = OpenSample();
        var tex = session.Scene.Textures[0];
        tex.RawBuffer = new byte[tex.RawBuffer.Length]; // all zeros

        byte[] saved = FileOperations.SaveArc(session);
        var reopened = FileOperations.OpenArc(saved, "flat.arc");

        Assert.Equal(session.Count, reopened.Count);
        Assert.True(reopened.Scene.Textures[0].RawBuffer.All(b => b == 0),
            "flat texture data was not preserved through the ARC round-trip");
    }
}
