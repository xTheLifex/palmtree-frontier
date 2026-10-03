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

## Synth species (IPC replacement)

`Synth` is Palmtree's replacement for the IPC species. It is a humanoid species that uses the
marking system to look like anything and approximates IPC traits without the Einstein Engines silicon
stack.

| Piece | Path |
|---|---|
| Species + base sprites + marking points | `Resources/Prototypes/_PS/Species/synth.yml` |
| Mob + dummy | `Resources/Prototypes/_PS/Entities/Mobs/Species/synth.yml` |
| Player mob | `Resources/Prototypes/_PS/Entities/Mobs/Player/synth.yml` |
| Body parts (1.5x leg speed) | `Resources/Prototypes/_PS/Body/Parts/synth.yml` |
| Body prototype | `Resources/Prototypes/_PS/Body/Prototypes/synth.yml` |
| Damage container + modifier set | `Resources/Prototypes/_PS/Damage/{containers,modifier_sets}.yml` |
| Coolant reagent | `Resources/Prototypes/_PS/Reagents/oxidant.yml`, locale `_PS/reagents/oxidant.ftl` |
| Species locale | `Resources/Locale/en-US/_PS/species/synth.ftl` |
| Test | `Content.IntegrationTests/Tests/_PS/SynthSpeciesTest.cs` |

Properties:

- **1.5x speed**: Synth legs carry `MovementBodyPart walkSpeed: 3.75 / sprintSpeed: 6.75` (human
  2.5/4.5). The `Body` system derives base speed from leg parts, so this must be done on the parts,
  not only on the mob's `MovementSpeedModifier`. (Higher values caused client prediction instability.)
- **3x durability**: `MobThresholds` 300 Critical / 600 Dead (human 100/200); `SlowOnDamage` and gib
  thresholds scaled.
- **IPC-like**: custom `Synth` damage container (Brute/Burn groups, Radiation/Bloodloss types),
  `Synth` damage modifier set (0.8 brute, immune poison/asphyxiation/cold, weak heat/shock),
  `ZombieImmune`, `CanHostGuardian`, robot typing indicator, insulated temperature, `Oxidant` coolant
  blood, fixture density 462.5 (heavier, matching IPC).
- **Silicon parts**: the Synth body uses `_EE/Mobs/Species/IPC/parts.rsi` sprites (robot limbs/head),
  `Inorganic` damage container on parts, and synthetic organs (`OrganSynthEyes`, `OrganSynthPump`,
  `OrganSynthBrain`, plus unused `OrganSynthTongue`/`OrganSynthEars`). Base sprites default to the
  robot look; species-adaptor markings replace them.
- **No breathing**: `SynthComponent` marks the mob and `RespiratorSystem.Update` skips it, so synths
  do not gasp or suffocate despite having no lungs.
- **Digitigrade**: Synth base sprites carry `altSprites` to the `DigilegSynthliz*` markings; the
  Coyote leg-displacement system (`LegDisplacementPrototype`, `HumanoidAppearanceComponent.LegDisplacements`,
  `_CS/LegDisplacement.yml`) is ported and wired into `ClientClothingSystem` so jumpsuit/shoes/
  outerclothing get digileg displacement maps. Leg style is chosen in the marking picker.
- **Any look**: kinds `BasicHumanlike`/`BasicFurry`/`BasicRobot`/`VoxLike` plus all marking layers
  and unlimited points, so species-adaptor markings can replace the base robot parts. All
  species-restricted markings now carry `kindAllowance` (Coyote behavior), so any species can wear
  them.
- Sexes: Male/Female/Unsexed.
- Old exports that reference the removed `IPC` species are automatically imported as Synth
  (`SharedHumanoidAppearanceSystem.FromStream` fallback).

Dragging: `PullingSystem.OnRefreshMovespeed` was patched so a pulled entity heavier than the puller
scales the puller's speed down (`clamp(pullerMass / pulledMass, 0.35, 1)`). A Synth (~178 kg vs ~71 kg
human) slows the puller noticeably; light objects are unaffected.

Differences from old IPCs (by design): no battery/power drain, no radio/encryption holder, no
`Silicon` component (EMP/battery interactions), no silicon guidebook entry, no tongue/ear organ slots
in the body prototype.

## Dependencies

Upstream actions/implants/inventory/equipment visuals, `ContentAudioSystem`/lobby CVar, GameTicker,
`_NF` contractor loadout groups, human body parts/organs and marking system.

## Tests

`ConcealableClothingTest` covers action grant/remove and loadout spawning.
`LobbyBackgroundTest` covers the 30-second rotation server-side.
