using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: Feroxi (ported from DeltaV/Coyote) are shark-like and can breathe both
/// oxygen and water vapor, using their own lungs/gills organ.
/// </summary>
[TestFixture]
public sealed class FeroxiSpeciesTest
{
    [Test]
    public async Task FeroxiBreathesOxygenAndWaterVapor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var respirator = server.System<RespiratorSystem>();
        var bodySystem = server.System<BodySystem>();

        await server.WaitAssertion(() =>
        {
            var feroxi = entMan.SpawnEntity("MobFeroxi", testMap.GridCoords);

            // Feroxi use their own lungs/gills organ with the Feroxi metabolizer type.
            var body = entMan.GetComponent<BodyComponent>(feroxi);
            var lungs = bodySystem.GetBodyOrganEntityComps<LungComponent>((feroxi, body));
            Assert.That(lungs, Is.Not.Empty);
            Assert.That(entMan.GetComponent<MetaDataComponent>(lungs[0].Owner).EntityPrototype?.ID,
                Is.EqualTo("OrganFeroxiLungs"));

            var metabolizer = entMan.GetComponent<MetabolizerComponent>(lungs[0].Owner);
            Assert.That(metabolizer.MetabolizerTypes, Is.Not.Null);
            Assert.That(metabolizer.MetabolizerTypes!.Select(t => t.Id), Contains.Item("Feroxi"));

            // Oxygen and water vapor both oxygenate.
            var oxygen = new GasMixture(1f);
            oxygen.AdjustMoles(Gas.Oxygen, 1f);
            Assert.That(respirator.CanMetabolizeInhaledAir(feroxi, oxygen), Is.True);

            var waterVapor = new GasMixture(1f);
            waterVapor.AdjustMoles(Gas.WaterVapor, 1f);
            Assert.That(respirator.CanMetabolizeInhaledAir(feroxi, waterVapor), Is.True);

            // Control: a human cannot breathe water vapor.
            var human = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            Assert.That(respirator.CanMetabolizeInhaledAir(human, waterVapor), Is.False);

            entMan.DeleteEntity(feroxi);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FeroxiPrototypesExist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.HasIndex<SpeciesPrototype>("Feroxi"), Is.True);
            Assert.That(protoMan.HasIndex<EntityPrototype>("MobFeroxi"), Is.True);
            Assert.That(protoMan.HasIndex<EntityPrototype>("OrganFeroxiLungs"), Is.True);
            Assert.That(protoMan.HasIndex<EntityPrototype>("WaterVaporTankFilled"), Is.True);
            Assert.That(protoMan.HasIndex<DamageModifierSetPrototype>("Feroxi"), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
