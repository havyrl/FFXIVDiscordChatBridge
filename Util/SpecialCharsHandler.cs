using System.Text;
using Discord.WebSocket;

namespace FFXIVDiscordBridgePlugin.Util;

/// <summary>
/// Maps FFXIV private-use Unicode codepoints to readable Discord-friendly text.
/// Optionally resolves custom guild emotes (xivHq, xivAtl, xivAtr) once the bot connects.
/// Call <see cref="RefreshEmotes"/> in BotService.OnReady to pick up server emotes.
/// </summary>
public sealed class SpecialCharsHandler
{
    private readonly object _lock = new();
    private Dictionary<char, string> _map;

    private string? _hqEmote;
    private string? _atlEmote;
    private string? _atrEmote;

    public SpecialCharsHandler() => _map = BuildMap();

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Replaces FFXIV private-use characters in <paramref name="input"/> with Discord-safe text.</summary>
    public string Transform(string input)
    {
        if (input.Length == 0) return input;

        lock (_lock)
        {
            var sb = new StringBuilder(input);
            foreach (var c in input)
            {
                if (_map.TryGetValue(c, out var replacement))
                    sb.Replace(c.ToString(), replacement);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Scans all guilds for custom xivHq / xivAtl / xivAtr emotes and rebuilds the map.
    /// Call once after the bot reports Ready.
    /// </summary>
    public void RefreshEmotes(DiscordSocketClient client)
    {
        string? hq = null, atl = null, atr = null;
        foreach (var guild in client.Guilds)
        foreach (var emote in guild.Emotes)
        {
            if (emote.Name == "xivHq")  hq  = $"<:xivHq:{emote.Id}>";
            if (emote.Name == "xivAtl") atl = $"<:xivAtl:{emote.Id}>";
            if (emote.Name == "xivAtr") atr = $"<:xivAtr:{emote.Id}>";
        }

        lock (_lock)
        {
            _hqEmote  = hq;
            _atlEmote = atl;
            _atrEmote = atr;
            _map = BuildMap();
        }
    }

    // ── Map builder ────────────────────────────────────────────────────────

    private Dictionary<char, string> BuildMap()
    {
        var hq  = _hqEmote  ?? "❇";
        var atl = _atlEmote ?? "🟩";
        var atr = _atrEmote ?? "🟥";

        var map = new Dictionary<char, string>
        {
            { '\uE020', "あ"         },
            { '\uE021', "ア"         },
            { '\uE022', "🇪\u200B"  },
            { '\uE023', "_ｧ"        },
            { '\uE024', "_ᴀ"        },
            { '\uE025', "가"         },
            { '\uE026', "中"         },
            { '\uE027', "英"         },
            { '\uE028', "ₘ"          },
            { '\uE029', "分"         },

            { '\uE031', "⏰"         },
            { '\uE032', "⇟"          },
            { '\uE033', "🟉"         },
            { '\uE034', "🌱"         },
            { '\uE035', "🠗"          },
            { '\uE039', "💲"         },
            { '\uE03A', "🇪🇺\u200B" },
            { '\uE03B', "➕"         },
            { '\uE03C', hq           }, // HQ marker
            { '\uE03D', "📦"         },
            { '\uE03E', "⚂"          },
            { '\uE03F', "·"          },

            { '\uE040', atl          }, // auto-translate left bracket
            { '\uE041', atr          }, // auto-translate right bracket
            { '\uE042', "⬡"          },
            { '\uE043', "🚫"         },
            { '\uE044', "🔗"         },
            { '\uE048', "♢+"         },
            { '\uE049', "ʛ"          }, // Gil
            { '\uE04A', "⚪"         },
            { '\uE04B', "⬜"         },
            { '\uE04C', "❌"         },
            { '\uE04D', "△"          },
            { '\uE04E', "➕"         },

            { '\uE050', "🖰"         },
            { '\uE051', "🖰L"        },
            { '\uE052', "🖰R"        },
            { '\uE053', "🖰LR"       },
            { '\uE054', "🖱"         },
            { '\uE055', "🖰1"        },
            { '\uE056', "🖰2"        },
            { '\uE057', "🖰3"        },
            { '\uE058', "🖰4"        },
            { '\uE059', "🖰5"        },
            { '\uE05A', "…"          },
            { '\uE05B', "⌧"          },
            { '\uE05C', "⧇"          },
            { '\uE05D', "🌐"         },
            { '\uE05E', "🎯"         },
            { '\uE05F', "🗷"          },

            { '\uE060', "⁰"          },
            { '\uE061', "¹"          },
            { '\uE062', "²"          },
            { '\uE063', "³"          },
            { '\uE064', "⁴"          },
            { '\uE065', "⁵"          },
            { '\uE066', "⁶"          },
            { '\uE067', "⁷"          },
            { '\uE068', "⁸"          },
            { '\uE069', "⁹"          },
            { '\uE06A', "Lᴠ"         },
            { '\uE06B', "Sᴛ"         },
            { '\uE06C', "Nᴠ"         },
            { '\uE06D', "Aᴍ"         },
            { '\uE06E', "Pᴍ"         },
            { '\uE06F', "➨"          },

            { '\uE070', "❓"         },

            { '\uE0AF', "➕"         },

            { '\uE0B0', "🇪\u200B"  },
            { '\uE0B1', "❶"          },
            { '\uE0B2', "❷"          },
            { '\uE0B3', "❸"          },
            { '\uE0B4', "❹"          },
            { '\uE0B5', "❺"          },
            { '\uE0B6', "❻"          },
            { '\uE0B7', "❼"          },
            { '\uE0B8', "❽"          },
            { '\uE0B9', "❾"          },
            { '\uE0BB', "➲"          },
            { '\uE0BC', "☯️"         },
            { '\uE0BD', "♋️"         },
            { '\uE0BE', "🔽"         },
            { '\uE0BF', "☒"          },

            { '\uE0C0', "🌟"         },
            { '\uE0C1', "Ⅰ"          },
            { '\uE0C2', "Ⅱ"          },
            { '\uE0C3', "Ⅲ"          },
            { '\uE0C4', "Ⅳ"          },
            { '\uE0C5', "Ⅴ"          },
            { '\uE0C6', "Ⅵ"          },

            { '\uE0D0', "Lᴛ"         },
            { '\uE0D1', "Sᴛ"         },
            { '\uE0D2', "Eᴛ"         },
            { '\uE0D3', "Oᴢ"         },
            { '\uE0D4', "Sᴢ"         },
            { '\uE0D5', "Eᴢ"         },
            { '\uE0D6', "Hʟ"         },
            { '\uE0D7', "Hs"          },
            { '\uE0D8', "Hᴇ"         },
            { '\uE0D9', "本"          },
            { '\uE0DA', "服"          },
            { '\uE0DB', "艾"          },
        };

        // Block letters (xiv E071–E08A) → regional indicator letters + zero-width space
        // (prevents unintended flag emoji combinations)
        for (var i = 0; i < 26; i++)
        {
            var xivChar = (char)(0xE071 + i);
            map[xivChar] = char.ConvertFromUtf32('A' + 0x1F1A5 + i) + "\u200B";
        }

        // Number squares (xiv E08F–E0AF) → enclosed number Unicode
        // ⓪ is special; ①–⑲ are one sequence; ㉑–㉛ are another (xiv goes to 31, i=20 is unmapped)
        for (var i = 0; i <= 31; i++)
        {
            var xivChar = (char)(0xE08F + i);
            string? unicode = i switch
            {
                0            => char.ConvertFromUtf32('\u24EA'),           // ⓪
                > 0 and <= 19 => char.ConvertFromUtf32('\u2460' + i - 1), // ①–⑲
                >= 21        => char.ConvertFromUtf32('\u3251' + i - 21), // ㉑–㉛
                _            => null,                                      // i == 20, no mapping
            };
            if (unicode is not null)
                map[xivChar] = unicode;
        }

        return map;
    }
}
