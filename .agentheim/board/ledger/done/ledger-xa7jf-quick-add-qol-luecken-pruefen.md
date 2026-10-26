---
id: ledger-xa7jf
title: "Spike: Quick Add und Import-Seite nach Komfortfunktionen durchsehen, die dem Kassenbuch fehlen; Ergebnis ist eine Liste mit Begründung, aus der Roman die Backlog-Tasks auswählt"
status: done
type: spike
context: ledger
created: 2026-10-25
completed:
depends_on: []
blocks: []
tags: [captured, quick-add, import, kassenbuch, komfort]
related_adrs: ["0021"]
related_research: []
prior_art: []
---

## Why
Im neuen Kassenbuch fehlen Quality-of-Life-Funktionen. Quick Add und die Import-Seite
(SyncFlow) sind älter und haben im Alltag schon Komfort angesammelt (z. B. die Vorlagen
in Quick Add, ynab-t4n8p). Ein Vergleich zeigt, was davon im Kassenbuch fehlt und helfen
würde.

## What
Quick Add (`Navigation.QuickAdd`, Client-State `QuickAdd*`) und die Import-Seite
(`src/Client/Components/SyncFlow/`) durchsehen und jede Funktion notieren, die das
Kassenbuch (`src/Client/Components/Ledger/`) nicht hat. Zu jeder Lücke: was sie tut,
wo sie heute steckt, ob und wie sie im Kassenbuch helfen würde.

Ausdrücklich nicht im Umfang:
- Die Frage, ob die Quick-Add-Seite ganz im Kassenbuch aufgehen kann (Entscheid Roman,
  2026-10-26: nur einzelne Funktionen vergleichen).
- Umsetzung. Der Spike ändert keinen Code.
- Follow-up-Tasks direkt anlegen. Roman wählt aus der Liste aus, erfasst wird danach per
  `modeling`.

## Acceptance criteria
- [ ] Das Outcome enthält eine Tabelle der Lücken mit den Spalten Funktion, Quelle
      (Quick Add oder Import-Seite, mit Datei bzw. Symbol), Nutzen im Kassenbuch und
      Einschätzung (sinnvoll / fraglich / nicht übertragbar) samt einem Satz Begründung.
- [ ] Funktionen, die das Kassenbuch schon in anderer Form hat, stehen nicht als Lücke in
      der Tabelle, sondern in einer kurzen Liste "schon abgedeckt" mit Verweis auf die
      Kassenbuch-Stelle oder die erledigte Task.
- [ ] Jede als sinnvoll eingeschätzte Lücke wird gegen das offene ledger-Backlog geprüft;
      Überschneidungen (z. B. ledger-hg5m5 Regel-Angebot) sind in der Tabelle vermerkt.
- [ ] Es entstehen keine Code-Änderungen und keine neuen Backlog-Tasks.

## Notes
Wörtlich vom Nutzer: "man müsste noch mal rüber schauen, ob es aus dem Quick Think noch
andere Quality of Life Dinge gibt, die helfen könnten." Im Refine am 2026-10-26 geklärt:
gemeint sind Quick Add und zusätzlich die Import-Seite.

Hintergrund: seit ledger-tdnpk (ADR 0021) schreibt Quick Add zuerst ins Kassenbuch und
pusht danach. Quick Add ist im Kassenbuch eine Herkunft, die Oberfläche liegt auf einer
eigenen Seite. Concept-Page zu Quick Add: siehe ynab-t4n8p.

Stop-loss (ADR-0065): if, mid-spike, the mitigation is already known and cheap, record it and stop.

## Outcome

Der Spike hat Quick Add (`src/Client/Views/QuickAddPage.fs`, Client-State `QuickAdd*` in `src/Client/State.fs`) und die Import-Seite (`src/Client/Components/SyncFlow/`: `Views/TransactionRow.fs`, `Views/TransactionList.fs`, `Views/StatusViews.fs`, `Views/InlineRuleForm.fs`, `Views/ViewHelpers.fs`, `State.fs`) mit dem Kassenbuch (`src/Client/Components/Ledger/`, `src/Client/DesignSystem/CategoryPicker.fs`) verglichen. Es gab keine Code-Änderungen und keine neuen Backlog-Tasks. Die Quick-Add-Seite als Ganzes ist ausdrücklich nicht bewertet (Entscheid Roman, 2026-10-26).

Befund in einem Satz: Das Kassenbuch deckt Filter, Tagesgruppen, Kategorie ändern mit Payee-Vorschlägen, Aufteilen, Memo, Lena und Mehrfachauswahl schon ab. Offen sind vier sinnvolle Funktionen, davon zwei bereits im Backlog.

### Lücken

| Nr | Funktion | Quelle | Nutzen im Kassenbuch | Einschätzung | Überschneidung mit offenem Backlog |
|---|---|---|---|---|---|
| 1 | Zuletzt verwendete Kategorien im Kategorie-Picker | Quick Add: `QuickAddPage.view` reicht `model.SyncFlow.RecentlyUsedCategoryIds` an `BottomSheet.categoryPickerLayered` (Abschnitt "Zuletzt verwendet"). Import-Seite: `SyncFlow.State` pflegt `RecentlyUsedCategoryIds` bei `CategorizeTransaction`. | Der Picker im Kassenbuch (`CategoryPicker.fs`, Props in `Ledger/View.fs` `categoryField`) kennt nur `Suggested` (Vorschläge zum Payee aus `getPayeeCategorySuggestions`). Die zuletzt benutzten Kategorien gibt es dort nur im Filter (`Ledger/Types.fs` `pushRecentCategories`, `View.fs` `categoryPickerBody`). Beim Durchgehen vieler Buchungen und bei der Kategorie für die Auswahl wären die letzten fünf Kategorien ein Tipp entfernt. | sinnvoll: kleiner Eingriff, die Daten (`RecentCategories`, im Gerätespeicher) liegen im Ledger-Modell schon vor. | Keine. |
| 2 | Barabhebung-Kurzweg | Import-Seite: Aktionschip "Barabhebung" in `TransactionRow.fs`, Nachricht `StartCashbackSplit` in `SyncFlow/State.fs`. Legt zwei Split-Zeilen an: Kategorie der Buchung plus Übertrag auf das Quick-Add-Konto (Bar). | Der Split-Editor im Kassenbuch (`OpenSplitEditor`, `Ledger/SplitEditor.fs`, ledger-pafkp) startet leer und ist allgemein. Eine EC-Zahlung mit Bargeld-Anteil (Kassenzettel mit Cashback) lässt sich nur zeilenweise von Hand aufteilen. Der Kurzweg füllt Übertrag-Konto und Restbetrag vor. | sinnvoll: der Code kommentiert es als den häufigsten Split-Fall, und die Kassenbuch-Buchungen sind dieselben, die vorher im Import standen. | Keine (ledger-pafkp und ledger-t7v2q sind erledigt und decken nur den allgemeinen Editor und das Verknüpfen). |
| 3 | Regel aus einer manuellen Kategorie-Änderung anlegen ("+ Regel", "Regel erstellen") | Import-Seite: `InlineRuleForm.inlineRuleForm`, `OpenInlineRuleForm`, `SaveInlineRule`; Sichtbarkeit über `shouldShowCreateRule` (manuell kategorisiert, Kategorie gesetzt) in `TransactionRow.fs`. | Wer im Kassenbuch eine Kategorie ändert, wiederholt das oft für denselben Payee. Der Import bietet die Regel am Ort der Änderung an. Im Kassenbuch fehlt das Angebot. | sinnvoll: derselbe Gedanke, nur an anderer Stelle. Offen bleibt, dass Regeln nur neue Importe betreffen. | Ja: ledger-hg5m5 (Regel-Angebot nach Kategorie-Änderung). Dort steht die Entscheidung von Roman vom 2026-10-24. Kein neuer Task nötig. |
| 4 | Link zur Bestellung (Amazon/PayPal) und Stern für Kategorie-Vorschlag aus der Bestellung | Import-Seite: `tx.ExternalLinks` als `tx-external-link` und als Chip `link.Label` in `TransactionRow.fs`; Stern (`hasOrderIdSuggestion`) am Kategorie-Badge. | Im Kassenbuch gibt es den Link nicht. Die Kategorie-Quelle "per Bestellnummer" zeigt `Ledger/View.fs` schon als Punkt (`MatchedByOrderId`, Zeile um 1102), aber ohne Sprung zur Bestellung. | sinnvoll: Roman hat den Link in der Erfassung als wichtigste Lücke genannt. | Ja: ledger-rjhwp (Amazon-Link im Kassenbuch). Der Stern wäre ein Randpunkt davon, die Kategorie-Quelle ist schon sichtbar. |
| 5 | Payee ändern mit Auswahlfeld | Import-Seite: `Input.comboBoxGrouped` in `TransactionRow.fs`, Nachricht `SetPayeeOverride`/`CommitPayeeChange`, Optionen `payeeOptions` (YNAB-Payees gruppiert). | Das Kassenbuch zeigt den Payee nur an (`payeeLabel`, Detail-Feld "Payee" mit Filterknopf) und kann ihn nicht ändern. Vereinheitlichte Payee-Namen würden Filter und Payee-Vorschläge sauberer machen. | fraglich: braucht einen neuen Befehl samt Ereignis, Rückschreiben nach YNAB und Klärung mit den Payee-Regeln. Der Nutzen beim bereits importierten Bestand ist unklar, im Import wirkt der Override vor dem Push. | Keine. |
| 6 | Payee in Schreibweise "Title Case" anzeigen | Import-Seite: `ViewHelpers.titleCasePayee` (mit `commonAbbreviations`), genutzt in `transactionRow` für `displayPayeeTitleCase`. | Kassenbuch zeigt den Payee roh (`payeeLabel`). Bei Bankbuchungen in GROSSBUCHSTABEN wäre die Liste ruhiger zu lesen. | fraglich: reine Anzeige, aber die Anzeige weicht dann vom Wert in YNAB ab und Filter/Suche arbeiten auf dem Rohwert. Im Import hilft es, weil dort Rohdaten der Bank stehen, im Kassenbuch kommen die Payees meist schon aus YNAB. | Keine. |
| 7 | Eingabeformular mit Richtungsschalter, Dezimaltastatur, Autofocus, Datum, Reihenerfassung (Formular nach dem Speichern zurücksetzen) | Quick Add: `QuickAddPage.view`, `freshQuickAddForm`, `QuickAddSaved`. | Das Kassenbuch legt keine Buchungen an. Eine solche Funktion gehört zur ausgeklammerten Frage, ob die Seite im Kassenbuch aufgeht. | nicht übertragbar: ohne neues Formular im Kassenbuch gibt es keinen Anknüpfungspunkt. | Keine. |
| 8 | Vorlagen "Letzte Buchungen" (ein Tipp füllt das Formular) | Quick Add: `QuickAddPage.templatesSection`/`templateChip`, `PrefillQuickAdd`, `applyTemplateToForm`, Server `getRecentQuickAddTemplates` (ynab-t4n8p). | Die Vorlagen füllen ein Eingabeformular. Das Kassenbuch hat keins. Ein "Buchung wiederholen" an einer bestehenden Zeile wäre eine neue Funktion, die an Punkt 7 hängt. | nicht übertragbar: gehört zur Quick-Add-Seite selbst. Sollte die Seite später im Kassenbuch aufgehen, wandert die Funktion mit. | Keine. |

Reihenfolge nach Aufwand und Nutzen (Empfehlung zur Auswahl): 1 (klein), 2 (mittel), 3 und 4 (bereits als Backlog-Entwurf, nur Priorität zu setzen), 5 und 6 nur auf ausdrücklichen Wunsch.

### Schon abgedeckt

| Funktion in Quick Add oder Import | Kassenbuch-Stelle oder erledigte Task |
|---|---|
| Kategorie ändern, Picker mit "Vorgeschlagen" zum Payee und Anzeige des Verfügbar-Betrags | `Ledger/View.fs` `categoryField` mit `CategoryPicker`, `RunLoadCategorySuggestions`/`getPayeeCategorySuggestions`; ledger-k2rbn, ledger-sfab0, ledger-kwwxz |
| Kategorie für mehrere Buchungen auf einmal, Alle überspringen/Alle einschließen (`SkipAllVisible`) | Auswahl-Modus mit Summe und Kategorie für die Auswahl: ledger-r1d2f, ledger-w29y6, ledger-nhwrt, ledger-w4vte |
| Aufteilen (`StartSplitEdit`, `SplitSheet`) | Split-Editor `Ledger/SplitEditor.fs` und Detail: ledger-kxme4, ledger-pafkp, ledger-b7qmx (nur der Kurzweg Barabhebung fehlt, siehe Lücke 2) |
| Filterpillen Alle/Import/Prüfen/Duplikate/Übersprungen (`TransactionList.filterPill`, `SetFilter`) | Filterleiste und Chips (Prüfen, Ohne Kategorie, Kategorie, Datum, Payee, Memo, Betrag): ledger-j8xq4, ledger-m23nz, ledger-vgfw3, ledger-saqsk |
| Gruppen nach Datum mit Tagessumme (`ViewHelpers.groupTransactionsByDate`, `formatGroupTotal`) | `Ledger/View.fs` `groupByDate`, `dateGroupHeader` (Tagessumme nur bei Sortierung nach Datum): ledger-n4vte |
| Zähler "x von y importieren", "n ohne Kategorie", Fortschrittsbalken (`calculateImportCounts`) | Summenzeile und Trefferzahl der Filterleiste: ledger-j8xq4, ledger-d4kx8, ledger-nnn47 |
| YNAB-Ergebnis je Buchung (`ynabResultView`, Ablehnungsgrund) und Hinweis zum Push | Status "noch nicht in YNAB", Push-Fehler und erneut senden: ledger-cks06, ledger-q1hw2, ledger-rynk5, ledger-fgw2q |
| Memo anzeigen (`memo-text` in `transactionRow`) | Memo im Detail lesen und ändern mit Rückgängig: ledger-fa1fp |
| Lena fragen (nur im Import-Entwurf, parallel in Arbeit) | Im Kassenbuch erledigt: ledger-xfjcp, ledger-y54gr. Lena fragen schon beim Import ist ynab-sync-d9e51 (in Arbeit, nicht Teil dieser Liste). |
| Rückmeldung nach Speichern (Toast "Transaktion in YNAB gespeichert") und Haptik (`Viewport.vibrate`) | Toasts mit Rückgängig (`toastActionMsg`), Wisch geprüft: ledger-th36h; Haptik in `Ledger/View.fs` (`Viewport.vibrate 10`) |
| Konto-Wahl für Quick Add (`QuickAddAccountId`) | Konto-Wechsler mit gemerkter Sicht: ledger-ybmqc |
| Entprellen und Versionszähler bei schnell wiederholter Kategorie-Wahl (`CommitCategoryChange`, `Debounce`) | Im Kassenbuch trägt jeder Befehl den Zustand "Wird gesendet" und einen Fehlerhinweis (`CategorySending`, `categoryHintTitle`), die Zeile ist beim Senden gesperrt. Kein Eingriff nötig. |
| Duplikat-Erkennung, "Erneut importieren" (`ForceImportDuplicates`), TAN, Abholen, Abschluss-Seite (`StatusViews`) | Gehören zum Import-Weg selbst, im Kassenbuch ohne Gegenstück (kein Fehlen, nicht übertragbar). Force-Import schreibt ins Kassenbuch: ledger-fq3kd, ledger-w6tbq. |

### Hinweise für die Auswahl

- Lücken 3 und 4 haben schon Backlog-Einträge (ledger-hg5m5, ledger-rjhwp). Beide sind laut Dateikopf unrefined; Roman müsste sie per `modeling` schärfen lassen, ein neuer Task wird nicht gebraucht.
- Lücke 1 und 2 sind nicht erfasst. Falls Roman sie will, erfasst `modeling` sie nach seiner Auswahl.
- Die Quick-Add-Seite hat selbst kein Payee-Autocomplete (`Input.text`), dort gibt es also nichts zu übertragen. Das Payee-Auswahlfeld liegt nur im Import (Lücke 5).
