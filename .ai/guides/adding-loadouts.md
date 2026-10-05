# Guide: Adding Loadouts and Their Items

Loadouts are prototypes (`type: loadout`) that grant equipment/implants/storage at spawn. They must be
listed in a `loadoutGroup` (`type: loadoutGroup`), and job loadout prototypes reference groups or
subgroups. This is how the Palmtree implanter, Bayou clothes and clicker were ported.

## 1. The item first

Create the entity prototype in the appropriate `Resources/Prototypes/<prefix>/Entities/...` path,
with sprites and any audio. Example patterns:

- Clicker: `_DEN/Entities/Objects/Fun/clicker.yml` + `_DEN/Objects/Fun/clicker.rsi` +
  `/Audio/_DEN/clicker.ogg`.
- Foot protectors: `_DEN/Entities/Clothing/Shoes/misc.yml` + RSI.
- Bayou uniforms: `_CS/CoyoteBayouClothes/coyote_bayou_unififorms_items.yml` + `_CS/Clothes/cbuniforms.rsi`.

Check referenced parent prototypes exist (`grep -rn "id: ParentId" Resources/Prototypes`).

## 2. The loadout prototype

```yaml
- type: loadout
  id: LoadoutThing
  price: 500            # optional
  equipment:
    shoes: ClothingShoesThing
  # or storage:
  #   back: [ SomeItem ]
  # or implants:
  #   - SomeImplanter
  # optional:
  # hideEffects:
  # - !type:GroupLoadoutEffect
  #   proto: SomeLoadoutEffect
```

Place loadout files under `Resources/Prototypes/<prefix>/Loadouts/...`, mirroring existing folders
(`Jobs/Contractor`, `Generic`, ...). If the file already exists in the target (e.g.
`_NF/Loadouts/Jobs/Contractor/backpack_items.yml`), append the new block rather than overwriting.

## 3. Wire it into a group

Groups live in `*_loadout_groups.yml`. Add the loadout id to the relevant group's `loadouts:` list:

- Backpacks → `ContractorBackpack`
- Backpack items → `ContractorBackpackItems`
- Shoes → `ContractorShoes`
- Implanters → `ContractorImplanter`
- Uniforms → `ContractorJumpsuit` or a subgroup like `CoyoteJumpsuit`

For large thematic sets, create a separate group (as `CoyoteJumpsuit` does) and reference it from
each job group's `subgroups:` list:

```yaml
  subgroups:
  - CoyoteJumpsuit
```

The old repo adds `CoyoteJumpsuit` to the same job groups; port those patches by matching the source
group ids, not by assuming the target file layout is identical (some target groups lack
`subgroups:` entirely — insert the block at the end of the group).

## 4. Locale and names

- Loadout UI names use `loadout-name-<id>` keys when present; item names are usually inline in the
  prototype. Check existing locale before adding keys (duplicates fail loading).
- Group names use `loadout-group-*` keys.

## 5. Verify

```
dotnet build SpaceStation14.sln
dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj
dotnet run --project Content.YAMLLinter -c Debug --no-build
```

Then verify in-game or via an integration test that spawning with the loadout grants the item.
Any `Content.IntegrationTests/Tests/_PS` test shows the `PoolManager` server/client template
(`StationSpawningSystem.SpawnPlayerMob` + `RoleLoadout`).

## 6. Pitfalls

- A loadout not listed in any group is unreachable in the UI.
- Job group files are per-job; adding the loadout to only one group limits it to those jobs.
- `hideEffects`/`GroupLoadoutEffect` prototypes must exist (`ShoesCapableNF` etc.).
- `ProtoId` fields (items, effects, implants) are validated by the YAMLLinter.
