# Plan: Money-Ausrichtung und EUR-Positionierung fixen

## Problem
- Screenshot zeigt: Beträge umbrechen → EUR steht unter dem Betrag statt daneben
- Beträge sind nicht rechtsbündig/untereinander ausgerichtet
- Erste Zeile (Desktop) korrekt: `-5.20 EUR`
- Andere Zeilen (Mobile): Amount und EUR auf separaten Zeilen

## Ursache
1. `Money.fs` span hat kein `whitespace-nowrap` → Text kann umbrechen
2. Mobile Amount-Container hat keine feste Breite → keine Ausrichtung möglich

## Lösung

### Datei: `src/Client/DesignSystem/Money.fs`

**Zeile 89 ändern:**
```fsharp
// ALT:
prop.className $"font-mono font-semibold tabular-nums {sizeClass} {colorClass} {glowClass} {extraClass}"

// NEU:
prop.className $"font-mono font-semibold tabular-nums whitespace-nowrap {sizeClass} {colorClass} {glowClass} {extraClass}"
```

### Datei: `src/Client/Components/SyncFlow/View.fs`

**Mobile Amount Container (Zeile 321-332) ändern:**
```fsharp
// ALT:
Html.div [
    prop.className "flex-shrink-0"
    prop.children [ ... ]
]

// NEU:
Html.div [
    prop.className "flex-shrink-0 w-24 text-right"
    prop.children [ ... ]
]
```

## Erwartetes Ergebnis
- Alle Beträge: `-25.99 EUR` auf einer Zeile
- Rechtsbündig ausgerichtet
- Untereinander aligned durch feste Breite `w-24`
