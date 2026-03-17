using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Discord;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;
using LuminaLogMessage = Lumina.Excel.Sheets.LogMessage;

namespace FFXIVDiscordBridgePlugin.Chat;

/// <summary>
/// IGameEventSource that fires when a party invite system message is detected in FFXIV chat.
/// Sends a notification embed to all ChannelMappings with <see cref="ChannelMapping.IsPartyInvite"/> = true.
///
/// Detection strategy:
///   1. Primary filter:   normalized chat type == SystemMessage (57), same as ChatEventSource uses
///   2. Secondary filter: EndsWith check against the invite suffix from LogMessage row 3
///                        (language-independent row ID, confirmed via startup scan).
/// </summary>
public sealed class PartyInviteEventSource : IGameEventSource
{
    public event Func<DiscordMessagePayload, Task>? OnDiscordMessage;

    // LogMessage row ID for the party invite message (confirmed: row 3 = " hat dich in eine Gruppe eingeladen.").
    private const uint InviteLogMessageRow = 3;

    // Suffix loaded from LogMessage sheet at init time — language-agnostic detection.
    private string? _inviteSuffix;

    private readonly IChatGui _chatGui;
    private readonly IPlayerState _playerState;
    private readonly IConfigStore _configStore;
    private readonly IDataManager _dataManager;
    private readonly ILocalizer _localizer;
    private readonly IPluginLog _log;

    public PartyInviteEventSource(IChatGui chatGui, IPlayerState playerState,
                                   IConfigStore configStore, IDataManager dataManager,
                                   ILocalizer localizer, IPluginLog log)
    {
        _chatGui     = chatGui;
        _playerState = playerState;
        _configStore = configStore;
        _dataManager = dataManager;
        _localizer   = localizer;
        _log         = log;
    }

    public void Initialize()
    {
        _chatGui.ChatMessage += OnChatMessage;
        LoadInviteSuffix();
    }

    private void LoadInviteSuffix()
    {
        var sheet = _dataManager.GetExcelSheet<LuminaLogMessage>();
        var row   = sheet?.GetRow(InviteLogMessageRow);
        _inviteSuffix = row?.Text.ExtractText();

        if (_inviteSuffix is null)
            _log.Warning("[PartyInviteEventSource] Could not load invite suffix from LogMessage row {Row}", InviteLogMessageRow);
        else
            _log.Debug("[PartyInviteEventSource] Invite suffix loaded: {Suffix}", _inviteSuffix);
    }

    // ── IChatGui handler ───────────────────────────────────────────────────

    private void OnChatMessage(XivChatType type, int timestamp,
                                ref SeString sender, ref SeString message, ref bool isHandled)
    {
        // Primary filter: only system messages (normalized, same as ChatEventSource)
        if (((int)type & 0x7F) != (int)XivChatType.SystemMessage) return;
        if (OnDiscordMessage is null) return;

        var msgText = message.TextValue;
        _log.Debug("[PartyInviteEventSource] msg={Msg}", msgText);

        var config  = _configStore.Load();
        var targets = config.ChannelMappings.Where(m => m.IsPartyInvite).ToList();
        if (targets.Count == 0) return;

        // Secondary filter: suffix check against LogMessage row 5
        if (_inviteSuffix is null || !msgText.EndsWith(_inviteSuffix, StringComparison.Ordinal)) return;

        var inviterName = msgText[..^_inviteSuffix.Length].TrimEnd();
        var playerName  = _playerState.IsLoaded ? _playerState.CharacterName : "Unknown";
        var world       = _playerState.HomeWorld.ValueNullable?.Name.ToString() ?? "?";

        _log.Debug("[PartyInviteEventSource] Party invite from {Inviter}", inviterName);

        var embed = new EmbedBuilder()
            .WithTitle(string.Format(_localizer.T("party.invite"), inviterName))
            .WithColor(new Color(0x47, 0x8C, 0xFF))
            .Build();

        var components = new ComponentBuilder()
            .WithButton(_localizer.T("party.accept_btn"),  "bridge:party:accept",  ButtonStyle.Success, new Emoji("✅"))
            .WithButton(_localizer.T("party.decline_btn"), "bridge:party:decline", ButtonStyle.Danger,  new Emoji("❌"))
            .Build();

        foreach (var mapping in targets)
        {
            var payload = new DiscordMessagePayload
            {
                ChannelId        = mapping.DiscordChannelId,
                WebhookUrl       = mapping.WebhookUrl,
                IsDm             = mapping.IsDm,
                Username         = $"FFXIV System - {playerName}@{world}",
                AvatarUrl        = CharacterAvatarService.FallbackAvatarUrl,
                Content          = "",
                Embeds           = [embed],
                Components       = components,
                ComponentTimeout = TimeSpan.FromSeconds(300),
            };
            _ = OnDiscordMessage.Invoke(payload);
        }
    }

    public void Dispose() => _chatGui.ChatMessage -= OnChatMessage;
}
