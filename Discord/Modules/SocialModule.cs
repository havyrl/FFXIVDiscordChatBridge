using Discord;
using Discord.Interactions;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin.Discord.Modules;

/// <summary>
/// Slash commands for viewing the in-game social lists:
///   /friendlist — who of your friends is currently online
///   /fclist     — who of your FC members is currently online
/// Both commands accept an optional `show_offline` flag to include offline members.
/// Results of both commands are also available as /tell autocomplete suggestions.
/// </summary>
[DefaultMemberPermissions(GuildPermission.SendMessages)]
public sealed class SocialModule(ILocalizer localizer, PermissionGuard guard, SocialListService social)
    : LocalizedModuleBase(localizer)
{
    [SlashCommand("friendlist", "Shows your FFXIV friends list (online status).")]
    public async Task FriendListAsync(
        [Summary("show_offline", "Also show offline friends (default: false)")]
        bool showOffline = false)
    {
        if (!guard.CanViewStatus(Context.User))
        {
            await RespondAsync(T("common.no_permission"), ephemeral: true);
            return;
        }

        var onlineOnly = !showOffline;
        var friends    = await social.GetFriendsAsync(onlineOnly);

        var embed = BuildListEmbed(
            T("social.friends.title"),
            T(onlineOnly ? "social.friends.online_header" : "social.friends.all_header"),
            friends,
            T("social.friends.empty"));

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("fclist", "Shows your FC members list (online status).")]
    public async Task FcListAsync(
        [Summary("show_offline", "Also show offline FC members (default: false)")]
        bool showOffline = false)
    {
        if (!guard.CanViewStatus(Context.User))
        {
            await RespondAsync(T("common.no_permission"), ephemeral: true);
            return;
        }

        var onlineOnly = !showOffline;
        var members    = await social.GetFcMembersAsync(onlineOnly);

        var embed = BuildListEmbed(
            T("social.fc.title"),
            T(onlineOnly ? "social.fc.online_header" : "social.fc.all_header"),
            members,
            T("social.fc.empty"));

        await RespondAsync(embed: embed, ephemeral: true);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Embed BuildListEmbed(
        string title, string header, IReadOnlyList<string> names, string emptyText)
    {
        var builder = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(0x478CFF);

        if (names.Count == 0)
        {
            builder.WithDescription(emptyText);
        }
        else
        {
            // Discord embed field values are limited to 1024 chars; split into chunks.
            var lines  = names.Select(n => $"• {n}").ToList();
            var chunks = SplitIntoChunks(lines, maxCharsPerField: 1000);

            foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
            {
                var fieldName = index == 0 ? $"{header} ({names.Count})" : "\u200b";
                builder.AddField(fieldName, string.Join('\n', chunk));
            }
        }

        return builder.Build();
    }

    private static IEnumerable<List<string>> SplitIntoChunks(List<string> lines, int maxCharsPerField)
    {
        var current = new List<string>();
        var charCount = 0;

        foreach (var line in lines)
        {
            if (charCount + line.Length + 1 > maxCharsPerField && current.Count > 0)
            {
                yield return current;
                current  = [];
                charCount = 0;
            }
            current.Add(line);
            charCount += line.Length + 1;
        }

        if (current.Count > 0) yield return current;
    }
}
