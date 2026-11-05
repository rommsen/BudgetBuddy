---
id: budgeting-zkbbd
title: "BB führt das Budget, YNAB wird Spiegel: Zuweisen, Verschieben und Targets entstehen als Ereignisse in BB, nach YNAB geht nur Zugewiesen je Monat und Kategorie"
status: done
type: decision
context: budgeting
created: 2026-11-05
completed:
depends_on: [infrastructure-c7f53]
blocks: []
tags: [event-sourcing, targets, ynab-spiegel, fuehrung]
related_adrs: [0012, 0065, 0072, 0074, 0079, 0080, 0082, 0083, 0084]
related_research: []
prior_art: [budgeting-h2zfn, budgeting-afd8h, budgeting-c5w2n, budgeting-k8wq3, budgeting-t6p8z]
---

## Why
Ziel ist, von YNAB wegzukommen. Im Budget ist die Wahrheit heute YNAB: BB liest die Beträge live
(ADR 0065) und hält nur fest, was es selbst nach YNAB geschrieben hat. Weist Roman in YNAB zu,
sieht BB das nicht. Die Budget-Historie ist dadurch lückenhaft, und Targets können nur, was
YNAB-Goals können.

Beim Budget lässt sich die Richtung umdrehen, ohne auf ein vollständiges Kassenbuch zu warten
(ING-Import fehlt noch): BB führt die Entscheidungen, YNAB rechnet vorerst weiter aus den
Buchungen. Gespräch mit Roman am 2026-11-05, Refinement mit tactical-modeler und architect am
selben Tag.

## What
Ein ADR mit Scope `budgeting`, das den Wechsel der Führung festlegt. Alle Punkte unten sind
entschieden (Roman, 2026-11-05) oder haben eine technische Antwort aus dem Refinement. Der Worker
schreibt sie aus und begründet sie, er entscheidet sie nicht neu. Grundlage: ADR 0083 (Tabelle
`events`, Umschlag, `processed_commands`, Read-Model-Kriterium, Sprachregel).

### Grundsatz
- BB führt im Budget die Entscheidungen: Zuweisen, Verschieben, Targets, Pausen. Sie entstehen als
  Ereignisse in `events`.
- YNAB wird Spiegel: Nach YNAB geht nur der Zielzustand Zugewiesen je (Monat, Kategorie), absolut
  gesetzt, erst festhalten, dann senden (ADR 0065).
- YNAB rechnet vorerst weiter Aktivität, Verfügbar und Zu verteilen aus den Buchungen; BB liest das
  live wie heute. BB übernimmt diese Rechnung erst mit vollständigem Kassenbuch (eigene spätere
  Entscheidung).
- Nur Roman weist im Budget zu. Weist er doch in YNAB zu, übernimmt BB das als Beobachtung und
  überschreibt es nicht.

### 1. Stream-Schnitt
- `budgeting:month:<YYYY-MM>` für Zuweisen, Verschieben, Targets anwenden, Pausen, Beobachtungen,
  Rückschreib-Ergebnisse und Übernahme. Passt zur Sperre je Monat (`MonthGuard`,
  `src/Server/Budgeting/CommandHandlers.fs:36-47`). Eine Verschiebung ist ein Ereignis in einem
  Stream, Targets anwenden ebenso.
- `budgeting:target:<categoryId>` für die Target-Regel. Passt zu `TargetGuard` (`:52-63`).
- Es gibt keine Invariante über mehrere Kategorien (BB prüft weder Verfügbar noch Zu verteilen
  auf >= 0). Hingenommene Lücke: Targets anwenden liest die Regeln unter `MonthGuard`, ein
  `TargetSet` läuft unter `TargetGuard`. Bei einem Nutzer harmlos, die nächste Anwendung korrigiert.
- Monats-Id im Format `YYYY-MM` (im ADR festlegen und überall gleich verwenden).
- Verworfen: Stream je Kategorie (Verschieben in 2 Streams, Anwenden in rund 100), Stream je
  (Monat, Kategorie) (kein Aggregat schützt eine Invariante, keine Sperre passt).

### 2. Vokabular (englisch nach ADR 0083)
| Heute (README, Tabelle) | Typ | Stream | Actor |
|---|---|---|---|
| GeldZugewiesen, Fehlbetrag-Knopf, Holen/Abgeben | `AssignmentChangedInBb` (categoryId, previousAssigned, assigned) | month | UserInBb |
| GeldVerschoben (heute zwei Zeilen) | `MoneyMoved` (from/to mit vorher/nachher, amount) | month | UserInBb |
| TargetsAngewendet | `TargetsApplied` (lines, skippedPaused) | month | UserInBb |
| TargetPausiert | `TargetPaused` | month | UserInBb |
| PauseAufgehoben ("Fortgesetzt") | `TargetPauseLifted` | month | UserInBb |
| TargetGeändert (Geändert, Angelegt, Wechsel) | `TargetSet` (vollständige Regel, `effectiveFrom`) | target | UserInBb |
| TargetGeändert (Entfernt) | `TargetRemoved` | target | UserInBb |
| ZuweisungNachYnabGeschrieben | `AssignmentWrittenBackToYnab` (decisionEventId, categoryId) | month | YnabMirror |
| ZuweisungRückschreibenFehlgeschlagen | `AssignmentWriteBackFailed` (reason) | month | YnabMirror |
| Ergebnis "changed-in-ynab" | `AssignmentWriteBackSupersededInYnab` | month | YnabMirror |
| Ziel ungesendet geschlossen | `AssignmentWriteBackWithdrawn` | month | UserInBb |
| neu: Zuweisung in YNAB | `AssignmentChangeObservedInYnab` (previousAssigned = BB-Soll, assigned = YNAB) | month | YnabMirror |
| neu: Übernahme | `BudgetTakenOverFromYnab` (je Monat, categories), `TargetTakenOverFromYnab` (Regel plus rohes `ynabGoalType`) | month / target | YnabMirror |

- Eine Verschiebung in YNAB erscheint bewusst als zwei `AssignmentChangeObservedInYnab`. Einen Typ
  `MoneyMovedInYnab` gibt es nicht.
- Created/Changed/KindChanged entfallen: "Angelegt" und "50 → 60" leitet die Historie aus der
  vorigen Faltung ab (heute `TargetChangeKind`, `src/Shared/Budgeting.fs:587`).
- Gruppen-Id ist die `event_id` des Entscheidungsereignisses; für Altbestände bleibt `applicationId`
  im Payload.
- Kein neuer Actor-Wert für die Übernahme, der Typname benennt sie.

### 3. YNAB-Änderungen erkennen, Echo
- BB vergleicht beim Lesen eines Monats und vor jedem Befehl. Das Lesen ruft danach einen eigenen
  Befehl `observeMonth` auf; `QueryHandlers.budgetMonthOf` bleibt rein. Scheitert die Beobachtung,
  scheitert das Lesen nicht (nur Log). Vorbild: der Seitenaufruf stößt den Kassenbuch-Spiegel an
  (`src/Server/Ledger/CommandHandlers.fs:1028-1034`).
- **Harte Regel:** Verglichen wird nur ein YNAB-Stand, der unter `MonthGuard` gelesen wurde. Jeder
  Befehl beobachtet zuerst und entscheidet dann.
- Echo-Regel, verglichen gegen das gefaltete BB-Soll je (Monat, Kategorie), Vorbild
  `src/Server/Ledger/Domain.fs:900-999`:

  | YNAB zeigt | offene Rückschreibung | Ereignis |
  |---|---|---|
  | das Soll | keine | keins |
  | das Soll | ja | `AssignmentWrittenBackToYnab` |
  | die Baseline der Entscheidung | ja | keins, bleibt offen |
  | anderen Wert | egal | Beobachtung, Soll wird YNAB-Wert, offene Rückschreibung geschlossen |

  "Ausstehend" (Absturz vor dem Senden) gilt wie "fehlgeschlagen"; dafür trägt das
  Entscheidungsereignis die Baseline.
- Rasterprüfung (10 Milliunits) gilt für alle Wege, auch Übernahme und Migration, sonst entsteht bei
  jedem Lesen eine Beobachtung.
- Verworfen: Knopf "Abgleichen" (wird vergessen). Später möglich: Abgleich im Hintergrund über das
  Delta von `/budgets/{id}`, erst nach einem Spike.

### 4. Migration
- Ein Startschritt in einer Transaktion nach ledger-k9tm2: alle fünf Tabellen (`budget_movements`,
  `budget_movement_results`, `target_pause_events`, `target_changes`, `target_change_results`)
  nach `events`, Schreibwege und Leser umstellen, Tabellen in derselben Transaktion entfernen. Kein
  Lesestand, sonst zwei Historienquellen. YNAB führt in diesem Schritt noch, Verhalten unverändert.
- Ergebniszeilen werden je ein Ergebnis-Ereignis. `target_changes` ohne `step` gilt als Schritt 1
  (`src/Server/Budgeting/Persistence.fs:569-570`). Alte `target_changes` tragen nur Teilwerte und
  werden ein Altbestand-Typ (etwa `YnabGoalChangedFromBb` mit Schritt-Ergebnissen).
- Reihenfolge im Stream: `occurred_at`, bei Gleichstand Entscheidung vor Ergebnis, dann `rowid`. Die
  `position` migrierter Budget-Ereignisse liegt hinter dem Kassenbuch (ADR 0083, Reihenfolge des
  Festhaltens). Unbekannte Herkunft bricht den Start ab.
- Der Zustand (Soll, Regeln, Pausen) beginnt beim Übernahme-Ereignis; migrierte Ereignisse davor
  sind Historie und müssen nicht in die Faltung.
- Vorher Probe gegen eine Kopie der Produktion. Abnahme: Vorher-nachher-Vergleich von "Zuletzt
  verwendet", den Historien, den Pausen und der letzten Target-Änderung.

### 5. Übernahme (entschieden: Roman, 2026-11-05)
- Laufender und künftige Monate werden beim Umschalten sofort übernommen, ältere nach und nach im
  Hintergrund unter der YNAB-Rate-Grenze (rund 200 Anfragen pro Stunde, rund 97 Monate). Liest BB
  vorher einen noch nicht übernommenen Monat, übernimmt es ihn dabei.
- Ein `BudgetTakenOverFromYnab` je Monat unter `MonthGuard`. Kategorien `internal` werden nicht
  übernommen. Idempotenz: unter der Sperre prüfen, ob schon ein Übernahme-Ereignis vorliegt (Version
  1 ist kein Merkmal, migrierte Altereignisse können davor liegen).
- Ein spät geschriebenes Übernahme-Ereignis ist korrekt: Es hält fest, wann BB den Stand erfahren
  hat; `occurred_at` wird nicht zurückdatiert. Eine YNAB-Änderung an einem alten Monat vor dessen
  Übernahme geht im Schnappschuss auf. Hingenommen.
- Targets werden einmal aus `/categories` übernommen (`goal_*`, `src/Server/YnabClient.fs:90-103`),
  das gehört zu T5.
- Verworfen: alles auf einmal vor dem Umschalten (Rate-Grenze), nur beim ersten Lesen (ältere
  Monate blieben unbestimmt lange offen).

### 6. Targets und YNAB-Goals (entschieden: Roman, 2026-11-05)
- **Target-Arten 1:1:** Beim Wechsel genau die heutigen YNAB-Arten, beschränkt auf die in Romans
  Budget genutzten. DEBT wird nicht übernommen und gemeldet. Erweiterungen (etwa Auffüllen über den
  Monatswechsel, budgeting-bk6tq) kommen danach als eigene Tasks mit neuen Ereignistypen.
- **YNAB-Goals einfrieren:** Beim Umschalten bleiben die Goals in YNAB auf ihrem Stand und werden nur
  noch gelesen (Übernahme, Parallelrechnung). BB schreibt im Alltag keine Goals mehr.
- **Rückweg:** Eine bewusst auszulösende Aktion "Targets nach YNAB zurückschreiben" schreibt BBs
  Targets als Goals nach YNAB, soweit darstellbar, und listet, was YNAB nicht darstellen kann. Für
  den Fall, dass Roman zu YNAB zurückkehrt. Zuweisungen brauchen keinen Rückweg, YNAB hat sie
  laufend.
- Verworfen: laufendes Spiegeln (hielte den Zwei-Schritt-Schreibweg aus ADR 0072/0080 im Alltag am
  Leben), Goals entfernen (kein Rückweg).

### 7. Fehlbetrag und bisherige Target-Arbeit
- Fehlbetrag als reine Funktion `underfunded rule month assigned carryover activity`, ersetzt
  `goal_under_funded` (`src/Shared/Budgeting.fs:93`, verwendet in `src/Server/Budgeting/Domain.fs:78-114`).
  Zugewiesen aus der BB-Faltung, Übertrag live (Verfügbar − Zugewiesen − Aktivität).

  | Art | Fehlbetrag |
  |---|---|
  | NEED monatlich, MF | Betrag − Zugewiesen |
  | NEED wöchentlich | Wochentage im Monat × Betrag − Zugewiesen |
  | NEED, TBD mit Fälligkeit | (Betrag − Übertrag) / verbleibende Monate − Zugewiesen |
  | Auffüllen bis | Betrag − (Übertrag + Zugewiesen) |
  | TB | Betrag − Verfügbar |
  | DEBT | nicht übernommen |

  Die Formeln sind Annahmen über YNABs Logik. **Bedingung für das Umschalten von "Targets
  anwenden":** Parallelrechnung gegen `goal_under_funded` für alle Targets mit Abweichungsliste
  (T4). Die Toleranz legt Roman anhand dieser Liste fest.
- Entfällt mit T5: `planTargetWrite` (`Domain.fs:206`), `sendSteps` (`CommandHandlers.fs:444-483`),
  Port `SetTarget`, die Zwei-Schritt-Logik aus ADR 0072 Punkt 5 und ADR 0080 Punkte 4 und 6,
  `TargetPartlyWritten`, `TargetChangedInYnab`, `PausedInYnab`/`PausedInBoth` (ADR 0079 Punkt 2).
  `setCategoryTarget` (`YnabClient.fs:553-577`) bleibt nur, falls die Rückweg-Aktion (T6) ihn nutzt.
- Bleibt: `changeCategoryTarget` (`src/Shared/Api.fs:551`, hängt nur noch ein Ereignis an),
  `TargetGuard`, der Vergleich gegen `Shown` (jetzt gegen die BB-Faltung), ADR 0080 Punkt 1
  (`effectiveFrom` ist der laufende Monat). Neu: vergangene Monate zeigen die damals gültige Regel.
- Der Wochentag wöchentlicher Targets wird in BB setzbar (heute nur lesbar, README).
- **Reihenfolge der offenen Tasks (entschieden: Roman, 2026-11-05):** budgeting-q4v8n (Seite Targets
  prüfen) und budgeting-m3x7f (Monatserster) werden **nach** dem Wechsel gebaut, auf den neuen
  Schreibweg. budgeting-bk6tq (Spike Monatswechsel) ist unabhängig; seine Folge-Tasks kommen nach T4
  und T5.

### 8. Idempotenz
- `CommandId: Guid option` in `MoveRequest`, `AssignRequest`, `TargetPauseRequest`,
  `TargetChangeRequest` (`src/Shared/Budgeting.fs:343-428`); `applyTargets` bekommt einen
  Request-Record oder ein Feld in `TargetPreview`. Optional, damit ein alter Client `None` schickt
  und der Server sich wie heute verhält. Per Test belegen, dass Fable.Remoting ein fehlendes
  Optionsfeld als `None` liest.
- Für alle UI-Entscheidungen, nicht nur `moveMoney`: Eine verspätete Wiederholung eines absoluten
  Zuweisens setzte sonst einen späteren Wert zurück. `retryFailed` braucht keine.
- Prüfung zuerst unter `MonthGuard` gegen `processed_commands` (spart den YNAB-Aufruf), Netz ist die
  Anhänge-Transaktion. Die Antwort auf eine Wiederholung baut der Server über `command_id` →
  Entscheidung → `reportWith` (`CommandHandlers.fs:115`) aus dem Stream.
- Client: Id je Absicht, nach Status 0 einmal automatisch wiederholen (`Retry.writing` neben
  `src/Client/Retry.fs:45`).
- Ergebnis-Ereignisse entstehen in einer späteren Transaktion und tragen keine `command_id`.

## Acceptance criteria
- [ ] Ein ADR mit Scope `budgeting` hält Grundsatz und die Punkte 1 bis 8 fest, mit den
      verworfenen Varianten und ihren Gründen.
- [ ] Das ADR enthält die Abbildungstabelle der deutschen Namen auf die englischen Typen und die
      Echo-Tabelle.
- [ ] ADR 0065 (Punkte 1, 4, 6), 0079 (Punkt 2), 0080 (Punkte 3 bis 6) und 0082 (Punkt 7) bekommen
      je einen Nachtrag mit Verweis auf das neue ADR (Text im OUTCOME unter "Für den Conductor",
      der Conductor schreibt ihn).
- [ ] Die `budgeting`-README nennt die neue Führung und stellt die Ereignisnamen auf die englischen
      Typen um (README_DELTA, vorher lokal `npm run check:readme-delta -- --bc budgeting --delta -`).
- [ ] budgeting-q4v8n und budgeting-m3x7f bekommen die Notiz "nach dem Wechsel, auf den neuen
      Schreibweg" mit der Abhängigkeit auf T5; budgeting-bk6tq die Notiz "unabhängig, Folge-Tasks
      nach T4/T5" (Texte im OUTCOME unter "Für den Conductor").
- [ ] Folge-Tasks liegen im Backlog (BACKLOG_ITEMS) mit diesen Abhängigkeiten:
      T4 Fehlbetrag als reine Funktion mit Parallelrechnung und Abweichungsliste (hängt an nichts);
      T1 Budget-Ereignistypen, Codec, Startmigration der fünf Tabellen (ledger-k9tm2);
      T2 Befehls-Id im Budget (T1);
      T3 Übernahme sofort plus Hintergrund, Soll-Faltung, `observeMonth`, Echo-Regel (T1);
      T5 Targets als BB-Regeln, Übernahme 1:1, Rückbau des Goal-Schreibwegs und der YNAB-Pause,
      Wochentag setzbar, Shared und Fixtures (T1, T4);
      T6 Rückweg-Aktion "Targets nach YNAB zurückschreiben" (T5).
- [ ] An infrastructure-e4vq7 ist vermerkt, dass Ergebnis-Ereignisse ohne `command_id` entstehen
      (Text im OUTCOME unter "Für den Conductor").

## Notes
- Zuweisen und Verschieben gibt es in BB schon (budgeting-c5w2n, budgeting-afd8h). Der Wechsel
  betrifft vor allem, wer Quelle der Wahrheit ist und wie YNAB-Änderungen behandelt werden.
- Umschlag, Stream-Namen, Befehls-Id und Sprachregel stehen in ADR 0083 (infrastructure-c7f53).
  Die Umsetzungs-Tasks T1, T2, T3, T5, T6 hängen an ledger-k9tm2. Kein Budget-Ereignis wird in
  `events` geschrieben, bevor ledger-k9tm2 erledigt ist. T4 braucht `events` nicht und darf sofort
  laufen (entschieden: Roman, 2026-11-05).
- Voraussetzungen geprüft am 2026-11-05: Die Produktions-Datenbank ist im täglichen
  Off-site-Backup, das ist kein Blocker. Sinnvoll vor dem Wechsel, aber kein Blocker:
  infrastructure-g4wy8 (Verbindungsabbrüche) und infrastructure-aaryk (alter Client gegen neuen
  Server). Neu einzuordnen nach diesem ADR: budgeting-w3p7n, budgeting-q8m2v, budgeting-h9c3w.
- Risiken: falsche Beobachtungen bei einem YNAB-Stand außerhalb der Sperre (Test aller
  Reihenfolgen); Rasterabweichungen erzeugen Beobachtungen in Dauerschleife; eine abweichende
  Fehlbetrag-Rechnung weist andere Beträge zu als heute; die Startmigration kann den Serverstart
  verhindern; ein vergessener `context`-Filter beim Laden aus `events`; Fixtures in
  `scripts/*.test.mjs` brechen bei Shared-Änderungen erst zur Laufzeit.
- Offen für später, nicht Teil dieses ADRs: Wartezeit beim Lesen eines Monats hinter einem
  laufenden Befehl desselben Monats (bis zu drei YNAB-Anfragen). Fällt sie im Alltag auf, Vergleich
  über die zuletzt gesehene Position prüfen.

## Outcome

ADR 0084 (provisorisch `0084-bb-fuehrt-das-budget-ynab-wird-spiegel.md`, `scope: budgeting`) hält
den Wechsel der Führung im Budget fest: Grundsatz und die Punkte 1 bis 8 mit den verworfenen
Varianten, die Abbildungstabelle der deutschen Namen auf die englischen Typen und die
Echo-Tabelle. Die budgeting-README nennt die neue Führung (Actors, YNAB) und stellt die Key events
auf die englischen Typen um (README_DELTA, lokal mit `npm run check:readme-delta` geprüft, Exit 0).
Sechs Folge-Tasks liegen im Backlog:

| Kurz | Id | hängt an |
|---|---|---|
| T4 | budgeting-v7kfw | nichts |
| T1 | budgeting-hyn77 | ledger-k9tm2 |
| T2 | budgeting-qm5x9 | budgeting-hyn77 |
| T3 | budgeting-dgrp7 | budgeting-hyn77 |
| T5 | budgeting-eh8xe | budgeting-hyn77, budgeting-v7kfw |
| T6 | budgeting-e2z7v | budgeting-eh8xe |

Kein Quellcode geändert. Die im Task genannten Codestellen sind geprüft und stimmen. Eine
Korrektur: Der Endpoint heißt `retryFailedTargets`, nicht `retryFailed` (`src/Shared/Api.fs:524`).

### Hinweise zu den Entscheidungen

- **Monats-Id:** Das ADR legt `YYYY-MM` für Stream-Namen und Payloads fest. Die
  Remoting-Schnittstelle tauscht den Monat weiter als "YYYY-MM-01" aus. "Überall gleich" ist
  damit als "im Ereignismodell überall gleich" ausgelegt. Soll auch Remoting umstellen, braucht
  das einen eigenen Task.
- **Prüfung der Befehls-Id unter `MonthGuard`:** Target ändern läuft unter `TargetGuard`. Das ADR
  schreibt "unter der Sperre (`MonthGuard`)"; T2 vermerkt, dass Target ändern unter `TargetGuard`
  prüft. Ebenso gilt "jeder Befehl beobachtet zuerst" für Befehle auf einem Monat.
- **Neue Target-Änderungen zwischen T1 und T5:** Der Zwei-Schritt-Weg bleibt bis T5. T1 schreibt
  bis dahin den Altbestand-Typ (etwa `YnabGoalChangedFromBb`) auch für neue Änderungen. Das ist
  aus dem Task abgeleitet, nicht ausdrücklich entschieden.
- **Fehlbetrag negativ ist 0:** im ADR und in T4 ergänzt, wie heute bei `goal_under_funded`.
- **T5 und T3:** BBs Fehlbetrag braucht Zugewiesen aus der Soll-Faltung von T3. Die vorgegebenen
  Abhängigkeiten (T5 an T1 und T4) sind übernommen; T5 vermerkt in Notes, dass T3 in
  `depends_on` gehören könnte.
- **Altbestand "Ziel ungesendet geschlossen":** heute als `changed-in-ynab` gespeichert
  (`CommandHandlers.fs:559`). Ob die Migration es von einem echten "in YNAB geändert"
  unterscheiden kann, prüft T1.
- **ADR 0082 Punkt 7:** Der Nachtrag unten sagt, dass `TargetTakenOverFromYnab` nicht als
  Änderung zählt. Das ist abgeleitet (sonst begänne die Zählung für alle Kategorien am Tag der
  Übernahme neu) und sollte Roman bestätigen.
- **README, offene Frage "Wer führt die Targets":** Der Bullet hat keinen fetten Kopf und ist per
  `replace` nicht erreichbar. Vorschlag für die Konsolidierung: als gelöst markieren mit Verweis
  auf ADR 0084.

### Für den Conductor

**(a) Nachträge zu ADRs** (je am Dateiende anhängen):

Datei `.agentheim/knowledge/decisions/0065-budget-zustand-live-aus-ynab-budget-bewegungen-in-bb.md`:

```markdown
## Nachtrag 2026-11-05 (ADR 0084)

ADR 0084 macht BB im Budget führend. Bis zur Umsetzung der Folge-Tasks gilt dieses ADR weiter.

- **Punkt 1:** Zugewiesen kommt nach budgeting-dgrp7 aus BBs Faltung (Soll je Monat und
  Kategorie). Aktivität, Verfügbar und Zu verteilen liest BB weiter live aus YNAB. Den
  Fehlbetrag rechnet BB nach budgeting-eh8xe selbst.
- **Punkt 4:** Der Betrag je Kategorie ist nach budgeting-eh8xe altes Zugewiesen plus BBs
  Fehlbetrag (reine Funktion aus budgeting-v7kfw), nicht mehr `goal_under_funded`. Pausen kommen
  nur noch aus BB. Das Raster von 10 Milliunits gilt auf allen Wegen, auch bei Übernahme und
  Migration.
- **Punkt 6:** Übernehmen vergleicht weiter unter der Sperre je Monat gegen die gezeigte
  Vorschau. Vorher beobachtet der Befehl YNAB (`observeMonth`, Echo-Regel aus ADR 0084 Punkt 3),
  und die Vorschau rechnet mit dem Soll aus BB.
- Die Tabellen aus Punkt 2 gehen mit budgeting-hyn77 in `events` auf und werden entfernt.
```

Datei `.agentheim/knowledge/decisions/0079-pause-als-ereignisfolge-in-bb.md`:

```markdown
## Nachtrag 2026-11-05 (ADR 0084)

- **Punkt 2:** Mit budgeting-eh8xe entfallen `PausedInYnab` und `PausedInBoth`. Eine Pause gibt
  es nur noch in BB (`TargetPaused`, `TargetPauseLifted` im Stream `budgeting:month:<YYYY-MM>`);
  eine Pause in YNAB wirkt in BB nicht mehr.
- Die Tabelle aus Punkt 1 geht mit budgeting-hyn77 in `events` auf und wird entfernt.
```

Datei `.agentheim/knowledge/decisions/0080-target-aenderungen-nur-im-laufenden-monat.md`:

```markdown
## Nachtrag 2026-11-05 (ADR 0084)

Nach ADR 0084 führt BB die Targets, die Goals in YNAB sind eingefroren. Mit budgeting-eh8xe gilt:

- **Punkt 3:** Geplant wird weiter gegen den gezeigten Stand, verglichen wird gegen BBs Faltung
  des Streams `budgeting:target:<categoryId>` statt gegen YNAB. `planTargetWrite` entfällt, es
  gibt keine Patches mehr.
- **Punkt 4:** Eine Änderung ist ein Ereignis (`TargetSet` mit vollständiger Regel oder
  `TargetRemoved`). Nach YNAB geht nichts, die Zwei-Schritt-Logik entfällt.
- **Punkt 5:** `TargetGuard` bleibt und reiht die Ereignisse im Target-Stream einer Kategorie.
- **Punkt 6:** Ohne Senden gibt es keinen Teilfehler; die Regeln zu Schritt 1 und 2 entfallen.
  Erneutes Speichern plant weiter gegen den gezeigten Stand.
- Punkt 1 gilt weiter (`effectiveFrom` ist der laufende Monat). Die Tabellen aus Punkt 2 gehen
  mit budgeting-hyn77 in `events` auf, alte Zeilen als Altbestand-Typ.
```

Datei `.agentheim/knowledge/decisions/0082-targets-pruefen-als-eigene-seite-mit-pruefregel.md`:

```markdown
## Nachtrag 2026-11-05 (ADR 0084)

- **Punkt 7:** Die letzte Target-Änderung kommt nach budgeting-hyn77 aus `events` (Altbestand-Typ
  mit geschriebenem Schritt 1), nach budgeting-eh8xe aus dem jüngsten `TargetSet` oder
  `TargetRemoved` im Stream `budgeting:target:<categoryId>`. Eine Änderung direkt in YNAB gibt
  es nach dem Einfrieren der Goals nicht mehr. `TargetTakenOverFromYnab` zählt nicht als
  Änderung: Die Übernahme hält einen bestehenden Stand fest, sonst begänne die Zählung für alle
  Kategorien am Tag der Übernahme neu.
```

**(b) budgeting-q4v8n und budgeting-m3x7f:**

Dateien `.agentheim/board/budgeting/backlog/budgeting-q4v8n-seite-targets-pruefen.md` und
`.agentheim/board/budgeting/backlog/budgeting-m3x7f-monatserster-leiste.md`: in beiden die Id
`budgeting-eh8xe` zu `depends_on` hinzufügen und den Text anhängen (bei budgeting-q4v8n in `## Notes`; budgeting-m3x7f hat keine `## Notes`, dort als neuer Abschnitt `## Hinweis aus budgeting-zkbbd (2026-11-05)` am Dateiende):

```markdown
- Nach dem Wechsel, auf den neuen Schreibweg (ADR 0084 Punkt 7, entschieden von Roman am
  2026-11-05): Dieser Task wird erst nach budgeting-eh8xe gebaut und schreibt Targets als
  Ereignisse in BB, nicht über den Zwei-Schritt-Weg nach YNAB.
```

**(c) budgeting-bk6tq:**

Datei `.agentheim/board/budgeting/backlog/budgeting-bk6tq-vortrag-ab-dem-ersten-tag.md`, in
`## Notes` anhängen (ohne Änderung an `depends_on`):

```markdown
- Unabhängig vom Wechsel der Führung (ADR 0084), der Spike kann jederzeit laufen. Seine
  Folge-Tasks (etwa Auffüllen über den Monatswechsel als neue Target-Art mit eigenem
  Ereignistyp) kommen erst nach budgeting-v7kfw und budgeting-eh8xe.
```

**(d) infrastructure-e4vq7:**

Datei `.agentheim/board/infrastructure/backlog/infrastructure-e4vq7-gemeinsame-ereignistabelle-events-schema-append-only-trigger.md`,
in `## Notes` anhängen:

```markdown
- Ergebnis-Ereignisse (Rückschreib-Ergebnisse im Budget, ADR 0084 Punkt 8) entstehen in einer
  späteren Transaktion als ihre Entscheidung und tragen keine `command_id`. Das generische
  Anhängen muss Ereignisse ohne Befehls-Id zulassen und schreibt dann keine Zeile in
  `processed_commands`.
```
