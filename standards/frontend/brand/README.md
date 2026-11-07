# Buddy: Maskottchen und Zeichen

Entwurf vom 2026-11-07, mit Gemini erstellt und von Roman freigegeben als Vorlage für Etappe 3
(Fahrplan, ADR 0086). Die Dateien sind Rasterbilder aus dem Entwurf, keine fertigen Assets.

| Datei | Inhalt | Verwendung |
|---|---|---|
| `buddy-entwurf-2026-11-07.png` | ganzes Entwurfsblatt | Referenz, nicht ausliefern |
| `buddy-app-symbol.png` | ganze Figur mit Münze und Düse | Vorlage für App-Symbol und PWA-Icon |
| `buddy-kopf.png` | nur der Kopf | Vorlage für das Zeichen in der App (Seitenleiste, ab 24 px) |

## Farben

Die Figur nutzt Palette 2 aus der Skizze zu Etappe 1:

- Grund `#08081a` (bg-app)
- Körper Petrol `#1e5d6b`
- Visier, Augen, Lächeln, Kanten Cyan `#28d6df` (Akzent)
- Ohren und Münze Gold `#cdb27a` (calm-yellow)

## Regeln

- In der App erscheint nur der Kopf. Die ganze Figur ist unter 64 px nicht lesbar.
- Das App-Symbol steht außerhalb der App und darf die ganze Figur zeigen.
- Der Schriftzug folgt der App, nicht dem Entwurf: „Budget“ in `text-primary`, „Buddy“ im Akzent.
  Das Grün und das Gold des Entwurfs-Schriftzugs gehören nicht zur Palette.
- Die Düse ist im Entwurf noch orange. Orange gibt es in Palette 2 nicht mehr; für das fertige
  Symbol wird sie gold oder entfällt.

## Offen

- Saubere Vektor-Fassung (SVG) von Kopf und Figur, ohne Hintergrundraster des Entwurfsblatts.
- PWA-Icons in den nötigen Größen (192, 512, maskable) aus dieser Fassung.
