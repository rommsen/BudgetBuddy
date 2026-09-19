---
id: ledger-ezrw0
title: "getLedgerEntriesPage im ledger-README um die IncludeDeleted-true-Hälfte ergänzen"
status: done
type: chore
context: ledger
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [doku, kassenbuch, readme]
related_adrs: [0017]
related_research: []
prior_art: [ledger-d4kx8, ledger-b1xwe, ledger-p3n8k]
---

## Why
`ledger-d4kx8` hat am 2026-09-19 Romans Entscheidung festgehalten: sind gelöschte Buchungen
eingeblendet, zählen sie bewusst in Trefferzahl **und** Summe, während der Kontostand sie
immer ausklammert. Die Regel steht seitdem im Absatz `**Gelöschte Buchungen**` des
ledger-README.

Der Eintrag zu `getLedgerEntriesPage` unter `## Sichten` beschreibt aber weiterhin nur die
andere Hälfte. Er sagt heute:

> Ohne `IncludeDeleted` bleiben gelöschte Buchungen aus Seite, Trefferzahl und Summe außen
> vor, sodass die sichtbare Liste sich zum Kontostand summiert

Das ist richtig, aber unvollständig: was bei eingeschaltetem Schalter gilt, steht dort nicht.
Wer die Sicht nachschlägt, um sie zu benutzen, liest die halbe Regel und muss den zweiten
Absatz kennen, um die andere Hälfte zu finden.

Der Verifier von `ledger-d4kx8` hat das ausdrücklich als Backlog-Kandidaten benannt und
zugleich festgestellt, dass daran kein Kriterium jener Aufgabe hängt: der Eintrag wird
unvollständig, nicht falsch.

## What
Den `getLedgerEntriesPage`-Eintrag unter `## Sichten` im ledger-README um die
`IncludeDeleted = true`-Hälfte ergänzen: eingeblendet zählen gelöschte Buchungen in
Trefferzahl und Summe mit, der Kontostand (`getLedgerAccountBalances` /
`ledgerBalancesByAccount`) lässt sie weiterhin immer draußen, und die Abweichung zwischen
Summenzeile und Kontostand ist dadurch gewollt.

**Ein Werkzeug-Hinweis, der eine Iteration spart.** Dieser Eintrag ist ein eingerückter
Unterpunkt, der mit Inline-Code beginnt (`` `getLedgerEntriesPage` (ledger-b1xwe) — … ``).
`applyReadmeDelta` adressiert ausschließlich Zeilen, die an Spalte 0 mit `- ` beginnen
(`BULLET_START_RE`). Ein `replace` auf diesen Eintrag findet seinen Anker nie, liefert
`anchor-missing` und hängt den Text stattdessen ans Ende der letzten Bullet-Liste des
Abschnitts. Genau das ist `ledger-d4kx8` in Iteration 2 passiert, weshalb dort von Hand
gesetzt wurde. Wer diese Aufgabe umsetzt, sollte das einkalkulieren und die Grenze im
Ergebnis benennen, statt gegen sie zu laufen.

## Acceptance criteria
- [ ] Der `getLedgerEntriesPage`-Eintrag unter `## Sichten` nennt beide Hälften: das
      Verhalten ohne und mit `IncludeDeleted`.
- [ ] Die Aussage deckt sich wörtlich mit dem Verhalten im Code (`getLedgerEntriesPage` in
      `src/Server/Ledger/QueryHandlers.fs`) und mit dem Absatz `**Gelöschte Buchungen**`
      weiter unten im selben README. Kein dritter, abweichender Wortlaut.
- [ ] Die bestehende Aussage über den Kontostand bleibt erhalten und wird nicht
      abgeschwächt: gelöschte Buchungen bleiben dort immer draußen.
- [ ] Falls das Delta-Werkzeug den Eintrag nicht erreichen kann, steht das im Ergebnis
      benannt, statt dass der Text an einer anderen Stelle landet.

## Notes
Erfasst am 2026-09-19 aus der Verifikation von `ledger-d4kx8` (Iteration 2, PASS). Reine
Doku-Aufgabe: am Code ändert sich nichts, die Regel selbst ist entschieden und umgesetzt.

Zusammenhang: die Summenzeile über der Liste ist `ledger-vgfw3`. Diese Aufgabe hier stellt
sicher, dass deren Worker die Regel auch dann findet, wenn er über die Sichten-Liste
einsteigt und nicht über den Oberflächen-Abschnitt.

## Verifier note (iteration 1)

**REASONS:**
- `expected` und `body` des Deltas entfernen die führende Einrückung des gesamten
  `**Sichten**`-Blocks. 31 der 32 `expected`-Zeilen unterscheiden sich von
  `.agentheim/knowledge/contexts/ledger/README.md` Zeilen 96-127 im führenden Leerraum (nur
  Zeile 96, das Spalte-0-Eltern-Bullet, stimmt überein). Die sechs Unterbullets stehen in der
  README auf Einrückung 2 und im Delta auf 0; ihre Fortsetzungszeilen stehen in der README
  auf 4 und im Delta auf 0.
- Die Behauptung im OUTCOME, "alle übrigen fünf Unterpunkte sind zeichengleich aus README.md
  Zeilen 96-127 übernommen", ist falsch. Ihr **sichtbarer Text** ist identisch (`expected`
  gegen `body` diffiert: genau ein Hunk, auf das `getLedgerEntriesPage`-Unterbullet begrenzt,
  kein Geschwistertext angefasst), aber jedes Geschwister verliert seine Einrückung. Das ist
  eine ungeprüfte Änderung an fünf unbeteiligten Bullets, also genau das Kollateral, das der
  Anker-Ausweg vermeiden sollte.
- Der Schaden ist nachgestellt, nicht hergeleitet: `applyReadmeDelta` (Plugin-Cache 0.9.4,
  `lib/readme-delta.mjs`) mit diesem Delta gegen die aktuelle README laufen lassen —
  Disposition `applied`, `replaceRange` splict `body` wörtlich ein. Die entstehende README
  hat die ganze Sichten-Liste auf Spalte 0 (neue Zeilen 96-132); die sechs Lesefunktionen
  werden damit Geschwister von `**Kassenbuch**`, `**Konto**` und den übrigen
  Ubiquitous-language-Begriffen statt Kinder von `**Sichten**`. Die Gruppierung, die den
  Abschnitt lesbar macht, ist zerstört.
- Zweitschaden: nach dem Flachklopfen sieht `findBulletsInRange` die Unterbullets als
  Spalte-0-Bullets, die `Sichten`-Bullet-Einheit schrumpft auf ihre 3 Kopfzeilen. Nachgestellt
  durch ein Folge-`replace` mit Anker `Sichten` gegen die flachgeklopfte Ausgabe: es landet
  zwischen den Kopfzeilen und `getLedgerAccounts` (Disposition `merged`) und deckt nur den
  Kopf ab. Die Unterbullets tragen dann keinen fetten Begriffskopf mehr, `termHeadOf` liefert
  für alle `null`, und kein künftiges `replace` kann sie je wieder erreichen. Die Notluke,
  die dieser Worker benutzt hat, fällt hinter ihm zu — und die beiden von ihm gemeldeten
  Folgeaufgaben (`ledger-f8q2x`, `ledger-t4m9c`) zielen genau auf diesen Block.

**SUGGESTED_FIX:** Dasselbe Delta erneut ausgeben, aber mit der echten Einrückung der README
in `expected` UND `body`: 2 Leerzeichen auf den Fortsetzungszeilen des Eltern-Bullets und auf
jedem `- `-Unterbullet, 4 Leerzeichen auf jeder Fortsetzungszeile eines Unterbullets, Byte für
Byte wie README-Zeilen 96-127, abgesehen von der beabsichtigten Ergänzung beim
`getLedgerEntriesPage`-Unterbullet. Der `expected`-Abgleich leidet darunter nicht, weil
`collapseWs` vor dem Vergleich jeden Leerraum normalisiert; nur `body` wird wörtlich
eingespliced, deshalb ist dessen Einrückung das, worauf es ankommt.

**Ausdrücklich geprüft und in Ordnung (nicht erneut anfassen):**
- Der neue Text ist gegen den Code nach `ledger-vgfw3` korrekt. `toEntryFilter` reicht
  `IncludeDeleted` an `buildFilterClause` (`src/Server/Ledger/Persistence.fs:604`), das
  daraufhin das Prädikat `AND deleted_in_ynab = 0` weglässt; gelöschte Buchungen zählen also
  tatsächlich in `loadEntriesPage` (Seite), `countEntries` (TotalCount) und
  `loadAmountsForQuery` (TotalAmount). `countDeletedEntries` erzwingt intern
  `IncludeDeleted = true` und bleibt damit unabhängig vom Schalter.
  `Shared.Domain.ledgerBalancesByAccount` (`src/Shared/Domain.fs:1006`) filtert
  `not i.DeletedInYnab` bedingungslos und nimmt gar keinen `IncludeDeleted`-Parameter, der
  Kontostand klammert sie also immer aus.
- Wortlaut und Entscheidungsverweis (Variante 2, Roman 2026-09-18, `ledger-d4kx8`) sind aus
  dem Absatz `**Gelöschte Buchungen**` übernommen, es entsteht keine dritte Formulierung.
  "Seite" in die Aufzählung aufzunehmen ist eine vom Code gedeckte Präzisierung, kein
  Widerspruch. Die Anker-Einschränkung ist im OUTCOME benannt, wie Kriterium 4 es verlangt.
- `TESTS_ADDED: 0` ist richtig, das Worktree ist sauber (HEAD = Batch-Start `c8335b2`, kein
  wip-Commit), `dotnet test` nicht zu laufen ist hier vertretbar.
- Zurückstellung 1 (`getLedgerPayeeSuggestions` / `ledger-f8q2x`): richtig zurückgestellt.

**Zurückstellung 2 (veraltete "künftig"-Verweise / `ledger-t4m9c`) — abweichende Empfehlung
des Verifiers, nicht FAIL-relevant:** Der Satz "Fundament für die künftigen Filter
(`ledger-vgfw3`)" steht innerhalb genau des Textes, den der Worker ohnehin von Hand neu
ausgibt, und er hatte ihn bereits als veraltet erkannt. Ihn wörtlich mitzuschleppen liefert
wissentlich einen falschen Absatz aus. Der Verifier empfiehlt, diese Hälfte beim
Neu-Ausgeben des Bodys mitzukorrigieren und `ledger-t4m9c` auf das Vorkommen in
`## Oberfläche` zu verengen. **Das ist eine Änderung der Akzeptanzkriterien und damit eine
Entscheidung des Builders, nicht des Workers** — in dieser Iteration daher NICHT umzusetzen.

**ITERATION_HINT:** likely-fixable

## Outcome

Der `getLedgerEntriesPage`-Eintrag unter `## Sichten` im ledger-README nennt jetzt beide
Hälften des `IncludeDeleted`-Verhaltens. Der Zusatz (nach "... während sie ausgeblendet
sind." und vor "Fundament für die künftigen Filter ...") lautet: "Ist `IncludeDeleted =
true` gesetzt, zählen gelöschte Buchungen dagegen bewusst in Seite, Trefferzahl und
Summe mit (Variante 2, Entscheidung Roman 2026-09-18, `ledger-d4kx8`); der Kontostand
(`getLedgerAccountBalances` / `Shared.Domain.ledgerBalancesByAccount`) lässt sie davon
unabhängig immer draußen, die Abweichung zwischen Summenzeile und Kontostand ist damit
gewollt." Wortlaut und Entscheidungsverweis sind aus dem bestehenden Absatz
`**Gelöschte Buchungen**` (README-Zeilen ~366-377) übernommen, es entsteht keine dritte
Formulierung. Die bestehende Aussage über den Kontostand bleibt unverändert und
unabgeschwächt: er lässt gelöschte Buchungen immer draußen.

Gegen den Code geprüft (unverändert gegenüber Iteration 1, dort bereits verifiziert):
`toEntryFilter` reicht `IncludeDeleted` an `buildFilterClause`
(`src/Server/Ledger/Persistence.fs:604`) weiter, das daraufhin das Prädikat
`AND deleted_in_ynab = 0` weglässt — gelöschte Buchungen zählen also tatsächlich in
`loadEntriesPage` (Seite), `countEntries` (TotalCount) und `loadAmountsForQuery`
(TotalAmount) mit, sobald `IncludeDeleted = true` gesetzt ist. `countDeletedEntries`
erzwingt intern `IncludeDeleted = true` und bleibt vom Schalter unabhängig.
`Shared.Domain.ledgerBalancesByAccount` (`src/Shared/Domain.fs:1006`) filtert
`not i.DeletedInYnab` bedingungslos und nimmt gar keinen `IncludeDeleted`-Parameter —
der Kontostand klammert gelöschte Buchungen also immer aus, unabhängig vom Schalter.

**Werkzeug-Grenze (wie im Task-Text vorhergesagt).** `applyReadmeDelta`
(`BULLET_START_RE`) adressiert nur Zeilen, die an Spalte 0 mit `- ` beginnen. Der
`getLedgerEntriesPage`-Eintrag selbst ist ein Unterbullet auf Einrückung 2 und damit
nie direkt adressierbar. Weil `findBulletsInRange` aber jede col-0-Bullet-Einheit bis
zur nächsten col-0-Bullet-Zeile laufen lässt, bildet der gesamte Block von der
`**Sichten**`-Kopfzeile bis einschließlich `getLastMirrorRun` (README-Zeilen 96-127)
EINE Bullet-Einheit — der Delta oben ankert auf dem Eltern-Bullet `**Sichten**` und
ersetzt den kompletten Block per `replace` (`expected`/`body` byte-für-byte identisch
mit den aktuellen README-Zeilen 96-127, außer der beabsichtigten Ergänzung).

**Iteration-1-Fehler diesmal vermieden und per Dry-Run belegt.** Iteration 1 hatte
`expected`/`body` ohne die reale README-Einrückung ausgegeben (Spalte 0 statt 2/4),
wodurch `replaceRange` beim Einspleißen die ganze Sichten-Liste flachgeklopft hätte —
Kollateralschaden an fünf unbeteiligten Geschwister-Unterbullets und ein dauerhaft
verlorener Anker für künftige `replace`-Operationen auf diesen Block. Dieses Mal trägt
sowohl `expected` als auch `body` die echte Einrückung (2 Leerzeichen auf den
Fortsetzungszeilen des Eltern-Bullets und auf jedem `- `-Unterbullet, 4 Leerzeichen auf
jeder Fortsetzungszeile eines Unterbullets). Verifiziert per Dry-Run: `applyReadmeDelta`
aus `/Users/romansachse/.claude/plugins/cache/agentheim/agentheim/0.9.4/lib/readme-delta.mjs`
gegen eine Kopie der README ausgeführt
(`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d375c1eb-d424-438a-89f5-a7d3c6f29661/scratchpad/README.copy.md`)
mit exakt diesem Delta — Disposition `["applied"]`. `diff -u` zwischen der Kopie vorher
und nachher zeigt genau einen Hunk (5 neue Zeilen im `getLedgerEntriesPage`-Bullet),
alle fünf Geschwister-Unterbullets (`getLedgerAccounts`, `getLedgerEntriesForAccount`,
`getLedgerAccountBalances`, `getEntryHistory`, `getLastMirrorRun`) sowie deren
Einrückung bleiben Zeichen für Zeichen unverändert. Die echte README wurde dabei nicht
angefasst — nur die Kopie im Scratch-Verzeichnis.

Der Satz "Fundament für die künftigen Filter (`ledger-vgfw3`)" ist bewusst unverändert
übernommen (auch wenn er inzwischen veraltet ist, da `ledger-vgfw3` erledigt ist) — die
Korrektur dieser Formulierung ist Sache von `ledger-t4m9c`, nicht dieser Aufgabe; sie
hier vorwegzunehmen wäre eine Änderung der Akzeptanzkriterien gewesen.

Zwei Folgeaufgaben erneut als Backlog-Items gemeldet (siehe `BACKLOG_ITEMS`):
`ledger-f8q2x` (`getLedgerPayeeSuggestions` fehlt komplett unter `## Sichten`) und
`ledger-t4m9c` (veraltete "künftig"-Verweise auf `ledger-vgfw3` im Abschnitt
`## Oberfläche`).

Kein Code geändert, kein Build/Test-Lauf nötig; das Worktree ist unverändert (reine
Doku-Aufgabe über den Report-Kanal).
