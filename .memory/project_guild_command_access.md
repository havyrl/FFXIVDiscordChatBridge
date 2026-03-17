---
name: Guild Channel Command Access Issue
description: /config commands nicht in Guild-Channels sichtbar, obwohl User Guild-Owner ist
type: project
---

Slash Commands (`/config *`) sind für den Guild-Owner in Guild-Channels nicht sichtbar, nur per DM.

**Why:** Unklar. Mögliche Ursachen:
- Bot wurde ohne `applications.commands` OAuth2-Scope eingeladen
- Discord Sync-Delay für global registrierte Commands (bis zu 1h)
- Globale Commands erscheinen in Guild-Channels erst nach vollständiger Propagation

**Stand:** `[DefaultMemberPermissions(GuildPermission.Administrator)]` wurde entfernt (2026-03-17) — Commands sind nun für alle sichtbar, Code-Guard schützt die Ausführung. Ob das das Problem löst, wurde noch nicht getestet.

**How to apply:** Bei Problemen mit Command-Sichtbarkeit in Guilds zuerst prüfen: Bot-Invite-Link hat `applications.commands` Scope? Dann ggf. Guild-spezifische Command-Registrierung als Alternative zu `RegisterCommandsGloballyAsync()` untersuchen.
