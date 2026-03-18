using Dalamud.Configuration;
using Dalamud.Game.Text;

namespace FFXIVDiscordBridgePlugin.Config;

/// <summary>Root configuration object — serialised by Dalamud to pluginConfigs/FFXIVDiscordBridgePlugin.json</summary>
[Serializable]
public sealed class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // ── Bot ────────────────────────────────────────────────────────────────
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// When set, slash commands are registered to this guild only (instant propagation).
    /// Leave at 0 for global registration (up to 1 h propagation delay).
    /// </summary>
    public ulong PrimaryGuildId { get; set; }

    // ── Duplicate filter ───────────────────────────────────────────────────
    /// <summary>
    /// Time window in milliseconds within which identical messages are suppressed.
    /// Set to 0 to disable. Default: 5000 ms.
    /// </summary>
    public int DuplicateCheckMs { get; set; } = 5000;

    // ── Bridge admin ───────────────────────────────────────────────────────
    /// <summary>Discord User ID of the bridge admin (configured in-game). Has unrestricted access.</summary>
    public ulong AdminDiscordUserId { get; set; }

    // ── Channel mappings ───────────────────────────────────────────────────
    public List<ChannelMapping> ChannelMappings { get; set; } = [];

    // ── Character ↔ Discord links ──────────────────────────────────────────
    public List<CharLink> CharLinks { get; set; } = [];

    // ── Whitelist ──────────────────────────────────────────────────────────
    public List<WhitelistEntry> Whitelist { get; set; } = [];

    // ── Tell tracker ──────────────────────────────────────────────────────
    /// <summary>Recently seen tell partners for /tell autocomplete (CharacterName@World).</summary>
    public List<string> RecentTellPartners { get; set; } = [];

    // ── Message formatting ────────────────────────────────────────────────
    /// <summary>ID of the active item database (matches ItemDatabaseDefinition.Id).</summary>
    public string ItemDatabaseId { get; set; } = "teamcraft";

    /// <summary>URL template for the custom item database. Use {id} as placeholder.</summary>
    public string CustomItemUrlTemplate { get; set; } = string.Empty;

    /// <summary>Locale segment used in database URLs (en / de / fr / ja).</summary>
    public string ItemLinkLocale { get; set; } = "de";
}

// ── Channel Mapping ────────────────────────────────────────────────────────

[Serializable]
public sealed class ChannelMapping
{
    /// <summary>Discord channel ID (or 0 for DM with AdminDiscordUserId).</summary>
    public ulong DiscordChannelId { get; set; }

    /// <summary>True if this mapping targets the admin's DM instead of a guild channel.</summary>
    public bool IsDm { get; set; }

    /// <summary>FFXIV chat types whose messages are forwarded to this Discord channel.</summary>
    public List<XivChatType> InboundChatTypes { get; set; } = [];

    /// <summary>
    /// When set: messages posted in this Discord channel are forwarded to FFXIV
    /// using this chat type. Null = read-only channel (no back-channel).
    /// </summary>
    public XivChatType? BackChannelType { get; set; }

    /// <summary>Webhook URL used to post FFXIV→Discord messages with character names/avatars.</summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>Human-readable label shown in the plugin GUI.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>When true, this channel receives Duty Finder pop notifications.</summary>
    public bool IsContentFinder { get; set; }

    /// <summary>When true, this channel receives party invite notifications.</summary>
    public bool IsPartyInvite { get; set; }

    /// <summary>When true, this channel receives Retainer Sale notifications (with item icon).</summary>
    public bool IsRetainerSale { get; set; }

    /// <summary>
    /// When true, the original Discord message is deleted after being forwarded to FFXIV via the back-channel.
    /// Requires the bot to have the "Manage Messages" permission in this channel.
    /// </summary>
    public bool DeleteBackChannelMessages { get; set; }
}

// ── Character ↔ Discord Link ───────────────────────────────────────────────

[Serializable]
public sealed class CharLink
{
    /// <summary>"Firstname Lastname@World"</summary>
    public string FfxivCharacter { get; set; } = string.Empty;

    public ulong DiscordUserId { get; set; }
}

// ── Whitelist ──────────────────────────────────────────────────────────────

[Serializable]
public sealed class WhitelistEntry
{
    public ulong DiscordId { get; set; }

    /// <summary>True = this is a role ID; false = this is a user ID.</summary>
    public bool IsRole { get; set; }

    public WhitelistPermissions Permissions { get; set; } = new();
}

[Serializable]
public sealed class WhitelistPermissions
{
    /// <summary>Can send messages via back-channels.</summary>
    public bool CanSendToBackChannel { get; set; }

    /// <summary>Can use /tell slash command.</summary>
    public bool CanSendTell { get; set; }

    /// <summary>Can use /say, /fc, /party and similar direct chat slash commands.</summary>
    public bool CanUseChatCommands { get; set; }

    /// <summary>Can view bridge status (/status, /who).</summary>
    public bool CanViewStatus { get; set; } = true;
}
