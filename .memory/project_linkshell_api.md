---
name: FFXIVClientStructs Linkshell API
description: Exakte Typen und Methoden zum Lesen von LS/CWLS-Namen aus dem Spielspeicher, plus deutsche Präfix-Konvention
type: project
---

## FFXIVClientStructs Typen (Namespace: `FFXIVClientStructs.FFXIV.Client.UI.Info`)

**Reguläre Linkshells:**
- Typ: `InfoProxyLinkshell` (kleines 's' am Ende!)
- `InfoProxyLinkshell.Instance()` → Pointer oder null
- `proxy->GetLinkshellInfo(uint slot)` → `Entry*` (slot: 0-basiert)
- `entry->Id` → 0 = Slot leer
- `proxy->GetLinkshellName(ulong id)` → `CStringPointer` → `.ToString()`

**Cross-World Linkshells:**
- Typ: `InfoProxyCrossWorldLinkshell`
- `InfoProxyCrossWorldLinkshell.Instance()` → Pointer oder null
- `proxy->GetCrossworldLinkshellName(uint slot)` → `Utf8String*` (slot: 0-basiert)
- `name->ToString()` liefert den Namen

## Sprachabhängige Präfixe

| Sprache | LS-Präfix | CWLS-Präfix |
|---------|-----------|-------------|
| Deutsch (`ClientLanguage.German`) | `kk` | `wkk` |
| Alle anderen | `ls` | `cwls` |

Quelle: Bestehende `/kk` und `/wkk` Slash-Commands in `Discord/Modules/ChatModule.cs`.

## Implementierung

- `Core/LinkshellNameService.cs` — `TryGetSlug(XivChatType)` gibt z.B. `"wkk3:Chocobo Gang"` zurück
- Muss auf dem **Framework-Thread** aufgerufen werden (in `IChatGui.ChatMessage`-Callbacks bereits erfüllt)
- Alle native Aufrufe sind in try/catch gewrappt → bei Fehler wird null zurückgegeben
- `ChatEventSource` nutzt `_linkshellNames.TryGetSlug(type) ?? ChatTypeHelper.GetSlug(type)` als Fallback

## Slug-Format

`[prefix{nummer}:{name}]` wenn Name bekannt, sonst `[prefix{nummer}]`

Beispiel: `[wkk3:Chocobo Gang] Hey Leute`
