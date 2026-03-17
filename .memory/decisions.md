# Architekturentscheidungen

## Format

Jede Entscheidung folgt dem Schema:
**Entscheidung:** Was wurde entschieden?
**Kontext:** Warum war eine Entscheidung nötig?
**Begründung:** Warum diese Option?
**Konsequenzen:** Was bedeutet das?

---

## Entscheidungen

### Dedizierter FFXIV-Account erforderlich
**Entscheidung:** Die Bridge benötigt einen separaten FFXIV-Account.
**Kontext:** FFXIV hat keine offizielle API für Chat-Interaktion.
**Begründung:** Sharlayan liest Spielspeicher und InputSimulator simuliert Tastatureingaben – das ist nur mit einem dedizierten Client möglich, der das Spielfenster steuern kann.
**Konsequenzen:** Nutzer brauchen ein zweites Account/Abo.

### Konfiguration via CLI-Argumente (kein appsettings.json)
**Entscheidung:** Alle Konfiguration über Kommandozeilenparameter.
**Begründung:** Einfachere Deployment-Pakete, keine sensiblen Daten in Konfigurationsdateien, die versehentlich committed werden könnten.

### Windows-only
**Entscheidung:** Nur Windows wird unterstützt (WinExe, Windows Forms).
**Begründung:** FFXIV läuft nur auf Windows/Mac; Sharlayan und InputSimulator sind Windows-spezifisch. DirectX 11 Voraussetzung.

### Producer-Consumer-Muster
**Entscheidung:** Strikte Trennung von Empfang (Consumer) und Senden (Producer).
**Begründung:** Klare Verantwortlichkeiten, einfacheres Testen, unabhängige Weiterentwicklung der beiden Seiten (Discord vs. FFXIV).

---

### Dalamud-Plugin statt Standalone-App (2026-03-16)
**Entscheidung:** Neues Projekt als Dalamud-Plugin, nicht als externe Standalone-App.
**Kontext:** User nutzt XIVLauncher/Penumbra; bestehende App benötigt dedizierten Account + Keyboard-Simulation + Sharlayan-Memory-Reading.
**Begründung:** Dalamud hat direkte, stabile APIs für Chat-Lesen (`IChatGui.ChatMessage`) und Chat-Schreiben (`ICommandManager.ProcessCommand`). Kein dedizierter Account nötig.
**Konsequenzen:** Erfordert .NET 10 SDK (Dalamud 14.x läuft auf .NET 10). Plugin läuft im Spielprozess.

### Bestes Vorgehen wäre Fork von Dalamud.DiscordBridge gewesen (2026-03-16)
**Entscheidung:** Rückblickend wäre `reiichi001/Dalamud.DiscordBridge` forken + Discord→FFXIV ergänzen besser gewesen.
**Kontext:** Dalamud.DiscordBridge hat FFXIV→Discord mit 46+ Chattypen bereits fertig; fehlt nur die Gegenrichtung (~100 LOC).
**Begründung:** Weniger Arbeit, aktiv gewartet, bewährte Basis.
**Konsequenzen:** User startet neues separates Repo mit beiden alten Projekten als Referenz.

---

### Discord-Transport: Bot + Webhook kombiniert (2026-03-17)
**Entscheidung:** Bot-Verbindung für Empfang/Slash Commands, Webhooks pro Kanal für das Senden von FFXIV→Discord-Nachrichten.
**Kontext:** Webhooks erlauben es, Charakter-Namen und Avatar als Absender darzustellen (wie reiichi001). Ein reiner Bot würde immer den Bot-Namen zeigen.
**Begründung:** Bester UX-Kompromiss: schöne Darstellung (Webhook) + volle Funktionalität (Bot). Der User hat bereits einen bestehenden Bot-Token vom reiichi001-Plugin.
**Konsequenzen:** Pro konfiguriertem Discord-Kanal muss ein Webhook erstellt/gespeichert werden.

### Alle 46+ FFXIV-Chattypen, pro Discord-Kanal konfigurierbar (2026-03-17)
**Entscheidung:** Alle FFXIV-Chattypen (Say, Yell, Shout, Party, FC, Linkshells 1-8, CW-Linkshells, Novice Network, Tell, System etc.) werden unterstützt. Jeder Chattyp kann einem Discord-Kanal oder einer DM zugeordnet werden.
**Begründung:** Parität mit reiichi001; maximale Flexibilität für den User.
**Konsequenzen:** Konfigurationsmodell ist ein N:M-Mapping (ein Discord-Kanal kann mehrere FFXIV-Chattypen empfangen).

### Rückkanäle optional pro Discord-Kanal (2026-03-17)
**Entscheidung:** Jeder Discord-Kanal kann optional als Rückkanal konfiguriert werden (Discord→FFXIV). Ein Rückkanal ist einem bestimmten FFXIV-Chattyp zugeordnet.
**Begründung:** Nicht alle Kanäle brauchen bidirektionale Kommunikation (z.B. reiner Log-Kanal).
**Konsequenzen:** Slash Commands sind in jedem Kanal verfügbar, dem mindestens ein FFXIV-Chattyp zugeordnet ist — unabhängig vom Rückkanal.

### Modulares Slash-Command-System via Discord.Net InteractionService (2026-03-17)
**Entscheidung:** Alle Slash Commands sind in Interaction-Module-Klassen organisiert, die beim Start automatisch per Reflection registriert werden.
**Begründung:** Neuen Command hinzufügen = neue Klasse, kein Änderungsbedarf an bestehendem Code. /help liest alle Commands dynamisch aus.
**Konsequenzen:** Module-Gruppierung: `InfoModule` (/help, /status, /who), `ConfigModule` (/config channel, /config dm, /config permissions, /config link), `ChatModule` (/say, /fc, /party, /tell etc.).

### Berechtigungsmodell: Admin + Whitelist (2026-03-17)
**Entscheidung:** Zwei Ebenen: (1) Bridge-Admin = in Plugin-GUI konfigurierter Discord-User, darf alles. (2) Whitelist = Discord-User oder -Rollen mit granularen Rechten.
**Begründung:** Einfacher Start (Admin reicht), späterer Ausbau möglich ohne Architekturänderung.
**Konsequenzen:** Whitelist-Granularität (welche Aktionen erlaubt) ist niedrige Priorität — wird später ausgebaut. Admin-Check ist immer der erste Guard.

### Tell-Handling: Incoming wie Chattyp, Antwort via /tell + Reply-Button (2026-03-17)
**Entscheidung:** Eingehende Tells sind ein konfigurierbarer Chattyp (wie FC, Party etc.). Antworten per `/tell <name> <text>` mit Autocomplete (aus letzten Tells, Freundesliste, FC) oder per Reply-Button auf der Discord-Nachricht.
**Begründung:** Reply-Button für schnelle Antworten auf konkrete Nachrichten; /tell für neue Konversationen.
**Konsequenzen:** Plugin trackt zuletzt erhaltene Tell-Partner. Dalamud-APIs für Freundesliste/FC-Mitglieder werden genutzt wo verfügbar.

### Nachrichtenformat: reiichi001-Standard + optionales Char-Linking (2026-03-17)
**Entscheidung:** Standard-Anzeige wie reiichi001: Webhook-Username = `CharName@Server`, Nachrichtentext = `[kanalprefix] Text`. Optional: FFXIV-Charakter ↔ Discord-Account verknüpfbar; verknüpfte Chars bekommen Reply-Button auf ihren Nachrichten.
**Begründung:** Bewährtes Format, sofort verständlich. Char-Linking ermöglicht direktere Interaktion für bekannte Personen.
**Konsequenzen:** Char-Linking-Verwaltung via `/config link` und in-game GUI.

### Konfiguration ausschließlich via In-Game ImGui GUI (2026-03-17)
**Entscheidung:** Alle Plugin-Einstellungen (Bot-Token, Kanal-Mappings, Berechtigungen, Char-Links) werden über ein Dalamud ImGui-Einstellungsfenster verwaltet. Persistenz via Dalamud Plugin-Config-System (`%AppData%\XIVLauncher\pluginConfigs\`).
**Begründung:** Dalamud-Standard, keine externe Konfigurationsdatei nötig, alles im Spiel erreichbar.

---

### Naming-Konventionen (2026-03-17)
**Entscheidung:** Verschiedene Namen je Kontext.
- Projekt/Solution: `FFXIVDiscordBridgePlugin`
- Dalamud In-Game Anzeigename: `DiscordBridge`
- Discord Bot-Name: `FFXIVBridge`
- GitHub Repo: `FFXIVDiscordChatBridge` (havyrl, bereits korrekt)
**Konsequenzen:** Aktuelle Projektdateien (`DiscordBridgePlugin.csproj`, `.slnx`) werden beim Umsetzen des Entwurfs umbenannt.

### Generisches Event/Action-System (2026-03-17)
**Entscheidung:** Plugin-Architektur ist nicht auf Chat beschränkt — generisches `IGameEventSource` / `IDiscordActionHandler`-System, modular erweiterbar wie Slash Commands.
**Kontext:** "DiscordBridgePlugin" nicht "DiscordChatBridgePlugin" — Scope bewusst weiter gefasst. Beispiele: Party-Invite annehmen, Duty-Pop bestätigen via Discord-Button.
**Begründung:** Gleiches Modulmuster wie Slash Commands: neue Ereignistypen = neue Klasse, automatisch registriert. Chat-Bridge ist erste/wichtigste Implementierung.
**Konsequenzen:** Im ersten Schritt nur Chat implementiert; Architektur ist von Anfang an für beliebige Events/Actions ausgelegt.

*Neue Entscheidungen hier ergänzen, sobald sie in Sessions getroffen werden.*
