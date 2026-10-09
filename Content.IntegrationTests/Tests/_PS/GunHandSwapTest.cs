#nullable enable
using System;
using System.Numerics;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: firing a gun while the other hand also holds a gun swaps the active hand so
/// dual-wielded firearms can be fired alternately, and the swapped-to gun keeps its own cooldown
/// instead of eating the hand-select fire-rate penalty. Knives/melee and unwielded two-handed guns
/// must never trigger a swap.
/// </summary>
[TestFixture]
public sealed class GunHandSwapTest
{
    [Test]
    public async Task FiringSwapsHandsOnlyForFirearms()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        EntityUid player = default;
        EntityUid gunA = default, gunB = default, gunC = default, knife = default, gunD = default, mosin = default;
        var handA = string.Empty;
        var handB = string.Empty;

        EntityUid SpawnPistol()
        {
            var gunSystem = server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var gun = entMan.SpawnEntity("CSWeaponPistolM9FS", map.GridCoords);
            if (entMan.TryGetComponent<ChamberMagazineAmmoProviderComponent>(gun, out var chamber))
                gunSystem.SetBoltClosed(gun, chamber, true);
            return gun;
        }

        // Player with a pistol in each hand.
        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            player = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var handsComp = entMan.GetComponent<HandsComponent>(player);
            Assert.That(handsComp.SortedHands.Count, Is.GreaterThanOrEqualTo(2), "test mob has no hands");
            handA = handsComp.SortedHands[0];
            handB = handsComp.SortedHands[1];

            gunA = SpawnPistol();
            gunB = SpawnPistol();
            hands.SetActiveHand(player, handA);
            Assert.That(hands.TryForcePickup(player, gunA, handA), Is.True);
            Assert.That(hands.TryForcePickup(player, gunB, handB), Is.True);
        });

        // Equipping a gun into the active hand applies the hand-select cooldown - let it expire.
        await pair.RunTicksSync(120);

        // Firing one gun hands off to the other, which inherits the fired gun's cooldown so the
        // akimbo pair stays paced at the weapon's own fire rate.
        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var gunSystem = server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var gunAComp = entMan.GetComponent<GunComponent>(gunA);
            var gunBComp = entMan.GetComponent<GunComponent>(gunB);
            var shotCoordinates = entMan.GetComponent<TransformComponent>(player).Coordinates.Offset(new Vector2(1f, 0f));

            Assert.That(hands.GetActiveItem(player), Is.EqualTo(gunA));

            gunSystem.AttemptShoot(player, gunA, gunAComp, shotCoordinates);
            Assert.Multiple(() =>
            {
                Assert.That(hands.GetActiveItem(player), Is.EqualTo(gunB), "firing should swap to the gun in the other hand");
                Assert.That(gunBComp.NextFire, Is.EqualTo(gunAComp.NextFire), "the akimbo partner should inherit the fired gun's cooldown");
                Assert.That(gunBComp.NextFire, Is.GreaterThan(TimeSpan.Zero), "the inherited cooldown should pace the swap");
            });
        });

        // After the copied cooldown expires the other gun fires and hands back.
        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var gunSystem = server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var gunAComp = entMan.GetComponent<GunComponent>(gunA);
            var gunBComp = entMan.GetComponent<GunComponent>(gunB);
            var shotCoordinates = entMan.GetComponent<TransformComponent>(player).Coordinates.Offset(new Vector2(1f, 0f));

            gunSystem.AttemptShoot(player, gunB, gunBComp, shotCoordinates);
            Assert.Multiple(() =>
            {
                Assert.That(hands.GetActiveItem(player), Is.EqualTo(gunA), "firing the second gun should swap back");
                Assert.That(gunAComp.NextFire, Is.EqualTo(gunBComp.NextFire), "the swap back should copy the second gun's cooldown");
            });
        });

        // And the cycle continues: wait out the cooldown and the first gun fires again.
        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var gunSystem = server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var gunAComp = entMan.GetComponent<GunComponent>(gunA);
            var shotCoordinates = entMan.GetComponent<TransformComponent>(player).Coordinates.Offset(new Vector2(1f, 0f));

            gunSystem.AttemptShoot(player, gunA, gunAComp, shotCoordinates);
            Assert.That(hands.GetActiveItem(player), Is.EqualTo(gunB), "the akimbo cycle should keep alternating");

            entMan.DeleteEntity(gunA);
            entMan.DeleteEntity(gunB);
        });

        // Knife in the other hand: no swap.
        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            gunC = SpawnPistol();
            knife = entMan.SpawnEntity("KitchenKnife", map.GridCoords);
            Assert.That(hands.TryForcePickup(player, gunC, handA), Is.True);
            Assert.That(hands.TryForcePickup(player, knife, handB), Is.True);
            hands.SetActiveHand(player, handA);
        });

        await pair.RunTicksSync(120);

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var gunSystem = server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var shotCoordinates = entMan.GetComponent<TransformComponent>(player).Coordinates.Offset(new Vector2(1f, 0f));

            gunSystem.AttemptShoot(player, gunC, entMan.GetComponent<GunComponent>(gunC), shotCoordinates);
            Assert.That(hands.GetActiveItem(player), Is.EqualTo(gunC), "a knife in the other hand must not trigger a hand swap");

            entMan.DeleteEntity(gunC);
            entMan.DeleteEntity(knife);
        });

        // Unwielded two-handed gun in the other hand: no swap (it cannot fire anyway).
        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            gunD = SpawnPistol();
            mosin = entMan.SpawnEntity("CSWeaponRifleMosin", map.GridCoords);
            Assert.That(hands.TryForcePickup(player, gunD, handA), Is.True);
            Assert.That(hands.TryForcePickup(player, mosin, handB), Is.True);
            hands.SetActiveHand(player, handA);
        });

        await pair.RunTicksSync(120);

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var gunSystem = server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var shotCoordinates = entMan.GetComponent<TransformComponent>(player).Coordinates.Offset(new Vector2(1f, 0f));

            gunSystem.AttemptShoot(player, gunD, entMan.GetComponent<GunComponent>(gunD), shotCoordinates);
            Assert.That(hands.GetActiveItem(player), Is.EqualTo(gunD), "an unwielded two-handed gun must not trigger a hand swap");

            entMan.DeleteEntity(gunD);
            entMan.DeleteEntity(mosin);
        });

        await server.WaitPost(() => entMan.DeleteEntity(player));
        await pair.CleanReturnAsync();
    }
}
