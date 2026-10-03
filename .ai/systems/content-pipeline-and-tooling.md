# System: Content Pipeline & Tooling

## Purpose

How prototypes, locale, maps and tests flow from YAML/assets into the running game, and which tools
gate changes.

## Prototypes

- Loaded by the engine `PrototypeManager` from `/EnginePrototypes/` then content `/Prototypes/`.
- Inheritance via `parent`/`abstract`; `Resources/IgnoredPrototypes/ignoredPrototypes.yml` marks
  upstream prototypes abstract so forks can replace them.
- IDs are referenced from C# with `ProtoId<T>`/`EntProtoId`; the YAMLLinter validates references.
- Runtime load errors are per-file logs and skips; `Content.YAMLLinter` is the authoritative gate.
- Duplicate IDs, unknown fields, missing RSIs, and duplicate locale keys are all lint errors.

## Localization

- Fluent `.ftl` under `Resources/Locale/<culture>/`; server and client load all files.
- Duplicate keys across all files in a culture are load errors.
- Marking keys: `marking-<id>` / `marking-<id>-<state>`; loadout keys: `loadout-name-<id>`,
  `loadout-group-*`; UI keys: `markings-category-*`, `humanoid-leg-style-*`.
- Fork locales live under the matching prefix folder (`_PS`, `_Floof`, `_CS`, `_DV`, ...).

## Sprites/audio

- `.rsi` = directory + `meta.json` listing states/license/copyright. Missing states are lint errors
  only when referenced by prototypes with `ValidateStaticFields` coverage; otherwise they fail at
  runtime.
- Audio is referenced by path (`/Audio/...`); missing files generally fail at runtime.

## Maps

- Format 7; grids are entities. `Resources/Maps` holds station/ship/dungeon maps.
- Migrations: `Resources/migration.yml` + `Resources/nf_migration.yml` applied on load.
- Engine `savegrid`/`savemap` are server-console-only and write to `data/`.
- Map merge driver: `Content.Tools/MappingMergeDriver.cs`.

## Tests and CI

| Tool | Command | Purpose |
|---|---|---|
| Unit tests | `dotnet test Content.Tests/Content.Tests.csproj` | chemistry, atmos, localization, preferences, marking serialization |
| Integration tests | `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj` | pooled server+client; `EntityTest`, `CharacterCreationTest`, `_NF/ShipyardTests`, `_PS/*` |
| YAML linter | `dotnet run --project Content.YAMLLinter` | prototype + locale validation (CI gate) |
| Map renderer | `dotnet run --project Content.MapRenderer` | renders maps to images |
| Packaging | `dotnet run --project Content.Packaging server\|client` | distributable zips |

Workflows live under `.github/workflows` (build/test, YAML validation, changelog validation, etc.).

**Lint order after C# changes:** `dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj` first,
then run with `--no-build`, otherwise the linter uses stale content assemblies (HAZARDS §10).

## Adding content checklist

1. Prototypes under the right prefix folder.
2. Sprites/audio copied with `meta.json`.
3. Locale keys (no duplicates).
4. If referenced by loadouts/species/markings: wire groups/layers/points.
5. Build → rebuild linter → lint → relevant tests.
6. Update `.ai/` if architecture shifted.

## Changelogs

Player-visible changes carry a `:cl:` block in the commit message; the commit/PR workflows append
those to `Resources/Changelog/Palmtree.yml`. Full format and fallbacks: `.ai/guides/changelogs.md`.

## Unknowns

- Which CI workflows actually gate merges on this branch.
- Whether missing RSI states are always caught by the linter.
