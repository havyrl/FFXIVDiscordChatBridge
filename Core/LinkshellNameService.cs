using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace FFXIVDiscordBridgePlugin.Core;

/// <summary>
/// Reads Linkshell and Cross-World Linkshell names from game memory (via FFXIVClientStructs)
/// and returns a language-aware slug for use in Discord messages.
///
/// Must be called on the Framework thread (IChatGui.ChatMessage callbacks already are).
///
/// Slug format examples:
///   English: "ls3:MyShell",  "cwls3:Chocobo Gang"
///   German:  "kk3:MeinKK",   "wkk3:Chocobo Gang"
/// Falls back to the plain slug (e.g. "ls3") when the name is unavailable.
/// </summary>
public sealed class LinkshellNameService(IDataManager dataManager)
{
    private static readonly XivChatType[] LsTypes =
    [
        XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4,
        XivChatType.Ls5, XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8,
    ];

    private static readonly XivChatType[] CwlsTypes =
    [
        XivChatType.CrossLinkShell1, XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3, XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8,
    ];

    /// <summary>
    /// Returns the full slug string for a LS or CWLS chat type, including the shell name when
    /// available. For all other chat types returns <c>null</c> (caller uses ChatTypeHelper.GetSlug).
    /// Must be called on the Framework thread.
    /// </summary>
    public unsafe string? TryGetSlug(XivChatType type)
    {
        var lsIndex = Array.IndexOf(LsTypes, type);
        if (lsIndex >= 0)
        {
            var number = lsIndex + 1;
            var prefix = dataManager.Language == ClientLanguage.German ? "kk" : "ls";
            var name   = GetLsName((uint)lsIndex);
            return string.IsNullOrEmpty(name) ? $"{prefix}{number}" : $"{prefix}{number}:{name}";
        }

        var cwlsIndex = Array.IndexOf(CwlsTypes, type);
        if (cwlsIndex >= 0)
        {
            var number = cwlsIndex + 1;
            var prefix = dataManager.Language == ClientLanguage.German ? "wkk" : "cwls";
            var name   = GetCwlsName((uint)cwlsIndex);
            return string.IsNullOrEmpty(name) ? $"{prefix}{number}" : $"{prefix}{number}:{name}";
        }

        return null;
    }

    private static unsafe string? GetLsName(uint slot)
    {
        try
        {
            var proxy = InfoProxyLinkshell.Instance();
            if (proxy is null) return null;

            var entry = proxy->GetLinkshellInfo(slot);
            if (entry is null || entry->Id == 0) return null;

            return proxy->GetLinkshellName(entry->Id).ToString();
        }
        catch
        {
            return null;
        }
    }

    private static unsafe string? GetCwlsName(uint slot)
    {
        try
        {
            var proxy = InfoProxyCrossWorldLinkshell.Instance();
            if (proxy is null) return null;

            var name = proxy->GetCrossworldLinkshellName(slot);
            if (name is null) return null;

            return name->ToString();
        }
        catch
        {
            return null;
        }
    }
}
