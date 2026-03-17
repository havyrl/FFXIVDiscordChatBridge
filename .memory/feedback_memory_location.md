---
name: Memory-Speicherort Präferenz
description: Wo Projekt-Memories gespeichert werden sollen
type: feedback
---

Alle projektbezogenen Memories **nur** in `.memory/` im Projektstamm speichern.

**Why:** Der Nutzer möchte alle Infos im Projekt selbst haben, nicht in externen Claude-Verzeichnissen (`~/.claude/projects/...`).

**How to apply:** Beim Erstellen neuer Memory-Dateien immer `.memory/<dateiname>.md` verwenden und den Eintrag in `.memory/MEMORY.md` ergänzen. Niemals `C:\Users\Pryos\.claude\projects\...` verwenden.
