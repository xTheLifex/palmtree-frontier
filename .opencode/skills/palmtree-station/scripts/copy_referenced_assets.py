#!/usr/bin/env python3
"""Copy assets referenced by ported prototypes from an old checkout.

Usage:
    python3 copy_referenced_assets.py --source /path/to/coyote-frontier \
        Resources/Prototypes/_Floof/Entities/Mobs/Customization/markings

Scans the given prototype files/directories for `sprite:` paths and `/Audio/...` paths and copies
missing ones from `<source>/Resources/Textures` / `<source>/Resources/Audio` into this repository.
Directories (.rsi) are copied whole. Use --dry-run to only report.
"""

from __future__ import annotations

import argparse
import os
import re
import shutil
import sys

SPRITE_RE = re.compile(r"sprite:\s*(\S+)")
AUDIO_RE = re.compile(r"(/Audio/[A-Za-z0-9_\-/\.]+\.ogg)")

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))


def iter_yaml(paths: list[str]):
    for path in paths:
        if os.path.isdir(path):
            for root, _dirs, files in os.walk(path):
                for name in files:
                    if name.endswith((".yml", ".yaml")):
                        yield os.path.join(root, name)
        elif path.endswith((".yml", ".yaml")):
            yield path


def copy_item(source: str, target: str, dry_run: bool) -> str:
    if os.path.exists(target):
        return "ok"
    if not os.path.exists(source):
        return "missing"
    if dry_run:
        return "copy"
    os.makedirs(os.path.dirname(target), exist_ok=True)
    if os.path.isdir(source):
        shutil.copytree(source, target, dirs_exist_ok=True)
    else:
        shutil.copy2(source, target)
    return "copy"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="+", help="Prototype files or directories to scan.")
    parser.add_argument("--source", required=True, help="Old checkout root (contains Resources/).")
    parser.add_argument("--repo", default=REPO, help="Target repository root.")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    sprites: set[str] = set()
    audio: set[str] = set()
    for path in iter_yaml(args.paths):
        with open(path, encoding="utf-8", errors="replace") as fh:
            text = fh.read()
        sprites.update(SPRITE_RE.findall(text))
        audio.update(AUDIO_RE.findall(text))

    copied = missing = 0
    for ref in sorted(sprites):
        target = os.path.join(args.repo, "Resources", "Textures", ref)
        source = os.path.join(args.source, "Resources", "Textures", ref)
        result = copy_item(source, target, args.dry_run)
        if result == "copy":
            copied += 1
            print(f"{'would copy' if args.dry_run else 'copied'}  {ref}")
        elif result == "missing":
            missing += 1
            print(f"MISSING   {ref}")

    for ref in sorted(audio):
        rel = ref.lstrip("/")
        target = os.path.join(args.repo, "Resources", rel)
        source = os.path.join(args.source, "Resources", rel)
        result = copy_item(source, target, args.dry_run)
        if result == "copy":
            copied += 1
            print(f"{'would copy' if args.dry_run else 'copied'}  {ref}")
        elif result == "missing":
            missing += 1
            print(f"MISSING   {ref}")

    print(f"done: {copied} copied, {missing} missing", file=sys.stderr)
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
