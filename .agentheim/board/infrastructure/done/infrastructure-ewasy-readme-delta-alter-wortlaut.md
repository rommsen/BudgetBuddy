---
id: infrastructure-ewasy
title: README-Delta-Gate meldet alten Wortlaut, der nach einem replace anderswo in der README stehen bleibt
status: done
type: chore
context: infrastructure
created: 2026-11-08
completed:
depends_on: []
blocks: []
tags: [laufend, harness]
related_adrs: [0040]
related_research: []
prior_art: []
---

## Why
Ein README_DELTA ersetzt einen Bullet, aber andere Bullets, `## Ereignisse`, `## Key commands`, `## Key events`,
Open questions oder Verweise nennen dieselbe Regel noch im alten Wortlaut. Der Verifier lehnt ab. Das kam im
work-Lauf 2026-11-08 viermal vor (ledger-qvjvs, budgeting-e2z7v, ledger-at63w zweimal), vorher bei budgeting-eh8xe
und im Protokoll als "README-Bullets Push-Fehlschlag und Schreib-Sperre nennen dieselbe Regel und sind nicht
nachgezogen".

## What
`scripts/readme-delta-check.mjs` bekommt ein siebtes Kriterium „alter Wortlaut bleibt stehen“ und eine Liste „ganz
nachlesen“. Entschieden (Roman, 2026-11-13): Treffer lassen das Gate durchfallen (Exit 1), mit einem bewussten Ausweg
`--keep`.

### Kriterium 7 (Exit 1)
Für jedes `replace`-Op des Deltas:
- **Kandidaten** sind die Begriffe in Backticks und die Sätze in „…“ aus `expected`, die in keinem `body` des ganzen Deltas
  vorkommen (über alle Ops und Abschnitte).
- **Selten** ist ein Backtick-Begriff, der in der README nach dem Anwenden des ganzen Deltas höchstens dreimal steht.
  Häufige Begriffe (etwa `import_id`) sind keine Treffer. Ein „…“-Satz zählt immer, egal wie oft.
- **Treffer:** ein seltener Kandidat oder ein „…“-Satz, der in der README nach dem Anwenden noch irgendwo steht (ganze Datei:
  alle Abschnitte, auch `## Key commands`, `## Key events`, `## Ereignisse`, Open questions, Bullets ohne fetten Kopf).
  Vergleich nach `collapseWs`, also über Zeilenumbrüche hinweg.
- Ausgabe je Treffer: Begriff, das Op (Abschnitt und Anker), Zeilennummer(n) der Fundstelle in der README nach dem Anwenden.
  Exit 1 mit dem Grund „alter Wortlaut bleibt stehen“.
- **`--keep "<begriff>"`** (mehrfach erlaubt) nimmt einen Begriff bewusst aus. Das Gate druckt jeden genutzten `--keep`. Ein
  `--keep`, das keinen Treffer abdeckt, ist eine ungültige Eingabe (Exit 2), damit keine toten Ausnahmen liegen bleiben.

### Liste „ganz nachlesen“ (immer, ohne Einfluss auf den Exit-Code)
Jeder Eintrag unter `## Key commands`, `## Key events` und `## Ereignisse` (Bullet mit kursivem oder Backtick-Kopf), der
einen seltenen Backtick-Begriff mit dem `expected` oder `body` eines `replace` teilt, steht mit Kopf und Zeilennummer in
einer Liste. Grund: ledger-ay81j Iteration 2, *PushErneutSenden* beschrieb das alte Verhalten umformuliert, ohne
gemeinsamen seltenen Begriff mit dem ersetzten Satz, aber mit dem gemeinsamen Befehl `retryPushToYnab`.

### Doku
- Der Hilfetext des Skripts nennt Kriterium 7, `--keep` und die Liste.
- `CLAUDE.md`, Abschnitt README-Deltas: Die Prosa-Regel zum Grep über die ganze README wird durch einen Satz zum neuen
  Kriterium ersetzt. Dazu: Jeder `--keep` steht mit Grund im OUTCOME, jeder Eintrag der Liste „ganz nachlesen“ steht im
  OUTCOME mit „passt“ oder „nachgezogen“.

## Acceptance criteria
- [ ] Kriterium 7: ein Delta, das einen Bullet mit einem seltenen Backtick-Begriff ersetzt, der woanders in der README stehen
      bleibt, ergibt Exit 1 mit Begriff und Zeilennummer. Gegenfall: derselbe Begriff steht im `body` eines Ops, Exit 0.
- [ ] Ein „…“-Satz aus `expected`, der woanders stehen bleibt, ergibt Exit 1, auch wenn er über einen Zeilenumbruch läuft.
- [ ] Häufige Begriffe (mehr als dreimal nach dem Anwenden) ergeben keinen Treffer.
- [ ] `--keep "<begriff>"` nimmt genau diesen Treffer aus und wird ausgegeben. Ein `--keep` ohne Treffer ergibt Exit 2.
- [ ] Die Liste „ganz nachlesen“ nennt Key commands, Key events und Ereignisse mit gemeinsamem seltenen Backtick-Begriff,
      mit Kopf und Zeilennummer, und ändert den Exit-Code nicht.
- [ ] Gegenbeweise in `scripts/readme-delta-check.test.mjs` mit erfundenen README-Fixtures, je Kriterium ein Treffer und ein
      Gegenfall. Ein Fixture bildet den Fall ledger-ay81j Iteration 1 nach (Begriff bleibt in einem anderen Bullet), eines
      Iteration 2 (Key command mit gemeinsamem Befehl landet in der Liste).
- [ ] Die echte ledger-README auf `main` mit einem leeren Delta (`[]`) ergibt Exit 0, ohne Ausgabe in der Liste.
- [ ] Hilfetext und `CLAUDE.md` (Abschnitt README-Deltas) beschreiben Kriterium 7, `--keep` und die Liste. Die Kopplung von
      Skriptpfad und Text in CLAUDE.md bleibt grün.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Notes
Captured via `quick-capture` on 2026-11-08 aus dem `learn`-Lauf nach dem Review-Lauf e83f2d00..ad18f0c9, raw,
unrefined. Bis das Kriterium gebaut ist, gilt die geschärfte Prosa-Regel in CLAUDE.md, Abschnitt README-Deltas.

Nachtrag 2026-11-13 (learn nach ledger-ay81j): Zwei Verifier-FAILs in Folge, obwohl der Worker beide Male
Vollständigkeit per grep meldete. Iteration 1: Bullet **Schreib-Sperre** nannte weiter „feste `import_id`“
(reiner Begriffstreffer, das Kriterium aus What hätte ihn gefunden). Iteration 2: Key command *PushErneutSenden*
sagte „trägt die Buchung eine `import_id`, geht sie mit“, der ersetzte Bullet hatte „mit der `import_id`, falls
die Buchung eine trägt“: umformuliert, kein gemeinsamer seltener Begriff. Folgerungen fürs Verfeinern:
- Begriffe, die auch im `body` stehen oder in der README häufig sind (etwa `import_id`), taugen nicht als
  Treffer. Nur Begriffe, die nach dem Delta seltener werden, sonst Fehlalarme.
- Ergänzend eine Liste statt Exit 1: jeder Key command, Key event und `## Ereignisse`-Eintrag, dessen Name
  (kursiv oder in Backticks) im `expected` oder `body` eines replace vorkommt, wird mit Zeilennummer als
  „ganz nachlesen“ ausgegeben. Der Worker nennt im OUTCOME je Eintrag „passt“ oder „nachgezogen“.
- Damit wird die Task vom Kann- zum Alltagsfall: drei Verifier-Iterationen kosten mehr als der Wächter.

Verfeinert 2026-11-13: Kriterium mit Exit 1 und `--keep` (Roman). Schwelle „selten“ = höchstens dreimal nach dem
Anwenden; der Worker darf sie begründet anpassen, wenn die echten READMEs (ledger, budgeting) sonst Fehlalarme liefern, und
nennt das im OUTCOME. Ort im Skript: neben `evaluateOp`, Auswertung nach dem Anwenden aller Ops; `collapseWs` und
`findBulletBlocks` gibt es schon. Durchsetzung: das Gate selbst (Agentheim ADR 0059, mechanize-or-drop), die Prosa in CLAUDE.md
beschreibt nur noch den Aufruf.

## Verifier note (iteration 1)
**REASONS:**
- Kriterium „Häufige Begriffe (mehr als dreimal nach dem Anwenden) ergeben keinen Treffer“ ist nicht falsifizierbar getestet: P7c legt `oft_term` in den `body` beider Ops, damit fällt der Begriff schon über die body-Regel heraus, bevor die Seltenheitsschwelle greift.
- Mutation `return n >= 1 && n <= RARE_MAX;` zu `return n >= 1;` in `rareTerms`: alle 32 Tests bleiben grün. Die Seite „häufig“ hat keinen echten Test.
- Kein Code-Fehler, nur eine Testlücke; `./verify.sh` lief in dieser Iteration nicht.

**SUGGESTED_FIX:** P7c so ändern, dass der häufige Begriff in keinem `body` steht: ein replace, dessen `expected` einen Begriff nennt, der nach dem Delta mehr als dreimal in Backticks in der README steht, mit einem body ohne den Begriff. Exit 0 und kein Treffer prüfen, dann bestätigen, dass der Test ohne `n <= RARE_MAX` rot wird. Ein passender Fall für die Liste „ganz nachlesen“ (ein häufiger gemeinsamer Begriff setzt keinen Eintrag) sichert dieselbe Schwelle dort.

**ITERATION_HINT:** likely-fixable

## Outcome

`scripts/readme-delta-check.mjs` hat Prüfung 7 (alter Wortlaut bleibt stehen) und die Liste „ganz nachlesen“. Beide laufen nach dem Anwenden des ganzen Deltas, je Zieldatei, und nur wenn die Prüfungen 1 bis 6 grün sind.

- Prüfung 7 (`findStaleWording`): Kandidaten sind Backtick-Begriffe und „…“-Sätze aus dem `expected` jedes `replace`, die in keinem `body` des Deltas stehen. Ein Begriff zählt, wenn er danach 1 bis 3 Mal in Backticks in der Datei steht (`RARE_MAX = 3`), ein Satz immer. Der Vergleich läuft über die kollabierte Fassung (`collapsedWithLines`), Zeilennummern zeigen auf die Startzeile, auch über Zeilenumbrüche. Ausgabe auf stderr je Treffer: Begriff, Anker, Abschnitt, Zeile(n). Exit 1.
- `--keep "<begriff>"` (mehrfach, umschließende Backticks oder „…“ werden toleriert) nimmt Treffer aus. Jeder genutzte `--keep` steht auf stdout, ein `--keep` ohne Treffer ist Exit 2.
- Liste „ganz nachlesen“ (`findRereadEntries`): Einträge mit kursivem oder Backtick-Kopf unter `## Key commands`, `## Key events`, `## Ereignisse`, die einen seltenen Backtick-Begriff mit `expected` oder `body` eines `replace` teilen, mit Datei:Zeile, Kopf und gemeinsamen Begriffen. Leer: „Ganz nachlesen: keine Einträge“. Kein Einfluss auf den Exit-Code.
- Schwelle bei 3 belassen. Gegenprobe an der echten ledger-README: identischer `replace` des Bullets **Schreib-Sperre** mit einem geänderten Begriff ergibt Exit 0 und sieben Listeneinträge unter Key commands, plausibel für einen großen Bullet, ohne Fehlalarm in Prüfung 7. Leeres Delta `[]` auf ledger: Exit 0, Liste leer (Test P9).
- Hilfetext nennt alle Prüfungen, Prüfung 7, `--keep` und die Liste. `CLAUDE.md`, Abschnitt README-Deltas: die grep-Prosa-Regel ist durch einen Satz zu Prüfung 7, `--keep` (mit Grund im OUTCOME) und der Liste („passt“ oder „nachgezogen“ im OUTCOME) ersetzt; T8 (Kopplung) bleibt grün, P10 koppelt Hilfetext und CLAUDE.md an die neuen Begriffe.
- Tests in `scripts/readme-delta-check.test.mjs`: P7a bis P7d und P8 je mit Gegenfall, P7a bildet ledger-ay81j Iteration 1 nach (Begriff bleibt in anderem Bullet), P8 Iteration 2 (Key command mit gemeinsamem Befehl). Dazu P9 (echte ledger-README), P10 (Doku-Kopplung) und ein Einheitentest für `collapsedWithLines`. Alle Fixtures erfunden.
- Iteration 2 (Verifier-Note): P7c neu, der häufige Begriff `oft_term` steht in keinem `body` (Fixture `FREQUENT_README`, nach dem Anwenden viermal in Backticks), Exit 0 ohne Treffer. Neu P8c: derselbe häufige gemeinsame Begriff setzt keinen Eintrag in die Liste „ganz nachlesen“, obwohl *NotizLesen* ihn trägt. Mutant `return n >= 1;` in `rareTerms`: P7c und P8c rot (31 grün, 2 rot), Mutant zurückgenommen, danach 33 grün.
- Eigenes README_DELTA dieser Task durch das Gate geprüft: Exit 0, „Ganz nachlesen: keine Einträge“, kein `--keep`.
- `./verify.sh`: `VERIFY: PASS` (dotnet 4986 grün, node 336 grün).