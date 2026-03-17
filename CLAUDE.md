# Claude Instructions — FFXIVDiscordBridgePlugin

## Session-Start: Pflicht

Lies zu Beginn jeder Session die folgenden Dateien ein:

- `.memory/MEMORY.md` — Index aller Memory-Dateien
- Alle dort verlinkten Dateien die für die aktuelle Aufgabe relevant sind

## Memory-Verwaltung

**Alle** projektbezogenen Erinnerungen, TODOs und Entscheidungen werden ausschließlich in `.memory/` gespeichert.
**Niemals** in `~/.claude/projects/...` oder anderen externen Pfaden.

Beim Schreiben neuer Memory-Dateien:
1. Datei in `.memory/` anlegen
2. Eintrag in `.memory/MEMORY.md` ergänzen

## Projekt-Kurzübersicht

Dalamud-Plugin für FFXIV (.NET 10, `net10.0-windows`), das FFXIV-Chat bidirektional mit Discord verbindet.

- **Architektur:** Microsoft.Extensions.DependencyInjection, `System.Threading.Channels`, Discord.Net Slash Commands
- **Referenzprojekt** (nur lesen, nicht bearbeiten): `C:\dev\c#\references\Dalamud.DiscordBridge`
- **Config** (Laufzeit): `%AppData%\XIVLauncher\pluginConfigs\FFXIVDiscordBridgePlugin.json`
- **Dalamud SDK:** `Dalamud.NET.Sdk/14.0.2`, ImGui-Namespace: `Dalamud.Bindings.ImGui` (nicht `ImGuiNET`)
