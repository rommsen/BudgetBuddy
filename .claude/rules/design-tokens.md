---
paths: src/Client/**
---

# Design Tokens Only — Keine Standard-Tailwind-Farben

## Verboten

- Standard-Tailwind-Farben: `red-*`, `green-*`, `blue-*`, `gray-*`, `yellow-*`, `orange-*`, `purple-*`, `pink-*`
- Hardcoded Hex-Farben in `prop.className`: `bg-[#...]`, `text-[#...]`
- Inline `style.color` / `style.backgroundColor` mit Hex-Werten
- Inline `style.fontSize` — umgeht die Typography-Token-Scale
- Tailwind arbitrary font sizes: `text-[Npx]`, `text-[1.2rem]` etc.

## Richtig

- Design Tokens aus `product/design-system.md` bzw. deinem Token-System:
  - Semantische Farben: `text-danger`, `bg-success`, `border-warning`
  - Palette-Farben: projekt-spezifische Farbklassen aus der Design-System-Definition
  - UI-Farben: `bg-surface`, `text-body`, `border-border`
- Typography Tokens statt inline `style.fontSize`:
  - Projekt-definierte Klassen wie `text-heading`, `text-body`, `text-meta` etc.
- Bei neuem Farbwert: erst im Design-System definieren, dann in Tailwind nutzen

## Grep-Checks

```bash
# Finde Standard-Tailwind-Farben in Client-Code
grep -rn "text-red-\|bg-red-\|text-green-\|bg-green-\|text-blue-\|bg-blue-\|text-gray-\|bg-gray-" src/Client/

# Finde hardcoded Hex-Farben
grep -rn 'bg-\[#\|text-\[#' src/Client/

# Finde inline style Farben
grep -rn "style.color\|style.backgroundColor" src/Client/

# Finde inline fontSize (Typography-Token-Verletzung)
grep -rn "style.fontSize" src/Client/

# Finde arbitrary font-size values
grep -rn 'text-\[[0-9]' src/Client/
```

## Visuelle Werte NIE erfinden

Wenn ein Plan Farben, Gradients, Spacing nicht explizit angibt:
1. IMMER im Style Guide / Design System nachschlagen
2. NIE Hex-Werte raten — auch nicht "passend zum Farbschema"
3. Konkreter Grep: `grep -i "keyword" product/styleguide/src/sections/*.jsx` oder im Design-System-Dokument suchen

## Dynamische Tailwind-Klassen

Tailwind purged Klassen die nicht als String-Literal im Code stehen.
- Dynamisch generierte Klassen (z.B. `$"bg-{colorName}"`) werden entfernt
- Lösung: `safelist` in `tailwind.config.js` für dynamische Klassen
- Oder: vollständige Klassen als Konstanten definieren
