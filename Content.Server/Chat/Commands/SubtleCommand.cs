using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Server.Chat.Commands
{
    // Palmtree/Floof: ported from Coyote. A subtle emote only reaches players standing right next
    // to the source, is blocked by walls, and is never shown to ghosts.
    [AnyCommand]
    internal sealed class SubtleCommand : IConsoleCommand
    {
        [Dependency] private readonly IEntityManager _e = default!;

        public string Command => "subtle";
        public string Description => "Perform a subtle action that only people right next to you can see.";
        public string Help => "subtle <text>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (shell.Player is not { } player)
            {
                shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
                return;
            }

            if (player.AttachedEntity is not { Valid: true } entity)
                return;

            if (player.Status != SessionStatus.InGame)
                return;

            if (args.Length < 1)
                return;

            var message = string.Join(" ", args).Trim();
            if (string.IsNullOrEmpty(message))
                return;

            _e.System<ChatSystem>()
                .TrySendInGameICMessage(entity, message, InGameICChatType.Subtle, ChatTransmitRange.NoGhosts, false, shell, player);
        }
    }
}
