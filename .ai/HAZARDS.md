# HAZARDS

> Things a future AI agent can easily get wrong. None of these are bugs to "fix" as part of documentation.

## 1. Client sandbox type checking (high risk for new shared/client code)

The client runs the engine's IL type checker (`res.typecheck`). Only whitelisted BCL APIs are allowed
in `Content.Shared` and `Content.Client`. A violation crashes the client at startup with
`Assembly Content.Shared failed type checks`, not a compile error.

Observed failure: `string.Create(IFormatProvider, ref DefaultInterpolatedStringHandler)` is blocked.
`float.ToString(IFormatProvider)`, `CultureInfo`, `System.Single` (`All: True`) and `Enum`
(`All: True`) are allowed; most `string` members are individually whitelisted.
Whitelist lives in `RobustToolbox/Robust.Shared/ContentPack/Sandbox.yml`.

To verify locally without a display:
`dotnet run --project Content.Client -c Debug --no-build -- --headless` → look for
`Content.Shared: Verified IL` / `Content.Client: Verified IL`; the process then dies on OpenGL,
which is expected. There is no sandbox check in `Content.IntegrationTests`.

## 2. Marking filters must be kind-aware

There are several places that filter markings by species; all must use
`MarkingManager.IsAllowedBySpeciesOrKindAllowance`:

- `MarkingManager.MarkingsByCategoryAndSpecies(AndSex)`
- `MarkingManager.IsValidMarking`
- `MarkingManager.CanBeApplied` (both overloads)
- `MarkingsSet.EnsureSpecies`

Using `SpeciesRestrictions.Contains(species)` directly silently strips markings shared through
`kindAllowance` (e.g. `FeroxiTorsoCountershadingF` on a Vulpkanin). Regression test:
`Content.IntegrationTests/Tests/_PS/MarkingKindAllowanceTest.cs`.

Also remember `onlyWhitelisted` logic: a marking counts as whitelisted if it has
`speciesRestriction` **or** `kindAllowance`.

## 3. `MarkingsSet.EnsureDefault` loop

The loop must be `while (points.Points > 0 && index < points.DefaultMarkings.Count)`. Using `||`
indexes past `DefaultMarkings` whenever points > defaults (Rodentia uses 999 points with one default
marking) and crashes entity spawning. It also skips categories that already have markings (so
imported characters are not given defaults on top).

## 4. Fork-patched upstream files

Frontier and this port edit upstream files in place, marked with `// Frontier`, `// Coyote`,
`// Palmtree`, `// Floof`, `// _CS`, etc. Never assume a core file is untouched upstream. Grep for
markers before editing. Keep patches minimal and annotated.

Notable in-place patches added by this port:

- `Resources/Prototypes/Entities/Clothing/Back/backpacks.yml` — `ConcealableClothing` on
  `ClothingBackpack`.
- `Content.Server/GameTicking/GameTicker.LobbyBackground.cs` — 30s rotation timer + status broadcast.
- `Content.Client/Lobby/LobbyState.cs` + `LobbyGui.xaml(.cs)` — background crossfade.
- Species YAML (`Resources/Prototypes/Species/*`, `_DV`, `_NF`, `Nyanotrasen`, `_CS`) — new layers,
  `kind`, `altSprites`.
- `Resources/Prototypes/_NF/Loadouts/**` — `CoyoteJumpsuit` subgroups and extra loadout entries.

## 5. Deliberate namespace collisions for partial classes

Fork code extends upstream classes from `_NF`/`_PS` folders using the upstream namespace, e.g.
`Content.Server/_NF/GameTicking/GameTicker.NFSpawning.cs` declares
`namespace Content.Server.GameTicking; // Intentionally colliding namespaces`. Compile errors like
"partial class already has member" usually mean a duplicate hook in an unexpected folder. Always
`grep -rn "partial class GameTicker"`.

## 6. Engine is off-limits

`RobustToolbox/` is a submodule and the maintainer context says only Wizden modifies the engine. If a
fix seems to require engine changes, document it as an engine dependency instead.

## 7. Engine APIs that differ from public documentation

This engine revision (v267.3.0) differs from commonly documented SS14 APIs:

- **No `[EventHandler]` attribute** — explicit `Subscribe*` calls only.
- **No `[DependsOn]`** — use `UpdatesBefore/UpdatesAfter` in `Initialize`.
- **No `GetPVSInterest`**; PVS uses `SessionSpecific`, `SendOnlyToOwner`, `PvsOverrideSystem`,
  `ExpandPvsEvent`, filters.
- **No `PrototypeReloadEvent`** — use `PrototypesReloadedEventArgs`.
- **No `EntityDeletedEvent`** bus event; only the C# `EntityManager.EntityDeleted` event.
- **No `RoundEndedEvent`**; use `RoundEndMessageEvent` + `GameRunLevelChangedEvent`.
- **No `RoundIdle`**; run levels are `PreRoundLobby`, `InRound`, `PostRound`.
- Map format is **7**.
- `EntityQueryEnumerator<T>` skips paused entities; use `AllEntityQueryEnumerator<T>` to include them.
- Component/field renames observed during the port: `SoundOnTrigger` → `EmitSoundOnTrigger`,
  `OnUseTimerTrigger` → `TimerTrigger`, `ImplantImplantedEvent.Implanted` is now non-nullable.
  Old Coyote prototypes may use the old names.

## 8. Broadcast vs directed events

`RaiseLocalEvent<T>(msg)` never calls `SubscribeLocalEvent<TComp,TEvent>` handlers. If a handler
"doesn't fire", check the raise overload and whether the event is `[ComponentEvent]`.

## 9. Subscription lock

Subscribe only in `Initialize()`. Subscribing later throws "Subscription locked." after startup. Use
`Subs` helpers for CVar/C# event handlers so they auto-unsubscribe on shutdown.

## 10. YAMLLinter staleness and strictness

- Runtime prototype loading logs and skips per-file errors; a broken YAML may only appear as a
  missing entity at runtime.
- `dotnet run --project Content.YAMLLinter --no-build` runs the previously built output **including
  its copied `Content.Shared.dll`**. After C# changes (new enum values, new prototype fields), rebuild
  the linter or the solution first, or you will get bogus errors like
  `Requested value 'BaseLegs' was not found`.
- Unknown prototype fields are errors in the linter, even though runtime deserialization of
  non-prototype data (e.g. character exports) ignores them.
- `ProtoId` fields are validated: an `altSprites` entry pointing at a marking that does not exist
  fails the linter.

## 11. Locale duplicates

Adding a locale key that already exists anywhere in `Resources/Locale/<culture>` fails prototype
loading with `already exist entry of type: Message`. When porting locale files, check for keys that
already exist (the ported `_Floof/anthro.ftl` already contained the leg-style keys that were also
added to `markings-picker.ftl`; the duplicate was removed).

## 12. DB and persistence

- `LogType` numeric values are persisted; do not renumber.
- Migrations exist twice (SQLite + Postgres); update both or a provider breaks.
- Character markings are stored in `Profile.Markings` (jsonb) as strings:
  `markingId@#rrggbb,...` with an optional `@scale,offsetX,offsetY` suffix. The parser tolerates the
  legacy two-segment format. Changing this format requires updating `Marking.ToString` **and**
  `Marking.ParseFromDbString`.
- No consent tables exist; no migrations were added by the port.

## 13. ERP/marking semantics

- Genital markings start hidden: `SharedHumanoidAppearanceSystem.AddMarking` sets `Visible = false`
  for `MarkingCategories.Genital`. Visibility is session state (not persisted in the DB string);
  characters get hidden genitals again after respawn/relog. `ModifyUndies` flips `Marking.Visible`
  via `SetMarkingVisibility`.
- `ModifyUndies` is **self-only** (no consent system). Do not assume other players can toggle your
  markings; the old consent-gated behavior was intentionally dropped.
- Base markings (`Base*` categories) hide the species base layer via `HiddenBaseLayers`; the list is
  rebuilt on every `UpdateLayers` and must be recomputed before hiding.

## 14. Configuration defaults differ from upstream

- `game.defaultpreset = "nfpirate"`, `game.map = "Frontier"`, `game.map_pool = "NFMapPool"`.
- Upstream game presets have `rules: []` — Traitor/Nukeops/Wizard are inert by default.
- `audio.lobby_music_collection` default was changed from `NFLobbyMusic` to `PSLobbyMusic`.
- `game.destination_file` triggers a one-shot data dump and shuts the server down.

## 15. State/global mutable traps

- `IoCManager` is per-thread; systems/managers are process singletons, not per round. Reset
  round-scoped state on `RoundRestartCleanupEvent`.
- `ServerPreferencesManager` caches profiles per user.
- `GameTicker` is a large partial class; prefer a system + rule over adding cross-concern logic.
- Prototype reloads mutate live entities; handlers must be idempotent.

## 16. Content-authoring hazards (carried over from the old docs)

- **Species Head/Chest base sprites require sex-morph variants** (`HeadMale`/`HeadFemale`/
  `TorsoMale`/`TorsoFemale`) or they render wrong/invisible.
- **`MarkingPoints.Required` is effectively unused.**
- **Dungeon rooms are copied by prototype ID only** — atlas entity DataFields are lost.
- **Salvage-magnet asteroid configs are commented out** while code still references them; only debris
  offers work.
- **`RandomHumanoidSpawner` markers self-delete on MapInit** — one-shot.
- **Admin permission cache is built once** in `AdminManager.Initialize()`; commands registered later
  become server-console-only for players.
- **Engine `savegrid`/`savemap` are server-console-only** and write to `data/`, never `Resources/`.

## 17. Stray repository artifacts

- `identifier.sqlite` — 0-byte tracked file at repo root, referenced nowhere.
- `PORTING/` — remnant of the abandoned full-port audit. Most of it was removed from the working
  tree; the decision history is in `.ai/PORTING.md` and the `port`/`port-wip` branches. Do not add it
  to commits accidentally if it reappears.
- `Resources/manifest.yml` window title/logo may not match Palmtree branding.
