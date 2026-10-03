# System: Palmtree (`_PS`) Systems

## Purpose

`_PS` is Palmtree Station's own module: lobby presentation, the concealable-clothing backpack
implant, and the server-specific weapon/grenade set ported from the old codebase.

## Locations

- Code: `Content.Server/_PS`, `Content.Shared/_PS`, `Content.Client/_PS`.
- Prototypes: `Resources/Prototypes/_PS`.
- Locale: `Resources/Locale/en-US/_PS`.
- Textures/audio: `Resources/Textures/_PS`, `Resources/Audio/_PS`.

## Concealable clothing (backpack implant)

| Piece | Path |
|---|---|
| Shared logic | `Content.Shared/_PS/Clothing/SharedConcealableClothingSystem.cs` |
| Server logic | `Content.Server/_PS/Clothing/ServerConcealableClothingSystem.cs` |
| Client visuals | `Content.Client/_PS/Clothing/ClientConcealableClothingSystem.cs` |
| Components | `Content.Shared/_PS/Clothing/Components/{ConcealableClothing,ConcealableClothingImplant,ConcealableClothingUser}Component.cs` |
| Action | `Resources/Prototypes/_PS/Actions/concealable.yml` (`ActionToggleConcealment`, `...Backpack`) |
| Implant/implanter | `Resources/Prototypes/_PS/Entities/Objects/Misc/{subdermal_implants,implanters}.yml` |
| Effects | `Resources/Prototypes/_PS/Entities/effects.yml` (`BlueFlashEffect`) |
| Loadout | `Resources/Prototypes/_PS/Loadouts/Jobs/Contractor/implanter.yml` |
| Locale | `Resources/Locale/en-US/_PS/actions/clothing.ftl` |
| Integration test | `Content.IntegrationTests/Tests/_PS/ConcealableClothingTest.cs` |

How it works:

1. Base `ClothingBackpack` carries `ConcealableClothing` (`toggleAction: ActionToggleConcealmentBackpack`,
   `category: backpack`). This core patch is required — without it nothing can be concealed.
2. Injecting `ClothingConcealmentImplantBackpack` adds `ConcealableClothingUserComponent` with the
   category and refreshes actions for already-equipped clothing.
3. The item action toggles `IsConcealed`; the client clears equipment visual layers for concealed
   items; the server pops up text and spawns `BlueFlashEffect`.
4. Removing the implant removes the category and action.

## Lobby presentation

- Backgrounds + music: `Resources/Prototypes/_PS/lobby.yml` (21 `lobbyBackground` prototypes plus the
  `PSLobbyMusic` sound collection), textures `Resources/Textures/_PS/Lobby/*`, audio
  `Resources/Audio/_PS/Lobby/*`.
- Music selection: `audio.lobby_music_collection` CVar default was changed to `PSLobbyMusic` in
  `Content.Shared/CCVar/CCVars.Audio.cs`.
- Rotation: `Content.Server/GameTicking/GameTicker.LobbyBackground.cs` re-rolls the background every
  30 seconds of game time and broadcasts `TickerLobbyStatusEvent` via `SendStatusToAll()`.
- Crossfade: `Content.Client/Lobby/LobbyState.cs` keeps `_currentBg`/`_nextBg` and animates a second
  `BackgroundFade` `TextureRect` (in `LobbyGui.xaml`) over 1 second with a smoothstep curve.

## Weapons / grenades / hitscan

- `Resources/Prototypes/_PS/Entities/Objects/Weapons/Guns/Battery/battery_guns.yml` — Svalinn Caspian,
  pulse N1984, phaser, medical gun.
- `.../Guns/Launchers/launchers.yml` — China Lake flashbang variants.
- `.../Guns/Projectiles/projectiles.yml`, `.../Guns/Ammunition/explosives.yml`,
  `.../Guns/Ammunition/Projectiles/hitscan.yml` (defines `PSXrayLaserCaspian`, `PhaserLethal`,
  `HealBeam`), `.../Throwable/grenades.yml`.
- `SoundCollections/weapons.yml` + `Resources/Audio/_PS/Weapons/*`, `Resources/Audio/_PS/Effects/Grenades/*`.

Adaptations made when porting (old 2023 prototypes):

- `prefix:` on `EntityPrototype` was removed → folded into `name:`.
- `inhandVisuals` moved from `HitscanBatteryAmmoProvider` to a `- type: Item` component.
- Gun spread fields (`minAngle`, `maxAngle`, `angleIncrease`, `angleDecay`, `fireRate`) must sit on
  `- type: Gun`, not inside `soundGunshot:`.
- `proto: PulseWeak` → `NFPulse` (Frontier hitscan id).
- `BallisticAmmoProvider.count` was removed → use `capacity`.
- `SoundOnTrigger` → `EmitSoundOnTrigger`, `OnUseTimerTrigger` → `TimerTrigger`.
- `noSpawn: true` → `categories: [ HideSpawnMenu ]`.

Not ported from `_PS`: turrets, RCD, strobe lighting, shipyard cauterizer, announcement/interaction
sound collections, custom emotes. See `.ai/PORTING.md`.

## Dependencies

Upstream actions/implants/inventory/equipment visuals, `ContentAudioSystem`/lobby CVar, GameTicker,
`_NF` contractor loadout groups.

## Tests

`ConcealableClothingTest` covers action grant/remove and loadout spawning.
`LobbyBackgroundTest` covers the 30-second rotation server-side.
