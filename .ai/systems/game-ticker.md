# System: Game Ticker (Rounds)

## Purpose

`GameTicker` is the server's round state machine: lobby, map load, spawning, rules, end/restart.

## Locations

`Content.Server/GameTicking/GameTicker*.cs` (partials: `RoundFlow`, `Player`, `Spawning`,
`GamePreset`, `GameRule`, `Lobby`, `LobbyBackground`, `CVars`, `Replays`, `StatusShell`),
`Content.Server/_NF/GameTicking/GameTicker.NFSpawning.cs` (intentional namespace collision),
`Content.Server/GameTicking/Rules/*`.

## Run levels

`PreRoundLobby → InRound → PostRound`. There is no `RoundIdle`. Level changes raise
`GameRunLevelChangedEvent`.

## Presets and rules

- Presets are `gamePreset` prototypes; live ones: `NFAdventure`, `NFPirate`, `NFTest`
  (`Resources/Prototypes/_NF/game_presets.yml`).
- Upstream presets (`Resources/Prototypes/game_presets.yml`) have `rules: []` and are inert.
- Rules are `GameRuleSystem<T>` entities; `GameTicker.GameRule.cs` spawns/starts/stops them and
  raises `GameRuleAddedEvent`/`GameRuleStartedEvent`/`GameRuleEndedEvent`.
- `GameRuleComponent` has no `MaxTime`/`EndDelay` in this revision; use
  `MaxTimeRestartRuleComponent`.

## Round flow

```
StartRound → LoadMaps → RoundStartingEvent → RoundStartAttemptEvent → RulePlayerSpawningEvent
    → job assignment → SpawnPlayer per player → RulePlayerJobsAssignedEvent → RoundStartedEvent
    → in-round → EndRound → RoundEndTextAppendEvent → RoundRestartCleanupEvent → lobby
```

Round-scoped state must reset on `RoundRestartCleanupEvent`.

## Lobby

- `GameTicker.Lobby.cs` handles ready state and status events (`TickerLobbyStatusEvent`,
  `TickerLobbyInfoEvent`, `TickerLobbyCountdownEvent`).
- `GameTicker.LobbyBackground.cs` selects the background and (Palmtree) rotates it every 30 seconds
  of game time, broadcasting status so clients refresh.
- Client `LobbyState` renders the screen, lobby song, player list, ready button and the crossfading
  background.

## Config defaults

| CVar | Default |
|---|---|
| `game.defaultpreset` | `nfpirate` |
| `game.fallbackpreset` | upstream fallback (verify in `CCVars.Game.cs`) |
| `game.map` | `Frontier` |
| `game.map_pool` | `NFMapPool` |
| `game.lobbyduration` | 180 |
| `audio.lobby_music_collection` | `PSLobbyMusic` (Palmtree) |

## Spawning hooks (NF/Palmtree)

`GameTicker.NFSpawning.cs` + `StationSpawningSystem.SpawnPlayerMob` apply NF loadouts and bank
balance; species appearance is applied through `HumanoidAppearanceSystem.LoadProfile`.

## Unknowns

- Live config values; see `.ai/UNKNOWN.md`.
