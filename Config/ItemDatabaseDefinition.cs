using System.Text.RegularExpressions;

namespace FFXIVDiscordBridgePlugin.Config;

/// <summary>
/// Describes a single item-link target database: display name, URL template, and locale behaviour.
/// </summary>
/// <param name="Id">Stable key stored in PluginConfig.</param>
/// <param name="DisplayName">Human-readable name shown in the GUI.</param>
/// <param name="UrlTemplate">URL with {id} and optional {locale} placeholders.</param>
/// <param name="UsesLocale">Whether {locale} appears in the template.</param>
public sealed record ItemDatabaseDefinition(
    string Id,
    string DisplayName,
    string UrlTemplate,
    Regex ItemRegex,
    bool UsesLocale)
{
    // ── Built-in databases ─────────────────────────────────────────────────

    public static readonly ItemDatabaseDefinition Teamcraft = new(
        "teamcraft",
        "Teamcraft",
        "https://ffxivteamcraft.com/db/{locale}/item/{id}",
        new(@"https://ffxivteamcraft\.com/db/\w+/item/(\d+)(?:/[^\s]*)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        UsesLocale: true);

    public static readonly ItemDatabaseDefinition GarlandTools = new(
        "garlandtools",
        "GarlandTools",
        "https://www.garlandtools.org/db/#item/{id}",
        new(@"https://garlandtools\.org/db/#item/(\d+)(?:[^\s]*)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        UsesLocale: false);

    /// <summary>All built-in databases in display order. "Custom" is handled separately.</summary>
    public static readonly IReadOnlyList<ItemDatabaseDefinition> Builtin =
        [Teamcraft, GarlandTools];

    // ── URL building ───────────────────────────────────────────────────────
        
    /// <summary>
    /// Builds the full item URL and wraps it in angle brackets to suppress
    /// Discord embed previews: [Name](&lt;url&gt;).
    /// </summary>
    public string BuildUrl(uint itemId, string locale)
    {
        var url = UrlTemplate
            .Replace("{id}",     itemId.ToString())
            .Replace("{locale}", locale);

        return $"<{url}>";
    }
    
}
