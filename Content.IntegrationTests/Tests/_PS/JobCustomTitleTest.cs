using System.Linq;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.IoC;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: custom ID card job titles are opt-in per job and are stripped during profile validation
/// for restricted jobs.
/// </summary>
[TestFixture]
public sealed class JobCustomTitleTest
{
    [Test]
    public async Task OnlyOptInJobsKeepCustomTitles()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var session = server.ResolveDependency<IPlayerManager>().Sessions.Single();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithJobCustomTitle("Contractor", "  Freelancer  ")
                .WithJobCustomTitle("Deputy", "Sheriff");

            profile.EnsureValid(session, IoCManager.Instance!);

            Assert.That(profile.GetJobCustomTitle("Contractor"), Is.EqualTo("Freelancer"));
            Assert.That(profile.GetJobCustomTitle("Deputy"), Is.Null);
        });

        await pair.CleanReturnAsync();
    }
}
