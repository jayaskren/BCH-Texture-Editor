# PRD: GitHub Actions Cross-Platform Build & Release

## 1. Summary

Add a GitHub Actions CI/CD pipeline that builds, on every push/PR, and releases on every `v*` tag, two self-contained desktop binaries for the BCH Texture Editor: a Windows `win-x64` executable and a Linux `linux-x64` executable. Both are produced from the **single Avalonia `net8.0` codebase** (`BCH.TextureTool.Avalonia`), giving us one cross-platform source of truth, zero end-user runtime dependencies, and a clean Releases page that the README already points users to.

The legacy WinForms .NET Framework 4.8 app is **deliberately excluded** from CI (see §4). The pipeline targets the Avalonia project's `.csproj` directly — never the solution — because the solution drags in the unbuildable legacy project.

## 2. Goals / Non-Goals

**Goals**
- On push to `main`/`avalonia-port` and on PRs: build both platforms, upload artifacts (continuous build verification).
- On `v*` tag push: build both platforms and publish a GitHub Release with both binaries attached.
- Each binary is **self-contained** (no .NET install required by end users) and **single-file**.
- The vendored `libs/*.dll` flow into the output with no extra CI steps (they are committed).
- Trimming stays **off** to protect ImageSharp, Avalonia XAML/Skia, and the Shift-JIS code-page provider.
- Version is sourced from the git tag — the single source of truth.

**Non-Goals**
- Building or releasing the legacy WinForms .NET Framework 4.8 app.
- ARM64 builds (`win-arm64`, `linux-arm64`) — noted as a future matrix leg, not in scope.
- macOS builds.
- Trimmed/minimized binaries.
- Code signing / notarization.
- Running the GUI app on headless CI to smoke-test rendering (build-only verification).

(The README has already been updated to describe the Avalonia self-contained downloads for all platforms, so it is no longer a pending follow-up.)

## 3. Background & Constraints

The repo contains **three projects** (`BCH Texture Tool.sln`):

| Project | Type | Framework | FE3D dependency | Buildable on clean CI? |
|---|---|---|---|---|
| `BCH Texture Tool/` | Legacy WinForms, old-style csproj, `packages.config` (Costura.Fody, Fody, Magick.NET, WindowsAPICodePack) | .NET Framework **4.8**, `WinExe`, Windows-only | **`ProjectReference` to sibling repo** `..\..\FE3D\FE3D\FE3D.csproj` + `..\..\FE3D\FE3D.Graphics\...` | **No** — sibling repo absent on checkout |
| `BCH.TextureCore/` | SDK-style class library | `net8.0`, `SixLabors.ImageSharp 3.1.5` | Vendored `libs/*.dll` via `<Reference><HintPath>` | Yes |
| `BCH.TextureTool.Avalonia/` | SDK-style Avalonia app | `net8.0`, `WinExe`, Avalonia `12.0.4` | Vendored `libs/*.dll` via `<Reference><HintPath>`, `ProjectReference` to TextureCore | Yes (any OS) |

**Key constraints:**
- **Vendored libs are committed.** `libs/FE3D.dll`, `libs/FE3D.Graphics.dll`, `libs/System.Numerics.Vectors.dll` are tracked (`git ls-files libs/` returns all three, ~480 KB total, all AnyCPU managed IL). `.gitignore` does not ignore `libs/`. A plain `actions/checkout` is sufficient; `<Reference><HintPath>` defaults to CopyLocal, so they land in publish output and get embedded by single-file automatically. No NuGet, no submodule, no git-lfs.
- **The Avalonia project is merged into `main`.** CI builds and releases are cut from `main` (the `avalonia-port` branch is also still listed as a trigger for in-progress work).
- **`OutputType=WinExe` is cross-platform-safe**: on non-Windows, .NET treats `WinExe` as `Exe`. `app.manifest` is Windows-only and ignored on Linux.
- **No `<Version>` property** anywhere; no `global.json`; no `nuget.config`; no `packages.lock.json`; no `.github/` directory yet.
- **`Program.cs` registers `CodePagesEncodingProvider`** (Shift-JIS/cp932 for ARC kanji names — the bug fixed in commit `596635d`). This is reflection/data-table based and is a trimming hazard.
- **ImageSharp must be on `3.1.11`.** `3.1.5` carries advisories `NU1903` (high), `NU1902`/`NU1908` (moderate), including CVE-2025-27598 (out-of-bounds write) and CVE-2025-54575 (GIF-decoder infinite loop). `3.1.11` is the latest `3.1.x` patch and clears all known advisories. Bump `BCH.TextureCore/BCH.TextureCore.csproj` from `3.1.5` to `3.1.11` (a drop-in patch bump — no API changes) as part of this work.
- **The solution name has a space** (`BCH Texture Tool.sln`) and contains the broken-on-CI legacy project. Always target `BCH.TextureTool.Avalonia.csproj` directly and quote the path.

## 4. Key Decision: Which Windows binary do we ship?

**Decision: Ship the Avalonia `net8.0` self-contained `win-x64` binary. Do NOT build or ship the legacy WinForms .NET Framework 4.8 app in CI.**

**Justification — the WinForms path is externally coupled and fragile:**

The legacy `BCH Texture Tool.csproj` pulls FE3D in via **`ProjectReference` to `..\..\FE3D\FE3D\FE3D.csproj`**, a **separate sibling git repo** (`VelouriasMoon/FE3D`) that exists only on the developer's machine. It is **not a submodule and not committed** to this repo. On a clean `actions/checkout`, that path does not exist and the build fails. Shipping it via CI would require:
- A **second `actions/checkout`** of `VelouriasMoon/FE3D` into the exact sibling path, **pinned to a commit SHA** (upstream HEAD can break the build at any time, and the local copy may be patched relative to upstream).
- `windows-latest` + MSBuild + `nuget restore` against `packages.config` (Costura.Fody, Magick.NET) — a completely different toolchain from `dotnet`.
- An AssemblyInfo rewrite to honor tag-derived versioning (the old-style project can't take `-p:Version` cleanly through nuget/Costura).

That is the single largest source of CI fragility, for **no end-user upside**: the Avalonia `win-x64` build already gives Windows users a native double-click GUI, builds purely from committed `libs/`, needs no external repo or nuget/Costura, and shares 100% of its code with the Linux build.

**Trade-off acknowledged:** The README currently describes the Windows app as the .NET Framework WinForms build. Shipping Avalonia for Windows means a (potentially) different UI for Windows users and a follow-up README edit. We accept this: a **single cross-platform codebase** is worth far more than preserving a Windows-only fork that can't even build in CI without cloning a second repo. The legacy WinForms project remains in the repo for local/manual builds; it is simply not a CI deliverable.

The proposed workflow keeps a disabled (`if: false`) `build-winforms` job stub so Option B is trivially enableable later if a strong reason emerges — but it is off by default.

## 5. Requirements

### 5.1 Windows build

- **Runner:** `windows-latest` — native target OS validates the `WinExe`/manifest subsystem. (Cross-compiling `win-x64` on `ubuntu-latest` also works and is cheaper; we choose `windows-latest` for on-OS validation. This is the matrix's Windows leg.)
- **SDK:** .NET 8 via `actions/setup-dotnet@v4`, `dotnet-version: 8.0.x`.
- **Target:** `BCH.TextureTool.Avalonia/BCH.TextureTool.Avalonia.csproj` (quoted, never the `.sln`).
- **Publish:** self-contained, single-file, `win-x64`, native libs self-extracted, symbols off, **trimming off**:
  ```
  dotnet publish "BCH.TextureTool.Avalonia/BCH.TextureTool.Avalonia.csproj" \
    -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishTrimmed=false \
    -p:DebugType=none -p:DebugSymbols=false \
    -p:Version=<tag-or-ci> -o publish/win-x64
  ```
- **`IncludeNativeLibrariesForSelfExtract=true` is mandatory.** Without it, single-file leaves `libSkiaSharp.dll`, `libHarfBuzzSharp.dll`, `av_libglesv2.dll` loose, and the exe **silently fails to start on a clean machine**.
- **Output:** `publish/win-x64/BCH.TextureTool.Avalonia.exe` (~96 MB). Two droppable native `.pdb`s (SkiaSharp/HarfBuzz NuGet symbols) may appear; exclude them from the artifact.
- **End-user requirement:** none. No .NET install, no extra DLLs. Double-click runs.

### 5.2 Linux build

- **Runner:** `ubuntu-latest` (Ubuntu 24.04). No `apt install` needed at build time — Skia/HarfBuzz native blobs ship inside NuGet runtime packages.
- **SDK:** .NET 8 via `actions/setup-dotnet@v4`, `dotnet-version: 8.0.x`.
- **Publish:** self-contained, single-file, `linux-x64`, native `.so` self-extracted, **trimming off**:
  ```
  dotnet publish "BCH.TextureTool.Avalonia/BCH.TextureTool.Avalonia.csproj" \
    -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishTrimmed=false \
    -p:DebugType=none \
    -p:Version=<tag-or-ci> -o publish/linux-x64
  ```
- **Output:** `publish/linux-x64/BCH.TextureTool.Avalonia` (no extension; ~70–110 MB).
- **Exec bit + packaging:** GitHub's `upload-artifact` zip **drops the Unix exec bit**. Package the Linux binary as **`.tar.gz`** (tar preserves the bit) after `chmod +x`:
  ```
  chmod +x "publish/linux-x64/BCH.TextureTool.Avalonia"
  tar -C publish/linux-x64 -czf BCH-Texture-Editor-linux-x64.tar.gz .
  ```
- **End-user requirement:** none for .NET. Document (not a CI step) that the desktop machine needs standard X11/font libs (`libx11-6 libice6 libsm6 libfontconfig1 libfreetype6`); Wayland runs via XWayland; GTK is not required.

### 5.3 Workflow structure, triggers & release

**Triggers:**
```yaml
on:
  push:
    branches: [main, avalonia-port]   # CI build + artifacts, no release
    tags: ['v*']                      # full build + GitHub Release
  pull_request:
    branches: [main, avalonia-port]   # CI build only
  workflow_dispatch:                  # manual rerun; releases only if tagged
```
- **Tag push `v*` is the release signal** (the tag string is the version → single source of truth, sidesteps the no-`<Version>` problem). Chosen over `on: release` because tag-driven builds create the release with assets atomically and re-run cleanly.
- PR/branch runs and `workflow_dispatch` upload artifacts only; the release job is gated on `startsWith(github.ref, 'refs/tags/v')`.

**Job layout:** A **matrix** over the two Avalonia targets (`build-avalonia`: `win-x64` / `linux-x64`), plus a **`release`** aggregation job (`needs: build-avalonia`, runs on `ubuntu-latest`). A disabled `build-winforms` stub (`if: false`) is retained for future Option B.

**Caching:** No lockfile exists. Use `actions/setup-dotnet@v4` `cache: true` keyed on the two SDK-style csprojs. If that errors for lack of `packages.lock.json` on the runner image, fall back to `actions/cache@v4` on `~/.nuget/packages` keyed on `hashFiles('**/*.csproj')`. (The workflow below uses the manual `actions/cache` form as the robust default.)

**Permissions / concurrency:**
- Workflow default `contents: read`; the `release` job elevates to `contents: write`.
- `concurrency` group per `workflow`+`ref`, `cancel-in-progress: true` for branch/PR but `false` for tags (don't cancel an in-flight release).

**Packaging / naming (stable, unversioned filenames — the tag conveys the version):**
- `BCH-Texture-Editor-win-x64.zip` (zip the win publish folder via `Compress-Archive`)
- `BCH-Texture-Editor-linux-x64.tar.gz` (tar preserves the exec bit)

**Release:** `softprops/action-gh-release@v2` (pinned to a SHA in practice) — it upserts the release for the tag, which matters on re-runs. `generate_release_notes: true`.

## 6. Proposed workflow file

`.github/workflows/build.yml`:

```yaml
name: build-and-release

on:
  push:
    branches: [main, avalonia-port]
    tags: ['v*']
  pull_request:
    branches: [main, avalonia-port]
  workflow_dispatch:

permissions:
  contents: read   # release job elevates to write

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ !startsWith(github.ref, 'refs/tags/') }}

jobs:
  # --------------------------------------------------------------------------
  # Avalonia net8.0 cross-platform build. Builds from committed libs/*.dll.
  # Targets the .csproj directly (NEVER the .sln — it contains the
  # unbuildable .NET Framework WinForms project).
  # --------------------------------------------------------------------------
  build-avalonia:
    strategy:
      fail-fast: false
      matrix:
        include:
          - os: windows-latest
            rid: win-x64
            asset: BCH-Texture-Editor-win-x64.zip
          - os: ubuntu-latest
            rid: linux-x64
            asset: BCH-Texture-Editor-linux-x64.tar.gz
    runs-on: ${{ matrix.os }}
    steps:
      - name: Checkout (includes committed libs/*.dll)
        uses: actions/checkout@v4

      - name: Setup .NET 8 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Derive version
        id: ver
        shell: bash
        run: |
          if [[ "${GITHUB_REF_TYPE}" == "tag" ]]; then
            echo "version=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"
          else
            echo "version=0.0.0-ci.${GITHUB_RUN_NUMBER}" >> "$GITHUB_OUTPUT"
          fi

      - name: Restore
        run: dotnet restore "BCH.TextureTool.Avalonia/BCH.TextureTool.Avalonia.csproj" -r ${{ matrix.rid }}

      - name: Publish self-contained single-file (${{ matrix.rid }})
        shell: bash
        run: |
          dotnet publish "BCH.TextureTool.Avalonia/BCH.TextureTool.Avalonia.csproj" \
            -c Release \
            -r ${{ matrix.rid }} \
            --self-contained true \
            --no-restore \
            -p:PublishSingleFile=true \
            -p:IncludeNativeLibrariesForSelfExtract=true \
            -p:PublishTrimmed=false \
            -p:DebugType=none \
            -p:DebugSymbols=false \
            -p:Version=${{ steps.ver.outputs.version }} \
            -o "publish/${{ matrix.rid }}"

      # Package — Linux: tar.gz preserves the exec bit (artifact zip does not).
      - name: Package (Linux)
        if: runner.os == 'Linux'
        run: |
          chmod +x "publish/${{ matrix.rid }}/BCH.TextureTool.Avalonia"
          rm -f publish/${{ matrix.rid }}/*.pdb
          tar -C "publish/${{ matrix.rid }}" -czf "${{ matrix.asset }}" .

      # Package — Windows: zip the single-file exe (drop stray native .pdb).
      - name: Package (Windows)
        if: runner.os == 'Windows'
        shell: pwsh
        run: |
          Remove-Item "publish/${{ matrix.rid }}/*.pdb" -ErrorAction SilentlyContinue
          Compress-Archive -Path "publish/${{ matrix.rid }}/*" -DestinationPath "${{ matrix.asset }}"

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: ${{ matrix.asset }}
          path: ${{ matrix.asset }}
          if-no-files-found: error
          retention-days: 14

  # --------------------------------------------------------------------------
  # OPTIONAL Option B: legacy .NET Framework 4.8 WinForms (Costura single exe).
  # Disabled. Requires an external VelouriasMoon/FE3D checkout as a SIBLING
  # because "BCH Texture Tool.csproj" ProjectReferences ..\..\FE3D\...csproj.
  # Flip `if` to enable; pin the FE3D ref to a commit SHA.
  # --------------------------------------------------------------------------
  build-winforms:
    if: ${{ false }}
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
        with: { path: BCH-Texture-Editor }
      - uses: actions/checkout@v4
        with:
          repository: VelouriasMoon/FE3D
          ref: <PIN-A-COMMIT-SHA>
          path: FE3D
      - name: Add MSBuild to PATH
        uses: microsoft/setup-msbuild@v2
      - name: NuGet restore
        run: nuget restore "BCH-Texture-Editor\BCH Texture Tool.sln"
      - name: Build (Costura single exe)
        run: msbuild "BCH-Texture-Editor\BCH Texture Tool\BCH Texture Tool.csproj" /p:Configuration=Release
      - name: Zip
        shell: pwsh
        run: Compress-Archive -Path "BCH-Texture-Editor/BCH Texture Tool/bin/Release/BCH Texture Tool.exe" -DestinationPath "BCH-Texture-Editor-winforms-win-x64.zip"
      - uses: actions/upload-artifact@v4
        with:
          name: BCH-Texture-Editor-winforms-win-x64.zip
          path: BCH-Texture-Editor-winforms-win-x64.zip
          if-no-files-found: error

  # --------------------------------------------------------------------------
  # Release — only on v* tag push. Gathers artifacts, attaches to a Release.
  # --------------------------------------------------------------------------
  release:
    if: startsWith(github.ref, 'refs/tags/v')
    needs: [build-avalonia]
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - name: Download all artifacts
        uses: actions/download-artifact@v4
        with:
          path: dist
          merge-multiple: true

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2   # pin to a commit SHA in practice
        with:
          name: ${{ github.ref_name }}
          generate_release_notes: true
          fail_on_unmatched_files: true
          files: |
            dist/BCH-Texture-Editor-win-x64.zip
            dist/BCH-Texture-Editor-linux-x64.tar.gz
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## 7. Risks & Open Questions

1. **Trimming must stay OFF.** ImageSharp (reflection-based codec/pixel-format discovery), Avalonia 12 (XAML/Skia/X11 reflection), and `CodePagesEncodingProvider` (Shift-JIS data tables — the exact thing commit `596635d` fixed) will silently break under the IL trimmer with runtime `MissingMethod`/`TypeLoad`/`XamlLoadException` that don't surface at build time. The vendored FE3D DLLs have no trim annotations. Cost: ~70–110 MB binaries. Accepted. Do not let "the exe is huge, let's trim it" creep in without a dedicated smoke-test job.
2. **`IncludeNativeLibrariesForSelfExtract=true` is mandatory** with single-file, or Skia/HarfBuzz native libs aren't bundled and the app fails to start on a clean machine.
3. **Single-file self-extract uses a temp dir at first launch.** On locked-down machines with no-execute temp dirs this can fail. Fallback: drop `PublishSingleFile` and ship a zipped publish folder.
4. **ImageSharp bump to `3.1.11` (decided).** `3.1.5` advisories (`NU1903`/`NU1902`/`NU1908`, incl. CVE-2025-27598 and CVE-2025-54575) are non-blocking today but will fail CI the moment `-warnaserror` or `dotnet restore` audit gating is added. Bump `BCH.TextureCore.csproj` to `3.1.11` (drop-in patch, no API changes) as part of this work; re-run the preview/import/export paths once to confirm no regression.
5. **Branch reality (resolved).** The Avalonia project is merged into `main`. Releases are cut from `main`; the `avalonia-port` trigger remains for any in-progress work on that branch.
6. **`setup-dotnet` `cache: true` vs manual cache.** The workflow uses manual `actions/cache` (robust without a lockfile). **Open question / optional hardening:** enable `RestorePackagesWithLockFile` and commit `packages.lock.json` for reproducible, lockfile-keyed caching (a repo change, out of CI scope).
7. **`upload-artifact@v4` immutability.** Artifact names must be unique per run (keyed on the asset name here) and can't be re-uploaded within a run.
8. **Third-party action trust.** Pin `softprops/action-gh-release@v2` to a commit SHA; or substitute the first-party `gh release create … --generate-notes` CLI in the release job.
9. **No ARM64 / macOS.** Out of scope; add matrix legs (`win-arm64`, `linux-arm64`) later — SkiaSharp/HarfBuzz ship arm64 natives but it's untested here.
10. **README drift (resolved).** The README has been updated to present the Avalonia self-contained downloads (`BCH-Texture-Editor-win-x64.zip` / `-linux-x64.tar.gz`) as the supported build on every platform, and notes the legacy WinForms app is local-build-only.
11. **Optional filename match.** The README references `BCH Texture Tool.exe`; the artifact is `BCH.TextureTool.Avalonia.exe`. **Open question:** rename via `-p:AssemblyName="BCH Texture Tool"` to match docs (changes internal assembly name), or keep the project name? Recommend keeping the project name and fixing the README.

## 8. Acceptance Criteria

- [ ] `.github/workflows/build.yml` exists and is valid YAML.
- [ ] A push to `avalonia-port` triggers `build-avalonia` and produces **two** artifacts: `BCH-Texture-Editor-win-x64.zip` and `BCH-Texture-Editor-linux-x64.tar.gz`.
- [ ] The Windows artifact contains `BCH.TextureTool.Avalonia.exe` (~96 MB) and **no** loose native DLLs or `.pdb`s.
- [ ] The Linux artifact's tarball contains the `BCH.TextureTool.Avalonia` binary with the **executable bit set**.
- [ ] Neither build targets `BCH Texture Tool.sln`; both target `BCH.TextureTool.Avalonia.csproj` directly.
- [ ] Both publishes use `--self-contained true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, and `PublishTrimmed=false`.
- [ ] No `apt install` / extra DLL / second-repo checkout is required for either Avalonia build (plain `actions/checkout` only); the `build-winforms` job is present but `if: false`.
- [ ] On a clean Windows machine **without .NET installed**, the downloaded `.exe` launches by double-click.
- [ ] On a clean Linux desktop **without .NET installed**, `tar xzf … && ./BCH.TextureTool.Avalonia` launches (standard X11/font libs present).
- [ ] A pushed `v1.2.3` tag creates a GitHub Release named `v1.2.3` with both binaries attached and auto-generated notes; the produced binaries report version `1.2.3`.
- [ ] PR and `workflow_dispatch` runs upload artifacts but do **not** create a Release.
- [ ] Workflow default permission is `contents: read`; only the `release` job has `contents: write`.
- [ ] `concurrency` cancels in-progress branch/PR runs but not tag runs.
- [ ] `BCH.TextureCore.csproj` references `SixLabors.ImageSharp` `3.1.11` (not `3.1.5`); CI restore reports no `NU1903`/`NU1902`/`NU1908` advisories.
