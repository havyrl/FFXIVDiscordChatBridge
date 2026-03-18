using Dalamud.Game.Text;
using Discord;
using Discord.Interactions;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Discord.Interactions;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Modules;

/// <summary>
/// /channel — FFXIV→Discord channel mapping configuration.
/// Restricted to guild channels only.
/// Includes backchannel and webhook configuration.
/// </summary>
[CommandContextType(InteractionContextType.Guild)]
[Group("channel", "Manage FFXIV→Discord channel mappings.")]
public sealed class ChannelModule(ILocalizer localizer, PermissionGuard guard, IConfigStore configStore)
    : LocalizedModuleBase(localizer)
{
    [SlashCommand("add", "Forward a FFXIV chat type to a Discord channel.")]
    public async Task AddAsync(
        [Summary("channel", "Discord channel")] ITextChannel channel,
        [Summary("type", "FFXIV chat type slug (e.g. fc, say, tell)")][Autocomplete(typeof(ChatTypeAutocompleteHandler))] string type)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;
        if (!TryParseType(type, out var chatType)) { await RespondAsync(T("common.unknown_chat_type", type), ephemeral: true); return; }

        var config  = configStore.Load();
        var mapping = GetOrCreateMapping(config, channel.Id);

        if (mapping.InboundChatTypes.Contains(chatType)) { await RespondAsync(T("config.channel.already_mapped", type, channel.Id), ephemeral: true); return; }

        mapping.InboundChatTypes.Add(chatType);
        if (string.IsNullOrEmpty(mapping.Label)) mapping.Label = channel.Name;
        configStore.Save(config);

        await RespondAsync(T("config.channel.added", ChatTypeHelper.GetLocalizedName(chatType, Localizer, Context.Interaction.UserLocale), channel.Id), ephemeral: true);
    }

    [SlashCommand("remove", "Stop forwarding a FFXIV chat type to a Discord channel.")]
    public async Task RemoveAsync(
        [Summary("channel", "Discord channel")] ITextChannel channel,
        [Summary("type", "FFXIV chat type slug")][Autocomplete(typeof(ChatTypeAutocompleteHandler))] string type)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;
        if (!TryParseType(type, out var chatType)) { await RespondAsync(T("common.unknown_chat_type_short", type), ephemeral: true); return; }

        var config  = configStore.Load();
        var mapping = config.ChannelMappings.FirstOrDefault(m => m.DiscordChannelId == channel.Id);

        if (mapping is null || !mapping.InboundChatTypes.Remove(chatType)) { await RespondAsync(T("config.channel.not_mapped", type, channel.Id), ephemeral: true); return; }

        configStore.Save(config);
        await RespondAsync(T("config.channel.removed", ChatTypeHelper.GetLocalizedName(chatType, Localizer, Context.Interaction.UserLocale), channel.Id), ephemeral: true);
    }

    [SlashCommand("duty", "Enable or disable Duty Finder pop notifications for a channel.")]
    public async Task DutyAsync(
        [Summary("channel", "Discord channel")] ITextChannel channel,
        [Summary("enabled", "true to enable, false to disable")] bool enabled)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;

        var config  = configStore.Load();
        var mapping = GetOrCreateMapping(config, channel.Id);
        if (string.IsNullOrEmpty(mapping.Label)) mapping.Label = channel.Name;
        mapping.IsContentFinder = enabled;
        configStore.Save(config);

        await RespondAsync(T(enabled ? "config.channel.duty_enabled" : "config.channel.duty_disabled", channel.Id), ephemeral: true);
    }

    [SlashCommand("party", "Enable or disable party invite notifications for a channel.")]
    public async Task PartyAsync(
        [Summary("channel", "Discord channel")] ITextChannel channel,
        [Summary("enabled", "true to enable, false to disable")] bool enabled)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;

        var config  = configStore.Load();
        var mapping = GetOrCreateMapping(config, channel.Id);
        if (string.IsNullOrEmpty(mapping.Label)) mapping.Label = channel.Name;
        mapping.IsPartyInvite = enabled;
        configStore.Save(config);

        await RespondAsync(T(enabled ? "config.channel.party_enabled" : "config.channel.party_disabled", channel.Id), ephemeral: true);
    }

    [SlashCommand("info", "Show the mapping for the current channel.")]
    public async Task InfoAsync()
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;

        var config  = configStore.Load();
        var mapping = config.ChannelMappings.FirstOrDefault(m => m.DiscordChannelId == Context.Channel.Id);

        if (mapping is null) { await RespondAsync(T("config.channel.info_not_configured"), ephemeral: true); return; }

        var embed = BuildInfoEmbed(mapping, T("config.channel.info_title"),
            T("config.channel.info_inbound"), T("config.channel.info_duty"), T("config.channel.info_party"),
            backLabel: T("config.channel.info_back"), webhookLabel: T("config.channel.info_webhook"));

        var components = ChannelInfoButtons.Build(
            Localizer, mapping.DiscordChannelId,
            mapping.IsContentFinder, mapping.IsPartyInvite,
            Context.Interaction.UserLocale);

        await RespondAsync(embed: embed, components: components, ephemeral: true);
    }

    [SlashCommand("list", "List all current channel mappings.")]
    public async Task ListAsync()
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;

        var config = configStore.Load();
        if (config.ChannelMappings.Count == 0) { await RespondAsync(T("config.channel.no_mappings"), ephemeral: true); return; }

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

    [SlashCommand("backchannel", "Set or clear the Discord\u2192FFXIV back-channel for a mapping.")]
    public async Task BackchannelAsync(
        [Summary("channel", "Discord channel")] ITextChannel channel,
        [Summary("type", "FFXIV chat type slug, or 'none' to disable")][Autocomplete(typeof(ChatTypeAutocompleteHandler))] string type)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;

        var config  = configStore.Load();
        var mapping = config.ChannelMappings.FirstOrDefault(m => m.DiscordChannelId == channel.Id);

        if (mapping is null) { await RespondAsync(T("config.backchannel.no_mapping", channel.Id), ephemeral: true); return; }

        if (type.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            mapping.BackChannelType = null;
            configStore.Save(config);
            await RespondAsync(T("config.backchannel.disabled", channel.Id), ephemeral: true);
            return;
        }

        if (!TryParseType(type, out var chatType)) { await RespondAsync(T("common.unknown_chat_type_short", type), ephemeral: true); return; }

        mapping.BackChannelType = chatType;
        configStore.Save(config);
        await RespondAsync(T("config.backchannel.set", channel.Id, ChatTypeHelper.GetLocalizedName(chatType, Localizer, Context.Interaction.UserLocale)), ephemeral: true);
    }

    [SlashCommand("webhook", "Set the webhook URL for a channel mapping.")]
    public async Task WebhookAsync(
        [Summary("channel", "Discord channel")] ITextChannel channel,
        [Summary("url", "Webhook URL")] string url)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;

        var config  = configStore.Load();
        var mapping = GetOrCreateMapping(config, channel.Id);
        mapping.WebhookUrl = url;
        if (string.IsNullOrEmpty(mapping.Label)) mapping.Label = channel.Name;
        configStore.Save(config);

        await RespondAsync(T("config.webhook.set", channel.Id), ephemeral: true);
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

    private static Embed BuildInfoEmbed(ChannelMapping mapping, string title,
        string inboundLabel, string dutyLabel, string partyLabel,
        string? backLabel = null, string? webhookLabel = null)
    {
        var types = mapping.InboundChatTypes.Count > 0
            ? $"`{string.Join(", ", mapping.InboundChatTypes.Select(ChatTypeHelper.GetSlug))}`"
            : "*(none)*";

        var builder = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(0x478CFF)
            .AddField(inboundLabel, types, inline: false);

        if (backLabel is not null)
        {
            var back = mapping.BackChannelType.HasValue
                ? $"`{ChatTypeHelper.GetSlug(mapping.BackChannelType.Value)}`"
                : "*(none)*";
            builder.AddField(backLabel, back, inline: true);
        }

        if (webhookLabel is not null)
        {
            var webhook = string.IsNullOrEmpty(mapping.WebhookUrl) ? "*(none)*" : "\u2705 configured";
            builder.AddField(webhookLabel, webhook, inline: true);
        }

        builder
            .AddField(dutyLabel,  mapping.IsContentFinder ? "\u2705" : "\u274c", inline: true)
            .AddField(partyLabel, mapping.IsPartyInvite   ? "\u2705" : "\u274c", inline: true);

        return builder.Build();
    }

    private static bool TryParseType(string slug, out XivChatType result)
    {
        var match = ChatTypeHelper.All.FirstOrDefault(kv =>
            kv.Value.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

        if (match.Value is not null) { result = match.Key; return true; }

        result = default;
        return false;
    }
}
