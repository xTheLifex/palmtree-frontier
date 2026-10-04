# System: Genital Organs

> Palmtree's organ-based replacement for genital markings (2026-10). Players pick type + size per
> organ in character creation; interactions and cum/drip mechanics read organs, and the marking
> system is reused purely to draw them.

## Purpose

- Persisted, player-configured genital organs: penis, vagina, balls, breasts, butt, belly.
- Drives the interaction panel requirements (`GenitalSystem`), organ arousal visuals and semen
  volume.
- Replaces the old genital markings in the character editor; the marking prototypes remain as
  render targets only.

## Locations

| Piece | Path |
|---|---|
| Catalog prototype (`type: genitalOrgan`) | `Content.Shared/_PS/Organs/GenitalOrganPrototype.cs` |
| Catalog YAML (generated) | `Resources/Prototypes/_PS/Organs/genital_organs.yml` |
| Runtime component | `Content.Shared/_PS/Organs/GenitalOrganComponent.cs` |
| Spawn/render/sync system | `Content.Shared/_PS/Organs/GenitalOrganSystem.cs` |
| Profile data | `Content.Shared/_PS/Organs/GenitalOrganSettings.cs` |
| Profile field | `Content.Shared/Preferences/HumanoidCharacterProfile.cs` (`Genitals`) |
| Organ entities | `Resources/Prototypes/_PS/Entities/Organs/genital_organs.yml` |
| Editor tab | `Content.Client/Lobby/UI/HumanoidProfileEditor.xaml.cs` (`RefreshGenitals`, `BuildGenitalControls`, `OnGenitalControlsChanged`) |
| Catalog generator | `Tools/gen_genital_organ_catalog.py` |
| DB | `Profile.Genitals` string column (`Content.Server.Database/Model.cs`), migrations `*_GenitalOrgans`, `ServerDbBase.ConvertProfiles` |
| Tests | `Content.IntegrationTests/Tests/_PS/InteractionPanelTest.cs`, `ServerDbSqliteTests` round-trip |

## Data model

`GenitalOrganSettings` (profile + network): one `GenitalOrganData { GenitalType Type; string
Prototype; int Size; GenitalVisibility Visibility; Color? Color; Color? DetailColor }` per organ (at
most one per type) plus `SemenVolume` (1-200, default 30). Persisted as a compact string via
`ToDbString`/`FromDbString` in a plain text column, e.g.
`Penis:PenisHuman:3:NeverHidden:#f2c9a0:-;Vagina:VaginaHuman:1:HiddenByJumpsuit;semen=42` (older
3/4-part entries parse as `HiddenByJumpsuit`/skin colors; `-` means "follow skin").

`GenitalOrganPrototype` (catalog): `genitalType`, localized `name`, and an ordered `sizes` list.
Each size maps to a `flaccid` render marking and an optional `aroused` one. The catalog is
generated from existing marking ids by `Tools/gen_genital_organ_catalog.py`, e.g. `PenisHuman`
sizes 1-5 map to `Genital-Penis-Human-N-0` / `-N-1`, `BreastsCoyote` maps to
`GenitalBreastsRoundA..Impossible`, `BreastsSplurt` to `PSGenitalBreasts0..17`.

The organ entity (`OrganGenital*`) carries `Organ` + `GenitalOrganComponent` and is inserted into a
custom slot on the torso (`genital_penis`, `genital_vagina`, ...). `GenitalOrganSystem` creates the
slot at runtime with `TryCreateOrganSlot` (organs in unregistered slots are invisible to body
queries).

## Flow

1. The editor's **Genitals** tab builds dropdowns from the catalog and stores choices in
   `HumanoidCharacterProfile.Genitals` via `WithGenitals`. `EnsureValid` validates prototypes and
   sizes and clamps semen volume.
2. `SharedHumanoidAppearanceSystem.LoadProfile` raises `HumanoidProfileAppliedEvent` after applying
   the profile; `GenitalOrganSystem` reacts and syncs organs. The client lobby preview override does
   not call the shared implementation, so it calls `GenitalOrganSystem.SyncFromProfile` directly and
   refreshes the sprite.
3. `SyncFromProfile` adds/updates/removes organ entities to match the profile, copies `SemenVolume`
   onto the penis organ, then `SyncRender`.
4. `SyncRender` clears the `Genital` marking category and re-adds one render marking per organ
   **that is currently exposed** (the visibility rule gates rendering directly; covered organs are
   not drawn at all, so oversized sprites cannot poke out of clothing). Markings use
   `Forced = true`, `Visible = true`, `CanToggleVisible/OtherCanToggleVisible = false` (so
   ModifyUndies adds no verbs) and colors built from the organ's primary/detail colors (skin color
   when null), following the marking's `colorLinks` so linked states share a color group.
   `NeverHidden` additionally sets `Marking.RenderOverClothing` to draw above outer clothing.
5. The interaction panel's genital tab lists organs, toggles `Aroused` per organ
   (`InteractionGenitalArouseMessage`) and changes the visibility rule
   (`InteractionGenitalSetVisibilityMessage`); arousal swaps the render marking and visibility
   controls both exposure and whether/how the sprite draws.
6. `GenitalSystem` (interactions) answers `HasGenital`/`GetExposure` with the per-organ
   **visibility rule** (SPLURT/Sandstorm port), and `GenitalOrganSystem` re-renders on
   `DidEquipEvent`/`DidUnequipEvent` (jumpsuit) and `MarkingVisibilityChangedEvent` (undies
   toggled), so the rule stays in sync with clothing:

   | Rule | Exposed when | Sprite |
   |---|---|---|
   | Always hidden | never | not drawn |
   | Hidden by underwear | no visible relevant undergarment marking and no jumpsuit | under clothing/undergarments |
   | Hidden by jumpsuit (default) | no jumpsuit | under clothing |
   | Never hidden | always | drawn above outer clothing (`Marking.RenderOverClothing`) |

   Undergarments are the fork's undergarment markings (`UndergarmentTop` for breasts,
   `UndergarmentBottom` for the rest). The anus is still simulated (no organ): humanoids have one,
   exposed when bottomless. The `Genital` sprite layer sits below `UndergarmentBottom` (base species
   maps) so bottom underwear occludes bottom organs.

## Cum, drip and washing

| Piece | Path | Notes |
|---|---|---|
| Reagent | `Resources/Prototypes/_PS/Reagents/semen.yml` | `Semen`, white, viscous; no longer used for puddles/overlays (kept for future fluid work) |
| Drip | `Content.Shared/_PS/Organs/DrippingCumComponent.cs`, `Content.Server/_PS/Organs/DrippingCumSystem.cs` | stores units; 1 u/s into **droplet** decals (`SemenDrip1..5` only), pausing while a jumpsuit is worn |
| Ejaculation decals | `DrippingCumSystem.SpawnCumDecals` | drops `SemenPuddle1..4` scaled by `SemenVolume`; each climax adds a new decal (never replaces) |
| Decal sprites/prototypes | `Resources/Textures/_PS/Effects/decals/semen.rsi` (ported from SPLURT `effects.dmi`), `Resources/Prototypes/_PS/Decals/semen.yml` | cleanable with space cleaner; no reagent puddles |
| Washing | — | decals are removed by space cleaner (`CleanDecalsReaction`); the character overlay feature was removed |

- Internal climaxes (`cumTarget: vagina`/`anus`) store the penis organ's `SemenVolume` in the
  partner's drip reservoir; `vagina` and `anus` both drip (receiver with no jumpsuit). Drips only
  ever place the small droplet decals (`SemenDrip1..5`); the full cum decals are reserved for actual
  ejaculation. The receiver gets a private popup ("trickle" or "pool, held back by your clothes"
  while a jumpsuit is worn).
- Receiver-side acts (`Ride`, `TakeAnal`) route the **target's** climax through the same cum target,
  so the penis owner finishing fills the actor.
- Exterior climaxes (handjob, breastfuck, frotting, **Cum on them**, manual Climax with no recent
  interaction) drop the full cum decals at the recipient. **Cum on them** is a `forceClimax`
  exterior action with no orgasm gate; the **Climax** action triggers immediately with no range or
  orgasm requirement, on yourself or a partner. Both use the same `TriggerClimax`/`EmitClimax` path.
- Each interaction can define `cumMessages` for a context-sensitive finish line (e.g. breastfuck
  "cums all over {target}'s breasts"); the generic per-target text is the fallback. The generic
  Climax reuses the last interaction's proto/target within `continueTimeout`, so the text and the
  destination both follow what you were doing.
- Decals are placed at the mob's actual position (not snapped to the tile) with a small random
  scatter, and every drop is a new decal, so the floor builds up a mess. If the scattered position
  lands on a space tile (standing on an edge/corner) placement falls back to the mob's exact
  position. Note `GetDecalsInRange` compares against `coordinate + (0.5, 0.5)`, so tests should
  query around `position + (0.5, 0.5)`.
- The shower structure does not yet wash mobs (no SS14 shower system); space cleaner removes decals.

## Editor tab

Dynamic `Genitals` tab (like Flavor Text, so upstream tab indices do not change). Per organ type:
enable checkbox, Type dropdown (catalog prototypes), Size dropdown (catalog sizes), Visibility
dropdown (Always hidden / Hidden by underwear / Hidden by jumpsuit / Never hidden), a collapsible
**Colors** section with primary + detail `ColorSelectorSliders` and a "Use skin color" reset (null
colors follow the skin), plus a `Semen per climax` SpinBox. Changing anything calls `WithGenitals`,
marks the profile dirty and reloads the preview.

The MarkingPicker ignores the `Genital` category; profiles with old genital markings are stripped in
`HumanoidCharacterProfile.EnsureValid`.

- Colors: each organ stores `Color`/`DetailColor` (null = skin). The detail color maps to the second
  independent color group, which is the nipple layer for the Splurt breast set; the editor only
  shows the detail picker when the selected marking actually has a second group (Coyote breasts
  don't). `GenitalOrganSystem.CountColorGroups` resolves `colorLinks`.
- Preview: the Genitals tab has a **Show undergarments in preview** checkbox that toggles
  undergarment marking visibility on the preview dummy only (not saved), so organs can be inspected.
  The tab is wrapped in a `ScrollContainer` (the six organ sections overflow the window). Local
  visibility toggles call `HumanoidAppearanceSystem.RefreshSprite` because the event bus only allows
  one `MarkingVisibilityChangedEvent` subscriber (owned by `GenitalOrganSystem`).

## Hazards

- The profile event is raised during `ComponentInit`; `SyncFromProfile` must check
  `BodyComponent.RootContainer` is non-null or it NREs before MapInit.
- Profiles applied before the body exists (admin respawns, deferred MapInit) are stored in
  `GenitalOrganSystem._pendingProfiles` and retried each tick; without this the character spawns
  without genitals and never recovers. A successful sync must remove the pending entry — the mob
  receives a default genital-less profile during `ComponentInit`, and a stale pending entry would
  otherwise re-sync over the real profile on the next tick and erase the organs.
- `GenitalOrganSystem.SyncRender` must set `CanToggleVisible = false` on render markings or
  ModifyUndies will expose them as toggle verbs.
- The client `HumanoidAppearanceSystem.LoadProfile` override does not call the shared method; any
  new profile-driven visual must be applied there too (see the preview call).
- Editing the catalog by hand will be overwritten; run the generator instead.
- Migration drift: `ServerDbSqliteTests.TestNoPendingDatabaseChanges` must stay green for both
  providers after model changes.

## Not ported / future

- Pregnancy, womb, fluids other than semen, cumflation, refractory period.
- Character cum overlays (tried and removed; the sprite layer never showed for the reporter).
  Objects/furniture overlays are not planned either.
- Per-organ fluid type selection (SPLURT has `custom_genital_fluids`); currently one `Semen` reagent.
- Real organ surgery/removal wiring; organs can be removed with body API calls but have no
  interactions with surgery steps.
