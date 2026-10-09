#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: the gun's magazine layer must follow the magazine slot - visible when a magazine is
/// loaded and hidden when there is none. Guards the reported bug where racking made the magazine
/// appear (RPD base art had the drum baked in) and where loading/ejecting never updated the sprite.
/// </summary>
[TestFixture]
public sealed class GunMagazineSpriteTest
{
    private static readonly (string Gun, string Mag)[] Cases =
    {
        ("CSWeaponLightMachineGunBAR", "CSMagazine308"),
        ("CSWeaponLightMachineGunM1919", "CSMagazine308Belt"),
        ("CSWeaponLightMachineGunRPD", "CSMagazine308Rpd"),
        ("CSWeaponSubMachineGunUzi", "CSMagazine9mmUzi"),
        ("CSWeaponSubMachineGunMP5", "CSMagazine9mmUzi"),
        ("CSWeaponSubMachineGunPPSh", "CSMagazine9mmPpsh"),
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

            await server.WaitPost(() =>
            {
                sEnt.DeleteEntity(sEnt.GetEntity(gunNet));
            });
            await pair.RunTicksSync(5);
        }

        await pair.CleanReturnAsync();
    }
}
