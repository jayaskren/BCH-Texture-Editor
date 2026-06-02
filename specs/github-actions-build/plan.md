# Plan: GitHub Actions Cross-Platform Build & Release

Slug: github-actions-build
Status: planned
Created: 2026-05-31
Spec: [spec.md](spec.md)

## Architecture Delta

This feature is **build/release infrastructure**. It adds no runtime modules to the application and changes no application behavior. The only source change is a NuGet version bump.

New repository artifacts (none are compiled into the app):

- **`.github/workflows/build.yml`** (new) — the CI/CD pipeline. A `build` matrix job over `{windows-latest → win-x64, ubuntu-latest → linux-x64}`, plus a `release` job gated on `v*` tags. The Linux leg produces **two** artifacts (`.deb` + `.tar.gz`) from one publish output.
- **`packaging/linux/`** (new directory) — committed, reviewable Debian-package assets, consumed by the workflow's `.deb` assembly step:
  - `packaging/linux/control.template` — Debian `control` file with a `__VERSION__` placeholder substituted at build time.
  - `packaging/linux/bch-texture-editor.desktop` — XDG desktop entry (app-menu launcher), `Icon=bch-texture-editor`.
  - `packaging/linux/bch-texture-editor` — thin `/usr/bin` wrapper script that `exec`s the payload in `/opt`.
  - `packaging/linux/bch-texture-editor.png` — 256×256 RGBA application icon (already committed; supplied by the user).
- **`BCH.TextureCore/BCH.TextureCore.csproj`** (modified) — `SixLabors.ImageSharp` `3.1.5 → 3.1.11`.

**`.deb` install layout** (FHS-compliant):

```
/opt/bch-texture-editor/                 ← entire self-contained publish output
  BCH.TextureTool.Avalonia               ← the executable
  (bundled .NET runtime + native blobs)
/usr/bin/bch-texture-editor              ← wrapper: exec /opt/bch-texture-editor/BCH.TextureTool.Avalonia "$@"
/usr/share/applications/bch-texture-editor.desktop
/usr/share/icons/hicolor/256x256/apps/bch-texture-editor.png
```

**Module Registry impact on `docs/ARCHITECTURE.md`**: none for application modules. The `.github/` and `packaging/` paths are build infrastructure; `docs/ARCHITECTURE.md` already lists this feature in its Change Log. A post-implement `/sdlc-doc-sync` may add a one-line note that release artifacts are produced by CI.

## Affected Files

Primary file:
- `.github/workflows/build.yml`: created — the entire pipeline; the bulk of the work.

Additional files in scope:
- `packaging/linux/control.template`: created — Debian control metadata (Package, Version placeholder, Architecture, Depends, Maintainer, Section, Priority, Homepage, Description).
- `packaging/linux/bch-texture-editor.desktop`: created — desktop entry (`Exec=bch-texture-editor`, `Icon=applications-graphics`, `Categories=Graphics;Utility;`).
- `packaging/linux/bch-texture-editor`: created — `/usr/bin` wrapper script (`#!/bin/sh` + `exec`).
- `packaging/linux/bch-texture-editor.png`: created — 256×256 RGBA app icon (committed; installed into the hicolor theme by the `.deb`).
- `BCH.TextureCore/BCH.TextureCore.csproj`: modified — single-line ImageSharp version bump.

Read-only by default: all application source (`BCH.TextureCore/*.cs`, `BCH.TextureTool.Avalonia/*`), `libs/*.dll`, `README.md`, `docs/PRD.md`.

## Phases

### Phase 1: Dependency bump (baseline first)
Goal: Move `SixLabors.ImageSharp` to `3.1.11` and confirm no regression and no advisories.
Exit criterion: `dotnet restore BCH.TextureCore/BCH.TextureCore.csproj` and `dotnet build BCH.TextureTool.Avalonia -c Release` both succeed locally with **no** `NU1903`/`NU1902`/`NU1908` warnings, and the app still opens/exports a known-good `.arc` (manual smoke).

### Phase 2: Core build & release pipeline (no .deb yet)
Goal: Author `build.yml` with the matrix build, Windows `.zip`, Linux `.tar.gz`, artifact upload, and the tag-gated `release` job.
Exit criterion: The workflow YAML parses as valid (`python -c 'import yaml,sys; yaml.safe_load(open(sys.argv[1]))' .github/workflows/build.yml`), and a local dry run of the publish + tarball commands produces `publish/linux-x64/BCH.TextureTool.Avalonia` with the exec bit and a `.tar.gz` that preserves it.

### Phase 3: Debian packaging
Goal: Add `packaging/linux/` assets and the `.deb` assembly step (stage tree → substitute version → `dpkg-deb --root-owner-group --build`).
Exit criterion: Running the staging + `dpkg-deb --build` steps **locally on this Linux machine** produces `bch-texture-editor_<version>_amd64.deb` such that `dpkg-deb --info` shows the correct `Package`/`Version`/`Architecture`/`Depends`, and `dpkg-deb --contents` shows `/opt/bch-texture-editor/BCH.TextureTool.Avalonia`, `/usr/bin/bch-texture-editor` (mode 0755), `/usr/share/applications/bch-texture-editor.desktop`, and `/usr/share/icons/hicolor/256x256/apps/bch-texture-editor.png`.

## Risks

- **libicu soname drift across Ubuntu releases**: a self-contained .NET 8 app uses the system ICU; the soname (`libicu70`, `libicu72`, `libicu74`…) differs per Ubuntu release, so a single hard `Depends` would be wrong on some targets. **Mitigation**: declare an alternatives list (`libicu74 | libicu72 | libicu70 | libicu66`) plus the GUI libs (`libx11-6, libsm6, libice6, libfontconfig1, libfreetype6`) and base (`libc6`). **Fallback if brittle**: publish the Linux build with `-p:InvariantGlobalization=true` to drop the ICU dependency entirely (verified not to affect Shift-JIS code-page handling, which is encoding- not culture-based). Documented here so the fallback is a known lever, not a surprise.
- **Debian version-string validity for CI builds**: the non-tag version `0.0.0-ci.<run>` contains `-`, which Debian treats as the upstream/revision separator. **Mitigation**: sanitize to a Debian-valid form for the package only (e.g. `0.0.0~ci<run>`); tag builds (`1.2.3`) are already valid.
- **`dpkg-deb` file ownership**: files staged by the runner would otherwise be owned by the runner UID. **Mitigation**: build with `--root-owner-group` so installed files are `root:root`.
- **Single-file self-extract temp dir**: on machines with a `noexec` `/tmp`, the self-extracting single-file host fails at first launch (affects all three Linux/Windows artifacts equally). **Mitigation**: out of scope to fix; documented limitation in `docs/ARCHITECTURE.md`. The `.deb` does not make this worse.
- **Menu entry refresh without `update-desktop-database`**: some desktop environments cache `.desktop` entries. **Mitigation**: accepted — the entry appears on next desktop scan/login; a `postinst` running `update-desktop-database` is a noted future refinement, deliberately omitted to keep maintainer scripts out of scope.
- **Cannot fully exercise GitHub-hosted runners locally**: matrix behavior, `windows-latest`, and the release job only run on GitHub. **Mitigation**: phase exit criteria are written to be locally verifiable for everything except the GitHub-only orchestration (YAML validity + local publish/package dry runs cover the substance).

## Dependencies

- `SixLabors.ImageSharp`: `3.1.11` (bump from `3.1.5`) — clears CVE-2025-27598 and CVE-2025-54575; drop-in patch, no API changes.
- `dpkg-deb`: preinstalled on `ubuntu-latest` — used to assemble the `.deb`. Not a repository or runtime dependency; no `apt install` step.
- GitHub Actions (pinned by major tag): `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/cache@v4`, `actions/upload-artifact@v4`, `actions/download-artifact@v4`, `softprops/action-gh-release@v2`.

## Constitution Check (re-verified at validate)

| Principle | Status | Notes |
|---|---|---|
| I. File Format Correctness | PASS | No format code changes. ImageSharp bump is a patch with no API change; Phase 1 exit includes a manual `.arc` round-trip smoke. |
| II. Cross-Platform Parity | PASS | The `.deb` and `.tar.gz` wrap the **same** `publish/linux-x64` output — no divergent build. Windows/Linux use identical publish flags. |
| III. No Silent Data Loss | PASS | No application save paths touched. |
| IV. Dependency Security | PASS | ImageSharp → 3.1.11 removes the active high/critical advisories. The `.deb` `Depends` list is declared so missing libs fail at install, not runtime. |
| V. UI Thread Safety | N/A | No UI code changed. |
| VI. No IL Trimming | PASS | **All three** publish invocations (win zip, linux deb, linux tarball — same publish) set `-p:PublishTrimmed=false`. Enforced mechanically in the workflow; a validator can grep for any `PublishTrimmed=true`. |
| VII. Malformed Input Resilience | N/A | No parsing code changed. |
| VIII. Build Reproducibility | PASS | Version from `GITHUB_REF_NAME` for app and `.deb` `control`; sanitized to a Debian-valid string. Workflow targets `BCH.TextureTool.Avalonia.csproj`, never the `.sln`. |

## Revision Log

- 2026-05-31: Folded in the resolved application icon (clarify phase). Added `packaging/linux/bch-texture-editor.png` to Architecture Delta, install layout (`/usr/share/icons/hicolor/256x256/apps/`), Affected Files, and the Phase 3 exit criterion. Desktop entry now `Icon=bch-texture-editor`.
