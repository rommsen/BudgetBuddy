---
id: ledger-k2rbn
title: "Kategorie ändern in BB (v2), Schreibweg für eine Buchung: Befehl setEntryCategory, Ereignis EntryRecategorized, PUT nach YNAB, Fehlschlag mit Baseline, Erneut senden, YNAB gewinnt, Schreib-Sperre gegen den Spiegel"
status: doing
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001, ledger-q1hw2]
blocks: [ledger-sfab0, ledger-w29y6, ledger-kxme4, ledger-xfjcp]
tags: [backend, kassenbuch, kategorie, v2, schreibweg]
related_adrs: [0028, 0050, 0051, 0053, 0054, 0055]
related_research: []
prior_art: [ledger-r777c, ledger-nhwrt]
---

## Why
Vision v2 ("Ownership"): Roman öffnet YNAB nicht mehr zum Nachbearbeiten, Kategorie ändern
ist der häufigste Fall. Roman hat am 2026-10-24 entschieden, v2 mit dieser Fähigkeit zu
beginnen (ADR 0054). Diese Aufgabe ist der Server-Teil für eine Buchung; Oberfläche und
Picker sind eigene Aufgaben (`design-system-gffps`, `ledger-sfab0`), die Sammelaktion ist
`ledger-w29y6`.

## What
Entscheidungen in ADR 0055; hier die Umsetzung, im Muster von ADR 0050 und
`Ledger.CommandHandlers.changeReviewMark`.

1. **Vertrag** (`Shared.Domain`, `Shared.Api.LedgerApi`):
   - `setEntryCategory: Guid -> YnabCategoryId option -> Async<Result<CategoryChangeOutcome, string>>`.
     `None` heißt "ohne Kategorie".
   - `retryCategorizationWriteBack: Guid -> Async<Result<CategoryWriteBack, string>>`.
   - `CategoryChangeOutcome = { Previous: CategoryRef-ähnlich (Id und Name, option; die Id wird für Rückgängig und die Karte im Client gebraucht); Change: CategoryChanged | CategoryUnchanged; WriteBack: CategoryWriteBack }`,
     `CategoryWriteBack = CategorySent | CategorySendFailed of reason | CategoryNotSent`.
   - `LedgerEntryView.CategorizationWriteBackFailure: string option`.
2. **Befehl** `setEntryCategory` in `Ledger.CommandHandlers`: lädt die Buchung, lehnt ab
   (Fehler, kein Ereignis): unbekannte Buchung, in YNAB gelöscht, Übertrag ohne Kategorie und
   ohne Split-Zeilen (`TransferAccountId` gesetzt), Buchung mit Split-Zeilen, unbekannte
   Kategorie-Id (Name und Gruppe aus `ledger_categories`, `liveCategoryName`). Ziel gleich
   Ist nach `categorizationIdentity`: kein Ereignis, kein Push, `CategoryUnchanged`.
   Sonst `EntryRecategorized` (Actor `UserInBb`, `OldPayee = NewPayee`, Splits leer) per
   `appendAndApply`, danach der Push (ADR 0055, Punkt 1 und 2).
3. **Push** `YnabClient.sendCategory` (injizierbarer `HttpClient`) und `updateCategory`:
   PUT `.../transactions/{id}`, Body `{"transaction":{"category_id": <id|null>}}`, nur
   dieses Feld. Fehlerzuordnung wie `sendReviewMark`; Grund-Text über eine Funktion
   neben `describeReviewMarkPushError`.
4. **Fehlschlag** (ADR 0055, Punkt 4): Ereignisse `EntryCategorizationWriteBackFailed`
   (Reason, Baseline) und `EntryCategorizationWrittenBackToYnab` (Version 1, Codec,
   Namens-Abbildung, `apply`), `Entry.CategorizationWriteBackFailure`. Ohne YNAB-Verknüpfung
   sofort der Fehlschlag "noch nicht in YNAB verknüpft". `retryCategorizationWriteBack`
   sendet den aktuellen BB-Stand; ohne offenen Fehlschlag tut er nichts.
5. **`observe`** (ADR 0055, Punkt 5): ohne offenen Fehlschlag unverändert. Mit offenem
   Fehlschlag: beobachteter Stand gleich Baseline: kein Ereignis; gleich BB-Stand:
   `EntryCategorizationWrittenBackToYnab` (Actor `YnabMirror`); sonst das
   Beobachtungs-Ereignis, das den Fehlschlag in `apply` beendet.
6. **Schreib-Sperre** (ADR 0055, Punkt 6): `MirrorGuard` und `setEntryCategory` samt
   `retryCategorizationWriteBack` teilen einen Wächter; Befehl und Spiegel-Lauf laufen nicht
   ineinander.
7. **Historie:** `describeRecategorized` bleibt; zusätzlich Sätze für die zwei neuen
   Ereignisse ("Kategorie nicht in YNAB: <Grund>", "Kategorie in YNAB angekommen").

## Acceptance criteria
- [ ] Domain-Test `apply`: `EntryRecategorized` mit Splits leer setzt Kategorie und lässt die
      übrigen Felder; `EntryCategorizationWriteBackFailed` setzt den Zustand samt Baseline,
      `EntryCategorizationWrittenBackToYnab` löscht ihn; eine zweite Änderung bei offenem
      Fehlschlag behält die erste Baseline.
- [ ] Codec-Test: beide neuen Ereignisse laufen verlustfrei durch Encode und Decode und
      haben Namen und Version 1 in der Abbildung.
- [ ] Handler-Test `setEntryCategory` (Fake-Push): Änderung schreibt genau ein
      `EntryRecategorized` (Actor `UserInBb`) vor dem Push und genau einen Push mit
      `category_id`; Erfolg erzeugt kein weiteres Ereignis, `WriteBack = CategorySent`.
- [ ] Handler-Test Rücknahme: `setEntryCategory` mit dem alten Wert nach einer Änderung (auch
      von und nach "ohne Kategorie") schreibt ein zweites `EntryRecategorized` und einen zweiten
      Push; `Previous` trägt Id und Namen der vorherigen Kategorie. Das genügt für das
      Rückgängig im Toast, ein eigener Befehl ist nicht nötig.
- [ ] Handler-Test: Ziel gleich Ist erzeugt weder Ereignis noch Push (`CategoryUnchanged`);
      Wechsel von einer Kategorie auf "ohne Kategorie" sendet `category_id = null`.
- [ ] Handler-Test: scheitert der Push (Netz, 400, 429), steht `EntryCategorizationWriteBackFailed`
      im Log, die Kategorie bleibt in BB gesetzt, `CategorySendFailed reason` kommt zurück;
      ohne YNAB-Verknüpfung entsteht der Fehlschlag mit "noch nicht in YNAB verknüpft".
- [ ] Handler-Test: gelöschte Buchung, Übertrag ohne Kategorie und ohne Splits,
      Split-Buchung und unbekannte Kategorie liefern `Error` ohne Ereignis.
- [ ] Handler-Test `retryCategorizationWriteBack`: Erfolg schreibt
      `EntryCategorizationWrittenBackToYnab` und löscht den Zustand; erneuter Fehler ersetzt
      den Grund; ohne offenen Fehlschlag `CategoryNotSent`.
- [ ] `observe`-Tests mit offenem Fehlschlag: YNAB zeigt die Baseline: kein Ereignis, Zustand
      bleibt; YNAB zeigt den BB-Stand: `WrittenBackToYnab` (Actor `YnabMirror`); YNAB zeigt
      eine dritte Kategorie: `EntryCategorizationChangeObservedInYnab`, Zustand beendet.
      Ohne offenen Fehlschlag übernimmt `observe` einen abweichenden YNAB-Stand
      (YNAB gewinnt).
- [ ] Wächter-Test: ein `setEntryCategory`, das während eines laufenden Spiegel-Laufs
      startet, schreibt sein Ereignis erst nach dem Ende des Laufs.
- [ ] JSON-Vertragstest `buildCategoryBody`: nur `category_id` im Body, `null` für
      "ohne Kategorie"; PUT-Test mit Fake-`HttpClient`: Pfad, Methode, Fehlerzuordnung
      (200, 400, 401, 404, 429).
- [ ] Lese-Test: `LedgerEntryView.CategorizationWriteBackFailure` ist der Grund bei offenem
      Fehlschlag, sonst `None`; Historie-Sätze der zwei neuen Ereignisse.
- [ ] `dotnet build`, `dotnet test` grün. Die BC-README ist nachgezogen (neue Ereignisse,
      Befehle, Zustand).

## Notes
Verfeinert am 2026-10-24 (Sammel-Modeling): aus der groben Task (Picker, zwei Einstiege,
Schreibweg) in vier Scheiben geschnitten. Diese Task behält nur den Server-Teil für eine
Buchung. Picker: `design-system-gffps`, Einstieg Detail: `ledger-sfab0`, Auswahl-Leiste:
`ledger-w29y6` (braucht `ledger-w4vte`, daher aus `depends_on` hier entfernt), Splits:
`ledger-mas5g` bis `-6`, Regel-Angebot: `ledger-hg5m5`. Offene Fragen aus der
Vorfassung sind entschieden: Splits werden mitgebaut (Pfeil öffnet den Split-Editor),
YNAB gewinnt bei Konflikt, Überträge ohne Kategorie gesperrt, Sortierung: Zeile bleibt,
Hinweisleiste "Neu laden" (Client, `ledger-sfab0`), Regel-Angebot eigene Task. ADR 0054
und 0055 halten das fest.

Dateien, die ein Worker anfasst: `src/Shared/Domain.fs`, `src/Shared/Api.fs`,
`src/Server/Api.fs`, `src/Server/YnabClient.fs`, `src/Server/Ledger/Events.fs`, `Domain.fs`,
`CommandHandlers.fs`, `QueryHandlers.fs`, `Persistence.fs` (nur falls die Projektion Spalten
braucht), Tests.

Verfeinert am 2026-10-24 (Sammel-Modeling, Ablauf 4): Rückgängig im Toast (Roman, 2026-10-24)
braucht serverseitig nichts Neues: der Client ruft `setEntryCategory` mit `Previous` auf, die
Historie zeigt beide Ereignisse. Nur `Previous.Id` muss im Ergebnis stehen. Handoff:
`docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md`.

Hinweis zur Namensfalle: `describeRecategorized` sagt "In BB umkategorisiert". Roman nennt
die Zeitleistenzeile "In BB geändert"; der Satz bleibt, weil er Alt und Neu schon nennt.
