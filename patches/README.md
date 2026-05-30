# Patches

## fe3d-shiftjis-strings.patch

The vendored `libs/FE3D.Graphics.dll` is built from a **patched** copy of
[FE3D](https://github.com/VelouriasMoon/FE3D), not the upstream source.

### What it fixes

Fire Emblem BCH files store texture names (Japanese kanji such as キメ, 怒, 汗)
in **Shift-JIS (codepage 932)**. SPICA's serializer was using `Encoding.ASCII`
to write strings and `BinaryReader.ReadChar()` (UTF-8) to read them, which
corrupted every non-ASCII name to `?` on save.

The patch changes both the serializer and deserializer in
`FE3D.Graphics/SPICA/Serialization/` to use Shift-JIS (codepage 932) for
string encode/decode.

### How to rebuild the DLL

1. Clone FE3D next to this repo:
   ```bash
   git clone https://github.com/VelouriasMoon/FE3D.git ../FE3D
   ```
2. Apply the patch:
   ```bash
   cd ../FE3D
   git apply ../BCH-Texture-Editor/patches/fe3d-shiftjis-strings.patch
   ```
3. Build `FE3D.Graphics.dll` targeting net8.0 and copy it to `libs/`.

> Note: the consuming app must also call
> `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` at startup
> so that codepage 932 is available on .NET 8 (done in
> `BCH.TextureTool.Avalonia/Program.cs`).
