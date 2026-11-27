using System.Numerics;
using System.Threading.Tasks;
using Content.Client.Lobby.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._PS;

[TestFixture]
public sealed class LobbyLayoutTest
{
    private const float ScreenWidth = 2390f;
    private const float ScreenHeight = 1060f;
    private const float MainContainerMargin = 20f;
    private const float ChatMargin = 8f;

    [Test]
    public async Task LobbyChatLayoutIsStable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
        });

        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var lobby = new LobbyGui();
            lobby.SetChatWidth(650);
            lobby.SetChatOpen(true);
            lobby.SetMenuTransparent(true);
            Layout(lobby);

            var panel = lobby.FindControl<Control>("MainMenuPanel");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel!.ModulateSelfOverride?.A, Is.LessThan(1f),
                "transparent menu should have an alpha below 1");
            var center = panel.GlobalPosition.X + panel.Width / 2f;
            Assert.That(center, Is.EqualTo(ScreenWidth / 2f).Within(2f), $"panel center was {center}");

            var chat = lobby.FindControl<Control>("ChatPanel");
            Assert.That(chat, Is.Not.Null);
            Assert.That(chat!.Visible, Is.True, "open chat panel should be visible");
            var collapseButton = lobby.FindControl<Control>("ChatCollapseButton");
            Assert.That(collapseButton, Is.Not.Null);
            Assert.That(collapseButton!.Visible, Is.True, "main menu chat should be collapsible");
            Assert.That(chat.Width, Is.EqualTo(650f).Within(2f), $"chat width was {chat.Width}");
            Assert.That(chat.GlobalPosition.X + chat.Width,
                Is.EqualTo(ScreenWidth - MainContainerMargin - ChatMargin).Within(2f),
                $"chat right edge was {chat.GlobalPosition.X + chat.Width}");
            Assert.That(chat.Height,
                Is.EqualTo(ScreenHeight - MainContainerMargin * 2 - ChatMargin * 2).Within(2f),
                $"chat height was {chat.Height}");

            lobby.SetChatOpen(false);
            Layout(lobby);

            var chatButton = lobby.FindControl<Control>("ChatCollapsedPanel");
            Assert.That(chatButton, Is.Not.Null);
            Assert.That(chatButton!.Visible, Is.True, "collapsed chat button should be visible");
            Assert.That(chatButton.Width, Is.GreaterThanOrEqualTo(150f),
                $"chat button width was {chatButton.Width}");
            Assert.That(chatButton.Height, Is.GreaterThanOrEqualTo(56f),
                $"chat button height was {chatButton.Height}");
            var chatExpandButton = lobby.FindControl<Control>("ChatExpandButton");
            Assert.That(chatExpandButton, Is.Not.Null);
            Assert.That(chatExpandButton!.Width, Is.GreaterThanOrEqualTo(120f),
                $"inner chat button width was {chatExpandButton.Width}");
            Assert.That(chatExpandButton.Height, Is.GreaterThanOrEqualTo(32f),
                $"inner chat button height was {chatExpandButton.Height}");
            Assert.That(chatButton.GlobalPosition.X + chatButton.Width,
                Is.EqualTo(ScreenWidth - MainContainerMargin - ChatMargin).Within(2f),
                $"chat button right edge was {chatButton.GlobalPosition.X + chatButton.Width}");

            lobby.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
            Layout(lobby);

            var setup = lobby.FindControl<Control>("CharacterSetupPanel");
            Assert.That(setup, Is.Not.Null);
            Assert.That(setup!.Visible, Is.True, "character setup panel should be visible");
            Assert.That(chat.Visible, Is.True, "character setup should auto-open chat when there is room");
            Assert.That(setup.Width,
                Is.EqualTo(ScreenWidth - MainContainerMargin * 2 - 650f - ChatMargin * 2).Within(2f),
                $"character setup width was {setup.Width}");
            Assert.That(setup.GlobalPosition.X + setup.Width,
                Is.EqualTo(chat.GlobalPosition.X - ChatMargin).Within(2f),
                "character setup should leave a gap before the chat");
            Assert.That(collapseButton.Visible, Is.False,
                "character setup chat must not be collapsible");

            // Trying to close chat while editing must not give the editor the chat's space back.
            lobby.SetChatOpen(false);
            Layout(lobby);

            Assert.That(chat.Visible, Is.True, "character setup chat stays open when close is requested");
            Assert.That(setup.Width,
                Is.EqualTo(ScreenWidth - MainContainerMargin * 2 - 650f - ChatMargin * 2).Within(2f),
                $"character setup width changed to {setup.Width} after a close request");

            // On a narrow screen the editor keeps the whole area and chat is not offered at all.
            Layout(lobby, 1400f, 900f);
            lobby.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
            Layout(lobby, 1400f, 900f);

            Assert.That(chat.Visible, Is.False, "narrow screens should not show chat in character setup");
            Assert.That(chatButton.Visible, Is.False, "narrow screens should not offer a chat button in character setup");
            Assert.That(setup.Width,
                Is.EqualTo(1400f - MainContainerMargin * 2).Within(2f),
                $"narrow character setup width was {setup.Width}");
        });

        await pair.CleanReturnAsync();
    }

    private static void Layout(Control control, float width = ScreenWidth, float height = ScreenHeight)
    {
        control.InvalidateMeasure();
        control.Measure(new Vector2(width, height));
        control.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, height)));
    }
}
