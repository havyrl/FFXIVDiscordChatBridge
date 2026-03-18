using Dalamud.Game.Text;

namespace FFXIVDiscordBridgePlugin.Util;

/// <summary>
/// Slug and display-name mappings for all supported XivChatTypes.
/// Ported from reiichi001/Dalamud.DiscordBridge — XivChatTypeExtensions.
/// </summary>
public static class ChatTypeHelper
{
    public sealed record ChatTypeInfo(string Slug, string FancyName);

    public static readonly IReadOnlyDictionary<XivChatType, ChatTypeInfo> All =
        new Dictionary<XivChatType, ChatTypeInfo>
        {
            { XivChatType.None,              new("none",          "No Chat Type") },
            { XivChatType.Debug,             new("debug",         "Debug Messages") },
            { XivChatType.Urgent,            new("urgent",        "Urgent Messages") },
            { XivChatType.Notice,            new("notice",        "Server Notices") },
            { XivChatType.Say,               new("say",           "Say") },
            { XivChatType.Shout,             new("shout",         "Shout") },
            { XivChatType.TellOutgoing,      new("tellout",       "Tell (Outgoing)") },
            { XivChatType.TellIncoming,      new("tell",          "Tell (Incoming)") },
            { XivChatType.Party,             new("p",             "Party Chat") },
            { XivChatType.Alliance,          new("alliance",      "Alliance") },
            { XivChatType.Ls1,               new("ls1",           "Linkshell 1") },
            { XivChatType.Ls2,               new("ls2",           "Linkshell 2") },
            { XivChatType.Ls3,               new("ls3",           "Linkshell 3") },
            { XivChatType.Ls4,               new("ls4",           "Linkshell 4") },
            { XivChatType.Ls5,               new("ls5",           "Linkshell 5") },
            { XivChatType.Ls6,               new("ls6",           "Linkshell 6") },
            { XivChatType.Ls7,               new("ls7",           "Linkshell 7") },
            { XivChatType.Ls8,               new("ls8",           "Linkshell 8") },
            { XivChatType.FreeCompany,       new("fc",            "Free Company") },
            { XivChatType.NoviceNetwork,     new("nn",            "Novice Network") },
            { XivChatType.CustomEmote,       new("customemote",   "Custom Emote") },
            { XivChatType.StandardEmote,     new("standardemote", "Standard Emote") },
            { XivChatType.Yell,              new("yell",          "Yell") },
            { XivChatType.CrossParty,        new("cp",            "Cross-World Party") },
            { XivChatType.PvPTeam,           new("pvpt",          "PvP Team") },
            { XivChatType.CrossLinkShell1,   new("cwls1",         "Cross-World Linkshell 1") },
            { XivChatType.CrossLinkShell2,   new("cwls2",         "Cross-World Linkshell 2") },
            { XivChatType.CrossLinkShell3,   new("cwls3",         "Cross-World Linkshell 3") },
            { XivChatType.CrossLinkShell4,   new("cwls4",         "Cross-World Linkshell 4") },
            { XivChatType.CrossLinkShell5,   new("cwls5",         "Cross-World Linkshell 5") },
            { XivChatType.CrossLinkShell6,   new("cwls6",         "Cross-World Linkshell 6") },
            { XivChatType.CrossLinkShell7,   new("cwls7",         "Cross-World Linkshell 7") },
            { XivChatType.CrossLinkShell8,   new("cwls8",         "Cross-World Linkshell 8") },
            { XivChatType.Echo,              new("e",             "Echo") },
            { XivChatType.SystemMessage,     new("sysmsg",        "System Message") },
            { XivChatType.SystemError,       new("syserror",      "System Error") },
            { XivChatType.GatheringSystemMessage, new("gathersysmsg", "Gathering System Message") },
            { XivChatType.ErrorMessage,      new("errmsg",        "Error Message") },
            { XivChatType.RetainerSale,      new("retainersale",  "Retainer Sale") },
            { (XivChatType)55,               new("alarm",         "Alarm") },
            { (XivChatType)61,               new("npctalk",       "NPC Talk") },
            { (XivChatType)66,               new("synthmsg",      "Synthesis Message") },
            { (XivChatType)68,               new("npcannounce",   "NPC Announcement") },
            { (XivChatType)69,               new("fcannounce",    "Free Company Announcement") },
            { (XivChatType)70,               new("fclogin",       "Free Company Login/Logout") },
            { (XivChatType)73,               new("sign",          "Sign") },
            { (XivChatType)74,               new("random",        "Random Number") },
            { (XivChatType)75,               new("nnn",           "Novice Network Notifications") },
            { (XivChatType)80,               new("gmtell",        "GM Tell") },
            { (XivChatType)81,               new("gmsay",         "GM Say") },
            { (XivChatType)82,               new("gmshout",       "GM Shout") },
            { (XivChatType)83,               new("gmyell",        "GM Yell") },
            { (XivChatType)84,               new("gmp",           "GM Party Chat") },
            { (XivChatType)85,               new("gmfc",          "GM Free Company") },
            { (XivChatType)86,               new("gmls1",         "GM Linkshell 1") },
            { (XivChatType)87,               new("gmls2",         "GM Linkshell 2") },
            { (XivChatType)88,               new("gmls3",         "GM Linkshell 3") },
            { (XivChatType)89,               new("gmls4",         "GM Linkshell 4") },
            { (XivChatType)90,               new("gmls5",         "GM Linkshell 5") },
            { (XivChatType)91,               new("gmls6",         "GM Linkshell 6") },
            { (XivChatType)92,               new("gmls7",         "GM Linkshell 7") },
            { (XivChatType)93,               new("gmls8",         "GM Linkshell 8") },
            { (XivChatType)94,               new("gmnn",          "GM Novice Network") },
        };

    /// <summary>
    /// Chat types that originate from the game server / engine rather than a player character.
    /// These should not be posted with the local character's name or avatar.
    /// </summary>
    private static readonly HashSet<XivChatType> SystemTypes =
    [
        XivChatType.SystemMessage,
        XivChatType.SystemError,
        XivChatType.GatheringSystemMessage,
        XivChatType.ErrorMessage,
        XivChatType.Notice,
        XivChatType.Urgent,
        XivChatType.Debug,
        XivChatType.Echo,
        (XivChatType)55,  // Alarm
        (XivChatType)61,  // NPC Talk
        (XivChatType)66,  // Synthesis Message
        (XivChatType)68,  // NPC Announcement
        (XivChatType)69,  // Free Company Announcement
        (XivChatType)70,  // Free Company Login/Logout
        (XivChatType)74,  // Random Number
        (XivChatType)75,  // Novice Network Notifications
    ];

    public static bool IsSystemType(XivChatType type)
        => SystemTypes.Contains((XivChatType)((int)type & 0x7F));

    public static string GetSlug(XivChatType type)
        => All.TryGetValue((XivChatType)((int)type & 0x7F), out var info) ? info.Slug : type.ToString();

    public static string GetFancyName(XivChatType type)
        => All.TryGetValue((XivChatType)((int)type & 0x7F), out var info) ? info.FancyName : type.ToString();

    /// <summary>
    /// Returns the localized display name for <paramref name="type"/> using the given Discord locale.
    /// Falls back to <see cref="GetFancyName"/> if no locale key is found.
    /// </summary>
    public static string GetLocalizedName(XivChatType type, ILocalizer localizer, string? discordLocale = null)
    {
        var key = $"chattype.{GetSlug(type)}";
        var result = localizer.T(key, discordLocale);
        return result != key ? result : GetFancyName(type);
    }

    private static readonly IReadOnlyDictionary<XivChatType, string> GameCommands =
        new Dictionary<XivChatType, string>
        {
            { XivChatType.Say,              "/say" },
            { XivChatType.Yell,             "/yell" },
            { XivChatType.Shout,            "/shout" },
            { XivChatType.Party,            "/p" },
            { XivChatType.FreeCompany,      "/fc" },
            { XivChatType.NoviceNetwork,    "/novice" },
            { XivChatType.Ls1,              "/ls1" },
            { XivChatType.Ls2,              "/ls2" },
            { XivChatType.Ls3,              "/ls3" },
            { XivChatType.Ls4,              "/ls4" },
            { XivChatType.Ls5,              "/ls5" },
            { XivChatType.Ls6,              "/ls6" },
            { XivChatType.Ls7,              "/ls7" },
            { XivChatType.Ls8,              "/ls8" },
            { XivChatType.CrossLinkShell1,  "/cwl1" },
            { XivChatType.CrossLinkShell2,  "/cwl2" },
            { XivChatType.CrossLinkShell3,  "/cwl3" },
            { XivChatType.CrossLinkShell4,  "/cwl4" },
            { XivChatType.CrossLinkShell5,  "/cwl5" },
            { XivChatType.CrossLinkShell6,  "/cwl6" },
            { XivChatType.CrossLinkShell7,  "/cwl7" },
            { XivChatType.CrossLinkShell8,  "/cwl8" },
        };

    /// <summary>
    /// Returns the in-game chat command prefix for a given chat type, or <c>null</c> if
    /// the type cannot be sent as a player message (e.g. system types, tells without target).
    /// </summary>
    public static string? GetGameCommand(XivChatType type)
        => GameCommands.TryGetValue(type, out var cmd) ? cmd : null;
}
