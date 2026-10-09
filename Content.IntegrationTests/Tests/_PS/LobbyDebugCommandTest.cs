#nullable enable
using Content.Server.Audio;
using Content.Server.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: the debug commands that force the lobby background and music must set their override
/// and reset cleanly, so new lobby content can be checked in-game.
/// </summary>
[TestFixture]
public sealed class LobbyDebugCommandTest
{
    [Test]
    public async Task ForceAndResetLobbyBackgroundAndMusic()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();
        var audio = entMan.System<ContentAudioSystem>();

        // Background: force a known prototype, then reset.
        await server.ExecuteCommand("setlobbybackground SpaceCarp");
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(ticker.LobbyBackgroundOverride, Is.Not.Null, "background override should be set");
                Assert.That(ticker.LobbyBackground, Is.EqualTo(ticker.LobbyBackgroundOverride),
                    "the forced background should be the active one");
            });
        });

        await server.ExecuteCommand("setlobbybackground reset");
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.LobbyBackgroundOverride, Is.Null, "background override should clear on reset");
            Assert.That(ticker.LobbyBackground, Is.Not.Null, "rotation should pick a background again");
        });

        // Music: force a single track, then a whole collection, then reset.
        await server.ExecuteCommand("setlobbymusic /Audio/_PS/Lobby/NI4NI.ogg");
        await server.WaitAssertion(() =>
        {
            Assert.That(audio.LobbyPlaylistForced, Is.True, "single-track force should set the flag");
        });

        await server.ExecuteCommand("setlobbymusic PSLobbyMusic");
        await server.WaitAssertion(() =>
        {
            Assert.That(audio.LobbyPlaylistForced, Is.True, "collection force should keep the flag set");
        });

        await server.ExecuteCommand("setlobbymusic reset");
        await server.WaitAssertion(() =>
        {
            Assert.That(audio.LobbyPlaylistForced, Is.False, "music force should clear on reset");
        });

        await pair.CleanReturnAsync();
    }
}
