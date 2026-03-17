# Projekt-Memory: FFXIVDiscordChatBridge

> **Zweck dieser Datei:** Zentrale Erinnerungsdatei für alle Claude-Sessions in diesem Projekt.
> Sie enthält die wichtigsten Informationen auf einen Blick und verweist auf thematische Detaildateien.
> **Beim Start jeder Session einlesen.**

---

## Projekt auf einen Blick

**Was ist das?** Eine Windows-Anwendung (.NET 7, WinExe), die Discord-Kanäle mit dem FFXIV-In-Game-Chat bidirektional verbindet.

**Version:** 2.4.2 (CHANGELOG.md)

**Besitzer/Maintainer:** havyrl (GitHub-Nutzername aus Repo-Pfad)

**Kritische Einschränkungen:**
- Läuft nur auf Windows
- Benötigt einen dedizierten FFXIV-Account (nicht den Hauptaccount)
- FFXIV muss im Fenstermodus laufen (DirectX 11)
- .NET 7.0 Runtime erforderlich

---

## Architektur-Überblick

```
FFXIVDiscordChatBridge/   ← Haupt-App (WinExe)
  Consumer/               ← Empfängt Nachrichten von Discord & FFXIV
  Producer/               ← Sendet Nachrichten an Discord & FFXIV
  Program.cs              ← Einstiegspunkt
  Startup.cs              ← DI-Konfiguration & Service-Initialisierung

FFXIVHelpers/             ← Shared Library (net7.0)
  Models/                 ← Datenmodelle (Character, FromFFXIV, Mapping, ConfirmationState)
  Persistence/            ← IPersistence, FilePersistence
  Extensions/             ← DiscordMessageConverter, etc.
  FFXIVByteHandler.cs     ← FFXIV-Speicher-Parsing (via Sharlayan)
  DiscordClientWrapper.cs ← Discord-API-Wrapper
  UsernameMapping.cs      ← Discord ↔ FFXIV Nutzernamen-Verknüpfung

FFXIVHelpers.Test/        ← Unit-Tests
FFXIVByteParser.Test/     ← Testdaten für Byte-Parsing
BinaryFromLogGenerator/   ← Hilfstool: generiert Test-Binärdaten aus FFXIV-Logs
```

**Pattern:** Producer-Consumer, Dependency Injection (Microsoft.Extensions)

---

## Wichtigste Abhängigkeiten

| Paket | Version | Zweck |
|---|---|---|
| Discord.Net | 3.16.0 | Discord API Client |
| Sharlayan | 8.0.1 | FFXIV Memory Reader |
| InputSimulator | 1.0.4 | Tastatureingaben in FFXIV simulieren |
| Microsoft.Extensions.* | 8-9.x | DI, Logging, Konfiguration |
| NLog | 5.3.4 | Strukturiertes Logging |
| Polly (via Http) | 8.x | HTTP Resilience (Retry/Timeout) |

---

## CI/CD & Tooling

- **Build/Release:** `.github/workflows/build-release-publish.yml` – Multi-Platform (Win/Ubuntu/macOS), Tests, Codecov
- **Auto-Merge:** `.github/workflows/automerge.yml` – Dependabot PRs werden automatisch gemergt
- **Versionierung:** Versionize (Conventional Commits → Semver)
- **Dependabot:** Tägliche Updates für NuGet & GitHub Actions

---

## Thematische Memory-Dateien

| Datei | Thema |
|---|---|
| [architecture.md](architecture.md) | Detaillierte Architektur, Datenfluss, Komponenten |
| [development.md](development.md) | Setup, Build-Anweisungen, Tests, Konventionen |
| [decisions.md](decisions.md) | Architekturentscheidungen & Begründungen |
| [known-issues.md](known-issues.md) | Bekannte Bugs, Einschränkungen, TODOs |
| [sessions.md](sessions.md) | Verlauf wichtiger Session-Erkenntnisse |
| [project_pending_features.md](project_pending_features.md) | Features aus Dalamud.DiscordBridge (Referenz) die noch portiert werden sollen |
| [feedback_memory_location.md](feedback_memory_location.md) | Memories nur in `.memory/` speichern, nicht extern |
| [feedback_localization.md](feedback_localization.md) | Jede neue user-facing Zeichenkette muss in alle Locale-Dateien (en, de) eingetragen werden |

---

## Letzte Aktualisierung

2026-03-16 — Initiale Erstellung der Memory-Struktur.
