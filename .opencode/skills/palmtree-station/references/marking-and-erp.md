# Marking & ERP system reference

Deep dive: `.ai/systems/marking-and-appearance.md`; ERP status: `.ai/systems/consent-and-erp.md`.

## Files

| Piece | Path |
|---|---|
| Data model | `Content.Shared/Humanoid/Markings/Marking.cs` |
| Prototype | `Content.Shared/Humanoid/Markings/MarkingPrototype.cs` |
| Categories/conversions | `Content.Shared/Humanoid/Markings/MarkingCategories.cs` |
| Set/filters | `Content.Shared/Humanoid/Markings/MarkingsSet.cs`, `MarkingManager.cs` |
| Layer enums | `Content.Shared/Humanoid/HumanoidVisualLayers.cs`, `HumanoidLegStyle.cs` |
| Profile/appearance | `Content.Shared/Humanoid/HumanoidCharacterAppearance.cs`, `HumanoidAppearanceComponent.cs` |
| Shared logic | `Content.Shared/Humanoid/SharedHumanoidAppearanceSystem.cs` |
| Client renderer | `Content.Client/Humanoid/HumanoidAppearanceSystem.cs` |
| Editor UI | `Content.Client/Humanoid/MarkingPicker.xaml(.cs)`, `Content.Client/Lobby/UI/HumanoidProfileEditor.xaml.cs` |
| Undies verbs | `Content.Server/_Floof/ModifyUndies/*`, `Content.Shared/_Floof/ModifyUndiesDoAfterEvent.cs` |
| Leg displacement | `Content.Shared/_CS/Humanoid/LegDisplacementPrototype.cs`, `Resources/Prototypes/_CS/LegDisplacement.yml` |
| Content | `Resources/Prototypes/Entities/Mobs/Customization/Markings/*`, `_Floof/.../markings/*`, `_PS/.../genitals.yml`, `_DV`, `_DEN`, `_EE`, `_Starlight`, `_White` |

## Prototype fields

`bodyPart` (layer), `markingCategory`, `speciesRestriction`, **`kindAllowance`**,
`sexRestriction`, `sprites`, `layering` (state → layer), `colorLinks` (child → parent),
`altSprites` (leg style → marking), `hidden`, `baseLayerSprite`, `shader`, coloring fields.

### kindAllowance (critical)

- Allowed if `speciesRestriction` is null/contains species, **or** `kindAllowance` intersects
  `SpeciesPrototype.Kind`.
- This repo adds `kindAllowance: [BasicHumanlike, BasicFurry, BasicRobot, VoxLike]` to all
  species-restricted markings (Coyote behavior). Always include it on new restricted markings.
- Every filter must call `MarkingManager.IsAllowedBySpeciesOrKindAllowance`:
  `MarkingManager.MarkingsByCategoryAndSpecies(AndSex)`, `IsValidMarking`, both `CanBeApplied`,
  and `MarkingsSet.EnsureSpecies`. Direct `SpeciesRestrictions.Contains` silently strips
  kind-shared markings (this bug shipped once).

## Runtime instance data & persistence

`Marking` carries colors, `visible`, `forced`, `scale`, `offsetX/offsetY`, `glowLevels` (+ legacy
`glow`) and the toggle settings `customName`, `canToggleVisible` (default true),
`otherCanToggleVisible` (default false). DB string format
(`Marking.ToString`/`ParseFromDbString`):

```
markingId@#rrggbb,...[@scale,offsetX,offsetY][@g0.5,1,...][@m3][@cCustom Name]
```

Transform segment omitted when default; glow (`g`) omitted when all zero; visibility flags (`m`,
bit 1 = self, bit 2 = others) omitted at defaults; custom name (`c`) omitted when empty (`@` is
sanitized to `_`). Old strings remain valid and default to self-toggleable. Change `ToString` and
`ParseFromDbString` together.

**Profile load**: `SharedHumanoidAppearanceSystem.LoadProfile` must use
`AddMarking(uid, Marking marking, colors, ...)` so scale/offset/glow survive. The
`(string, colors)` overload creates a fresh marking and drops them.

## Rendering (client)

- `UpdateLayers` rebuilds `BaseLayers` from species `speciesBaseSprites`, applying base-sprite
  `altSprites` for the current `LegStyle`.
- `ApplyMarkingSet` swaps markings via `GetMarkingForLegStyle` (marking `altSprites`), then
  `ApplyMarking`:
  - `layering` routes sprites to arbitrary layers; `colorLinks` copies colors (and glow).
  - scale/offset applied per layer; glow creates `<id>-<state>-glow` with `unshaded` shader and
    alpha split; hidden/removed markings clean up glow layers.
  - genital layers clamp below `jumpsuit`/`outerClothing`.
  - `Base*` categories add to `HiddenBaseLayers`; `UpdateLayersAgain` hides them.
- `RemoveMarking` removes both base and `-glow` layers.

## Genital markings & ModifyUndies

- Genital markings: `bodyPart: Genital`, `markingCategory: Genital`, start hidden
  (`AddMarking` sets `Visible = false` for the category), toggled by `ModifyUndies`.
- `ModifyUndiesSystem` adds verbs for **any** marking whose `CanToggleVisible` (owner) or
  `OtherCanToggleVisible` (others) is set. Defaults: owner on, others off. The per-marking opt-in is
  the consent gate for others (no consent system). It starts a 1s do-after, then
  `SharedHumanoidAppearanceSystem.SetMarkingVisibility` flips `Marking.Visible` and dirties. Verb
  and popup text uses `CustomName` when set.
- Visibility is session state, not persisted; toggle settings are persisted in the DB string.

## Digitigrade legs & clothing

- Leg style is chosen in the marking picker; persisted in `HumanoidCharacterAppearance.LegStyle`.
- Base sprites and markings swap via `altSprites` (e.g. Synth → `DigilegSynthliz*`, adaptors →
  `DigilegPaw*`/`DigilegFurry*`).
- Clothing displacement: `LegDisplacementPrototype` (`_CS/LegDisplacement.yml`) +
  `HumanoidAppearanceComponent.LegDisplacements` (default `LegDisplacementDigitigrade`) +
  `ClientClothingSystem.RenderEquipment` → `HumanoidAppearanceSystem.GetDisplacementForLegStyle`,
  gated by `SpeciesPrototype.AllowDigilegDisplacement`. Coverage is partial (same as Coyote).

## Species requirements for markings

1. `kind` list on the species prototype.
2. Layer entries in `speciesBaseSprites` (`Genital`, `TailBehind`, `TailOversuit`, `NeckFluff`,
   behind legs, `Undershirt*`, `TailExtras`, `SnoutCover`, ...).
3. Layer maps in the mob `Sprite` (`Entities/Mobs/Species/base.yml` covers most; custom-sprite
   species need them added).
4. `Genital` marking-points entry.
5. Optional `altSprites` on `humanoidBaseSprite` entries for digitigrade.

## Adding a marking

`.ai/guides/adding-markings.md` + skill `species-and-loadouts.md`. Checklist: RSI states exist,
locale `marking-<id>` / `marking-<id>-<state>`, layer exists in species, kindAllowance present,
lint green.
