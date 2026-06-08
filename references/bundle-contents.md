# Bundle Contents

## Included Package

This skill vendors a snapshot of:

- `package/com.nz.visualtest`

Source snapshot came from:

- `/Users/xpy/Desktop/NanZhai/MIDA2025/LocalPackage/com.nz.visualtest`

What it contains:

- `Runtime/VisualTestBase.cs`
- `Runtime/VisualTestGuiHelper.cs`
- `Runtime/InputTestUtility.cs`
- `FFmpegOut/` runtime, editor, shader, and internal pipe code
- package metadata and asmdefs

## Included Binaries

Current bundled binaries:

- `assets/bin/macos-arm64/ffmpeg`
- `assets/bin/macos-arm64/ffprobe`

Resolved from local machine paths:

- `/opt/homebrew/Cellar/ffmpeg/8.1_1/bin/ffmpeg`
- `/opt/homebrew/Cellar/ffmpeg/8.1_1/bin/ffprobe`

Version:

- `ffmpeg version 8.1`
- `ffprobe version 8.1`

## Why These Are Bundled

- The package snapshot makes this skill repo the source of truth for the reusable Unity visual-test package.
- The ffmpeg binary allows Unity projects to use the package without depending on Homebrew PATH state.
- The ffprobe binary is useful for artifact validation and debugging.

## Expected Consumption In A Unity Project

- Package dependency:
  - `https://github.com/UnityX103/Unity-visualtest-skill.git?path=/package/com.nz.visualtest#main`
- Binaries:
  - `<project>/Assets/StreamingAssets/FFmpegOut/macOS/ffmpeg`
  - `<project>/Assets/StreamingAssets/FFmpegOut/macOS/ffprobe`

If a project needs a local editable copy of the package, `scripts/install_bundle.py --vendor-package` can still install:

- `<project>/LocalPackage/com.nz.visualtest`

By default the installer also auto-adds the Git dependency to `Packages/manifest.json` when `com.nz.visualtest` is missing.

## Follow-up Work If You Need Cross-Platform Support

- Add Windows binary:
  - `assets/bin/windows-x64/ffmpeg.exe`
- Add Linux binary:
  - `assets/bin/linux-x64/ffmpeg`
- Extend `scripts/install_bundle.py` to install the correct platform folder
