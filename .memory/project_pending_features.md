---
name: Pending features to port from reference project
description: Features from C:\dev\c#\references\Dalamud.DiscordBridge that should be ported into FFXIVDiscordBridgePlugin using the modern DI/async architecture, without redundancy.
type: project
---

Features to port from `C:\dev\c#\references\Dalamud.DiscordBridge\Dalamud.DiscordBridge\` into the current plugin.

**Why:** The reference project is the old plugin. These features are useful but should be reimplemented in the modern structure (DI, System.Threading.Channels, Discord.Net slash commands).

**How to apply:** Each feature should be a new service/class registered in `Plugin.cs`. No static helpers, no background threads — use async patterns and DI throughout.

---

## Implementierte Features (2026-03-17)

| Feature | Implementiert in | Notizen |
|---|---|---|
| Spezielle FFXIV-Zeichen (Unicode → Discord Emotes) | `Util/SpecialCharsHandler.cs` | Singleton; `Transform()` in `ChatEventSource.FirePayloadsAsync`; `RefreshEmotes()` in `BotService.OnReady` |
| Duplikat-Filter | `Discord/WebhookSender.cs` | In-memory `ConcurrentDictionary`; Schwellwert `PluginConfig.DuplicateCheckMs` (default 5000ms) |
| Character Avatar via XIVAPI | `Core/CharacterAvatarService.cs` | HttpClient + ConcurrentDictionary-Cache; URL: xivapi.com character search |
| Retainer Sale Events | `Chat/RetainerSaleEventSource.cs` | IGameEventSource; `ChannelMapping.IsRetainerSale`; Item-Icon via `IDataManager` + beta.xivapi.com |
| Duty Finder Events | `Chat/DutyFinderEventSource.cs` | IGameEventSource; `IClientState.CfPop`; `ChannelMapping.IsContentFinder`; Duty-Icon via xivapi.com |
| Bot Presence (Online/Idle) | `Discord/BotService.cs` | `IClientState.Login/Logout`; initiale Presence in `OnReady` |

## Offene Features

*Keine bekannten offenen Features aus dem Referenzprojekt mehr.*
