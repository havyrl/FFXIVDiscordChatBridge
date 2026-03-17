# Session-Verlauf

Wichtige Erkenntnisse, Entscheidungen und Änderungen aus einzelnen Arbeitssessions.

---

## 2026-03-17 — Tell-Partner-Cache Fix & Config-Migration

**Aktivität:** Bug behoben: `RecentTellPartners` speicherte Namen ohne `@World`, `PlayerPayload` wurde nicht genutzt.

**Erkenntnisse:**
- `PluginConfig` wird in `%AppData%\XIVLauncher\pluginConfigs\FFXIVDiscordBridgePlugin.json` persistiert — überlebt jedes Deployment. Bestehende fehlerhafte Daten müssen aktiv migriert werden.
- **Config-Migrations-Pattern:** Migration in `DalamudConfigStore.Load()` nach Deserialisierung, `SavePluginConfig()` nur wenn tatsächlich etwas geändert wurde.
- **FFXIV SeString Cross-World:** `sender.TextValue` konkateniert Name und World **ohne Trenner** (z.B. `"R'yloh TiaOdin"`). Für saubere Trennung immer `PlayerPayload.PlayerName` + `PlayerPayload.World` verwenden. Same-Server-Tells haben ggf. keinen `PlayerPayload` → Fallback auf `senderName + "@" + localWorld`.

**Geänderte Dateien:**
- `Chat/ChatEventSource.cs` — `TrackTellPartner` + `CharLinks`-Lookup nutzen jetzt `Name@World`
- `Config/DalamudConfigStore.cs` — `Migrate()` entfernt alte Einträge ohne `@`

---

## 2026-03-17 — Architekturentwurf finalisiert

**Aktivität:** Vollständigen Architekturentwurf ausgearbeitet und in architecture.md gespeichert.

**Inhalt:** Projektstruktur, Kernabstraktionen, Datenfluss (4 Richtungen), Config-POCO, DI-Setup, kritische Implementierungshinweise.

**Nächster Schritt:** Implementierung beginnen — Projekt umbenennen, .csproj konfigurieren, Grundstruktur anlegen.

---

## 2026-03-17 — Entwurfs-Klärung: Alle Designfragen beantwortet

**Aktivität:** Alle offenen Designfragen für das neue Plugin systematisch durchgesprochen und entschieden.

**Entscheidungen:**
- **Transport:** Bot (Empfang + Slash Commands) + Webhook pro Kanal (Senden mit Char-Namen)
- **Chattypen:** Alle 46+ FFXIV-Typen, pro Discord-Kanal konfigurierbar (N:M-Mapping, inkl. DM)
- **Rückkanäle:** Optional pro Discord-Kanal; Slash Commands in jedem Kanal mit mind. 1 Chattyp
- **Slash Commands:** Modular via `InteractionService` (Auto-Discovery); Module: InfoModule, ConfigModule, ChatModule
- **Berechtigungen:** Admin (Plugin-GUI) + Whitelist (granular, niedriger Prio aber architektonisch vorbereitet)
- **Tells:** Incoming = konfigurierbarer Chattyp; Antwort via `/tell` (Autocomplete: letzte Tells, Freundesliste, FC) + Reply-Button
- **Anzeigeformat:** reiichi001-Standard (`CharName@Server`, `[prefix] text`) + optionales Char↔Discord-Linking mit Reply-Button
- **Konfiguration:** Ausschließlich In-Game ImGui GUI; Persistenz via Dalamud Plugin-Config

**Nächster Schritt:** Architekturentwurf ausarbeiten (nach weiteren offenen Punkten des Users).

---

## 2026-03-17 — Neustart: Separates Repo geplant

**Aktivität:** Entscheidung für kompletten Neustart als separates Repo mit beiden alten Projekten als Referenz-Unterordner.

**Entscheidungen:**
- VS Community 2026 (neu installiert) — Workload: nur **.NET-Desktopentwicklung** nötig
- Neues Projekt in VS: **Klassenbibliothek** (Class Library, C#) — Dalamud-Plugins sind DLLs
- Struktur des neuen Repos:
  ```
  neues-repo/
    DiscordBridgePlugin/       ← Neues Plugin (net10.0-windows)
    reference/
      FFXIVDiscordChatBridge/  ← alter Standalone-Ansatz (Sharlayan + InputSimulator)
      Dalamud.DiscordBridge/   ← reiichi001-Fork als Referenz (FFXIV→Discord, 46+ Chattypen)
  ```

**Klarheit über alten Standalone-Ansatz:**
- FFXIV→Discord: Sharlayan liest Spielspeicher extern via `ReadProcessMemory()` — kennt feste RAM-Offsets die nach Patches aktualisiert werden müssen
- Discord→FFXIV: `InputSimulator` simuliert Tastatureingaben ins FFXIV-Fenster — dedizierter Account + Fenstermodus erforderlich
- Fazit: extern, fragil, kein Dalamud nötig — aber viele Einschränkungen

**Versionen für neues Plugin (aus development.md):**
- .NET SDK: **10.0**, TargetFramework: `net10.0-windows`
- Dalamud: `14.0.4.1` (API-Level 14)
- DalamudPackager: `2.1.12`
- Discord.Net: `3.16.0` (inkl. `Discord.Net.Interactions`)
- Microsoft.Extensions.DependencyInjection: `9.0.x`

---

## 2026-03-16 — Architektur-Analyse & Neuausrichtung

**Aktivität:** Projekt analysiert, Architektur-Entscheidung getroffen, neues Dalamud-Plugin implementiert.

**Kernerkenntnisse:**

- Der User nutzt **XIVLauncher + Penumbra/Dalamud** und hatte zwei Projekte verwechselt:
  - `reiichi001/Dalamud.DiscordBridge` (installiert) — Dalamud-Plugin, einseitig FFXIV→Discord, kein dedizierter Account
  - `ViMaSter/FFXIVDiscordChatBridge` (geklont) — Standalone WinExe, bidirektional, dedizierter Account + Sharlayan + Keyboard-Simulation
- Das geklonte Repo wurde trotzdem als Basis benutzt; neues Dalamud-Plugin (`DiscordBridgePlugin/`) als Unterordner angelegt

**Wie FFXIVDiscordChatBridge (alt) intern funktioniert:**
- FFXIV→Discord: Sharlayan liest `ReadProcessMemory()` → FFXIV-Chatlog-Buffer im RAM → `FFXIVByteHandler` dekodiert binäre FFXIV-Sonderzeichen
- Discord→FFXIV: `InputSimulator` simuliert Tastatureingaben ins FFXIV-Fenster (fragil, Dedicated Account nötig)

**Dalamud-Umgebung des Users (Stand 2026-03-16):**
- Dalamud Version: `14.0.4.1` — läuft auf **.NET 10.0** (nicht .NET 9!)
- XIVLauncher Dalamud-Pfad: `%AppData%\XIVLauncher\addon\Hooks\dev\`
- ImGui-DLL heißt jetzt `Dalamud.Bindings.ImGui.dll`, Namespace `Dalamud.Bindings.ImGui` (nicht mehr `ImGuiNET`)
- Dalamud Plugin Interface: `IDalamudPluginInterface` (nicht `DalamudPluginInterface`)

**Was gebaut wurde:**
- `DiscordBridgePlugin/` — neues bidirektionales Dalamud-Plugin
- Slash Commands: `/link`, `/unlink`, `/status`, `/help` (auch per DM via `CommandContextType`)
- FFXIVHelpers/Tests/BinaryFromLogGenerator auf net9.0 migriert (alle 87 Tests grün)
- Blocker: .NET 10 SDK nötig für den Plugin-Build

**Rückblick / was besser gewesen wäre:**
- `reiichi001/Dalamud.DiscordBridge` forken und nur Discord→FFXIV-Richtung ergänzen wäre pragmatischer gewesen
- FFXIV→Discord war dort bereits mit 46+ Chattypen fertig implementiert
- User plant: neues separates Repo, beide alten Projekte als Referenz-Unterordner

---

## 2026-03-16 — Initiale Session

**Aktivität:** Memory-Struktur für das Projekt angelegt.

**Angelegt:**
- `.memory/MEMORY.md` – zentrale Erinnerungsdatei
- `.memory/architecture.md` – Architektur & Datenfluss
- `.memory/development.md` – Build, Tests, Konventionen
- `.memory/decisions.md` – Architekturentscheidungen
- `.memory/known-issues.md` – Bugs & TODOs
- `.memory/sessions.md` – diese Datei

**Kontext:** Erster Aufbau des Memory-Systems auf Wunsch des Users.

---

*Neue Sessions am Anfang dieser Datei einfügen (neueste oben).*
