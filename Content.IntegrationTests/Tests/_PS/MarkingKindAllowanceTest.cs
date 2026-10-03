using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
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
}
