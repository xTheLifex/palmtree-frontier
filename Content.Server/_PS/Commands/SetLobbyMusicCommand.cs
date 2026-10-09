using System.Linq;
using Content.Server.Administration;
using Content.Server.Audio;
using Content.Shared.Administration;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Server._PS.Commands;

/// <summary>
/// Palmtree: debug command to force the lobby soundtrack - a single track (content file path) or a
/// whole sound collection - until it is reset. Only clients waiting in the lobby are notified, so
/// this never starts lobby music for players mid-round.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class SetLobbyMusicCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    public string Command => "setlobbymusic";
    public string Description => "Forces a lobby track or sound collection until reset (debug; affects clients waiting in the lobby).";
    public string Help => $"{Command} <trackPath | soundCollectionId | reset>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError($"Usage: {Help}");
            return;
        }

        var audio = _entMan.System<ContentAudioSystem>();

        if (args[0] == "reset")
        {
            audio.ClearForcedLobbyPlaylist();
            shell.WriteLine("Lobby music override cleared; the configured collection is back.");
            return;
        }

        // A leading slash means a content file path; otherwise try a sound collection first.
        if (!args[0].StartsWith('/') &&
            _proto.TryIndex<SoundCollectionPrototype>(args[0], out var collection))
        {
            var collectionPlaylist = collection.PickFiles.Select(x => x.ToString()).ToArray();
            if (collectionPlaylist.Length == 0)
            {
                shell.WriteError($"Sound collection '{args[0]}' has no files.");
                return;
            }

            audio.ForceLobbyPlaylist(collectionPlaylist);
            shell.WriteLine($"Lobby music forced to sound collection '{args[0]}' ({collectionPlaylist.Length} tracks).");
            return;
        }

        var path = args[0].StartsWith('/') ? args[0] : "/" + args[0];
        if (!_res.ContentFileExists(path))
        {
            shell.WriteError($"No content file at '{path}'.");
            return;
        }

        audio.ForceLobbyPlaylist(new[] { path });
        shell.WriteLine($"Lobby music forced to '{path}'. Run '{Command} reset' to restore the collection.");
    }
}
