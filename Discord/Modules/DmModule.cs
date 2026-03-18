using Dalamud.Game.Text;
using Discord;
using Discord.Interactions;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Modules;

/// <summary>
/// /dm — FFXIV→Discord DM configuration.
/// Restricted to bot DMs only.
/// </summary>
[CommandContextType(InteractionContextType.BotDm)]
[Group("dm", "Configure FFXIV chat types forwarded to your DM.")]
public sealed class DmModule(ILocalizer localizer, PermissionGuard guard, IConfigStore configStore)
    : LocalizedModuleBase(localizer)
{
    [SlashCommand("add", "Forward a FFXIV chat type to the admin's DM.")]
    public async Task AddAsync(
        [Summary("type", "FFXIV chat type slug")][Autocomplete(typeof(ChatTypeAutocompleteHandler))] string type)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;
        if (!TryParseType(type, out var chatType)) { await RespondAsync(T("common.unknown_chat_type_short", type), ephemeral: true); return; }

        var config  = configStore.Load();
        var mapping = GetOrCreateDmMapping(config);

        if (!mapping.InboundChatTypes.Contains(chatType))
            mapping.InboundChatTypes.Add(chatType);

        configStore.Save(config);
        await RespondAsync(T("config.dm.added", ChatTypeHelper.GetLocalizedName(chatType, Localizer, Context.Interaction.UserLocale)), ephemeral: true);
    }

    [SlashCommand("remove", "Stop forwarding a FFXIV chat type to the admin's DM.")]
    public async Task RemoveAsync(
        [Summary("type", "FFXIV chat type slug")][Autocomplete(typeof(ChatTypeAutocompleteHandler))] string type)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;
        if (!TryParseType(type, out var chatType)) { await RespondAsync(T("common.unknown_chat_type_short", type), ephemeral: true); return; }

        var config  = configStore.Load();
        var mapping = config.ChannelMappings.FirstOrDefault(m => m.IsDm);
        mapping?.InboundChatTypes.Remove(chatType);
        configStore.Save(config);

        await RespondAsync(T("config.dm.removed", ChatTypeHelper.GetLocalizedName(chatType, Localizer, Context.Interaction.UserLocale)), ephemeral: true);
    }

    [SlashCommand("duty", "Enable or disable Duty Finder pop notifications in DM.")]
    public async Task DutyAsync([Summary("enabled", "true to enable, false to disable")] bool enabled)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;

        var config  = configStore.Load();
        var mapping = GetOrCreateDmMapping(config);
        mapping.IsContentFinder = enabled;
        configStore.Save(config);

        await RespondAsync(T(enabled ? "config.dm.duty_enabled" : "config.dm.duty_disabled"), ephemeral: true);
    }

    [SlashCommand("party", "Enable or disable party invite notifications in DM.")]
    public async Task PartyAsync([Summary("enabled", "true to enable, false to disable")] bool enabled)
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;

        var config  = configStore.Load();
        var mapping = GetOrCreateDmMapping(config);
        mapping.IsPartyInvite = enabled;
        configStore.Save(config);

        await RespondAsync(T(enabled ? "config.dm.party_enabled" : "config.dm.party_disabled"), ephemeral: true);
    }

    [SlashCommand("info", "Show the current DM mapping configuration.")]
    public async Task InfoAsync()
    {
        if (!await RequireAdminAsync(guard.IsAdmin)) return;

        var config  = configStore.Load();
        var mapping = config.ChannelMappings.FirstOrDefault(m => m.IsDm);

        if (mapping is null) { await RespondAsync(T("config.dm.info_not_configured"), ephemeral: true); return; }

        var embed = BuildInfoEmbed(mapping, T("config.dm.info_title"),
            T("config.channel.info_inbound"), T("config.channel.info_duty"), T("config.channel.info_party"));

        await RespondAsync(embed: embed, ephemeral: true);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ChannelMapping GetOrCreateDmMapping(PluginConfig config)
    {
        var mapping = config.ChannelMappings.FirstOrDefault(m => m.IsDm)
                      ?? new ChannelMapping { IsDm = true, Label = "DM" };
        if (!config.ChannelMappings.Contains(mapping))
            config.ChannelMappings.Add(mapping);
        return mapping;
    }

    private static Embed BuildInfoEmbed(ChannelMapping mapping, string title,
        string inboundLabel, string dutyLabel, string partyLabel)
    {
        var types = mapping.InboundChatTypes.Count > 0
            ? $"`{string.Join(", ", mapping.InboundChatTypes.Select(ChatTypeHelper.GetSlug))}`"
            : "*(none)*";

        return new EmbedBuilder()
            .WithTitle(title)
            .WithColor(0x478CFF)
            .AddField(inboundLabel, types, inline: false)
            .AddField(dutyLabel,  mapping.IsContentFinder ? "\u2705" : "\u274c", inline: true)
            .AddField(partyLabel, mapping.IsPartyInvite   ? "\u2705" : "\u274c", inline: true)
            .Build();
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
