using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Shared.Console;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree/Floof: subtle chat (ported from Coyote) is command-driven and never reaches ghosts.
/// </summary>
[TestFixture]
public sealed class SubtleChatTest
{
    [Test]
    public async Task SubtleCommandsAndLocaleExist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var console = server.ResolveDependency<IConsoleHost>();
            var loc = server.ResolveDependency<ILocalizationManager>();

            Assert.That(console.AvailableCommands, Contains.Key("subtle"));
            Assert.That(console.AvailableCommands, Contains.Key("subtlelooc"));
            Assert.That(
                loc.TryGetString("chat-manager-entity-subtle-wrap-message", out _,
                    ("entityName", "Test"), ("entity", "Test"), ("message", "test")),
                Is.True);
            Assert.That(
                loc.TryGetString("chat-manager-entity-subtle-looc-wrap-message", out _,
                    ("entityName", "Test"), ("message", "test")),
                Is.True);
            Assert.That(loc.TryGetString("hud-chatbox-select-channel-Subtle", out _), Is.True);
            Assert.That(loc.TryGetString("hud-chatbox-select-channel-SubtleLOOC", out _), Is.True);
            Assert.That(loc.TryGetString("hud-chatbox-channel-SubtleLOOC", out _), Is.True);
            Assert.That(loc.TryGetString("ui-options-subtle-sound", out _), Is.True);
            Assert.That(loc.TryGetString("ui-options-function-focus-subtle", out _), Is.True);
            Assert.That(loc.TryGetString("ui-options-function-focus-subtle-looc-window", out _), Is.True);

            // Subtle chat must stay tighter than normal voice range.
            Assert.That(SharedChatSystem.SubtleRange, Is.LessThan(SharedChatSystem.VoiceRange));

            // Chat prefixes and selectable channels for subtle chat.
            Assert.That(SharedChatSystem.SubtlePrefix, Is.EqualTo('-'));
            Assert.That(SharedChatSystem.SubtleLOOCPrefix, Is.EqualTo('='));
            Assert.That(ChatSelectChannel.Subtle, Is.Not.EqualTo(ChatSelectChannel.Emotes));
            Assert.That(ChatSelectChannel.SubtleLOOC, Is.Not.EqualTo(ChatSelectChannel.LOOC));
            Assert.That(ChatChannel.Subtle & ChatChannel.Emotes, Is.EqualTo(ChatChannel.None));
            Assert.That(ChatChannel.SubtleLOOC & ChatChannel.LOOC, Is.EqualTo(ChatChannel.None));
            Assert.That(CCVars.SubtleSoundEnabled.DefaultValue, Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
