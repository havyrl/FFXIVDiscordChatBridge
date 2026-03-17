using Discord;
using Discord.WebSocket;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Interactions;

/// <summary>
/// IDiscordActionHandler for the Duty Finder toggle button shown in /config channel info.
/// Custom ID schema: bridge:config:duty-toggle:&lt;channelId&gt;
/// Flips IsContentFinder on the matching ChannelMapping and updates the embed in-place.
/// Admin-only.
/// </summary>
public sealed class DutyToggleActionHandler(ILocalizer localizer, PermissionGuard guard, IConfigStore configStore)
    : IDiscordActionHandler
{
    private const string Prefix = "bridge:config:duty-toggle:";

    public bool CanHandle(string customId) => customId.StartsWith(Prefix);

    public async Task HandleAsync(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent button) return;

        if (!guard.IsAdmin(interaction.User.Id))
        {
            await interaction.RespondAsync(localizer.T("common.admin_only", interaction.UserLocale), ephemeral: true);
            return;
        }

        if (!ulong.TryParse(button.Data.CustomId[Prefix.Length..], out var channelId))
        {
            await interaction.RespondAsync("Invalid channel ID.", ephemeral: true);
            return;
        }

        var config  = configStore.Load();
        var mapping = config.ChannelMappings.FirstOrDefault(m => m.DiscordChannelId == channelId);
        if (mapping is null)
        {
            await interaction.RespondAsync(localizer.T("config.channel.info_not_configured", interaction.UserLocale), ephemeral: true);
            return;
        }

        mapping.IsContentFinder = !mapping.IsContentFinder;
        configStore.Save(config);

        // Rebuild the original embed with the updated duty flag and refresh all buttons
        var original = button.Message.Embeds.FirstOrDefault();
        if (original is not null)
        {
            var dutyKey = localizer.T("config.channel.info_duty");
            var builder = original.ToEmbedBuilder();
            builder.Fields.Clear();
            foreach (var f in original.Fields)
            {
                var value = f.Name == dutyKey ? (mapping.IsContentFinder ? "✅" : "❌") : f.Value;
                builder.AddField(f.Name, value, f.Inline);
            }

            var newComponents = ChannelInfoButtons.Build(
                localizer, channelId, mapping.IsContentFinder, mapping.IsPartyInvite, interaction.UserLocale);
            await button.UpdateAsync(msg =>
            {
                msg.Embed      = builder.Build();
                msg.Components = newComponents;
            });
        }
        else
        {
            var key = mapping.IsContentFinder ? "config.channel.duty_enabled" : "config.channel.duty_disabled";
            await interaction.RespondAsync(
                string.Format(localizer.T(key, interaction.UserLocale), channelId),
                ephemeral: true);
        }
    }
}
