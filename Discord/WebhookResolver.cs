using Dalamud.Plugin.Services;
using Discord;
using Discord.WebSocket;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;

namespace FFXIVDiscordBridgePlugin.Discord;

/// <summary>
/// Automatically resolves or creates Discord webhooks for all channel mappings
/// that don't have a webhook URL configured yet.
/// Called once on bot ready; resolved URLs are persisted to config.
/// Requires the bot to have ManageWebhooks permission in each channel.
/// </summary>
public sealed class WebhookResolver(IPluginLog log, IConfigStore configStore)
{
    private const string WebhookName = "FFXIV Bridge";

    public async Task ResolveAllAsync(DiscordSocketClient client)
    {
        var config  = configStore.Load();
        var changed = false;

        foreach (var mapping in config.ChannelMappings)
        {
            if (!string.IsNullOrWhiteSpace(mapping.WebhookUrl)) continue;
            if (mapping.IsDm || mapping.DiscordChannelId == 0) continue;

            var url = await TryResolveAsync(client, mapping.DiscordChannelId, mapping.Label);
            if (url is null) continue;

            mapping.WebhookUrl = url;
            changed = true;
        }

        if (changed)
            configStore.Save(config);
    }

    private async Task<string?> TryResolveAsync(DiscordSocketClient client, ulong channelId, string label)
    {
        try
        {
            if (client.GetChannel(channelId) is not IIntegrationChannel channel)
            {
                log.Warning("[WebhookResolver] Channel {Id} not found or not a text channel.", channelId);
                return null;
            }

            // Reuse an existing webhook this bot created in this channel
            var webhooks = await channel.GetWebhooksAsync();
            var existing = webhooks.FirstOrDefault(w =>
                !string.IsNullOrEmpty(w.Token) &&
                w.ChannelId == channelId &&
                w.Creator?.Id == client.CurrentUser.Id);

            if (existing is not null)
            {
                log.Information("[WebhookResolver] Reusing webhook {WId} for channel {CId}",
                    existing.Id, channelId);
                return $"https://discord.com/api/webhooks/{existing.Id}/{existing.Token}";
            }

            // Create a new webhook; name it after the mapping label if available
            var name    = string.IsNullOrWhiteSpace(label) ? WebhookName : $"{WebhookName} ({label})";
            var created = await channel.CreateWebhookAsync(name);
            log.Information("[WebhookResolver] Created webhook '{Name}' for channel {Id}", name, channelId);
            return $"https://discord.com/api/webhooks/{created.Id}/{created.Token}";
        }
        catch (Exception ex)
        {
            log.Warning(ex,
                "[WebhookResolver] Could not resolve webhook for channel {Id} — ManageWebhooks permission missing?",
                channelId);
            return null;
        }
    }
}
