#!/usr/bin/env python3
"""Check exported SS14 characters against this repo's prototypes.

Usage:
    python3 check_character_refs.py [--characters DIR] [--repo DIR]

Collects every marking id, loadout/item prototype id and species from exported character YAML files
and reports which ones do not exist in the repository's Resources/Prototypes. Unknown prototypes are
silently dropped on import, so anything reported here is data loss.
"""

from __future__ import annotations

import argparse
import glob
import os
import re
import sys

try:
    import yaml
except ImportError:  # pragma: no cover
    print("PyYAML is required (pip install pyyaml)", file=sys.stderr)
    sys.exit(2)

ID_PATTERN = re.compile(r"^\s*-?\s*id:\s*([A-Za-z0-9_\-.]+)\s*$")


def walk_collect(obj, key: str, out: set[str]) -> None:
    if isinstance(obj, dict):
        for k, v in obj.items():
            if k == key and isinstance(v, str):
                out.add(v)
            else:
                walk_collect(v, key, out)
    elif isinstance(obj, list):
        for item in obj:
            walk_collect(item, key, out)


def collect_repo_ids(repo: str) -> set[str]:
    ids: set[str] = set()
    prototypes = os.path.join(repo, "Resources", "Prototypes")
    for root, _dirs, files in os.walk(prototypes):
        for name in files:
            if not name.endswith((".yml", ".yaml")):
                continue
            with open(os.path.join(root, name), encoding="utf-8", errors="replace") as fh:
                for line in fh:
                    m = ID_PATTERN.match(line)
                    if m:
                        ids.add(m.group(1))
    return ids


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--characters",
        default=os.path.expanduser("~/Documents/SS14/Characters"),
        help="Directory containing exported character .yml files (searched recursively).",
    )
    parser.add_argument(
        "--repo",
        default=os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..")),
        help="Repository root (defaults to four levels above this script).",
    )
    args = parser.parse_args()

    files = sorted(glob.glob(os.path.join(args.characters, "**", "*.yml"), recursive=True))
    if not files:
        print(f"No character files found under {args.characters}", file=sys.stderr)
        return 2

    markings: set[str] = set()
    prototypes: set[str] = set()
    traits: set[str] = set()
    species: dict[str, str] = {}

    for path in files:
        try:
            with open(path, encoding="utf-8") as fh:
                data = yaml.safe_load(fh)
        except Exception as exc:  # noqa: BLE001
            print(f"!! failed to parse {path}: {exc}", file=sys.stderr)
            continue

        if not isinstance(data, dict):
            continue
        profile = data.get("profile")
        if not isinstance(profile, dict):
            continue

        name = os.path.basename(path).removesuffix(".yml")
        species[name] = str(profile.get("species", "?"))

        walk_collect(profile, "markingId", markings)
        walk_collect(profile, "prototype", prototypes)
        for trait in profile.get("_traitPreferences") or []:
            traits.add(str(trait))

    repo_ids = collect_repo_ids(args.repo)

    def report(label: str, values: set[str]) -> None:
        missing = sorted(v for v in values if v not in repo_ids)
        print(f"{label}: {len(values) - len(missing)} present, {len(missing)} missing")
        for item in missing:
            print(f"  {item}")

    report("markings", markings)
    report("loadouts/items", prototypes)
    report("traits", traits)

    print("species:")
    for char, sp in sorted(species.items()):
        print(f"  {char}: {sp} {'OK' if sp in repo_ids else 'MISSING'}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
