# Workflow

## When To Use This Global Skill

Use this skill when the job is one of these:

- bootstrap a Unity project with a reusable Git-backed video visual-test package
- point a Unity project at the standard visual-test package repository
- install or refresh bundled ffmpeg binaries for Unity runtime recording
- provide a clean shared base for project-specific visual-test code and docs

## Recommended Layering

Split responsibilities like this:

- Global skill: package repo, binaries, installation, shared recorder conventions
- Target project: scene setup, level setup, manual runner, test flow, assertions, artifact delivery

## Shared Recorder Conventions

The shared package is built around these assumptions:

- video artifact output under `Application.persistentDataPath/TestOutput/<TestClass>/Video`
- PlayMode test inheritance from `NZ.VisualTest.VisualTestBase`
- ffmpeg available either from PATH or from `StreamingAssets/FFmpegOut/<platform>/ffmpeg`

## Minimal Project Bootstrap

1. Run `scripts/install_bundle.py --project <unity-project-root>`.
2. The script installs the bundled ffmpeg binaries into the project root.
3. If `com.nz.visualtest` is missing from `Packages/manifest.json`, the script adds the Git dependency automatically.
3. Create a PlayMode test assembly that references the package.
4. Inherit a test class from `NZ.VisualTest.VisualTestBase`.
5. Add project-specific input and assertions.

If the project needs to patch the shared package locally, vendor it with `scripts/install_bundle.py --vendor-package --force-dependency` so the manifest switches to `file:../LocalPackage/com.nz.visualtest`.

## If A Project Needs A Custom Recorder

Do not modify the global bundle by default. Prefer one of these:

- subclass or wrap behavior inside the target project
- copy the package into the target project and patch there
- only update the global bundle if the improvement is meant to become the standard for future projects
