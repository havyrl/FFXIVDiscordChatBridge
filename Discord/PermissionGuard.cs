using Discord.WebSocket;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;

namespace FFXIVDiscordBridgePlugin.Discord;

/// <summary>
/// Centralised permission check for Discord interactions.
/// The bridge admin (configured in-game) always passes. Everyone else is checked against the whitelist.
/// </summary>
public sealed class PermissionGuard(IConfigStore configStore)
{
    public bool IsAdmin(ulong userId)
        => configStore.Load().AdminDiscordUserId == userId;

    public bool CanSendToBackChannel(SocketUser user)
        => IsAdmin(user.Id) || HasPermission(user, p => p.CanSendToBackChannel);

    public bool CanSendTell(SocketUser user)
        => IsAdmin(user.Id) || HasPermission(user, p => p.CanSendTell);

    public bool CanUseChatCommands(SocketUser user)
        => IsAdmin(user.Id) || HasPermission(user, p => p.CanUseChatCommands);

    public bool CanViewStatus(SocketUser user)
        => IsAdmin(user.Id) || HasPermission(user, p => p.CanViewStatus);

    // ── Internals ──────────────────────────────────────────────────────────

    private bool HasPermission(SocketUser user, Func<WhitelistPermissions, bool> check)
    {
        var config = configStore.Load();

        // Check direct user whitelist entry
        var userEntry = config.Whitelist.FirstOrDefault(e => !e.IsRole && e.DiscordId == user.Id);
        if (userEntry is not null && check(userEntry.Permissions)) return true;

        // Check role whitelist entries (guild members only)
        if (user is SocketGuildUser guildUser)
        {
            foreach (var role in guildUser.Roles)
            {
                var roleEntry = config.Whitelist.FirstOrDefault(e => e.IsRole && e.DiscordId == role.Id);
                if (roleEntry is not null && check(roleEntry.Permissions)) return true;
            }
        }

        return false;
    }
}
