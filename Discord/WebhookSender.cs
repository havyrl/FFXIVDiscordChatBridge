using System.Collections.Concurrent;
using System.Threading.Channels;
using Dalamud.Plugin.Services;
using Discord;
using Discord.WebSocket;
using Discord.Webhook;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;

namespace FFXIVDiscordBridgePlugin.Discord;

/// <summary>
/// Sends DiscordMessagePayloads to Discord channels via per-channel webhooks,
/// or directly to the admin's DM channel for payloads with IsDm = true.
/// Uses a single async queue to preserve message ordering and avoid rate-limit races.
/// Webhook clients are cached by URL and disposed on shutdown.
/// Identical messages within the configured DuplicateCheckMs window are silently dropped.
/// </summary>
public sealed class WebhookSender : IDisposable
{
    private readonly IPluginLog _log;
    private readonly IConfigStore _configStore;
    private readonly BotService _botService;
    private readonly ConcurrentDictionary<string, DiscordWebhookClient> _clientCache = new();
    private readonly ConcurrentDictionary<string, long> _recentlySent = new(StringComparer.Ordinal);
    private readonly Channel<DiscordMessagePayload> _queue =
        System.Threading.Channels.Channel.CreateUnbounded<DiscordMessagePayload>(
            new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    public WebhookSender(IPluginLog log, IConfigStore configStore, BotService botService)
    {
        _log = log;
        _configStore = configStore;
        _botService  = botService;
        _worker = Task.Run(ProcessQueueAsync);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Enqueues a message for delivery. Returns immediately; delivery is async.</summary>
    public void Enqueue(DiscordMessagePayload payload) => _queue.Writer.TryWrite(payload);

    /// <summary>Task-returning overload for use as Func&lt;DiscordMessagePayload, Task&gt; event handler.</summary>
    public Task EnqueueAsync(DiscordMessagePayload payload)
    {
        Enqueue(payload);
        return Task.CompletedTask;
    }

    // ── Queue worker ───────────────────────────────────────────────────────

    private async Task ProcessQueueAsync()
    {
        await foreach (var payload in _queue.Reader.ReadAllAsync(_cts.Token))
        {
            try
            {
                await SendAsync(payload);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[WebhookSender] Failed to send message to channel {Id}", payload.ChannelId);
            }
        }
    }

    private async Task SendAsync(DiscordMessagePayload payload)
    {
        if (payload.IsDm)
        {
            await SendDmAsync(payload);
            return;
        }

        if (string.IsNullOrWhiteSpace(payload.WebhookUrl))
        {
            _log.Warning("[WebhookSender] No webhook URL for channel {Id} — message dropped.", payload.ChannelId);
            return;
        }

        // ── Duplicate filter ───────────────────────────────────────────────
        var threshold = _configStore.Load().DuplicateCheckMs;
        if (threshold > 0)
        {
            var key = $"{payload.ChannelId}|{payload.Username}|{payload.Content}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_recentlySent.TryGetValue(key, out var sentAt) && (now - sentAt) < threshold)
            {
                _log.Debug("[WebhookSender] Duplicate suppressed ({Ms} ms old): {Key}", now - sentAt, key);
                return;
            }
            _recentlySent[key] = now;
        }

        var client = _clientCache.GetOrAdd(payload.WebhookUrl,
            url => new DiscordWebhookClient(url));

        var messageId = await client.SendMessageAsync(
            text: payload.Content,
            username: payload.Username,
            avatarUrl: payload.AvatarUrl,
            embeds: payload.Embeds,
            components: payload.Components);

        if (payload.ComponentTimeout.HasValue)
            _ = RemoveWebhookComponentsAfterAsync(client, messageId, payload.ComponentTimeout.Value);
    }

    // ── DM sending ─────────────────────────────────────────────────────────

    private async Task SendDmAsync(DiscordMessagePayload payload)
    {
        var socketClient = _botService.Client;
        if (socketClient is null)
        {
            _log.Warning("[WebhookSender] Bot not connected — DM dropped.");
            return;
        }

        var config = _configStore.Load();
        if (config.AdminDiscordUserId == 0)
        {
            _log.Warning("[WebhookSender] No admin user ID configured — DM dropped.");
            return;
        }

        var user = await socketClient.GetUserAsync(config.AdminDiscordUserId);
        if (user is null)
        {
            _log.Warning("[WebhookSender] Admin user {Id} not found — DM dropped.", config.AdminDiscordUserId);
            return;
        }

        var dmChannel = await user.CreateDMChannelAsync();

        var content = string.IsNullOrEmpty(payload.Content)
            ? null
            : $"**{payload.Username}**: {payload.Content}";

        var sentMsg = await dmChannel.SendMessageAsync(
            text: content,
            embeds: payload.Embeds,
            components: payload.Components);

        if (payload.ComponentTimeout.HasValue)
            _ = RemoveDmComponentsAfterAsync(sentMsg, payload.ComponentTimeout.Value);
    }

    // ── Component timeout helpers ──────────────────────────────────────────

    private async Task RemoveWebhookComponentsAfterAsync(DiscordWebhookClient client, ulong messageId, TimeSpan delay)
    {
        await Task.Delay(delay);
        try
        {
            await client.ModifyMessageAsync(messageId, msg => msg.Components = new ComponentBuilder().Build());
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[WebhookSender] Failed to remove components from message {Id}", messageId);
        }
    }

    private async Task RemoveDmComponentsAfterAsync(IUserMessage message, TimeSpan delay)
    {
        await Task.Delay(delay);
        try
        {
            await message.ModifyAsync(props => props.Components = new ComponentBuilder().Build());
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[WebhookSender] Failed to remove DM components from message {Id}", message.Id);
        }
    }

    // ── Disposal ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _queue.Writer.Complete();
        _cts.Cancel();
        _worker.GetAwaiter().GetResult();
        foreach (var client in _clientCache.Values)
            client.Dispose();
        _clientCache.Clear();
        _cts.Dispose();
    }
}
