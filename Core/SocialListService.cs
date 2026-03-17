using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;

namespace FFXIVDiscordBridgePlugin.Core;

/// <summary>
/// Reads the in-game friend list and FC member list from game memory via FFXIVClientStructs.
/// If the proxy has no data yet, RequestData() is called automatically and we poll briefly
/// for the server response — no window needs to be opened manually.
/// </summary>
public sealed class SocialListService(IFramework framework, IDataManager dataManager)
{
    /// <summary>Returns friends as "Name@World" strings (online only by default).</summary>
    public Task<IReadOnlyList<string>> GetFriendsAsync(bool onlineOnly = true)
        => GetEntriesAsync<InfoProxyFriendList>(onlineOnly);

    /// <summary>Returns FC members as "Name@World" strings (online only by default).</summary>
    public Task<IReadOnlyList<string>> GetFcMembersAsync(bool onlineOnly = true)
        => GetEntriesAsync<InfoProxyFreeCompanyMember>(onlineOnly);

    // ── Core implementation ─────────────────────────────────────────────────

    private async Task<IReadOnlyList<string>> GetEntriesAsync<TProxy>(bool onlineOnly)
        where TProxy : unmanaged
    {
        // Trigger a server request if the proxy has no data yet, then poll briefly.
        var needsLoad = await framework.RunOnFrameworkThread(() => EnsureRequested<TProxy>());
        if (needsLoad)
        {
            for (var attempt = 0; attempt < 8; attempt++)   // up to ~4 s
            {
                await Task.Delay(500);
                var ready = await framework.RunOnFrameworkThread(() => HasData<TProxy>());
                if (ready) break;
            }
        }

        return await framework.RunOnFrameworkThread(() => ReadEntries<TProxy>(onlineOnly));
    }

    // ── Framework-thread helpers ────────────────────────────────────────────

    private static unsafe bool EnsureRequested<TProxy>() where TProxy : unmanaged
    {
        if (typeof(TProxy) == typeof(InfoProxyFriendList))
        {
            var p = InfoProxyFriendList.Instance();
            if (p is null || p->EntryCount > 0) return false;
            p->RequestData();
            return true;
        }
        if (typeof(TProxy) == typeof(InfoProxyFreeCompanyMember))
        {
            var p = InfoProxyFreeCompanyMember.Instance();
            if (p is null || p->EntryCount > 0) return false;
            p->RequestData();
            return true;
        }
        return false;
    }

    private static unsafe bool HasData<TProxy>() where TProxy : unmanaged
    {
        if (typeof(TProxy) == typeof(InfoProxyFriendList))
        {
            var p = InfoProxyFriendList.Instance();
            return p != null && p->EntryCount > 0;
        }
        if (typeof(TProxy) == typeof(InfoProxyFreeCompanyMember))
        {
            var p = InfoProxyFreeCompanyMember.Instance();
            return p != null && p->EntryCount > 0;
        }
        return false;
    }

    private unsafe IReadOnlyList<string> ReadEntries<TProxy>(bool onlineOnly)
        where TProxy : unmanaged
    {
        ReadOnlySpan<InfoProxyCommonList.CharacterData> span = [];

        if (typeof(TProxy) == typeof(InfoProxyFriendList))
        {
            var p = InfoProxyFriendList.Instance();
            if (p != null) span = p->CharDataSpan;
        }
        else if (typeof(TProxy) == typeof(InfoProxyFreeCompanyMember))
        {
            var p = InfoProxyFreeCompanyMember.Instance();
            if (p != null) span = p->CharDataSpan;
        }

        return ExtractNames(span, onlineOnly);
    }

    private IReadOnlyList<string> ExtractNames(
        ReadOnlySpan<InfoProxyCommonList.CharacterData> entries, bool onlineOnly)
    {
        var worldSheet = dataManager.GetExcelSheet<World>();
        var result     = new List<string>(entries.Length);

        foreach (ref readonly var entry in entries)
        {
            // Skip empty slots — ContentId == 0 means this entry was never populated.
            if (entry.ContentId == 0) continue;

            // Online status: OnlineStatus is a nested type; we check the raw bitmask via
            // ExtraFlags is not the right field for this — instead, Location == 0 means
            // the player is not in any zone (offline). Location > 0 = in a zone = online.
            if (onlineOnly && entry.Location == 0) continue;

            var name = entry.NameString;
            if (string.IsNullOrWhiteSpace(name)) continue;

            // HomeWorld and CurrentWorld are directly-accessible named fields.
            var homeWorldId = entry.HomeWorld;
            var worldName   = string.Empty;
            if (homeWorldId > 0 && worldSheet != null && worldSheet.TryGetRow(homeWorldId, out var worldRow))
                worldName = worldRow.Name.ExtractText() ?? string.Empty;

            result.Add(string.IsNullOrEmpty(worldName) ? name : $"{name}@{worldName}");
        }

        return result;
    }
}
