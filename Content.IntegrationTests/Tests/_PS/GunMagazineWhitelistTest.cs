#nullable enable
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: guns must only accept their own magazines - never ammo boxes or magazines from
/// another caliber/weapon. Guards against shallow prototype merges silently dropping an
/// ItemSlots whitelist when a child prototype overrides the slot for sounds or starting items.
/// </summary>
[TestFixture]
public sealed class GunMagazineWhitelistTest
{
    [Test]
    public async Task GunsRejectForeignMagazinesAndAmmoBoxes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var slotsSys = entMan.System<ItemSlotsSystem>();

            // gun, own magazine (nullable), foreign items that must be rejected, has chamber slot
            var cases = new (string Gun, string? Good, string[] Bad, bool Chamber)[]
            {
                ("CSWeaponSubMachineGunP90", "CSMagazine10mmP90", new[] { "CSAmmunitionBox10mm", "CSMagazine9mmUzi" }, true),
                ("CSWeaponRifleAUGA10", "CSMagazine5mm", new[] { "CSAmmunitionBox5mm", "CSMagazine9mmUzi" }, true),
                ("CSWeaponAssaultRifleAKM", "NFMagazineRifle30", new[] { "CSAmmunitionBox308", "CSMagazine5mm" }, true),
                ("NFWeaponRifleAssaultNovaliteC1", "NFMagazineClipRifle20", new[] { "CSAmmunitionBox22", "CSMagazine5mm" }, true),
                ("CSWeaponSubMachineGunUzi", "CSMagazine9mmUzi", new[] { "CSAmmunitionBox9mm", "CSMagazine10mmP90" }, true),
                ("CSWeaponRifleM1Garand", "CSMagazine3006Clip", new[] { "CSAmmunitionBox3006" }, true),
                ("CSWeaponPistolAutomag", "CSMagazine44Automag", new[] { "CSAmmunitionBox44" }, true),
                ("CSWeaponRifleScarL", "CSMagazine5mm", new[] { "CSAmmunitionBox5mm" }, true),
                ("CSWeaponShotgunSaiga12", "CSMagazineSaiga", new[] { "CSAmmunitionBox50AE" }, false),
                ("CSWeaponRifleMosin", "CSMagazine54RClip", new[] { "CSAmmunitionBox54R" }, true),
                ("NFWeaponPistolMk58", null, new[] { "CSAmmunitionBox9mm", "CSMagazine9mmUzi" }, true),
                ("NFWeaponSubMachineGunWt550", null, new[] { "CSAmmunitionBox10mm", "CSMagazine10mmP90" }, true),
            };

            Assert.Multiple(() =>
            {
                foreach (var (gun, good, bad, chamber) in cases)
                {
                    var gunUid = entMan.SpawnEntity(gun, MapCoordinates.Nullspace);

                    if (!slotsSys.TryGetSlot(gunUid, "gun_magazine", out var slot))
                    {
                        Assert.Fail($"{gun} has no gun_magazine slot");
                        continue;
                    }

                    var tags = slot!.Whitelist?.Tags == null
                        ? "<null whitelist>"
                        : string.Join(",", slot.Whitelist.Tags);

                    if (chamber)
                        Assert.That(slotsSys.TryGetSlot(gunUid, "gun_chamber", out _), Is.True,
                            $"{gun} is missing its gun_chamber slot");

                    if (good != null)
                    {
                        var goodUid = entMan.SpawnEntity(good, MapCoordinates.Nullspace);
                        Assert.That(slotsSys.CanInsert(gunUid, goodUid, null, slot, swap: true), Is.True,
                            $"{gun} rejected its own magazine {good} (whitelist: {tags})");
                    }

                    foreach (var badItem in bad)
                    {
                        var badUid = entMan.SpawnEntity(badItem, MapCoordinates.Nullspace);
                        Assert.That(slotsSys.CanInsert(gunUid, badUid, null, slot, swap: true), Is.False,
                            $"{gun} accepted {badItem} as a magazine (whitelist: {tags})");
                    }
                }

                // The marksman crossbow only takes bolts, never ammo boxes.
                var crossbowUid = entMan.SpawnEntity("CSWeaponCrossbowMarksman", MapCoordinates.Nullspace);
                Assert.That(slotsSys.TryGetSlot(crossbowUid, "projectiles", out var boltSlot), Is.True,
                    "marksman crossbow has no projectiles slot");
                if (boltSlot != null)
                {
                    var boltTags = boltSlot.Whitelist?.Tags == null
                        ? "<null whitelist>"
                        : string.Join(",", boltSlot.Whitelist.Tags);
                    var boltUid = entMan.SpawnEntity("CrossbowBolt", MapCoordinates.Nullspace);
                    Assert.That(slotsSys.CanInsert(crossbowUid, boltUid, null, boltSlot, swap: true), Is.True,
                        $"marksman crossbow rejected its bolt (whitelist: {boltTags})");
                    var boxUid = entMan.SpawnEntity("CSAmmunitionBox9mm", MapCoordinates.Nullspace);
                    Assert.That(slotsSys.CanInsert(crossbowUid, boxUid, null, boltSlot, swap: true), Is.False,
                        $"marksman crossbow accepted an ammo box as a bolt (whitelist: {boltTags})");
                }

                // The Hristov's internal magazine must keep its parent's capacity (5) and ammo.
                var hristovUid = entMan.SpawnEntity("NFWeaponRifleSniperHristov", MapCoordinates.Nullspace);
                var ammoEv = new GetAmmoCountEvent();
                entMan.EventBus.RaiseLocalEvent(hristovUid, ref ammoEv);
                Assert.That(ammoEv.Count, Is.EqualTo(5),
                    $"Hristov internal magazine spawned {ammoEv.Count} rounds (expected 5) - the provider override dropped the parent's data");
                Assert.That(ammoEv.Capacity, Is.EqualTo(5),
                    $"Hristov internal magazine capacity is {ammoEv.Capacity} (expected 5)");
            });
        });
    }
}
