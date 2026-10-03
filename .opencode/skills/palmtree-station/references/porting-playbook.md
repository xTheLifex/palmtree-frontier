# Porting playbook (Coyote/Floof → Palmtree Frontier)

Source checkout: `/home/thelife/Desktop/Things/Development/SS14/coyote-frontier` (branch `palm3`,
frozen). Target: this repository. The previous full-merge attempt lives on the `port`/`port-wip`
branches — reference only.

## 0. Scope discipline

Port only what was asked. `.ai/PORTING.md` lists what was deliberately excluded (consent, traits,
scent, size, vore, RPI, old IPC silicon stack). Adding an excluded subsystem drags in dependencies
and DB work; confirm first.

## 1. Locate the source

```bash
grep -rn "id: SomeProto" coyote-frontier/Resources/Prototypes --include='*.yml'
grep -rn "class SomeSystem" coyote-frontier/Content.* --include='*.cs'
grep -rn "marking-SomeMarking" coyote-frontier/Resources/Locale/en-US --include='*.ftl'
```

## 2. Classify every field/type

For each YAML field or C# type, check it exists here:

```bash
grep -rn "public .* SomeField" Content.Shared Content.Server Content.Client --include='*.cs'
grep -rn "type: SomeComponent" Resources/Prototypes | head
```

| Outcome | Action |
|---|---|
| Exists with same name | Copy as-is |
| Renamed/refactored by 2026 | Adapt (table below) |
| Coyote-only system (Needs, RPI, scent, size, vore) | Remove component/field |
| Floof marking feature not yet ported | Port the feature additively first (see marking reference) |

Known 2023→2026 adaptations:

| Old | New |
|---|---|
| `SoundOnTrigger` | `EmitSoundOnTrigger` |
| `OnUseTimerTrigger` | `TimerTrigger` |
| `ImplantImplantedEvent.Implanted` (nullable) | non-nullable `EntityUid` |
| `prefix:` on entity | fold into `name:` |
| `inhandVisuals` on ammo provider | `- type: Item` component |
| `BallisticAmmoProvider.count` | `capacity` |
| `noSpawn: true` | `categories: [ HideSpawnMenu ]` |
| Gun spread fields under `soundGunshot` | fields on `- type: Gun` |
| `string.Create(IFormatProvider, ...)` in shared/client | `float.ToString(CultureInfo.InvariantCulture)` |
| Old species size fields (`minHeight`, `defaultHeight`, `maxWidth`, ...) | remove (unsupported) |
| `Needs` component | remove (not ported) |
| `altSprites` / `baseLayerSprite` / `hidden` / `layering` / `colorLinks` | supported (ported) |

## 3. Copy content and assets

- Prototypes → same relative path under `Resources/Prototypes/<prefix>/...`.
- Locale → same relative path under `Resources/Locale/en-US/<prefix>/...`. **Check for duplicate
  keys first** — duplicates anywhere in a culture are load errors.
- Sprites/audio: use `scripts/copy_referenced_assets.py` to copy every `.rsi`/audio path referenced
  by the copied prototypes from the old checkout. Copy whole `.rsi` directories including
  `meta.json`; when merging into an existing RSI, merge states instead of overwriting.
- Prefer new small files for speech sounds/typing indicators/emotes to avoid clobbering shared files.

## 4. Species ports

Species files need extra stripping: unsupported top-level fields, `Needs`, and any layer they
reference must exist. See `.ai/guides/adding-species.md` and `species-and-loadouts.md` here.

## 5. Character-save compatibility

The maintainer's exports live in `~/Documents/SS14/Characters` (do not modify). Use
`scripts/check_character_refs.py` to list every referenced marking/loadout/species id and what is
missing. Unknown prototypes are silently dropped on import (`EnsureValid`), so missing ids mean data
loss, not import failure. Unknown fields (old `glowLevels`, `height`, `width`, `takeOffVerb2p`, ...)
are ignored by the serializer. Missing species fall back to Synth (see marking reference).

## 6. Validation

Run the full pipeline from `validation-and-hazards.md`. At minimum:
solution build → rebuild `Content.YAMLLinter` → lint → unit tests → `EntityTest` + targeted tests →
headless client typecheck.

## 7. Commit and document

- One commit per coherent feature; list adaptations in the message.
- Never `git add -A` while `PORTING/` exists; add specific paths.
- Update `.ai/PORTING.md` (ported/excluded), `.ai/HAZARDS.md` (new pitfalls) and the relevant
  `.ai/systems/*` or `.ai/guides/*` document in the same change.
- Do not commit unless the user asks (the maintainer tests in-game first).
