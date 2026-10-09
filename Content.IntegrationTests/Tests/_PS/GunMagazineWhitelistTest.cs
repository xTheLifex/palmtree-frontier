#nullable enable
using System.Collections.Generic;
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
            var spawned = new List<EntityUid>();

            EntityUid Spawn(string id)
            {
                var uid = entMan.SpawnEntity(id, MapCoordinates.Nullspace);
                spawned.Add(uid);
                return uid;
            }

            // gun, own magazine (nullable), foreign items that must be rejected, has chamber slot
            var cases = new (string Gun, string? Good, string[] Bad, bool Chamber)[]
            {
                ("CSWeaponSubMachineGunP90", "CSMagazine10mmP90", new[] { "CSAmmunitionBox10mm", "CSMagazine9mmUzi" }, true),
                ("CSWeaponRifleAUGA10", "CSMagazine5mm", new[] { "CSAmmunitionBox5mm", "CSMagazine9mmUzi" }, true),
                ("CSWeaponAssaultRifleAKM", "NFMagazineRifle30", new[] { "CSAmmunitionBox308", "CSMagazine5mm" }, true),
                ("CSWeaponLightMachineGunBAR", "CSMagazine308", new[] { "CSMagazine308Sks", "CSMagazine308Rpd", "CSMagazine308Belt", "CSAmmunitionBox308" }, true),
                ("CSWeaponLightMachineGunM1919", "CSMagazine308Belt", new[] { "CSMagazine308", "CSMagazine308Ext", "CSMagazine308Rpd", "CSMagazine308Sks", "CSAmmunitionBox308" }, true),
                ("CSWeaponLightMachineGunRPD", "CSMagazine308Rpd", new[] { "CSMagazine308Sks", "CSMagazine308Belt", "CSMagazine308", "CSMagazine308Lewis" }, true),
                ("CSWeaponLightMachineGunDP27", "CSMagazine308Lewis", new[] { "CSMagazine308Sks", "CSMagazine308Belt", "CSMagazine308Rpd", "CSMagazine308" }, true),
                ("CSWeaponRifleSKS", "CSMagazine308Sks", new[] { "CSMagazine308Rpd", "CSMagazine308Belt", "CSMagazine308Lewis", "CSMagazine308" }, true),
                ("NFWeaponRifleAssaultNovaliteC1", "NFMagazineClipRifle20", new[] { "CSAmmunitionBox22", "CSMagazine5mm" }, true),
                ("PSWeaponRifleAssaultNovaliteC2", "NFMagazineRifle20", new[] { "NFAmmunitionBoxRifle20", "CSMagazine5mm" }, true),
                ("CSWeaponSubMachineGunUzi", "CSMagazine9mmUzi", new[] { "CSAmmunitionBox9mm", "CSMagazine10mmP90", "CSMagazine9mmPpsh" }, true),
                ("CSWeaponSubMachineGunMP5", "CSMagazine9mmUzi", new[] { "CSAmmunitionBox9mm", "CSMagazine9mmPpsh" }, true),
                ("CSWeaponSubMachineGunPPSh", "CSMagazine9mmPpsh", new[] { "CSAmmunitionBox9mm", "CSMagazine9mmUzi" }, true),
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
                    var gunUid = Spawn(gun);

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
                        var goodUid = Spawn(good);
                        Assert.That(slotsSys.CanInsert(gunUid, goodUid, null, slot, swap: true), Is.True,
                            $"{gun} rejected its own magazine {good} (whitelist: {tags})");
                    }

                    foreach (var badItem in bad)
                    {
                        var badUid = Spawn(badItem);
                        Assert.That(slotsSys.CanInsert(gunUid, badUid, null, slot, swap: true), Is.False,
                            $"{gun} accepted {badItem} as a magazine (whitelist: {tags})");
                    }
                }

                // The marksman crossbow only takes bolts, never ammo boxes.
                var crossbowUid = Spawn("CSWeaponCrossbowMarksman");
                Assert.That(slotsSys.TryGetSlot(crossbowUid, "projectiles", out var boltSlot), Is.True,
                    "marksman crossbow has no projectiles slot");
                if (boltSlot != null)
                {
                    var boltTags = boltSlot.Whitelist?.Tags == null
                        ? "<null whitelist>"
                        : string.Join(",", boltSlot.Whitelist.Tags);
                    var boltUid = Spawn("CrossbowBolt");
                    Assert.That(slotsSys.CanInsert(crossbowUid, boltUid, null, boltSlot, swap: true), Is.True,
                        $"marksman crossbow rejected its bolt (whitelist: {boltTags})");
                    var boxUid = Spawn("CSAmmunitionBox9mm");
                    Assert.That(slotsSys.CanInsert(crossbowUid, boxUid, null, boltSlot, swap: true), Is.False,
                        $"marksman crossbow accepted an ammo box as a bolt (whitelist: {boltTags})");
                }

                // The Hristov's internal magazine must keep its parent's capacity (5) and ammo.
                var hristovUid = Spawn("NFWeaponRifleSniperHristov");
                var ammoEv = new GetAmmoCountEvent();
                entMan.EventBus.RaiseLocalEvent(hristovUid, ref ammoEv);
                Assert.That(ammoEv.Count, Is.EqualTo(5),
                    $"Hristov internal magazine spawned {ammoEv.Count} rounds (expected 5) - the provider override dropped the parent's data");
                Assert.That(ammoEv.Capacity, Is.EqualTo(5),
                    $"Hristov internal magazine capacity is {ammoEv.Capacity} (expected 5)");

                foreach (var uid in spawned)
                {
                    entMan.DeleteEntity(uid);
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
