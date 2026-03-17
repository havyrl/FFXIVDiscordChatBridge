---
name: PandorasBox Feature Ideas
description: Feature-Ideen für die Bridge, abgeleitet aus Analyse des PandorasBox-Referenzprojekts (2026-03-17)
type: project
---

Analyse von `C:\dev\c#\references\PandorasBox` auf übertragbare Features für die FFXIVDiscordBridgePlugin.

**Why:** PandorasBox enthält viele ausgefeilte Beispiele für Game-Event-Handling und Addon-Interaktion, die als Referenz für neue Bridge-Features dienen können.
**How to apply:** Bei der Planung neuer Features zuerst hier nachschauen, ob PandorasBox eine Referenzimplementierung hat.

---

## Feature-Ideen (priorisiert)

### Priorität Hoch

#### IPC-API für andere Plugins
- **Referenz:** `PandorasBox/IPC/PandoraIPC.cs`
- **Idee:** Eine IPC-Schnittstelle anbieten, damit andere Dalamud-Plugins Nachrichten über die Bridge an Discord senden können (z.B. Crafting-Plugin → "Craft abgeschlossen").
- **Aufwand:** Mittel

#### Post-Duty-Summary
- **Referenz:** `PandorasBox/Features/UI/AutoVoteMvp.cs`
- **Idee:** Nach einer Duty eine Zusammenfassung an Discord schicken: wer hat Commendation bekommen, Tode, Rollen, Duty-Dauer.
- **Techniken:** `BannerMIP`-Addon-Lifecycle, Partymitglieder-Tracking, Rollen aus ClassJob-Daten, Tode zählen.
- **Aufwand:** Mittel

### Priorität Mittel

#### Map-Link-Formatting
- **Referenz:** `PandorasBox/Features/Chat/AutoOpenCoords.cs`, `PreserveMapLinks.cs`
- **Idee:** `MapLinkPayload` in Chat-Nachrichten als lesbaren Text nach Discord weiterleiten statt als Rohdaten (z.B. `[Mor Dhona (23, 14)]`).
- **Techniken:** `Dalamud.Game.Text.SeStringHandling`, Payload-Extraktion, Regex für Koordinaten.
- **Aufwand:** Niedrig

#### FATE-Benachrichtigungen
- **Referenz:** `PandorasBox/Features/Other/AutoSyncFate.cs`
- **Idee:** Wenn der Spieler an einem FATE teilnimmt, Discord-Notification schicken (FATE-Name, Gebiet, Typ, Start/Ende).
- **Techniken:** `FateManager.CurrentFate` überwachen via Framework.Update.
- **Aufwand:** Niedrig

### Priorität Niedrig

#### Job-Change-Status-Update
- **Referenz:** `PandorasBox/FeaturesSetup/Events.cs` (`Events.OnJobChanged`)
- **Idee:** Optionaler Discord-Status-Update bei Job-Wechsel ("spielt jetzt als Paladin").
- **Aufwand:** Niedrig

#### Party-Finder-Notifications
- **Referenz:** `PandorasBox/Features/UI/AutoJoinPF.cs`
- **Idee:** Benachrichtigung wenn jemand einem eigenen PF-Listing beitritt oder es sich ändert.
- **Techniken:** `LookingForGroupDetail`-Addon überwachen.
- **Aufwand:** Hoch

---

## Technische Referenzen aus PandorasBox

| Thema | Datei | Technik |
|---|---|---|
| Chat-Payload-Parsing | `Features/Chat/PreserveMapLinks.cs` | `MapLinkPayload`, Regex, Hook |
| Addon-Lifecycle | `Features/UI/AutoVoteMvp.cs` | `Svc.AddonLifecycle.RegisterListener` |
| Framework-Update-Loop | `FeaturesSetup/Events.cs` | `Svc.Framework.Update` |
| IPC-Pattern | `IPC/PandoraIPC.cs` | `DalamudPluginInterface.GetIpcProvider` |
| Game-Object-Abfrage | `Features/Targets/AutoInteractDungeons.cs` | `Svc.Objects`, Distance-Checks |
| Addon-Callback | `Features/Commands/CallbackCommand.cs` | `Callback.Fire()` |
