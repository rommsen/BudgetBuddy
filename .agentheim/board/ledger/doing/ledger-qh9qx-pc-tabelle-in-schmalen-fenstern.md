---
id: ledger-qh9qx
title: "PC-Tabelle des Kassenbuchs bricht in schmalen Fenstern: Payee, Kategorie und Memo schrumpfen auf null, die Spaltenköpfe liegen übereinander"
status: doing
type: bug
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001]
blocks: [ledger-q1hw2]
tags: [frontend, kassenbuch, responsive, tabelle]
related_adrs: [0049]
related_research: []
prior_art: [ledger-n4vte, ledger-qns4j]
---

## Why
Ab `md` (768px) zeigt das Kassenbuch die PC-Tabelle. Die festen Spalten (Auswahl, Datum
96px, Konto 72px, Betrag 132px, Status 128px) brauchen zusammen rund 480px, Payee und
Kategorie bekommen Prozentanteile der Tabellenbreite, Memo den Rest
(`tableColumnWidths`, `Components/Ledger/View.fs`). Neben der Kontenspalte bleibt in einem
halb breiten Fenster kaum Platz: Payee, Kategorie und Memo werden auf ein bis zwei
Zeichen gekürzt ("Jo...", "Üb...") oder ganz unsichtbar, die Köpfe "PAYEE",
"KATEGORIE" und "MEMO" liegen übereinander. Roman hat das mit Screenshots gemeldet
(2026-10-20).

## What
Die Tabelle blendet Spalten stufenweise aus, statt sie zusammenzudrücken (Entscheidung
Roman 2026-10-20):

1. Payee und Kategorie haben eine Mindestbreite (Richtwert Payee 140px, Kategorie 120px,
   beim Bau gegen echte Werte wie "Transfer : Tagesgeld ING" prüfen).
2. Reicht die Breite dafür nicht, fällt zuerst die Memo-Spalte weg.
3. Reicht es dann immer noch nicht, fällt in der Gesamtsicht die Konto-Spalte weg; das
   Kontokürzel steht dann klein vor oder unter dem Payee in derselben Zelle.
4. Erst wenn auch das nicht reicht, greift die Handy-Liste. Die Schwelle richtet sich nach
   der verfügbaren Tabellenbreite, nicht allein nach dem Viewport-Breakpoint `md`.

Welche Spalten sichtbar sind, entscheidet eine reine Funktion aus Tabellenbreite und
Gesamtsicht/Konto (etwa `visibleColumns tableWidth showAccountColumn`), damit sie ohne
Browser testbar ist. Die Breite misst ein `ResizeObserver` auf dem Tabellencontainer, wie
schon `FilterBarColumn` die Höhe der Filterleiste misst.

## Acceptance criteria
- [ ] Eine reine Funktion bestimmt die sichtbaren Spalten aus Tabellenbreite und
      Gesamtsicht/Konto; Expecto-Tests decken die Stufen ab (alle Spalten, ohne Memo,
      ohne Memo und Konto, Handy-Liste) inklusive der Grenzwerte.
- [ ] In keiner Stufe ist eine sichtbare Spalte schmaler als ihre Mindestbreite.
- [ ] Bei Fensterbreiten 800, 1024, 1280 und 1440px (Gesamtsicht und ein Konto) überlappt
      kein Spaltenkopf seinen Nachbarn: für jedes `th` gilt `scrollWidth <= clientWidth`,
      geprüft im echten Browser (Chrome DevTools, Screenshot im Verifier-Bericht).
- [ ] Ist die Konto-Spalte ausgeblendet, ist das Konto jeder Zeile in der Gesamtsicht
      weiter sichtbar (Kürzel in der Payee-Zelle).
- [ ] Der Sticky-Kopf und die Statuskante "zu prüfen" funktionieren in jeder Stufe
      unverändert.
- [ ] Die gekürzte Darstellung bei schmaler Breite wirkt ruhig und lesbar (ADR 0049). [human-eye]

## Notes
- `memoMinShare` und `referenceTableWidth` in `View.fs` sind ein früherer Ansatz für die
  Memo-Breite; prüfen, ob sie in der neuen Funktion aufgehen oder wegfallen.
- `Viewport.isDesktopViewport()` entscheidet heute zwischen Tabelle und Handy-Liste
  (ADR 0043). Stufe 4 verschiebt diese Grenze; die Filterleisten-Chrome soll dabei nicht
  ungewollt mitwechseln.
- Verwandt: `ledger-bn913` (Sortierung über die Spaltenköpfe) braucht dieselben Köpfe.
