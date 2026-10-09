# Guide: Adding Dungeons

> Companion to `.ai/systems/dungeons.md` (how the system works). This guide covers authoring: maps,
> rooms, configs, loot and rare boss arenas. Frontier's generator lives in
> `Content.Server/Procedural`; Palmtree content should go under `Resources/**/_PS/`.

The result is one or more new rooms/structures that the procedural generator can place into
expedition sites and salvage wrecks, wired into loot and (optionally) a boss encounter.

## TL;DR checklist

1. Pick the delivery model: **atlas room** (generated dungeons), **standalone wreck**
   (`salvageMap`) or an entire **site** (`dungeonConfig` used by a salvage job/event).
2. Author the map in the mapping editor; save it (format 7) under
   `Resources/Maps/_PS/Dungeon/<theme>.yml`.
3. Declare a room **tag** (`- type: tag`).
4. Add one `dungeonRoom` **per room rectangle** (`atlas` + `offset` + `size` + `tags`).
5. Reference the tag from a `dungeonConfig` `!type:PrefabDunGen` `roomWhitelist` (and/or place a
   `RoomFill` marker where you want the room stamped).
6. Add loot/mobs via `entityTable`s; for jobs add `salvageDungeonMod`/difficulty/faction wiring.
7. Validate: build, YAMLLinter, map loading in an integration test, then in game.

## 1. Decide the model

| You want | Use | Player route |
|---|---|---|
| Rooms assembled by the generator (halls, labs, caves, vaults) | `dungeonRoom` slices of an **atlas map** + `dungeonConfig` | Expeditions/salvage missions, bluespace events |
| A complete self-contained wreck/ship | `salvageMap` (`mapPath`) with a whole map file | Salvage wrecks, magnet debris |
| A place a room is stamped into at runtime (vgroid/wreck interior) | `RoomFill` marker entity | Whatever generates the host grid |

You author just the **atlas rooms**; their entrance/corridor wiring stays in the `dungeonConfig` — you
do not place doors between rooms by hand; the config layers (`DungeonEntranceDunGen`,
`RoomEntranceDunGen`, `JunctionDunGen`, ...) connect them.

## 2. Author the map (maps vs grids)

- A map file is the YAML under `Resources/Maps/...`. A **grid** is an entity (`MapGrid`) inside that
  map file (`grids:` list). Dungeon rooms do **not** need separate grid prototypes: you build the
  room's tiles and entities in a map, and the generator copies a rectangle out of it.
- Start from a template (`Resources/Maps/Dungeon/Templates/3x5.yml`, `17x17.yml`, ...) or an empty
  map in the editor. New maps should be **format 7**; most existing `Resources/Maps/Dungeon/*`
  atlases are the older format 6 and still load.
- Keep each room in a **known rectangle** and leave padding so slices don't bleed into neighbours.
- Place props, machines and markers as entities; remember the copy rules:
  - copied entity prototypes must exist and not be abstract;
  - **only the prototype ID survives** — DataField overrides on atlas entities are lost, so make a
    dedicated prototype instead of tweaking a component instance;
  - structures should be anchored; runtime machines need `ApcPowerReceiver: needsPower: false` or
    `AlwaysPowered*` prototypes; generated maps need `Gravity: enabled/inherent` only if you build a
    standalone site (configs usually handle it).
- Save with the mapping editor and copy the file into `Resources/Maps/_PS/Dungeon/`. Engine
  `savemap` writes to `bin/Content.Server/data`; map loading prefers that user-data copy, so delete
  stale copies after moving the file.

## 3. Declare the room tag

`dungeonRoom.tags` and `PrefabDunGen.roomWhitelist` use **TagPrototype** ids:

```yaml
# Resources/Prototypes/_PS/tags.yml (new file if absent)
- type: tag
  id: PSScienceWing
```

## 4. Slice the map into `dungeonRoom`s

One prototype per rectangle. `offset` is the lower-left corner in grid tiles; `size` is in tiles.

```yaml
- type: dungeonRoom
  id: PSScienceWing17x11a
  size: 17,11
  atlas: /Maps/_PS/Dungeon/science_wing.yml
  offset: 0,0
  tags:
    - PSScienceWing
# add more rooms by offset (e.g. 20,0 and 0,20) with distinct ids
```

Use `ignoreTile: <ContentTileDefinition>` when the atlas uses a filler tile that should not be
copied (see `_NF/Procedural/Themes/supercompacted.yml`).

## 5. Wire it into generation

**A. Into a `dungeonConfig`** (the normal case): the layout layers reference the tags, presets pick
the geometry, and the rest of the stack (corridors, walls, entrances, clutter, ores, mobs) runs after.

```yaml
- type: dungeonConfig
  id: PSScienceWing
  layers:
  - !type:PrefabDunGen
    roomWhitelist:
      tags:
      - PSScienceWing
    presets:
    - Bucket
    - Wow
    - SpaceShip
    - Tall
  - !type:CorridorDunGen
    width: 3
    tile: FloorSteel
  - !type:DungeonEntranceDunGen
    count: 2
    tile: FloorSteel
    contents: NFBaseAirlock
  - !type:BoundaryWallDunGen
    wall: WallSolid
    cornerWall: WallReinforced
  - !type:EntityTableDunGen
    minCount: 1
    maxCount: 2
    table:
      id: PSScienceWingClutter
```

- `dungeonPreset`/`dungeonRoomPack` control which rectangle grid the rooms are placed on: every room
  slot in a pack must match a `dungeonRoom` size (or its rotated size), and `PrefabDunGen.fallbackTile`
  fills a slot when no matching room exists. `DungeonTests.TestDungeonRoomPackBounds`/`TestDungeonPresets`
  enforce bounds, non-overlap and sizes — run them after adding packs/presets.
- To make the site reachable, add a `salvageDungeonMod` (`proto: PSScienceWing`) and reference it from
  a salvage faction/difficulty setup (see `_NF/Procedural/salvage_mods.yml`).

**B. Stamped by a `RoomFill` marker**: put a marker entity into generated geometry, or place it in an
atlas/wreck map, and the `RoomFillSystem` stamps a matching room at runtime.

```yaml
- type: entity
  id: PSScienceWingRoomMarker
  parent: BaseRoomMarker
  name: science wing marker
  components:
  - type: RoomFill
    roomWhitelist:
      tags:
      - PSScienceWing
    # optional: minSize/maxSize tile sizes, rotation enum, clearExisting
```

## 6. Loot and mobs

Everything is `entityTable`-driven. Put your loot spawner markers inside the atlas rooms
(they are copied with the room) or have a config layer place them.

```yaml
# Resources/Prototypes/_PS/Entities/Markers/Spawners/Random/dungeon_spawners.yml
- type: entityTable
  id: PSScienceWingClutter
  table: !type:GroupSelector
    children:
    - !type:NestedSelector
      tableId: TableDungeonLootEngineeringParts
      weight: 0.4
    - !type:NestedSelector
      tableId: TableDungeonLootWeaponsPalmtree
      weight: 0.2
    - id: PSScienceWingLootSpawner
```

For a **rare named drop**, follow the weapon-case chain (worked example):
1. case entity in `Resources/Prototypes/_PS/Catalog/Fills/Items/weapon_cases_expedition.yml`
   (`parent: [WeaponCaseLong, RareWeaponCase]`, `StorageFill`);
2. list it in a weighted `entityTable` (`TableDungeonLootWeaponsPalmtree` in
   `Resources/Prototypes/_CS/Entities/Markers/Spawners/Random/weapon_tables_fallout.yml`);
3. that table is already nested into `SalvageEquipmentLegendary`
   (`Resources/Prototypes/Entities/Markers/Spawners/Random/Salvage/tables_loot.yml`), which feeds
   valuable equipment spawners and `salvageLoot` budgets.

`weight`/`prob`/`cost`/`rolls`/`orGroup` are the standard selectors. Keep weights small for rare
finds.

## 7. Rare boss arena

There is no `bossRoom` prototype type; build it from the same pieces:

1. Author a **large room** (17x17 template) in your atlas with pillars/cover and a clear centre.
2. Tag it uniquely (e.g. `PSBossArena`) and slice it as a `dungeonRoom`.
3. Give it its own `dungeonConfig` whose `PrefabDunGen.roomWhitelist.tags` includes the arena tag
   plus your normal rooms (the arena is then one of the possible rooms) — or gate the whole config
   behind a dedicated `salvageDungeonMod` + difficulty so it only appears on rare jobs.
4. Place the encounter in the atlas room:
   - boss spawner: a `ConditionalSpawner`/`EntityTableSpawner` marker, or a `!type:MobsDunGen`
     table containing `SpawnMobExplorerBoss`/your custom boss;
   - boss loot: `SpawnDungeonLootNpcBossLootShared` (or a custom
     `TableDungeonLootNpcBossLootShared`-style table) and loot markers.
5. Make the boss a proper boss: inherit `MobHostileBossBase` (damage set, stamina, FTL immunity,
   `NFMobBossRestrictions` despawn/pacify rules), give it `GhostRole` +
   `ghost-role-information-dungeon-boss-rules`, and wire its gear through a loadout with
   `orGroup: BossLoot` (see `MobHumanoidExplorerGearBoss`).
6. For an elimination-style hunt instead of a room, add the boss to a
   `salvageFaction.configs.Megafauna` — the mission then tracks it to kill.

## Testing in game

1. `dotnet build SpaceStation14.sln`, then rebuild + run the YAMLLinter
   (`Content.YAMLLinter`, expect "No errors found"). It validates prototypes, **not maps**.
2. Run the layout tests:
   `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~DungeonTests"`
   (room-pack bounds/overlap/size and preset checks).
3. Generate it with the debug command: `dungen <mapId> <yourConfig> <x> <y> [seed]` on a dev server;
   visualise layouts with `dungen_preset_vis <mapId> <preset>` / `dungen_pack_vis <mapId> <pack>`.
4. Check: rooms connect, doors/windows spawn, gravity/power work, loot budgets spawn, nothing is
   missing ("Missing prototype for map: X" at load = a copied entity prototype does not exist).
   Atlas maps are cached per round — restart the server after editing a map.
5. Balance pass: run a full expedition/dungeon and confirm the loot rarity feels right.

## Notes and gotchas

- **Tags are TagPrototypes** — a typo silently drops the room from the whitelist (no error).
- **Rooms are copied by prototype ID only**; DataField overrides in atlas maps are lost.
- **Do not hand-edit tile chunks** in map files; use the mapping editor. Prefer format 7 for new maps.
- **`savemap` writes to user data**, not `Resources/`; delete stale user-data copies or the server
  loads the wrong map.
- **Space/magnet configs**: asteroid magnet dungeon configs are commented out and the magnet's
  asteroid list is hardcoded in `SharedSalvageSystem.Magnet.cs`; round-start `DungeonSpawnGroup`s are
  disabled on Frontier maps (`BaseStationShuttles` is commented out). Route new dungeons through
  expeditions (`salvageDungeonMod`) or bluespace events until those paths are restored.
- **`RandomHumanoidSpawner` self-deletes on MapInit** — use it only for one-shot spawns.
- Palmtree files live under `_PS` (asset placement law in `.ai/CONVENTIONS.md`); ported Coyote
  content keeps `_CS`.
