# PERSISTENCE

> DB code is server-only (`IServerDbManager`); clients never see EF models. Marking data lives inside
> the profile JSON, not in its own tables.

## Schema areas (`Content.Server.Database/Model.cs`)

| Area | Tables/columns | Notes |
|---|---|---|
| Preferences | `Preference` + `Profile` | Profile includes `Markings` (jsonb), `CharacterConsentFreetext` is **absent here** |
| Admin | `Admin`, `AdminRank`, notes, bans, role bans | `LogType` numeric values are load-bearing |
| Playtime | playtime/role time tracking | Frontier/job requirements |
| Whitelist / Patreon | whitelist, patreon tiers | server config dependent |
| Connection log | connection log entries | |
| Consent | **does not exist in this repo** | would need new tables + migrations |

Markings are stored as a JSON array of strings in `Profile.Markings` (jsonb column). Each string is
`Marking.ToString()`:

```
markingId@#rrggbb,#rrggbb,...                       # legacy format
markingId@#rrggbb,...@1.25,0.1,-0.2                 # Palmtree: scale,offsetX,offsetY
markingId@#rrggbb,...@1.25,0.1,-0.2@g0.5,1          # Palmtree: + per-color glow levels
markingId@#rrggbb,...@m3@cCustom Name               # Palmtree: + toggle flags (1 self, 2 others) and custom name
```

- `Marking.ParseFromDbString` accepts all forms and clamps values.
- The transform segment is omitted when scale/offset are defaults; the glow segment (prefixed `g`)
  is omitted when all glows are zero; the visibility segment (prefixed `m`) is omitted at the
  defaults (`CanToggleVisible=true`, `OtherCanToggleVisible=false`); the custom-name segment
  (prefixed `c`) is omitted when empty. Old data stays valid and defaults to self-toggleable.
- Changing the format requires updating `ToString` and `ParseFromDbString` together.

## Migrations

- Two providers: `Content.Server.Database/Migrations/Sqlite` and `.../Postgres`.
- Adding a model change requires migrations for **both**; the repo's tooling (`add-migration.sh`)
  generates both, but verify.
- No migrations were added by the port (no schema changes were needed).
- Migration IDs/order matter; don't renumber or hand-edit old migrations.
- A drift test compares the model to migrations; keep it green.

## Profile export/import

- Export: `HumanoidAppearanceSystem.ToDataNode(HumanoidCharacterProfile)` → `HumanoidProfileExport`
  YAML (`ForkId`, `Version`, `Profile`).
- Import: `FromStream` reads YAML → `SerializationManager.Read<HumanoidProfileExport>` →
  `profile.EnsureValid(session, collection)`.
- Unknown fields (old Floof marking fields, `height`/`width`, consent blocks) are ignored by the
  serializer.
- Unknown prototypes (traits/markings/loadouts) are filtered out by `EnsureValid`/`EnsureSpecies`,
  not errors. This is why the port had to ensure ids existed rather than relying on import failure.

## Runtime files

| Path | Contents |
|---|---|
| `data/preferences.db` | SQLite DB (default) |
| `data/replays/` | replay files |
| `data/` saved maps | engine `savegrid`/`savemap` output (never `Resources/`) |
| `server_config.toml` | engine server config |
| `client_config.toml` | client config |

## File-based persistence

- Changelogs: `Resources/Changelog/Palmtree.yml` (this fork's entries, first tab; auto-generated
  from `:cl:` blocks) plus the upstream `Changelog.yml`/`Frontier.yml` history; see
  `.ai/guides/changelogs.md`.
- Map migrations: `Resources/migration.yml` + `Resources/nf_migration.yml` applied on map load.
- Prototype ignore list: `Resources/IgnoredPrototypes/ignoredPrototypes.yml`.

## Hazards

- `LogType` values are persisted; never renumber.
- Admin logs can be dropped past `adminlogs.drop_threshold` with no file fallback.
- `database.sqlite_dbpath` default is `preferences.db`; integration tests use in-memory SQLite.
- There is no Postgres integration test in-repo.
