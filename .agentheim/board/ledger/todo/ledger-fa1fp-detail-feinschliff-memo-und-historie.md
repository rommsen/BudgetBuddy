---
id: ledger-fa1fp
title: "Detail-Feinschliff nach den Tafeln: Memo in BB bearbeiten mit Schreibweg nach YNAB, Historie nach dem Aufteilen mit Zielen und Beträgen, Kategorie am Handy in Kurzform, Fußtext im Picker, Auswahl-Bausteine im Styleguide"
status: todo
type: feature
context: ledger
created: 2026-10-25
completed:
depends_on: [design-system-001, ledger-xfjcp]
blocks: []
tags: [frontend, backend, kassenbuch, detail, memo, historie, styleguide, tafeln]
related_adrs: [0045, 0050, 0055, 0058, 0059]
related_research: []
prior_art: [ledger-q1hw2, ledger-pafkp, ledger-sfab0, ledger-nhwrt]
---

## Why
Abgleich der Design-Tafeln (2026-10-25, Ablauf 4, 5 und 7). Mehrere kleinere Lücken im
Detail, gebündelt, weil sie dieselbe Fläche berühren. Das Memo lässt sich in BB nicht
bearbeiten (Detail zeigt "Kein Memo"), die Tafeln sehen "Memo hinzufügen" vor (5.2, 7.8).

## What
1. **Memo bearbeiten.** Im Detail lässt sich das Memo setzen, ändern und leeren (Handy und PC).
   Schreibweg nach dem Muster der Kategorie (ADR 0055): Befehl, Ereignis mit alt und neu,
   PATCH/PUT nach YNAB, Fehlschlag-Zustand mit Hinweisleiste und "Erneut senden", Historie-Zeile,
   Toast mit Rückgängig. Gesperrt bei gelöschten Buchungen. Leerer Text heißt "kein Memo".
2. **Historie nach dem Aufteilen.** Die Historie-Zeile eines Aufteilens nennt die Zeilen mit
   Kategorie und Betrag (Tafel 4.7), statt nur deren Anzahl.
3. **Kategorie am Handy kurz.** Im Detail-Sheet am Handy steht die Kategorie in Kurzform
   (Tafel 4.9); voll qualifiziert bleibt sie am PC und bei Split-Zeilen nach ADR 0045, sofern
   die Kurzform dort mehrdeutig wäre.
4. **Fußtext im Picker.** Der Kategorie-Picker am PC zeigt unten "Esc schließt ohne zu
   ändern · Klick wählt sofort" (Tafel 4.2).
5. **Styleguide.** `/styleguide` zeigt den Baustein `MultiSelect`: Summenzeile im
   Auswahl-Zustand, Auswahl-Leiste Handy und PC, Kopfzeilen-Kästchen. Der veraltete
   Kopfkommentar in `MultiSelect.fs` wird berichtigt.

## Acceptance criteria
- [ ] Domain-/Validierungs-Tests für das Memo: Setzen, Ändern, Leeren, Ablehnung bei gelöschter Buchung, Längengrenze nach YNAB.
- [ ] Server-Test: der Memo-Befehl schreibt das Ereignis mit alt und neu und schickt das Memo nach YNAB; Fehlschlag setzt den Zustand "Memo nicht in YNAB"; Retry sendet erneut.
- [ ] Reducer-Test: Memo speichern zeigt Toast mit Rückgängig; Rückgängig stellt das alte Memo wieder her.
- [ ] Die Historie-Zeile nach dem Aufteilen enthält jede Zeile mit Kategorie und Betrag (Test am QueryHandler bzw. an der Formatierung).
- [ ] View-Test: Handy-Detail zeigt die Kategorie in Kurzform, PC-Detail unverändert.
- [ ] Der Picker am PC rendert den Fußtext; am Handy nicht.
- [ ] `/styleguide` rendert die drei Auswahl-Bausteine (Playwright oder View-Test über die Styleguide-Seite).
- [ ] Playwright: Memo im Detail setzen, Liste und Detail zeigen das neue Memo.
- [ ] `guard:tokens` und `guard:classes` grün.

## Notes
- Tafeln: `docs/ux/kassenbuch/tafeln/` (Ablauf 4, 5, 7).
- Hängt an `ledger-xfjcp`: dieselbe Hinweisleiste im Detail (`DetailNotice`, `detailNoticeActionMsg`), und der Memo-Schreibweg folgt dem Flag-Schreibweg als Muster.
- Das Memo ist der größte Teil. Wird die Task zu groß, darf der Worker die Punkte 2 bis 5 in einer Folge-Task melden, statt sie halb zu bauen.
- Gap-Bericht Ablauf 3 und 4 ("Offen, ohne Task"), Ablauf 5 bis 7 Punkt 6.
