using Content.Server.GameTicking;
using NUnit.Framework;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree/Coyote: the lobby background is rotated while players wait in the lobby.
/// </summary>
[TestFixture]
public sealed class LobbyBackgroundTest
{
    [Test]
    public async Task LobbyBackgroundCyclesOverTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var ticker = server.System<GameTicker>();

        string? initial = null;
        await server.WaitAssertion(() =>
        {
            initial = ticker.LobbyBackground;
            Assert.That(initial, Is.Not.Null, "A lobby background should be selected on startup.");
        });

        // The cycle timer runs every 30 seconds of game time. Allow a few cycles so a repeated
        // random pick cannot make this flaky.
        var changed = false;
        for (var i = 0; i < 5 && !changed; i++)
        {
            await server.WaitRunTicks(31 * 60);
            changed = ticker.LobbyBackground != initial;
        }

        Assert.That(changed, Is.True, "The lobby background never changed; the rotation timer did not fire.");

        await pair.CleanReturnAsync();
    }
}
