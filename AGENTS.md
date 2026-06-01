# BCH Texture Editor — Agent Guide

## Purpose

Cross-platform desktop tool for editing texture files in Nintendo 3DS Fire Emblem games (Fates, Awakening). Supports opening, previewing, importing, replacing, and exporting textures from `.bch`, `.lz`, and `.arc` files used in game modding.

## Stack

- **C# / .NET 8** — `BCH.TextureCore` (headless shared library) and `BCH.TextureTool.Avalonia` (cross-platform UI)
- **Avalonia 12.0.4** — cross-platform desktop UI framework (Windows, Linux, macOS)
- **SixLabors.ImageSharp 3.1.11** — image encode/decode; handles PICA200 RGBA8 Morton-swizzled pixel data
- **FE3D** (vendored `libs/FE3D.dll`, `libs/FE3D.Graphics.dll`) — LZ11/LZ13 decompression and ARC archive format
- **SPICA** (embedded in FE3D.Graphics) — BCH / H3D container format parsing
- **C# / .NET Framework 4.8** — legacy `BCH Texture Tool/` WinForms app (Windows-only, local-build; not a CI deliverable)

## Commands

```
build:   dotnet build BCH.TextureTool.Avalonia
run:     dotnet run --project BCH.TextureTool.Avalonia
test:    dotnet test BCH.TextureCore.Tests   # xUnit; ARC round-trip regression tests
lint:    (no linter configured)

publish (win-x64):
  dotnet publish BCH.TextureTool.Avalonia -c Release -r win-x64 \
    --self-contained true -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false

publish (linux-x64):
  dotnet publish BCH.TextureTool.Avalonia -c Release -r linux-x64 \
    --self-contained true -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false
```

> **Never** target `BCH Texture Tool.sln` in build or publish commands — the solution includes the legacy WinForms project whose `ProjectReference` to the sibling `FE3D` repo will fail on a clean checkout. Always target the `.csproj` directly.

## Conventions

- Match existing style in this codebase, even if you'd write it differently in greenfield.
- Surgical changes: name the file(s) you're editing; treat all others as read-only by default.
- Output discipline: redirect long command output to a file; never let it flood the conversation.
- Baseline first: the first change on any feature branch should be reproducible — improvement deltas only after baseline.
- Cite code as `path:line`; cite specs as `specs/<slug>/spec.md`; cite decisions as `docs/DECISIONS/NNNN`.
- Confidence-calibrated findings: ship `[SEVERITY] (confidence: N/10) location — description`. Sub-7 suppressed.
- **Never enable IL trimming** (`PublishTrimmed=true`) — ImageSharp, Avalonia, and `CodePagesEncodingProvider` (Shift-JIS) all rely on reflection and break silently under the trimmer.
- **Never reference UI frameworks from `BCH.TextureCore`** — it must remain headless and platform-agnostic.
- Shift-JIS (cp932) must be registered at startup via `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` before any ARC file operation.

## Key files

| File | Role |
|---|---|
| `BCH.TextureCore/TextureSession.cs` | Open scene + per-texture operations (import/export/replace/rename) |
| `BCH.TextureCore/FileOperations.cs` | `OpenFile` / `OpenArc` / `Save` / `SaveArc` — all static, byte[]-based |
| `BCH.TextureCore/ImageHelpers.cs` | PICA200 RGBA8 Morton swizzle ↔ PNG, alpha split/merge |
| `BCH.TextureTool.Avalonia/MainWindow.axaml.cs` | All UI event handlers |
| `patches/README.md` | How to rebuild the patched FE3D.Graphics.dll (Shift-JIS fix) |

## Pointers

- Architecture: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- Decisions: [docs/DECISIONS/](docs/DECISIONS/)
- Glossary: [docs/GLOSSARY.md](docs/GLOSSARY.md)
- Constitution: [CONSTITUTION.md](CONSTITUTION.md)
- Active specs: [specs/](specs/)

## SDLC workflow

- Start a feature: `/sdlc-feature`
- Validate current state: `/sdlc-validate-implementation`
- Update arch doc from current code: `/sdlc-doc-sync`

<!-- AUTO:END -->

## Manual additions
<!-- Everything below this line is human-edited and preserved across regenerations. -->
