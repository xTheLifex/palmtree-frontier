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
markingId@#rrggbb,#rrggbb,...            # legacy format
markingId@#rrggbb,#rrggbb,...@1.25,0.1,-0.2   # Palmtree: scale,offsetX,offsetY
```

- `Marking.ParseFromDbString` accepts both forms and clamps values.
- Only non-default transform values are written, keeping old data valid.
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

- Changelogs: `Resources/Changelog/Frontier.yml` (history; the client shows the Frontier tab).
- Map migrations: `Resources/migration.yml` + `Resources/nf_migration.yml` applied on map load.
- Prototype ignore list: `Resources/IgnoredPrototypes/ignoredPrototypes.yml`.

## Hazards

- `LogType` values are persisted; never renumber.
- Admin logs can be dropped past `adminlogs.drop_threshold` with no file fallback.
- `database.sqlite_dbpath` default is `preferences.db`; integration tests use in-memory SQLite.
- There is no Postgres integration test in-repo.
