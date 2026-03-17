using Dalamud.Game.Text;
using Discord;
using Discord.Interactions;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Modules;

/// <summary>
/// /config — all bridge configuration commands.
/// Only the bridge admin may use these.
/// Groups: channel, backchannel, webhook, dm, permissions, link
/// </summary>
[Group("config", "Bridge configuration (admin only).")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class ConfigModule(ILocalizer localizer, PermissionGuard guard, IConfigStore configStore)
    : LocalizedModuleBase(localizer)
{
    // ── /config channel ────────────────────────────────────────────────────

    [Group("channel", "Manage FFXIV→Discord channel mappings.")]
    public sealed class ChannelGroup(ILocalizer localizer, PermissionGuard guard, IConfigStore configStore)
        : LocalizedModuleBase(localizer)
    {
        [SlashCommand("add", "Forward a FFXIV chat type to a Discord channel.")]
        public async Task AddAsync(
            [Summary("channel", "Discord channel")] ITextChannel channel,
            [Summary("type", "FFXIV chat type slug (e.g. fc, say, tell)")] string type)
        {
            if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

            if (!TryParseType(type, out var chatType))
            {
                await RespondAsync(T("common.unknown_chat_type", type), ephemeral: true);
                return;
            }

            var config  = configStore.Load();
            var mapping = GetOrCreateMapping(config, channel.Id);

            if (mapping.InboundChatTypes.Contains(chatType))
            {
                await RespondAsync(T("config.channel.already_mapped", type, channel.Id), ephemeral: true);
                return;
            }

            mapping.InboundChatTypes.Add(chatType);
            if (string.IsNullOrEmpty(mapping.Label)) mapping.Label = channel.Name;
            configStore.Save(config);

            await RespondAsync(T("config.channel.added", ChatTypeHelper.GetFancyName(chatType), channel.Id), ephemeral: true);
        }

        [SlashCommand("remove", "Stop forwarding a FFXIV chat type to a Discord channel.")]
        public async Task RemoveAsync(
            [Summary("channel", "Discord channel")] ITextChannel channel,
            [Summary("type", "FFXIV chat type slug")] string type)
        {
            if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

            if (!TryParseType(type, out var chatType))
            {
                await RespondAsync(T("common.unknown_chat_type_short", type), ephemeral: true);
                return;
            }

            var config  = configStore.Load();
            var mapping = config.ChannelMappings.FirstOrDefault(m => m.DiscordChannelId == channel.Id);

            if (mapping is null || !mapping.InboundChatTypes.Remove(chatType))
            {
                await RespondAsync(T("config.channel.not_mapped", type, channel.Id), ephemeral: true);
                return;
            }

            configStore.Save(config);
            await RespondAsync(T("config.channel.removed", ChatTypeHelper.GetFancyName(chatType), channel.Id), ephemeral: true);
        }

        [SlashCommand("info", "Show the mapping for the current channel.")]
        public async Task InfoAsync()
        {
            if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

            var config  = configStore.Load();
            var mapping = config.ChannelMappings.FirstOrDefault(m => m.DiscordChannelId == Context.Channel.Id);

            if (mapping is null)
            {
                await RespondAsync(T("config.channel.info_not_configured"), ephemeral: true);
                return;
            }

            var types = mapping.InboundChatTypes.Count > 0
                ? $"`{string.Join(", ", mapping.InboundChatTypes.Select(ChatTypeHelper.GetSlug))}`"
                : "*(none)*";
            var back = mapping.BackChannelType.HasValue
                ? $"`{ChatTypeHelper.GetSlug(mapping.BackChannelType.Value)}`"
                : "*(none)*";
            var webhook = string.IsNullOrEmpty(mapping.WebhookUrl) ? "*(none)*" : "✅ configured";

            var embed = new EmbedBuilder()
                .WithTitle(T("config.channel.info_title"))
                .WithColor(0x478CFF)
                .AddField(T("config.channel.info_inbound"),  types,   inline: false)
                .AddField(T("config.channel.info_back"),     back,    inline: true)
                .AddField(T("config.channel.info_webhook"),  webhook, inline: true)
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("list", "List all current channel mappings.")]
        public async Task ListAsync()
        {
            if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

            var config = configStore.Load();
            if (config.ChannelMappings.Count == 0)
            {
                await RespondAsync(T("config.channel.no_mappings"), ephemeral: true);
                return;
            }

            var lines = config.ChannelMappings.Select(m =>
            {
                var target = m.IsDm ? "DM" : $"<#{m.DiscordChannelId}>";
                var types  = m.InboundChatTypes.Count > 0
                    ? $"`{string.Join(", ", m.InboundChatTypes.Select(ChatTypeHelper.GetSlug))}`"
                    : "*(none)*";
                var back   = m.BackChannelType.HasValue ? $" | back→{ChatTypeHelper.GetSlug(m.BackChannelType.Value)}" : "";
                return $"{target}: {types}{back}";
            });

            var embed = new EmbedBuilder()
                .WithTitle(T("config.channel.list_title"))
                .WithColor(0x478CFF)
                .WithDescription(string.Join("\n", lines))
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);
        }
    }

    // ── /config backchannel ────────────────────────────────────────────────

    [SlashCommand("backchannel", "Set or clear the Discord→FFXIV back-channel for a mapping.")]
    public async Task BackchannelAsync(
        [Summary("channel", "Discord channel")] ITextChannel channel,
        [Summary("type", "FFXIV chat type slug, or 'none' to disable")] string type)
    {
        if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

        var config  = configStore.Load();
        var mapping = config.ChannelMappings.FirstOrDefault(m => m.DiscordChannelId == channel.Id);

        if (mapping is null)
        {
            await RespondAsync(T("config.backchannel.no_mapping", channel.Id), ephemeral: true);
            return;
        }

        if (type.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            mapping.BackChannelType = null;
            configStore.Save(config);
            await RespondAsync(T("config.backchannel.disabled", channel.Id), ephemeral: true);
            return;
        }

        if (!TryParseType(type, out var chatType))
        {
            await RespondAsync(T("common.unknown_chat_type_short", type), ephemeral: true);
            return;
        }

        mapping.BackChannelType = chatType;
        configStore.Save(config);
        await RespondAsync(T("config.backchannel.set", channel.Id, ChatTypeHelper.GetFancyName(chatType)), ephemeral: true);
    }

    // ── /config webhook ────────────────────────────────────────────────────

    [SlashCommand("webhook", "Set the webhook URL for a channel mapping.")]
    public async Task WebhookAsync(
        [Summary("channel", "Discord channel")] ITextChannel channel,
        [Summary("url", "Webhook URL")] string url)
    {
        if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

        var config  = configStore.Load();
        var mapping = GetOrCreateMapping(config, channel.Id);
        mapping.WebhookUrl = url;
        if (string.IsNullOrEmpty(mapping.Label)) mapping.Label = channel.Name;
        configStore.Save(config);

        await RespondAsync(T("config.webhook.set", channel.Id), ephemeral: true);
    }

    // ── /config dm ────────────────────────────────────────────────────────

    [Group("dm", "Configure FFXIV chat types forwarded to your DM.")]
    public sealed class DmGroup(ILocalizer localizer, PermissionGuard guard, IConfigStore configStore)
        : LocalizedModuleBase(localizer)
    {
        [SlashCommand("add", "Forward a FFXIV chat type to the admin's DM.")]
        public async Task AddAsync([Summary("type", "FFXIV chat type slug")] string type)
        {
            if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

            if (!TryParseType(type, out var chatType))
            {
                await RespondAsync(T("common.unknown_chat_type_short", type), ephemeral: true);
                return;
            }

            var config  = configStore.Load();
            var mapping = config.ChannelMappings.FirstOrDefault(m => m.IsDm)
                          ?? new ChannelMapping { IsDm = true, Label = "DM" };

            if (!config.ChannelMappings.Contains(mapping))
                config.ChannelMappings.Add(mapping);

            if (!mapping.InboundChatTypes.Contains(chatType))
                mapping.InboundChatTypes.Add(chatType);

            configStore.Save(config);
            await RespondAsync(T("config.dm.added", ChatTypeHelper.GetFancyName(chatType)), ephemeral: true);
        }

        [SlashCommand("remove", "Stop forwarding a FFXIV chat type to the admin's DM.")]
        public async Task RemoveAsync([Summary("type", "FFXIV chat type slug")] string type)
        {
            if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

            if (!TryParseType(type, out var chatType))
            {
                await RespondAsync(T("common.unknown_chat_type_short", type), ephemeral: true);
                return;
            }

            var config  = configStore.Load();
            var mapping = config.ChannelMappings.FirstOrDefault(m => m.IsDm);
            mapping?.InboundChatTypes.Remove(chatType);
            configStore.Save(config);

            await RespondAsync(T("config.dm.removed", ChatTypeHelper.GetFancyName(chatType)), ephemeral: true);
        }
    }

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
            if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

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
            if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

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
            if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

            if (!ulong.TryParse(id, out var discordId))
            {
                await RespondAsync(T("config.permissions.invalid_id"), ephemeral: true);
                return;
            }

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
            if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

            var config = configStore.Load();
            if (config.Whitelist.Count == 0)
            {
                await RespondAsync(T("config.permissions.empty"), ephemeral: true);
                return;
            }

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
        if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

        var config = configStore.Load();
        config.CharLinks.RemoveAll(l => l.DiscordUserId == user.Id);
        config.CharLinks.Add(new CharLink { DiscordUserId = user.Id, FfxivCharacter = character });
        configStore.Save(config);

        await RespondAsync(T("config.link.linked", character, user.Id), ephemeral: true);
    }

    [SlashCommand("unlink", "Remove the FFXIV↔Discord character link for a user.")]
    public async Task UnlinkAsync([Summary("user", "Discord user")] IUser user)
    {
        if (!guard.IsAdmin(Context.User.Id)) { await RespondAsync(T("common.admin_only"), ephemeral: true); return; }

        var config  = configStore.Load();
        var removed = config.CharLinks.RemoveAll(l => l.DiscordUserId == user.Id);
        configStore.Save(config);

        await RespondAsync(
            removed > 0 ? T("config.unlink.removed", user.Id) : T("config.unlink.not_found"),
            ephemeral: true);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ChannelMapping GetOrCreateMapping(PluginConfig config, ulong channelId)
    {
        var mapping = config.ChannelMappings.FirstOrDefault(m => m.DiscordChannelId == channelId);
        if (mapping is not null) return mapping;

        mapping = new ChannelMapping { DiscordChannelId = channelId };
        config.ChannelMappings.Add(mapping);
        return mapping;
    }

    private static bool TryParseType(string slug, out XivChatType result)
    {
        var match = ChatTypeHelper.All.FirstOrDefault(kv =>
            kv.Value.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

        if (match.Value is not null)
        {
            result = match.Key;
            return true;
        }

        result = default;
        return false;
    }
}
