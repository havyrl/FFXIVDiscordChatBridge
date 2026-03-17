using Dalamud.Plugin.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;
using Microsoft.Extensions.DependencyInjection;

namespace FFXIVDiscordBridgePlugin.Discord;

/// <summary>
/// Owns the Discord WebSocket connection and the InteractionService.
/// Dispatches button/modal interactions to registered IDiscordActionHandler implementations.
/// Slash commands are handled via Discord.Net InteractionModules (auto-discovered).
/// </summary>
public sealed class BotService : IDisposable
{
    private readonly IPluginLog _log;
    private readonly IConfigStore _configStore;
    private readonly IServiceProvider _services;
    private readonly IEnumerable<IDiscordActionHandler> _actionHandlers;
    private readonly IClientState _clientState;
    private readonly SpecialCharsHandler _specialChars;

    private DiscordSocketClient? _client;
    private InteractionService? _interactions;
    private CancellationTokenSource _cts = new();

    public bool IsConnected => _client?.ConnectionState == ConnectionState.Connected;
    public DiscordSocketClient? Client => _client;

    public BotService(IPluginLog log, IConfigStore configStore,
                      IServiceProvider services, IEnumerable<IDiscordActionHandler> actionHandlers,
                      IClientState clientState, SpecialCharsHandler specialChars)
    {
        _log = log;
        _configStore = configStore;
        _services = services;
        _actionHandlers = actionHandlers;
        _clientState = clientState;
        _specialChars = specialChars;

        clientState.Login  += OnPlayerLogin;
        clientState.Logout += OnPlayerLogout;
    }

    private void OnPlayerLogin()               => _ = SetPresenceAsync(UserStatus.Online);
    private void OnPlayerLogout(int type, int code) => _ = SetPresenceAsync(UserStatus.Idle);

    private async Task SetPresenceAsync(UserStatus status)
    {
        if (_client is null) return;
        try { await _client.SetStatusAsync(status); }
        catch (Exception ex) { _log.Warning(ex, "[BotService] Failed to set presence to {Status}", status); }
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    public async Task StartAsync()
    {
        var config = _configStore.Load();
        if (string.IsNullOrWhiteSpace(config.BotToken))
        {
            _log.Warning("[BotService] No bot token configured — bot will not start.");
            return;
        }

        _cts = new CancellationTokenSource();

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            MessageCacheSize = 20,
            GatewayIntents = GatewayIntents.AllUnprivileged
                             | GatewayIntents.GuildMessages
                             | GatewayIntents.GuildWebhooks
                             | GatewayIntents.MessageContent
                             | GatewayIntents.DirectMessages,
        });

        _interactions = new InteractionService(_client, new InteractionServiceConfig
        {
            DefaultRunMode = RunMode.Async,
        });

        _client.Log += OnLog;
        _client.Ready += OnReady;
        _client.InteractionCreated += OnInteraction;
        _client.ButtonExecuted += OnButtonExecuted;
        _client.ModalSubmitted += OnModalSubmitted;
        _interactions.SlashCommandExecuted += OnSlashCommandExecuted;

        await _client.LoginAsync(TokenType.Bot, config.BotToken);
        await _client.StartAsync();
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        if (_client is not null)
        {
            await _client.StopAsync();
            await _client.LogoutAsync();
        }
    }

    // ── Discord event handlers ─────────────────────────────────────────────

    private async Task OnReady()
    {
        _log.Information("[BotService] Connected as {User}", _client!.CurrentUser.ToString());

        // Register all InteractionModules (slash commands) found via reflection
        await _interactions!.AddModulesAsync(typeof(Plugin).Assembly, _services);
        await _interactions.RegisterCommandsGloballyAsync();

        _log.Information("[BotService] Slash commands registered ({Count} modules).",
                         _interactions.Modules.Count);

        // Set initial bot presence based on whether the player is already logged in
        await _client.SetStatusAsync(_clientState.IsLoggedIn ? UserStatus.Online : UserStatus.Idle);

        // Resolve any custom guild emotes for FFXIV special characters
        _specialChars.RefreshEmotes(_client);
    }

    private async Task OnInteraction(SocketInteraction interaction)
    {
        var ctx = new SocketInteractionContext(_client!, interaction);
        await _interactions!.ExecuteCommandAsync(ctx, _services);
    }

    private async Task OnButtonExecuted(SocketMessageComponent component)
    {
        var handler = _actionHandlers.FirstOrDefault(h => h.CanHandle(component.Data.CustomId));
        if (handler is null)
        {
            await component.RespondAsync("Unknown action.", ephemeral: true);
            return;
        }
        await handler.HandleAsync(component);
    }

    private async Task OnModalSubmitted(SocketModal modal)
    {
        var handler = _actionHandlers.FirstOrDefault(h => h.CanHandle(modal.Data.CustomId));
        if (handler is null)
        {
            await modal.RespondAsync("Unknown action.", ephemeral: true);
            return;
        }
        await handler.HandleAsync(modal);
    }

    private Task OnSlashCommandExecuted(SlashCommandInfo info, IInteractionContext ctx, IResult result)
    {
        if (!result.IsSuccess)
            _log.Warning("[BotService] Slash command '{Name}' failed: {Error}", info.Name, result.ErrorReason);
        return Task.CompletedTask;
    }

    private Task OnLog(LogMessage msg)
    {
        var level = msg.Severity switch
        {
            LogSeverity.Critical or LogSeverity.Error => 4,
            LogSeverity.Warning => 3,
            _ => 1,
        };
        if (level == 4) _log.Error(msg.Exception, "[Discord] {Message}", msg.Message);
        else if (level == 3) _log.Warning("[Discord] {Message}", msg.Message);
        else _log.Debug("[Discord] {Message}", msg.Message);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _clientState.Login  -= OnPlayerLogin;
        _clientState.Logout -= OnPlayerLogout;
        StopAsync().GetAwaiter().GetResult();
        _interactions?.Dispose();
        _client?.Dispose();
        _cts.Dispose();
    }
}
