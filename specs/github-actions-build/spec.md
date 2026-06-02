# Spec: GitHub Actions Cross-Platform Build & Release

Slug: github-actions-build
Status: validating
Branch: main
Created: 2026-05-31

## Summary

Add a GitHub Actions CI/CD pipeline that automatically builds self-contained desktop binaries for Windows (`win-x64`) and Linux (`linux-x64`) on every push and PR, and publishes them to a GitHub Release on every `v*` tag push. Windows ships as a single-file `.exe` inside a `.zip`. Linux ships **two** artifacts from the same publish output: a Debian package (`.deb`) that installs on Ubuntu/Debian with a desktop launcher, and a distro-agnostic `.tar.gz` fallback. All binaries are produced from the single Avalonia `net8.0` codebase (`BCH.TextureTool.Avalonia`). No .NET runtime install is required by end users. The legacy WinForms .NET Framework 4.8 app is intentionally excluded from CI. As a bundled prerequisite, `SixLabors.ImageSharp` in `BCH.TextureCore` is bumped from `3.1.5` to `3.1.11` to clear active CVE advisories.

## User Stories

### US1: Continuous build verification on push/PR

**As a** developer, **I want** both platform builds to run automatically on every push to `main` and every pull request, **so that** broken builds are caught before they reach the release flow.

**Acceptance criteria**:
- Given a push to `main`, when the workflow runs, then both the `win-x64` and `linux-x64` matrix legs complete (pass or fail independently — `fail-fast: false`).
- Given a pull request targeting `main`, when the workflow runs, then both matrix legs run and the PR status check reflects their outcome.
- Given a successful build, then all three artifacts (`BCH-Texture-Editor-win-x64.zip`, `bch-texture-editor_<version>_amd64.deb`, and `BCH-Texture-Editor-linux-x64.tar.gz`) are uploaded and retained for 14 days.
- Given a non-tag push or PR run, then no GitHub Release is created.

### US2: Versioned release from a tag

**As a** maintainer, **I want** pushing a `v*` tag to automatically create a GitHub Release with all platform binaries attached, **so that** users always find the latest build on the Releases page without manual upload.

**Acceptance criteria**:
- Given a pushed tag matching `v*` (e.g. `v1.2.3`), when the workflow completes, then a GitHub Release named `v1.2.3` exists with `BCH-Texture-Editor-win-x64.zip`, `bch-texture-editor_1.2.3_amd64.deb`, and `BCH-Texture-Editor-linux-x64.tar.gz` attached.
- Given the tag `v1.2.3`, then the binaries inside all artifacts report internal version `1.2.3` (sourced from `GITHUB_REF_NAME`, not a hardcoded property), and the `.deb` control file's `Version:` field is `1.2.3`.
- Given a re-run of the same tag's workflow, then the existing Release is upserted (not duplicated).
- Given a workflow triggered by `workflow_dispatch` without a tag ref, then no Release is created.

### US3: Zero-install Windows experience

**As a** Windows user, **I want** to download a single `.exe` from the Releases page and run it by double-clicking, **so that** I don't need to install .NET or any other dependency.

**Acceptance criteria**:
- Given a Windows machine with no .NET runtime installed, when the user unzips `BCH-Texture-Editor-win-x64.zip` and double-clicks `BCH.TextureTool.Avalonia.exe`, then the application launches.
- Given the artifact zip, then it contains `BCH.TextureTool.Avalonia.exe` and no loose native `.dll` or `.pdb` files (native libs are embedded in the single-file executable).

### US4: One-command install on Ubuntu/Debian (.deb)

**As an** Ubuntu/Debian user, **I want** to install the editor from a `.deb` package that puts it in my application menu, **so that** it integrates like a native app and I can update or remove it with the system package manager.

**Acceptance criteria**:
- Given an Ubuntu/Debian desktop with no .NET runtime installed, when the user runs `sudo apt install ./bch-texture-editor_<version>_amd64.deb` (or `sudo dpkg -i`), then the package installs without error and `apt` resolves its declared dependencies.
- Given the installed package, then the self-contained application is placed under `/opt/bch-texture-editor/`, a launcher exists at `/usr/bin/bch-texture-editor`, a desktop entry at `/usr/share/applications/bch-texture-editor.desktop` makes it appear in the application menu, and the icon is installed at `/usr/share/icons/hicolor/256x256/apps/bch-texture-editor.png`.
- Given the installed package, when the user runs `bch-texture-editor` (or clicks the menu entry), then the application launches.
- Given the package's `control` file, then `Package` is `bch-texture-editor`, `Architecture` is `amd64`, `Version` matches the release version, and `Depends` declares the X11/font/ICU runtime libraries needed by Avalonia + self-contained .NET.
- Given `sudo apt remove bch-texture-editor`, then all installed files are removed cleanly.

### US5: Distro-agnostic Linux fallback (tarball)

**As a** non-Debian Linux user (Fedora, Arch, etc.), **I want** a plain tarball I can extract and run, **so that** I'm not blocked by the `.deb` packaging format.

**Acceptance criteria**:
- Given a Linux desktop with no .NET runtime installed (but standard X11/font system libs present), when the user runs `tar xzf BCH-Texture-Editor-linux-x64.tar.gz && ./BCH.TextureTool.Avalonia`, then the application launches.
- Given the artifact tarball, then the extracted binary has the executable bit set (i.e., the tarball is `.tar.gz`, not a plain zip).

### US6: ImageSharp security advisory resolution

**As a** maintainer, **I want** `SixLabors.ImageSharp` bumped to `3.1.11` in `BCH.TextureCore`, **so that** CI restores cleanly without active CVE advisories (`NU1903`, `NU1902`, `NU1908`) that will block future audit-gated builds.

**Acceptance criteria**:
- Given the updated `BCH.TextureCore.csproj`, then `SixLabors.ImageSharp` is pinned to `3.1.11`.
- Given `dotnet restore` on that project, then no `NU1903`, `NU1902`, or `NU1908` warnings appear.
- Given the existing preview/import/export/split-channel operations, then they produce the same correct output after the bump (no regression).

## Non-Functional Requirements

- **Build time**: N/A — no explicit bound, but `dotnet publish` for self-contained single-file typically completes within GitHub Actions' 6-hour job limit. NuGet caching is applied to reduce restore time on repeat runs.
- **Security**: Workflow default permission is `contents: read`; only the `release` job elevates to `contents: write`. No `GITHUB_TOKEN` with write scope is granted to build legs. Third-party actions (`softprops/action-gh-release`) should be pinned to a commit SHA in follow-up hardening.
- **Reproducibility**: Version is derived from the git tag. Non-tag builds use `0.0.0-ci.<run_number>`. No hardcoded version properties in any `.csproj`.
- **Binary correctness**: `PublishTrimmed=false` is mandatory. IL trimming would silently break ImageSharp (reflection-based pixel-format discovery), Avalonia 12 (XAML/Skia reflection), and `CodePagesEncodingProvider` (Shift-JIS data tables required for ARC kanji texture names).
- **Artifact integrity**: `if-no-files-found: error` on `upload-artifact` steps — build does not silently succeed with empty output.
- **Concurrency**: Concurrent branch/PR runs are cancelled in-progress; tag runs are never cancelled mid-flight.
- **Debian packaging**: The `.deb` is built with `dpkg-deb --build` (preinstalled on `ubuntu-latest` — no extra `apt install`). It is a binary package only (no source package, no `dh`/`debhelper` toolchain). Package metadata (`Maintainer`, `Section: graphics`, `Priority: optional`, `Homepage`) is hand-authored in the `control` file. The package must pass `dpkg-deb --info` and `dpkg-deb --contents` inspection in CI.
- **FHS compliance**: Installed file layout follows the Filesystem Hierarchy Standard — application payload under `/opt/`, executable launcher under `/usr/bin/`, desktop entry under `/usr/share/applications/`.

## Out of Scope

- Building or releasing the legacy WinForms .NET Framework 4.8 app in CI (its `ProjectReference` to the sibling `VelouriasMoon/FE3D` repo makes clean checkout impossible without fragile workarounds).
- macOS builds.
- ARM64 builds (`win-arm64`, `linux-arm64`); the `.deb` is `amd64` only.
- RPM (`.rpm`), Flatpak, Snap, or AppImage packaging — the `.tar.gz` is the fallback for non-Debian distros.
- Publishing the `.deb` to an APT repository or PPA — it is attached to the GitHub Release only; users install with `apt install ./file.deb`.
- A Debian *source* package or `debian/` rules tree — the `.deb` is assembled directly with `dpkg-deb --build`.
- IL trimming / binary size reduction.
- Code signing or notarization on either platform.
- Headless GUI smoke testing on CI runners.
- Enabling `RestorePackagesWithLockFile` / committing `packages.lock.json` (optional future hardening).
- Pinning third-party action `softprops/action-gh-release` to a commit SHA (noted as follow-up, not blocking).
- Renaming the output binary to `BCH Texture Tool.exe` to match older docs (keep project name; README already updated).

## Open Questions

- [x] Should `softprops/action-gh-release` be pinned to a commit SHA before merge, or is `@v2` acceptable for now? **Resolved: `@v2` is acceptable for initial implementation. SHA pinning is noted in PRD §7 Risk #8 as optional follow-up hardening.**
- [x] Should the `avalonia-port` branch remain in the push/PR trigger list, or can it be removed now that the branch is merged to `main`? **Resolved: Remove `avalonia-port` from triggers. The branch is merged; `main` is the only release-bearing ref.**
- [x] **Application icon for the `.deb` desktop entry.** **Resolved: ship a real bundled PNG icon.** The user-provided 256×256 RGBA PNG is committed at `packaging/linux/bch-texture-editor.png`. The `.deb` installs it to `/usr/share/icons/hicolor/256x256/apps/bch-texture-editor.png`, and the desktop entry references it as `Icon=bch-texture-editor`.

## Constitution Check

| Principle | Status | Notes |
|---|---|---|
| I. File Format Correctness | PASS | CI produces the same binaries as local publish; no format logic is changed. ImageSharp bump (3.1.5→3.1.11) is a drop-in patch with no API changes — format output is unaffected. |
| II. Cross-Platform Parity | PASS | Both builds use identical `dotnet publish` flags and the same source. `BCH.TextureCore` is not modified. |
| III. No Silent Data Loss | PASS | CI only adds a workflow file and bumps a NuGet version. No save paths are touched. |
| IV. Dependency Security | PASS | The ImageSharp bump to 3.1.11 directly addresses CVE-2025-27598 and CVE-2025-54575 (the high/critical advisories on 3.1.5). This feature is the mechanism by which the principle is satisfied. |
| V. UI Thread Safety | N/A | No UI code is added or changed. |
| VI. No IL Trimming | PASS | `PublishTrimmed=false` is an explicit flag in every `dotnet publish` command in the workflow. The workflow enforces the principle mechanically. |
| VII. Malformed Input Resilience | N/A | No file parsing code is added or changed. The `.deb` declares runtime `Depends` so a missing system lib surfaces as an apt error at install time, not a runtime crash. |
| VIII. Build Reproducibility | PASS | Version is sourced from `GITHUB_REF_NAME` (git tag). Workflow targets `.csproj` directly, never `.sln`. Principle is the rationale for this feature's design choices. |

## Revision Log

- 2026-05-31: Added Debian package (`.deb`) as a Linux deliverable alongside the tarball (US4 split into US4 `.deb` + US5 tarball; ImageSharp renumbered to US6). Updated Summary, US1/US2 artifact lists, NFRs (Debian packaging, FHS compliance), Out of Scope, and added the application-icon open question. Resolved the two original open questions (action pinning, branch triggers).
- 2026-05-31 (clarify): Resolved the application-icon question — user supplied a 256×256 RGBA PNG, committed at `packaging/linux/bch-texture-editor.png`. `.deb` installs it to the hicolor theme; desktop entry uses `Icon=bch-texture-editor`. Updated US4 acceptance criteria and removed the icon out-of-scope line.
