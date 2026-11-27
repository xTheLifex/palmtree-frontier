using System.Linq;
using Content.Client.Humanoid;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: species leg styles must swap the base leg/foot/torso sprites, not just the clothing
/// displacement. SlimePerson previously offered Digitigrade in the editor but had no altSprites
/// wired, so only the shoes changed.
/// </summary>
[TestFixture]
public sealed class DigitigradeSpeciesTest
{
    [Test]
    public async Task OnlySpeciesWithDigitigradeArtSupportTheStyle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();

        Assert.Multiple(() =>
        {
            Assert.That(SharedHumanoidAppearanceSystem.SupportsLegStyle(
                prototypes, "SlimePerson", HumanoidLegStyle.Digitigrade), Is.True);
            Assert.That(SharedHumanoidAppearanceSystem.SupportsLegStyle(
                prototypes, "Vulpkanin", HumanoidLegStyle.Digitigrade), Is.True);
            Assert.That(SharedHumanoidAppearanceSystem.SupportsLegStyle(
                prototypes, "Human", HumanoidLegStyle.Digitigrade), Is.False);
            Assert.That(SharedHumanoidAppearanceSystem.SupportsLegStyle(
                prototypes, "Diona", HumanoidLegStyle.Digitigrade), Is.False);
            Assert.That(SharedHumanoidAppearanceSystem.SupportsLegStyle(
                prototypes, "Vox", HumanoidLegStyle.Digitigrade), Is.False);
            Assert.That(SharedHumanoidAppearanceSystem.SupportsLegStyle(
                prototypes, "Human", HumanoidLegStyle.Plantigrade), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    [NonParallelizable]
    public async Task SlimePersonDigitigradeSwapsBaseLegSprites()
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

        var digitigrade = HumanoidCharacterProfile.DefaultWithSpecies("SlimePerson")
            .WithSex(Sex.Male);
        digitigrade = digitigrade.WithCharacterAppearance(
            digitigrade.Appearance.WithLegs(HumanoidLegStyle.Digitigrade));

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);
            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value, digitigrade);
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.ResolveDependency<IEntityManager>();
            var attached = client.Session.AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var sprite = clientEntMan.GetComponent<SpriteComponent>(attached.Value);
            var spriteSystem = client.System<SpriteSystem>();
            var state = State(sprite, spriteSystem, attached.Value);

            Assert.That(state(HumanoidVisualLayers.LLeg), Is.EqualTo("PawSlime-Left-Leg"),
                "digitigrade slime legs must use the slime digitigrade leg sprite");
            Assert.That(state(HumanoidVisualLayers.RLeg), Is.EqualTo("PawSlime-Right-Leg"));
            Assert.That(state(HumanoidVisualLayers.LFoot), Is.EqualTo("PawSlime-Left-Foot"),
                "digitigrade slime feet must use the slime digitigrade foot sprite");
            Assert.That(state(HumanoidVisualLayers.RFoot), Is.EqualTo("PawSlime-Right-Foot"));
            Assert.That(state(HumanoidVisualLayers.Chest), Is.EqualTo("SlimeChestMale"),
                "digitigrade slime torsos must use the digitigrade chest sprite");
        });

        var plantigrade = digitigrade.WithCharacterAppearance(
            digitigrade.Appearance.WithLegs(HumanoidLegStyle.Plantigrade));

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);
            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value, plantigrade);
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.ResolveDependency<IEntityManager>();
            var attached = client.Session.AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var sprite = clientEntMan.GetComponent<SpriteComponent>(attached.Value);
            var spriteSystem = client.System<SpriteSystem>();
            var state = State(sprite, spriteSystem, attached.Value);

            Assert.That(state(HumanoidVisualLayers.LLeg), Is.EqualTo("l_leg"),
                "switching back to plantigrade must restore the plantigrade leg sprite");
            Assert.That(state(HumanoidVisualLayers.RLeg), Is.EqualTo("r_leg"));
            Assert.That(state(HumanoidVisualLayers.LFoot), Is.EqualTo("l_foot"));
            Assert.That(state(HumanoidVisualLayers.RFoot), Is.EqualTo("r_foot"));
            Assert.That(state(HumanoidVisualLayers.Chest), Is.EqualTo("torso_m"));
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

    private static Func<HumanoidVisualLayers, string> State(
        SpriteComponent sprite,
        SpriteSystem spriteSystem,
        EntityUid uid)
    {
        return layer =>
        {
            Assert.That(spriteSystem.LayerMapTryGet((uid, sprite), layer, out var index, false), Is.True,
                $"layer {layer} must exist");
            Assert.That(spriteSystem.TryGetLayer((uid, sprite), index, out var spriteLayer, false), Is.True);
            return spriteLayer!.State.Name ?? string.Empty;
        };
    }
}
