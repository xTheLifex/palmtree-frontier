using Content.Server.GameTicking.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [ViewVariables]
    public string? LobbyBackground { get; private set; }

    // Palmtree: forced lobby background (admin debug command); while set, the automatic rotation
    // is paused until the override is cleared.
    [ViewVariables]
    public string? LobbyBackgroundOverride { get; private set; }

    [ViewVariables]
    private List<ResPath>? _lobbyBackgrounds;

    private static readonly string[] WhitelistedBackgroundExtensions = new string[] {"png", "jpg", "jpeg", "webp"};

    private void InitializeLobbyBackground()
    {
        _lobbyBackgrounds = _prototypeManager.EnumeratePrototypes<LobbyBackgroundPrototype>()
            .Select(x => x.Background)
            .Where(x => WhitelistedBackgroundExtensions.Contains(x.Extension))
            .ToList();

        // Palmtree/Coyote: rotate the lobby background while players wait.
        Timer.SpawnRepeating(30000, CycleLobbyBackground, System.Threading.CancellationToken.None);
        RandomizeLobbyBackground();
    }

    // Palmtree/Coyote: cycles the lobby background and tells clients to refresh it.
    private void CycleLobbyBackground()
    {
        if (LobbyBackgroundOverride != null)
            return;

        if (_lobbyBackgrounds == null || _lobbyBackgrounds.Count == 0)
            return;

        RandomizeLobbyBackground();
        SendStatusToAll();
    }

    // Palmtree: admin command support - force a lobby background until the override is cleared.
    public void SetLobbyBackgroundOverride(string path)
    {
        LobbyBackgroundOverride = path;
        LobbyBackground = path;
        SendStatusToAll();
    }

    // Palmtree: admin command support - clear the forced background and resume the rotation.
    public void ClearLobbyBackgroundOverride()
    {
        LobbyBackgroundOverride = null;
        RandomizeLobbyBackground();
        SendStatusToAll();
    }

    private void RandomizeLobbyBackground() {
        LobbyBackground = _lobbyBackgrounds!.Any() ? _robustRandom.Pick(_lobbyBackgrounds!).ToString() : null;
    }
}
