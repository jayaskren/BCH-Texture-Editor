# Tasks: GitHub Actions Cross-Platform Build & Release

Slug: github-actions-build
Generated from: [plan.md](plan.md)
Generated: 2026-05-31

## Conventions

- `- [ ]` open, `- [X]` done.
- `[P]` marks parallelizable tasks (different files, no ordering dependency).
- Each task names the primary file in scope; all other files are read-only for that task.
- Every task ends with a Checkpoint — validators re-run these. All Checkpoints run from the repo root on Linux.

## Phase 1: Dependency bump (baseline first)

- [X] T001 [Phase 1] `BCH.TextureCore/BCH.TextureCore.csproj`: change the `SixLabors.ImageSharp` `PackageReference` version from `3.1.5` to `3.1.11`.
  Checkpoint: `grep -q 'SixLabors.ImageSharp" Version="3.1.11"' BCH.TextureCore/BCH.TextureCore.csproj && dotnet restore BCH.TextureCore/BCH.TextureCore.csproj 2>&1 | grep -E 'NU1903|NU1902|NU1908' ; test $? -ne 0` — checkpoint passed (restore clean, no advisories)

- [X] T002 [Phase 1] `BCH.TextureTool.Avalonia/BCH.TextureTool.Avalonia.csproj`: build the app in Release against the bumped dependency to confirm no compile/restore regression (manual `.arc` open/export smoke noted in plan Phase 1 exit).
  Checkpoint: `dotnet build BCH.TextureTool.Avalonia/BCH.TextureTool.Avalonia.csproj -c Release` — checkpoint passed (build succeeded, 0 warnings)

## Phase 2: Core build & release pipeline (no .deb yet)

- [X] T003 [Phase 2] `.github/workflows/build.yml`: create the workflow with `name`, `on` triggers (`push` to `main` + tags `v*`, `pull_request` to `main`, `workflow_dispatch`), top-level `permissions: contents: read`, `concurrency` (cancel-in-progress only for non-tags), and the `build-avalonia` matrix job (`windows-latest→win-x64`, `ubuntu-latest→linux-x64`) with steps: checkout, setup-dotnet 8.0.x, cache `~/.nuget/packages`, derive version (tag → `GITHUB_REF_NAME`; else `0.0.0-ci.<run>`), restore, and `dotnet publish` self-contained single-file with `PublishTrimmed=false`.
  Checkpoint: `python3 -c 'import yaml; yaml.safe_load(open(".github/workflows/build.yml"))' && grep -q 'PublishTrimmed=false' .github/workflows/build.yml && grep -q 'win-x64' .github/workflows/build.yml && grep -q 'linux-x64' .github/workflows/build.yml`

- [X] T004 [Phase 2] `.github/workflows/build.yml`: add the packaging + upload steps to `build-avalonia` — Windows `Compress-Archive` → `BCH-Texture-Editor-<rid>.zip`; Linux `chmod +x` + `tar -czf` → `BCH-Texture-Editor-<rid>.tar.gz`; drop stray `.pdb`; `actions/upload-artifact@v4` with `if-no-files-found: error`. (Filenames are parameterized via `${{ matrix.rid }}`, which resolves to `win-x64`/`linux-x64`; the literal final names appear in the release job — T005.)
  Checkpoint: `python3 -c 'import yaml; yaml.safe_load(open(".github/workflows/build.yml"))' && grep -Fq 'BCH-Texture-Editor-${{ matrix.rid }}.zip' .github/workflows/build.yml && grep -Fq 'BCH-Texture-Editor-${{ matrix.rid }}.tar.gz' .github/workflows/build.yml && grep -q 'if-no-files-found: error' .github/workflows/build.yml`

- [X] T005 [Phase 2] `.github/workflows/build.yml`: add the `release` job — `if: startsWith(github.ref, 'refs/tags/v')`, `needs: [build-avalonia]`, `permissions: contents: write`, `actions/download-artifact@v4` (merge-multiple), and `softprops/action-gh-release@v2` attaching the artifacts with `generate_release_notes: true`.
  Checkpoint: `python3 -c 'import yaml; yaml.safe_load(open(".github/workflows/build.yml"))' && grep -q "startsWith(github.ref, 'refs/tags/v')" .github/workflows/build.yml && grep -q 'softprops/action-gh-release@v2' .github/workflows/build.yml`

- [X] T006 [Phase 2] `.github/workflows/build.yml`: verify the Linux publish + tarball commands locally (phase exit) — run the exact publish then tar, and assert the binary carries the executable bit inside the archive.
  Checkpoint: `dotnet publish BCH.TextureTool.Avalonia/BCH.TextureTool.Avalonia.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o /tmp/pub-linux && chmod +x /tmp/pub-linux/BCH.TextureTool.Avalonia && tar -C /tmp/pub-linux -czf /tmp/lin.tar.gz . && tar -tvzf /tmp/lin.tar.gz | grep -E '^-rwx.*BCH.TextureTool.Avalonia$'`

## Phase 3: Debian packaging

- [X] T007 [P] [Phase 3] `packaging/linux/control.template`: create the Debian `control` template with `Package: bch-texture-editor`, `Version: __VERSION__`, `Architecture: amd64`, `Maintainer`, `Section: graphics`, `Priority: optional`, `Homepage`, a `Depends` alternatives list (`libc6, libicu74 | libicu72 | libicu70 | libicu66, libx11-6, libsm6, libice6, libfontconfig1, libfreetype6`), and a `Description`.
  Checkpoint: `grep -q '^Package: bch-texture-editor$' packaging/linux/control.template && grep -q '^Architecture: amd64$' packaging/linux/control.template && grep -q '__VERSION__' packaging/linux/control.template && grep -q '^Depends:.*libicu' packaging/linux/control.template`

- [X] T008 [P] [Phase 3] `packaging/linux/bch-texture-editor.desktop`: create the XDG desktop entry (`Type=Application`, `Name=BCH Texture Editor`, `Exec=bch-texture-editor`, `Icon=bch-texture-editor`, `Terminal=false`, `Categories=Graphics;Utility;`).
  Checkpoint: `grep -q '^Exec=bch-texture-editor$' packaging/linux/bch-texture-editor.desktop && grep -q '^Icon=bch-texture-editor$' packaging/linux/bch-texture-editor.desktop && grep -q '^Categories=Graphics;Utility;$' packaging/linux/bch-texture-editor.desktop && { command -v desktop-file-validate >/dev/null && desktop-file-validate packaging/linux/bch-texture-editor.desktop || true; }`

- [X] T009 [P] [Phase 3] `packaging/linux/bch-texture-editor`: create the `/usr/bin` wrapper script (`#!/bin/sh` then `exec /opt/bch-texture-editor/BCH.TextureTool.Avalonia "$@"`).
  Checkpoint: `head -1 packaging/linux/bch-texture-editor | grep -q '^#!/bin/sh$' && grep -q 'exec /opt/bch-texture-editor/BCH.TextureTool.Avalonia' packaging/linux/bch-texture-editor && sh -n packaging/linux/bch-texture-editor`

- [X] T010 [P] [Phase 3] `packaging/linux/bch-texture-editor.png`: confirm the user-supplied application icon is committed at the expected path and is a 256×256 PNG (asset already copied into the repo; this task verifies it, no creation needed).
  Checkpoint: `test -f packaging/linux/bch-texture-editor.png && file packaging/linux/bch-texture-editor.png | grep -q '256 x 256'`

- [X] T011 [Phase 3] `.github/workflows/build.yml`: add the `.deb` assembly to the Linux leg — stage `pkgroot/opt/bch-texture-editor/` (publish output), `pkgroot/usr/bin/bch-texture-editor` (mode 0755), `pkgroot/usr/share/applications/bch-texture-editor.desktop`, `pkgroot/usr/share/icons/hicolor/256x256/apps/bch-texture-editor.png`, `pkgroot/DEBIAN/control` (sed `__VERSION__` → Debian-sanitized version), then `dpkg-deb --root-owner-group --build pkgroot` → `bch-texture-editor_<version>_amd64.deb`; upload it and add it to the `release` job's `files`.
  Checkpoint: `python3 -c 'import yaml; yaml.safe_load(open(".github/workflows/build.yml"))' && grep -q 'dpkg-deb --root-owner-group --build' .github/workflows/build.yml && grep -q 'amd64.deb' .github/workflows/build.yml && grep -q 'hicolor/256x256/apps' .github/workflows/build.yml`

- [X] T012 [Phase 3] `.github/workflows/build.yml`: build the `.deb` locally end-to-end (phase exit) — stage the tree from the Phase 2 publish output, substitute version, run `dpkg-deb --build`, and inspect metadata + contents (including the icon).
  Checkpoint: `rm -rf /tmp/pkgroot && mkdir -p /tmp/pkgroot/opt/bch-texture-editor /tmp/pkgroot/usr/bin /tmp/pkgroot/usr/share/applications /tmp/pkgroot/usr/share/icons/hicolor/256x256/apps /tmp/pkgroot/DEBIAN && cp -r /tmp/pub-linux/. /tmp/pkgroot/opt/bch-texture-editor/ && install -m0755 packaging/linux/bch-texture-editor /tmp/pkgroot/usr/bin/bch-texture-editor && cp packaging/linux/bch-texture-editor.desktop /tmp/pkgroot/usr/share/applications/ && cp packaging/linux/bch-texture-editor.png /tmp/pkgroot/usr/share/icons/hicolor/256x256/apps/ && sed 's/__VERSION__/1.2.3/' packaging/linux/control.template > /tmp/pkgroot/DEBIAN/control && dpkg-deb --root-owner-group --build /tmp/pkgroot /tmp/bch-texture-editor_1.2.3_amd64.deb && dpkg-deb --info /tmp/bch-texture-editor_1.2.3_amd64.deb | grep -E 'Package: bch-texture-editor|Architecture: amd64|Version: 1.2.3' && dpkg-deb --contents /tmp/bch-texture-editor_1.2.3_amd64.deb | grep -E '/opt/bch-texture-editor/BCH.TextureTool.Avalonia$|/usr/bin/bch-texture-editor$|bch-texture-editor.desktop$|apps/bch-texture-editor.png$'`
