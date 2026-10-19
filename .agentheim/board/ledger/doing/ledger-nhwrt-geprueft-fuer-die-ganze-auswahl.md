---
id: ledger-nhwrt
title: "Aktion \"Geprüft\" für die ganze Auswahl in der Auswahl-Leiste, ein Sammel-PATCH an YNAB, ein Toast mit Rückgängig"
status: doing
type: feature
context: ledger
created: 2026-10-19
completed:
depends_on: [ledger-r777c, ledger-r1d2f, design-system-001]
blocks: []
tags: [frontend, backend, kassenbuch, schreibend]
related_adrs: [0028, 0032, 0050]
related_research: []
prior_art: [ledger-r777c]
---

## Why
Roman will einen Stapel unauffälliger Buchungen in einem Zug als geprüft markieren,
etwa nach dem Filtern auf eine Kategorie. `ledger-r1d2f` liefert die Auswahl-Leiste mit
Summe, `ledger-r777c` den schreibenden Weg für eine Buchung.

## What
Bild und Text: Handoff Ablauf 1, Screens 1.11 und 1.12
(`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`) und Handoff Ablauf 3
(Auswahl-Leiste).

1. **Aktion "Geprüft"** in der Auswahl-Leiste, als einzige primäre Aktion (Orange), setzt
   `Reviewed` für jede Buchung der Auswahl. "Ungeprüft" ist nicht Teil des Umfangs. Die
   Handy-Leiste bekommt dafür die Reihe aus Screen 3.2 zurück ("Geprüft" in Reihe 2).
2. **Auswahl an den Server:** der Stapel-Befehl nimmt die Auswahl aus `ledger-r1d2f`
   entgegen, entweder als Ids (`Picked`) oder als Treffer-Anfrage plus Ausnahmen
   (`AllExcept`: dieselbe Filter-Beschreibung wie `getLedgerEntriesPage`, plus die
   abgewählten Ids). Der Server löst die Menge selbst auf, weil der Client die Ids nicht
   geladener Buchungen nicht kennt.
3. **Ereignisse je Buchung:** der Server prüft jede Buchung wie in `ledger-r777c` (Ziel
   gleich Ist → `NoChange`, kein Ereignis; in YNAB gelöscht oder nicht verknüpft →
   `Failed` ohne Ereignis) und schreibt für jede geänderte Buchung ein eigenes
   `EntryReviewMarkChangedInBb`. Keine Sammel-Ereignisse.
4. **Ein Sammel-PATCH an YNAB** (Entscheidung Roman 2026-10-19): alle geänderten Buchungen
   gehen in einer Anfrage an `PATCH /budgets/{id}/transactions`, mit denselben Feldern je
   Buchung wie der Einzelweg (`approved`; `cleared` nur, wenn nicht Reconciled, ADR 0050).
   Scheitert die Anfrage (Netz, 4xx, 429), tragen **alle** Buchungen dieser Anfrage
   `EntryReviewMarkWriteBackFailed` mit demselben Grund. Der Weg "Erneut senden" aus
   `ledger-r777c` bleibt je Buchung.
5. **Rückmeldung:** ein Toast "n Buchungen geprüft · Rückgängig" (Screen 1.12). Gab es
   `NoChange` oder Fehlschläge, nennt der Toast sie mit ("2 ohne Änderung, 3 nicht in
   YNAB"); gescheiterte Zeilen tragen den Hinweis aus `ledger-r777c`. Die Auswahl wird
   danach aufgehoben, mit aktivem Chip "Zu prüfen" verlassen die gesetzten Zeilen die Liste
   und der Zähler sinkt. `LocallyRemoved` (`ledger-be1yh`) steigt um die Zahl der Zeilen,
   die die geladene Liste verlassen.
6. **Ein Rückgängig für den ganzen Stapel** (Entscheidung Roman 2026-10-19, Handoff 1.12:
   "eine Rücknahme, nicht drei"): stellt für jede geänderte Buchung ihren Vorzustand wieder
   her (Logik von `restoreReviewMark`, ADR 0050 Punkt 1 und 10), wieder mit einem
   Ereignis je Buchung und einem Sammel-PATCH. `NoChange`-Buchungen werden nicht angefasst.
   Danach lädt der Client die Liste neu (`resetEntries`), statt Zeilen einzeln
   zurückzustecken: unter `AllExcept` waren viele der Buchungen nie geladen, und das
   Neuladen setzt Seitenversatz und Zähler auf den Stand des Servers.
7. **Toast-Aktion:** der Slot steht seit `ledger-th36h` (`Types.Toast.Action`,
   `ToastActionKind`). Diese Task ergänzt einen Fall `UndoReviewMarkBatch` mit den
   Vorzuständen je Buchung aus der Antwort; die Wurzel übersetzt ihn wie
   `UndoReviewMark`.
8. **Leiste:** am Handy steht "Geprüft" in Reihe 2 neben "Alle" und "Auswahl aufheben",
   am PC in der einreihigen Leiste rechts (`MultiSelect.barMobile`/`barDesktop`).

## Acceptance criteria
- [ ] Stapel mit drei zu prüfenden Buchungen (`Picked`): drei `EntryReviewMarkChangedInBb`,
      genau ein PATCH an YNAB mit drei Buchungen (DB-Test mit Zähler auf dem HTTP-Stub).
- [ ] Stapel mit `AllExcept`: der Server löst Filter plus Ausnahmen auf und setzt genau die
      Buchungen des Treffers ohne die Ausnahmen, auch wenn sie im Client nie geladen waren
      (DB-Test mit mehr Buchungen als eine Seite).
- [ ] Eine bereits geprüfte Buchung ergibt `NoChange`: kein Ereignis, nicht im PATCH.
- [ ] Eine in YNAB gelöschte Buchung ergibt `Failed` ohne Ereignis und steht nicht im
      PATCH; die übrigen werden verarbeitet.
- [ ] Reconciled-Buchungen stehen im PATCH nur mit `approved`, ohne `cleared`.
- [ ] Scheitert der Sammel-PATCH (Netzfehler und HTTP 429, je ein Test), trägt jede Buchung
      des PATCH `EntryReviewMarkWriteBackFailed` mit dem Grund; die Ereignisse
      `EntryReviewMarkChangedInBb` bleiben stehen.
- [ ] Ein Stapel ohne geänderte Buchung schickt keinen PATCH.
- [ ] Sammel-Rückgängig stellt für jede geänderte Buchung genau ihren Vorzustand her (auch
      `approved = true`, `Uncleared`), sendet nie eine Reconciled-Angabe, fasst
      `NoChange`-Buchungen nicht an und schickt einen PATCH.
- [ ] Reducer-Test: "Geprüft" löst den Stapel-Aufruf mit der Auswahl aus (`Picked` und
      `AllExcept`, je ein Test); bei Erfolg ist die Auswahl aufgehoben, die Zeilen
      verlassen die Liste (Chip aktiv) und der Zähler sinkt um die Zahl der gesetzten
      Buchungen.
- [ ] Reducer-Test: der Toast nennt "n Buchungen geprüft" und bei Bedarf die Zahlen ohne
      Änderung und nicht in YNAB; gescheiterte Zeilen tragen den Hinweis.
- [ ] Reducer-Test: Rückgängig aus dem Toast löst den Sammel-Rückgängig-Aufruf mit den
      Vorzuständen aus der Antwort aus; nach Erfolg lädt die Liste neu.
- [ ] Reducer-Test: nach "Geprüft" mit aktivem Chip steigt `LocallyRemoved` um die Zahl
      der Zeilen, die die geladene Liste verlassen.
- [ ] Ansichts-Test: die Auswahl-Leiste zeigt "Geprüft" als primäre Aktion auf Handy und PC.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes` grün.

## Notes
Abgespalten am 2026-10-19 beim Verfeinern von `ledger-r777c` (Entscheidung von Roman).

Verfeinert am 2026-10-19. Entscheidungen Roman: ein Sammel-PATCH statt einzelner
Anfragen (YNAB erlaubt rund 200 Anfragen pro Stunde je Token, ein Stapel über "Alle" kann
Dutzende Buchungen treffen); Sammel-Rückgängig gehört in diese Task (Handoff 1.12). Damit
entfallen die frühere Reihenfolge-Regel ("nacheinander, nicht parallel") und der Zustand
"nicht verarbeitet" bei 429.

Für den Worker zu prüfen: ob die YNAB-API für `PATCH /transactions` eine Obergrenze je
Anfrage nennt (dann in Blöcken senden) und wie die Antwort aussieht, wenn einzelne
Buchungen abgelehnt werden. Nennt die Antwort einzelne Buchungen, sollen nur diese als
gescheitert gelten. Die Einzelanfrage in `YnabClient.sendReviewMark` ist ein PUT auf eine
Buchung; der Sammelweg ist ein neuer Aufruf daneben, der Einzelweg bleibt.

Die Seitenversatz-Frage beim Nachladen nach dem Entfernen von Zeilen löst `ledger-be1yh`.

Nachgezogen am 2026-10-19 nach `ledger-th36h` und `ledger-r1d2f`: Toast-Slot ist da
(Punkt 7), Platz in der Leiste benannt (Punkt 8), Rückgängig lädt neu statt einzeln
einzufügen (Punkt 6). Laut YNAB-OpenAPI ist PATCH die Sammel-Route
`PATCH /budgets/{id}/transactions` mit `{"transactions": [...]}` und je Eintrag `id`;
der Einzelweg ist PUT (ADR 0050 Nachtrag).
