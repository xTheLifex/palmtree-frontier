# Character Editor Modernization (2026-11)

**Status**: Partially implemented (2026-11) on `ps/character-editor-modernization`: phases 1-2
(editor extraction + Body tab redesign) and most of the shell wave are in the tree. The
MarkingPicker and final polish waves remain.
**Scope**: UI-only cleanup of the lobby character editor, with the **Body** (genital organ) tab as
the primary target: replace the six always-expanded organ blocks with a single-organ editor, group
related controls, explain the visibility rules, and remove duplicated slider/spin-box plumbing.
The second wave tidies the editor shell (save header, preview controls, tab titles) and the
MarkingPicker.
**Touched areas**: `Content.Client/Lobby/UI/HumanoidProfileEditor.xaml(.cs)`, new
`Content.Client/_PS/Organs/UI/*` controls, `Content.Client/Humanoid/MarkingPicker.*` (wave 2),
`Resources/Locale/en-US/_PS/organs/editor.ftl`, possibly a `_PS` `SliderSpinBox` control. No
`Content.Shared`, server, DB, prototype or rendering changes.

## Why the old Genitals tab felt cluttered

Anchors: `HumanoidProfileEditor.BuildGenitalControls`, `RefreshGenitals`,
`UpdateGenitalControls`, `OnGenitalControlsChanged`, `GenitalControls`.

1. **Everything is expanded at once.** The tab stacks the global fluid row, three preview rows
   and six organ sections (Penis, Balls, Vagina, Breasts, Butt, Belly). Finding "breasts"
   means scrolling past five unrelated organs.
2. **The per-organ row is too wide.** Each organ renders
   `CheckBox + "Type:" + OptionButton + "Size:" + OptionButton + "Visibility:" + OptionButton`
   inline. The three dropdowns alone carry 140/120/150 px minimum widths, plus labels and the
   checkbox. The tab's `ScrollContainer` has `HScrollEnabled = false`, so on a normal window the
   row is clipped rather than wrapped.
3. **The same scaffolding is copied six times.** Five hand-rolled slider↔spin-box pairs in the
   Genitals tab alone (glow, detail glow, scale, offset X, offset Y), plus the marking picker's
   equivalents, make the code noisy and easy to break.
4. **State is hidden.** There is no overview of which organs are configured, no selected-organ
   concept, and no summary such as "Penis · Human · Size 3 · Hidden by underwear". A disabled
   organ still occupies a full block with greyed controls.
5. **The visibility rule is jargon.** "Hidden by underwear", "Hidden by jumpsuit",
   "Visible under clothes" and "Never hidden" are not self-explanatory, and there is no help text
   or "what happens right now" feedback.
6. **Preview and configuration are mixed.** "Show clothing", "Show undergarments" and
   "Preview aroused" sit above the organ list as three full-width rows, although they are
   preview-only helpers. The duplicated "Show clothing" logic keeps two buttons in sync by hand.
7. **Unsafe editing.** There is no per-organ reset, no reset-all, and the "Use skin color" button
   does not show whether it is active. The fluid field lives at the top, far away from the organs
   it belongs to, and it also affects female climaxes, so placing it under "Penis" would be wrong.
8. **Ordering is enum order, not anatomy.** `GenitalType` is
   `Penis, Balls, Vagina, Breasts, Anus, Butt, Belly`; `Anus` has no catalog and is skipped, and
   the remaining order reads like an implementation detail.
9. **Maintainability.** Roughly 800 of the editor's ~3,000 lines exist only to build and
   synchronize this tab, all in a code-behind that also owns jobs, traits, markings, species and
   preview logic.

The same audit applies more mildly to the rest of the editor: `Appearance` is one long scroll
mixing identity, body size, skin, hair and eyes; Save/Reset/Import/Export occupy a vertical
column; and tab titles are assigned by index (`TabContainer.SetTabTitle(0, ...)`), which is
fragile when Frontier or Palmtree adds a tab.

## Design principles

1. **One organ at a time.** A compact selector chooses the organ; only that organ's detail is
   shown.
2. **State at a glance.** The selector marks enabled organs, and the detail header summarizes
   the current type/size/visibility.
3. **Explain in place.** Every visibility option gets a one-line description; advanced controls
   are grouped, not scattered.
4. **Safe editing.** "Use skin color" becomes a real toggle; every organ gets Reset, and the tab
   gets Reset all.
5. **Preview is a tool, not configuration.** Preview helpers become compact toggles with
   tooltips and a "preview only" cue.
6. **Reuse, don't copy.** One `SliderSpinBox` control replaces the eight sync blocks; the organ
   editor moves out of `HumanoidProfileEditor` into its own control.
7. **No data model churn.** The profile, DB string, organ catalog, visibility enum and render
   system stay untouched. This is a UI refactor first.

## Proposed Body tab (implemented in phase 2)

### Layout

```
┌ Genitals ───────────────────────────────────────────────────────────────────┐
│ Preview: [Clothes] [Undergarments] [Aroused]          (preview only)        │
│                                                                             │
│ Organs:  [● Penis] [● Vagina] [○ Balls] [○ Breasts] [○ Butt] [○ Belly]      │
│                                                                             │
│ ┌ Penis ─────────────────────────────────────────────── [x] Enabled ──────┐  │
│ │ Type  [Human v]        Size  [Size 3 v]        Visibility [Covered by ... v] │
│ │ Only appears when no jumpsuit or matching undergarment is worn.          │  │
│ │                                                                          │  │
│ │ Appearance                                                               │  │
│ │ [x] Use skin color          Color  [ color picker ]                      │  │
│ │ Skin tone [ ]               Glow   [ slider ] [ 0 ]%                     │  │
│ │                                                                          │  │
│ │ Transform                                                                │  │
│ │ Scale    [ slider ] [100]%     Offset X [ slider ] [ 0 ]                 │  │
│ │ Offset Y [ slider ] [ 0 ]      [Reset organ]                             │  │
│ └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│ Climax fluid  [ 30 ]   (applies to all climaxes)                            │
│                                                              [Reset all]    │
└─────────────────────────────────────────────────────────────────────────────┘
```

The exact widget widths are flexible, but the structure is not: **selector strip → one detail
card → global fluid card**. Two-pane (list + detail) is rejected because the editor already
shares horizontal space with the 325 px character list and the sprite preview; a horizontal
selector fits without squeezing the color pickers.

### Organ selector

- One toggle button per `GenitalType` that actually has a catalog (currently Penis, Balls,
  Vagina, Breasts, Butt, Belly; `Anus` stays absent until it has art).
- Buttons use a `ButtonGroup`; the selected organ is the only visible detail panel.
- Enabled organs show a filled dot/check; disabled organs show a hollow dot and "Not set" in
  their tooltip. The button itself is the add affordance: selecting a disabled organ opens the
  detail with the Enable checkbox in focus.
- Default selection when the tab opens: the first enabled organ, otherwise Penis (or the first
  available type).
- Enum order is replaced with an anatomical/frequency order: Penis, Vagina, Balls, Breasts,
  Butt, Belly. Keep `GenitalType` untouched and sort in the UI.
- Short labels only ("Penis", "Vagina", ...). The current type/name goes in the detail header.

### Detail card

Only the selected organ's controls exist visually (the panels may be cached and toggled with
`Visible` to preserve widget state). The card has three groups plus a footer:

1. **Basics** — Enable checkbox in the header; Type and Size side by side; Visibility dropdown
   with a help label underneath that changes with the selection (table below). When the organ is
   disabled, the body is disabled/dimmed but still browsable, so players can pre-configure before
   turning it on.
2. **Appearance** — "Use skin color" checkbox first. When checked, the primary/detail color
   pickers are disabled and show the skin color; unchecking seeds the current skin color so the
   pickers do not jump. Detail color (and its glow) is only shown when the selected size's
   marking actually has a second color group, using `GenitalOrganSystem.CountColorGroups` as
   today. The detail label stays dynamic ("Nipple color" for breasts). "Skin tone" appears only
   for catalogs with skin-shaded variants (`CanSkinTone`), next to the color controls.
3. **Transform** — one `SliderSpinBox` per value: Scale X (25-300%) with a small lock button
   that reveals a separate Scale Y, Offset X (-100..100), Offset Y (-100..100), plus a single
   "Reset organ" button that restores type-default size 1, default visibility, skin colors,
   uniform scale 1, zero offset, zero glow and skin tone off.
4. **Footer** — "Remove organ" (same as unchecking Enable, but explicit) and maybe "Reset all"
   at tab level. No confirmation popup for one organ; Reset all asks once.

Global card at the bottom: **Climax fluid** (`SemenVolume`, 1–500) with a short hint that it
applies to all climaxes, not just penis owners.

### Visibility copy

The enum and DB values do not change. Only the editor labels/help text do; the interaction panel
uses the same base strings, so either share the new copy or add editor-specific keys.

| Enum | Proposed label | Help text |
|---|---|---|
| `AlwaysHidden` | Never shown | Hidden at all times. Interactions still know the organ exists. |
| `HiddenByUnderwear` | Covered by underwear | Shown only when no jumpsuit and no matching undergarment are worn. |
| `HiddenByJumpsuit` | Covered by jumpsuit | Shown unless a jumpsuit is worn; undergarments do not hide it. |
| `VisibleUnderClothes` | Drawn under clothing | Always active, but clothing draws over it normally. |
| `NeverHidden` | Always drawn on top | Always active and drawn above outer clothing. |

Optional polish: a live status line under the dropdown — "Now: covered by jumpsuit" / "Now:
visible" — computed from the preview dummy with `GenitalOrganSystem.IsExposed`. This is
preview-only and must not be persisted.

### Behaviour contract

- Enable seeds a new organ from the selected type's first catalog/size 1/default visibility.
- Disable removes the organ entry, matching `Setting.Set(type, null)` today.
- Changing Type rebuilds the size list and **clamps** the current size instead of resetting to 1,
  unless the new catalog is shorter.
- Changing Size re-evaluates detail color and skin-tone availability.
- Every user edit still ends in `Profile.WithGenitals`, `SetDirty()` and
  `ReloadProfilePreview()`.
- Preview helpers keep their current semantics: `_showUndergarments` flips undergarment marking
  visibility on the dummy only; `_showAroused` calls `GenitalOrganSystem.SetAroused`; both must
  call `HumanoidAppearanceSystem.RefreshSprite` because the event bus has a single
  `MarkingVisibilityChangedEvent` subscriber (see `.ai/systems/genital-organs.md` hazards).
- The NSFW gate still decides whether the tab exists. Removal must capture the actual page
  control instead of assuming "last tab" (Flavor Text and Genitals are both dynamic).
- No new permission or persistence behaviour.

## Code architecture

| Piece | Responsibility |
|---|---|
| `Content.Client/_PS/Organs/UI/GenitalEditorControl.xaml(.cs)` | Selector strip, detail host, global fluid, Reset all. Public `SetSettings(GenitalOrganSettings, HumanoidCharacterProfile)` and `event Action<GenitalOrganSettings> SettingsChanged`. |
| `Content.Client/_PS/Organs/UI/GenitalOrganPanel.xaml(.cs)` | One organ's card: basics, appearance, transform, reset. Emits `Changed`; owns its suppression flag. |
| `Content.Client/_PS/UserInterface/Controls/SliderSpinBox.cs` | Reusable slider + spin box pair with min/max/step/format and one `ValueChanged` event; used by the organ panels and later by `MarkingPicker` glow/transform. |
| `HumanoidProfileEditor` | Keeps only tab lifecycle, NSFW gating, preview helpers and `WithGenitals`/dirty wiring. |
| Locale | New keys in `Resources/Locale/en-US/_PS/organs/editor.ftl`; no hardcoded strings. |

Implementation notes:

- Resolve catalogs once per profile refresh, group by `GenitalType`, and sort in UI order.
- Keep one panel instance per type and switch `Visible`; this avoids rebuilding six cards on
  every profile reload and preserves focus/scroll state.
- Use a single `_suppress` guard in `GenitalEditorControl`; do not let each child own a different
  `_updating...` flag. A programmatic `SetSettings` should raise no events.
- Move the widget tree to XAML where possible; the current tab is built entirely in code, which
  makes visual iteration slow. The dynamic show/hide of optional rows can stay in code-behind.
- Do not touch `GenitalOrganSettings`, `GenitalOrganData`, `GenitalOrganSystem`,
  `GenitalOrganPrototype`, the catalog YAML, the DB string or migrations.

## Broader editor cleanup (follow-up waves)

### Wave 1 — shell and preview

- **Header bar**: name + randomize on the left; on the right a small "Unsaved changes" badge,
  primary Save, Reset, and an overflow menu for Import / Export / Export image / Open images.
  This frees the 60 px vertical button column.
- **Preview pane**: keep the 8x sprite, add a zoom slider and keep rotate buttons directly under
  it; move "Show clothing" next to those controls so it is a preview control instead of an
  Appearance-tab checkbox. Give the view a neutral backdrop.
- **Tab titles**: use `TabContainer.SetTabTitle(Control, string)` instead of numeric indices for
  the static pages, so Frontier/Palmtree tab insertion cannot silently retitle the wrong page.
- **Dynamic pages**: keep Flavor Text and Genitals appended last, but store their page controls
  explicitly and remove exactly those.
- **Section headers**: add `StyleClassLabelHeading` headings and separators inside Appearance
  (Identity / Body / Hair & eyes) rather than one undifferentiated scroll.
- Optional (bigger diff): split Appearance into **Identity** (name, species, custom species name,
  voice bark, age, sex, pronouns, spawn priority) and **Appearance** (height, width, skin, hair,
  facial hair, eyes, clothing). This is the cleanest information architecture, but it adds a tab
  and upstream merge surface; decide after the Genitals work lands.

### Wave 2 — MarkingPicker

The Markings tab is the last dense editor surface. It currently uses a category `OptionButton`, two
fixed lists and a color/settings block that appears under them, so selecting a marking changes the
height of the whole tab. The plan keeps the familiar available/applied workflow but makes the
layout stable.

Target layout (left-pane friendly; the character preview stays on the right):

```
┌ Markings ────────────────────────────────────────────────────────────────┐
│ [Chest 3/10] [Head 0/10] [Tail 5/10] ...   [Search................] [x]   │
│ 8/10 markings · 5 points left                                            │
│                                                                          │
│ Available                          Applied                               │
│ [icon] Ahoge                       [1] [icon] Fox tail                   │
│ [icon] ...                         [2] [icon] ...                        │
│ ...                                ...                                   │
│ [Add]                              [▲] [▼] [Remove]                      │
│                                                                          │
│ ┌ Selected: Fox tail (Tail) ──────────────────────────────── [Remove] ─┐ │
│ │ [large icon] Colors                                                   │ │
│ │  Color 1 [picker]  Glow [slider] [0]%                                 │ │
│ │  Color 2 [picker]  Glow [slider] [0]%                                 │ │
│ │ Transform  Scale [slider] [100]%  Offset X [slider] [0]  [Reset]      │ │
│ │ Advanced   [ ] Self toggle  [ ] Others can toggle  Name [.........]   │ │
│ └───────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────┘
```

Concrete work:

1. **Category chips** — replace `CMarkingCategoryButton` with a `GridContainer` of toggle
   buttons (3–4 columns, selected via `ButtonGroup`). Each shows the localized category and its
   applied/limit count (`Tail 5/10`); show only categories that have markings for the current
   species/sex and are not in `IgnoreCategories`. Keep the search field and add a compact
   `Applied x/y · N points left` summary. This removes the hidden dropdown and makes navigation
   one click.
2. **Stable detail card** — move the current `CMarkingColors` block into a dedicated
   `MarkingDetailPanel` below the two lists. It shows the selected marking's name, category,
   larger icon and a forced badge; the lists do not move when it opens. Empty states:
   "Select a marking to edit" and "No markings in this category".
3. **Color groups, not sprites** — show one color picker/glow pair per independent color group
   resolved through `MarkingPrototype.ColorLinks`, instead of one per sprite. The renderer already
   copies the parent color to linked child states, so the current extra pickers are misleading and
   make complex markings huge. Extract the existing root-resolution logic from
   `GenitalOrganSystem` into a shared `MarkingColorGroups` helper and use it in both places.
4. **Transform and settings** — reuse `SliderSpinBox` for scale/offset/glow, then expose
   `Scale`, `Offset X`, `Offset Y` in one compact transform row. Keep the collapsible custom
   name/self-toggle/other-toggle settings, but show a "modified" marker when they differ from
   defaults. Add **Reset marking** (colors, glow, transform and settings) next to Remove.
5. **Exact marking binding** — the current detail panel resolves the selected marking with
   `FindIndexOf(category, prototype.ID)`. Store the actual `Marking` instance/index in the used
   list item metadata so duplicate IDs or rank moves cannot edit the wrong entry.
6. **Lists** — keep the two-list add/remove workflow and rank buttons. Add headers with counts,
   empty states, and keep the selected used marking highlighted after add/rank. Do not add
   drag-and-drop: `ItemList` has no reliable reorder gesture and the rank buttons already work.
7. **Unchanged contracts** — `OnMarkingAdded`/`OnMarkingRemoved`/`OnMarkingColorChange`/
   `OnMarkingRankChange`, `SetData` overloads, `Forced`, `IgnoreSpecies`, `ShowSnoutAboveHair`,
   leg-style events and the `HumanoidMarkingModifierWindow` admin use must all keep working.
   This is UI-only; no profile, DB or marking serialization changes.

Risks/verification: color grouping must match `HumanoidAppearanceSystem`'s `colorLinks` pass
(unit-test the helper); duplicate marking IDs; points at the cap and `Forced` mode; switching
species/sex; search with a selection; import/export; the admin modifier window at its 700 px
minimum; narrow editor widths and 1.5 UI scale.


### Wave 3 — polish

- Tooltips for every non-obvious control (glow, offset, detail color, skin tone).
- Keyboard navigation for the organ selector and lists.
- Narrow-window audit at 1280×720 and 1.5 UI scale; stack paired controls vertically when the
  width is insufficient.
- Verify localization (no hardcoded English), color-blind-safe enabled/disabled indicators (dot +
  text, not color alone), and focus order.

## Non-goals

- No DB schema, DB string or profile serialization changes.
- No changes to organ rendering, clipping, layer order, arousal or interaction logic.
- No new organ prototypes/sizes/content.
- No change to the NSFW age gate.
- No redesign of Jobs, Traits, loadouts or Flavor Text beyond shared shell polish.

## Risks and open questions

- **Width** is the main risk. The editor lives beside the 325 px character list and the sprite
  preview; the detail card must not repeat the current horizontal overflow. Test at 1280×720 and
  1.5 UI scale before locking widget widths.
- **Event loops**: centralize suppression; the current code has per-control `setting...From...`
  flags and an unguarded `enabled.OnToggled` that only works because
  `OnGenitalControlsChanged` early-outs.
- **Preview rebuilds**: every edit reloads the profile onto the dummy; re-apply the selected
  organ and preview toggles after the reload, or the tab will visually reset.
- **Shared visibility strings**: `InteractionWindow` also uses
  `genital-visibility-*`; changing copy affects both. Prefer updating both, or add
  `genital-editor-visibility-*` keys.
- **Dynamic tab removal**: do not keep assuming the Body page is the last child.
- **Resolved**: the tab is renamed **Body**; disabled organs remain browsable and editable, so a
  player can pre-configure one before enabling it.

## Implementation phases

1. **Extract** — done: `GenitalEditorControl` + `GenitalOrganPanel` now own the tab; the old
   `BuildGenitalControls`/`UpdateGenitalControls`/`GenitalControls` block is gone from
   `HumanoidProfileEditor`.
2. **Redesign** — done: selector strip, single detail card, grouped Appearance/Transform, use-skin
   toggle, reset/reset-all, compact preview toggles, global fluid card, visibility help, and
   independent X/Y scale behind the lock button.
3. **SliderSpinBox** — done for the organ panels; applying it to `MarkingPicker` remains.
4. **Shell polish** — partially done: compact header with save state and an expandable
   import/export row, preview zoom and clothing toggle, control-based tab titles, and grouped
   Appearance sections (Identity / Body / Hair & eyes / Preferences). The optional
   Identity/Appearance tab split remains open.
5. **MarkingPicker** layout polish — pending.
6. **Docs and changelog** — `.ai/systems/genital-organs.md` is updated. Add a `:cl:` block for the
   player-visible editor change when committing.

## Manual verification checklist

- NSFW gate on/off: tab appears/disappears, no other tab is removed, preview toggles reset.
- Fresh profile: no organs; enabling each type seeds defaults and renders on the dummy.
- Existing profile with all six organs, custom colors, glow, offsets, scale, skin tone and
  non-default visibility: every value round-trips through save/reload and import/export.
- Disable → enable an organ (regression: slot reuse in `GenitalOrganSystem`).
- Change type/size: size list, detail-color row and skin-tone checkbox update correctly.
- Toggle clothes/undergarments/aroused: sprite updates immediately; none of them dirty the
  profile.
- Rotate all four directions with exposed, underwear-covered and jumpsuit-covered organs.
- Narrow window / high UI scale: no clipped dropdown rows; scrollbars behave.
- Reset organ and Reset all restore defaults without touching unrelated organs.
