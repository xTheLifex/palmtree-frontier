# Idea: Highroller Table (Buckshot Roulette)

| | |
|---|---|
| **Status** | Proposed — not implemented |
| **Scope** | New `_PS` feature: an interactive table entity running a self-contained, turn-based shotgun minigame |
| **Touched areas** | `Content.Shared/_PS/Highroller`, `Content.Server/_PS/Highroller`, `Content.Client/_PS/Highroller`, `Resources/Prototypes/_PS`, `Resources/Locale/en-US/_PS`, possibly `Resources/Textures/_PS` / `Resources/Audio/_PS` |
| **Research anchors** | `_PS/HilbertHotel` (per-actor BUI + context verbs + registry), `Tabletop` (session/`GetVerbsEvent<ActivationVerb>` pattern), `InteractionPanelSystem` (`GetVerbsEvent<Verb>`, per-actor UI), `UnremoveableComponent`/`GettingPickedUpAttemptEvent` (anti-theft), `TableBase` (`base_structuretables.yml`) |
| **Prior art** | None. No Buckshot Roulette code exists in this repo, the `COYOTE` checkout, or the BYOND checkouts (searched `buckshot`/`roulette`/`highroller`). Everything below is a from-scratch design. |

This document is the reviewed plan requested before implementation. It is deliberately opinionated
and calls out the decisions that need the maintainer's sign-off (§13).

---

## 1. Summary

Add a **Highroller Table**, an anchored structure players interact with to run a Buckshot Roulette
game:

- Right-click context menu (verbs): **Join Game**, **Leave Game**, **Begin Game** (host),
  **Kick &lt;player&gt;** (host).
- The first player to join becomes the **host**. Host starts the match, which **locks** new players
  out.
- Anyone who walks **16 tiles** from the table is automatically removed from the game.
- Starting a match spawns a **special shotgun** tethered to the table. It cannot be stolen: if it
  leaves the table's tether radius, is put into a container, or would be taken by someone whose turn
  it is not, it is cancelled/returned (deleted and respawned at the table).
- The match is a fully **server-authoritative** state machine (match → stage → load → turn) with a
  hidden randomized shell order. Clients only ever receive public counts and their own private
  reveals.

The game tracks its **own** health/charges and item inventory — it must not use the SS14 mob damage
or inventory systems for game damage/items (see §13.1). The shotgun is a prop/token, not a
functional `GunComponent` weapon.

---

## 2. Terminology

Adopt the spec's four-level vocabulary everywhere in code and locale:

| Term | Meaning |
|---|---|
| **Match** | The whole multiplayer game (all stages). |
| **Stage** | One scoring contest; lasts until one player is left alive. |
| **Load** | One shotgun ammunition batch (2–8 shells); a new load re-rolls counts + order. |
| **Turn** | One player's opportunity to use items and fire once. |

Avoid the word "round" in code and UI — it is ambiguous in this game.

---

## 3. Entity model

### 3.1 The table

New prototype in `Resources/Prototypes/_PS/Entities/Structures/Furniture/highroller_table.yml`:

```yaml
- type: entity
  id: HighrollerTable
  parent: TableBase              # anchored, climbable, placeable, structural; see base_structuretables.yml
  name: highroller table
  description: A felt-topped table set up for a high-stakes game of buckshot roulette.
  components:
  - type: Sprite
    sprite: _PS/Structures/Furniture/highroller_table.rsi   # or reuse an existing table rsi initially
    state: table
  - type: HighrollerTable
    joinRange: 2.0             # must be this close to join
    leaveRange: 16.0           # auto-kick distance
    tetherRange: 3.0           # shotgun yank-back distance
    maxPlayers: 4
    minPlayers: 2
    stageCount: 3
    stageHealth: [2, 4, 6]     # per-stage max charges
    itemsPerLoad: [2, 2, 1]    # distributed per player at each load start (by stage)
    gunPrototype: HighrollerShotgun
  - type: UserInterface
    interfaces:
      enum.HighrollerTableUiKey.Key:
        type: HighrollerTableBoundUserInterface
  - type: ActivatableUI         # pressing E opens the same panel as the "Open game" verb
    key: enum.HighrollerTableUiKey.Key
    requiresComplex: false
```

`HighrollerTableComponent` lives in `Content.Shared/_PS/Highroller/Components/` and is a plain
`[RegisterComponent]` holding **configuration only** (the fields above). Runtime match state is
**not** stored on the component — see §5 — because a networked component would leak the hidden shell
order to any modified client (the exact anti-cheat requirement in the spec).

### 3.2 The shotgun

New prototype in `Resources/Prototypes/_PS/Entities/Objects/Weapons/Guns/Shotguns/highroller_shotgun.yml`:

```yaml
- type: entity
  id: HighrollerShotgun
  parent: BaseItem
  name: highroller's shotgun
  description: A well-worn pump-action shotgun with the serial number filed off. It only works on the table it came from.
  components:
  - type: Sprite
    sprite: _PS/Objects/Weapons/Guns/Shotguns/highroller.rsi   # or reuse Weapons/Guns/Shotguns/pump.rsi
    state: icon
  - type: Item
    size: Large
    shape:
    - 0,0,4,1
  - type: HighrollerTableGun
  - type: Unremoveable        # optional belt-and-braces; see §7
    deleteOnDrop: false
```

`HighrollerTableGunComponent` (`Content.Shared/_PS/Highroller/Components/`):

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HighrollerTableGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Table;    // the HighrollerTable this gun belongs to
}
```

**No `GunComponent`, no `BallisticAmmoProviderComponent`.** If the shotgun were a real gun it could
actually shoot/damage mobs outside the rules and would break the game's separate damage model. It is
a visual token whose "firing" is resolved entirely by the table system.

---

## 4. Lobby interaction (context menu)

Subscribe `GetVerbsEvent<Verb>` on `HighrollerTableComponent` (server), mirroring
`InteractionPanelSystem.OnGetVerbs` / `TabletopSystem.AddPlayGameVerb`. Guard on
`args.CanAccess && args.CanInteract` and on the viewer having an `ActorComponent`.

Verbs added, filtered by viewer state:

| Condition | Verb | Effect |
|---|---|---|
| Viewer not in match, lobby not locked, in `joinRange`, match has room | `Join Game` | `TryJoin` → add participant; if first, become host |
| Viewer in match | `Leave Game` | `TryLeave` |
| Viewer is host, in lobby, participant count ≥ `minPlayers` | `Begin Game` | `TryBegin` → lock + start Stage 1 |
| Viewer is host, participant is not the host | `Kick <name>` (one verb per participant) | `TryKick` |
| Viewer in match | `Open Game` (`ActivationVerb`, Priority) | open the BUI |

Notes / decisions:

- **Host** = first joiner. If the host leaves/kicked/disconnects during the lobby, promote the next
  participant (join order). During a match, a leaving host does not transfer control — there are no
  host-only in-match actions; the new host only matters if the match returns to a lobby (it does not)
  or for a rematch feature (out of scope).
- `Begin Game` uses `Disabled = true` + `Message` when there are too few players, matching the
  engine's recommended pattern, rather than hiding the verb.
- Kick verbs are generated per participant; `Verb.CompareTo` dedupes on text/category, so include the
  target name in `Text` (`Kick Alice`) and a shared `VerbCategory`.
- All verbs act server-side; popups must use `PopupEntity(message, entity, recipient)` (not
  `PopupClient`, which is a no-op on the server).

`TryJoin` validation (all server-side):

1. `TryComp<ActorComponent>(user, out var actor)`.
2. Match is in `Lobby` and `!Locked`.
3. `_transform.InRange(user, table, joinRange)` and same map.
4. `match.Players.Count < maxPlayers`.
5. User is not already a participant and not the same session as another participant.
6. Spawn/prepare `HighrollerPlayer` (mob, `NetUserId`, name from `MetaData`).

`TryLeave`:

- **Lobby:** remove participant, reassign host if needed, broadcast new lobby state.
- **Match:** treat as an elimination (health → 0 / `Alive = false`, removed from turn rotation),
  announce, and check stage/match end. Decide between "forfeit the match" vs "eliminated from the
  current stage only" (§13.6).

---

## 5. Server-authoritative runtime state

Follow the `HilbertHotelSystem` pattern: keep a registry in the server system, not on the networked
component.

`Content.Server/_PS/Highroller/HighrollerTableSystem.cs` owns:

```csharp
private readonly Dictionary<EntityUid, HighrollerMatch> _matches = new();
```

Runtime classes (server-only, `Content.Server/_PS/Highroller/`):

```csharp
public enum HighrollerMatchState { Lobby, InProgress, Finished }

public sealed class HighrollerMatch
{
    public EntityUid Table;
    public HighrollerMatchState State = HighrollerMatchState.Lobby;
    public bool Locked;                       // true once Begin pressed (blocks new joiners)
    public List<HighrollerPlayer> Players = new();   // also the seating/turn order
    public int CurrentIndex;
    public TurnDirection Direction = TurnDirection.Clockwise;
    public int StageNumber;                   // 1-based
    public int StageCount = 3;
    public List<Shell> Shells = new();        // front = index 0; never sent to clients
    public bool IsSawed;
    public EntityUid? Gun;
    public Dictionary<NetUserId, int> StageWins = new();
    public Dictionary<NetUserId, Dictionary<int, bool>> KnownShells = new(); // shellId -> isLive
    // target-selection continuation state for Jammer/Adrenaline etc.
}

public sealed class HighrollerPlayer
{
    public EntityUid Mob;
    public NetUserId UserId;
    public string Name = string.Empty;
    public int Health;
    public int MaxHealth;
    public bool Alive => Health > 0;
    public bool SkipNextTurn;
    public List<HighrollerItemType> Items = new(); // capped (8)
}

public sealed class Shell
{
    public int Id;      // unique within the load, stable across Beer/Inverter
    public bool Live;
}

public enum TurnDirection : byte { Clockwise, Counterclockwise }
```

**Privacy invariant:** `Shells`, `IsSawed` (until fired), and other players' `Items` never cross the
network. Only `KnownShells[viewer]` may be sent to that viewer. This is the core anti-cheat rule from
the spec; enforce it by never putting these fields on an `AutoGenerateComponentState` component and
by building BUI state per actor.

---

## 6. Match / stage / load / turn flow

### 6.1 Stage setup (`StartStage`)

1. `Locked = true`.
2. `StageNumber++`.
3. For each participant: `MaxHealth = stageHealth[StageNumber-1]`, `Health = MaxHealth`,
   `SkipNextTurn = false`, clear `Items` and `KnownShells`.
4. `GenerateLoad()`.
5. `DistributeItems()` (`itemsPerLoad[StageNumber-1]` per living player, capped at 8, no duplicates of
   `Adrenaline`? see §9).
6. Choose starting player (recommend: rotate the starter each stage, or random; §13.4).
7. `SpawnOrResetGun()`.
8. Announce shell counts (see §6.2) and begin the turn.

### 6.2 Load generation (`GenerateLoad`)

- Random count in `[2, 8]` with at least 1 live and 1 blank (per spec).
- Build a list of `Shell { Id, Live }`, shuffle, store as `Shells`.
- **Reveal counts to everyone:** "3 LIVE. 2 BLANK." (chat + BUI). Never reveal order.
- Reloads within a stage generate a new load and redistribute items. The player who fired the last
  shell **keeps the turn** after a reload when that last shot was a self-fired blank (spec edge case
  13); otherwise normal turn advancement happens first, then the next player starts the fresh load.

### 6.3 Turn resolution (`FireShotgun`)

Faithful implementation of the spec's `TakeTurn`/`AdvanceTurn` pseudocode:

```text
shooter, target (shooter or another living participant)
shell = Shells[0]; Shells.RemoveAt(0)
damage = 0
if shell.Live: damage = IsSawed ? 2 : 1
IsSawed = false
target.Health -= damage
if target.Health <= 0: eliminate target (Alive=false)
announce result (live/blank, damage)
if OnlyOneAlive(): EndStage(); return
if Shells.Count == 0: GenerateLoad(); DistributeItems()
if target == shooter && !shell.Live: StartTurn(shooter)   // keep turn
else: AdvanceTurn()
```

`AdvanceTurn` walks the cyclic `Players` list in `Direction`, skipping the dead, and consumes
`SkipNextTurn` on the first living player that has it (which is then skipped, and the search
continues). Wrapping must be safe when only one player is alive (stage already ended by then).

### 6.4 Stage / match end

- Stage ends when `OnlyOneAlive()`. Award a stage win to the survivor.
- If `StageNumber < StageCount`, start the next stage.
- Match winner = most stage wins after the last stage; tie → sudden-death stage or shared win
  (§13.5). On match end, clean up the gun, unlock the table for a new lobby, and announce.

---

## 7. The tethered shotgun

### 7.1 Spawn

- On stage start (and whenever the gun is missing), `SpawnAtPosition(gunPrototype, table coords)` and
  `EnsureComp<HighrollerTableGunComponent>(gun).Table = table`.
- The gun sits on the table's `PlaceableSurface` (it already exists on `TableBase`) so it is visible
  to non-holders.

### 7.2 Take restriction

Subscribe `GettingPickedUpAttemptEvent` (directed at the item) and/or `PickupAttemptEvent` (directed
at the mob):

- Cancel unless the would-be holder is the **current turn player** in an `InProgress` match.
- Cancel while the match is not in progress.
- Popup the reason ("It is not your turn." / "The game has not started.").
- Consider allowing any player to *move* the gun back to the table but not into their inventory.

### 7.3 Tether sweep (in `Update`)

Run the same periodic sweep that handles distance auto-kick (once per second is plenty):

- If `match.Gun` is deleted → respawn.
- If the gun's map differs from the table's, or `!InRange(gun, table, tetherRange)`, or the gun is
  inside a container that is not a hand slot → `QueueDel(gun)` and respawn at the table.
- Optionally cancel `EntInsertedIntoContainerMessage` on the gun (belt/backpack/pocket) instead of
  waiting for the sweep.

### 7.4 Firing

Because the gun is not a real weapon, firing is driven by the **current player** through the BUI
(§8) or through the gun's own context verbs:

- `Shoot self` → `FireShotgun(shooter: viewer, target: viewer)`.
- `Shoot <name>` for each other living participant → `FireShotgun(shooter: viewer, target: them)`.

Both are only added when the viewer is the current player, is holding the gun, and the match is in
progress. This satisfies "only the next in turn order can take the shotgun and do anything with it".

---

## 8. UI and networking

### 8.1 Why a BUI

Lobby actions fit the context menu, but item use, target selection, private reveals and per-player
health/turn status need a persistent panel. Reuse the `_PS/HilbertHotel` per-actor BUI pattern:
`BoundUserInterfaceMessage`s carrying a state object built **per viewer**.

- `Content.Shared/_PS/Highroller/HighrollerUi.cs` — `HighrollerTableUiKey`, enums, state DTOs,
  request messages.
- `Content.Client/_PS/Highroller/HighrollerTableBoundUserInterface.cs` +
  `HighrollerTableWindow.xaml{,.cs}` — plain `DefaultWindow` (same style as
  `HilbertHotelRoomControllerWindow`).
- Server sends on `BoundUIOpenedEvent` and on every state change, via
  `UserInterfaceSystem.ServerSendUiMessage(..., actorUid)`.

### 8.2 State message (per viewer)

```csharp
[Serializable, NetSerializable]
public sealed class HighrollerTableStateMessage : BoundUserInterfaceMessage
{
    public HighrollerMatchState State;
    public bool IsHost;
    public bool InMatch;
    public bool Locked;
    public int StageNumber;
    public int StageCount;
    public string CurrentPlayer = string.Empty;
    public TurnDirection Direction;
    public int LiveRemaining;      // public
    public int BlankRemaining;     // public
    public List<HighrollerPlayerEntry> Players = new(); // name, health, max, alive, current, skip, stageWins
    public List<HighrollerItemType> YourItems = new();
    public bool YourTurn;
    public bool HoldingGun;
    // private to the viewer only:
    public bool? KnownCurrentShellLive;      // magnifying glass / inverter inference
    public List<HighrollerShellHint> Hints = new(); // burner phone: offset + live
}
```

### 8.3 Requests

`HighrollerJoinMessage` is not needed (join is a verb), but the panel needs:
`HighrollerUseItemMessage(item, optional targetUserId)`, `HighrollerShootMessage(targetUserId or self)`,
`HighrollerRefreshMessage`, and lobby `HighrollerBeginMessage` / `HighrollerKickMessage` for parity
with verbs (optional).

Server validates every request against `ActorComponent` and the match state; never trust the client.

---

## 9. Items

Implement items as an enum + server logic (not physical SS14 entities), matching the original's
abstract inventory. The multiplayer pool is exactly:

`Adrenaline, Beer, BurnerPhone, CigarettePack, HandSaw, Inverter, Jammer, MagnifyingGlass, Remote`.

`Handcuffs` and `ExpiredMedicine` are **excluded** from the multiplayer pool (Handcuffs is the
two-player analogue of Jammer; Expired Medicine is a Double-or-Nothing item).

| Item | Server behaviour | Turn ends? | Notes |
|---|---|---|---|
| Magnifying Glass | Reveal current shell to user only (`KnownShells[user][Shells[0].Id] = Shells[0].Live`) | no | private |
| Beer | Remove `Shells[0]`, announce its type publicly, keep turn | no | if the gun empties, reload; per edge case 13 the same player stays active |
| Cigarette Pack | `Health = min(MaxHealth, Health + 1)` | no | usable at full health (still consumed) |
| Hand Saw | `IsSawed = true` | no | not stackable; consumed by the next discharge even if blank |
| Jammer | chosen living player `SkipNextTurn = true` | no | targets the player's *next* turn |
| Inverter | flip `Shells[0].Live`; invert `KnownShells[*][id]` for everyone who knew that shell | no | unknown stays unknown |
| Burner Phone | pick a random shell **after** the current one; store `KnownShells[user][id] = live` with an offset hint | no | fails (failure message) when ≤ 2 shells remain |
| Remote | flip `Direction` | no | no effect with 2 players (documented) |
| Adrenaline | two-step: choose a living player, then one of their items (not Adrenaline); remove it and immediately execute it as the user | inherits | must explicitly block stealing Adrenaline to prevent recursion |

Implementation notes:

- Keep knowledge keyed by `Shell.Id`, never by index, so Beer/Inverter/Burner Phone compose
  correctly (spec edge cases 4–8).
- `Inverter` must also update any player's stored knowledge for the flipped shell id.
- Item distribution at load start: `itemsPerLoad[stage]` random items per living player, capped at 8.
  Avoid giving `Adrenaline` on the very first load of a stage if that is too swingy (§13.7).
- Adrenaline and Jammer require a target-selection continuation. Model it explicitly in
  `HighrollerMatch` (e.g. `PendingItem` + `PendingUser`) rather than via UI-only state, and clear it
  when the turn advances.

---

## 10. Distance auto-kick and lifecycle sweeps

`HighrollerTableSystem.Update(frameTime)` (throttled, e.g. every 1 s, following
`HilbertHotelSystem`'s `SweepInterval`):

For every match:
1. If the table is deleted → clean up and skip.
2. For each participant: if the mob is deleted, is a ghost, or
   `!InRange(mob, table, leaveRange)` (or different map) → remove/eliminate + popup.
3. Tether sweep for the gun (§7.3).
4. Rebuild/refresh open BUI state if anything changed.

Lifecycle:

- `ComponentShutdown` / entity termination on the table → delete gun, remove `_matches[table]`.
- `RoundRestartCleanupEvent` → clear `_matches` (the engine deletes maps/entities anyway), mirroring
  `HilbertHotelSystem.OnRoundRestartCleanup`.
- Player disconnect (`PlayerDetachedEvent` or session detach) → treat as leave/elimination.
- Actual SS14 death of a participant's mob: the game keeps its own health, so a mob dying from
  unrelated causes should remove the player from the match (decide in §13.2).

---

## 11. Proposed file layout

```
Content.Shared/_PS/Highroller/
  Components/HighrollerTableComponent.cs
  Components/HighrollerTableGunComponent.cs
  HighrollerUi.cs                      # UI key, enums, DTOs, messages
  HighrollerItemType.cs                # shared enum (client shows names)

Content.Server/_PS/Highroller/
  HighrollerTableSystem.cs             # verbs, lobby, match loop, sweeps
  HighrollerTableSystem.Ui.cs          # per-actor state build/send
  HighrollerTableSystem.Items.cs       # item behaviours
  HighrollerTableSystem.Gun.cs         # spawn/tether/firing
  HighrollerMatch.cs                   # server-only runtime classes
  HighrollerShotgunSystem.cs           # pickup lock + gun verbs (optional split)

Content.Client/_PS/Highroller/
  HighrollerTableBoundUserInterface.cs
  HighrollerTableWindow.xaml
  HighrollerTableWindow.xaml.cs

Resources/Prototypes/_PS/Entities/Structures/Furniture/highroller_table.yml
Resources/Prototypes/_PS/Entities/Objects/Weapons/Guns/Shotguns/highroller_shotgun.yml
Resources/Locale/en-US/_PS/highroller.ftl
Resources/Textures/_PS/...            # optional custom sprites
Resources/Audio/_PS/...               # optional shell rack/fire sounds
```

Tests (only if the feature lands): extend the existing `_PS` integration test file rather than adding
a new one (see `.ai/systems/ps-systems.md` "Tests"). Suggested coverage: join/host/begin/kick,
distance auto-kick, load counts + privacy (shell order never in state), self-blank keeps turn,
inverter + magnifying-glass knowledge, gun pickup lock and tether respawn.

---

## 12. Validation pipeline (when implemented)

Per the project skill and `.ai/guides/changelogs.md`:

```bash
dotnet build SpaceStation14.sln
dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj
dotnet run --project Content.YAMLLinter -c Debug --no-build      # expect "No errors found"
dotnet test Content.Tests/Content.Tests.csproj
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj \
  --filter "FullyQualifiedName~EntityTest|FullyQualifiedName~_PS"
dotnet run --project Content.Client -c Debug --no-build -- --headless   # sandbox IL check
```

Player-visible change ⇒ commit message carries a `:cl:` block (add/tweak entries); never edit the
upstream changelog files.

---

## 13. Decisions needed before implementation

1. **Damage model.** Recommended: game-owned "charges" (health) that do **not** touch SS14 mob
   damage, so a game cannot maim/kill characters and Cigarettes can heal game health. Alternative:
   apply real damage (rejected — breaks healing semantics and is grief-prone). Confirm.
2. **Real mob death / incapacitation** mid-match: remove the participant, or keep playing? Recommend
   remove.
3. **Gun in hand required to fire?** Recommended yes for the turn player (spec: "only the next in
   turn order can take the shotgun and do anything with it"); BUI shooting buttons disabled
   otherwise.
4. **Stage starter.** Rotate, random, or previous stage winner? Recommend rotate for fairness.
5. **Scoring.** 3 stages + most wins, or "first to 2 stage wins", or prize money? Recommend
   "first to 2 stage wins" for a clean SS14 minigame, or 3 stages + tie-break stage.
6. **Leaving mid-match** = eliminate for the current stage, or forfeit the whole match? Recommend
   eliminate for the match.
7. **Adrenaline / item distribution balance** per stage, and whether the 8-item cap is per player
   for the whole match or per load.
8. **Physical items?** Recommended no (abstract inventory). Physical consumable entities are a
   stretch goal but add grief surface and art cost.
9. **Sprites/audio.** Reuse existing table/shotgun art first, or commission/produce `_PS` art? A
   felt-table sprite and a couple of shell sounds are the main art asks.
10. **Max players.** Spec says 2–4 for multiplayer; confirm 4.

---

## 14. Risks / hazards to keep in mind

- **Never network the shell order** (or `IsSawed` before resolution, or others' items). Keep runtime
  state off the component; build BUI state per actor.
- **Server popups:** use `PopupEntity(message, entity, recipient)`; `PopupClient` silently does
  nothing on the server.
- **Subscription lock:** subscribe only in `Initialize()`.
- **Sandbox:** `Content.Shared`/`Content.Client` are IL-verified; avoid blocked BCL calls (e.g.
  `string.Create(IFormatProvider, …)`) in client code — use `float.ToString(CultureInfo.InvariantCulture)`.
- **Stale YAMLLinter:** rebuild the linter after C# changes before trusting `--no-build` output.
- **Locale duplicates** are load errors; unknown prototype fields are lint errors.
- **Cleanup:** table deletion, round restart, and player disconnect must all tear down the match and
  the gun.
- **`Transform.InRange`** is straight-line; always also compare maps so a player on another map is
  not treated as "near" (and vice versa for the gun tether).
- **Verb dedupe:** per-target kick verbs must have unique `Text`.
- **Multiple tables:** everything is keyed by the table `EntityUid`; never assume a single instance.
- **`UnremoveableComponent` semantics:** `DeleteOnDrop` deletes the item, which is *not* the desired
  respawn behaviour by itself — use the explicit tether sweep + `GettingPickedUpAttemptEvent`
  cancellation, and treat `Unremoveable` as optional hardening.
