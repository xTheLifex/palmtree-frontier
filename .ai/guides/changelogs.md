# Guide: Changelogs

> How changes reach the in-game changelog in this repository.

## TL;DR

The in-game changelog is **client-side YAML only** — `Content.Client/Changelog/ChangelogManager.cs`
reads every direct `Resources/Changelog/*.yml` file and renders one tab per file. Nothing scans git
history. A commit appears in-game only if someone writes an entry into one of those files.

Existing files in this repo:

| File | Tab | Notes |
|---|---|---|
| `Resources/Changelog/Changelog.yml` | upstream | Wizden history, third-party |
| `Resources/Changelog/Frontier.yml` | Frontier | Frontier Station history |
| `Resources/Changelog/Maps.yml` | Maps | mapping changes |
| `Resources/Changelog/Admin.yml` | Admin | `AdminOnly: true` |

There is currently **no Palmtree-specific tab**. If you want one, add
`Resources/Changelog/Palmtree.yml` (`Name: Palmtree`, `Order:` chosen to sort after Frontier) plus a
`changelog-tab-title-Palmtree` locale key, and put future entries there.

## Entry format

```yaml
Entries:
- author: yourname
  changes:
    - type: Add        # Add / Remove / Tweak / Fix
      message: Adds a thing.
    - type: Fix
      message: Fixes another thing.
  id: 1                # unique, incrementing per file
  time: '2026-10-03T15:47:27.0000000+00:00'
```

## How the client reads it

- `ChangelogManager.LoadChangelog()` enumerates `/Changelog/`, keeps direct `*.yml` children
  (`Parts/` is ignored), deserializes `Changelog` (`Name`, `Entries`, `AdminOnly`, `Order`).
- Tabs sort by `Order`; tab title uses `changelog-tab-title-<Name>`.
- The window reloads YAML on every open (`ChangelogWindow.PopulateChangelog`), so a dev client shows
  edits immediately.
- `changelog_last_seen_<ServerId>` in client user data drives the "new changes" badge.

## Validation / automation

- CI validates changelog YAML via `.github/workflows/nf-validate-changelog.yml`.
- `Tools/` contains helper scripts (`dump_commits_since.ps1`, `dump_contributors_since.ps1`,
  `actions_changelogs_since_last_run.py`, ...); adapt them if you add a Palmtree file.
- Never append Palmtree entries to the upstream history files.

## Checklist

- [ ] Entry added to the correct file (create `Palmtree.yml` if desired).
- [ ] Unique `id`, sensible `author`, ISO `time`.
- [ ] `type` is one of Add/Remove/Tweak/Fix.
- [ ] YAML validates (`nf-validate-changelog` workflow or run the validator locally).
