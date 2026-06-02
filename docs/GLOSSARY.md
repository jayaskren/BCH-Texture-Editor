# Glossary — BCH Texture Editor

Domain terms used in code, specs, and decisions.

---

| Term | Definition |
|---|---|
| **ARC** | Nintendo 3DS archive file (`.arc`). Contains multiple LZ-compressed BCH files, each named with a Shift-JIS kanji string (e.g. `キメ.bch.lz`). The primary format for Fire Emblem Fates portraits. |
| **BCH** | Binary Container Header — Nintendo 3DS proprietary binary format for textures (and optionally meshes/models). Magic bytes: `BCH`. Parsed by SPICA via FE3D. |
| **BU** | Battle Bust portrait type. 128×128 pixels. Appears in battle scenes. |
| **CodePagesEncodingProvider** | .NET class that registers legacy code-page encodings (including Shift-JIS / cp932) not available by default in .NET 8. Must be registered at app startup via `Encoding.RegisterProvider(...)` before any ARC name read/write. |
| **CT** | Conversation portrait type. 512×512 pixels. Used in dialogue scenes. |
| **FE3D** | Open-source library by VelouriasMoon for Fire Emblem 3DS file formats. Provides LZ11/LZ13 compression and ARC archive handling. Vendored in `libs/` as patched DLLs (Shift-JIS fix). |
| **H3D** | SPICA's in-memory representation of a BCH scene. Contains `Textures`, `Models`, etc. |
| **H3DTexture** | A single texture entry inside an `H3D` scene. Has `Name` (Shift-JIS string), `Width`, `Height`, `Format` (`RGBA8`), and `RawBuffer` (Morton-swizzled pixel data). |
| **LZ11** | Nintendo LZ compression variant used in 3DS game files. Header byte `0x11`. |
| **LZ13** | Nintendo LZ13 compression variant. Header prefix `0x13 … 0x11`. Used for saving `.lz` files in this tool. |
| **Morton swizzle** | Z-order curve tiling of pixels into 8×8 blocks, used by the PICA200 GPU. Raw RGBA8 data in BCH files is Morton-swizzled; `ImageHelpers.cs` contains the forward and inverse lookup table (`SwizzleLUT`). Also called Z-order or Morton encoding. |
| **PICA200** | The GPU inside the Nintendo 3DS. Expects textures in RGBA8 format, Morton-swizzled, with dimensions rounded to powers of 2, stored bottom-to-top. |
| **RID** | .NET Runtime Identifier. Strings like `win-x64` or `linux-x64` that identify the target OS and CPU architecture for `dotnet publish`. |
| **RGBA8** | Pixel format: 4 bytes per pixel. PICA200 stores them in order `[A, R, G, B]`; `ImageSharp`/PNG uses `[R, G, B, A]`. `ImageHelpers.cs` translates between the two. |
| **Self-contained** | A `dotnet publish` mode that bundles the .NET runtime inside the output. End users need no .NET install. |
| **Shift-JIS** | Japanese character encoding (code page 932 / cp932). Used for texture slot names in BCH/ARC files. Non-ASCII names (e.g. `キメ`, `怒`) must be encoded as cp932 bytes, not ASCII or UTF-8. |
| **Single-file** | A `dotnet publish` option (`PublishSingleFile=true`) that bundles all managed assemblies (and, with `IncludeNativeLibrariesForSelfExtract=true`, native blobs) into one executable. |
| **SPICA** | Open-source library by gdkchan for parsing Nintendo 3DS H3D/BCH files. Embedded inside `FE3D.Graphics.dll`. |
| **ST** | Cutscene/support portrait type. 256×256 pixels. Used in supports and cutscenes. |
| **TextureCore** | Short name for the `BCH.TextureCore` project — the headless, UI-free, cross-platform shared library. |
| **TextureSession** | The central domain object in `BCH.TextureCore`. Wraps an `H3D` scene and exposes all texture operations as byte[]-based methods with no UI or platform dependencies. |
