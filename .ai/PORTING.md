# PORTING — Coyote/Floof → Palmtree (Frontier)

> This document records what was ported from the old `coyote-frontier` checkout into this repository,
> what was deliberately left out, why, and how to port more. It complements the untracked `PORTING/`
> folder, which contains the audit logs of an earlier **full merge attempt** that was abandoned in
> favor of a targeted port.

## 1. Background

- Old base: `coyote-frontier` (branch `palm3`), fork chain Wizden → Frontier → Coyote (`_CS`) →
  Palmtree (`_PS`). Frozen since Coyote was abandoned in 2026.
- New base: this repo, Frontier Station @ 2026-10-03 (`df24c19f08`) + Palmtree branch `ps-erp`.
- An earlier attempt merged the **entire** Coyote tree into Frontier (11,722 changed files, 1,347
  conflicts). It is preserved on the `port`/`port-wip` branches and the untracked `PORTING/` folder
  (`summary.md`, `conflicts.md`, `pending-patches/`, etc.). It is reference material only.
- The current approach is **targeted**: only the requested features are ported, using additive
  changes to Frontier code where possible, and adapting old content to 2026 APIs.

## 2. Port commits on `ps-erp`

| Commit | Contents |
|---|---|
| `805dfc6b5f` | ERP marking system core (Genital category/layer, kindAllowance, layering/colorLinks, scale/offset, ModifyUndies, genital content, `_PS` lobby/backpack/weapons, Rodentia + Anthromorph) |
| `8eacfa17e4` | Fix client sandbox violation (`string.Create` in `Marking.ToString`); strongly type `Layering` |
| `0acc512d11` | Fix concealable backpack implant (component on base `ClothingBackpack`, loadout group entry, tests) |
| `d4e63c28e7` | Rotate lobby background every 30s (Coyote timer patch) |
| `4f8dd74e02` | Crossfade lobby backgrounds (second `TextureRect` + smoothstep fade) |
| `76e0e55d1c` | Digitigrade/base-marking infrastructure, missing markings, Tajaran, Bayou clothing, missing loadouts |
| `afdd481c16` | Fix kind-allowed markings being stripped by `EnsureSpecies` |

## 3. What was ported

### 3.1 Marking infrastructure (`Content.Shared/Humanoid`, `Content.Client/Humanoid`)

- `Genital` + `TailBehind` + all Floof layers (`TailOversuit`, `NeckFluff`, behind-leg layers,
  `UndershirtUnderclothes`/`UndershirtOverclothes`, `TailExtras`, `Disregard`).
- `MarkingPrototype`: `kindAllowance`, `layering`, `colorLinks`, `altSprites`, `hidden`, `baseLayerSprite`.
- `Marking`: `scale`, `offsetX/Y` with backward-compatible DB string format.
- `MarkingCategories`: `Genital`, `NeckFluff`, `TailExtras`, and the Floof `Base*` categories plus
  `Category2Layer`.
- `SpeciesPrototype`: `kind`, `DefaultLegStyle`, `AllowDigilegDisplacement`.
- `HumanoidAppearanceComponent`: `HiddenBaseLayers`, `LegStyle`.
- `HumanoidCharacterAppearance`: `LegStyle` field and `WithLegs`.
- `MarkingPicker`: advanced size/offset editor and Leg Style selector.
- Client renderer: layering routing, color links, scale/offset, genital-under-clothing ordering,
  `altSprites` swapping for base layers and markings, `HiddenBaseLayers` hiding.
- `ModifyUndies` (`Content.Server/_Floof`, `Content.Shared/_Floof`): self-only show/hide verbs.

### 3.2 Content

- **Genital marking sets**: core Floof library (379), butts/bellies (19), Palmtree breasts (18) with
  sprites (`Resources/Textures/Mobs/Customization/Markings/Genital/*`, `_PS/.../Genital/breasts.rsi`)
  and locale (`_CS/genitals.ftl`, `_PS/genitals.ftl`).
- **Floof anthro markings**: ears, tails, snouts, wings, digilegs/chests, species adaptors
  (`_Floof/Entities/Mobs/Customization/markings/*` + `Floof/Mobs/Customization/*` + `_CS`/`_DV` parts).
- **Core undergarments** (218 markings, source `undergarments.yml` is a superset of Frontier's 17) and
  **underovergarments** (67).
- **Feroxi, Ovinia, Resomi, Eastern Dragon** markings with sprites and locale.
- **Tajaran** species (species/entity/player/body/markings/damage/names/sprites/locale) from `_EE`.
- **Rodentia** (`_DV`) and **Anthromorph** (`_CS`) species, adapted to Frontier 2026.
- **Coyote Bayou clothes** (`_CS/CoyoteBayouClothes/*`), including latex items
  (`CoolCoyoteClothesDomina`, `CoolCoyoteClothesLatexHalf`), wired into job loadout groups via a
  `CoyoteJumpsuit` subgroup.
- **Missing contractor loadouts/items**: `ContractorClicker` (+ `Clicker` item/audio),
  `ContractorClothingBackpackSatchelLeather`, `LoadoutFootProtectors` (+ `ClothingShoesFootProtectors`).
- **Palmtree `_PS`**: lobby backgrounds + `PSLobbyMusic` (CVar default), concealable backpack implant
  (systems/components/action/implanter/loadout), battery guns/launchers/grenades/hitscan, audio.

## 4. What was deliberately NOT ported

| Feature | Reason / status |
|---|---|
| Consent system (Floof UI/DB/toggles) | Maintainer decision: no consent system or DB migrations. `ModifyUndies` is self-only. See `systems/consent-and-erp.md` for how to add it later. |
| Traits (`BodyType*`, `Horny*`, `Scent*`, 46 ids) | Tied to un-ported HornyQuirks/BodyType/Scent systems. Imports filter unknown traits out safely (`HumanoidCharacterProfile.EnsureValid`). |
| Scent / aphrodisiac visibility | Explicitly excluded. `ScentSystem`, `AphroLacedVisibility`, smell traits absent. |
| Size manipulation / height-width system | Excluded. Character saves' `height`/`width` fields are ignored on import. |
| Vore | Not ported. |
| RPI economy, Needs, Healing bank, Space janitor, etc. (`_CS`) | Not ported; not requested. |
| IPC species | Deferred on purpose — it depends on the Einstein Engines silicon stack (`Silicon`, battery slot locks, encryption holders, `SiliconEmitSoundOnDrained`, IPC inventory template) absent from this base. See §6. |
| Kitsune species | **Does not exist in the old codebase.** It only appears inside marking allowlists. Nothing to port. |
| Turrets, RCD, strobe lighting, shipyard cauterizer, `_PS` emotes/interaction sounds | Not requested / possibly stale systems. |
| Digitigrade leg displacement maps | The old `LegDisplacements` field is documented as "currently unused because it crashes" in the source; not ported. `altSprites` cover the visuals. |

## 5. Bugs found and fixed during the port

These are important because they are easy to reintroduce:

1. **Sandbox violation** — `Marking.ToString()` used
   `string.Create(IFormatProvider, ref DefaultInterpolatedStringHandler)`, which is not on the
   engine's client sandbox whitelist. Replaced with `float.ToString(CultureInfo.InvariantCulture)`
   concatenation. `Layering` was also changed from `Dictionary<string,string>` to
   `Dictionary<string,HumanoidVisualLayers>` to avoid runtime `Enum.Parse`.
2. **Concealable backpack did nothing** — the old repo patched base `ClothingBackpack` with a
   `ConcealableClothing` component (`toggleAction: ActionToggleConcealmentBackpack`,
   `category: backpack`). Without it no clothing could be concealed. Also added the implanter to the
   `ContractorImplanter` group.
3. **Lobby never rotated** — Coyote patched `GameTicker.LobbyBackground.cs` with a 30-second
   `Timer.SpawnRepeating(30000, CycleLobbyBackground, ...)` + `SendStatusToAll()`. Frontier only
   randomized once at startup.
4. **`MarkingsSet.EnsureDefault` overrun** — the loop condition used `||`, so a category with more
   points than `DefaultMarkings` (e.g. Rodentia's 999-point tail) indexed past the list and crashed
   entity spawning. Fixed to `&&`.
5. **Kind-shared markings stripped** — `MarkingsSet.EnsureSpecies` filtered with
   `SpeciesRestrictions.Contains(species)` only, removing markings allowed through `kindAllowance`
   (e.g. `FeroxiTorsoCountershadingF` on a Vulpkanin). Fixed to use
   `MarkingManager.IsAllowedBySpeciesOrKindAllowance`. Regression test:
   `Content.IntegrationTests/Tests/_PS/MarkingKindAllowanceTest.cs`.
6. **Stale YAMLLinter assemblies** — running `Content.YAMLLinter --no-build` after C# changes used
   old `Content.Shared.dll` copies, producing bogus "value not found" enum errors. Always rebuild
   before linting.

## 6. Porting IPC later (maintainer intent)

IPC was held back deliberately. The old implementation (`_EinsteinEngines`) needs:

- `Silicon` component/`SiliconSystem` (EE), `BatterySlotRequiresLock`, `EncryptionHolderRequiresLock`,
  `SiliconEmitSoundOnDrained`, `EmitBuzzWhileDamaged`, `DeadStartupButton`, `EncryptionKeyHolder`,
  `BatteryDrinker`, plus the `ipc` inventory template.
- Silicon body parts/organs/prototypes and robot speech/typing indicators.
- Coyote additions it referenced (`Needs`, `RoleplayIncentive`, `ShortWhitelist`, `TallWhitelist`,
  `Vore`) which are not ported.

A cleaner path is to build IPC on the upstream/engine silicon & borg systems already in this repo
(`Content.Server/Silicons`, `Content.Shared/Silicons`) instead of importing the EE stack. This has
not been attempted yet.

## 7. Character saves compatibility

The maintainer's exported characters live outside the repo
(`~/Documents/SS14/Characters`). Facts established while porting:

- Import path: client `HumanoidAppearanceSystem.FromStream` → `HumanoidProfileExport` →
  `HumanoidCharacterProfile.EnsureValid`.
- Unknown fields in the YAML (e.g. `height`, `width`, old Floof marking fields like `glowLevels`,
  `takeOffVerb2p`, `customName`) are ignored by the serializer.
- Unknown prototypes (traits, markings, loadouts) are filtered by `EnsureValid`/`EnsureSpecies`
  rather than failing the import.
- After the port, all 114 markings, 166 loadouts/items and all species referenced by those saves
  resolve except IPC.
- `legStyle: Digitigrade` is now imported and honored.

## 8. How to port more (short version)

See `.ai/guides/porting-from-coyote.md` for the full method. In short:

1. Find the source files in `coyote-frontier` by prototype ID or feature name.
2. Classify each field/component against this repo (`grep` the C# type / prototype type).
3. Copy content files and assets; adapt fields that do not exist here (remove or rewrite).
4. Wire loadouts/groups/locale; keep prefixes for provenance.
5. Rebuild the solution, rebuild `Content.YAMLLinter`, run it; then run relevant tests.
6. Commit with a note about adaptations; update `.ai/PORTING.md` and `.ai/HAZARDS.md` if you learn
   something new.
