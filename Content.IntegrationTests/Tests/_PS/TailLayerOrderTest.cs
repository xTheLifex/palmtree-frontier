using System.Linq;
using Content.Client._PS.Humanoid;
using Content.Client.Humanoid;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.IoC;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: a tail hanging from the back must render behind the body (and therefore behind clothing
/// and undergarments) in the south view. In the north view the same tail sits between the camera and
/// the body, so it must keep rendering above them.
/// </summary>
[TestFixture]
public sealed class TailLayerOrderTest
{
    [Test]
    public async Task SouthTailRendersBehindBodyAndUndergarments()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var client = pair.Client;

        var serverPlayers = server.ResolveDependency<IPlayerManager>();

        EntityUid mob = default;
        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);
            mob = attached.Value;

            var appearance = HumanoidCharacterAppearance.DefaultWithSpecies("Human");
            appearance.Markings.Add(new Marking("TailFox", 1));
            appearance.Markings.Add(new Marking("UndergarmentBottomBriefs", 1));

            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(mob,
                HumanoidCharacterProfile.DefaultWithSpecies("Human").WithCharacterAppearance(appearance));
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

            direction.Direction = RsiDirection.South;
            appearance.RefreshSprite(attached.Value);

            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                "TailFox-tail_fox_primary", out var southTailLayer, false), Is.True,
                "the tail marking must render in the south view");
            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                HumanoidVisualLayers.UndergarmentBottom, out var southUnderwearLayer, false), Is.True,
                "the undergarment layer must exist in the south view");
            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                HumanoidVisualLayers.Chest, out var southBodyLayer, false), Is.True,
                "the body layer must exist in the south view");

            Assert.That(southTailLayer, Is.LessThan(southBodyLayer),
                "a south-facing tail hangs behind the body");
            Assert.That(southTailLayer, Is.LessThan(southUnderwearLayer),
                "a south-facing tail must not draw over underwear");

            direction.Direction = RsiDirection.North;
            appearance.RefreshSprite(attached.Value);

            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                "TailFox-tail_fox_primary", out var northTailLayer, false), Is.True,
                "the tail marking must render in the north view");
            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                HumanoidVisualLayers.UndergarmentBottom, out var northUnderwearLayer, false), Is.True,
                "the undergarment layer must exist in the north view");
            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                HumanoidVisualLayers.Chest, out var northBodyLayer, false), Is.True,
                "the body layer must exist in the north view");

            Assert.That(northTailLayer, Is.GreaterThan(northBodyLayer),
                "a north-facing tail sits in front of the body");
            Assert.That(northTailLayer, Is.GreaterThan(northUnderwearLayer),
                "north-facing tail ordering must be unchanged");

            clientEntMan.RemoveComponent<HumanoidLayerDirectionComponent>(attached.Value);
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

    /// <summary>
    /// Palmtree/Coyote: digitigrade tails are swapped to an alternate marking prototype. Rebuilding
    /// the sprite must remove those alternate layers using the alternate prototype ID; otherwise a
    /// south-facing tail stays on TailBehind when the character turns north.
    /// </summary>
    [Test]
    public async Task DigitigradeAlternateTailFollowsDirection()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var client = pair.Client;
        var serverPlayers = server.ResolveDependency<IPlayerManager>();

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var appearance = HumanoidCharacterAppearance.DefaultWithSpecies("Vulpkanin");
            appearance = appearance.WithLegs(HumanoidLegStyle.Digitigrade);
            appearance.Markings.Add(new Marking("VulpTailFoxWagTip", 2));

            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value,
                HumanoidCharacterProfile.DefaultWithSpecies("Vulpkanin").WithCharacterAppearance(appearance));
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

            direction.Direction = RsiDirection.South;
            appearance.RefreshSprite(attached.Value);

            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                "DigiVulpTailFoxWagTip-fox_wag", out var southTail, false), Is.True,
                "the digitigrade alternate tail must exist");
            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                HumanoidVisualLayers.Chest, out var southBody, false), Is.True);

            Assert.That(southTail, Is.LessThan(southBody),
                "the south-facing tail belongs behind the body");

            direction.Direction = RsiDirection.North;
            appearance.RefreshSprite(attached.Value);

            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                "DigiVulpTailFoxWagTip-fox_wag", out var northTail, false), Is.True,
                "the rebuilt north view must keep the alternate tail");
            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                HumanoidVisualLayers.Chest, out var northBody, false), Is.True);
            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                HumanoidVisualLayers.Tail, out var northTailLayer, false), Is.True);

            Assert.That(northTail, Is.GreaterThan(northBody),
                "a north-facing tail sits in front of the body");
            Assert.That(northTail, Is.GreaterThan(northTailLayer),
                "the marking must be rebuilt above the base tail layer");

            clientEntMan.RemoveComponent<HumanoidLayerDirectionComponent>(attached.Value);
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
