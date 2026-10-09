using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Prototypes;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._PS.Commands;

/// <summary>
/// Palmtree: debug command to force a lobby background (by lobbyBackground prototype id) and pause
/// the automatic 30-second rotation until the override is cleared.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class SetLobbyBackgroundCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public string Command => "setlobbybackground";
    public string Description => "Forces a lobby background by prototype id until reset (debug; pauses rotation).";
    public string Help => $"{Command} <lobbyBackgroundId | reset>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError($"Usage: {Help}");
            return;
        }

        var ticker = _entMan.System<GameTicker>();

        if (args[0] == "reset")
        {
            ticker.ClearLobbyBackgroundOverride();
            shell.WriteLine("Lobby background override cleared; automatic rotation resumed.");
            return;
        }

        if (!_proto.TryIndex<LobbyBackgroundPrototype>(args[0], out var background))
        {
            shell.WriteError($"Unknown lobbyBackground prototype id '{args[0]}'.");
            return;
        }

        ticker.SetLobbyBackgroundOverride(background.Background.ToString());
        shell.WriteLine($"Lobby background forced to '{args[0]}'. Run '{Command} reset' to resume rotation.");
    }
}
