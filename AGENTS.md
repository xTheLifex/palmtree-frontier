# AGENTS.md

This file is a pointer, not a knowledge base. **All AI context for this repository lives in
`.ai/`.** Do not add project knowledge, notes, or conventions here — write them to the relevant
file under `.ai/` instead.

## Start here

1. Read `.ai/README.md` first. It is the navigation guide: project overview, fork prefixes,
   documentation map, dependency boundaries, and the most important hazards.
2. Before changing a feature, read the matching document in `.ai/systems/` and, for content work,
   the matching procedural guide in `.ai/guides/`.
3. Before editing shared or core systems, read `.ai/HAZARDS.md`.
4. Machine-specific checkout paths (Coyote, BYOND, etc.) are in `.ai/file_paths.md`. That file is
   gitignored; on a new machine, create it from its header template.

The complete document map is in `.ai/README.md` §4. Common entry points:

| Task | Read first |
|---|---|
| Any task | `.ai/README.md` |
| Architecture, ECS, round flow | `.ai/ARCHITECTURE.md`, `.ai/API.md`, `.ai/EVENTS.md` |
| Prototypes / YAML / content pipeline | `.ai/guides/prototype-tutorial.md`, `.ai/systems/content-pipeline-and-tooling.md` |
| Porting from Coyote/Floof or SS13 | `.ai/guides/porting-from-coyote.md`, `.ai/guides/porting-from-ss13.md`, `.ai/PORTING.md` |
| Markings, species, ERP/consent | `.ai/systems/marking-and-appearance.md`, `.ai/systems/consent-and-erp.md`, `.ai/guides/adding-markings.md`, `.ai/guides/adding-species.md` |
| Persistence / DB | `.ai/PERSISTENCE.md` |
| Upstream sync | `.ai/guides/upstream-sync.md` |
| Changelog entries | `.ai/guides/changelogs.md` |
| Known traps | `.ai/HAZARDS.md`, `.ai/UNKNOWN.md` |

An operational playbook mirroring this context lives at
`.opencode/skills/palmtree-station/SKILL.md`.

## Always-on rules (details in `.ai/`)

- **Never modify the engine.** `RobustToolbox/` is a frozen submodule; build on its APIs.
- **Respect fork prefixes.** New Palmtree code/assets go under `_PS`; content ported from another
  fork keeps its original prefix (`_NF`, `_Floof`, `_CS`, `_DV`, ...). Preserve upstream patch
  markers (`// Frontier`, `// Coyote`, `// Palmtree`) in files patched in place.
- **Dependencies point one way.** `Content.Shared` must not reference server or client code;
  network messages live in `Content.Shared`; DB access is server-only through `IServerDbManager`.
- **Changelog.** Every player-visible change needs a `:cl:` block in the commit message
  (`:cl:` on its own line, then `- add|remove|tweak|fix: message`). Never write entries into
  upstream changelog files; see `.ai/guides/changelogs.md`.
- **Keep docs current.** When architecture or workflows change, update the affected `.ai/`
  document in the same change. Prefer symbol names over line numbers as anchors.
- **After pushing, stop.** Do not poll or watch CI/changelog workflow runs; that is the
  maintainer's job. Report the push and end the turn.
