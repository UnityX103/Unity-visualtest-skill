---
name: unity-visual-video-test
description: Standardize Unity MP4-based visual-test setup by bundling a reusable Git-backed `com.nz.visualtest` UPM package plus ffmpeg binaries inside a global skill. Use when Codex needs to bootstrap or reuse a Unity video visual-test stack, point a Unity project at the shared package repository, install bundled ffmpeg binaries into Unity-friendly locations, or build project-specific visual-test flows on top of the shared package.
---

# Unity Visual Video Test

Use this skill as the global standard layer for Unity video visual tests. It carries the reusable UPM package and the local ffmpeg binaries so target projects do not need to vendor them again.

## Quick Start

- Read `references/bundle-contents.md` to see what is bundled and where it came from.
- Use `scripts/install_bundle.py --project <unity-project-root>` to install the bundled ffmpeg binaries and auto-configure `com.nz.visualtest` in the target project manifest when it is missing.
- Read `references/workflow.md` before authoring project-specific tests on top of the shared package.

## What This Skill Owns

- Shared UPM package: `package/com.nz.visualtest`
- Shared binaries:
  - `assets/bin/macos-arm64/ffmpeg`
  - `assets/bin/macos-arm64/ffprobe`
- Shared installation workflow for Unity projects

## What This Skill Does Not Own

- Project-specific level flows
- Project-specific test drivers
- Project-specific delivery rules
- Unity scene or gameplay assertions

Keep those in target-project docs, code, and tests. This global skill is only the reusable base.

## Install Workflow

1. Run the installer script against the Unity project root.
2. Copy `ffmpeg` and `ffprobe` into `Assets/StreamingAssets/FFmpegOut/macOS/`.
3. If `Packages/manifest.json` does not already contain `com.nz.visualtest`, the script adds:
   - `"com.nz.visualtest": "https://github.com/UnityX103/Unity-visualtest-skill.git?path=/package/com.nz.visualtest#main"`
4. Let Unity reimport, then verify compilation.
5. If you need to replace an existing non-standard source, rerun with `--force-dependency`.
6. Only if the project explicitly needs a local editable copy, rerun the installer with `--vendor-package --force-dependency`.

Example:

```bash
python3 scripts/install_bundle.py --project /abs/path/to/UnityProject --overwrite
```

## Platform Notes

- The bundled binaries in this skill are for `macOS arm64` only.
- The bundled package supports PATH-based ffmpeg discovery and `StreamingAssets/FFmpegOut/<platform>/ffmpeg` discovery.
- `ffprobe` is bundled for validation workflows, but the runtime recorder itself only requires `ffmpeg`.

## Validation

- Run `python3 -m py_compile scripts/install_bundle.py`
- Run the installer into a disposable test directory before using it in a real project
- In the Unity project, confirm that:
  - `Assets/StreamingAssets/FFmpegOut/macOS/ffmpeg` exists
  - `Packages/manifest.json` contains `com.nz.visualtest`
  - the project compiles after the dependency is added or updated
  - if `--vendor-package` was used, the package exists under `LocalPackage/com.nz.visualtest`

## Do Not

- Do not put project-specific gameplay logic into this global skill.
- Do not assume Windows or Linux binaries exist unless you bundle them explicitly later.
- Do not use `--force-dependency` unless the user asked to replace an existing dependency source.
