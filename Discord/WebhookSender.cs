using System.Collections.Concurrent;
using System.Threading.Channels;
using Dalamud.Plugin.Services;
using Discord;
using Discord.Webhook;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;

namespace FFXIVDiscordBridgePlugin.Discord;

/// <summary>
/// Sends DiscordMessagePayloads to Discord channels via per-channel webhooks.
/// Uses a single async queue to preserve message ordering and avoid rate-limit races.
/// Webhook clients are cached by URL and disposed on shutdown.
/// Identical messages within the configured DuplicateCheckMs window are silently dropped.
/// </summary>
public sealed class WebhookSender : IDisposable
{
    private readonly IPluginLog _log;
    private readonly IConfigStore _configStore;
    private readonly ConcurrentDictionary<string, DiscordWebhookClient> _clientCache = new();
    private readonly ConcurrentDictionary<string, long> _recentlySent = new(StringComparer.Ordinal);
    private readonly Channel<DiscordMessagePayload> _queue =
        System.Threading.Channels.Channel.CreateUnbounded<DiscordMessagePayload>(
            new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    public WebhookSender(IPluginLog log, IConfigStore configStore)
    {
        _log = log;
        _configStore = configStore;
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
                _log.Verbose("[WebhookSender] Duplicate suppressed ({Ms} ms old): {Key}", now - sentAt, key);
                return;
            }
            _recentlySent[key] = now;
        }

        var client = _clientCache.GetOrAdd(payload.WebhookUrl,
            url => new DiscordWebhookClient(url));

        await client.SendMessageAsync(
            text: payload.Content,
            username: payload.Username,
            avatarUrl: payload.AvatarUrl,
            components: payload.Components);
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
