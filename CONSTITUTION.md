# Constitution — BCH Texture Editor

Version: 0.1.0
Last amended: 2026-05-31
Ratified: 2026-05-31

## Purpose

This constitution defines the non-negotiable engineering principles for the BCH Texture Editor. Every validation skill checks code against these. Plans must declare a Constitution Check at creation; that check is re-verified at validate time.

Principles use Roman numerals. **MUST** clauses are hard rules — violations are validation failures. **SHOULD** clauses are soft rules — violations are warnings unless waived in `docs/DECISIONS/`. **SHOULD NOT** clauses are soft anti-rules with the same treatment.

---

## I. File Format Correctness

**MUST**: All BCH, LZ, and ARC output produced by the tool must be accepted by the Nintendo 3DS game engine without error. Output must be byte-for-byte equivalent to what the original game tools would produce for the same input.

**MUST**: Shift-JIS (cp932) texture slot names (e.g. `キメ`, `怒`, `汗`, `照`, `笑`, `苦`, `通常`, `ベース`) must be preserved exactly, including encoding. No character substitution or transliteration is permitted.

**SHOULD NOT**: Any code path silently truncate, pad, or reorder binary data structures. If a format constraint cannot be met (e.g. non-power-of-2 image dimensions), the user must be informed.

**Rationale**: This is a game modding tool. A corrupted output file means the game crashes, displays garbage, or refuses to load the character. There is no automated integration test against the actual game engine — correctness is the single most critical property. The Shift-JIS name rule exists because the game engine uses these names as lookup keys; changing encoding or value breaks the lookup silently.

---

## II. Cross-Platform Parity

**MUST**: `BCH.TextureCore` must produce identical binary output on Windows and Linux for the same input bytes. Platform must not affect the result.

**MUST NOT**: `BCH.TextureCore` reference `System.Drawing`, `System.Windows`, `System.Windows.Forms`, Avalonia, WPF, or any other platform-specific or UI-framework namespace. It must remain a pure .NET 8 class library.

**SHOULD**: New image-processing code in `BCH.TextureCore` use `SixLabors.ImageSharp` exclusively, not any platform-specific imaging library.

**Rationale**: The Avalonia port exists specifically to serve Linux (and macOS) users. If the core library behaves differently per platform — even subtly, due to `System.Drawing`'s GDI+ backing on Windows — users get different results depending on their OS. The headless/UI-free isolation of `BCH.TextureCore` is what makes the cross-platform port possible and maintainable.

---

## III. No Silent Data Loss

**MUST NOT**: Any save operation overwrite the user's source file with partial or corrupt data. Write to a complete in-memory buffer first; only write to disk when the full buffer is ready.

**MUST NOT**: Any error during file parsing, image processing, or serialization be silently swallowed. Every error that would produce incorrect output must be surfaced to the user with a meaningful message.

**SHOULD**: File save dialogs default to the same format as the opened file, so users do not accidentally save an ARC as a BCH (or vice versa) without choosing to.

**Rationale**: Users' modded game files represent hours of creative work and may be the result of careful pixel editing in external tools. A half-written save on crash, or a silently wrong format conversion, leaves the file unrecoverable. The tool must never make the user's situation worse than before they opened it.

---

## IV. Dependency Security

**MUST NOT**: Any NuGet package with an active high or critical CVE advisory (as classified by GitHub Advisory Database) be present in a released build.

**SHOULD**: Moderate advisories be addressed within the next patch release after discovery.

**SHOULD**: `SixLabors.ImageSharp` be kept on the latest patch of its current minor version, as it processes untrusted image bytes from user-supplied PNG files.

**Rationale**: This tool is downloaded and executed directly by end users, often on personal machines without corporate endpoint protection. The primary third-party image library (`ImageSharp`) processes untrusted input — users import PNGs that may have been sourced from the internet or other modders. An unpatched image-decoding vulnerability (such as CVE-2025-27598, out-of-bounds write, or CVE-2025-54575, GIF infinite loop) exposes every user who opens a malicious file.

---

## V. UI Thread Safety

**MUST NOT**: File I/O, decompression, image encoding/decoding, or any `BCH.TextureCore` method be called synchronously on the Avalonia UI thread.

**SHOULD**: All blocking operations in `MainWindow.axaml.cs` be invoked via `async`/`await`, with UI controls updated only after the awaited task completes.

**Rationale**: Avalonia freezes the main window if the UI thread blocks. ARC file parsing (multiple LZ11 decompression passes), image encoding (Morton swizzle over large textures), and `dotnet publish` artifact operations can each take hundreds of milliseconds. A frozen window looks like a crash to users and provides no feedback during long operations. This is especially important for CT-format portraits (512×512, ~1 MB raw) where encoding takes measurably longer.

---

## VI. No IL Trimming

**MUST**: `PublishTrimmed` be set to `false` (or omitted) in all publish configurations for all projects.

**MUST NOT**: Any CI workflow, build script, or local publish command enable `PublishTrimmed=true` for any project in this solution.

**Rationale**: Three critical runtime paths rely on reflection and are silently broken by the IL trimmer:
1. `SixLabors.ImageSharp` discovers codecs and pixel formats via reflection at runtime. Trimming removes unused format handlers, causing `UnknownImageFormatException` on valid PNG files.
2. Avalonia 12 loads XAML, resolves bindings, and initialises the Skia renderer via reflection. Trimming causes `XamlLoadException` or blank windows.
3. `CodePagesEncodingProvider.Instance` (required for Shift-JIS / cp932) loads encoding data tables via reflection. Trimming removes the tables, causing `NotSupportedException` on any ARC file operation.

None of these failures surface at build time — they appear only at runtime on end-user machines, after the tool has been distributed. The cost of disabling trimming is ~70–110 MB binaries; this is accepted.

---

## VII. Malformed Input Resilience

**MUST NOT**: Any malformed, truncated, or wrong-format input file cause an unhandled exception that crashes the application.

**MUST**: All file-open code paths (BCH, LZ, ARC) catch parse failures and surface a user-readable error message identifying the file and the nature of the failure.

**SHOULD**: The tool reject (with a clear message) BCH files that contain models in addition to textures, rather than attempting to process them. (The format supports models, but the tool is texture-only.)

**Rationale**: Users will attempt to open files that are partially downloaded, wrong format, encrypted, or simply not a BCH/ARC file at all. A crash is a worse outcome than a clear error — it provides no information, may leave the UI in an unknown state, and erodes trust in the tool. The model-rejection rule exists because model data in a BCH scene would be silently discarded on save, potentially corrupting a file the user intended to keep intact.

---

## VIII. Build Reproducibility

**MUST**: The version embedded in a released binary be sourced exclusively from the git tag used to trigger the release (`GITHUB_REF_NAME`). No hardcoded version strings in `.csproj` files.

**MUST**: CI release builds target `BCH.TextureTool.Avalonia.csproj` directly, never `BCH Texture Tool.sln`, because the solution includes the legacy WinForms project whose `ProjectReference` to the sibling `FE3D` repo will fail on a clean checkout.

**SHOULD**: NuGet package restore be reproducible across runs. When a `packages.lock.json` is committed, CI must use `--locked-mode`. (Currently no lockfile exists; this principle applies when one is added.)

**Rationale**: Reproducible builds ensure that the binary a user downloads corresponds exactly to the source code tagged in git. Non-reproducible builds — where the version comes from a developer's local `AssemblyInfo.cs` edit, or where the build silently pulls a newer dependency than expected — undermine release integrity. The solution-file restriction is a hard constraint, not a convention: building the solution on CI has caused and will cause build failures.

---

## Amendments

| Version | Date | Change | Rationale |
|---|---|---|---|
| 0.1.0 | 2026-05-31 | Initial constitution — 8 principles | Established for first SDLC-tracked feature (GitHub Actions CI) |
