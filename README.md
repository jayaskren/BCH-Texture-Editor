# BCH Texture Editor

A texture editor for Nintendo 3DS Fire Emblem games (Fates, Awakening). Lets you open, preview, import, replace, and export textures from `.bch`, `.lz`, and `.arc` files.

---

## Running on Windows

The Windows version is a .NET Framework 4.8 WinForms application.

### Prerequisites

- **[.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)** — already installed on Windows 10 and later. If you are on an older version of Windows, download and install it from Microsoft.

### Download and run

1. Download `BCH Texture Tool.exe` from the [Releases](../../releases) page.
2. Double-click to run. No installation needed.

### Building from source (Windows)

1. Install [Visual Studio 2019 or later](https://visualstudio.microsoft.com/) with the **.NET desktop development** workload.
2. Clone this repository and the [FE3D](https://github.com/VelouriasMoon/FE3D) repository as a sibling folder:
   ```
   dev/
   ├── BCH-Texture-Editor/
   └── FE3D/
   ```
3. Open `BCH Texture Tool.sln` in Visual Studio.
4. Build → **Build Solution** (`Ctrl+Shift+B`).
5. The output is `BCH Texture Tool/bin/Release/BCH Texture Tool.exe`.

---

## Running on Linux (and macOS)

The cross-platform version uses the Avalonia UI framework and lives on the `avalonia-port` branch.

### Prerequisites

**Ubuntu / Debian:**
```bash
sudo apt install dotnet-sdk-8.0
```

**Fedora / RHEL:**
```bash
sudo dnf install dotnet-sdk-8.0
```

**macOS (via Homebrew):**
```bash
brew install dotnet
```

Verify the install:
```bash
dotnet --version   # should print 8.0.x
```

### Running from source

```bash
# Clone the repo and switch to the Avalonia branch
git clone https://github.com/jayaskren/BCH-Texture-Editor.git
cd BCH-Texture-Editor
git checkout avalonia-port

# Run the app
dotnet run --project BCH.TextureTool.Avalonia
```

The window will open on your desktop. First launch takes ~10 seconds while it compiles; subsequent runs are faster.

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
