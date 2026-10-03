# UNKNOWN

Questions that could not be answered confidently from the working tree. Treat these as
"verify before relying on" items. Each entry says what is known and what is missing.

## Deployment / operations

1. **Production `server_config.toml` is not in the repo.** Presets, map, auth mode, DB engine,
   admin API token and `nf.server_auth_list` are unknown.
2. **Live server topology.** Whether the Palmtree network is multi-server and how it connects is not
   in-repo.
3. **Postgres in production?** SQLite is the code default; no evidence either way in-repo.
4. **`identifier.sqlite`** (0 bytes, tracked): origin and intended use unknown; referenced nowhere.
5. **`PORTING/`** was the audit folder of the abandoned full-port attempt. Most of it was removed
   from the working tree; only a few `PORTING/*.md` files are tracked on the `port`/`port-wip`
   branches. Whether the maintainer wants anything else preserved is unknown; `.ai/PORTING.md`
   captures the decisions.

## Ported-content behavior

6. **IPC port direction.** The maintainer wants IPCs "ported differently later"; the exact design
   (upstream silicon/borg systems vs EE stack vs a new robot species) is undecided. See
   `.ai/PORTING.md` §6.
7. **Consent.** Whether/when the Floof consent system will be ported is undecided. Until then
   `ModifyUndies` is self-only and no consent-gated systems exist.
8. **Traits.** 46 BodyType/Horny/Scent trait ids from character exports are not ported and are
   silently dropped on import. Whether they will be added later is unknown.
9. **Size system.** Character `height`/`width` are ignored. Whether the size manipulation system
   will be ported is unknown.
10. **Marking visibility persistence.** `Marking.Visible` is session-only; genital markings reset to
    hidden after respawn/relog. Whether this should persist is undecided.
11. **`RenderOverClothing` and directional marking offsets** exist in the old Floof system but were
    not ported (no current content uses them). Whether future content needs them is unknown.
12. **Digitigrade displacement maps.** The old `LegDisplacements` field was documented as crashing
    and was not ported; whether clothing/leg displacement is needed for a good digileg look is
    untested in-game.

## Runtime behavior hidden in the engine

13. **Engine internals not line-traced**: handshake/encryption, PVS budget algorithms, physics
    solver details, map chunk serialization, UI layout/rendering.
14. **Sandbox whitelist coverage**: `RobustToolbox/Robust.Shared/ContentPack/Sandbox.yml` is large;
    whether a specific BCL API is allowed was only checked for the APIs used by this port
    (`string.Create` is blocked; `float.ToString(IFormatProvider)` and `CultureInfo` are allowed).
15. **Engine upgrade risk**: which content code depends on undocumented engine behavior is not fully
    enumerable without an upgrade attempt.

## Content/gameplay reachability

16. **Which upstream content is reachable under live NF presets.** Upstream presets have no rules,
    but individual systems/events can still be attached by admins.
17. **`_PS` weapon balance/reachability.** Ported weapons are defined but not audited for spawn
    placement/vendors; whether they should be obtainable and how is undecided.
18. **Bayou clothing reachability.** The `CoyoteJumpsuit` subgroup was wired into the same job
    groups as the old repo; whether all of them are used on the live server is unknown.
19. **`CoyoteShoes` group**: the old repo had a separate shoes group for `LoadoutFootProtectors`;
    this port instead added the loadout to `ContractorShoes`. Behavior should match, but the group
    layout differs from the old server.
20. **`_Starlight` vs `_StarLight` casing**: two prototype directories exist; whether one is legacy
    is unknown.

## Moderation / ERP

21. **Data protection posture.** No consent freetext is stored; character flavor text exposure
    behavior (if any) is upstream and unaudited.
22. **Censor implementation** (`SimpleCensor`, `RegexCensor`) exists upstream but is not wired to
    live chat paths.

## Repo/tooling

23. **CI gating**: which GitHub workflows run on this branch and whether they match local commands
    is unverified.
24. **`Content.Docfx`** is not in the solution; its output/docs coverage was not assessed.
25. **Map renderer viewer JSON consumers** are external tooling not in this repo.

## Documentation gaps

26. `.ai/` docs prefer symbol names; line numbers will drift. Update docs when architecture shifts.
27. The generic Frontier system docs (`systems/*.md`) are summaries; deep NF internals should be
    verified against `_NF` source when relied upon.
