# Entwicklungs-Referenz: FFXIVDiscordChatBridge

## Build & Run

```bash
# Build
dotnet build FFXIVDiscordChatBridge.sln

# Tests ausführen
dotnet test

# Release-Build
dotnet publish FFXIVDiscordChatBridge/FFXIVDiscordChatBridge.csproj -c Release
```

## Projektstruktur (Dateipfade)

```
FFXIVDiscordChatBridge/Program.cs          ← Einstiegspunkt
FFXIVDiscordChatBridge/Startup.cs          ← DI & Service-Setup
FFXIVDiscordChatBridge/Consumer/Discord.cs ← Discord-Listener
FFXIVDiscordChatBridge/Consumer/FFXIV.cs   ← FFXIV-Listener
FFXIVDiscordChatBridge/Producer/Discord.cs ← Sendet nach Discord
FFXIVDiscordChatBridge/Producer/FFXIV.cs   ← Sendet nach FFXIV
FFXIVHelpers/FFXIVByteHandler.cs           ← Byte-Parsing-Kern
FFXIVHelpers/DiscordClientWrapper.cs       ← Discord-Client
FFXIVHelpers/UsernameMapping.cs            ← Nutzer-Mapping
FFXIVHelpers/Persistence/FilePersistence.cs ← Datei-Persistenz
```

## Logging

- **NLog** konfiguriert in `FFXIVDiscordChatBridge/NLog.config`
- Ausgaben: Console, Datei, Debugger (Trace-Level)

## Commit-Konventionen

Versionize (Semantic Versioning via Conventional Commits):
- `feat:` → Minor-Version-Bump
- `fix:` → Patch-Version-Bump
- `BREAKING CHANGE:` → Major-Version-Bump
- `chore:`, `build:`, `docs:` → kein Version-Bump

## Tests

- `FFXIVHelpers.Test/` – Unit Tests für Helpers
  - Fixtures/, Extensions/, Persistence/, UsernameMapping/
- `FFXIVByteParser.Test/` – Testdaten (Binärdateien) für Byte-Parsing
  - ChatsFromOtherCharacters/, ChatsFromOwnCharacter/
- `BinaryFromLogGenerator/` – Tool zum Generieren von Test-Binärdaten aus FFXIV-Logs

## Ziel-Plattform (alt — Standalone-App)

- Windows (WinExe, Windows Forms)
- .NET 7.0 (auf net9.0 migriert 2026-03-16)
- Ausgabe: `discord-chat-bridge.exe`

---

## Neues Dalamud Plugin — Versionen (Stand 2026-03-16)

| Paket / Tool | Version | Hinweis |
|---|---|---|
| **.NET SDK** | **10.0** | Pflicht — Dalamud 14.x läuft auf .NET 10 |
| **TargetFramework** | `net10.0-windows` | im `.csproj` |
| **Dalamud DLLs** | lokal aus `%AppData%\XIVLauncher\addon\Hooks\dev\` | `Private=false`, nicht bundeln |
| **Dalamud Version (installiert)** | `14.0.4.1` | .NET 10, API-Level 14 |
| **DalamudPackager** | `2.1.12` | NuGet, `PrivateAssets="all"` |
| **Discord.Net** | `3.16.0` | inkl. `Discord.Net.Interactions` für Slash Commands |
| **Microsoft.Extensions.DependencyInjection** | `9.0.x` | für Discord-Modul-Injection |

### Dalamud API-Änderungen (wichtig!)
- ImGui-DLL: `Dalamud.Bindings.ImGui.dll` — Namespace `Dalamud.Bindings.ImGui` (nicht mehr `ImGuiNET`)
- Plugin Interface: `IDalamudPluginInterface` (nicht `DalamudPluginInterface`)
- DM-Support für Slash Commands: `[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]` (ersetzt veraltetes `[EnabledInDm(true)]`)
- Chat lesen: `IChatGui.ChatMessage` Event
- Chat schreiben: `ICommandManager.ProcessCommand("/say ...")`
- Game-Thread für Game-API: `IFramework.RunOnFrameworkThread()`

### Empfohlene Projektstruktur (neues Repo)
```
neues-repo/
  DiscordBridgePlugin/    ← neues Plugin (net10.0-windows)
  reference/
    FFXIVDiscordChatBridge/   ← alter Standalone-Ansatz
    Dalamud.DiscordBridge/    ← FFXIV→Discord Referenz (reiichi001)
```
