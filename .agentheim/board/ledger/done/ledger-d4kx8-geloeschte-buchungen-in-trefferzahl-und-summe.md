---
id: ledger-d4kx8
title: "Gelöschte Buchungen: Entwurf zählt sie in der Trefferzahl, aber nicht in der Summe; gebaut zählen sie in beidem"
status: done
type: decision
context: ledger
created: 2026-09-18
completed:
depends_on: []
blocks: []
tags: [frontend, backend, kassenbuch, summenzeile, entwurf-gegen-code]
related_adrs: [0017]
related_research: []
prior_art: [ledger-p3n8k, ledger-b1xwe]
---

## Why
Beim Abgleich der Screens aus Claude Design mit dem Repo (2026-09-18) aufgefallen.

**Der Entwurf** (Ablauf 7): gelöschte Buchungen "zählen **in der Trefferzahl, nicht in der
Summe**" und nie im Kontostand. Als offener Punkt steht dort die Alternative daneben: sie
ganz aus Zähler und Summe nehmen, dann stimmt die Zahl aber nicht mehr mit den sichtbaren
Zeilen überein.

**Gebaut ist das nicht so.** Bei eingeblendeten gelöschten Buchungen
(`IncludeDeleted = true`) läuft dieselbe SQL-Klausel für beides:
- `src/Server/Ledger/Persistence.fs:538` (Trefferzahl) und `:584` (Beträge) bauen je
  `let deletedClause = if includeDeleted then "" else "AND deleted_in_ynab = 0"`.
- `src/Server/Ledger/QueryHandlers.fs:300-316` bildet daraus `TotalCount` und
  `TotalAmount = sumMoney "EUR" totalAmounts`; `sumMoney` (`src/Shared/Domain.fs:873-876`)
  summiert alles, was es bekommt.

Gelöschte zählen eingeblendet also in Zähler **und** Summe. Der Kontostand geht bewusst
einen anderen Weg (`ledgerBalancesByAccount` lässt gelöschte immer draußen), deshalb können
Summenzeile und Kontostand bei eingeschaltetem Schalter auseinanderlaufen. Genau diese
Übereinstimmung nennt der Doc-Kommentar in `Persistence.fs:579-580` als Zweck des
Standardfalls.

## What
Entscheiden, was die Summenzeile bei eingeblendeten gelöschten Buchungen zeigt, und die
gewählte Variante herstellen.

1. **Wie im Entwurf:** Zähler mit, Summe ohne. Die Summe bleibt dann mit dem Kontostand
   vergleichbar. Preis: Zähler und Summe beziehen sich auf verschiedene Mengen, das braucht
   eine Beschriftung, die das sagt.
2. **Wie gebaut:** beides mit. Ehrlich gegenüber der sichtbaren Liste (die Zeilen summieren
   sich zur Summe), weicht aber vom Kontostand ab.
3. **Dritte Variante aus dem Entwurf:** gelöschte ganz aus Zähler und Summe nehmen, obwohl
   sie sichtbar sind.

`ledger-p3n8k` hat den Standardfall (ausgeblendet) bereits sauber gelöst; hier geht es nur
um den eingeschalteten Schalter. Die Anzeige "N gelöschte Buchungen eingeblendet" steht
schon über der Liste und ist der natürliche Ort für den Hinweis.

## Entscheidung (Roman, 2026-09-18)
**Variante 2 gilt: eingeblendet zählen gelöschte Buchungen in Trefferzahl und Summe.** Der
gebaute Stand bleibt, der Entwurf wird angepasst. Begründung: so addieren sich die
sichtbaren Zeilen genau zur angezeigten Summe. Die Variante des Entwurfs hätte Zähler und
Summe über verschiedene Mengen gebildet, was ohne erklärenden Text nicht zu verstehen ist.

Was daraus folgt:
1. **Die Zeile über der Liste sagt es aus**, im Stil "13 Buchungen, davon 1 in YNAB
   gelöscht". Die bestehende Zeile "N gelöschte Buchungen eingeblendet" aus `ledger-p3n8k`
   ist der Ort dafür.
2. Der **Kontostand** lässt gelöschte weiterhin immer draußen
   (`ledgerBalancesByAccount`). Dass Summe und Kontostand bei eingeschaltetem Schalter
   auseinanderlaufen, ist damit gewollt und benannt, nicht mehr ein stiller Widerspruch.
3. **Am Code ändert sich nichts** an Trefferzahl und Summe; die Arbeit steckt in der
   Beschriftung und darin, dass die Summenzeile aus `ledger-vgfw3` dieselbe Regel trägt.
4. Im Design-Projekt steht die Gegenregel noch in der `CLAUDE.md` (Abschnitt "Aus Ablauf 7
   dazugekommen"). Sie muss dort weg, sonst kommt sie beim nächsten Zeichnen zurück.
   **Das kann kein Werkzeug von hier aus tun:** `CLAUDE.md` und `.claude/` sind im
   Design-Projekt reservierte Pfade und werden abgelehnt, weil sie den Design-Agenten
   steuern. Der einzufügende Text liegt fertig in
   `docs/ux/kassenbuch/design-projekt-nachtrag-2026-09-18.md`, Abschnitt 2; Roman gibt ihn
   in einen Chat des Design-Projekts.

## Acceptance criteria
- [ ] Am Code für Trefferzahl und Summe ändert sich nichts: `getLedgerEntriesPage` liefert
      bei `IncludeDeleted = true` weiterhin beide **einschließlich** gelöschter Buchungen
      (Variante 2). Ein Test hält das fest, damit die Regel nicht später still kippt.
- [ ] Die Zeile über der Liste sagt bei eingeblendeten gelöschten Buchungen, wie viele der
      gezählten gelöscht sind, im Stil "13 Buchungen, davon 1 in YNAB gelöscht". Ort ist die
      bestehende Zeile aus `ledger-p3n8k` (`deletedCountText` /
      `deletedBanner` in `src/Client/Components/Ledger/View.fs`).
- [ ] Sind gelöschte Buchungen ausgeblendet (Standard), bleibt die Zeile wie heute
      ("N gelöschte Buchungen ausgeblendet") — dieser Fall ist von `ledger-p3n8k` gelöst und
      wird nicht angefasst.
- [ ] Singular und Plural stimmen in beiden Zuständen; durch Tests der reinen
      Textfunktion belegt, nicht per Augenschein.
- [ ] Der Kontostand lässt gelöschte Buchungen weiterhin immer draußen
      (`ledgerBalancesByAccount`), auch bei eingeschaltetem Schalter. Ein Test hält fest,
      dass Summenzeile und Kontostand hier bewusst auseinanderlaufen dürfen.
- [ ] Die Regel steht im ledger-README, damit die Summenzeile aus `ledger-vgfw3` sie
      übernimmt statt sie neu zu entscheiden.
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

**Nicht Teil dieser Aufgabe:** Punkt 4 der Entscheidung (die Gegenregel in der `CLAUDE.md`
des Design-Projekts). Das kann kein Werkzeug von hier aus: `CLAUDE.md` und `.claude/` sind
dort reservierte Pfade. Der fertige Text liegt in
`docs/ux/kassenbuch/design-projekt-nachtrag-2026-09-18.md`, Abschnitt 2; Roman gibt ihn in
einen Chat des Design-Projekts.

## Notes
Erfasst am 2026-09-18 beim Abgleich mit dem Design-Projekt "Gelesen UI-Mockups"
(`5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`). Brief: `docs/ux/kassenbuch/briefs.md`, Ablauf 7.

Betrifft die Summenzeile aus `ledger-vgfw3` mit: sobald sie gebaut wird, muss die Regel
feststehen.

## Verifier note (iteration 1)

**VERDICT: FAIL** — ITERATION_HINT: likely-fixable

**Der Fehlschlag betrifft ausschließlich das README-Delta. Code und Tests sind grün und
bleiben, wie sie sind.**

**REASONS:**

- **Das README-Delta lässt einen direkt widersprechenden Satz stehen.**
  `.agentheim/knowledge/contexts/ledger/README.md:366-367` lautet weiterhin:
  `steht darüber "N gelöschte Buchungen ausgeblendet" (bzw. "eingeblendet") samt Schalter`.
  Nach diesem Diff heißt das eingeblendete Label aber
  "13 Buchungen, davon 1 in YNAB gelöscht" (`src/Client/Components/Ledger/View.fs:101-103`).
  Das zitierte `(bzw. "eingeblendet")` wird also in dem Moment falsch, in dem die Aufgabe
  integriert wird. Das Delta ist ein bloßes `append`; keine Operation korrigiert das Bullet,
  das genau dieses Label beschreibt.
- **Die eingefügte Zeile landet in einer Lückenliste.** Der `append` zielt auf den Abschnitt
  `Oberfläche (ledger-xbrht, 2026-09-12)`, dessen letzte Bullet-Liste die Überschrift
  `**Was das Konzept voraussetzt und heute nicht gibt** (Scheiben-Material für ledger-s8m3w)`
  trägt (README.md:379-392). Ein angehängtes `- **Regel für die Summenzeile...**` wird damit
  zu einem weiteren Mitglied dieser Liste: eine entschiedene, ausgelieferte Regel liest sich
  dort als unerfüllte Voraussetzung.
- **AC6 will die Regel dort, wo der `ledger-vgfw3`-Worker sie sucht.** Dieser Leser geht zum
  Absatz `**Gelöschte Buchungen**` (README.md:362-369) oder zum `getLedgerEntriesPage`-Eintrag
  unter `## Sichten` (README.md:104-115), der heute nur die `IncludeDeleted = false`-Hälfte
  der Regel nennt.

**Was bereits geprüft und grün ist, damit die nächste Iteration es nicht wiederholt:**

- Check 1: jedes Akzeptanzkriterium ist einem Artefakt zugeordnet (AC1 der neuen
  `LedgerPageTests`-Zusicherung `shown.TotalAmount = -2020` plus dem kommentierten
  Pinning-Test, AC2/AC4 den drei `deletedCountText`-Tests, AC3 dem unveränderten
  ausgeblendeten Zweig, AC5 dem neuen Divergenz-Test, AC7 den beiden grünen Kommandos).
- Check 2: `dotnet test` vom Verifier selbst aus dem Worktree gefahren:
  `Fehler: 0, erfolgreich: 916, übersprungen: 7, gesamt: 923`, Exit 0. `npm run build` Exit 0.
- Check 3: drei Dateien, alle unter `src/`. `src/Server/Ledger/Persistence.fs` und
  `QueryHandlers.fs` sind nachweislich unberührt, genau wie AC1 es verlangt.
- Check 4: keine neuen Domänenbegriffe; `Trefferzahl`, `Summe` und `Kontostand` stehen bereits
  in der README.
- Genau eine produktive Aufrufstelle (`View.fs:699`), Argumente in Deklarationsreihenfolge
  (`model.TotalCount` vor `model.DeletedCount`), **nicht vertauscht**; der gerenderte Satz ist
  durch `deletedCountText 13 1 true` festgenagelt.
- Beide Zweige bilden Singular und Plural an der richtigen Zahl, auch im Grenzfall 1/1. Die
  `0` als Platzhalter in den ausgeblendeten Testfällen ist ein Lesbarkeitsmakel, kein Defekt,
  weil `deletedToggleRow` bei `DeletedCount <= 0` ohnehin `Html.none` liefert.
- Fixture-Konto G (`...000000000010`) und die Buchungen 40/41 sind in der Datei eindeutig, die
  Liste ist `testSequenced`, und die einzigen kontoübergreifenden Zusicherungen sind tolerant
  (`isGreaterThanOrEqual`, `List.find`).
- Der neue Test würde einen Kontostand fangen, der anfinge, gelöschte Buchungen mitzuzählen:
  `Expect.equal balanceG.Balance {-20}` trägt das Gewicht.
- Keine Gedankenstriche im hinzugefügten Text, echte Umlaute durchgehend.

**SUGGESTED_FIX:**

Das `README_DELTA` zu einem `replace` auf den Absatz `**Gelöschte Buchungen**`
(Anker README.md:362-369) umbauen, der die entschiedene Regel trägt **und** das zitierte
eingeblendete Label auf "13 Buchungen, davon 1 in YNAB gelöscht" korrigiert. Optional eine
zweite Operation, die den `getLedgerEntriesPage`-Eintrag unter `## Sichten` um die
`IncludeDeleted = true`-Hälfte ergänzt. Den bloßen `append` in die `ledger-s8m3w`-Lückenliste
fallen lassen. Am Code und an den Tests ist nichts zu ändern, beide Suiten sind grün.

Betroffene Dateien für den Fix: `.agentheim/knowledge/contexts/ledger/README.md`
(Zeilen 104-115 und 362-392) und `src/Client/Components/Ledger/View.fs` (Zeilen 88-103, 699,
nur als Referenz für den korrekten Labeltext, nicht zu ändern).

## Outcome

Iteration 1 hat die Trefferzahl- und Summenzeile über der Kassenbuch-Liste so geändert, dass eingeblendete gelöschte Buchungen mitgezählt werden (Variante 2 aus der Entscheidung Roman, 2026-09-18): Trefferzahl und Summe schließen sie ein, der Kontostand bleibt unberührt. Umgesetzt in `src/Client/Components/Ledger/View.fs:699` (einziger Aufrufort der geänderten Zeile), abgesichert durch zwei Tests in `src/Tests/LedgerViewTests.fs` und `src/Tests/LedgerPageTests.fs` (Fixture-Konto G, Buchungen 40/41), unter anderem für den 1/1-Grenzfall bei Singular/Plural. `dotnet test`: 916 bestanden / 7 übersprungen / 923 insgesamt; `npm run build` grün. Dieser Stand war fachlich korrekt, der Verifier hat ihn ausschließlich wegen des README-Delta zurückgewiesen und in Iteration 2 nichts am Code oder an den Tests geändert (Worktree-Diff zu `git log` HEAD `480efc8` ist leer).

Der Fehler in Iteration 1 lag im `README_DELTA`: ein bloßes `append` hätte die neue Regel als weitere Zeile ans Ende der Lücken-Liste "Was das Konzept voraussetzt und heute nicht gibt" (Scheiben-Material für `ledger-s8m3w`) gehängt, obwohl die Regel bereits umgesetzt und keine offene Lücke mehr ist. Außerdem wäre der bestehende Absatz `**Gelöschte Buchungen**` (README.md, Abschnitt "Oberfläche (ledger-xbrht, 2026-09-12)", Zeilen 362-369) unverändert stehen geblieben, obwohl sein Halbsatz "(bzw. \"eingeblendet\")" durch die neue Beschriftung "13 Buchungen, davon 1 in YNAB gelöscht" falsch geworden ist.

Iteration 2 ersetzt diesen Absatz per `replace`-Operation im selben Abschnitt. Anchor ist der fette Lead-in `Gelöschte Buchungen` (kein `(` darin, also keine Kürzung nötig). `expected` ist der komplette Absatz wörtlich aus der Datei übernommen, die acht hart umgebrochenen Zeilen zu einem Fließtext mit einfachen Leerzeichen zusammengefügt (Vergleich ist laut Anleitung whitespace-kollabiert). Der neue `body` behält alles weiterhin Zutreffende (Standard ausgeblendet, Schalter, gedämpfte/durchgestrichene Darstellung, Kontostand bleibt unberührt, Server-Zweig statt Client-Filter, schlichte Zeile statt Filter-Chip), korrigiert den falschen Halbsatz und trägt die entschiedene Regel samt neuem Beschriftungsstil ein.

Die zweite, im Verifier-Hinweis als optional markierte Ergänzung (den `getLedgerEntriesPage`-Eintrag unter `## Sichten` um die `IncludeDeleted = true`-Hälfte erweitern) habe ich bewusst weggelassen: dieser Eintrag beginnt nicht mit einem fetten Lead-in, sondern mit Inline-Code (`` `getLedgerEntriesPage` (ledger-b1xwe), eine **Seite** ...``). Der Anchor-Mechanismus ist laut Vorgabe an einen fetten Lead-in gebunden; ein Anchor auf einen Code-Lead-in hätte, wie beim Sibling-Vorfall heute Morgen, vermutlich nicht getroffen und wäre still zu einem `merged`-Append degradiert, der einen zweiten, widersprüchlichen Eintrag angehängt hätte statt den bestehenden zu korrigieren. Die eine, sicher adressierbare `replace`-Operation auf den `Gelöschte Buchungen`-Absatz trägt die entschiedene Regel bereits vollständig; die `## Sichten`-Ergänzung bleibt eine Formulierungsfeinheit für eine künftige, gezielt dafür geplante README-Pflege.

Kein Code, kein Test und keine Datei im Worktree wurden in Iteration 2 verändert; `FILE_LIST` und `TESTS_ADDED` entsprechen weiterhin Iteration 1.
