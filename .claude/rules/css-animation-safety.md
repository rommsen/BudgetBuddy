---
paths:
  - "src/Client/styles.css"
  - "src/Client/Views/**"
---

# CSS Animation Safety

## Verboten

- `animation-fill-mode: forwards` oder `both` auf Elementen die Kinder mit `position: fixed` haben
- `transform` permanent auf Containern die `fixed`-Kinder haben (Modals, Bottom Nav, Toast)
- `background` (Shorthand) wenn eine Tailwind `background-color` bereits gesetzt ist — resettet `bg-*`

## Warum

CSS-Spec: `transform` erstellt einen neuen Containing Block. `fixed`-Kinder positionieren sich relativ dazu statt zum Viewport. `animation-fill-mode: forwards` hält den `transform`-Wert nach Ablauf — gleicher Effekt.

## Richtig

```css
/* Entrance-Animation: fill-mode none (Default), transform resettet sich automatisch */
@keyframes page-enter {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: none; }
}
.page-enter { animation: page-enter 0.4s ease-out; /* fill-mode: none ist Default */ }

/* Wenn fill-mode noetig: Animation auf inneres Wrapper-Element legen */
.modal-overlay { position: fixed; }  /* Kein transform hier! */
.modal-content { animation: modal-enter 0.3s ease-out forwards; }  /* Inner element OK */
```

## CSS Shorthand Pitfalls

- `background` (Shorthand) resettet `background-color` — nutze `backgroundImage` für Gradients über Tailwind-Farben
- `style.lineHeight 1.5` in Feliz erzeugt `1.5px` — nutze Tailwind `leading-*` Klassen
- `prop.max` in Feliz akzeptiert keinen String — nutze `prop.custom("max", value)`

## Grep-Checks

```bash
# fill-mode auf Containern die fixed-Kinder haben koennten
grep -n 'fill-mode.*forwards\|fill-mode.*both' src/Client/styles.css

# transform auf app/page-level Containern
grep -n 'animation.*forwards' src/Client/styles.css
```
