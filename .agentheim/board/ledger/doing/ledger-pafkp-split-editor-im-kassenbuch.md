---
id: ledger-pafkp
title: "Split-Editor im Kassenbuch: Pfeil an der Kategorie einer Split-Buchung öffnet das Formular-Sheet zum Aufteilen, Ändern und Aufheben der Zeilen mit Rest-Anzeige und Speichern über setEntrySplits, Aufteilen als fester Knopf, Rückgängig im Toast"
status: doing
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [design-system-001, ledger-sfab0, ledger-kxme4, design-system-gffps]
blocks: []
tags: [frontend, kassenbuch, split, v2]
related_adrs: [0005, 0006, 0049, 0053, 0055, 0058, 0059]
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
Abgeglichen mit dem gebauten Stand von `ledger-kxme4`, `ledger-sfab0`, `design-system-gffps`
und `design-system-wfhxj` (2026-10-25, siehe Notes):
1. **Einstieg im Detail.** Bei einer unaufgeteilten Buchung zeigt das Detail-Feld Split "Kein
   Split" mit festem Knopf "Aufteilen" (Tafel 4.4, 4.9; nicht optional, Roman, 2026-10-24), bei
   einer aufgeteilten die Zeilen (`Detail.splitLines`) mit Knopf "Ändern" (4.7). Der Pfeil an
   der Kategorie-Zeile (`Detail.pickerArrow`) öffnet bei einer Split-Buchung den Editor; heute
   ist er dort gesperrt (`categoryLockFor` liefert `CategoryLockedSplit`, Hinweis "Split
   bearbeiten folgt" in `categoryLockHint`). Diese Sperre entfällt; Pfeil, "Aufteilen" und
   "Ändern" folgen der neuen Split-Sperre aus Punkt 3.
2. **Editor.** Formular-Sheet mit Speichern und Abbrechen, am PC im Panel, am Handy im selben
   Sheet. Je Zeile Kategorie oder Übertrag-Ziel, Betrag, Memo, "Rest"-Knopf, Zeile entfernen;
   darüber Gesamtbetrag und Rest; "Zeile hinzufügen". Kategorie je Zeile über den Picker aus
   `design-system-gffps` als zweite Ebene (ADR 0005): `CategoryPicker.popoverInPanel` am PC,
   `CategoryPicker.sheetLayered` am Handy, mit `LoadState` und `OnRetry` wie im
   Kategorie-Feld (`ledger-dmw83`). Für das Übertrag-Ziel gibt es am Handy
   `BottomSheet.accountPickerLayered`; am PC fehlt ein Konto-Popover, ebenso fehlt im
   Kassenbuch eine Liste der offenen Budget-Konten mit YNAB-Id (`LedgerAccountsView` trägt
   kein `OnBudget`; der Import-Ablauf filtert YNAB-Konten mit `openOnBudgetAccounts`). Beides
   gehört zu dieser Task (Roman, 2026-10-25: Übertrag-Ziel bleibt in dieser Task, keine
   Folge-Task). Optische Vorlage ist der Import-Split-Editor
   (`src/Client/Components/SyncFlow/Views/SplitSheet.fs`, `splitLineRow`, `splitAmountClass`);
   was der neue Editor davon braucht, wird ins Design-System gehoben (Zuordnungstabelle im
   Handoff), `SplitSheet.fs` bleibt im Verhalten unberührt.
3. **Split-Sperre im Lesepfad.** `LedgerEntryView` liefert heute nur `DeletedInYnab` und
   `IsTransfer`. Es fehlen: abgeglichen (`Cleared = Reconciled`), Tracking-Konto,
   `import_id` ohne Präfix `BB:` und der Zustand "in YNAB gelöscht, Neuanlage offen"
   (`CategorizationWriteBackFailure` ist nur der Grund-Text, `YnabDeletedTransactionId` steht
   nicht im View). Der Lesepfad bekommt dafür neue Felder (Vorschlag: ein Sperrgrund für
   Aufteilen und einer für Ändern/Aufheben, plus ein Merkmal "Neuanlage offen"), abgeleitet
   nach denselben Regeln wie `splitNotChangeableReason`, die Tracking-Prüfung und die
   Fremd-`import_id`-Prüfung in `Ledger.CommandHandlers.setEntrySplits`. Neue Felder in einem
   Remoting-Record brauchen einen Fallback für Antworten eines älteren Servers (Muster
   `CategorizationWriteBackFailure`). Die Tracking-Angabe kennt die Projektion heute nicht
   (der Befehl fragt die Konten live ab, `ledger-kxme4` Abweichung 2).
4. **Speichern** ruft `LedgerApi.setEntrySplits: Guid -> TransactionSplit list ->
   Async<Result<CategoryChangeOutcome, string>>`. Zwei oder mehr Zeilen teilen auf oder
   ersetzen die Zeilen; bleibt genau eine Zeile übrig, geht sie mit dem vollen Betrag als
   Kategorie- oder `Uncategorized`-Zeile hin und hebt den Split auf. Eine leere Liste und eine
   einzelne Übertrags-Zeile lehnt der Server ab, der Editor schickt sie nie. Antwort,
   Versandfehler und Hinweisleiste laufen wie bei `setEntryCategory` in `ledger-sfab0`
   (`CategoryChanged`/`CategoryUnchanged`, `CategorySendFailed`, Hinweis über `Detail.notice`,
   "Erneut senden" über `StartRetryCategorization` und `retryCategorizationWriteBack`, das
   seit `ledger-kxme4` Kategorie- und Split-Weg wählt). Liste: ausgegraute Zeile bei
   Filter-Nichttreffer (`FilteredOut`) und `OrderStale` bei `SortByCategory` wie in
   `ledger-sfab0`. Für "in YNAB gelöscht, Neuanlage offen" zeigt die Hinweisleiste einen
   eigenen Text.
5. **Beträge im Editor positiv** eingegeben, das Vorzeichen kommt vom Gesamtbetrag (Notiz 7
   des Handoffs); die Übersicht im Detail zeigt sie mit Vorzeichen ("-23,40 €"). Die Rest-Farbe
   folgt `splitAmountClass` (ADR 0049: neutral bei 0,00 €, `text-neon-red` bei einem Rest
   ungleich null); die Pink/Green-Angaben der Tafel 4.5 und 4.6 gelten nicht (Handoff,
   Zuordnungstabelle: "keine eigene Pink/Green-Wahl"). "Zeile hinzufügen" ausgegraut bei Rest
   0,00 €, "Rest"-Knopf je Zeile, Fußleiste mit "Abbrechen" und "Speichern" am PC, am Handy
   "Speichern" klebend unten und kein "Abbrechen". Bausteine je Element stehen in der
   Zuordnungstabelle des Handoffs, Abschnitt "Tafel-Element und heutiger Baustein".
6. **Rückgängig im Toast** (Roman, 2026-10-24): "Aufgeteilt in n Zeilen" mit Knopf
   "Rückgängig". `CategoryChangeOutcome.Previous` trägt nur die Kategorie, deshalb hält der
   Client den Stand vor dem Speichern selbst (Kategorie oder `Splits` aus `LedgerEntryView`)
   und stellt ihn über denselben Schreibweg wieder her: war die Buchung unaufgeteilt,
   `setEntrySplits` mit einer Zeile der vorherigen Kategorie über den vollen Betrag
   (`Uncategorized`, wenn sie ohne Kategorie war); war sie aufgeteilt, `setEntrySplits` mit
   den alten Zeilen. Zweites Ereignis, zweiter Push, die Historie zeigt beide. Der Rückweg über
   "Ändern" (4.8, Speichern macht wieder eine einzelne Kategorie) zeigt den Toast "Aufteilung
   aufgehoben" mit Rückgängig. Neue Variante `ToastActionKind.UndoSplit` in
   `src/Client/Types.fs`, übersetzt in `toastActionMsg` (`src/Client/State.fs`) wie
   `UndoCategory`.
7. **Zeile bleibt** nach dem Speichern an ihrem Platz und ungeprüft (4.7). Der Client ersetzt
   sie über `LedgerEntryView.Id` (BB-Eintrags-Id); die YNAB-Id steht nicht im View, ein Ersatz
   in YNAB ändert an der BB-Zeile nichts außer Kategorie und Splits.
8. Die Rechen- und Prüffunktionen kommen aus `Shared.Domain` (`splitRemainder`, `mkSplits`,
   ADR 0006), nicht aus `SyncFlow`.

## Acceptance criteria
- [ ] Detail einer unaufgeteilten Buchung rendert "Kein Split" und den Knopf "Aufteilen"
      (Trefferfläche mindestens 44 px am Handy); bei aufgeteilter Buchung die Zeilen mit
      Vorzeichen und den Knopf "Ändern". Der Pfeil an der Kategorie öffnet bei einer
      Split-Buchung den Editor; der Hinweis "Split bearbeiten folgt" ist entfernt.
- [ ] Reine Funktionen: Rest gleich Gesamtbetrag minus Summe der positiv eingegebenen
      Beträge; "Rest" füllt die Zeile mit dem Rest; Speichern nur bei Rest 0,00 €; die Zeilen
      gehen mit dem Vorzeichen des Gesamtbetrags an `setEntrySplits`. Rechen- und
      Prüffunktionen aus `Shared.Domain` (`splitRemainder`, `mkSplits`), nicht aus `SyncFlow`.
- [ ] Reine Funktion: bleibt beim Speichern genau eine Zeile übrig, geht sie als einzelne
      Zeile mit vollem Betrag an `setEntrySplits` (Kategorie oder `Uncategorized`), und der
      Toast heißt "Aufteilung aufgehoben". Eine leere Liste oder eine einzelne Übertrags-Zeile
      wird nie geschickt (Speichern inaktiv).
- [ ] Je Zeile ist eine Kategorie (Picker aus `design-system-gffps`, PC Popover, Handy
      `sheetLayered`) oder ein Übertrag auf ein offenes Budget-Konto wählbar (Handy
      `BottomSheet.accountPickerLayered`, PC ein Konto-Popover aus dem Design-System); die
      Kontenliste enthält nur offene Budget-Konten (`openOnBudgetAccounts`, Test mit Werten).
- [ ] Reducer-Test: Speichern ruft `setEntrySplits` genau einmal; der Toast "Aufgeteilt in n
      Zeilen" trägt "Rückgängig" (`ToastActionKind.UndoSplit`); Rückgängig ruft
      `setEntrySplits` mit den alten Zeilen bzw. mit einer Zeile der alten Kategorie über den
      vollen Betrag (zweiter Befehl, keine Sonderlogik).
- [ ] Reducer-Test: "Rückgängig" fehlt im Toast, wenn der Rückweg gesperrt wäre (Aufteilen
      einer Buchung, deren `import_id` nicht mit `BB:` beginnt, etwa `YNAB:…`; das Aufheben
      danach lehnt `setEntrySplits` ab).
- [ ] Lesepfad (Server-Test je Fall): `LedgerEntryView` trägt die Split-Sperre für
      abgeglichen, Tracking-Konto, Übertrag zwischen Budget-Konten, in YNAB gelöscht und
      `import_id` ohne `BB:` (nur Ändern/Aufheben gesperrt), dazu das Merkmal "Neuanlage
      offen"; ein älterer Server ohne die Felder ergibt "nicht gesperrt" statt eines Absturzes.
- [ ] Gesperrte Fälle (Ansichts- oder Reducer-Test je Fall): bei abgeglichen, Tracking-Konto,
      Übertrag zwischen Budget-Konten und in YNAB gelöschter Buchung sind "Aufteilen",
      "Ändern" und der Pfeil inaktiv mit Hinweistext im Detail; bei einer Split-Buchung mit
      `import_id` ohne `BB:` ist nur "Ändern" inaktiv. Die Angaben kommen aus `LedgerEntryView`,
      nicht aus Rohdaten im Client geraten.
- [ ] Die Hinweisleiste aus `ledger-sfab0` zeigt für den Fall "in YNAB gelöscht, Neuanlage
      offen" einen eigenen Text und "Erneut senden" (Reducer-Test: "Erneut senden" schickt
      `StartRetryCategorization` und ruft `retryCategorizationWriteBack`).
- [ ] Nach dem Speichern bleibt die Zeile an ihrem Platz und ungeprüft (4.7); Reducer-Test:
      die Zeile wird über `LedgerEntryView.Id` ersetzt, Liste und Detail zeigen danach die
      neuen Splits (bzw. die Kategorie nach dem Aufheben). Filter-Nichttreffer und
      `OrderStale` verhalten sich wie bei der Kategorieänderung.
- [ ] Ansichts-Test: Rest-Farbe über `splitAmountClass` (neutral bei 0,00 €, Warnfarbe bei
      Rest ungleich null), "Zeile hinzufügen" ausgegraut bei Rest 0,00 €, "Speichern" inaktiv
      bei Rest ungleich null, am Handy kein "Abbrechen".
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3, Vorrangregel in den Handoffs):
      keine Farb- oder Maßwerte aus den Design-Tafeln; ein neuer DS-Baustein erscheint in
      `/styleguide` (`src/Client/Components/Styleguide/View.fs`); `npm run guard:tokens` und
      `npm run guard:classes` grün.
- [ ] `dotnet build`, `dotnet test`, `npm run build` und `npm run test:scripts` (seriell) grün.
- [ ] Das Neue fügt sich optisch in die gebauten Screens (Kassenbuch, Buchungs-Detail,
      `/styleguide`) ein und fällt daneben nicht auf. [human-eye]

## Notes
Verfeinert am 2026-10-24 (Sammel-Modeling, Ablauf 4): Handoff liegt vor, "Aufteilen" fest statt
optional, Beträge im Editor positiv, Rückgängig im Toast (Roman, 2026-10-24). Der Task bleibt
in backlog, bis `ledger-kxme4` gebaut ist (Promote-Sperre über `depends_on`). Acceptance criteria
verfeinert am 2026-10-24 nach Probelauf und Annahme von ADR 0058. Handoff: `docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md`.

Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (e, Oberfläche). Ziel-Lage: backlog.
(Korrigiert am 2026-10-24: der erste Entwurf nannte den Editor fälschlich ungestaltet; der
Handoff ist inzwischen geholt und gegen ADR 0053 abgeglichen.) Dateien: Ledger-Client
`Types.fs`, `State.fs`, `View.fs`, eine neue Ansichtsdatei für den Editor.
Überschneidung mit `ledger-sfab0` und `-3` in den drei Ledger-Client-Dateien.

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).

Abgleich mit dem gebauten Code (2026-10-25): Geprüft gegen `src/Shared/Api.fs`, `src/Shared/Domain.fs`, `Ledger.CommandHandlers`,
die Ledger-Client-Dateien, `CategoryPicker.fs`, `BottomSheet.fs`, `SplitSheet.fs`,
`package.json` und die Outcomes der vier Abhängigkeiten. Stimmt: `setEntrySplits`
(Signatur jetzt in What 4), `splitRemainder` und `mkSplits` in `Shared.Domain`,
`OrderStale`, `retryCategorizationWriteBack` als Weg für "Erneut senden", `npm run
guard:tokens` (Zusatz "sobald integriert" gestrichen). Geändert: (1) "Wird nach
`ledger-kxme4` verfeinert" ersetzt, doppelte Nummer 3 aufgelöst. (2) `LedgerEntryView` trägt
weder abgeglichen noch Tracking-Konto noch `import_id` noch "Neuanlage offen"; das ist jetzt
Arbeit dieser Task (What 3, eigenes Kriterium). (3) Die Fremd-`import_id`-Regel des Servers
heißt "beginnt nicht mit `BB:`", `YNAB:…` ist nur ein Beispiel. (4) Rückgängig nach dem
Aufteilen: eine Zeile der alten Kategorie über den vollen Betrag; eine leere Liste lehnt der
Server ab. (5) Rest-Farbe nach `splitAmountClass` (`text-text-primary` bei 0,00 €, `text-neon-red` sonst) statt Pink/Green, wie es die
Zuordnungstabelle des Handoffs verlangt. (6) Picker-Aufrufe nach dem gebauten Stand
(`popoverInPanel`, `sheetLayered`, `LoadState`); Übertrag-Ziel braucht eine Kontenliste und
am PC ein Konto-Popover, beides fehlt bisher. (7) Der Hinweis "Split bearbeiten folgt" und
`CategoryLockedSplit` werden abgelöst. (8) Die Zeile wird über `LedgerEntryView.Id` gefunden;
die YNAB-Id steht nicht im View. (9) ADR 0059 in `related_adrs` ergänzt.

## Spike-Ergebnis und Probelauf (2026-10-24)

ADR 0058 ist angenommen (Probelauf gegen ein Wegwerf-Budget). Folgen für die Oberfläche:
1. Aufteilen ist ein einzelner Aufruf, die YNAB-Id bleibt. Ändern und Aufheben laufen als
   Löschen und Neuanlegen: Speichern kann länger dauern, und es gibt den zusätzlichen
   Fehlschlag "in YNAB gelöscht, Neuanlage offen" mit "Erneut senden".
2. Gesperrt: abgeglichene Buchungen (`reconciled`, Roman 2026-10-24) ganz; Split-Buchungen
   mit fremder `import_id` (`YNAB:…`) nur für Ändern und Aufheben. Roman hat solche Buchungen
   derzeit nicht.
3. "Rückgängig" nach Ändern oder Aufheben ist ein zweiter Ersatz und kostet zwei weitere
   YNAB-Aufrufe; die Historie zeigt beide Schreibvorgänge.
4. Die YNAB-Id ändert sich nach einem Ersatz, der Eintrag im Kassenbuch bleibt derselbe.

Entscheidung am 2026-10-25 (Roman): Das Übertrag-Ziel je Split-Zeile samt Kontenliste und
Konto-Popover am PC bleibt in dieser Task. Woher die Tracking-Angabe für den Lesepfad kommt
(Feld `OnBudget` in der Konten-Projektion oder Live-Abruf), entscheidet der Worker als
technische Frage; Vorgabe ist nur, dass der Lesepfad die Sperrgründe liefert.
