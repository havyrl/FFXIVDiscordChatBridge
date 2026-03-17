using Dalamud.Plugin.Services;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using Lumina.Excel.Sheets;

namespace FFXIVDiscordBridgePlugin.Chat;

/// <summary>
/// IGameEventSource that fires a notification whenever the Duty Finder queue pops.
/// Messages are sent to all ChannelMappings with <see cref="ChannelMapping.IsContentFinder"/> = true.
/// The duty icon is used as the webhook avatar.
/// </summary>
public sealed class DutyFinderEventSource : IGameEventSource
{
    public event Func<DiscordMessagePayload, Task>? OnDiscordMessage;

    private readonly IClientState _clientState;
    private readonly IConfigStore _configStore;
    private readonly IPluginLog _log;

    public DutyFinderEventSource(IClientState clientState, IConfigStore configStore, IPluginLog log)
    {
        _clientState = clientState;
        _configStore = configStore;
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

        var dutyName = condition.Name.ExtractText();
        var content  = $"**Duty is ready:** {dutyName}";

        // Build duty icon URL from XIVAPI icon path convention
        var iconId     = (uint)condition.Image;
        var iconFolder = iconId / 1000 * 1000;
        var avatarUrl  = $"https://xivapi.com/i/{iconFolder:D6}/{iconId:D6}.png";

        var playerName = _clientState.LocalPlayer?.Name.ToString() ?? "Unknown";

        _log.Debug("[DutyFinderEventSource] CfPop: {Duty} — notifying {Count} channel(s)", dutyName, targets.Count);

        foreach (var mapping in targets)
        {
            var payload = new DiscordMessagePayload
            {
                ChannelId  = mapping.DiscordChannelId,
                WebhookUrl = mapping.WebhookUrl,
                Username   = playerName,
                Content    = content,
                AvatarUrl  = avatarUrl,
            };
            _ = OnDiscordMessage.Invoke(payload);
        }
    }

    public void Dispose() => _clientState.CfPop -= OnCfPop;
}
