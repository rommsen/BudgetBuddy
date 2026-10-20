---
id: ledger-q1hw2
title: "Buchungs-Detail: Herkunft, Historie und \"noch nicht in YNAB\" (Sheet am Handy, Seitenpanel am PC)"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-b1xwe, design-system-001, ledger-rynk5, ledger-rjb8w]
blocks: [ledger-b7qmx]
tags: [frontend, kassenbuch, scheibe]
related_adrs: [0013, 0021, 0045, 0049, 0050]
related_research: []
prior_art: [ledger-q5w9c, ledger-fsz0a, ledger-h9tq2, ledger-r777c]
---

## Why
Das ist, was nur BB kann und YNAB nicht hat: woher eine Buchung stammt, was sich wann und
woher geändert hat, und ob sie überhaupt schon in YNAB angekommen ist. Heute steckt die
Historie in einem eigenen Sheet, das nur am Handy gedacht ist; am PC fehlt eine Form, die
neben der Liste stehen bleibt.

## What
Bild und Text: Handoff Ablauf 7 (`docs/ux/kassenbuch/handoff-ablauf-7-was-nur-bb-kann.md`),
Screens 7.2, 7.3 (ohne Pink-Leiste), 7.5, 7.7, 7.8 (ohne Pink-Leiste), 7.10.

Ein Detail je Buchung, am Handy als BottomSheet, am PC als **Seitenpanel rechts** (420 px),
damit die Liste sichtbar bleibt. Das bisherige Historie-Sheet (`OpenHistory`/
`historySheet`) geht darin auf.

1. **Öffnen und Schließen.**
   - Handy: Tipp auf eine Zeile öffnet das Sheet (außerhalb des Auswahl-Modus aus
     `ledger-r1d2f`; dort wählt ein Tipp weiterhin aus). Schließen über den Knopf oder den
     Griff.
   - PC: Klick auf eine Zeile oder `Enter` auf der Fokuszeile (`ledger-rjb8w`) öffnet das
     Panel. `↑`/`↓` bewegen die Fokuszeile, das offene Panel folgt. `Esc` schließt. `G`
     wirkt wie in `ledger-rjb8w` auf die Fokuszeile und damit auf die Buchung im Panel.
   - Bei offenem Panel fällt die Memo-Spalte weg; der Tastaturhinweis lautet
     "↑ ↓ bewegen · G geprüft · Esc Panel schließen".
2. **Kopf:** Betrag, Payee, "Konto · Datum", Schließen-Knopf.
3. **Hinweisleiste** direkt unter dem Kopf, höchstens eine, in dieser Rangfolge:
   - Rot **"in YNAB gelöscht"** (`DeletedInYnab`): Text mit Löschzeitpunkt aus der
     Historie, "Zählt nicht zum Kontostand.", keine Aktion.
   - Orange **"Noch nicht in YNAB"** (`LinkedToYnab = false`): mit
     `PushToYnabFailure` der Satz "Push gescheitert am <Datum> <Uhrzeit>: <Grund>. BB
     versucht es beim nächsten Lauf erneut."; ohne Fehlschlag "Noch nicht an YNAB
     gesendet." Keine Aktion (ein "Erneut senden" für den Push ist nicht Teil dieser
     Scheibe).
   - Orange **"Prüfvermerk nicht in YNAB"** (`ReviewMarkWriteBackFailure`): Grund und die
     wirkende Aktion "Erneut senden" (`retryReviewMarkWriteBack` aus `ledger-r777c`).
   - Die Pink-Leiste "Abweichung" ist v2 und wird nicht gebaut.
4. **Felder, auf Handy und PC gleich** (Roman, 2026-10-19: die PC-Fassung gilt überall):
   - **Kategorie** lesend, am PC voll qualifiziert (`categoryLabelQualified`, ADR 0039),
     ohne Chevron (Kategorie ändern ist v2).
   - **Split** lesend: "Kein Split" oder je Split-Zeile Ziel und Betrag. Die volle
     Qualifizierung der Split-Kategorien kommt mit `ledger-b7qmx`.
   - **Memo** lesend, leer als Platzhalter in `text-muted`.
   - **Geprüft-Schalter** (DS-Komponente, neu): wirkt über `setReviewMark` aus
     `ledger-r777c` (an: `Reviewed`, aus: `Unreviewed`), mit demselben Fehlschlag-Weg
     wie die Schaltfläche an der Zeile.
5. **Herkunft:** eigene Zeile "HERKUNFT" mit Teal-Badge aus `LedgerEntryView.Origin`:
   Comdirect-Import, Regel, YNAB-Spiegel, In BB entschieden.
6. **Historie** als Zeitleiste aus `getEntryHistory`, neueste Zeile oben
   (`historyLinesForDisplay`, `ledger-h9tq2`), je Eintrag Satz, Zeitstempel, Urheber.
   Punktfarbe nach Urheber: Teal Import, Violett Regel, Grau YNAB-Spiegel, Green eigene
   Prüfung in BB, Rot für Fehlschläge und Löschung. Als `RemoteData`: Lade- und
   Leerzustand ("Noch keine Einträge") sind Pflicht (Handoff, offener Punkt 12). Im
   Sheet scrollt die Historie.
7. **Gelöschte Buchung ist ein Lesezustand:** Betrag in `text-muted` statt Rot, Felder
   grau, kein Geprüft-Schalter, keine Aktion.
8. Neue DS-Bausteine nach der Tabelle "Neu zu bauende Komponenten" im Handoff
   (Herkunft-Zeile, Historie-Zeitleiste, Hinweisleiste mit Grund und Aktion, Seitenpanel,
   Geprüft-Schalter), je mit Galerie-Beispiel in `/styleguide`. Darstellung ruhig nach
   ADR 0049.

Für die offenen Punkte des Handoffs gilt: Badge-Schreibung im Satzanfang groß ("In YNAB
gelöscht", "Noch nicht in YNAB"), sonst wie im Styleguide; die markierte Zeile bei
offenem Panel nutzt die Fokus-Darstellung aus `ledger-rjb8w`.

## Acceptance criteria
- [ ] Reducer-Test: Tipp bzw. Klick auf eine Zeile öffnet das Detail für genau diese
      Buchung und lädt die Historie (`Loading`, dann `Success`); im Auswahl-Modus öffnet
      ein Tipp kein Detail.
- [ ] Reducer-Test (PC): bei offenem Panel bewegt `↓` die Fokuszeile und das Panel zeigt
      die neue Buchung mit neu geladener Historie; `Esc` schließt; `Enter` auf der
      Fokuszeile öffnet.
- [ ] Ansichts-Test: Rangfolge der Hinweisleiste: gelöscht schlägt "Noch nicht in YNAB",
      das schlägt "Prüfvermerk nicht in YNAB"; eine verknüpfte, nicht gelöschte Buchung
      ohne Fehlschlag hat keine Leiste.
- [ ] Ansichts-Test: "Noch nicht in YNAB" mit `PushToYnabFailure` zeigt Datum, Uhrzeit und
      Grund; ohne Fehlschlag den Satz "Noch nicht an YNAB gesendet."; keine Aktion.
- [ ] Reducer-Test: "Erneut senden" in der Prüfvermerk-Leiste ruft
      `retryReviewMarkWriteBack` für diese Buchung.
- [ ] Reducer-Test: der Geprüft-Schalter ruft `setReviewMark Reviewed` bzw. `Unreviewed`;
      Erfolg und Fehlschlag aktualisieren Detail und Listenzeile gleich wie die
      Schaltfläche an der Zeile.
- [ ] Ansichts-Test: Felder Kategorie, Split, Memo und Geprüft erscheinen auf Handy und PC;
      eine Buchung mit zwei Split-Zeilen zeigt beide Zeilen mit Betrag, ohne Splits steht
      "Kein Split".
- [ ] Ansichts-Test: die Herkunft-Zeile zeigt für jede der vier `LedgerOrigin`-Arten das
      richtige Badge.
- [ ] Ansichts-Test: die Historie erscheint neueste zuerst; die Punktfarbe folgt dem
      Urheber; Lade- und Leerzustand sind vorhanden.
- [ ] Ansichts-Test: eine gelöschte Buchung hat keinen Geprüft-Schalter, keine Aktion und
      einen grauen Betrag.
- [ ] Das alte Historie-Sheet ist entfernt; es gibt keinen zweiten Weg zur Historie.
- [ ] Playwright-Wächter (Handy 390 px und PC 1440 px, gemockte API): Detail öffnet sich,
      am PC bleibt die Liste sichtbar und die Memo-Spalte fehlt bei offenem Panel.
- [ ] Neue DS-Bausteine haben je ein Galerie-Beispiel in `/styleguide`.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün.
- [ ] Sheet und Panel lesen sich ruhig, die Hinweisleiste fällt auf, ohne zu schreien
      (ADR 0049). [human-eye]

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Brief: `docs/ux/kassenbuch/briefs.md`,
Ablauf 7.

Sortierung (ADR 0051, Punkt 7): Ändert ein Schreibweg aus dem Detail den Schlüssel der
aktiven Sortierung (etwa Kategorie ändern bei Kategorie-Sortierung), stimmt der
Seitenversatz `LocallyRemoved` nicht mehr; an der Seitengrenze können Buchungen fehlen
oder doppelt erscheinen. Beim Verfeinern festlegen, wie das Detail damit umgeht
(z. B. die geladenen Seiten neu laden).

Verfeinert am 2026-10-19. Entscheidungen Roman: Handy und PC zeigen dieselben Felder (die
PC-Fassung gilt); der Geprüft-Schalter wirkt und "Erneut senden" für den Prüfvermerk
wirkt; der Grund eines gescheiterten Pushs wird als Ereignis festgehalten
(`ledger-rynk5`, eigene Task, Vorbedingung). Die Fokuszeile und `G` kommen aus
`ledger-rjb8w`, deshalb hängt das PC-Panel daran. Die Historie-Reihenfolge ist in
`ledger-h9tq2` entschieden (neueste oben).

Nicht Teil: Abweichung (Pink, v2), Kategorie- und Split-Bearbeitung (v2), "Erneut senden"
für den Push, "Wiederherstellen" gelöschter Buchungen.
