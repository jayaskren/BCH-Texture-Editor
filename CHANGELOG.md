# Changelog — BCH Texture Editor

All notable changes to this project are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Added
- Avalonia cross-platform UI (`BCH.TextureTool.Avalonia`) — Windows and Linux support from a single codebase.
- `BCH.TextureCore` shared library — headless byte[]-based texture and file operations.
- PICA200 RGBA8 Morton-swizzle encoder/decoder (`ImageHelpers.cs`) using SixLabors.ImageSharp.
- Shift-JIS (cp932) string encoding for BCH texture names (fixes kanji corruption on save).
- GitHub Actions CI/CD pipeline (planned) — self-contained `win-x64` and `linux-x64` builds on tag push.

### Fixed
- Kanji texture names (キメ, 怒, 汗, …) corrupted to `?` on save — root cause was ASCII encoding in SPICA serializer; fixed by patching FE3D.Graphics.dll to use Shift-JIS (cp932).
- Linux runtime crashes caused by `System.Drawing` dependency — removed in favour of `SixLabors.ImageSharp`.

### Changed
- Windows release now ships the Avalonia app (`BCH.TextureTool.Avalonia`) rather than the legacy WinForms app.

---

## [Legacy — pre-Avalonia]

### Added
- Original WinForms .NET Framework 4.8 application (`BCH Texture Tool/`).
- LZ11 / LZ13 decompression and ARC archive read/write via FE3D.
- BCH / H3D texture container parsing via SPICA.
- Import, export, replace, rename, split-channel (RGB + alpha) texture operations.
- Portrait ARC editing support (ST 256×256, BU 128×128, CT 512×512 slots).
