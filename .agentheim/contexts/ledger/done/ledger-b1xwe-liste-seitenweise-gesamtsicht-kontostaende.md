---
id: ledger-b1xwe
title: "Kassenbuch-Liste neu: seitenweise laden, Gesamtsicht über alle Konten, Kontostände-Leiste"
status: done
type: feature
context: ledger
created: 2026-09-12
completed: 2026-09-12
depends_on: [design-system-001]
blocks: [ledger-vgfw3, ledger-r1d2f, ledger-q1hw2, ledger-p3n8k, ledger-kdnnz]
tags: [frontend, backend, kassenbuch, scheibe]
related_adrs: [0013, 0017, 0029, 0030]
related_research: []
prior_art: [ledger-q5w9c, ledger-kt0wq]
---

## Why
Erste lesende Scheibe aus dem UX-Konzept (`ledger/README.md`, Abschnitt "Oberfläche";
abgeleitet in `ledger-s8m3w`). Sie ist das Fundament: ohne serverseitige, seitenweise
Abfrage gibt es weder Filter noch Summenzeile noch Gesamtsicht, und bei rund 14.000
Buchungen lädt die Seite heute alles eines Kontos auf einmal.

Zugleich erfüllt sie ein v1-Kriterium der Vision: "Buchungen pro Konto und über alle
Konten, wie YNABs All Accounts".

An dieser Scheibe spielt Roman erstmals an **echten Daten** durch, ob sich das Kassenbuch
wie eine Arbeitsfläche anfühlt. Der Prototyp-Durchgang aus `ledger-xbrht` ist bewusst
hierher verschoben.

## What
**Server.** Eine neue Sicht neben `getLedgerEntriesForAccount` (die bleibt, bis nichts
mehr auf sie zeigt): Buchungen seitenweise, wahlweise für ein Konto oder über alle Konten,
neueste zuerst. Rückgabe je Seite: die Buchungen, die Gesamtzahl der Treffer und die
Summe über den **ganzen** Treffer, nicht über die Seite. Beträge weiter als exakter
Dezimal-Text summiert, nie per SQL-`SUM` (ADR 0017).

Kontostände aller Konten in einem Aufruf, damit die Leiste nicht je Konto nachlädt.
Kontostand bleibt der Zustand über die nicht gelöschten Buchungen, er kommt nie aus der
geladenen Liste.

**Client.** Die Kassenbuch-Seite bekommt den Aufbau aus dem Konzept:
- Oben die **Kontostände**: am Handy eine waagerecht scrollbare Leiste mit "Alle Konten"
  zuerst, am PC eine linke Spalte mit "Alle Konten" zuoberst. Aktives Konto hervorgehoben.
- Darunter die **Liste**. Am Handy Datum als Gruppenkopf, Zeile mit Payee, Kategorie,
  Betrag, darunter klein Memo. Am PC eine Tabelle mit Sticky-Kopf.
- **Konto ist ein Filter**: der Wechsel zwischen Gesamtsicht und Konto behält die
  Scroll-Position nicht, aber alle sonstigen Zustände. In der Gesamtsicht trägt jede Zeile
  ihr Konto (Kürzel am Handy, Spalte am PC).
- **Nachladen beim Scrollen**, 100 Buchungen je Seite. Während des Nachladens ein
  Skeleton, kein Sprung.
- Herkunfts-Badge und Kennzeichen wie heute; das Historie-Sheet bleibt vorerst, wie es ist
  (Umbau in `ledger-q1hw2`).

**Nicht in dieser Scheibe:** Filter-Chips, Summenzeile über dem Treffer, Auswahl-Modus,
Detail-Umbau, Prüfvermerk, gelöschte ausblenden. Die haben eigene Tasks.

## Acceptance criteria
- [x] Eine neue Lesefunktion liefert Buchungen seitenweise: Parameter Konto (optional,
      leer bedeutet alle Konten), Seitenzeiger und Seitengröße; Rückgabe sind die
      Buchungen der Seite, die Gesamtzahl der Treffer und die Summe über den gesamten
      Treffer. Die Summe wird in F# über exakten Dezimal-Text gebildet, nicht per
      SQL-`SUM` (ADR 0017). Test: eine Seite aus einer mehrseitigen Menge trägt die
      Gesamtsumme, nicht die Seitensumme.
- [x] Eine Lesefunktion liefert alle Konten mit ihrem Kontostand in einem Aufruf; der
      Stand rechnet über die nicht gelöschten Buchungen und ist unabhängig davon, welche
      Seite der Liste geladen ist. Test: Kontostand bleibt gleich, egal wie viele Seiten
      geladen sind.
- [x] Die Kassenbuch-Seite zeigt oben die Kontostände: am Handy eine waagerecht
      scrollbare Leiste, am PC eine linke Spalte; "Alle Konten" ist in beiden der erste
      Eintrag und trägt die Summe aller Konten. Das aktive Konto ist hervorgehoben.
- [x] Ein Klick oder Tipp auf "Alle Konten" zeigt die Buchungen aller Konten, neueste
      zuerst; jede Zeile trägt ihr Konto (Kürzel am Handy, eigene Spalte am PC). Ein Klick
      auf ein Konto zeigt nur dessen Buchungen, ohne Konto-Angabe je Zeile.
- [x] Die Liste lädt beim Scrollen nach, 100 Buchungen je Seite. Während des Nachladens
      steht ein Skeleton am Listenende; die Scroll-Position springt nicht.
- [x] Am Handy gruppiert die Liste nach Datum; am PC ist es eine Tabelle mit Sticky-Kopf.
      Beide nutzen ausschließlich Komponenten und Tokens des Design-Systems
      (Styleguide-Gate `design-system-001`).
- [x] Herkunfts-Badge und das Kennzeichen "in YNAB gelöscht" bleiben wie bisher; das
      bestehende Historie-Sheet funktioniert unverändert weiter.
- [x] `dotnet build`, `dotnet test` und `npm run build` sind grün; die neuen
      Lesefunktionen haben Tests, die Seitenlogik und Summenbildung abdecken.
- [x] Tests schreiben nie in die Produktionsdatenbank (In-Memory oder temporäre DB).

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12, verfeinert am selben Tag. Konzept und
Begründung: `ledger/README.md`, Abschnitt "Oberfläche". Brief mit Schrittfolge und
erfundenen Beispieldaten: `docs/ux/kassenbuch/briefs.md`, Ablauf 6.

**Bei der Verfeinerung entschieden** (Annahmen von Claude, Roman kann beim Durchspielen
korrigieren):
- **"Alle Konten" ist ein eigener Eintrag** in der Kontostände-Leiste, kein Schalter. Es
  ist damit sichtbar, dass es die Summe aller Konten trägt, und der Wechsel ist eine
  Geste wie jeder Konto-Wechsel.
- **Überträge erscheinen in der Gesamtsicht mit beiden Seiten.** Das ist ehrlich: es sind
  zwei Buchungen. Dass sie sich in der Summe aufheben, ist richtig und kein Fehler. Eine
  Zusammenfassung beider Seiten zu einer Bewegung ist v2 (`ledger-t7v2q`).
- **Nachladen per Beobachter am Listenende**, nicht per Button. Ein Button wäre ein
  Klick je hundert Buchungen; beim Durchsehen am Handy ist das zu viel.

**Bleibt offen:** Roman hat den YNAB-Referenzweg für Ablauf 6 (wie er "All Accounts" und
die Konto-Sicht heute nutzt, was ihm an den Kontoständen fehlt, wie er mit dem ETF-Konto
umgeht) noch nicht beschrieben. Das blockiert diese Scheibe nicht, wäre aber beim
Durchspielen der Maßstab.

## Outcome

Server: zwei neue Sichten neben `getLedgerEntriesForAccount` (bleibt bestehen).
`getLedgerEntriesPage` (Konto optional, Seitenzeiger, Seitengröße → Buchungen der Seite,
Gesamtzahl, Summe über den ganzen Treffer) und `getLedgerAccountBalances` (alle
Kontostände in einem Aufruf, unabhängig von jeder geladenen Seite). Beide summieren in
F# (`Shared.Domain.sumMoney`, `ledgerBalancesByAccount`), nie per SQL-`SUM` (ADR 0017).
Client: `Components.Ledger.Types.AccountFilter = AllAccounts | OneAccount of Guid` löst
die bisherige `Guid option`-Doppelbedeutung auf; die Seite lädt seitenweise nach
(100/Seite, `IntersectionObserver`-Beobachter am Listenende), zeigt eine
Kontostände-Leiste (Handy horizontal scrollbar, PC linke Spalte, "Alle Konten" zuerst)
und eine Liste, die am Handy nach Datum gruppiert und am PC eine Sticky-Header-Tabelle
ist; in der Gesamtsicht trägt jede Zeile ein Konto-Kürzel. Entscheidungen dazu in
ADR 0029.

Kritischer Nebenfund: `dotnet test` rief nie `Main.fs`s `main` auf (VSTest lädt die
Test-Assembly als Bibliothek), also griff `USE_MEMORY_DB=true` nie — Tests schrieben in
die echte Entwicklungsdatenbank unter `~/my_apps/budgetbuddy-dev/budgetbuddy.db`.
Behoben über `src/Tests/test.runsettings` + `RunSettingsFilePath` in `Tests.fsproj`
(ADR 0030); `dotnet test` ist jetzt ohne manuelles Setzen der Umgebungsvariable sicher.
Die bereits geschriebenen Testdaten in der echten Datei wurden **nicht** entfernt (vom
Sandbox-Berechtigungsfilter blockiert, ohnehin Romans Entscheidung) — Backup unter
`~/my_apps/budgetbuddy-dev/budgetbuddy.db.pre-cleanup-backup-20260912-150615`,
Aufräum-Task in `backlog/ledger-vm3dk-...`.

**Wichtige Dateien:**
- `src/Shared/Domain.fs` — `LedgerEntryPageQuery`, `LedgerEntryPage`,
  `LedgerAccountBalanceView`, `LedgerBalanceInput`, `sumMoney`, `ledgerBalancesByAccount`.
- `src/Shared/Api.fs`, `src/Server/Api.fs` — `getLedgerEntriesPage`, `getLedgerAccountBalances`.
- `src/Server/Ledger/Persistence.fs` — `loadEntriesPage`, `countEntries`,
  `loadAmountsForQuery`, `loadAmountsForBalances`.
- `src/Server/Ledger/QueryHandlers.fs` — `getLedgerEntriesPage`, `getLedgerAccountBalances`.
- `src/Client/Components/Ledger/{Types,State,View}.fs` — `AccountFilter`, seitenweises
  Laden, Kontostände-Leiste, Datums-Gruppierung/Tabelle, `ScrollSentinel`.
- `src/Tests/LedgerPageTests.fs` (erweitert), `src/Tests/LedgerViewTests.fs` (neu).
- `src/Tests/test.runsettings`, `src/Tests/Tests.fsproj`, `src/Tests/Main.fs` — Fix des
  Test-Infra-Funds.
- `.agentheim/knowledge/decisions/0029-...md`, `0030-...md`.
- `.agentheim/contexts/ledger/backlog/ledger-vm3dk-...md`.
- `.agentheim/contexts/ledger/README.md` — Abschnitte "Sichten" und "Oberfläche"
  aktualisiert.

`dotnet build` (Solution), `dotnet test` (805 grün, 7 übersprungen, 0 Fehler, ohne
`USE_MEMORY_DB` von Hand gesetzt) und `npm run build` sind grün.

## Diary

**2026-09-12** — Server-seitige Paginierung und Kontostände-Query gebaut
(`getLedgerEntriesPage`, `getLedgerAccountBalances`), Client-Modell auf
`AccountFilter` umgestellt, seitenweises Nachladen per `IntersectionObserver`,
Kontostände-Leiste und Datums-Gruppierung/Tabelle gebaut. Beim Verifizieren mit
`dotnet test` zeigte sich ein aus der Reihe fallendes, deterministisch reproduzierbares
Scheitern unbeteiligter Tests (`LedgerMirrorTests.fs`, `LedgerTests.fs`) — auf den
ersten Blick nach Test-Parallelität aussehend, per Bisektion (git stash, gezielte
`--filter`-Läufe, Dateien einzeln entfernt/wiederhergestellt) aber auf eine viel
grundlegendere Ursache zurückgeführt: `dotnet test` ruft `Main.fs`s Einstiegspunkt nie
auf, also griff `USE_MEMORY_DB=true` nie, und alle Tests schrieben in die echte
Entwicklungsdatenbank. Fix über `test.runsettings` (VSTest-eigene Einstellung,
`[<ModuleInitializer>]` scheiterte am F#-Compiler, FS0202). Backup der Datenbank
gezogen, Bereinigung selbst vom Sandbox-Filter blockiert und ohnehin nicht Sache eines
Workers ohne Rückfrage — Fund und Backlog-Eintrag dokumentiert (ADR 0030,
`ledger-vm3dk`). Danach alle Akzeptanzkriterien grün verifiziert.

**2026-09-12 (Iteration 2)** — Beide vom Verifier gefundenen Textmängel behoben: die
ADR-Verweise in `src/Tests/Main.fs:17` und `src/Tests/test.runsettings:11` zeigen jetzt
korrekt auf ADR 0030 statt 0029 (Grep über den Worktree bestätigt, keine weiteren
Fehlverweise). ADR 0029 durchgehend auf echte Umlaute umgeschrieben (Frontmatter-Titel
und Überschrift eingeschlossen), Codeblöcke und Bezeichner unangetastet. `dotnet build`
und `dotnet test` erneut grün (805/7/0).


## Verifier note (iteration 1)

**VERDICT: FAIL** — zwei reine Textmängel, am Code ist nichts zu tun. Die inhaltliche
Arbeit wurde geprüft und ist vollständig (alle neun Akzeptanzkriterien gedeckt, ADR 0017
eingehalten, Scope sauber, `dotnet test` 805 grün und `npm run build` grün, der
`test.runsettings`-Mechanismus empirisch nachgeprüft).

**REASONS:**
1. `src/Tests/test.runsettings:11` und `src/Tests/Main.fs:17` verweisen beide auf
   "ADR 0029" für den Test-Datenbank-Fund. Der Fund steht in **ADR 0030**; ADR 0029 ist
   die Paginierung und sagt zur Datenbank nichts. Wer die Absicherung später hinterfragt,
   landet im falschen Dokument, ausgerechnet an der riskantesten Stelle des Diffs. Die
   Task-Datei und der README referenzieren korrekt 0030, nur die beiden Code-Kommentare
   nicht.
2. `.agentheim/knowledge/decisions/0029-seitenweise-kassenbuch-liste-und-kontostaende-leiste.md`
   ist durchgehend in umschriebenem Deutsch geschrieben (Zeilen 3, 14, 19, 55
   "Kontostaende"; 18, 25, 31, 45 "ueber"; 30 "Seitengroesse"; 42 "gruen"; 43, 65
   "Faelle"; 75 "zurueck"; 77 "waehrend"; 89, 91, 95 "Kuerzel"; 106 "Zustaende"; 112
   "Aufraeumen"). Das verletzt die Projektregel "bei deutschen Texten immer echte
   Umlaute, nie die Umschreibung". Keine lokale Konvention: alle 27 bestehenden ADRs
   verwenden ausschliesslich echte Umlaute, und ADR 0030 aus demselben Commit macht es
   richtig. ADR 0029 ist der einzige Ausreisser im Verzeichnis. (In den F#-Kommentaren
   ist die Umschreibung die vorhandene Konvention der jeweiligen Dateien; dort ist
   nichts zu beanstanden.)

**SUGGESTED_FIX:** Die beiden "ADR 0029"-Verweise in `src/Tests/Main.fs` und
`src/Tests/test.runsettings` auf ADR 0030 korrigieren und ADR 0029 auf echte Umlaute
umschreiben (Frontmatter-Titel und die `# ADR 0029`-Überschrift eingeschlossen). Reine
Textänderungen.

**ITERATION_HINT:** likely-fixable

**Zusätzliche Beobachtung des Verifiers (kein FAIL-Grund, als Backlog erfasst):** zwei
stille Umgehungswege des Datenbank-Schutzes bleiben, `dotnet test --settings
andere.runsettings` und ein direkter `dotnet vstest`-Aufruf. Beide fallen lautlos auf die
Datei-Datenbank zurück. Ein Wächter-Test, der den aktiven Connection-String prüft und laut
scheitert, wäre die Absicherung.
