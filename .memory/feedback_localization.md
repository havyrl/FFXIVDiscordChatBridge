---
name: Lokalisierungspflicht
description: Jede neue user-facing Zeichenkette muss in alle vorhandenen Locale-Dateien aufgenommen werden
type: feedback
---

Jede neue user-facing Zeichenkette (Discord-Responses, ImGui-Texte, Modal-Titles, Embed-Felder usw.) muss sofort in **alle** vorhandenen Locale-Dateien eingetragen werden.

**Why:** Der User hat explizit festgelegt, dass Lokalisierung immer mitzupflegen ist — nicht nachträglich.

**How to apply:**
- Neue Strings zuerst als Key in `Loc/en.json` definieren (englischer Referenztext)
- Denselben Key dann in jede weitere vorhandene Datei (`Loc/de.json`, `Loc/ja.json`, `Loc/fr.json` sobald vorhanden) übersetzen
- Aktuell gepflegte Sprachen: **en**, **de**
- Keine hardcodierten Strings in Modulen, Handlern oder GUI-Code — ausnahmslos `T(key)` / `localizer.T(key, locale)` verwenden
- `LocalizedModuleBase` als Basisklasse für alle Discord-Interaction-Module (inkl. nested Groups)
- GUI-Code (`AdminRequestWindow` etc.) nutzt `ILocalizer.T(key)` ohne Locale-Parameter → FFXIV-Clientsprache
