using Dalamud.Plugin.Services;
using Discord;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;
using Lumina.Excel.Sheets;

namespace FFXIVDiscordBridgePlugin.Chat;

/// <summary>
/// IGameEventSource that fires a notification whenever the Duty Finder queue pops.
/// Messages are sent to all ChannelMappings with <see cref="ChannelMapping.IsContentFinder"/> = true.
/// Sends a rich embed with the duty artwork and a Join button.
/// </summary>
public sealed class DutyFinderEventSource : IGameEventSource
{
    public event Func<DiscordMessagePayload, Task>? OnDiscordMessage;

    private readonly IClientState _clientState;
    private readonly IPlayerState _playerState;
    private readonly IConfigStore _configStore;
    private readonly ILocalizer _localizer;
    private readonly IPluginLog _log;

    public DutyFinderEventSource(IClientState clientState, IPlayerState playerState,
                                 IConfigStore configStore, ILocalizer localizer, IPluginLog log)
    {
        _clientState = clientState;
        _playerState = playerState;
        _configStore = configStore;
        _localizer   = localizer;
        _log = log;
    }

    public void Initialize() => _clientState.CfPop += OnCfPop;

    // ── IClientState.CfPop handler ─────────────────────────────────────────

    private void OnCfPop(ContentFinderCondition condition)
    {
        if (OnDiscordMessage is null) return;

        var config = _configStore.Load();
        var targets = config.ChannelMappings.Where(m => m.IsContentFinder).ToList();
        if (targets.Count == 0) return;

        var dutyName    = condition.Name.ExtractText();
        var playerName  = _playerState.IsLoaded ? _playerState.CharacterName : "Unknown";
        var world       = _playerState.HomeWorld.ValueNullable?.Name.ToString() ?? "?";

        // Build duty icon URL from XIVAPI icon path convention
        var iconId      = (uint)condition.Image;
        var iconFolder  = iconId / 1000 * 1000;
        var iconUrl     = $"https://xivapi.com/i/{iconFolder:D6}/{iconId:D6}.png";

        var embed = new EmbedBuilder()
            .WithTitle(string.Format(_localizer.T("duty.ready"), dutyName))
            .WithColor(new Color(0x29, 0x7c, 0x00))
            .WithImageUrl(iconUrl)
            .WithCurrentTimestamp()
            .Build();

        var components = new ComponentBuilder()
            .WithButton(_localizer.T("duty.join_btn"), "bridge:duty:join", ButtonStyle.Success, new Emoji("⚔️"))
            .Build();

        _log.Debug("[DutyFinderEventSource] CfPop: {Duty} — notifying {Count} channel(s)", dutyName, targets.Count);

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
                ComponentTimeout = TimeSpan.FromSeconds(45),
            };
            _ = OnDiscordMessage.Invoke(payload);
        }
    }

    public void Dispose() => _clientState.CfPop -= OnCfPop;
}
