# System: Dungeons and Salvage Generation

> Frontier Station's procedural dungeon stack (`DungeonSystem`), its prototype data, and how generated
> content reaches players through expeditions, salvage wrecks, magnet debris and bluespace events.
> Verify specifics in `Content.Server/Procedural` and `Content.Server/Salvage` — this doc is the map.

## Purpose

Turns prototype-driven map data into playable dungeon layouts at runtime: corridors and rooms are
generated from **dungeon configs** (`dungeonConfig` + layer types), rooms are copied out of **authored
atlas maps** (`dungeonRoom`), or whole **salvage wreck maps** are spawned (`salvageMap`). The
generator then populates the result with loot budgets, mobs/factions, ores, biomes and boss spawns.
Frontier wraps all of this in **expeditions** (player missions) and salvage jobs; Palmtree only adds
loot tables on top.

## Locations

| Piece | Path |
|---|---|
| Generator (`DungeonSystem` + partials) | `Content.Server/Procedural/DungeonSystem*.cs` |
| Generation job (time-sliced) | `Content.Server/Procedural/DungeonJob/` |
| `RoomFillComponent` / room stamping | `Content.Server/Procedural/RoomFillComponent.cs`, `DungeonSystem.Rooms.cs` |
| Layer types (`PrefabDunGen`, `OreDunGen`, ...) | `Content.Shared/Procedural/{DungeonGenerators,DungeonLayers,PostGeneration}/` |
| Salvage/expedition jobs | `Content.Server/Salvage/SalvageSystem*.cs`, `SpawnSalvageMissionJob.cs` |
| Frontier expedition logic | `Content.Server/_NF/Salvage/Expeditions/`, `_NF/Salvage` |
| Dungeon configs | `Resources/Prototypes/Procedural/dungeon_configs.yml`, `Resources/Prototypes/_NF/Procedural/dungeon_configs.yml` |
| Themes (room definitions) | `Resources/Prototypes/Procedural/Themes/*.yml`, `Resources/Prototypes/_NF/Procedural/Themes/*.yml` |
| Presets / room packs | `Resources/Prototypes/Procedural/dungeon_presets.yml`, `dungeon_room_packs.yml` |
| Authored atlas maps | `Resources/Maps/Dungeon/*.yml`, `Resources/Maps/_NF/Dungeon/*.yml` (templates in `Resources/Maps/Dungeon/Templates/`) |
| Standalone wrecks | `Resources/Maps/Salvage/*.yml` + `Resources/Prototypes/Maps/salvage.yml` |
| Factions / difficulty / loot budgets | `Resources/Prototypes/Procedural/salvage_{factions,difficulties,loot,rewards,mods}.yml`, same under `_NF/Procedural/` |
| Biome / ore / magnet templates | `Resources/Prototypes/Procedural/biome_*.yml`, `biome_ore_*.yml`, `Magnet/*.yml` |
| Loot/mob spawner entities | `Resources/Prototypes/Entities/Markers/Spawners/Random/Salvage/`, `Resources/Prototypes/_NF/Entities/Markers/Spawners/` |
| Boss loot | `Resources/Prototypes/_NF/Catalog/Fills/NPCsLoot/` |
| Bluespace dungeon events | `Resources/Prototypes/_NF/Events/nf_bluespace_dungeons_events.yml` |

## Concept model

| Prototype kind | Role |
|---|---|
| `dungeonConfig` | The recipe: an ordered list of generation **layers** producing one dungeon. |
| `dungeonRoom` | A rectangle cut from an authored atlas map (`atlas`, `offset`, `size`) with `tags`. |
| `dungeonRoomPack` / `dungeonPreset` | Named room-rectangle sets and the preset layouts that place them (Bucket, Wow, SpaceShip, Tall, ...). |
| `RoomFill` (component on `BaseRoomMarker` children) | A marker entity placed in generated geometry that stamps a matching `dungeonRoom` in place (rotation/size/tag whitelist). |
| `salvageMap` | A whole authored map file spawned as a wreck (`mapPath`, `sizeString`). |
| `salvageFaction` | Mob mix + `DefenseStructure` + optional `Megafauna` (boss) + difficulty configs. |
| `salvageDifficulty` | Loot/mob/modifier budgets and player recommendation for a job. |
| `salvageLoot` | Budgeted loot table consumed by the mission spawner. |
| `salvageDungeonMod` / `salvageBiomeMod` / `salvageLightMod` / `salvageWeatherMod` / `salvageAirMod` / `salvageTemperatureMod` | Modifiers that pick which dungeon/config, biome, lighting, weather and atmosphere a generated site gets. |
| `biomeTemplate` / `biomeMarkerLayer` | Biome composition and per-biome mob/ore layers. |
| `entityTable` (`!type:GroupSelector`/`NestedSelector`/`AllSelector`) | Weighted/rolled lists used everywhere for loot, mobs and structures. |

## How generation works

1. **Job creation.** A salvage/expedition job is created (`SpawnSalvageMissionJob`, Frontier
   `_NF/Salvage/Expeditions`): a difficulty, faction, biome/light/weather/air modifiers and a dungeon
   config are selected. Bluespace events (`_NF/Events/nf_bluespace_dungeons_events.yml`) set up
   whole-grid spawns (vgroid/scrap/zombie/chromite/snow/cave) via `BluespaceErrorRule` +
   `DungeonSpawnGroup` (distance from station, spawn bias, prototype).
2. **Layout generation.** `DungeonSystem` runs the config's layers in order. Room layers
   (`PrefabDunGen`) pick `dungeonRoom`s whose `tags` match the layer's `roomWhitelist`; presets/room
   packs decide how the rectangles fit. Corridor layers (`CorridorDunGen`, `WormCorridorDunGen`)
   carve the connections; entrance/window/junction layers stamp doors, airlocks and windows; wall and
   cabling layers finish the shell. `DungeonEntranceDunGen`/`RoomEntranceDunGen` produce special
   `*Dungeon` airlocks/windows that block lasers.
3. **Room copying.** A room is copied tile-by-tile plus every entity inside the atlas rectangle
   (`DungeonRoomPrototype.Atlas` + `Offset` + `Size`). **Only prototype IDs survive the copy** —
   per-entity DataField overrides on atlas entities are lost (see Hazards).
4. **Room fill.** `RoomFill` markers inserted/placed by the layout pick a matching `dungeonRoom`
   (`roomWhitelist` tags, `MinSize`/`MaxSize`, optional rotation) and stamp it into the spot. Used
   heavily by vgroids/wrecks (`NFVGRoidInteriorRoomMarker`, `NFWreckRoomMarker`, ...).
5. **Population.** Later layers add ores (`OreDunGen`), mobs (`MobsDunGen`), entity tables
   (`EntityTableDunGen`), biomes (`BiomeDunGen`) and decoration/clutter. Mob tables can include
   bosses (`SpawnMobExplorerBossTable`, `SpawnMobRogueSiliconBoss`, ...).
6. **Loot.** Budgeted loot (`salvageLoot` → `salvageDifficulty`) and the legendary equipment table
   (`SalvageEquipmentLegendary` in `Resources/Prototypes/Entities/Markers/Spawners/Random/Salvage/tables_loot.yml`)
   place weapon cases, treasure and materials. Palmtree's cases (`TableDungeonLootWeaponsFallout`,
   `TableDungeonLootWeaponsPalmtree`) are nested there with small weights.
7. **Cleanup.** Generated grids/sites are torn down by the salvage/expedition systems when the job
   ends or the site empties; wrecks/POIs use the same runtime-map rules as Hilbert's Hotel
   (`.ai/systems/hilbert-hotel.md`).

## Encounter paths

- **Expeditions (jobs):** the main player route. A field/expedition console sends a team to a
  generated site; elimination missions track a `salvageFaction` `Megafauna` boss to kill
  (`SalvageEliminationExpeditionComponent`). Difficulties are fixed to
  `NFModerate`/`NFHazardous`/`NFExtreme` in `SalvageSystem.Expeditions.cs`.
- **Salvage wrecks:** `salvageMap` entries spawned by salvage missions/debris and the magnet.
- **Bluespace events:** whole grids spawned near the station by `BluespaceErrorRule` +
  `DungeonSpawnGroup`.
- **POIs/ruins:** authored maps (`_NF/POI`, `Resources/Maps/Ruins`) — not procedural.
- **Not active in this fork:** the magnet's asteroid offerings (configs commented out; the asteroid
  list is hardcoded in `SharedSalvageSystem.Magnet.cs`) and round-start `DungeonSpawnGroup`s on
  Frontier stations (`BaseStationShuttles` is commented out of the station prototypes).

## Bosses

There is **no dedicated boss-arena prototype type**. Bosses are:
- spawned by `MobsDunGen` tables inside generated rooms (explorer/rogue silicon/zombie/blood-cult
  bosses, xeno queen, dragons, syndicate commanders), with `NFMobBossRestrictions`
  (despawn/pacify when leaving the grid) and ghost roles (`ghost-role-information-dungeon-boss-rules`);
- or faction **Megafauna** for elimination expeditions (`salvageFaction.configs.Megafauna`).
- Boss loot ships as NPC-carried loot: `TableDungeonLootNpcBossLootShared` +
  `SpawnDungeonLootNpcBossLootShared`, wired through the boss loadouts (`MobHumanoidExplorerGearBoss`
  etc.) with `orGroup: BossLoot`.

To build a fixed rare arena, add a uniquely tagged large `dungeonRoom` slice and reference that tag
from a `PrefabDunGen` whitelist in a dedicated config (see `.ai/guides/adding-dungeons.md` §7).

## Hazards

- **Dungeon rooms are copied by prototype ID only** — atlas entity DataFields are lost
  (`.ai/HAZARDS.md`).
- **Atlas templates are cached for the whole round** (`DungeonAtlasTemplateComponent`, one loaded map
  per atlas path): editing an atlas map needs a server restart or prototype reload before the change
  shows up.
- **Salvage-magnet asteroid configs are commented out** while code still references them; only
  debris and wreck offers work.
- **YAMLLinter does not validate map files** — missing/abstract prototypes only fail at load
  ("Missing prototype for map: X"); map-loading integration tests are the gate.
- **`RandomHumanoidSpawner` markers self-delete on MapInit** — one-shot; do not reuse them as
  placeable content.
- **Engine `savegrid`/`savemap` are server-console-only** and write to `data/`, not `Resources/`;
  map loading prefers user-data copies, so delete stale files after copying.
- **Format 6 atlas maps still load** (most `Resources/Maps/Dungeon/*` are format 6); new maps should
  be format 7. Do not hand-edit tilemap chunks — use the mapping editor.
- **`GravityComponent`/power/anchoring defaults** for runtime maps apply to generated sites too; see
  `.ai/HAZARDS.md` §18.

## Tests

`Content.IntegrationTests/Tests/Procedural/DungeonTests.cs` validates the layout data:
`TestDungeonRoomPackBounds` (room rectangles stay in bounds, do not overlap, and match a real
`dungeonRoom` size) and `TestDungeonPresets`. Map files themselves are covered by the generic
map-loading tests — YAMLLinter does not validate maps.

Debug helpers: `dungen <mapId> <config> <x> <y> [seed]` generates a config on the map in game, and
`dungen_preset_vis` / `dungen_pack_vis` draw preset/pack layouts (`DungeonSystem.Commands.cs`).

## See also

- `.ai/guides/adding-dungeons.md` — authoring walkthrough.
- `.ai/systems/frontier-nf-systems.md` — salvage/expedition context.
- `.ai/systems/hilbert-hotel.md` — runtime map loading precedent.
- `.ai/systems/content-pipeline-and-tooling.md` — map format 7, `Resources/Maps` layout.
- `.ai/HAZARDS.md` §18 — runtime-loaded map traps.
