using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._PS.Lobby.UI;

/// <summary>
/// Palmtree: the main lobby menu panel. Draws a pre-blurred copy of the lobby background underneath
/// the translucent <c>AngleRect</c> style, which gives the card a frosted-glass look without a
/// shader or a screen copy.
/// </summary>
public sealed class LobbyMenuBackdropPanel : PanelContainer
{
    /// <summary>
    /// Blurred background crop for the currently displayed lobby background.
    /// </summary>
    public Texture? Backdrop { get; set; }

    /// <summary>
    /// Blurred background crop for the incoming lobby background during a crossfade.
    /// </summary>
    public Texture? BackdropNext { get; set; }

    /// <summary>
    /// Crossfade progress for <see cref="BackdropNext"/>, from 0 to 1.
    /// </summary>
    public float BackdropFade { get; set; }

    /// <summary>
    /// Whether the backdrop should be drawn. Turned off together with menu transparency.
    /// </summary>
    public bool BackdropEnabled { get; set; } = true;

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (BackdropEnabled && Backdrop != null)
        {
            // The panel's own modulate makes the card translucent. The backdrop itself has to be
            // opaque, otherwise the sharp background would leak through and defeat the blur.
            var oldModulate = handle.Modulate;
            handle.Modulate = Color.White;
            handle.DrawTextureRect(Backdrop, PixelSizeBox);

            if (BackdropNext != null && BackdropFade > 0f)
            {
                handle.Modulate = Color.White.WithAlpha(Math.Clamp(BackdropFade, 0f, 1f));
                handle.DrawTextureRect(BackdropNext, PixelSizeBox);
            }

            handle.Modulate = oldModulate;
        }

        // Draws the AngleRect style over the backdrop.
        base.Draw(handle);
    }
}
