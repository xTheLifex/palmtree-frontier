# Guide: Porting Content from Coyote / Other Forks

This is the method used to port Palmtree/Coyote/Floof content into this Frontier-based repo. It is
written for an AI agent working with the old `coyote-frontier` checkout available on disk (or any
other fork checkout).

## 0. Before you start

- Read `.ai/PORTING.md` (what is already ported / deliberately excluded).
- Read `.ai/HAZARDS.md` (sandbox, filters, lint staleness).
- Decide where the code belongs:
  - Palmtree-original → `_PS`.
  - Ported fork code → keep its original prefix (`_Floof`, `_CS`, `_DEN`, `_EE`, `_White`,
    `_Starlight`, ...) for provenance.
  - Core-file patches (upstream files) → keep them minimal, annotated (`// Palmtree`/`// Coyote`).

## 1. Locate the source

Search the old repo by prototype id or symbol:

```
grep -rn "id: SomeProto" coyote-frontier/Resources/Prototypes --include='*.yml'
grep -rn "class SomeSystem" coyote-frontier/Content.* --include='*.cs'
grep -rn "marking-SomeMarking" coyote-frontier/Resources/Locale/en-US --include='*.ftl'
```

Prefer porting whole self-contained files (marking packs, item sets) over cherry-picking single
prototypes, unless a file pulls in unsupported systems.

## 2. Classify every field/type against this repo

For each YAML field or C# type used by the source, check it exists here:

```
grep -rn "public .* SomeField" Content.Shared Content.Server Content.Client --include='*.cs'
grep -rn "type: SomeComponent" Resources/Prototypes | head
```

Typical outcomes:

| Outcome | Action |
|---|---|
| Field/type exists with the same name | Copy as-is |
| Field/type renamed by 2026 refactors | Adapt (see table below) |
| Field is a Coyote-only system (Needs, RPI, scent, size, vore) | Remove the component/field or replace with an equivalent here |
| Field is a Floof marking feature not yet ported | Prefer porting the feature additively (see `.ai/guides/adding-markings.md`) |

Known renames/removals encountered:

| Old | New here |
|---|---|
| `SoundOnTrigger` | `EmitSoundOnTrigger` |
| `OnUseTimerTrigger` | `TimerTrigger` |
| `ImplantImplantedEvent.Implanted` nullable | non-nullable `EntityUid` |
| `prefix:` on entities | fold into `name:` |
| `inhandVisuals` on ammo providers | `- type: Item` component |
| `BallisticAmmoProvider.count` | `capacity` |
| `noSpawn: true` | `categories: [ HideSpawnMenu ]` |
| Gun spread fields nested under `soundGunshot` | fields on `- type: Gun` |
| `string.Create(IFormatProvider, ...)` in shared/client | `float.ToString(CultureInfo.InvariantCulture)` (sandbox) |

## 3. Copy content and assets

- Prototypes → same relative path under `Resources/Prototypes/<prefix>/...`.
- Locale → same relative path under `Resources/Locale/en-US/<prefix>/...`; check for duplicate keys
  before copying (a duplicate key fails prototype load).
- Sprites → copy the whole `.rsi` directory (`meta.json` included). A quick script that extracts
  every `sprite:` path from the copied YAML and copies missing directories from
  `coyote-frontier/Resources/Textures` works well.
- Audio → copy referenced `/Audio/...` files.
- Names/typing indicators/speech verbs/sound collections → copy or merge as separate small files to
  avoid conflicts in shared files.

## 4. Adapt species/content

Species ports need extra work: unsupported top-level species fields (`minHeight`, `defaultHeight`,
`maxHeight`, `minWidth`, `defaultWidth`, `maxWidth`, `averageHeight`, `averageWidth`, `guideBookIcon`)
must be removed, `Needs` components dropped, and layers added. See `.ai/guides/adding-species.md`.

## 5. Verify (in this order)

```
dotnet build SpaceStation14.sln
dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj   # IMPORTANT after C# changes
dotnet run --project Content.YAMLLinter -c Debug --no-build
dotnet test Content.Tests/Content.Tests.csproj
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~EntityTest"
```

Then, for client code, verify the sandbox:

```
dotnet run --project Content.Client -c Debug --no-build -- --headless
# expect: Content.Shared/Content.Client "Verified IL", then an expected OpenGL error
```

## 6. Commit and document

- One commit per coherent feature; message lists adaptations.
- Update `.ai/PORTING.md` (what was ported/excluded) and `.ai/HAZARDS.md` if you learned a new
  pitfall.
- Never commit the untracked `PORTING/` audit folder by accident (`git add` specific paths).

## 7. Character-save compatibility

When porting for the maintainer's characters, compare their exports against the repo:

```
python3 - <<'EOF'
# parse ~/Documents/SS14/Characters/**.yml, collect markingId / loadout prototype / species
EOF
```

Then `grep` each id in `Resources/Prototypes`. Unknown prototypes are dropped on import
(`EnsureValid`), so missing ids mean silent data loss rather than import failure.
