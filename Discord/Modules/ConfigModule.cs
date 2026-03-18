using Discord;
using Discord.Interactions;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Modules;

/// <summary>
/// /config — bridge administration: permissions whitelist and character links.
/// Context-unrestricted; permissions and character links are not channel-specific.
/// </summary>
[Group("config", "Bridge configuration (admin only).")]
public sealed class ConfigModule(ILocalizer localizer, PermissionGuard guard, IConfigStore configStore)
    : LocalizedModuleBase(localizer)
{
    // ── /config permissions ────────────────────────────────────────────────

    [Group("permissions", "Manage the user/role whitelist.")]
    public sealed class PermissionsGroup(ILocalizer localizer, PermissionGuard guard, IConfigStore configStore)
        : LocalizedModuleBase(localizer)
    {
        [SlashCommand("add-user", "Add a Discord user to the whitelist.")]
        public async Task AddUserAsync(
            [Summary("user", "Discord user")] IUser user,
            [Summary("backchannel", "Can send to back-channels")] bool backChannel = false,
            [Summary("tell", "Can use /tell")] bool tell = false,
            [Summary("chat", "Can use /say, /fc, etc.")] bool chat = false,
            [Summary("status", "Can view bridge status")] bool status = true)
        {
            if (!await RequireAdminAsync(guard.IsAdmin)) return;

            var config = configStore.Load();
            config.Whitelist.RemoveAll(e => !e.IsRole && e.DiscordId == user.Id);
            config.Whitelist.Add(new WhitelistEntry
            {
                DiscordId   = user.Id,
                IsRole      = false,
                Permissions = new WhitelistPermissions
                {
                    CanSendToBackChannel = backChannel,
                    CanSendTell          = tell,
                    CanUseChatCommands   = chat,
                    CanViewStatus        = status,
                },
            });
            configStore.Save(config);
            await RespondAsync(T("config.permissions.user_added", user.Id), ephemeral: true);
        }

        [SlashCommand("add-role", "Add a Discord role to the whitelist.")]
        public async Task AddRoleAsync(
            [Summary("role", "Discord role")] IRole role,
            [Summary("backchannel", "Can send to back-channels")] bool backChannel = false,
            [Summary("tell", "Can use /tell")] bool tell = false,
            [Summary("chat", "Can use /say, /fc, etc.")] bool chat = false,
            [Summary("status", "Can view bridge status")] bool status = true)
        {
            if (!await RequireAdminAsync(guard.IsAdmin)) return;

            var config = configStore.Load();
            config.Whitelist.RemoveAll(e => e.IsRole && e.DiscordId == role.Id);
            config.Whitelist.Add(new WhitelistEntry
            {
                DiscordId   = role.Id,
                IsRole      = true,
                Permissions = new WhitelistPermissions
                {
                    CanSendToBackChannel = backChannel,
                    CanSendTell          = tell,
                    CanUseChatCommands   = chat,
                    CanViewStatus        = status,
                },
            });
            configStore.Save(config);
            await RespondAsync(T("config.permissions.role_added", role.Id), ephemeral: true);
        }

        [SlashCommand("remove", "Remove a user or role from the whitelist.")]
        public async Task RemoveAsync([Summary("id", "Discord user or role ID")] string id)
        {
            if (!await RequireAdminAsync(guard.IsAdmin)) return;

            if (!ulong.TryParse(id, out var discordId)) { await RespondAsync(T("config.permissions.invalid_id"), ephemeral: true); return; }

            var config  = configStore.Load();
            var removed = config.Whitelist.RemoveAll(e => e.DiscordId == discordId);
            configStore.Save(config);

            await RespondAsync(
                removed > 0 ? T("config.permissions.removed", discordId) : T("config.permissions.not_found"),
                ephemeral: true);
        }

        [SlashCommand("list", "Show all whitelist entries.")]
        public async Task ListAsync()
        {
            if (!await RequireAdminAsync(guard.IsAdmin)) return;

            var config = configStore.Load();
            if (config.Whitelist.Count == 0) { await RespondAsync(T("config.permissions.empty"), ephemeral: true); return; }

            var lines = config.Whitelist.Select(e =>
            {
                var mention = e.IsRole ? $"<@&{e.DiscordId}>" : $"<@{e.DiscordId}>";
                var perms   = new List<string>();
                if (e.Permissions.CanSendToBackChannel) perms.Add("back-channel");
                if (e.Permissions.CanSendTell)          perms.Add("tell");
                if (e.Permissions.CanUseChatCommands)   perms.Add("chat");
                if (e.Permissions.CanViewStatus)        perms.Add("status");
                return $"{mention}: {(perms.Count > 0 ? string.Join(", ", perms) : "none")}";
            });

            var embed = new EmbedBuilder()
                .WithTitle(T("config.permissions.list_title"))
                .WithColor(0x478CFF)
                .WithDescription(string.Join("\n", lines))
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);
        }
    }

    // ── /config link ───────────────────────────────────────────────────────

    [SlashCommand("link", "Link an FFXIV character to a Discord user (enables Reply button).")]
    public async Task LinkAsync(
        [Summary("user", "Discord user")] IUser user,
        [Summary("character", "FFXIV character name, e.g. Firstname Lastname@World")] string character)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;

        var config = configStore.Load();
        config.CharLinks.RemoveAll(l => l.DiscordUserId == user.Id);
        config.CharLinks.Add(new CharLink { DiscordUserId = user.Id, FfxivCharacter = character });
        configStore.Save(config);

        await RespondAsync(T("config.link.linked", character, user.Id), ephemeral: true);
    }

    [SlashCommand("unlink", "Remove the FFXIV\u2194Discord character link for a user.")]
    public async Task UnlinkAsync([Summary("user", "Discord user")] IUser user)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;

        var config  = configStore.Load();
        var removed = config.CharLinks.RemoveAll(l => l.DiscordUserId == user.Id);
        configStore.Save(config);

        await RespondAsync(
            removed > 0 ? T("config.unlink.removed", user.Id) : T("config.unlink.not_found"),
            ephemeral: true);
    }
}
