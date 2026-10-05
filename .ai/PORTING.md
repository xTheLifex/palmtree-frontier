# PORTING — Coyote/Floof → Palmtree (Frontier)

> This document records what was ported from the old `coyote-frontier` checkout into this repository,
> what was deliberately left out, why, and how to port more. The earlier full-merge attempt and its
> audit folder were deleted by the maintainer; this document is the surviving decision history.

## 1. Background

- Old base: the Coyote checkout (`COYOTE`, see `.ai/file_paths.md`; branch `palm3`), fork chain Wizden → Frontier → Coyote (`_CS`) →
  Palmtree (`_PS`). Frozen since Coyote was abandoned in 2026.
- New base: this repo, Frontier Station @ 2026-10-03 (`df24c19f08`) + Palmtree branch `ps-erp`
  (merged into `master`, branch deleted).
- An earlier attempt merged the **entire** Coyote tree into Frontier (11,722 changed files, 1,347
  conflicts). It was discarded; the current `master` history is the only maintained line.
- The current approach is **targeted**: only the requested features are ported, using additive
  changes to Frontier code where possible, and adapting old content to 2026 APIs.

## 2. Port commits (originally on `ps-erp`)

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
- `ModifyUndies` (`Content.Server/_Floof`, `Content.Shared/_Floof`): per-marking show/hide verbs
  (`canToggleVisible` default off except undergarments, `otherCanToggleVisible` default off).

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
- **Digitigrade leg displacement** (`_CS/LegDisplacement.yml` + `LegDisplacements`), wired into
  `ClientClothingSystem` for jumpsuit/shoes/outerclothing (partial coverage, same maps as Coyote).
- **Subtle chat** (`/subtle`, `/subtlelooc`): range- and wall-limited emotes/LOOC that ghosts cannot
  see (Coyote port; `ChatTransmitRange.NoGhosts`). Also ported: `-`/`=` chat prefixes, selectable
  Subtle/Subtle LOOC channels, `FocusSubtle`/`FocusSubtleLOOCWindow` keybinds and the subtle
  notification sound option.
- **Shower** (`_HL` prefix, from Coyote's HardLight content): `Shower`/`ShowerUnfinished`, the
  `Shower` construction graph/recipe, sprite (`_HL/Structures/Furniture/shower.rsi`) and toggle
  audio (`_HL/Ambience/Shower/*`). Adaptations: the unfinished prototype inlines the `_HL` sink
  base (no sink port needed) and the plumbing construction node was dropped (this fork has no
  plumbing assembly). `Tools/convert_dmm_room.py` maps `/obj/machinery/shower` to `Shower`, so the
  converted hotel rooms include working showers.

### 3.3 Sandstorm interaction panel (2026-10, `_PS`)

- Data-driven `interaction` prototypes, organ-based genital capability detection, session consent,
  lust/moans/climax, a 3-tab XAML window (Interactions / Genital Options / Preferences),
  search/favorites/auto-repeat, Ctrl+Shift-click + verb opening, purple ERP emotes and 47 Sandstorm
  ogg sounds.
- **Genital organs**: player-configured penis/vagina/balls/breasts/butt/belly with type + size and a
  semen-per-climax amount, persisted in `Profile.Genitals` (new `genitals` column + migrations).
  Replaces genital markings; the marking library is reused as the render backend.
- **Cum**: SPLURT cum overlay (converted `cumoverlay.dmi` RSI, washable with water/space cleaner),
  `Semen` reagent, Sandstorm-style drip puddles from internal climaxes, and an `AutoCumExterior`
  preference. No pregnancy, womb, other fluids or refractory period.
- Ported from `Sandstorm-Station-13` and `S.P.L.U.R.T-Station-13` (BYOND), not Coyote; see
  `.ai/systems/interaction-panel.md` and `.ai/systems/genital-organs.md`. Tests:
  `Content.IntegrationTests/Tests/_PS/InteractionPanelTest.cs` and the `ServerDbSqliteTests`
  round-trip.

## 4. What was deliberately NOT ported

| Feature | Reason / status |
|---|---|
| Consent system (Floof UI/DB/toggles) | Maintainer decision: no consent system or DB migrations. `ModifyUndies` uses the per-marking `otherCanToggleVisible` opt-in instead (default off). See `systems/consent-and-erp.md` for how to add consent later. |
| Traits (`BodyType*`, `Horny*`, `Scent*`, 46 ids) | Tied to un-ported HornyQuirks/BodyType/Scent systems. Imports filter unknown traits out safely (`HumanoidCharacterProfile.EnsureValid`). |
| Scent / aphrodisiac visibility | Explicitly excluded. `ScentSystem`, `AphroLacedVisibility`, smell traits absent. |
| Size manipulation / height-width system | Excluded. Character saves' `height`/`width` fields are ignored on import. |
| Vore | Not ported. |
| RPI economy, Needs, Healing bank, Space janitor, etc. (`_CS`) | Not ported; not requested. |
| IPC species | Replaced by the Palmtree **Synth** species (`_PS`) — see `systems/ps-systems.md`. Old IPC content (Einstein Engines silicon stack, battery/radio/EMP) remains unported. |
| Kitsune species | **Does not exist in the old codebase.** It only appears inside marking allowlists. Nothing to port. |
| Turrets, RCD, strobe lighting, shipyard cauterizer | Not requested / possibly stale systems. (Sandstorm interaction sounds/panel were later ported — see `systems/interaction-panel.md`.) |

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
   `MarkingManager.IsAllowedBySpeciesOrKindAllowance`.
6. **Stale YAMLLinter assemblies** — running `Content.YAMLLinter --no-build` after C# changes used
   old `Content.Shared.dll` copies, producing bogus "value not found" enum errors. Always rebuild
   before linting.

## 6. IPC replacement: Synth (implemented)

IPC was held back because the old implementation (`_EinsteinEngines`) needs the EE silicon stack:
`Silicon`, `BatterySlotRequiresLock`, `EncryptionHolderRequiresLock`, `SiliconEmitSoundOnDrained`,
`EmitBuzzWhileDamaged`, `DeadStartupButton`, `EncryptionKeyHolder`, `BatteryDrinker`, the `ipc`
inventory template, and silicon body parts/organs.

Instead, Palmtree implemented a replacement species, **Synth** (`_PS`), which reuses the marking
system so players can look like any species and approximates IPC behavior: 1.5× speed, 3× durability,
IPC-like damage container/modifier set, silicon body parts/organs (IPC sprites, `Inorganic` parts),
coolant blood, robot typing indicator, insulated temperature, heavy fixture density, no
hunger/thirst, and mass-based pull slowdown. See `.ai/systems/ps-systems.md` for details and known
differences (no battery, no radio/encryption, no EMP interactions).

If a true silicon implementation is ever wanted, build it on the upstream/engine silicon & borg
systems already in this repo (`Content.Server/Silicons`, `Content.Shared/Silicons`) instead of
importing the EE stack.

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
- **Unknown species fallback**: `FromStream` maps exports whose species no longer exists to `Synth`
  (if present) or the default species, so old IPC saves import cleanly and can be switched to Synth
  in the editor.

## 8. How to port more (short version)

See `.ai/guides/porting-from-coyote.md` for the full method. In short:

1. Find the source files in the Coyote checkout (`COYOTE`, see `.ai/file_paths.md`) by prototype ID or feature name.
2. Classify each field/component against this repo (`grep` the C# type / prototype type).
3. Copy content files and assets; adapt fields that do not exist here (remove or rewrite).
4. Wire loadouts/groups/locale; keep prefixes for provenance.
5. Rebuild the solution, rebuild `Content.YAMLLinter`, run it; then run relevant tests.
6. Commit with a note about adaptations; update `.ai/PORTING.md` and `.ai/HAZARDS.md` if you learn
   something new.

For **SS13 (BYOND) content** — features from the `BYOND` checkouts (see `.ai/file_paths.md`),
`.dmm` room maps and `.dmi` art — use `.ai/guides/porting-from-ss13.md` instead. It covers the TGM
`.dmm` format, the `Tools/convert_dmm_room.py` generator, format-7 map requirements (air, gravity,
power, anchoring), SS13→SS14 concept mapping and the related hazards.

## 9. 2026-10 feature batch (Coyote ports + Palmtree systems)

Ports from the `COYOTE` checkout (see `.ai/file_paths.md`):

- **Feroxi species** (`_DV/Species`, `_DV/Body/{Parts,Organs,Prototypes}`, `_DV/Entities/Mobs`,
  `_DV/Damage/modifier_sets.yml`, `_DV/Chemistry/metabolizer_types.yml`, `_DV/SoundCollections/feroxi.yml`,
  `_DV/Voice/*`, `_DV/typing_indicator.yml`, guidebook XML). Coyote's unused `FeroxiDehydrate`
  component and the RPI `Needs` system were dropped (not ported here); Hunger/Thirst are used instead.
  The body prototype uses `OrganFeroxiLungs` (Coyote's used human lungs and only had the metabolizer
  type on the unused organ) so Feroxi can breathe **water vapor and oxygen**:
  `Water` reagent `Gas` metabolism got the Feroxi `Oxygenate`/`SatiateThirst`/`ModifyLungGas` effects
  and `Oxygen` got a Feroxi `Oxygenate`.
- **Water vapor tanks** (`_DV/Entities/Objects/Tools/gas_tank.yml`,
  `_DV/Catalog/Fills/Items/gas_tanks.yml`, `_DV/Recipes/Lathes/misc.yml` + `DeltaV/Objects/Tanks/*.rsi`,
  tank dispenser inventory entries). Coyote's water-vapor survival boxes were skipped (they reference
  unported food/medipens).
- **Emotes + emote picker**: see `.ai/systems/ps-systems.md` for the system map. Content files were
  copied from `Floof/`, `_EinsteinEngines/`, `_PS/`, `_DEN/`, `_Funkystation/` and `_CS/` into the
  matching `_Floof`/`_EE`/`_PS`/`_DEN`/`_Funkystation`/`_CS` folders. `Whine` was kept and Coyote's
  separate `Whimper` emote was added next to it. The deathgasp was fixed: `DefaultDeathgasp` has its
  `deathgasp` chat triggers back (organic message + species sound), `SiliconDeathgasp` is named
  "Silicon Deathgasp", categorized `Borg` and whitelisted to `BorgChassis`/`Synth`, and both borgs
  and the Synth species use it as their on-death gasp.
- **Glass gas mask** (SS13 Skyrat/SPLURT `/obj/item/clothing/mask/gas/glass`): prototype
  `_PS/Clothing/Mask/glass_gas_mask.yml` + RSI extracted from the SS13 `.dmi` (`gas_clear`). It has no
  `IdentityBlocker`/`HideLayerClothing`, so the face/snout stays visible, and is a contractor face
  loadout. Species worn variants were extracted from Skyrat's `mask_muzzled.dmi` and `species/vox/mask.dmi`
  (`equipped-MASK-vulpkanin`/`felinid`/`rodentia`/`feroxi`/`harpy`/`reptilian` use the muzzled sprite,
  `equipped-MASK-vox` the vox one); the client picks these up from `InventoryComponent.SpeciesId`, no C#.
- **Custom species name** (profile field, DB column, editor field, examine/records use).
  Coyote's `SpeciesPrototype.CustomName` gated the feature but no species set it, so it never saved;
  here the default is `true` (all round-start species allow it).
- **Emote category picker** (profile field + DB column, `EmoteCategoryWindow`, wheel filtering,
  `EmoteCategory` enum widened to Coyote's `ushort` set, `ShowInWheel`, supplemental emote sounds).
  Server-side emote restrictions were disabled (Coyote behavior); the picker controls visibility.

Palmtree-original changes in the same batch: currency renamed to "space roubles" (ids unchanged),
fuel scaling for longer rounds (~2.7x the original durations), pest/power station events disabled, forensic swab 0.5 s,
SSD bodies don't suffocate, admin playtime-only bypass, `exportcharacters` console command,
Palmtree splash logo, non-`_PS` lobby backgrounds/music disabled.
