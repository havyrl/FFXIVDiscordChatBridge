# Projekt-Memory: FFXIVDiscordChatBridge

> **Zweck dieser Datei:** Zentrale Erinnerungsdatei für alle Claude-Sessions in diesem Projekt.
> Sie enthält die wichtigsten Informationen auf einen Blick und verweist auf thematische Detaildateien.
> **Beim Start jeder Session einlesen.**

---

## Projekt auf einen Blick

**Was ist das?** Dalamud-Plugin für FFXIV (.NET 10, `net10.0-windows`), das FFXIV-Chat bidirektional mit Discord verbindet. Kein dedizierter Account, kein Fenstermodus — läuft direkt im Spielprozess via Dalamud (API Level 14).

**Version:** 0.1.0-beta (aktuell in Beta)

**Besitzer/Maintainer:** havyrl

**Kritische Einschränkungen:**
- Nur Windows (DirectX 11)
- Benötigt XIVLauncher + Dalamud

---

## Architektur-Überblick

```
FFXIVDiscordBridgePlugin/
  Chat/          ← IGameEventSource: ChatEventSource, DutyFinderEventSource, RetainerSaleEventSource
  Config/        ← PluginConfig (POCO), DalamudConfigStore
  Core/          ← Shared Interfaces (IConfigStore, IGameEventSource, IDiscordActionHandler)
  Discord/
    Modules/     ← Discord.Net Slash Command Module
    Interactions/ ← IDiscordActionHandler Implementierungen (z.B. TellReplyActionHandler)
  Gui/           ← ImGui Fenster (MainWindow, AdminRequestWindow)
  Util/          ← SpecialCharsHandler, CharacterAvatarService, …
  Plugin.cs      ← Dalamud-Einstiegspunkt / DI-Root
```

**Pattern:** Microsoft.Extensions.DI, System.Threading.Channels (Producer-Consumer), Discord.Net Interactions

---

## Wichtigste Abhängigkeiten

| Paket | Version | Zweck |
|---|---|---|
| Dalamud.NET.Sdk | 14.0.2 | Dalamud Plugin SDK |
| Discord.Net | 3.18.0 | Discord API Client (inkl. Interactions) |
| System.Reactive | 6.1.0 | Rx für Event-Streams |
| NetStone | 1.3.1 | Lodestone-API (Character-Lookup) |
| Microsoft.Extensions.DependencyInjection | 9.0.0 | DI-Container |

---

## CI/CD & Tooling

- **Release:** `.github/workflows/release.yml` — Tag-Push `v*` oder `workflow_dispatch`; baut, patcht Manifeste, erstellt GitHub Release mit ZIP
- **Versionierung:** Tag-Format `vMAJOR.MINOR.PATCH[-prerelease.DATUM]`, z.B. `v0.1.0-beta.20260317`
- **Custom Repo URL:** `https://raw.githubusercontent.com/havyrl/FFXIVDiscordChatBridge/main/repo.json`

---

## Thematische Memory-Dateien

| Datei | Thema |
|---|---|
| [architecture.md](architecture.md) | Detaillierte Architektur, Datenfluss, Komponenten |
| [development.md](development.md) | Setup, Build-Anweisungen, Tests, Konventionen |
| [decisions.md](decisions.md) | Architekturentscheidungen & Begründungen |
| [known-issues.md](known-issues.md) | Bekannte Bugs, Einschränkungen, TODOs |
| [sessions.md](sessions.md) | Verlauf wichtiger Session-Erkenntnisse |
| [project_pending_features.md](project_pending_features.md) | Portierte Features aus Dalamud.DiscordBridge — alle implementiert, dient als Implementierungsreferenz |
| [feedback_memory_location.md](feedback_memory_location.md) | Memories nur in `.memory/` speichern, nicht extern |
| [feedback_localization.md](feedback_localization.md) | Jede neue user-facing Zeichenkette muss in alle Locale-Dateien (en, de) eingetragen werden |
| [feedback_primary_constructors.md](feedback_primary_constructors.md) | C# Primary Constructor: Parameter nie gleichzeitig als Property speichern und direkt in Methoden benutzen (CS9107/CS9124) |
| [project_guild_command_access.md](project_guild_command_access.md) | Slash Commands in Guild-Channels nicht sichtbar (nur per DM) — offen, Ursache unklar |
| [project_pandorasbox_feature_ideas.md](project_pandorasbox_feature_ideas.md) | Feature-Ideen aus PandorasBox-Analyse: IPC-API, Post-Duty-Summary, Map-Link-Formatting, FATE-Notifications |
| [project_linkshell_api.md](project_linkshell_api.md) | FFXIVClientStructs LS/CWLS-Namen API (Typen, Methoden, deutsche Präfixe kk/wkk) |
| [project_backchannel.md](project_backchannel.md) | Discord→FFXIV: zwei Einstiegspunkte (BotService.OnMessageReceived + ChatModule), beide via GameChatSender |
| [reference_branding.md](reference_branding.md) | Branding-Ressourcen: Discord & FFXIV Fankit URLs, Plugin-Icon Dateien & SVG→PNG Konvertierung |

---

## Regeln

- **Kein Co-Authored-By in Commits.** Niemals `Co-Authored-By: Claude ...` in Commit-Messages einfügen.

---

## Letzte Aktualisierung

2026-03-17 — Memory bereinigt, MEMORY.md auf aktuellen Projektstand (Dalamud-Plugin) aktualisiert.
