---
name: feedback_primary_constructors
description: C# Primary Constructor Warnungen CS9107/CS9124 vermeiden — Parameter nur einmal speichern
type: feedback
---

Bei C# Primary Constructors: Einen Parameter **nie** gleichzeitig als Property/Field speichern und direkt in Methodenbodies referenzieren.

**Regel:** Wenn ein Primary-Constructor-Parameter in Methoden gebraucht wird, entweder:
- (A) Nur das Property/Field verwenden (`Localizer.T(...)` statt `localizer.T(...)`), oder
- (B) Kein explizites Property/Field anlegen und den Parameter direkt benutzen

Nie beides mischen — das erzeugt CS9107 oder CS9124.

**Why:** CS9124: "Parameter wird im Zustand des einschließenden Typs erfasst und sein Wert wird auch zum Initialisieren eines Felds/Property verwendet." — zwei Kopien desselben Werts im Objekt.

**How to apply:** Wenn ein Parameter sowohl in einer Property-Initialisierung (`{ get; } = param`) als auch in Methodenbodies direkt (`param.Method()`) vorkommt → Methoden auf das Property umstellen.
