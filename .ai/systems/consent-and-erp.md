# System: Consent & ERP-Adjacent Content

> Palmtree is an adult-RP-permitting server (maintainer context), but **this repository currently has
> no consent system**. This document records what actually exists, what does not, and what porting
> consent would involve. Do not describe consent behavior that is not in the code.

## Consent status: not ported

There is **no** `Content.Shared/Consent`, `Content.Server/Consent`, `Content.Client/Consent`,
`Resources/Prototypes/consent.yml`, consent DB tables, or consent CVar in this repository.

Consequences:

- `ModifyUndies` defaults to self-only; per-marking `OtherCanToggleVisible` is the only gate for
  other players (there is no consent system). It defaults to off, including for undergarments and
  genitals, so other players cannot reveal anything unless the owner opted in.
- The interaction panel adds a **session-only** lewd-interaction toggle
  (`InteractionStateComponent.Consent`, default on) that each player controls in the panel's
  Preferences tab; it is intentionally not persisted and is not a consent system (no DB, no
  per-toggle preferences, no "Ask" flow). See `systems/interaction-panel.md`.
- No system gates anything on `GenitalMarkings`, `NSFWDescriptions`, `SizeManipulation`,
  `Transformation`, `Vore`, `Digestion`, etc.
- Character exports still contain `consentToggles`, `accountConsentFreetext` and
  `characterConsentFreetext` (the old Floof schema). The importer ignores these unknown fields; they
  are not stored or enforced.

The old Coyote checkout (see `.ai/file_paths.md`) has the complete Floof consent system; `.ai/PORTING.md` §8 and
`guides/porting-from-coyote.md` describe how to port it if requested. It requires DB migrations
(two tables + a profile column), which the maintainer explicitly deferred.

## ERP features that DO exist here

| Feature | Location | Notes |
|---|---|---|
| Genital markings (core library, butts/bellies, Palmtree breasts) | `Resources/Prototypes/Entities/Mobs/Customization/Markings/genitals.yml`, `butts_and_bellies.yml`, `_PS/.../genitals.yml` | `bodyPart: Genital`, start hidden, render under clothing |
| Show/hide verbs for undergarments (and any marking the player opts in) | `Content.Server/_Floof/ModifyUndies/*` | Per-marking opt-in (`canToggleVisible` default off except undergarments, `otherCanToggleVisible` default off), 1s do-after, `marking-toggle-*` locale |
| Marking visibility state | `SharedHumanoidAppearanceSystem.SetMarkingVisibility` | Flips `Marking.Visible`; session-only |
| Interaction panel (Sandstorm port) | `Content.{Shared,Server,Client}/_PS/Interactions/*`, `Resources/Prototypes/_PS/Interactions/`, `Resources/Audio/_PS/Interactions/` | Verb + Ctrl+Shift-click; kiss/hug/fuck/blowjob etc. gated on a session consent toggle (default on), organ-based genitals (see `systems/genital-organs.md`), lust/moans/climax, semen volume + drip + washable cum overlay; no pregnancy/refractory — see `systems/interaction-panel.md` |
| Genital organs + cum | `Content.{Shared,Server,Client}/_PS/Organs/*`, `Resources/Prototypes/_PS/Organs/`, `Resources/Prototypes/_PS/Reagents/semen.yml`, `Resources/Textures/_PS/Effects/cum.rsi` | Character-creation organ picks (penis/vagina/balls/breasts/butt/belly + semen volume) persisted in the profile; SPLURT cum overlay and Sandstorm-style drip; wash with water/space cleaner |
| Undergarment/over-garment marking library | `undergarments.yml`, `underovergarments.yml` | 218 + 67 markings, Floof sprites |
| Genital-under-clothing ordering | `Content.Client/Humanoid/HumanoidAppearanceSystem.ApplyMarking` | Clamps below `jumpsuit`/`outerClothing` |
| Digitigrade legs / base markings / kind sharing | see `systems/marking-and-appearance.md` | General customization, used heavily by ERP characters |

## ERP-adjacent features NOT ported

| Feature | Old location | Status |
|---|---|---|
| Consent system (UI/DB/toggles) | `Content.{Shared,Server,Client}/Consent`, `consent.yml` | Deferred |
| CustomExamine (NSFW descriptions) | `_Floof/Examine` | Not ported |
| Vore / Digestion | `Content.Server/FloofStation/VoreSystem.cs` | Not ported |
| Scent / sniff | `_CS/SniffAndSmell` | Explicitly excluded |
| Aphrodisiac visibility | `_CS/AphroLacedVisibility`, `ServerAphrodisiacChecker` | Explicitly excluded |
| Horny examine quirks + BodyType traits | `_CS/HornyQuirks`, `body_type_traits.yml`, `Traits.yml` | Not ported (46 trait ids dropped on import) |
| Size manipulation | `_CS/Body/Systems/Size*` | Not ported; character `height`/`width` fields ignored |
| RPI economy | `_CS/RolePlayIncentiveServer` | Not ported |

## Examine pipeline (where ERP text used to land)

Client examine request → server range/occlusion check (`ExamineSystemShared.CanExamine`) →
`ExamineSystemShared.GetExamineText` builds text from metadata + `ExaminedEvent` subscribers →
`ExamineCompletedEvent`. In this repo no ERP contributors are registered (CustomExamine/HornyQuirks/
Scent are absent); the pipeline itself is upstream.

## If you are asked to add consent

Follow `.ai/guides/porting-from-coyote.md`, porting from the old repo:

1. Shared: `PlayerConsentSettings`, `ConsentTogglePrototype`, `SharedConsentSystem`, `MsgUpdateConsent`.
2. Server: `ServerConsentManager`, `ConsentSystem`; register the manager in `ServerContentIoC`.
3. Client: `ClientConsentManager`, `ConsentUiController`, consent window XAML.
4. Prototypes/locale: `consent.yml`, `_Floof` locale.
5. DB: add `ConsentSettings`/`ConsentToggle` tables and `Profile.CharacterConsentFreetext`, then
   regenerate **both** SQLite and Postgres migrations.
6. Re-gate `ModifyUndies` on the target's `GenitalMarkings` consent (the old code does this in
   `ModifyUndiesSystem.AddModifyUndiesVerb`) and replace the interaction panel's session
   `InteractionStateComponent.Consent` check with the consent system
   (`systems/interaction-panel.md`).

## Unknowns

- Whether `NSFWDescriptions`/CustomExamine should come with consent.
- Whether visibility toggles should persist across sessions once consent exists.
