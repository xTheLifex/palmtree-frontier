using System.IO;
using System.Linq;
using System.Text;
using Content.Shared._PS.Synth;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: the Synth species is an IPC replacement built on the marking system
/// (2x speed, 3x durability, IPC-like resistances, heavier).
/// </summary>
[TestFixture]
public sealed class SynthSpeciesTest
{
    [Test]
    public async Task SynthHasIpcLikeStats()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var markingManager = server.ResolveDependency<MarkingManager>();

        await server.WaitAssertion(() =>
        {
            var synth = entMan.SpawnEntity("MobSynth", testMap.GridCoords);

            // Twice the default speed (2.5 / 4.5).
            var speed = entMan.GetComponent<MovementSpeedModifierComponent>(synth);
            Assert.That(speed.BaseWalkSpeed, Is.EqualTo(3.75f));
            Assert.That(speed.BaseSprintSpeed, Is.EqualTo(6.75f));

            // Three times the human durability (100 / 200).
            var thresholds = entMan.GetComponent<MobThresholdsComponent>(synth);
            Assert.That(thresholds.Thresholds[300], Is.EqualTo(MobState.Critical));
            Assert.That(thresholds.Thresholds[600], Is.EqualTo(MobState.Dead));

            // IPC-like damage handling.
            var damageable = entMan.GetComponent<DamageableComponent>(synth);
            Assert.That(damageable.DamageContainerID?.Id, Is.EqualTo("Synth"));
            Assert.That(damageable.DamageModifierSetId?.Id, Is.EqualTo("Synth"));

            // Heavier than a human (185).
            var fixtures = entMan.GetComponent<FixturesComponent>(synth);
            Assert.That(fixtures.Fixtures["fix1"].Density, Is.EqualTo(462.5f));

            // Species-adaptor markings must be usable so a synth can look like anything.
            var speciesProto = protoMan.Index<SpeciesPrototype>("Synth");
            var adaptor = protoMan.Index<MarkingPrototype>("SpeciesAdaptorVulpkaninLArm");
            Assert.That(MarkingManager.IsAllowedBySpeciesOrKindAllowance(speciesProto, adaptor), Is.True);
            Assert.That(markingManager.CanBeApplied("Synth", Sex.Unsexed, adaptor, protoMan), Is.True);

            // Species-restricted markings are shared through kindAllowance (Coyote behavior).
            var ear = protoMan.Index<MarkingPrototype>("VulpEarFade");
            var tail = protoMan.Index<MarkingPrototype>("VulpTailWagTip");
            Assert.That(MarkingManager.IsAllowedBySpeciesOrKindAllowance(speciesProto, ear), Is.True);
            Assert.That(MarkingManager.IsAllowedBySpeciesOrKindAllowance(speciesProto, tail), Is.True);

            // The Synth marker skips respiration (no gasping without lungs).
            Assert.That(entMan.HasComponent<SynthComponent>(synth), Is.True);

            // Silicon body parts: inorganic damage container and doubled leg speed.
            var leg = entMan.SpawnEntity("LeftLegSynth", testMap.GridCoords);
            var legDamageable = entMan.GetComponent<DamageableComponent>(leg);
            Assert.That(legDamageable.DamageContainerID?.Id, Is.EqualTo("Inorganic"));
            var legMovement = entMan.GetComponent<MovementBodyPartComponent>(leg);
            Assert.That(legMovement.WalkSpeed, Is.EqualTo(3.75f));
            Assert.That(legMovement.SprintSpeed, Is.EqualTo(6.75f));
            entMan.DeleteEntity(leg);

            entMan.DeleteEntity(synth);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OldIpcExportsFallBackToSynth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var humanoid = server.System<SharedHumanoidAppearanceSystem>();
            const string yaml = "forkId: Test\nversion: 1\nprofile:\n  species: IPC\n";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            var profile = humanoid.FromStream(stream, null!);
            Assert.That(profile.Species.Id, Is.EqualTo("Synth"));
        });

        await pair.CleanReturnAsync();
    }
}
