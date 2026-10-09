#nullable enable
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: the gun's magazine layer must follow the magazine slot - visible when a magazine is
/// loaded (even after firing a few rounds) and hidden when there is none. Guards the reported bugs
/// where racking made the magazine appear and where firing or loading/ejecting never updated it.
/// </summary>
[TestFixture]
public sealed class GunMagazineSpriteTest
{
    private static readonly (string Gun, string Mag)[] Cases =
    {
        ("CSWeaponLightMachineGunBAR", "CSMagazine308"),
        ("CSWeaponLightMachineGunM1919", "CSMagazine308Rpd"),
        ("CSWeaponLightMachineGunRPD", "CSMagazine308Rpd"),
        ("CSWeaponSubMachineGunUzi", "CSMagazine9mmUzi"),
        ("CSWeaponSubMachineGunMP5", "CSMagazine9mmUzi"),
        ("CSWeaponSubMachineGunPPSh", "CSMagazine9mmPpsh"),
        ("CSWeaponPistolSkorpion", "CSMagazine9mm"),
        ("CSWeaponAssaultRifleAKM", "NFMagazineRifle30"),
    };

    [Test]
    public async Task MagazineLayerFollowsMagazineSlot()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var sEnt = server.ResolveDependency<IEntityManager>();
        var cEnt = client.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        async Task<bool?> MagVisible(NetEntity gunNet)
        {
            bool? visible = null;
            await client.WaitPost(() =>
            {
                var uid = cEnt.GetEntity(gunNet);
                var sprite = cEnt.GetComponent<SpriteComponent>(uid);
                var spriteSys = client.System<SpriteSystem>();
                if (!spriteSys.LayerMapTryGet((uid, sprite), GunVisualLayers.Mag, out var layerId, false))
                    return;
                if (!spriteSys.TryGetLayer((uid, sprite), layerId, out var layer, false))
                    return;
                visible = layer.Visible;
            });
            return visible;
        }

        foreach (var (gun, mag) in Cases)
        {
            NetEntity gunNet = default;
            await server.WaitPost(() =>
            {
                gunNet = sEnt.GetNetEntity(sEnt.SpawnEntity(gun, map.GridCoords));
            });

            await pair.RunTicksSync(10);
            Assert.That(await MagVisible(gunNet), Is.True, $"{gun} spawned with a magazine but hides its mag sprite");

            bool ejected = false;
            await server.WaitPost(() =>
            {
                ejected = sEnt.System<ItemSlotsSystem>().TryEject(sEnt.GetEntity(gunNet), "gun_magazine", null, out _);
            });
            Assert.That(ejected, Is.True, $"{gun} could not eject its starting magazine");

            await pair.RunTicksSync(10);
            Assert.That(await MagVisible(gunNet), Is.False, $"{gun} still draws its magazine after the magazine was removed");

            bool inserted = false;
            await server.WaitPost(() =>
            {
                var magUid = sEnt.SpawnEntity(mag, map.GridCoords);
                inserted = sEnt.System<ItemSlotsSystem>().TryInsert(sEnt.GetEntity(gunNet), "gun_magazine", magUid, null);
            });
            Assert.That(inserted, Is.True, $"{gun} refused its own magazine {mag}");

            await pair.RunTicksSync(10);
            Assert.That(await MagVisible(gunNet), Is.True, $"{gun} does not draw its magazine after one was inserted");

            // Firing must not hide the magazine while rounds remain in it.
            bool magSlotStillFilled = false;
            EntityUid? fireUser = null;
            await server.WaitPost(() =>
            {
                var gunUid = sEnt.GetEntity(gunNet);
                var gunSystem = server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
                var gunComp = sEnt.GetComponent<GunComponent>(gunUid);

                // Wield-required guns (LMGs, rifles) can't fire from the floor - give them a wielder.
                var user = gunUid;
                if (sEnt.HasComponent<GunRequiresWieldComponent>(gunUid) &&
                    sEnt.TryGetComponent<WieldableComponent>(gunUid, out var wieldable))
                {
                    var player = sEnt.SpawnEntity("MobHuman", map.GridCoords);
                    fireUser = player;
                    var hands = sEnt.System<SharedHandsSystem>();
                    var handsComp = sEnt.GetComponent<HandsComponent>(player);
                    hands.TryForcePickup(player, gunUid, handsComp.SortedHands[0]);
                    sEnt.System<SharedWieldableSystem>().TryWield(gunUid, wieldable, player);
                    user = player;
                }

                if (sEnt.TryGetComponent<ChamberMagazineAmmoProviderComponent>(gunUid, out var chamber))
                    gunSystem.SetBoltClosed(gunUid, chamber, true);

                var coordinates = sEnt.GetComponent<TransformComponent>(user).Coordinates.Offset(new Vector2(1f, 0f));
                gunSystem.AttemptShoot(user, gunUid, gunComp, coordinates);
                magSlotStillFilled = sEnt.System<ItemSlotsSystem>().GetItemOrNull(gunUid, "gun_magazine") != null;
            });

            await pair.RunTicksSync(10);
            var magVisibleAfterFire = await MagVisible(gunNet);
            Assert.Multiple(() =>
            {
                Assert.That(magSlotStillFilled, Is.True, $"{gun} lost its magazine after firing one shot");
                Assert.That(magVisibleAfterFire, Is.True, $"{gun} hides its magazine after firing one shot");
            });

            await server.WaitPost(() =>
            {
                if (fireUser != null)
                    sEnt.DeleteEntity(fireUser.Value);
                sEnt.DeleteEntity(sEnt.GetEntity(gunNet));
            });
            await pair.RunTicksSync(5);
        }

        await pair.CleanReturnAsync();
    }
}
