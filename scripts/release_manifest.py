#!/usr/bin/env python3
"""Rebuild or validate the source-only Projecta distribution manifest.

Does not restore packages, build the solution, run tests, or execute source code.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "DISTRIBUTION-MANIFEST.json"
IGNORED_DIRS = frozenset({".git", ".vs", ".idea", ".tools", "artifacts", "bin", "obj", "TestResults", "__pycache__", ".pytest_cache"})


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def property_value(file: Path, name: str) -> str:
    root = ET.parse(file).getroot()
    values = [e.text.strip() for e in root.iter(name) if e.text and e.text.strip()]
    require(len(values) == 1, f"Expected one {name} in {file.relative_to(ROOT)}")
    return values[0]


def ensure_layout(version: str) -> int:
    require(version == "0.2.0", "Expected Projecta release 0.2.0")
    require(property_value(ROOT / "Directory.Build.props", "TargetFramework") == "net10.0", "Unexpected target framework")
    require(property_value(ROOT / "Directory.Build.props", "LangVersion") == "14.0", "Unexpected C# language version")
    require(property_value(ROOT / "Directory.Build.props", "Authors") == "Projecta contributors", "Package authorship not Projecta-only")
    require(f"Version **{version}**" in (ROOT / "README.md").read_text(encoding="utf-8"), "README version drift")
    require((ROOT / "RELEASE_NOTES.md").read_text(encoding="utf-8").startswith(f"# Projecta {version}\n"), "Release notes drift")
    require(f"## {version} " in (ROOT / "CHANGELOG.md").read_text(encoding="utf-8"), "Changelog drift")
    projecta_license = (ROOT / "LICENSE").read_text(encoding="utf-8")
    require("Copyright (c) 2026 ginmp" in projecta_license, "Projecta license copyright missing")
    require("Copyright (c) 2021" not in projecta_license, "Projecta license must not duplicate third-party copyright notices")
    third_party_notices = (ROOT / "THIRD-PARTY-NOTICES.md").read_text(encoding="utf-8")
    require("Copyright (c) 2021 " in third_party_notices, "Incorporated-code copyright notice missing")
    require("Permission is hereby granted" in third_party_notices, "Incorporated-code MIT permission notice missing")
    require("The above copyright notice and this permission notice" in third_party_notices, "Incorporated-code license condition missing")

    projects = sorted(ROOT.rglob("*.csproj"))
    count = 0
    for project in projects:
        if any(p in IGNORED_DIRS for p in project.relative_to(ROOT).parts):
            continue
        doc = ET.parse(project).getroot()
        for reference in doc.iter("ProjectReference"):
            include = reference.get("Include")
            require(bool(include), f"Missing ProjectReference Include in {project}")
            target = (project.parent / include.replace("\\", "/")).resolve()
            require(target.is_relative_to(ROOT.resolve()) and target.is_file(), f"Unresolved project reference: {project}: {include}")
        if project.is_relative_to(ROOT / "src"):
            if any(p.tag == "IsPackable" and (p.text or "").strip().lower() == "true" for p in doc.iter()):
                count += 1
    require(count == 11, f"Expected 11 packable Projecta modules, found {count}")

    compat = ROOT / "tests/Projecta.Compatibility.Tests/Projecta.Compatibility.Tests.csproj"
    require(compat.is_file(), "Compatibility regression test project not found")
    for name in ("Projecta.slnx", "scripts/verify.sh", "scripts/verify.ps1", "src/Projecta.Core/Projecta.Core.csproj"):
        content = (ROOT / name).read_text(encoding="utf-8")
        require("Projecta.Compatibility.Tests" in content, f"Missing compatibility test reference in {name}")
    for solution in ROOT.glob("*.slnx"):
        for project in ET.parse(solution).getroot().iter("Project"):
            path = project.get("Path")
            require(bool(path) and (ROOT / path).is_file(), f"Broken solution path: {solution.name}:{path}")
    return count


def inventory() -> list[dict]:
    entries = []
    folded = set()
    for f in sorted(ROOT.rglob("*"), key=lambda p: p.relative_to(ROOT).as_posix()):
        rel = f.relative_to(ROOT)
        if any(part in IGNORED_DIRS for part in rel.parts) or f == MANIFEST:
            continue
        require(not f.is_symlink(), f"Symlink not permitted in source distribution: {rel}")
        if f.is_file():
            posix = rel.as_posix()
            require(posix.casefold() not in folded, f"Case-insensitive file collision: {posix}")
            folded.add(posix.casefold())
            content = f.read_bytes()
            entries.append({"path": posix, "bytes": len(content), "sha256": hashlib.sha256(content).hexdigest()})
    return entries


def generate() -> dict:
    version = property_value(ROOT / "Directory.Build.props", "Version")
    packages = ensure_layout(version)
    entries = inventory()
    digest_data = json.dumps(entries, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return {
        "schema_version": 1,
        "release": f"Projecta-{version}",
        "tfm": "net10.0",
        "package_count": 0,  # Source manifest does not attest to built package artifacts.
        "packable_project_count": packages,
        "source_entries": len(entries),
        "tree_sha256": hashlib.sha256(digest_data).hexdigest(),
        "build_verification": None,
        "build_verification_status": "not-attested-by-source-manifest",
        "files": entries,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--write", action="store_true", help="write a current source manifest")
    group.add_argument("--check", action="store_true", help="verify current source against the manifest")
    args = parser.parse_args()
    try:
        expected = generate()
        if args.write:
            MANIFEST.write_text(json.dumps(expected, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
            print(f"WROTE {expected['release']} source manifest: {len(expected['files'])} files; tree SHA-256: {expected['tree_sha256']}")
        else:
            require(MANIFEST.is_file(), "Manifest absent")
            actual = json.loads(MANIFEST.read_text(encoding="utf-8"))
            require(actual == expected, "Source manifest mismatch: files, hashes, metadata or layout have changed")
            print(f"PASS {expected['release']} source manifest: {len(expected['files'])} files; tree SHA-256: {expected['tree_sha256']}")
        return 0
    except (ValueError, ET.ParseError, OSError, json.JSONDecodeError) as e:
        print(f"FAIL: {e}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
