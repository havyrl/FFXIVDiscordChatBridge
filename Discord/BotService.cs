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
    private readonly WebhookResolver _webhookResolver;
    private readonly PermissionGuard _permissionGuard;
    private readonly GameChatSender _chatSender;
    private readonly IFramework _framework;

    private DiscordSocketClient? _client;
    private InteractionService? _interactions;
    private CancellationTokenSource _cts = new();

    public bool IsConnected => _client?.ConnectionState == ConnectionState.Connected;
    public DiscordSocketClient? Client => _client;

    /// <summary>All guilds (servers) the bot is currently a member of. Empty when disconnected.</summary>
    public IReadOnlyList<(ulong Id, string Name)> AvailableGuilds
        => _client?.Guilds.Select(g => (g.Id, g.Name)).ToList()
           ?? (IReadOnlyList<(ulong, string)>)[];

    public BotService(IPluginLog log, IConfigStore configStore,
                      IServiceProvider services, IEnumerable<IDiscordActionHandler> actionHandlers,
                      IClientState clientState, SpecialCharsHandler specialChars,
                      WebhookResolver webhookResolver, PermissionGuard permissionGuard,
                      GameChatSender chatSender, IFramework framework)
    {
        _log = log;
        _configStore = configStore;
        _services = services;
        _actionHandlers = actionHandlers;
        _clientState = clientState;
        _specialChars = specialChars;
        _webhookResolver = webhookResolver;
        _permissionGuard = permissionGuard;
        _chatSender = chatSender;
        _framework = framework;

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
            DefaultRunMode    = RunMode.Async,
            LocalizationManager = new SlashCommandLocalizationManager(_services.GetRequiredService<ILocalizer>()),
        });

        _client.Log += OnLog;
        _client.Ready += OnReady;
        _client.InteractionCreated += OnInteraction;
        _client.ButtonExecuted += OnButtonExecuted;
        _client.ModalSubmitted += OnModalSubmitted;
        _client.MessageReceived += OnMessageReceived;
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

        var primaryGuildId = _configStore.Load().PrimaryGuildId;
        if (primaryGuildId != 0)
        {
            await _interactions.RegisterCommandsToGuildAsync(primaryGuildId);
            _log.Information("[BotService] Slash commands registered to guild {GuildId} ({Count} modules).",
                             primaryGuildId, _interactions.Modules.Count);
        }
        else
        {
            await _interactions.RegisterCommandsGloballyAsync();
            _log.Information("[BotService] Slash commands registered globally ({Count} modules).",
                             _interactions.Modules.Count);
        }

        // Set initial bot presence based on whether the player is already logged in
        await _client.SetStatusAsync(_clientState.IsLoggedIn ? UserStatus.Online : UserStatus.Idle);

        // Resolve any custom guild emotes for FFXIV special characters
        _specialChars.RefreshEmotes(_client);

        // Auto-create/resolve webhooks for all channel mappings that have none yet
        await _webhookResolver.ResolveAllAsync(_client);
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

    private async Task OnMessageReceived(SocketMessage message)
    {
        // Ignore bots, webhooks, and system messages — only process real user messages
        if (message.Source != MessageSource.User) return;

        var config  = _configStore.Load();
        var mapping = config.ChannelMappings.FirstOrDefault(
            m => !m.IsDm && m.DiscordChannelId == message.Channel.Id && m.BackChannelType.HasValue);
        if (mapping is null) return;

        if (!_permissionGuard.CanUseChatCommands(message.Author)) return;

        var gameCmd = ChatTypeHelper.GetGameCommand(mapping.BackChannelType!.Value);
        if (gameCmd is null) return;

        var text = message.Content;
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            await _framework.RunOnFrameworkThread(() => _chatSender.Execute($"{gameCmd} {text}"));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[BotService] Failed to forward back-channel message to FFXIV");
        }
    }

    private Task OnSlashCommandExecuted(SlashCommandInfo info, IInteractionContext ctx, IResult result)
    {
        if (!result.IsSuccess)
        {
            var ex = result is ExecuteResult er ? er.Exception : null;
            if (ex is not null)
                _log.Error(ex, "[BotService] Slash command '{Name}' exception", info.Name);
            else
                _log.Warning("[BotService] Slash command '{Name}' failed: {Error}", info.Name, result.ErrorReason);
        }
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
