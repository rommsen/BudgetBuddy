---
paths:
  - "src/**"
---

# Serena Tools — Situationsbasierte Empfehlung

Serena bietet symbolische Code-Tools (LSP-basiert). Nutze sie wo sie Vorteile bringen:

## Wann Serena sinnvoll ist

| Situation | Serena Tool | Warum |
|-----------|-------------|-------|
| Unbekannten Code explorieren | `get_symbols_overview` | Schneller Überblick ohne ganze Datei |
| Spezifisches Symbol in großer Datei | `find_symbol` mit `include_body=True` | Gezielt nur das Benötigte |
| Alle Verwendungen finden (Refactoring) | `find_referencing_symbols` | Findet alle Referenzen codebase-weit |
| Ganze Funktion/Klasse ersetzen | `replace_symbol_body` | Präziser, weniger Einrückungsfehler |

## Wann Read/Edit besser ist

| Situation | Standard Tool | Warum |
|-----------|---------------|-------|
| Kleine Datei (<200 Zeilen) | `Read` | Ein Call statt mehrere Serena-Calls |
| Kontext um Symbol herum brauchen | `Read` | Serena liefert nur das Symbol selbst |
| Kleine Änderung (1-3 Zeilen) | `Edit` | Schneller als symbolisches Editing |
| Nicht-Code Dateien (.md, .json, .css) | `Read`/`Edit` | Serena nur für Code |

## Pragmatische Regel

**Exploration/Refactoring -> Serena** | **Gezielte kleine Änderungen -> Read/Edit**
