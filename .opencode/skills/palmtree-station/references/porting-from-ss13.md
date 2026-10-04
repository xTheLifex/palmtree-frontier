# Porting SS13 (BYOND) content — operational reference

Full details: `.ai/guides/porting-from-ss13.md`. Use this when a task says "port X from
S.P.L.U.R.T / Sandstorm / an SS13 codebase" or when converting `.dmm` maps.

## Sources (on this machine, paths contain spaces)

Base: `/media/thelife/EC7443D97443A564/Documents and Settings/TheLife/Desktop/TheLife/Development/BYOND/`

- `S.P.L.U.R.T-tg` — newest SPLURT (modern /tg/). Newer Hilbert's Hotel "condo" system
  lives in `modular_zubbers/code/modules/condos/`; SPLURT extensions in
  `modular_zzplurt/code/modules/hilbertshotel/` and `condo_infinidorms*.dm`; vanilla /tg/ ruin in
  `code/modules/mapfluff/ruins/spaceruin_code/hilbertshotel.dm`.
- `S.P.L.U.R.T-Station-13` — old SPLURT. Old Hilbert's Hotel ERP variant in
  `modular_splurt/code/modules/ruins/spaceruin_code/hilbertshotel.dm`, templates under
  `_maps/splurt_maps/templates/`.
- `Sandstorm-Station-13` — Sandstorm fork with its own `.ai/` docs.

`modular_*` folders are fork additions; core code is `code/`. Find features with
`grep -ril "<feature>" <checkout> --include="*.dm"`.

## `.dmm` map conversion

`Tools/convert_dmm_room.py <in.dmm> <out.yml> --name "Room Name"` converts a TGM-format SS13 room
into an SS14 format-7 map.

TGM format gotchas:

- Dictionary `"key" = (atoms...)` at the top; grid `(x,y,z) = {"` with **one variable-length key
  per line**. Fixed-width row parsing is wrong.
- Atom variables `{dir = 4; ...}` may nest parentheses; depth-count when splitting.
- Normalize min x/y; BYOND y is up. Ignore `/area`.
- Facing is either a `dir` variable or a `/directional/<name>` path suffix
  (`north/south/east/west`).
- Common fork turfs: `hotelwood`, `hoteltile`, `dark`, `bathroom`, `hotelwall`, `hoteldoor`
  (+`fakedoor`), `fakeglass`, `bluespace`, `cordon` (edge), `template_noop` (skip).
- Multiple atoms per tile are normal; resolve large furniture overlaps, never cull walls/windows/
  doors/controllers, and dedupe exact duplicates.

Converter specifics: it resolves concrete prototypes and `ApcPowerReceiver` inheritance
(`needsPower: false`), emits `anchored: True`, grid `Gravity: enabled/inherent`, `MapLight` and a
standard-air `GridAtmosphere`, places the landing marker on the nearest entity-free floor tile from
the first exit, relocates a door-adjacent controller, and applies `DIR_TO_ROT`
(north π / south 0 / east π/2 / west -π/2) only to `DIRECTIONAL_PROTOS` (never rotate `noRot`
entities).

## SS14 map facts you must not forget

- Save as `category: Map` (`savemap`, not `savegrid`).
- Tiles are 16x16 chunks of 7-byte entries (int32 YAML id + flags + variant + rotationMirroring),
  base64. `GridAtmosphere` v2 uses `chunkSize: 4` + `uniqueMixes` (12 mole values) + bitmasks; it is
  the only reliable way to pressurize indoor runtime-loaded maps.
- `GravityComponent.Enabled` defaults to **false**.
- `TryLoadMap` returns a paused, uninitialized map; call `SharedMapSystem.InitializeMap`.
- Exits/windows/walls must be airtight or the room leaks.
- `savemap` writes to the server user-data dir (`bin/Content.Server/data`), which **shadows
  `Resources`** on later loads.

## SS13 → SS14 translation

| SS13 | SS14 |
|---|---|
| turf block reservation + `map_template.load` | one runtime-loaded map per room |
| `area/Exited` last-mind check | periodic occupancy sweep (`MobStateComponent`/`ActorComponent`, no ghosts/dead) |
| conserved/stored rooms | keep map loaded for a grace period, then delete |
| TGUI | XAML BUI + per-actor messages |
| `requires_power = FALSE` | `needsPower: false` + `AlwaysPowered*` |
| `default_gravity` | grid `Gravity: enabled/inherent` |

## Sprites

`.dmi` (PNG + zTXt metadata) vs `.rsi` (directory + `meta.json` + per-state PNGs). RSIEdit is
GUI-only (no CLI/source) — automated conversion needs a custom zTXt/Pillow script; prefer
SS14-native art. Keep `meta.json` license/copyright and audio `attributions.yml`.

## Validation

YAMLLinter does not check maps. Gate with integration tests (load each map, run atmos ticks, assert
pressure/gravity/anchoring/power) and `EntityTest`; visually check with
`Content.MapRenderer -f <filesystem path>`. Then run the normal pipeline (build → linter → unit →
targeted tests → headless client).

## Top hazards

- **`PopupClient` is a no-op server-side** — use `PopupEntity(message, entity, recipient)`.
- Never pause a runtime map with a body on it (`EntityPaused` freezes the player).
- `Gravity.Enabled` defaults false; machines default to needing APC power; props default unanchored.
- Rotating a `noRot` entity in a map file fails at load.
- Abstract prototypes in maps fail at load ("Missing prototype for map").
- Prototype YAML can start with a UTF-8 BOM — parse with `utf-8-sig`.
- Pooled integration tests share servers: clean up rooms/maps in `finally`.
