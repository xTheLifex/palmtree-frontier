# System: Core Gameplay (Summary)

> Upstream SS14 systems that the fork builds on. Only the parts most relevant to this fork are called
> out; verify behavior in the engine/upstream source.

## Interactions and verbs

- `InteractionSystem` + `GetVerbsEvent<T>` power context menus and interaction verbs.
- `ModifyUndies` uses `GetVerbsEvent<Verb>` (per-marking toggle opt-in) — see `systems/marking-and-appearance.md`.
- `SharedHandsSystem` handles pickup/drop; `ItemToggle`, `UseDelay`, `DoAfter` gate actions.

## Body, damage and health

- `BodySystem`/`BodyPartComponent` model limbs/organs; `DamageableSystem` applies damage via
  `damageModifierSet` prototypes.
- `MobStateSystem` (Alive/Critical/Dead), `StaminaSystem`, `BloodstreamSystem`, `RespiratorSystem`.
- Ported species supply body prototypes and damage sets (Rodentia/Tajaran/Anthromorph).

## Atmos, power, chemistry, construction

- Atmos: pipe networks, vents/scrubbers, gas mixtures; `_NF/Atmos` adds Frontier variants.
- Power: `PowerNet` + `Pow3r` debug tool; solars, generators, batteries, `_NF/Power`.
- Chemistry: reagent prototypes + reactions; metabolism in `MetabolismSystem`.
- Construction: construction graphs + `ConstructionSystem`; `_NF/Construction` adds Frontier
  recipes. Lathe recipes under `Resources/Prototypes/Recipes`.

## Shuttles

- `ShuttleSystem` (+ partials FTL/Impact/Docking), `ShuttleConsoleSystem`, `_NF/Shuttles`,
  `_NF/Shipyard` for purchase/drydock.
- Map format is 7; grids are separate entities; FTL uses `FTLDestinationComponent` prototypes.

## NPCs

- `NPCSystem` + HTN tasks/operators; `_NF/NPC` adds Frontier NPCs for expeditions/pirates.
- `RandomHumanoidSpawner` markers self-delete on MapInit; for preset looks use static mobs with
  `HumanoidAppearance.initial`.

## Radio/speech/chat

- Chat/radio/speech systems are upstream; no consent/censor contributors are registered here.
- Ported species add speech sounds/verbs/typing indicators (Rodentia, Tajaran).

## Lighting/storage/misc

- Standard upstream systems; `_PS` strobe lighting was not ported.
- Storage/containers are upstream; concealable clothing manipulates equipment visuals, not storage.

## Hazards

- Engine/API drift is the main risk (see `.ai/HAZARDS.md`); verify against the pinned engine.
