#nullable enable
using System.Collections.Generic;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
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

            // Mosin: internal 5-round magazine fed by stripper clips (the clip never goes inside the gun).
            var mosin = entMan.SpawnEntity("CSWeaponRifleMosin", map.GridCoords);
            Assert.That(entMan.HasComponent<ChamberMagazineAmmoProviderComponent>(mosin), Is.False,
                "Mosin should use an internal magazine, not a detachable one");
            Assert.That(entMan.TryGetComponent<BallisticAmmoProviderComponent>(mosin, out var mosinMag), Is.True,
                "Mosin should have an internal ballistic magazine");
            var mosinAmmo = Drain(mosin, entMan);
            Assert.Multiple(() =>
            {
                Assert.That(mosinMag!.Capacity, Is.EqualTo(5), "Mosin internal magazine should hold 5 rounds");
                Assert.That(mosinAmmo.Shots, Is.EqualTo(5), "Mosin should fire its 5 internal rounds");
                Assert.That(entMan.HasComponent<ItemSlotsComponent>(mosin), Is.False, "Mosin should have no magazine slot");
            });
            entMan.DeleteEntity(mosin);

            // Stripper clips feed internal magazines in one action.
            var clip = entMan.SpawnEntity("CSMagazine54RClip", map.GridCoords);
            Assert.That(entMan.GetComponent<BallisticAmmoProviderComponent>(clip).MayTransferAll, Is.True,
                "the 54R stripper clip should bulk-transfer into the Mosin");
            entMan.DeleteEntity(clip);

            // The Mosin's internal magazine only takes 54R cartridges - never .308 rounds or clips.
            var mosin2 = entMan.SpawnEntity("CSWeaponRifleMosin", map.GridCoords);
            var mosinProvider = entMan.GetComponent<BallisticAmmoProviderComponent>(mosin2);
            var whitelistSys = entMan.System<Content.Shared.Whitelist.EntityWhitelistSystem>();
            var cartridge54 = entMan.SpawnEntity("CSCartridge54R", map.GridCoords);
            var cartridge308 = entMan.SpawnEntity("CSCartridge308", map.GridCoords);
            var sksClip = entMan.SpawnEntity("CSMagazine308Sks", map.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(whitelistSys.IsWhitelistPass(mosinProvider.Whitelist, cartridge54), Is.True,
                    "Mosin should accept 54R cartridges");
                Assert.That(whitelistSys.IsWhitelistPass(mosinProvider.Whitelist, cartridge308), Is.False,
                    "Mosin must reject .308 cartridges");
                Assert.That(whitelistSys.IsWhitelistPass(mosinProvider.Whitelist, sksClip), Is.False,
                    "Mosin must reject SKS clips");
            });

            // And the interaction path must reject it too (clicking the Mosin with an SKS clip).
            var player = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var hands = entMan.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>();
            hands.TryForcePickup(player, sksClip, entMan.GetComponent<Content.Shared.Hands.Components.HandsComponent>(player).SortedHands[0]);
            entMan.System<Content.Shared.Interaction.SharedInteractionSystem>().InteractUsing(
                player, sksClip, mosin2, entMan.GetComponent<TransformComponent>(mosin2).Coordinates);
            var mosinEv = new GetAmmoCountEvent();
            entMan.EventBus.RaiseLocalEvent(mosin2, ref mosinEv);
            Assert.That(mosinEv.Count, Is.EqualTo(5), "clicking the Mosin with an SKS clip must not load .308 rounds");

            entMan.DeleteEntity(mosin2);
            entMan.DeleteEntity(cartridge54);
            entMan.DeleteEntity(cartridge308);
            entMan.DeleteEntity(sksClip);
            entMan.DeleteEntity(player);

            // .308 guns and 9mm SMGs spawn with their own magazine type, not a foreign one.
            var spawnMags = new (string Gun, string Mag)[]
            {
                ("CSWeaponLightMachineGunBAR", "CSMagazine308"),
                ("CSWeaponLightMachineGunM1919", "CSMagazine308Rpd"),
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

                if (mag != null && magId == "CSMagazine308Rpd")
                {
                    var drumAmmo = new GetAmmoCountEvent();
                    entMan.EventBus.RaiseLocalEvent(mag.Value, ref drumAmmo);
                    Assert.That(drumAmmo.Capacity, Is.EqualTo(100), "the .308 drum should hold 100 rounds");
                }

                entMan.DeleteEntity(gun);
            }
        });

        await pair.CleanReturnAsync();
    }
}
