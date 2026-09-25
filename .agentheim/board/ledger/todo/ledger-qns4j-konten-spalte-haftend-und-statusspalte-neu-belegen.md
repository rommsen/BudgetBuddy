---
id: ledger-qns4j
title: "Konten-Spalte beim Scrollen halten, und die Statusspalte mit Status statt Herkunft belegen"
status: todo
type: bug
context: ledger
created: 2026-09-13
completed:
depends_on: [ledger-x0yga, ledger-w9skm, design-system-001]
blocks: []
tags: [frontend, kassenbuch, layout]
related_adrs: [0032]
related_research: []
prior_art: [ledger-x0yga, ledger-kdnnz]
---

## Why
Zwei Befunde aus der Verifikation von `ledger-x0yga` (2026-09-12/13), beide am
PC-Tabellenlayout, beide ausserhalb der damaligen Akzeptanzkriterien.

## What

**1. Die Konten-Spalte scrollt weg.** Im Screenshot `pc-1440x900-gescrollt.png` ist die
linke Spalte leer: sie war nie haftend (`md:w-[268px] md:shrink-0 md:border-r`, ohne
`sticky`), und seit der Tabellenkopf klebt, fällt es auf. Beim Durchsehen von rund 14.000
Buchungen sind die Kontostände nach zwei Handgriffen verschwunden, obwohl sie der Bezug
für alles darunter sind.

Zu bauen: die Konten-Spalte bleibt beim senkrechten Scrollen stehen, wie der
Tabellenkopf. Randfall mitdenken: bei fünf Konten passt sie in die Höhe, bei zwanzig
nicht.

**2. Die Statusspalte trägt Herkunft, nicht Status.** Sie zeigt in praktisch jeder Zeile
„YNAB-Spiegel", weil fast alle Buchungen aus dem Spiegel stammen. Damit verbraucht sie die
zweitbreiteste Spalte der Tabelle für die geringste Information.

Das Übergabe-Dokument sieht die Spalte für den **Status** vor („Zu prüfen"), und die
Herkunft steht im Detail, wo sie etwas erklärt.

**3. Die Badge-Lösung von `ledger-x0yga` ist ein Pflaster**, ausdrücklich angemerkt vom
Verifier: Die Spalte wurde auf 208 px verbreitert und die Kennzeichen auf `flex-nowrap`
gesetzt, damit zwei davon die Zeile nicht in die Höhe treiben. Das verschiebt den Fehler
nur: ein drittes oder längeres Kennzeichen läuft künftig waagerecht aus der Zelle statt
umzubrechen.

### Entscheidung Roman, 2026-09-19: Status schlägt Herkunft, zwei Badges sind erlaubt

Der Widerspruch zwischen Punkt 3 (Kennzeichen kürzen, damit die Spalte schmal wird) und
dem Entwurf aus Ablauf 7 (bis zu zwei Badges) ist entschieden:

- Die Statusspalte trägt **nur noch Status**: *Zu prüfen*, *Nicht in YNAB*, später
  *Abweichung*. Die Herkunft verschwindet aus der Tabelle und steht im Detail
  (`ledger-q1hw2`), wo sie etwas erklärt.
- **Zwei Badges nebeneinander bleiben zulässig**, wie der Entwurf aus Ablauf 7 sie vorsieht.
  Damit sie in eine schmale Spalte passen, werden die Beschriftungen gekürzt oder durch
  Symbole ersetzt. Die Lösung liegt bei den Kennzeichen, nicht bei der Spaltenbreite.
- *In YNAB gelöscht* bleibt ein Ausnahme-Kennzeichen und darf weiterhin in der Zeile
  stehen, weil es selten ist und Bedeutung trägt.

**Diese Aufgabe wartet nicht auf `ledger-r777c`.** Der beobachtete Prüfvermerk steht
bereits (`ledger-kdnnz`, ADR 0032): `NeedsReview` wird gelesen und die Zeile trägt schon
heute ihren linken Rahmen danach. Die Spalte kann also sofort mit *Zu prüfen* belegt
werden; dass BB den Vermerk noch nicht schreiben kann, ändert an der Anzeige nichts.

### Ist-Zustand, beim Verfeinern am Code festgestellt (2026-09-19)

- **Konten-Spalte:** `src/Client/Components/Ledger/View.fs:391` setzt
  `md:w-[268px] md:shrink-0 md:border-r {Tokens.Borders.subtle} md:pr-3`. Kein `sticky`,
  der Befund stimmt wörtlich.
- **Spaltenbreite:** `statusColumnWidth = "208"` (Zeile 299), gegen den Handoff-Wert von
  128 px in Screen 1.9.
- **Inhalt der Spalte:** `entryTableRow` (Zeile 308-348) füllt die letzte Zelle mit
  `originBadge`, `flagBadge` und `deletedBadge` hinter einem `flex-nowrap`-Halter. Der
  `originBadge` ist genau die Regelherkunft, die verschwinden soll.
- **Der Prüfstand ist schon da:** dieselbe Funktion setzt bei `entry.NeedsReview` bereits
  `border-l-2 border-l-neon-pink`. Der Wert für das Status-Badge muss also nicht neu
  beschafft werden.

## Acceptance criteria
- [ ] Die Konten-Spalte bleibt am PC beim senkrechten Scrollen der Liste sichtbar. Belegt
      durch einen Screenshot im gescrollten Zustand (1440 × 900), auf dem die Kontostände
      noch stehen, im Vergleich zum heutigen `pc-1440x900-gescrollt.png`. [human-eye]
- [ ] Ist die Konten-Liste höher als der Viewport, scrollt sie in sich, statt den
      Seiteninhalt abzuschneiden oder die Haftung zu verlieren. Mit mehr Konten belegt,
      als in die Höhe passen, nicht nur mit den heutigen fünf.
- [ ] Die Statusspalte enthält keinen `originBadge` mehr. Belegt durch eine Zusicherung
      über die gerenderte Zeile, nicht nur durch das Lesen des Diffs.
- [ ] Eine ungeprüfte Buchung trägt in der Statusspalte ein Badge *Zu prüfen*; eine
      geprüfte trägt dort keines. Die Quelle bleibt `entry.NeedsReview` (ADR 0032), es
      wird kein neuer Zustand eingeführt.
- [ ] Zwei Status-Badges nebeneinander passen in die Spalte, ohne sie zu verbreitern und
      ohne die Zeilenhöhe zu ändern. Mit einer Buchung belegt, die gleichzeitig ungeprüft
      und nicht in YNAB verknüpft ist.
- [ ] `statusColumnWidth` ist auf den Handoff-Wert (128) zurückgesetzt, und die 80 px
      gehen an Payee und Memo zurück. Belegt am Spaltenraster, nicht nur am geänderten
      Literal.
- [ ] Die Herkunft ist in der Tabelle nicht ersatzlos verloren: entweder steht sie im
      Detail (`ledger-q1hw2`, noch nicht gebaut) oder der Task benennt im Ergebnis
      ausdrücklich, dass sie bis dahin nur über die Historie erreichbar ist.
- [ ] `dotnet build` und `dotnet test` grün, `npm run build` sauber.

## Notes
Captured am 2026-09-13 aus der Verifikation von `ledger-x0yga`. Vorlage bleibt
`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`, Abschnitt 1.9, ergänzt um
den Entwurf aus Ablauf 7 für die zwei Badges.

**`depends_on` um `ledger-w9skm` ergänzt** (verfeinert 2026-09-19): beide Aufgaben ändern
`entryTableRow` in `src/Client/Components/Ledger/View.fs`. `w9skm` fasst dort die
Kategorie-Zelle an, diese Aufgabe die Status-Zelle und das Spaltenraster. Nacheinander
statt gleichzeitig, sonst kollidieren zwei Worker in derselben Funktion.
