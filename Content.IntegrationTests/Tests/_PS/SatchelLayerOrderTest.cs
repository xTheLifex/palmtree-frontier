using System.Linq;
using Content.Client._PS.Consent;
using Content.Client._PS.Humanoid;
using Content.Client.Humanoid;
using Content.Shared._PS.Consent;
using Content.Shared._PS.Interactions;
using Content.Shared._PS.Organs;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.IoC;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: back-slot clothing (satchels, backpacks) renders over the exposed south organ stack.
/// Their south-facing art is just the strap/belt, so it must sit above the breasts rather than
/// being buried under them.
/// </summary>
[TestFixture]
public sealed class SatchelLayerOrderTest
{
    [Test]
    [NonParallelizable] // NsfwGate is a process-wide static.
    public async Task SatchelRendersOverExposedOrgansFacingSouth()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });

        var server = pair.Server;
        var client = pair.Client;
        var serverPlayers = server.ResolveDependency<IPlayerManager>();

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Breasts, new GenitalOrganData
            {
                Prototype = "BreastsSplurt",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });
            settings.Set(GenitalType.Belly, new GenitalOrganData
            {
                Prototype = "BellyTummy",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });

            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value,
                HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));

            var serverEntMan = server.ResolveDependency<IEntityManager>();
            var inventory = server.System<InventorySystem>();
            if (serverEntMan.TryGetComponent<InventoryComponent>(attached.Value, out var inv))
            {
                if (inventory.TryGetSlotEntity(attached.Value, "jumpsuit", out var jumpsuit, inv))
                    serverEntMan.DeleteEntity(jumpsuit.Value);
                if (inventory.TryGetSlotEntity(attached.Value, "back", out var oldBack, inv))
                    serverEntMan.DeleteEntity(oldBack.Value);
            }
        });

        // Let the client finish building the appearance layers before equipping the satchel. If the
        // equipment visual arrives first, the "back" bookmark does not exist yet and the satchel
        // layers are appended above everything instead of anchored under the neck.
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var serverEntMan = server.ResolveDependency<IEntityManager>();
            var inventory = server.System<InventorySystem>();
            var satchel = serverEntMan.SpawnEntity("ClothingBackpackSatchel",
                serverEntMan.GetComponent<TransformComponent>(attached.Value).Coordinates);
            Assert.That(inventory.TryEquip(attached.Value, satchel, "back", true, true), Is.True,
                "the satchel must equip");
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.ResolveDependency<IEntityManager>();
            var attached = client.Session.AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var appearance = client.System<HumanoidAppearanceSystem>();
            var direction = clientEntMan.EnsureComponent<HumanoidLayerDirectionComponent>(attached.Value);
            var sprite = clientEntMan.GetComponent<SpriteComponent>(attached.Value);
            var spriteSystem = client.System<SpriteSystem>();

            var previousNsfwState = NsfwGate.State;
            NsfwGate.SetState(NsfwConsentState.Allowed);

            void AssertSatchelOverOrgans(string facing)
            {
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "back-0", out var satchelLayer, false), Is.True,
                    $"the satchel must render facing {facing}");
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary", out var breastLayer, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "GenitalBellyTummy1-belly_1_tummy", out var bellyLayer, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.GenitalUnder, out var genitalUnder, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "neck", out var neckLayer, false), Is.True);

                Assert.That(sprite[satchelLayer].Visible, Is.True);
                Assert.That(satchelLayer, Is.GreaterThan(genitalUnder),
                    "the satchel must render over the exposed organ stack");
                Assert.That(satchelLayer, Is.GreaterThan(bellyLayer));
                Assert.That(satchelLayer, Is.GreaterThan(breastLayer),
                    "the satchel strap must render over exposed breasts");
                Assert.That(satchelLayer, Is.LessThan(neckLayer),
                    "neckwear stays above the satchel");
            }

            try
            {
                direction.Direction = RsiDirection.South;
                appearance.RefreshSprite(attached.Value);
                AssertSatchelOverOrgans("south");

                direction.Direction = RsiDirection.East;
                appearance.RefreshSprite(attached.Value);

                direction.Direction = RsiDirection.South;
                appearance.RefreshSprite(attached.Value);
                AssertSatchelOverOrgans("south after rotating");
            }
            finally
            {
                clientEntMan.RemoveComponent<HumanoidLayerDirectionComponent>(attached.Value);
                NsfwGate.SetState(previousNsfwState);
            }
        });

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            if (attached != null)
            {
                server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value,
                    HumanoidCharacterProfile.DefaultWithSpecies("Human"));
            }
        });

        await pair.RunTicksSync(5);
        await pair.CleanReturnAsync();
    }
}
