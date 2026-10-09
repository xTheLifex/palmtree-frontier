#nullable enable
using System.Collections.Generic;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: guards the ammo feeding of the ported Fallout guns - chambered rounds must fire real
/// projectiles (never dud shells from commented-out Frontier prototypes) and spent clips must leave
/// the gun when they should.
/// </summary>
[TestFixture]
public sealed class GunAmmoTest
{
    private static (int InitialCount, int InitialCapacity, int Shots, int Fired) Drain(EntityUid gun, IEntityManager entMan)
    {
        var ammoCount = new GetAmmoCountEvent();
        entMan.EventBus.RaiseLocalEvent(gun, ref ammoCount);

        var shots = 0;
        var fired = 0;
        for (var i = 0; i < 500; i++)
        {
            var ammo = new List<(EntityUid?, IShootable)>();
            var take = new TakeAmmoEvent(1, ammo, entMan.GetComponent<TransformComponent>(gun).Coordinates, null, true);
            entMan.EventBus.RaiseLocalEvent(gun, take);
            if (ammo.Count == 0)
                break;

            shots++;
            fired += ammo.Count;
        }

        return (ammoCount.Count, ammoCount.Capacity, shots, fired);
    }

    [Test]
    public async Task GunsFireAllTheirAmmo()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var slots = entMan.System<ItemSlotsSystem>();
            var gunSystem = server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();

            // Taurus Judge: three chambered shells, each firing a real projectile.
            var judge = entMan.SpawnEntity("CSWeaponRevolverTaurusJudge", map.GridCoords);
            var judgeAmmo = Drain(judge, entMan);
            Assert.Multiple(() =>
            {
                Assert.That(judgeAmmo.InitialCount, Is.EqualTo(3), "Taurus Judge should spawn with 3 loaded shells");
                Assert.That(judgeAmmo.Shots, Is.EqualTo(3), "Taurus Judge only fired some of its shells");
                Assert.That(judgeAmmo.Fired, Is.EqualTo(3), "Taurus Judge fired dud shells (no projectiles)");
            });
            entMan.DeleteEntity(judge);

            // Mosin: 5-round stripper clip + 1 chambered round; the clip must eject itself when spent.
            var mosin = entMan.SpawnEntity("CSWeaponRifleMosin", map.GridCoords);
            if (entMan.TryGetComponent<ChamberMagazineAmmoProviderComponent>(mosin, out var mosinChamber))
                gunSystem.SetBoltClosed(mosin, mosinChamber, true);
            var mosinAmmo = Drain(mosin, entMan);
            Assert.Multiple(() =>
            {
                Assert.That(mosinAmmo.Shots, Is.EqualTo(6), "Mosin should fire 5 clip rounds + 1 chambered round");
                Assert.That(slots.GetItemOrNull(mosin, "gun_magazine"), Is.Null, "Mosin stripper clip should eject when spent");
            });
            entMan.DeleteEntity(mosin);

            // .308 guns and 9mm SMGs spawn with their own magazine type, not a foreign one.
            var spawnMags = new (string Gun, string Mag)[]
            {
                ("CSWeaponLightMachineGunBAR", "CSMagazine308"),
                ("CSWeaponLightMachineGunM1919", "CSMagazine308Belt"),
                ("CSWeaponLightMachineGunRPD", "CSMagazine308Rpd"),
                ("CSWeaponLightMachineGunDP27", "CSMagazine308Lewis"),
                ("CSWeaponRifleSKS", "CSMagazine308Sks"),
                ("CSWeaponSubMachineGunUzi", "CSMagazine9mmUzi"),
                ("CSWeaponSubMachineGunMP5", "CSMagazine9mmUzi"),
                ("CSWeaponSubMachineGunPPSh", "CSMagazine9mmPpsh"),
            };

            foreach (var (gunId, magId) in spawnMags)
            {
                var gun = entMan.SpawnEntity(gunId, map.GridCoords);
                var mag = slots.GetItemOrNull(gun, "gun_magazine");
                var magProto = mag == null
                    ? null
                    : entMan.GetComponent<MetaDataComponent>(mag.Value).EntityPrototype?.ID;
                Assert.That(magProto, Is.EqualTo(magId), $"{gunId} should spawn with {magId}");

                if (mag != null && magId == "CSMagazine308Belt")
                {
                    var beltAmmo = new GetAmmoCountEvent();
                    entMan.EventBus.RaiseLocalEvent(mag.Value, ref beltAmmo);
                    Assert.That(beltAmmo.Capacity, Is.EqualTo(100), "M1919 ammo belt should hold 100 rounds");
                }

                entMan.DeleteEntity(gun);
            }
        });

        await pair.CleanReturnAsync();
    }
}
