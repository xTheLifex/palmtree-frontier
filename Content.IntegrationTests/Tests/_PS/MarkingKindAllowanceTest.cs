using System.Collections.Generic;
using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree/Floof: markings restricted to a species may also be allowed through a shared species kind.
/// </summary>
[TestFixture]
public sealed class MarkingKindAllowanceTest
{
    [Test]
    public async Task KindAllowedMarkingSurvivesEnsureSpecies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var proto = server.ResolveDependency<IPrototypeManager>();
            var markingManager = server.ResolveDependency<MarkingManager>();

            var speciesProto = proto.Index<SpeciesPrototype>("Vulpkanin");
            var markingProto = proto.Index<MarkingPrototype>("FeroxiTorsoCountershadingF");

            // Kind allowance must let a Feroxi-only marking stay on a Vulpkanin.
            Assert.That(MarkingManager.IsAllowedBySpeciesOrKindAllowance(speciesProto, markingProto), Is.True);
            Assert.That(markingManager.CanBeApplied("Vulpkanin", Sex.Male, markingProto, proto), Is.True);

            var set = new MarkingSet(speciesProto.MarkingPoints, markingManager, proto);
            set.AddBack(markingProto.MarkingCategory, markingProto.AsMarking());
            set.EnsureSpecies("Vulpkanin", null, markingManager, proto);

            Assert.That(
                set.Markings[markingProto.MarkingCategory].Any(m => m.MarkingId == markingProto.ID),
                Is.True,
                "The kind-allowed marking was removed by EnsureSpecies.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProfileLoadPreservesMarkingTransformAndGlow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human");
            var marking = new Marking("VulpEarFade", new List<Color> { Color.White, Color.White });
            marking.SetScale(1.5f);
            marking.SetOffset(0.1f, -0.2f);
            marking.SetGlow(0, 0.8f);
            profile = profile.WithCharacterAppearance(
                profile.Appearance.WithMarkings(new List<Marking> { marking }));

            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(mob);
            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(mob, profile, humanoid);

            var loaded = humanoid.MarkingSet.Markings.Values.SelectMany(list => list)
                .FirstOrDefault(m => m.MarkingId == "VulpEarFade");

            Assert.That(loaded, Is.Not.Null, "The marking was not applied to the mob.");
            Assert.That(loaded!.MarkingScale, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(loaded.MarkingOffset.X, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(loaded.MarkingGlow[0], Is.EqualTo(0.8f).Within(0.0001f));

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProfileLoadPreservesMarkingVisibilitySettings()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human");
            var marking = new Marking("VulpEarFade", new List<Color> { Color.White, Color.White });
            marking.CanToggleVisible = false;
            marking.OtherCanToggleVisible = true;
            marking.SetCustomName("Sleepy Ears");
            profile = profile.WithCharacterAppearance(
                profile.Appearance.WithMarkings(new List<Marking> { marking }));

            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(mob);
            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(mob, profile, humanoid);

            var loaded = humanoid.MarkingSet.Markings.Values.SelectMany(list => list)
                .FirstOrDefault(m => m.MarkingId == "VulpEarFade");

            Assert.That(loaded, Is.Not.Null, "The marking was not applied to the mob.");
            Assert.That(loaded!.CanToggleVisible, Is.False);
            Assert.That(loaded.OtherCanToggleVisible, Is.True);
            Assert.That(loaded.CustomName, Is.EqualTo("Sleepy Ears"));

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SlimeEyeglowAllowedForSynth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var proto = server.ResolveDependency<IPrototypeManager>();
            var markingManager = server.ResolveDependency<MarkingManager>();

            var speciesProto = proto.Index<SpeciesPrototype>("Synth");
            var markingProto = proto.Index<MarkingPrototype>("SlimeEyeglow");

            Assert.That(MarkingManager.IsAllowedBySpeciesOrKindAllowance(speciesProto, markingProto), Is.True);
            Assert.That(markingManager.CanBeApplied("Synth", Sex.Male, markingProto, proto), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
