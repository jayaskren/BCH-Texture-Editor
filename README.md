# BCH Texture Editor

A texture editor for Nintendo 3DS Fire Emblem games (Fates, Awakening). Lets you open, preview, import, replace, and export textures from `.bch`, `.lz`, and `.arc` files.

---

## Download and run

The editor is a single cross-platform Avalonia app that runs on **Windows, Linux, and macOS**. Each release ships a self-contained executable — **no .NET runtime needs to be installed**.

1. Go to the [Releases](../../releases) page and download the build for your platform:
   - **Windows** — `BCH-Texture-Editor-win-x64.zip`
   - **Linux** — `BCH-Texture-Editor-linux-x64.tar.gz`
2. Unpack and launch:
   - **Windows:** unzip and double-click `BCH.TextureTool.Avalonia.exe`.
   - **Linux:**
     ```bash
     tar xzf BCH-Texture-Editor-linux-x64.tar.gz
     ./BCH.TextureTool.Avalonia
     ```
     The desktop needs the usual X11/font libraries (`libx11-6`, `libsm6`, `libice6`, `libfontconfig1`, `libfreetype6`), which are present on any standard desktop install. Wayland works via XWayland.

> A legacy .NET Framework 4.8 WinForms version also exists in the repo (`BCH Texture Tool/`) for Windows-only local builds, but the Avalonia app is the released, supported build on every platform.

---

## Building from source

The Avalonia app builds the same way on every platform — the FE3D libraries are vendored in `libs/`, so there is nothing else to clone.

### Prerequisites

Install the **.NET 8 SDK**:

```bash
# Ubuntu / Debian
sudo apt install dotnet-sdk-8.0

# Fedora / RHEL
sudo dnf install dotnet-sdk-8.0

# macOS (Homebrew)
brew install dotnet
```

On Windows, install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or Visual Studio 2022 with the **.NET desktop development** workload). Verify with:

```bash
dotnet --version   # should print 8.0.x
```

### Run

```bash
git clone https://github.com/jayaskren/BCH-Texture-Editor.git
cd BCH-Texture-Editor
dotnet run --project BCH.TextureTool.Avalonia
```

First launch takes ~10 seconds while it compiles; subsequent runs are faster.

### Publish a self-contained executable

```bash
# Windows
dotnet publish BCH.TextureTool.Avalonia -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# Linux
dotnet publish BCH.TextureTool.Avalonia -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

---

## Usage

### File formats

| Extension | Description |
|-----------|-------------|
| `.arc`    | Archive containing multiple compressed BCH textures — the main format used in FE Fates |
| `.bch`    | Raw BCH texture container |
| `.lz`     | LZ-compressed BCH file |

### Texture slot names (kanji)

Each texture inside a `.arc` file uses a Japanese kanji name. These must not be changed or the game will not recognize them.

**ST and BU portrait slots:**

| Kanji | Meaning |
|-------|---------|
| キメ  | Posed   |
| 怒    | Angry   |
| 汗    | Sweat   |
| 照    | Blush   |
| 笑    | Happy   |
| 苦    | Suffering |
| 通常  | Standard |

**CT portrait slot:**

| Kanji | Meaning |
|-------|---------|
| ベース | Base (single image; top half appears first, bottom half second) |

### Portrait dimensions

| Type | File suffix | Dimensions |
|------|-------------|------------|
| ST — cutscene / support | `_st` | 256 × 256 |
| BU — battle bust | `_bu` | 128 × 128 |
| CT — conversation | `_ct` | 512 × 512 |

All images must be PNG with an alpha channel before importing.

### Operations

- **Open** — load a `.bch`, `.lz`, or `.arc` file.
- **New** — create a new empty BCH scene.
- **Save** — save the current scene back to file (choose format in the dialog).
- **Export All** — export every texture as a PNG to a folder.
- **Import** — add a new texture from a PNG file.
- **Import Split** — compose a texture from a separate RGB PNG and a grayscale alpha PNG.
- **Replace** — replace the selected texture with a new PNG.
- **Remove** — delete the selected texture.
- **Rename** — rename the selected texture (spaces are not allowed).
- **Export** — save the selected texture as a PNG.
- **Export Split** — save the selected texture as two files: `name_RGB.png` and `name_A.png`.

---

## Credits

| Library | Use |
|---------|-----|
| [FE3D](https://github.com/VelouriasMoon/FE3D) by VelouriasMoon | LZ11/LZ13 compression and ARC format |
| [SPICA](https://github.com/gdkchan/SPICA) by gdkchan | BCH / H3D file format |
| [Magick.NET](https://github.com/dlemstra/Magick.NET) by dlemstra | Image handling (Windows) |
| [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) | Image handling (Linux/macOS) |
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | Cross-platform UI |

Tutorial reference: [Fire Emblem Fates Modding: Portraits](https://gamebanana.com/tuts/17825) by LuminousClarity
