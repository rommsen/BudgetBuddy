---
id: ledger-pafkp
title: "Split-Editor im Kassenbuch: Pfeil an der Kategorie einer Split-Buchung öffnet das Formular-Sheet zum Aufteilen, Ändern und Aufheben der Zeilen mit Rest-Anzeige und Speichern über setEntrySplits, Aufteilen als fester Knopf, Rückgängig im Toast"
status: backlog
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [design-system-001, ledger-sfab0, ledger-kxme4, design-system-gffps]
blocks: []
tags: [frontend, kassenbuch, split, v2]
related_adrs: [0005, 0006, 0049, 0053, 0055]
related_research: []
prior_art: [ledger-q1hw2]
---

## Why
Die Oberfläche zu `ledger-kxme4`. Referenz ist der Split-Editor des Import-Ablaufs
(`src/Client/Components/SyncFlow/Views/SplitSheet.fs`, ynab-sync); er bleibt unberührt. Der
Gestaltung: Brief Ablauf 4 "Kategorie ändern, Split festlegen oder ändern"
(`docs/ux/kassenbuch/briefs.md`, Schrittfolge PC und Handy: "Aufteilen" im Detail,
Zeilen mit Kategorie-Picker, Betrag und Memo, "Zeile hinzufügen", Gesamtbetrag und Rest,
"Rest"-Knopf je Zeile, Speichern erst bei Rest 0,00 €, am Handy im selben Sheet mit
stehendem "Speichern"). Dazu die Tafel Ablauf 4 im Claude-Design-Projekt "Gelesen
UI-Mockups" (Screens 4.1 bis 4.11). Der Handoff liegt im Repo:
`docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md` (Screens 4.5 bis 4.8, 4.11 und die Maße
unter "Split-Editor im Detail"). Abweichung seit ADR 0053: Schritt 1 des Briefs (Klick auf die Kategorie-Zelle
öffnet den Picker) gilt nicht mehr; Picker und Editor öffnen nur aus dem Detail.

## What
Wird nach `ledger-kxme4` verfeinert; der Rahmen steht:
1. Der Pfeil an der Kategorie-Zeile im Detail öffnet bei einer Split-Buchung den Editor; bei
   einer unaufgeteilten Buchung zeigt das Detail-Feld Split "Kein Split" mit festem Knopf
   "Aufteilen" (Tafel 4.4, 4.9; nicht optional, Roman, 2026-10-24), bei einer aufgeteilten
   die Zeilen mit Knopf "Ändern" (4.7). Der Editor ist ein Formular-Sheet mit Speichern
   und Abbrechen. Zeilen mit Kategorie oder Übertrag-Ziel (Picker aus `design-system-gffps` als
   zweite Ebene, ADR 0005), Betrag je Zeile, Rest-Anzeige, Speichern gesperrt bei Rest
   ungleich null (Farbregel wie `splitAmountClass`, ADR 0049).
2. Speichern ruft `setEntrySplits` und nutzt Toast, Fehlschlag-Leiste, die Regeln für
   ausgegraute Zeile und `OrderStale` aus `ledger-sfab0`.
3. **Beträge im Editor positiv** eingegeben, das Vorzeichen kommt vom Gesamtbetrag (Notiz 7
   des Handoffs); die Übersicht im Detail zeigt sie mit Vorzeichen ("-23,40 €"). Maße,
   Farben und Zustände (Rest Pink bei ungleich null, Green bei 0,00 €, "Zeile hinzufügen"
   ausgegraut bei Rest 0,00 €, "Rest"-Knopf je Zeile, Fußleiste mit "Abbrechen" und
   "Speichern" am PC, kein "Abbrechen" am Handy) stehen im Handoff, Abschnitt "Split-Editor im
   Detail"; sie sind die Vorgabe für die Acceptance criteria.
4. **Rückgängig im Toast** (Roman, 2026-10-24): "Aufgeteilt in n Zeilen" mit Knopf
   "Rückgängig". Der Client hält den Stand vor dem Speichern (Kategorie oder Split-Zeilen aus
   `LedgerEntryView`) und stellt ihn über denselben Schreibweg wieder her: war die Buchung
   unaufgeteilt, `setEntrySplits` mit leerer Liste plus die vorherige Kategorie (Verlauf je
   `ledger-kxme4`), war sie aufgeteilt, `setEntrySplits` mit den alten Zeilen. Zweites
   Ereignis, zweiter Push, die Historie zeigt beide. Der Rückweg über "Ändern" (4.8, Speichern
   macht wieder eine einzelne Kategorie) zeigt den Toast "Aufteilung aufgehoben" mit
   Rückgängig. Neue `ToastActionKind`-Variante `UndoSplit`.
5. Die Zeile bleibt nach dem Speichern an ihrem Platz und ungeprüft (4.7).
3. Die Rechen- und Prüffunktionen kommen aus `Shared.Domain` (ADR 0006), nicht aus
   `SyncFlow`.

## Acceptance criteria
- [ ] Rahmen, vorbehaltlich der Verfeinerung nach `ledger-kxme4` (Platzhalter bis dahin):
  - [ ] Detail einer unaufgeteilten Buchung rendert "Kein Split" und den Knopf "Aufteilen"
        (Trefferfläche mindestens 44 px am Handy); bei aufgeteilter Buchung Zeilen und "Ändern".
  - [ ] Reine Funktionen: Rest gleich Gesamtbetrag minus Summe der positiv eingegebenen Beträge;
        "Rest" füllt die Zeile mit dem Rest; Speichern nur bei Rest 0,00 €; die Zeilen gehen mit
        dem Vorzeichen des Gesamtbetrags an `setEntrySplits`.
  - [ ] Reducer-Test: Speichern ruft `setEntrySplits` genau einmal; der Toast "Aufgeteilt in n
        Zeilen" trägt "Rückgängig"; Rückgängig stellt Kategorie oder alte Zeilen über
        `setEntrySplits` wieder her (zweiter Befehl, keine zweite Sonderlogik).
  - [ ] Ansichts-Test: Rest-Farbe (Pink, Green), "Zeile hinzufügen" ausgegraut bei Rest 0,00 €,
        "Speichern" inaktiv bei Rest ungleich null, am Handy kein "Abbrechen".
  - [ ] Die übrigen Kriterien nach `ledger-kxme4` und dem Spike `ledger-mas5g`.

## Notes
Verfeinert am 2026-10-24 (Sammel-Modeling, Ablauf 4): Handoff liegt vor, "Aufteilen" fest statt
optional, Beträge im Editor positiv, Rückgängig im Toast (Roman, 2026-10-24). Der Task bleibt
in backlog, weil `ledger-kxme4` und der Spike noch fehlen. Handoff: `docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md`.

Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (e, Oberfläche). Ziel-Lage: backlog.
(Korrigiert am 2026-10-24: der erste Entwurf nannte den Editor fälschlich ungestaltet; der
Handoff ist inzwischen geholt und gegen ADR 0053 abgeglichen.) Dateien: Ledger-Client
`Types.fs`, `State.fs`, `View.fs`, eine neue Ansichtsdatei für den Editor.
Überschneidung mit `ledger-sfab0` und `-3` in den drei Ledger-Client-Dateien.
