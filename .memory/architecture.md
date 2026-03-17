# Architektur: FFXIVDiscordBridgePlugin (Dalamud)

> Diese Datei beschreibt die geplante Architektur des **neuen** Dalamud-Plugins.
> Der alte Standalone-Ansatz (FFXIVDiscordChatBridge) ist unter `FFXIVDiscordChatBridge/` als Referenz verfügbar.

---

## Projektstruktur

```
FFXIVDiscordBridgePlugin/
├── FFXIVDiscordBridgePlugin.csproj
├── Plugin.cs                          # Einstiegspunkt, DI-Setup, Dalamud-Lifecycle
│
├── Core/
│   ├── IGameEventSource.cs            # Abstraktion: Spielereignis → Discord
│   ├── IDiscordActionHandler.cs       # Abstraktion: Discord-Button → Spielaktion
│   ├── EventBus.cs                    # Interner Bus für lose Kopplung
│   └── PluginLifecycleManager.cs      # Koordiniert Start/Stop aller Services
│
├── Discord/
│   ├── DiscordService.cs              # Discord.Net Client-Wrapper, Lifecycle
│   ├── WebhookSender.cs               # Pro-Kanal-Webhook-Cache + Sende-Logik
│   ├── BotEventDispatcher.cs          # Routed Messages/Buttons → Handler
│   ├── Modules/
│   │   ├── InfoModule.cs              # /help, /status, /who
│   │   ├── ConfigModule.cs            # /config channel|dm|permissions|link
│   │   └── ChatModule.cs              # /say, /fc, /party, /yell, /shout, /tell
│   └── Interactions/
│       ├── TellReplyHandler.cs        # Button-Handler: Reply auf Tell
│       └── LinkedCharReplyHandler.cs  # Button-Handler: Reply für verknüpfte Chars
│
├── Chat/
│   ├── ChatBridgeEventSource.cs       # IGameEventSource: XivChatType → Discord
│   ├── ChatBridgeActionHandler.cs     # IDiscordActionHandler: Discord → FFXIV-Chat
│   ├── ChatTypeRegistry.cs            # Alle 46+ XivChatTypes, Metadaten, Prefixe
│   └── TellTracker.cs                 # Letzte Tells + Autocomplete-Datenquelle
│
├── Config/
│   ├── IConfigStore.cs                # Interface für Persistenz
│   ├── DalamudConfigStore.cs          # Impl. via IDalamudPluginInterface
│   ├── PluginConfig.cs                # Root-POCO + alle Nested Classes
│   └── ConfigValidator.cs            # Validierung bei Änderungen
│
├── Gui/
│   ├── PluginWindow.cs                # Haupt-ImGui-Fenster
│   ├── Tabs/
│   │   ├── GeneralTab.cs              # Bot-Token, Admin-ID, Status
│   │   ├── ChannelMappingTab.cs       # N:M Discord↔FFXIV-Mappings
│   │   ├── PermissionsTab.cs          # Whitelist-Verwaltung
│   │   └── CharLinkTab.cs             # FFXIV↔Discord-Verknüpfungen
│   └── GuiService.cs                  # Registriert Draw-Handler bei Dalamud
│
└── Util/
    ├── DiscoveryHelper.cs             # Auto-Discovery via Reflection
    ├── XivTextSanitizer.cs            # FFXIV-Sonderzeichen bereinigen
    └── RateLimiter.cs                 # Discord-API-Rate-Limiting
```

---

## Kernabstraktionen

**`IGameEventSource`**
- Erkennt Spielereignisse (Chat, Duty-Pop, Party-Invite etc.)
- Methoden: `Initialize()`, `Dispose()`
- Ereignis: `OnDiscordMessage(DiscordMessagePayload)` — Zielkanal, Text, optionale Buttons
- Auto-Discovery via Reflection beim Start

**`IDiscordActionHandler`**
- Reagiert auf Discord-Button-Klicks und Modal-Submissions
- Methoden: `CanHandle(string customId): bool`, `HandleAsync(SocketInteraction)`
- Custom-IDs: `bridge:<domain>:<action>:<encodedPayload>`
- Auto-Discovery via Reflection beim Start

**`IConfigStore`**
- Methoden: `Load(): PluginConfig`, `Save(PluginConfig)`, `OnChanged: event`
- Austauschbar via DI — eine Zeile ändern

---

## Nachrichtenfluss

### FFXIV → Discord
1. `IChatGui.ChatMessage` Event
2. `ChatBridgeEventSource` prüft: XivChatType in aktivem Mapping?
3. Baut `DiscordMessagePayload`: Username=`CharName@Server`, Text=`[prefix] Nachricht`
4. `WebhookSender.SendAsync()` → Webhook-Nachricht im Discord-Kanal
5. Bei Tell oder verlinktem Char: Reply-Button wird angehängt

### Discord → FFXIV (Text-Nachricht im Rückkanal)
1. `DiscordSocketClient.MessageReceived`
2. `BotEventDispatcher` prüft: Kanal hat ReturnChatType? Absender berechtigt?
3. `ChatBridgeActionHandler` bestimmt XivChatType
4. `IFramework.RunOnFrameworkThread()` → `ICommandManager.ProcessCommand()`

### Discord → FFXIV (Slash Command)
1. InteractionService empfängt z.B. `/tell name text`
2. `ChatModule` prüft Berechtigung, gibt Autocomplete aus TellTracker
3. `IFramework.RunOnFrameworkThread()` → `ProcessCommand("/tell ...")`

### Discord → FFXIV (Button/Modal)
1. User klickt Reply-Button: CustomId `bridge:tell:reply:<CharName@Server>`
2. `BotEventDispatcher` → `TellReplyHandler.CanHandle()` → `HandleAsync()`
3. Modal öffnet sich → User tippt Antwort → Modal-Submit
4. `IFramework.RunOnFrameworkThread()` → `ProcessCommand("/tell ...")`

---

## Kanal-Mapping-Modell

```
ChannelMapping:
  DiscordChannelId    (ulong)
  ChannelName         (string)          # nur für GUI
  IsDM                (bool)
  DMTargetUserId      (ulong?)
  ReceiveChatTypes    (List<XivChatType>) # FFXIV → dieser Kanal
  ReturnChatType      (XivChatType?)    # Discord → FFXIV (null = kein Rückkanal)
  WebhookUrl          (string)
  ShowReplyButton     (bool)
```

Ein Discord-Kanal kann mehrere FFXIV-Chattypen empfangen (N:M).
Slash Commands verfügbar in jedem Kanal mit ≥1 ReceiveChatType.

---

## Config-Modell (PluginConfig POCO)

```
PluginConfig
├── Version: int
├── BotToken: string                    # DPAPI-verschlüsselt
├── AdminDiscordUserId: ulong
├── Channels: List<ChannelMapping>      # siehe oben
├── Whitelist: List<WhitelistEntry>
│   └── WhitelistEntry
│       ├── Id: ulong                   # User-ID oder Rollen-ID
│       ├── IsRole: bool
│       ├── DisplayName: string
│       └── Permissions: PermissionFlags  # [Flags] Enum
├── CharLinks: List<CharLink>
│   └── CharLink
│       ├── CharName: string            # "Vorname Nachname"
│       ├── WorldName: string
│       ├── DiscordUserId: ulong
│       └── ShowAvatar: bool
└── Display: DisplayConfig
    ├── UsernameFormat: string          # Default: "{CharName}@{World}"
    ├── MessagePrefix: bool
    └── SanitizeXivSpecialChars: bool
```

---

## Berechtigungsmodell

```
Guard-Reihenfolge:
  1. AdminDiscordUserId → immer erlaubt
  2. Whitelist (UserId oder RoleId) → PermissionFlags prüfen
  3. Abgelehnt
```

`PermissionFlags` [Flags] Enum — vorbereitet, Granularität später ausbaubar.

---

## DI-Registrierung (Plugin.cs)

| Kategorie | Registrierung |
|---|---|
| `IConfigStore` | `DalamudConfigStore` — **einzige Zeile zum Austauschen** |
| `PluginConfig` | aus `IConfigStore.Load()` |
| Dalamud-APIs | `IChatGui`, `ICommandManager`, `IFramework`, `IClientState` als Instanzen |
| `WebhookSender`, `DiscordService`, `BotEventDispatcher` | Singleton |
| Slash-Command-Module | Singleton, bei InteractionService registriert |
| `IGameEventSource`-Impls | Auto-Discovery via `DiscoveryHelper` |
| `IDiscordActionHandler`-Impls | Auto-Discovery via `DiscoveryHelper` |
| `GuiService`, `PluginWindow`, Tabs | Singleton |

---

## Kritische Implementierungshinweise

- **Thread-Safety**: Discord-Events laufen auf Discord.Net-Threads → **immer** `IFramework.RunOnFrameworkThread()` für Dalamud/Game-APIs
- **Bot-Token**: DPAPI-verschlüsselt (`ProtectedData.Protect()`) — nie Klartext in JSON
- **Button Custom-IDs**: 100-Zeichen-Limit; lange Namen → TellTracker als Lookup, ID enthält nur Hash/Index
- **Discord offline**: `Channel<T>` mit Capacity-Limit puffert FFXIV-Events; nach Reconnect senden, ältere verwerfen
- **Dispose**: `Plugin.Dispose()` → Bot disconnect + alle IChatGui-Subscriptions abmelden + alle IGameEventSource disposen
- **Slash Command Registrierung**: Development = guild-spezifisch (sofort), Production = global (bis 1h Propagation)
- **Discord.Net Startup**: `_ = Task.Run(...)` — Dalamud-Startup-Thread darf nicht blockiert werden
- **ChatTypeRegistry**: definiert je XivChatType ob als Rückkanal sinnvoll → GUI filtert entsprechend

---

## Referenz-Repos

- `FFXIVDiscordChatBridge/` — alter Standalone-Ansatz (Sharlayan + InputSimulator)
- `Dalamud.DiscordBridge/` (geplant) — reiichi001-Fork als Referenz (FFXIV→Discord, 46+ Chattypen)
