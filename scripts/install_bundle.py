#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path

PACKAGE_GIT_URL = "https://github.com/UnityX103/Unity-visualtest-skill.git?path=/package/com.nz.visualtest#main"
PACKAGE_FILE_URL = "file:../LocalPackage/com.nz.visualtest"


def copy_tree(source: Path, destination: Path, overwrite: bool) -> None:
    if destination.exists():
        if not overwrite:
            raise FileExistsError(f"Destination already exists: {destination}")
        shutil.rmtree(destination)
    shutil.copytree(source, destination)


def copy_file(source: Path, destination: Path, overwrite: bool) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and not overwrite:
        raise FileExistsError(f"Destination already exists: {destination}")
    shutil.copy2(source, destination)


def ensure_manifest_dependency(
    manifest_path: Path,
    dependency_value: str,
    force_dependency: bool,
) -> str:
    if not manifest_path.exists():
        raise FileNotFoundError(f"manifest not found: {manifest_path}")

    data = json.loads(manifest_path.read_text(encoding="utf-8"))
    dependencies = data.setdefault("dependencies", {})
    current_value = dependencies.get("com.nz.visualtest")

    if current_value == dependency_value:
        return "already-configured"

    if current_value and not force_dependency:
        return f"kept-existing:{current_value}"

    dependencies["com.nz.visualtest"] = dependency_value
    manifest_path.write_text(
        json.dumps(data, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    return f"configured:{dependency_value}"


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Install the global Unity visual video test bundle into a Unity project."
    )
    parser.add_argument("--project", required=True, help="Unity project root directory")
    parser.add_argument(
        "--package-dst",
        default="LocalPackage/com.nz.visualtest",
        help="Relative destination for the vendored package snapshot",
    )
    parser.add_argument(
        "--binary-dst",
        default="Assets/StreamingAssets/FFmpegOut/macOS",
        help="Relative destination for bundled macOS binaries",
    )
    parser.add_argument(
        "--vendor-package",
        action="store_true",
        help="Copy the package snapshot into the project for local editing",
    )
    parser.add_argument(
        "--skip-manifest",
        action="store_true",
        help="Do not auto-configure Packages/manifest.json",
    )
    parser.add_argument(
        "--force-dependency",
        action="store_true",
        help="Replace an existing com.nz.visualtest manifest entry with the standard value",
    )
    parser.add_argument("--overwrite", action="store_true", help="Replace existing files")
    args = parser.parse_args()

    skill_dir = Path(__file__).resolve().parent.parent
    package_source = skill_dir / "package" / "com.nz.visualtest"
    binary_source_dir = skill_dir / "assets" / "bin" / "macos-arm64"

    project_root = Path(args.project).expanduser().resolve()
    if not project_root.exists():
        print(f"ERROR: project root not found: {project_root}", file=sys.stderr)
        return 2

    manifest_path = project_root / "Packages" / "manifest.json"

    if args.vendor_package and not package_source.exists():
        print(f"ERROR: package source missing: {package_source}", file=sys.stderr)
        return 2

    if not binary_source_dir.exists():
        print(f"ERROR: binary source missing: {binary_source_dir}", file=sys.stderr)
        return 2

    binary_destination_dir = project_root / args.binary_dst

    if args.vendor_package:
        package_destination = project_root / args.package_dst
        copy_tree(package_source, package_destination, overwrite=args.overwrite)
    copy_file(binary_source_dir / "ffmpeg", binary_destination_dir / "ffmpeg", overwrite=args.overwrite)
    copy_file(binary_source_dir / "ffprobe", binary_destination_dir / "ffprobe", overwrite=args.overwrite)

    if args.vendor_package:
        print(f"Installed package: {package_destination}")
    print(f"Installed binary: {binary_destination_dir / 'ffmpeg'}")
    print(f"Installed binary: {binary_destination_dir / 'ffprobe'}")
    print()

    dependency_value = PACKAGE_FILE_URL if args.vendor_package else PACKAGE_GIT_URL
    if args.skip_manifest:
        manifest_result = "skipped"
    else:
        manifest_result = ensure_manifest_dependency(
            manifest_path=manifest_path,
            dependency_value=dependency_value,
            force_dependency=args.force_dependency,
        )

    print(f"Manifest: {manifest_result}")
    print()
    print("Next step:")
    if args.skip_manifest:
        if args.vendor_package:
            print(f'Add `"com.nz.visualtest": "{PACKAGE_FILE_URL}"` to Packages/manifest.json if needed.')
        else:
            print(f'Add `"com.nz.visualtest": "{PACKAGE_GIT_URL}"` to Packages/manifest.json if needed.')
    elif args.vendor_package:
        print("Let Unity reimport after the manifest switches to the vendored file dependency.")
    else:
        print("Let Unity reimport after the manifest points at the shared Git dependency.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
