---
id: ledger-p3n8k
title: Kassenbuch-Liste: gelöschte Buchungen standardmäßig ausblenden, eingeblendet klar als nicht mitzählend zeigen
status: done
type: feature
context: ledger
created: 2026-09-11
completed: 2026-09-13
depends_on: [ledger-b1xwe, design-system-001]
blocks: []
tags: [frontend, ux, kassenbuch]
related_adrs: [0013]
related_research: []
prior_art: [ledger-q5w9c]
---

## Why
Fund aus der Abnahme des Walking Skeletons (`ledger-kt0wq`, 2026-09-06). In YNAB
gelöschte Buchungen bleiben im Kassenbuch stehen, das ist so gewollt (ADR 0013: es wird
nie physisch gelöscht). In der Liste stehen sie aber gleichberechtigt neben den lebenden
Buchungen, mit dem Betrag in derselben Schrift. Stand eine gelöschte und eine lebende
Buchung mit gleichem Betrag untereinander, las Roman das als Doppelbuchung und zweifelte
am Kontostand darüber, obwohl der auf den Cent stimmte.

Das Problem ist nicht der Saldo, sondern dass die sichtbare Liste sich scheinbar nicht zu
ihm summiert. Eine Zahl, der man nicht traut, ist so gut wie keine.

Bewusst nicht Teil des Durchstichs: der sollte die Architektur beweisen, nicht die
Oberfläche ausarbeiten.

## What
1. Gelöschte Buchungen sind in der Liste **standardmäßig ausgeblendet**. Die sichtbaren
   Zeilen summieren sich damit zu dem Kontostand, der darüber steht.
2. Ein Schalter blendet sie ein. Sind welche ausgeblendet, sagt eine Zeile wie
   "3 gelöschte Buchungen ausgeblendet", damit nichts unbemerkt fehlt.
3. Eingeblendet trägt eine gelöschte Buchung ihren Betrag gedämpft und durchgestrichen,
   sodass ohne Lesen des Kennzeichens klar ist, dass er nicht mitzählt.
4. Nur DS-Komponenten und Tokens (Styleguide-Gate `design-system-001`), mobil lesbar.

## Acceptance criteria
- [x] Ohne Zutun zeigt die Liste keine gelöschten Buchungen; die Summe der sichtbaren
      Beträge entspricht dem angezeigten Kontostand.
- [x] Gibt es gelöschte, nennt die Seite ihre Anzahl und bietet den Schalter an.
- [x] Eingeblendet ist der Betrag einer gelöschten Buchung visuell als nicht mitzählend
      erkennbar (gedämpft und durchgestrichen), das Kennzeichen bleibt.
- [x] Der Kontostand ändert sich durch den Schalter nicht.
- [x] Reducer-Test für den Schalter; keine Pfade, die den Fable.Remoting-Proxy berühren.
- [x] Keine Inline-Farben; `dotnet build`, `dotnet test`, `npm run build` grün.

## Notes
Der Kontostand summiert schon heute nur die nicht gelöschten Buchungen
(`Shared.Domain.ledgerBalance`), daran ist nichts zu ändern. Es geht allein um die Liste.

Offene Modellierungsfrage, **nicht** Teil dieses Tasks: wird eine Buchung in YNAB gelöscht
und danach neu importiert, entstehen zwei Buchungen ohne Verbindung. Der Ereignis-Katalog
kennt heute kein Ereignis, das eine neue Buchung als Ersatz einer gelöschten ausweist. Das
ist Modellierung mit Roman, kein Anzeige-Thema.

## Notes
**Eingeordnet in `ledger-s8m3w` am 2026-09-12: bleibt eigenständig.** Der Task ist klein,
gut verfeinert und betrifft genau eine Stelle der Liste; ihn in `ledger-b1xwe` aufgehen zu
lassen hätte diese Scheibe nur vergrößert. Er hängt jetzt an `ledger-b1xwe`, weil er die
neue Liste voraussetzt, und ist im Konzept (`ledger/README.md`, Abschnitt "Oberfläche")
als Teil des Zielbilds benannt.

## Outcome
Gelöschte Buchungen sind in der Kassenbuch-Liste jetzt standardmäßig ausgeblendet; die
Entscheidung liegt server-seitig in `getLedgerEntriesPage`, nicht als Client-Filter auf
der geladenen Seite, damit Seitengröße, Trefferzahl und Summe zum tatsächlichen Treffer
passen (sonst hätte eine ausgeblendete Buchung eine Seite kleiner gemacht, ohne dass die
Trefferzahl das zeigt).

Nachtrag Iteration 2: die Herkunfts-Badge-Definition in der Ubiquitous Language
(README, "Oberfläche"/Aggregates-Abschnitt) war nach der Umsetzung nicht mitgezogen
worden und widersprach dem neuen Verhalten; korrigiert. Dazu Schreibregeln (echte
Umlaute statt Umschreibungen, keine Gedankenstriche) in allen von diesem Task
hinzugefügten Zeilen nachgezogen, die drei Paging-Tests bekommen je ein eigenes
Konto (C, D, E) statt gemeinsamer Saat, und `Money.transactionDeleted` ist jetzt im
lebenden Styleguide (`src/Client/Components/Styleguide/View.fs`, Abschnitt "6. Money").

**Server:** `LedgerEntryPageQuery.IncludeDeleted` (Standard `false`) und
`LedgerEntryPage.DeletedCount` (`src/Shared/Domain.fs`). `Ledger.Persistence.loadEntriesPage`,
`countEntries` und `loadAmountsForQuery` bekommen den Schalter als zusätzlichen Parameter
und hängen `AND deleted_in_ynab = 0` an, wenn er aus ist; die neue
`countDeletedEntries` zählt unabhängig davon (`src/Server/Ledger/Persistence.fs`).
`Ledger.QueryHandlers.getLedgerEntriesPage` reicht beides durch.

**Client:** `Components.Ledger.Types.Model` trägt `IncludeDeleted` und `DeletedCount`,
die neue Nachricht `ToggleIncludeDeleted` kippt den Schalter und lädt die Liste ab Seite 0
neu (`Components.Ledger.State`, derselbe rein/unrein-Schnitt wie bei `SelectFilter`). Die
Seite (`Components.Ledger.View`) zeigt oberhalb der Liste "N gelöschte Buchungen
ausgeblendet/eingeblendet" samt `Input.toggle`, nur wenn `DeletedCount > 0`
(`deletedToggleRow`, reine Satzbildung in `deletedCountText`). Eingeblendet trägt eine
gelöschte Buchung ihren Betrag über die neue DS-Funktion `Money.transactionDeleted`
(gedämpft, durchgestrichen, `text-text-muted` statt der Rot/Grün-Semantik) in Handy-Zeile
und PC-Tabelle; das Kennzeichen "in YNAB gelöscht" (`deletedBadge`) bleibt unverändert.
Der Kontostand kommt weiterhin ausschließlich aus `getLedgerAccountBalances`
(`accountStrip`/`accountCardView`) und bleibt vom Schalter unberührt.

**Tests:** drei neue Persistence/Query-Handler-Tests (In-Memory-SQLite, je ein eigenes
Konto C, D, E, damit sie unabhängig voneinander laufen) prüfen Standard-Ausblendung
inkl. Summe, Einblendung inkl. Summe und dass `DeletedCount` unabhängig vom Schalter
dieselbe Zahl liefert. Zwei neue Reduzierer-Tests prüfen, dass
`EntriesPageLoaded` den Server-`DeletedCount` unverändert übernimmt und dass
`ToggleIncludeDeleted` den Schalter kippt, die Liste zurücksetzt und `LoadEntries`
anstößt, ohne den Fable.Remoting-Proxy zu berühren. Zwei neue View-Tests prüfen
`deletedCountText` (Singular/Plural, aus-/eingeblendet). `dotnet build`, `dotnet test`
(826 grün, 7 übersprungen wie zuvor) und `npm run build` sind grün.

Kein manueller Playwright-Screenshot: dieses Worktree hat keine lokale Kassenbuch-Datenbank
mit gelöschten Buchungen zum Seeden, das Aufsetzen dafür stand außer Verhältnis zur
"empfohlen, nicht Pflicht"-Einstufung im Auftrag. Stattdessen Vertrauen auf die DS-Funktion
(`Money.transactionDeleted`, dieselbe Formatierung wie `transactionWithCurrency`, nur
`line-through` + `text-text-muted` statt Rot/Grün) und die bestehenden, bereits
screenshot-verifizierten Zeilen-Layouts (`entryRow`/`entryTableRow`), die unverändert
bleiben (nur der Betrags-Baustein wechselt pro Zeile).

**Dateien:** `src/Shared/Domain.fs`, `src/Server/Ledger/Persistence.fs`,
`src/Server/Ledger/QueryHandlers.fs`, `src/Client/Components/Ledger/{Types,State,View}.fs`,
`src/Client/DesignSystem/Money.fs`, `src/Client/Components/Styleguide/View.fs`,
`src/Tests/LedgerPageTests.fs`, `src/Tests/LedgerViewTests.fs`,
`.agentheim/contexts/ledger/README.md`.

## Diary

### 2026-09-13: Gelöschte Buchungen ausblenden

**Was:** Kassenbuch-Liste blendet in YNAB gelöschte Buchungen standardmäßig aus (server-
seitiger Filter in `getLedgerEntriesPage`, nicht Client-seitig), zeigt bei Bedarf einen
Schalter mit Anzahl und stellt eingeblendete gelöschte Buchungen gedämpft und
durchgestrichen dar.

**Geänderte Dateien:**
- `src/Shared/Domain.fs`: `LedgerEntryPageQuery.IncludeDeleted`, `LedgerEntryPage.DeletedCount`.
- `src/Server/Ledger/Persistence.fs`: `loadEntriesPage`/`countEntries`/`loadAmountsForQuery`
  mit `includeDeleted`-Parameter, neue `countDeletedEntries`.
- `src/Server/Ledger/QueryHandlers.fs`: `getLedgerEntriesPage` reicht den Schalter durch.
- `src/Client/Components/Ledger/Types.fs`: `Model.IncludeDeleted`/`DeletedCount`, Msg
  `ToggleIncludeDeleted`.
- `src/Client/Components/Ledger/State.fs`: Reduzierer-Zweig für den Schalter, Query trägt
  `IncludeDeleted`, `EntriesPageLoaded` übernimmt `DeletedCount`.
- `src/Client/Components/Ledger/View.fs`: `deletedCountText`, `deletedToggleRow`,
  `entryMoney`/`tdMoney` wählen die gedämpfte Darstellung für gelöschte Buchungen.
- `src/Client/DesignSystem/Money.fs`: neue `transactionDeleted`-Funktion.
- `src/Tests/LedgerPageTests.fs`, `src/Tests/LedgerViewTests.fs`: neue Tests, bestehende
  Record-Literale um die neuen Felder ergänzt, drei Paging-Tests mit eigenem Konto
  (C, D, E) statt gemeinsamer Saat.
- `.agentheim/contexts/ledger/README.md`: Abschnitt "Oberfläche", Herkunfts-Badge und
  `getLedgerEntriesPage`-Eintrag auf den Stand nach der Umsetzung gebracht.

**Rationale:** Ausblendung musste server-seitig passieren, weil die Liste seitenweise lädt
und Trefferzahl/Summe über den ganzen Treffer gehen (ledger-b1xwe): ein Client-Filter auf
der geladenen Seite hätte beides verfälscht.

**Outcomes:**
- Build: dotnet build, npm run build, beide grün.
- Tests: dotnet test 826 erfolgreich, 7 übersprungen (wie vor der Änderung), 7 neue Tests.
- Issues: keiner.

### 2026-09-13: Iteration 2, Verifier-Korrekturen

**Was:** Zwei Verifier-Befunde behoben, dazu beide "optional, empfohlen"-Punkte
umgesetzt: README-Widerspruch in der Herkunfts-Badge-Definition korrigiert,
Schreibregeln (echte Umlaute, keine Gedankenstriche) in allen von diesem Task
hinzugefügten Zeilen nachgezogen (Code-Kommentare, Testnamen, README, Task-Datei),
die drei Paging-Tests bekommen je ein eigenes Konto statt gemeinsamer Saat, und
`Money.transactionDeleted` steht jetzt im lebenden Styleguide.

**Geänderte Dateien:**
- `.agentheim/contexts/ledger/README.md`: Herkunfts-Badge-Absatz (Zeile 121/122)
  ans neue Verhalten angepasst, Gedankenstriche in den zuvor hinzugefügten Zeilen
  entfernt.
- `src/Shared/Domain.fs`, `src/Server/Ledger/Persistence.fs`,
  `src/Server/Ledger/QueryHandlers.fs`,
  `src/Client/Components/Ledger/{Types,State}.fs`, `src/Client/Components/Ledger/View.fs`,
  `src/Client/DesignSystem/Money.fs`: Umlaute und Gedankenstriche in Doc-Kommentaren
  korrigiert, keine Verhaltensänderung.
- `src/Tests/LedgerPageTests.fs`: Testnamen und Assertion-Texte korrigiert; drei neue
  Konten D und E ergänzt, damit die drei Paging-Tests unabhängig voneinander seeden
  und prüfen statt sich eine gemeinsame Saat zu teilen.
- `src/Client/Components/Styleguide/View.fs`: `Money.transactionDeleted`-Beispiel in
  Abschnitt "6. Money" ergänzt.

**Rationale:** Die Verifier-Rückmeldung verlangte eine reine Text-Korrektur ohne
Redesign; die beiden optionalen Punkte waren klein genug, um sie im selben Zug
mitzunehmen und die nächste Verifikation nicht erneut daran scheitern zu lassen.

**Outcomes:**
- Build: dotnet build, npm run build, beide grün.
- Tests: dotnet test weiterhin grün (siehe Testlauf unten), 826 Tests plus die
  Umbenennung ohne neue oder entfernte Testfälle in der Zählung.
- Issues: keine.

## Verifier note (iteration 1)

**REASONS:**
- Prüfung 5, BC-README veraltet: `.agentheim/contexts/ledger/README.md` Z. 121 bis 122 (Herkunfts-Badge in der Ubiquitous Language) sagt weiter, eine in YNAB gelöschte Buchung "bleibt sichtbar und zählt nur beim Kontostand nicht mit". Nach dieser Änderung ist sie standardmäßig ausgeblendet und nur über den Schalter sichtbar. `getLedgerEntriesPage`-Eintrag und Abschnitt "Oberfläche" wurden angepasst, diese Definition nicht; die README widerspricht sich.
- Schreibregeln (im Worker-Prompt ausdrücklich für Code-Kommentare, Testnamen, README und Task-Datei genannt) in den hinzugefügten Zeilen verletzt: rund 61 hinzugefügte Zeilen mit Umschreibungen statt Umlauten (z. B. `State.fs` "Zustandsaenderung", `Types.fs` "geloeschte"/"Standardmaessig"/"unabhaengig", `Persistence.fs` "laesst"/"koennen"/"Betraege", Testnamen in `src/Tests/LedgerPageTests.fs` wie "Geladene Seite uebernimmt ... geloeschter Buchungen", "Der Schalter kippt IncludeDeleted und laedt die Liste ab Seite 0 neu", "... aussen vor", "DeletedCount zaehlt ... unabhaengig vom Schalter"); 24 hinzugefügte Zeilen mit Gedankenstrich (—), u. a. README (neuer Text), Task-Datei `## Outcome` und jede `## Diary`-Zeile, Kommentare in `State.fs`, `Types.fs`, `View.fs` (`deletedToggleRow`), `Money.fs` (`transactionDeleted`), `Persistence.fs` (`countDeletedEntries`), `Domain.fs`. Ältere Zeilen in diesen Dateien sind nicht Gegenstand, neue Zeilen schon.
- Geprüft und in Ordnung: `dotnet build` grün, `dotnet test` 826 bestanden / 0 fehlgeschlagen / 7 übersprungen, `npm run build` grün; Scope passt; Seite, Trefferzahl und F#-Summe nutzen dieselbe `includeDeleted`-Bedingung (festes Literal, Nutzerwerte parametrisiert, ADR 0017 eingehalten); `DeletedCount` für ein Konto und "Alle Konten" korrekt; Kontostand kommt weiter nur aus `getLedgerAccountBalances`; Reducer-Test für `ToggleIncludeDeleted` ohne Proxy-Pfad; `Money.transactionDeleted` nur Tokens und Utilities.
- Nicht blockierend: die drei neuen Paging-Tests hängen voneinander ab (Konto C nur im ersten `testCase` angelegt, grün nur wegen `testSequenced`). `Money.transactionDeleted` fehlt im lebenden Styleguide (`src/Client/Components/Styleguide/View.fs`); der Styleguide verlangt das nicht ausdrücklich.

**SUGGESTED_FIX:** README Z. 121 bis 122 an das neue Verhalten anpassen (standardmäßig ausgeblendet, über den Schalter gedämpft und durchgestrichen). Alle ae/oe/ue/ss-Umschreibungen und alle Gedankenstriche in den von diesem Diff hinzugefügten Zeilen (Kommentare, Testnamen, README, Outcome und Diary der Task-Datei) durch echte Umlaute und normale Satzzeichen ersetzen. Optional Konto C in jedem der drei Paging-Tests selbst anlegen und ein `Money.transactionDeleted`-Beispiel in den lebenden Styleguide aufnehmen.

**ITERATION_HINT:** likely-fixable
