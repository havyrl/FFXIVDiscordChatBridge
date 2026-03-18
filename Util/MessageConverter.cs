using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using Lumina.Excel.Sheets;

namespace FFXIVDiscordBridgePlugin.Util;

/// <summary>
/// Central message converter for both directions of the bridge.
///
/// FFXIV → Discord (<see cref="ToDiscord"/>):
///   Iterates SeString payloads and renders them as Discord markdown.
///   ItemPayload → [Name](Teamcraft/GarlandTools/Custom URL)
///   MapLinkPayload → 📍 [Zone (X, Y)](Teamcraft map URL)
///   TextPayload → SpecialCharsHandler-transformed text
///
/// Discord → FFXIV (<see cref="ToGameText"/>):
///   Transforms Discord text to a plain string safe for GameChatSender.
///   Teamcraft item URLs → [Item Name] via Lumina lookup.
/// </summary>
public sealed class MessageConverter(SpecialCharsHandler specialChars, IConfigStore configStore,
                                     IDataManager dataManager, IPluginLog log)
{
    // ── FFXIV → Discord ───────────────────────────────────────────────────

    public string ToDiscord(SeString message)
    {
        var sb = new StringBuilder();
        var payloads = message.Payloads;

        for (var i = 0; i < payloads.Count; i++)
        {
            switch (payloads[i])
            {
                case ItemPayload item:
                    {
                        // FFXIV item link structure in SeString:
                        //   ItemPayload → UIForeground → UIGlow → TextPayload("\uE0BB") → /UIGlow → /UIForeground → RawPayload
                        //   → TextPayload(" Item Name")   ← item name is OUTSIDE the link markers
                        //
                        // Use Lumina as the authoritative name source, then skip ahead past all
                        // payloads that belong to this item link (the inner \uE0BB marker AND
                        // the outer name TextPayload) so they are not output twice.
                        var hqSuffix = item.IsHQ ? $" {specialChars.Transform("\uE03C")}" : string.Empty;
                        var name = (LookupItemName(item.ItemId) ?? "?") + hqSuffix;

                        for (var j = i + 1; j < payloads.Count; j++)
                        {
                            if (payloads[j] is ItemPayload or MapLinkPayload) break;
                            if (payloads[j] is TextPayload tp && tp.Text is not null)
                            {
                                i = j; // consume this text payload
                                       // The first TextPayload is the inner \uE0BB marker; the second
                                       // (if present) is the item name outside the link. Once we find
                                       // a payload with content beyond the marker we're done skipping.
                                if (tp.Text.Replace("\uE0BB", "").Trim().Length > 0) break;
                            }
                        }

                        var url = BuildItemUrl(item.ItemId);
                        sb.Append($"[{name}]({url})");
                        break;
                    }

                case MapLinkPayload map:
                    {
                        uint mapRowId = 0;
                        try { mapRowId = map.TerritoryType.Value.Map.Value.RowId; }
                        catch (Exception ex) { log.Warning(ex, "[MessageConverter] Could not resolve map row ID"); }

                        var zone = map.PlaceName ?? "?";
                        var x = map.XCoord.ToString("F1", CultureInfo.InvariantCulture);
                        var y = map.YCoord.ToString("F1", CultureInfo.InvariantCulture);
                        var locale = configStore.Load().ItemLinkLocale;

                        if (mapRowId > 0)
                        {
                            // Angle brackets suppress Discord link preview
                            var url = $"<https://ffxivteamcraft.com/db/{locale}/map/{mapRowId}>";
                            sb.Append($"📍 [{zone} ({x}, {y})]({url})");
                        }
                        else
                        {
                            sb.Append($"📍 {zone} ({x}, {y})");
                        }

                        // Skip the TextPayload(s) that contain the original raw map text.
                        // The structure can be a single TextPayload("\uE0BB Zonename ( X, Y )")
                        // or split into TextPayload("\uE0BB") + TextPayload("Zonename ( X, Y )").
                        // Strategy: consume the \uE0BB marker payload, then also consume the
                        // immediately following TextPayload (the zone+coords text).
                        var skippedMarker = false;
                        for (var j = i + 1; j < payloads.Count; j++)
                        {
                            if (payloads[j] is ItemPayload or MapLinkPayload) break;
                            if (payloads[j] is TextPayload { Text: not null } tp)
                            {
                                i = j;
                                if (!skippedMarker && tp.Text.Contains('\uE0BB'))
                                {
                                    skippedMarker = true;
                                    continue; // also consume the next TextPayload
                                }
                                break;
                            }
                        }
                        break;
                    }

                case TextPayload text:
                    sb.Append(specialChars.Transform(text.Text ?? string.Empty));
                    break;

                    // UIForegroundPayload, UIGlowPayload, RawPayload, etc. → no t ext output
            }
        }

        return sb.ToString();
    }

    // ── Discord → FFXIV ───────────────────────────────────────────────────

    /// <summary>
    /// Converts a Discord message string to FFXIV-safe plain text.
    /// </summary>
    public string ToGameText(string discordText)
    {
        return discordText;
    }

    /// <summary>
    /// Builds a <see cref="SeString"/> from a Discord message, replacing any Teamcraft item URLs
    /// with proper in-game item link payloads so clicking the link opens the item tooltip locally.
    /// Returns <c>null</c> when no item URLs are present (caller can skip printing).
    /// </summary>
    public SeString? BuildLocalMessage(string discordText)
    {
        var matches = new List<Match>();
        foreach (var db in ItemDatabaseDefinition.Builtin)
        {
            matches.AddRange(db.ItemRegex.Matches(discordText).Cast<Match>());
        }
        if (matches.Count == 0) return null;

        var builder = new SeStringBuilder();
        var lastIndex = 0;

        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
                builder.AddText(discordText[lastIndex..match.Index]);

            if (uint.TryParse(match.Groups[1].Value, out var itemId))
            {
                var itemName = LookupItemName(itemId);
                if (itemName is not null)
                {
                    // Reproduce the standard FFXIV item-link SeString structure:
                    //   ItemPayload → UIForeground(549) → UIGlow(550) → TextPayload("\uE0BB")
                    //   → UIGlow(0) → UIForeground(0) → LinkTerminator → TextPayload(" Name")
                    builder
                        .Add(new ItemPayload(itemId, false))
                        .AddUiForeground(549)
                        .AddUiGlow(550)
                        .AddText("\uE0BB")
                        .AddUiGlow(0)
                        .AddUiForeground(0)
                        .AddText(itemName)
                        .Add(RawPayload.LinkTerminator);
                }
                else
                {
                    builder.AddText(match.Value);
                }
            }
            else
            {
                builder.AddText(match.Value);
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < discordText.Length)
            builder.AddText(discordText[lastIndex..]);

        return builder.Build();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private string BuildItemUrl(uint itemId)
    {
        var config = configStore.Load();

        var db = ItemDatabaseDefinition.Builtin
            .FirstOrDefault(d => d.Id == config.ItemDatabaseId);

        if (db is not null)
            return db.BuildUrl(itemId, config.ItemLinkLocale);

        // Custom database
        if (!string.IsNullOrWhiteSpace(config.CustomItemUrlTemplate))
        {
            var raw = config.CustomItemUrlTemplate.Replace("{id}", itemId.ToString());
            return $"<{raw}>";
        }

        return ItemDatabaseDefinition.Teamcraft.BuildUrl(itemId, config.ItemLinkLocale);
    }

    private string? LookupItemName(uint itemId)
    {
        try
        {
            var name = dataManager.GetExcelSheet<Item>()?.GetRow(itemId).Name.ToString();
            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[MessageConverter] Failed to look up item {Id}", itemId);
            return null;
        }
    }
}
