---
id: 0084
title: BB führt das Budget, YNAB wird Spiegel; Zuweisen, Verschieben und Targets als Ereignisse in BB, nach YNAB geht nur Zugewiesen je Monat und Kategorie
scope: budgeting
status: accepted
date: 2026-11-05
supersedes: []
superseded_by: []
related_tasks: [budgeting-zkbbd, budgeting-v7kfw, budgeting-hyn77, budgeting-qm5x9, budgeting-dgrp7, budgeting-dkjc5, budgeting-eh8xe, budgeting-e2z7v]
related_adrs: [0012, 0013, 0065, 0072, 0074, 0079, 0080, 0082, 0083]
related_research: []
---

# ADR 0084: BB führt das Budget, YNAB wird Spiegel

## Context

Ziel ist, von YNAB wegzukommen (ADR 0012, Fähigkeit für Fähigkeit). Im Budget ist die Wahrheit
heute YNAB: BB liest die Beträge live (ADR 0065) und hält nur fest, was es selbst nach YNAB
geschrieben hat (`budget_movements`, `target_pause_events`, `target_changes` samt
Ergebnistabellen). Weist Roman in YNAB zu, sieht BB das nicht. Die Budget-Historie ist dadurch
lückenhaft, und Targets können nur, was YNAB-Goals können (ADR 0072).

Beim Budget lässt sich die Richtung umdrehen, ohne auf ein vollständiges Kassenbuch zu warten
(der ING-Import fehlt noch): BB führt die Entscheidungen, YNAB rechnet vorerst weiter aus den
Buchungen. ADR 0083 hat die Grundlage gelegt: die gemeinsame Tabelle `events` mit Umschlag,
`processed_commands`, das Kriterium für Read Models und die Sprachregel (englische Typnamen).
ADR 0083 Punkt 8 lässt die heutigen Budget-Tabellen stehen, bis dieses ADR ihre Überführung
festlegt; Punkt 2 überlässt diesem ADR den Stream-Schnitt.

Entschieden mit Roman am 2026-11-05, Refinement mit tactical-modeler und architect am selben
Tag. Dieses ADR schreibt die Entscheidungen aus. Die Umsetzung verteilt sich auf sechs
Folge-Tasks:

| Kurz | Task | Inhalt | hängt an |
|---|---|---|---|
| T4 | budgeting-v7kfw | Fehlbetrag als reine Funktion, Parallelrechnung mit Abweichungsliste | nichts |
| T1 | budgeting-hyn77 | Budget-Ereignistypen, Codec, Startmigration der fünf Tabellen | ledger-k9tm2 |
| T2 | budgeting-qm5x9 | Befehls-Id im Budget | T1 |
| T3 | budgeting-dgrp7 | Übernahme sofort und im Hintergrund, Soll-Faltung, `observeMonth`, Echo-Regel | T1 |
| T5 | budgeting-eh8xe | Targets als BB-Regeln, Rückbau des Goal-Schreibwegs und der YNAB-Pause | T1, T4 |
| T6 | budgeting-e2z7v | Rückweg-Aktion "Targets nach YNAB zurückschreiben" | T5 |

Kein Budget-Ereignis wird in `events` geschrieben, bevor ledger-k9tm2 erledigt ist (ADR 0083
Punkt 6, Abfolge). T4 braucht `events` nicht und darf sofort laufen.

## Decision

### Grundsatz

- **BB führt im Budget die Entscheidungen:** Zuweisen, Verschieben, Targets und Pausen. Sie
  entstehen als Ereignisse in `events`.
- **YNAB wird Spiegel.** Nach YNAB geht nur der Zielzustand Zugewiesen je (Monat, Kategorie),
  absolut gesetzt, erst festhalten, dann senden (ADR 0065 Punkt 3).
- **YNAB rechnet vorerst weiter** Aktivität, Verfügbar und Zu verteilen aus den Buchungen, BB
  liest das live wie heute. BB übernimmt diese Rechnung erst mit vollständigem Kassenbuch, in
  einer eigenen späteren Entscheidung.
- **Nur Roman weist im Budget zu.** Weist er doch in YNAB zu, übernimmt BB das als Beobachtung
  und überschreibt es nicht.

Begründung: Die Entscheidungen sind das, was Roman in BB tut und was eine Historie braucht
(Regel aus ADR 0083 Punkt 7). Die Rechnung aus den Buchungen braucht ein vollständiges
Kassenbuch, das es noch nicht gibt. Wer die Entscheidungen führt, kann sie jetzt umstellen, ohne
die Rechnung anzufassen.

### 1. Stream-Schnitt

- **`budgeting:month:<YYYY-MM>`** trägt Zuweisen, Verschieben, Targets anwenden, Pausen,
  Beobachtungen, Rückschreib-Ergebnisse und die Übernahme eines Monats. Der Schnitt passt zur
  Sperre je Monat (`MonthGuard`, `src/Server/Budgeting/CommandHandlers.fs:36-47`). Eine
  Verschiebung ist ein Ereignis in einem Stream, Targets anwenden ebenso.
- **`budgeting:target:<categoryId>`** trägt die Target-Regel einer Kategorie. Der Schnitt passt
  zu `TargetGuard` (`CommandHandlers.fs:52-63`), weil ein Target für alle Monate gilt (ADR 0080
  Punkt 5). Die Kategorie-Id steht im Format `D` (ADR 0083 Punkt 2).
- **Monats-Id:** Im Ereignismodell (Stream-Name und Payload-Felder für einen Monat) ist der
  Monat `YYYY-MM`, etwa `2026-11`. Dieses Format gilt in allen Budget-Ereignissen gleich. Den
  Text baut eine Funktion an einer Stelle aus dem Monat (wie den Stream-Namen, ADR 0083 Punkt 2).
- **Es gibt keine Invariante über mehrere Kategorien.** BB prüft weder Verfügbar noch Zu
  verteilen auf >= 0 (negatives Zugewiesen und negatives Zu verteilen sind gültig). Deshalb
  braucht kein Aggregat mehrere Kategorien zugleich zu sperren.
- **Hingenommene Lücke:** Targets anwenden liest die Regeln unter `MonthGuard`, ein `TargetSet`
  läuft unter `TargetGuard`. Ändert Roman ein Target, während eine Anwendung läuft, kann diese
  noch mit der alten Regel rechnen. Bei einem Nutzer ist das harmlos, die nächste Anwendung
  korrigiert es.

### 2. Vokabular

Englische Typnamen nach ADR 0083 Punkt 9. Die README erklärt sie auf Deutsch.

| Heute (README, Tabelle) | Typ | Stream | Actor |
|---|---|---|---|
| GeldZugewiesen, Fehlbetrag-Knopf, Holen und Abgeben | `AssignmentChangedInBb` (`categoryId`, `previousAssigned`, `assigned`) | month | `UserInBb` |
| GeldVerschoben (heute zwei Zeilen) | `MoneyMoved` (`from` und `to` je mit vorher und nachher, `amount`) | month | `UserInBb` |
| TargetsAngewendet | `TargetsApplied` (`lines`, `skippedPaused`) | month | `UserInBb` |
| TargetPausiert | `TargetPaused` | month | `UserInBb` |
| PauseAufgehoben (Altwert "Fortgesetzt") | `TargetPauseLifted` | month | `UserInBb` |
| TargetGeändert (Geändert, Angelegt, Wechsel der Art) | `TargetSet` (vollständige Regel, `effectiveFrom`) | target | `UserInBb` |
| TargetGeändert (Entfernt) | `TargetRemoved` | target | `UserInBb` |
| ZuweisungNachYnabGeschrieben | `AssignmentWrittenBackToYnab` (`decisionEventId`, `categoryId`) | month | `YnabMirror` |
| ZuweisungRückschreibenFehlgeschlagen | `AssignmentWriteBackFailed` (`reason`) | month | `YnabMirror` |
| Ergebnis `changed-in-ynab` | `AssignmentWriteBackSupersededInYnab` | month | `YnabMirror` |
| Ziel ungesendet geschlossen | `AssignmentWriteBackWithdrawn` | month | `UserInBb` |
| neu: Zuweisung in YNAB | `AssignmentChangeObservedInYnab` (`previousAssigned` = BB-Soll, `assigned` = YNAB-Wert) | month | `YnabMirror` |
| neu: Übernahme | `BudgetTakenOverFromYnab` (je Monat, `categories`) | month | `YnabMirror` |
| neu: Übernahme | `TargetTakenOverFromYnab` (Regel plus rohes `ynabGoalType`) | target | `YnabMirror` |

Die Ergebnis-Ereignisse (`AssignmentWrittenBackToYnab`, `AssignmentWriteBackFailed`,
`AssignmentWriteBackSupersededInYnab`, `AssignmentWriteBackWithdrawn`) tragen alle
`decisionEventId` und `categoryId`, damit eine Zeile einer Entscheidung eindeutig ist.

- **Eine Verschiebung in YNAB erscheint bewusst als zwei `AssignmentChangeObservedInYnab`.** Einen
  Typ `MoneyMovedInYnab` gibt es nicht: BB sieht in YNAB nur zwei geänderte Zugewiesen-Werte und
  kann nicht wissen, ob sie zusammengehören. Ein Typ, der das behauptet, wäre eine Vermutung.
- **Created, Changed und KindChanged entfallen.** `TargetSet` trägt immer die vollständige Regel.
  "Angelegt" und "50 → 60" leitet die Historie aus der vorigen Faltung des Target-Streams ab, wie
  heute `TargetChangeKind` (`src/Shared/Budgeting.fs:587`). Drei Typen für denselben Fakt hielten
  eine Unterscheidung fest, die sich jederzeit berechnen lässt.
- **Gruppen-Id ist die `event_id` des Entscheidungsereignisses.** Die Rückschreib-Ergebnisse
  verweisen über `decisionEventId` darauf. Für Altbestände bleibt die `applicationId` im Payload,
  damit alte Verweise lesbar bleiben.
- **Kein neuer Actor-Wert für die Übernahme.** Sie läuft als `YnabMirror`, der Typname benennt
  sie. Ein eigener Actor hielte nichts fest, was der Typ nicht schon sagt.

### 3. YNAB-Änderungen erkennen, Echo

- **Wann:** BB vergleicht beim Lesen eines Monats und vor jedem Befehl auf einem Monat. Das Lesen
  ruft danach einen eigenen Befehl `observeMonth` auf; `QueryHandlers.budgetMonthOf` bleibt rein.
  Scheitert die Beobachtung, scheitert das Lesen nicht, sie wird nur geloggt. Vorbild ist der
  Seitenaufruf, der den Kassenbuch-Spiegel anstößt (`src/Server/Ledger/CommandHandlers.fs:1028-1034`).
- **Harte Regel:** Verglichen wird nur ein YNAB-Stand, der unter `MonthGuard` gelesen wurde.
  `observeMonth` liest YNAB deshalb selbst unter der Sperre. Jeder Befehl auf einem Monat
  beobachtet zuerst und entscheidet dann. Grund: Ein außerhalb der Sperre gelesener Stand kann
  älter sein als eine gerade festgehaltene Entscheidung. Der Vergleich hielte dann BBs eigene
  Zuweisung für eine Änderung in YNAB und überschriebe das Soll mit dem alten Wert.
- **Echo-Regel.** Verglichen wird gegen das gefaltete BB-Soll je (Monat, Kategorie). Vorbild ist
  das Kassenbuch (`src/Server/Ledger/Domain.fs:900-999`, ADR 0055 Punkt 5).

  | YNAB zeigt | offene Rückschreibung | Ereignis |
  |---|---|---|
  | das Soll | keine | keins |
  | das Soll | ja | `AssignmentWrittenBackToYnab` |
  | die Baseline der Entscheidung | ja | keins, bleibt offen |
  | anderen Wert | egal | `AssignmentChangeObservedInYnab`, das Soll wird der YNAB-Wert, eine offene Rückschreibung wird mit `AssignmentWriteBackSupersededInYnab` geschlossen |

  "Ausstehend" (Absturz zwischen Festhalten und Senden) gilt wie "fehlgeschlagen": Beides ist
  eine offene Rückschreibung. Dafür trägt das Entscheidungsereignis die Baseline
  (`previousAssigned`, bei `MoneyMoved` je Seite der Wert vorher). Ohne sie ließe sich die dritte
  Zeile nicht von der vierten unterscheiden.
- **Rasterprüfung.** Jeder Betrag, der ins Soll eingeht, liegt auf 10 Milliunits (ADR 0065
  Punkt 4). Das gilt für alle Wege, auch für Übernahme und Migration. Läge ein Soll neben dem
  Raster, rundete YNAB beim Senden still, und jedes Lesen erzeugte eine neue Beobachtung.

### 4. Migration

- **Ein Startschritt in einer Transaktion, nach ledger-k9tm2.** Alle fünf Tabellen
  (`budget_movements`, `budget_movement_results`, `target_pause_events`, `target_changes`,
  `target_change_results`) gehen nach `events`, Schreibwege und Leser werden umgestellt, und die
  Tabellen werden in derselben Transaktion entfernt. Es gibt keinen Lesestand dazwischen, sonst
  gäbe es zwei Historienquellen. YNAB führt in diesem Schritt noch, das Verhalten bleibt
  unverändert. Die einmalige Kopie ist die Ausnahme vom Nicht-Anfassen gespeicherter Ereignisse,
  wie ADR 0083 Punkt 6 sie für das Kassenbuch festlegt.
- **Abbildung.** Die Bewegungen einer Gruppe (`application_id`) werden ein
  Entscheidungsereignis nach Herkunft (`TargetsApplied`, `MoneyMoved`, `AssignmentChangedInBb`),
  die `applicationId` bleibt im Payload. Jede Ergebniszeile wird ein Ergebnis-Ereignis. Eine
  Pause-Zeile wird `TargetPaused` oder `TargetPauseLifted`, der Altwert "Fortgesetzt" als
  `TargetPauseLifted`. `target_changes` ohne `step` gilt als Schritt 1
  (`src/Server/Budgeting/Persistence.fs:569-570`). Alte `target_changes` tragen nur Teilwerte
  (Betrag, Datum, Rhythmus, keine vollständige Regel) und werden deshalb ein Altbestand-Typ, etwa `YnabGoalChangedFromBb` mit den Schritt-Ergebnissen.
- **Reihenfolge im Stream:** `occurred_at`, bei Gleichstand die Entscheidung vor ihrem Ergebnis,
  dann `rowid`. Die `position` migrierter Budget-Ereignisse liegt hinter dem Kassenbuch: Sie
  zeigt die Reihenfolge des Festhaltens in `events` (ADR 0083 Punkt 1).
- **Unbekannte Herkunft bricht den Start ab.** Eine Zeile mit einer Herkunft, Art oder einem
  Status, den die Abbildung nicht kennt, lässt die Transaktion zurückrollen, und BB startet
  nicht. Ein still übersprungener Datensatz fehlte in der Historie, ohne dass es jemand merkt.
- **Der Zustand beginnt beim Übernahme-Ereignis** (Soll, Regeln, Pausen, Punkt 5). Migrierte
  Ereignisse davor sind Historie und müssen nicht in die Faltung. So braucht die Migration keine
  vollständige Regel aus alten Teilwerten zu rekonstruieren.
- **Probe und Abnahme.** Vorher läuft die Migration gegen eine Kopie der Produktion, außerhalb
  des Repos. Abnahme: Vorher-nachher-Vergleich von "Zuletzt verwendet", den Historien, den Pausen
  und der letzten Target-Änderung.

**Nachtrag 2026-11-05 (budgeting-hyn77, umgesetzt):**
- Gruppen-Id: Migrierte Entscheidungen, Pausen und Target-Änderungen behalten ihre alte Id als
  `event_id`. Die `applicationId` steht deshalb nicht im Payload (abweichend von Punkt 2), alte
  Verweise bleiben über die `event_id` lesbar. Zeilen tragen `lineId` und den Kategorienamen.
- Weitere Abbruchregeln: eine Anwendung mit mehreren Zeitpunkten, eine Kategorie doppelt in einer
  Anwendung, Zuweisen mit mehr als einer Zeile, Verschieben ohne genau eine sinkende Quelle, ein
  Monat nicht in der Form `YYYY-MM-01`. Die alten Schreibwege konnten keine davon verletzen.
- Bei gleichem `occurred_at` und gleichem Rang ordnet eine feste Tabellenfolge vor der `rowid`.
- Die Rasterprüfung aus Punkt 3 läuft in der Migration nicht: Migrierte Ereignisse sind Historie
  und gehen nicht in die Faltung.
- Altbestand `changed-in-ynab` wird `AssignmentWriteBackSupersededInYnab`; neue ungesendete
  Schließungen beim erneuten Senden schreiben `AssignmentWriteBackWithdrawn`.
- Die Probe gegen eine Kopie der Produktion vor dem Deploy war in allen Punkten grün.

**Nachtrag 2026-11-05 (Verfeinerung budgeting-qm5x9 und budgeting-dgrp7, neu budgeting-dkjc5):**
- Punkt 8: `applyTargets` bekommt die Befehls-Id als Feld `CommandId: Guid option` in
  `TargetPreview`; ein neuer Parametertyp bräche alte Clients. Die Prüfung der Befehls-Id steht
  unter der Sperre vor Übernahme, Beobachtung und jedem YNAB-Aufruf; eine Wiederholung liest YNAB
  nicht. Die Antwort kommt aus dem Stream: `reportWith` für Verschieben, Zuweisen und Anwenden
  (ohne Zu verteilen), bei der Pause aus dem Ereignis. Eine Id, die zu einem anderen Auftrag
  gehört, wird abgelehnt, eine leere Guid an der Grenze. Target ändern bekommt seine Befehls-Id
  erst mit budgeting-eh8xe (entschieden von Roman): Der Vergleich gegen `Shown` lässt eine
  verspätete Wiederholung schon vorher ins Leere laufen.
- Punkt 3, Faltung: Neben dem Soll führt die Faltung je (Monat, Kategorie) den bestätigten Stand
  (zuletzt von YNAB bestätigt) und höchstens eine offene Rückschreibung. Die Baseline der
  Echo-Regel ist der bestätigte Stand; `previousAssigned` bleibt Historie. Eine neue Entscheidung
  auf einer Kategorie mit offener Rückschreibung zieht die alte im selben Append mit
  `AssignmentWriteBackWithdrawn` zurück. `AssignmentWriteBackWithdrawn` setzt das Soll auf den
  bestätigten Stand. `decideRetry` entfällt, erneut senden beobachtet zuerst.
- Punkt 3, Lesen: Das Lesen eines Monats ruft `observeMonth` unter `MonthGuard` auf und baut seine
  Antwort aus dessen YNAB-Stand (ein YNAB-Aufruf je Lesen). Die Abbildung bleibt rein; scheitert
  nur das Anhängen, wird geloggt und der Monat geliefert. Das präzisiert "danach". Zugewiesen zeigt
  das Soll, bei offener Rückschreibung markiert; Verfügbar und Zu verteilen bleiben YNABs Werte
  (entschieden von Roman). Targets anwenden lehnt bis budgeting-eh8xe ab, solange eine Kategorie
  mit Fehlbetrag eine offene Rückschreibung hat.
- Punkt 3, Raster: Die Rasterprüfung gilt für Werte, die BB sendet. Übernahme und Beobachtung
  übernehmen YNABs Wert ungerundet; Runden erzeugte bei jedem Lesen eine neue Beobachtung
  (entschieden von Roman).
- Punkt 5: Umgeschaltet wird mit dem Deploy von budgeting-dgrp7, ohne Setting (entschieden von
  Roman); ein Rücksprung geht nur über das Rückspielen der Datenbank-Sicherung. Der Serverstart
  hängt nicht von YNAB ab. Laufender und künftige Monate übernimmt das erste Lesen oder der erste
  Befehl; ein Hintergrunddienst (budgeting-dkjc5) arbeitet laufenden, künftige und ältere Monate ab,
  höchstens einen je Minute. "Künftige" sind die Monate aus `GET /months` nach dem Berliner
  laufenden Monat. Ohne Übernahme und ohne lesbares YNAB wird ein Befehl abgelehnt. Die Übernahme
  umfasst alle Kategorien außer `internal`, auch ausgeblendete und gelöschte, und schließt offene
  Rückschreibungen migrierter Entscheidungen des Monats mit `AssignmentWriteBackWithdrawn`.
- Tabelle der Folge-Tasks: T3 ist geteilt in budgeting-dgrp7 (hängt an T1 und T2) und
  budgeting-dkjc5 (hängt an T3). Beide gehen nur zusammen in Produktion (entschieden von Roman).

**Nachtrag 2026-11-06 (budgeting-dgrp7 und budgeting-dkjc5, umgesetzt):**
- Punkt 3, erneut senden: Die Paarregel aus ADR 0069 Punkt 3 zieht eine Seite einer Verschiebung
  auch dann zurück, wenn ihr Partner von einer späteren Entscheidung in BB ersetzt wurde, nicht
  nur bei einer Änderung in YNAB. Gesendet wird nur eine Zeile, deren Entscheidung noch die
  offene Rückschreibung ihrer Kategorie ist.
- Punkt 3, Lesen: Scheitert beim Lesen nur das Festhalten von Übernahme oder Beobachtung, zeigt
  der Monat YNABs Zugewiesen ohne Markierung, und der Grund steht im Log. Ein Befehl lehnt in
  diesem Fall außen ab und entscheidet nichts.
- Historie: Beobachtung und Übernahme erscheinen als eigene Herkünfte (`HistoryOrigin`,
  `ObservedInYnab` und `TakenOverFromYnab` neben `Decision`), ohne Stand beim Schreiben.
  `MovementOrigin` bleibt auf die drei Entscheidungen beschränkt. Eine von einer späteren
  BB-Entscheidung zurückgezogene Zeile heißt "in BB ersetzt".
- Punkt 5, Hintergrund: Scheitert die Übernahme eines Monats mit einem anderen Fehler als 404,
  429 oder 401, überspringt der Dienst den Monat für diesen Durchlauf; der nächste Durchlauf
  versucht ihn neu (entschieden von Roman). Ob ein Monat schon übernommen ist, prüft er gezielt
  am Ereignistyp, ohne den Stream zu dekodieren.
- Anzeige: Die Markierung "noch nicht in YNAB" steht in der Budget-Zeile neutral, am PC und am
  Handy gleich; Gelb bleibt in der Zeile "fehlt" vorbehalten (entschieden von Roman).

**Nachtrag 2026-11-06 (budgeting-r4kqm und budgeting-b9869, Parallelrechnung in Prod):**
- Punkt 7, Formel "Mit Fälligkeit": Bei einem Target mit Rhythmus gilt die nächste Fälligkeit ab
  dem gelesenen Monat (das gespeicherte Datum, um den Rhythmus verschoben, `dueFrom`). Als gedeckt
  zählen Übertrag plus die **Ausgaben der laufenden Periode** (vom Monat nach der vorigen
  Fälligkeit bis vor den gelesenen Monat), wie YNABs `goal_overall_funded`. Belegt am Vergleich in
  Prod (Mai 2026 bis Dezember 2026, dann Dezember 2025 bis November 2026) und an YNABs `goal_*`-Feldern.
- Quelle der Ausgaben: YNABs Monatsaktivität, festgehalten als Zustand `category_month_activity`
  (ADR 0083), überschrieben bei jedem Lesen eines Monats, ohne zusätzlichen YNAB-Aufruf
  (entschieden von Roman). Fehlt ein Monat der Periode, rechnet BB nicht und nennt den Grund.
- Toleranz für die Umschaltung (budgeting-hbhzf, entschieden von Roman): jede Abweichung im
  laufenden Monat hat einen bekannten Grund.

**Nachtrag 2026-11-06 (budgeting-pf8n7 und budgeting-eh8xe):**
- Punkt 7, laufende Periode: Sie beginnt im Monat der vorigen Fälligkeit, einschließlich. Bei der
  ersten Fälligkeit eines Ziels beginnt sie im Anlegemonat (am Target `CreatedIn`, aus YNABs
  `goal_creation_month`). Das ersetzt "vom Monat nach der vorigen Fälligkeit" im Nachtrag davor.
  Vierter Vergleich in Prod (November 2026, Monate ab Dezember 2024 gelesen): 99 von 100 gleich,
  keine Abweichung, eine Kategorie "nicht gerechnet", weil ihre erste Periode vor Dezember 2024
  beginnt. Roman hat das als erfüllte Toleranz angenommen.
- Punkt 5, Targets (umgesetzt in budgeting-eh8xe): BB übernimmt sie, wenn ein Monat gelesen wird,
  bei der Vorschau und vor einem Befehl, sobald eine Kategorie des Monats ein übernehmbares Goal
  zeigt, dessen Target BB noch nicht führt. Übernehmbar sind NEED, TB, TBD und MF. Ein vergangener
  Monat löst keine Übernahme aus. Beim Lesen läuft sie nach bestem Bemühen, vor einem Befehl streng.
- Punkt 6: Der Anlegemonat gehört zur Regel. Die Übernahme bringt ihn mit, eine Änderung behält ihn,
  eine in BB neue Regel (Anlegen, Wechsel der Art) bekommt den Monat, ab dem sie gilt.
- Punkt 7, Target ändern: Der Wochentag eines wöchentlichen Targets ist Pflicht (entschieden bei
  der Triage, Roman informiert). Ohne YNAB-Aufruf prüft BB nicht mehr, ob die Kategorie im Monat
  sichtbar ist (hingenommen).

**Nachtrag 2026-11-06 (budgeting-k5q2w, Quelle der Ausgaben):**
- Punkt 7, Ausgaben der Periode: Die Aktivität kommt aus BBs Kassenbuch
  (`Ledger.QueryHandlers.getMonthlyActivityByCategory`, netto, mit Split-Anteil, ohne in YNAB gelöschte
  Buchungen), entschieden von Roman. Das löst "YNABs Monatsaktivität, festgehalten als Zustand
  `category_month_activity`" aus dem Nachtrag zu budgeting-r4kqm und budgeting-b9869 ab. Die Tabelle
  entfällt, das Schema entfernt sie.
- Lückenlos: Ein Monat ohne Buchung der Kategorie zählt als 0. Nur ein Monat vor dem Monat der ersten
  Buchung des Kassenbuchs fehlt, dann rechnet BB nicht und nennt den Grund. Grund für den Wechsel: Ein
  nie gelesener Monat fehlte, und ein vor seinem Ende gelesener Monat galt für immer mit einem Teil
  seiner Ausgaben.
- Die Parallelrechnung bleibt bis zum fünften Vergleich in Prod stehen, erst danach schaltet
  budgeting-hbhzf auf BBs Fehlbetrag um.

### 5. Übernahme

Entschieden von Roman am 2026-11-05.

- **Laufender und künftige Monate** werden beim Umschalten sofort übernommen. **Ältere Monate**
  folgen nach und nach im Hintergrund unter der Rate-Grenze von YNAB (rund 200 Anfragen pro
  Stunde, rund 97 Monate). Liest BB vorher einen noch nicht übernommenen Monat, übernimmt es ihn
  dabei.
- **Ein `BudgetTakenOverFromYnab` je Monat, unter `MonthGuard`.** Kategorien `internal` werden
  nicht übernommen. Idempotenz: Unter der Sperre prüft BB, ob im Monats-Stream schon ein
  Übernahme-Ereignis liegt. Version 1 ist kein Merkmal, weil migrierte Altereignisse davor liegen
  können.
- **Ein spät geschriebenes Übernahme-Ereignis ist korrekt.** Es hält fest, wann BB den Stand
  erfahren hat; `occurred_at` wird nicht zurückdatiert (ADR 0083 Punkt 1). Eine Änderung in YNAB
  an einem alten Monat vor dessen Übernahme geht im Schnappschuss auf. Das ist hingenommen.
- **Targets** werden einmal aus `/categories` übernommen (`goal_*`,
  `src/Server/YnabClient.fs:90-103`), als `TargetTakenOverFromYnab` je Kategorie mit dem rohen
  `ynabGoalType`. Das gehört zu T5 (budgeting-eh8xe).
- **Verworfen:**
  - Alles auf einmal vor dem Umschalten: rund 97 Monate passen nicht unter die Rate-Grenze, das
    Umschalten stünde stundenlang.
  - Nur beim ersten Lesen übernehmen: Ältere Monate blieben unbestimmt lange offen, und die
    Historie hätte Lücken, solange Roman sie nicht öffnet.

### 6. Targets und YNAB-Goals

Entschieden von Roman am 2026-11-05.

- **Target-Arten 1:1.** Beim Wechsel gibt es genau die heutigen YNAB-Arten, beschränkt auf die in
  Romans Budget genutzten. DEBT wird nicht übernommen und gemeldet. Erweiterungen, etwa Auffüllen
  über den Monatswechsel (budgeting-bk6tq), kommen danach als eigene Tasks mit neuen
  Ereignistypen. Grund: Der Wechsel soll das Verhalten nicht zugleich ändern, sonst lässt sich
  eine Abweichung nicht dem einen oder dem anderen zuordnen.
- **YNAB-Goals einfrieren.** Beim Umschalten bleiben die Goals in YNAB auf ihrem Stand und werden
  nur noch gelesen (Übernahme, Parallelrechnung). BB schreibt im Alltag keine Goals mehr.
- **Rückweg.** Eine bewusst auszulösende Aktion "Targets nach YNAB zurückschreiben" schreibt BBs
  Targets als Goals nach YNAB, soweit YNAB sie darstellen kann, und listet, was nicht geht. Sie
  ist für den Fall, dass Roman zu YNAB zurückkehrt (T6, budgeting-e2z7v). Zuweisungen brauchen
  keinen Rückweg, YNAB hat sie laufend.
- **Verworfen:**
  - Laufendes Spiegeln der Targets nach YNAB: Es hielte den Zwei-Schritt-Schreibweg aus ADR 0072
    und 0080 im Alltag am Leben, mit allen Teilfehlern (`TargetPartlyWritten`), obwohl kein
    Leser in YNAB die Goals mehr braucht.
  - Goals in YNAB entfernen: Es gäbe keinen Rückweg, und die Parallelrechnung gegen
    `goal_under_funded` (Punkt 7) fiele weg.

### 7. Fehlbetrag und bisherige Target-Arbeit

- **Fehlbetrag als reine Funktion** `underfunded rule month assigned carryover activity`. Sie
  ersetzt `goal_under_funded` (`src/Shared/Budgeting.fs:93`, verwendet in
  `src/Server/Budgeting/Domain.fs:78-114`). Zugewiesen kommt aus der BB-Faltung, der Übertrag
  live (Verfügbar − Zugewiesen − Aktivität).

  | Art | Fehlbetrag |
  |---|---|
  | NEED monatlich, MF | Betrag − Zugewiesen |
  | NEED wöchentlich | Anzahl des Wochentags im Monat × Betrag − Zugewiesen |
  | NEED und TBD mit Fälligkeit | (Betrag − Übertrag) / verbleibende Monate − Zugewiesen |
  | Auffüllen bis | Betrag − (Übertrag + Zugewiesen) |
  | TB | Betrag − Verfügbar |
  | DEBT | nicht übernommen |

  Ein negatives Ergebnis heißt 0 (erfüllt), wie heute bei `goal_under_funded`.
- **Die Formeln sind Annahmen über YNABs Logik.** Bedingung für das Umschalten von "Targets
  anwenden" ist deshalb eine Parallelrechnung gegen `goal_under_funded` für alle Targets mit
  Abweichungsliste (T4, budgeting-v7kfw). Die Toleranz legt Roman anhand dieser Liste fest. Eine
  abweichende Rechnung wiese sonst still andere Beträge zu als heute.
- **Entfällt mit T5** (budgeting-eh8xe): `planTargetWrite` (`Domain.fs:206`), `sendSteps`
  (`CommandHandlers.fs:444-483`), der Port `SetTarget`, die Zwei-Schritt-Logik aus ADR 0072
  Punkt 5 und ADR 0080 Punkte 4 und 6, `TargetPartlyWritten`, `TargetChangedInYnab` und
  `PausedInYnab`/`PausedInBoth` (ADR 0079 Punkt 2). `setCategoryTarget`
  (`YnabClient.fs:553-577`) bleibt nur, falls die Rückweg-Aktion (T6) ihn nutzt.
- **Bleibt:** `changeCategoryTarget` (`src/Shared/Api.fs:551`), es hängt nur noch ein Ereignis
  an. `TargetGuard`. Der Vergleich gegen `Shown`, jetzt gegen die BB-Faltung statt gegen YNAB.
  ADR 0080 Punkt 1 (`effectiveFrom` ist der laufende Monat). **Neu:** Vergangene Monate zeigen
  die damals gültige Regel, weil sich die Regel zu jedem Zeitpunkt aus dem Target-Stream falten
  lässt.
- **Der Wochentag wöchentlicher Targets wird in BB setzbar.** Heute ist er nur lesbar, weil YNAB
  ihn über die API nicht setzen lässt (ADR 0072 Punkt 4). Diese Grenze fällt mit BBs eigener
  Regel weg.
- **Reihenfolge der offenen Tasks** (entschieden von Roman am 2026-11-05): budgeting-q4v8n (Seite
  Targets prüfen) und budgeting-m3x7f (Monatserster) werden nach dem Wechsel gebaut, auf den
  neuen Schreibweg. Vorher gebaut, schrieben sie auf den Zwei-Schritt-Weg, der mit T5 entfällt.
  budgeting-bk6tq (Spike Monatswechsel) ist unabhängig; seine Folge-Tasks kommen nach T4 und T5.

### 8. Idempotenz

- **`CommandId: Guid option`** in `MoveRequest`, `AssignRequest`, `TargetPauseRequest` und
  `TargetChangeRequest` (`src/Shared/Budgeting.fs:343-428`). `applyTargets` bekommt einen
  Request-Record oder ein Feld in `TargetPreview`. Das Feld ist optional, damit ein alter Client
  `None` schickt und der Server sich wie heute verhält. Ein Test belegt, dass Fable.Remoting ein
  fehlendes Optionsfeld als `None` liest.
- **Für alle UI-Entscheidungen, nicht nur `moveMoney`.** Zuweisen setzt einen absoluten Wert.
  Eine verspätete Wiederholung setzte sonst einen inzwischen späteren Wert zurück.
  `retryFailedTargets` braucht keine, weil es nur offene Rückschreibungen absolut erneut sendet.
- **Prüfung zuerst unter der Sperre** (`MonthGuard`) gegen `processed_commands`. Das spart den
  YNAB-Aufruf. Das Netz ist die Anhänge-Transaktion (ADR 0083 Punkt 3). Die Antwort auf eine
  Wiederholung baut der Server aus dem Stream: `command_id` → Entscheidungsereignis →
  `reportWith` (`CommandHandlers.fs:115`).
- **Client:** eine Id je Absicht, nach Status 0 einmal automatisch wiederholen
  (`Retry.writing` neben `Retry.reading`, `src/Client/Retry.fs:45`).
- **Ergebnis-Ereignisse** entstehen in einer späteren Transaktion als ihre Entscheidung und
  tragen keine `command_id`. Die Befehls-Id gehört zur Absicht; das Senden nach YNAB bekommt keine.

### Verhältnis zu früheren ADRs

- ADR 0065 Punkte 1, 4 und 6, ADR 0079 Punkt 2, ADR 0080 Punkte 3 bis 6 und ADR 0082 Punkt 7
  bekommen je einen Nachtrag mit Verweis auf dieses ADR. Sie werden nicht ersetzt; was sie
  sagen, gilt bis zur Umsetzung des jeweiligen Folge-Tasks.
- ADR 0072 Punkt 1 ("In der Spiegel-Phase führt YNAB die Targets") endet mit T5. ADR 0072 ließ
  den Wechsel zu BB als führendem System ausdrücklich offen; dieses ADR entscheidet ihn.
- ADR 0083 gilt unverändert. Dieses ADR füllt die Stellen, die es offen ließ (Stream-Schnitt,
  Überführung der Budget-Tabellen, englische Typnamen).

## Consequences

### Positive
- Die Budget-Historie wird vollständig: auch Zuweisungen in YNAB erscheinen, als Beobachtung.
- Targets können künftig mehr als YNAB-Goals (Wochentag setzbar, später neue Arten), ohne auf
  YNABs API zu warten.
- Der Zwei-Schritt-Schreibweg für Goals mit seinen Teilfehlern fällt im Alltag weg.
- Vergangene Monate zeigen die damals gültige Regel.
- Wiederholte UI-Befehle setzen keinen späteren Wert mehr zurück.
- Der Weg zurück zu YNAB bleibt offen: Zuweisungen hat YNAB laufend, Targets über T6.

### Negative
- Jedes Lesen eines Monats kann warten, bis ein laufender Befehl desselben Monats fertig ist (bis
  zu drei YNAB-Anfragen). Fällt das im Alltag auf, wird ein Vergleich über die zuletzt gesehene
  Position geprüft; das ist nicht Teil dieses ADRs.
- Ein YNAB-Stand außerhalb der Sperre erzeugte falsche Beobachtungen. Das verlangt Tests aller
  Reihenfolgen von Lesen und Senden (T3).
- Die Startmigration kann den Serverstart verhindern. Deshalb vorher die Probe gegen eine Kopie.
- Die eigene Fehlbetrag-Rechnung kann von YNAB abweichen und andere Beträge zuweisen als heute.
  Deshalb die Parallelrechnung vor dem Umschalten (T4).
- Ein vergessener `context`-Filter beim Laden aus `events` liest Kassenbuch-Ereignisse.
- Neue Optionsfelder in `src/Shared` brechen die Fixtures in `scripts/*.test.mjs` erst zur
  Laufzeit. Jeder Task mit Änderungen dort zieht sie im selben Schritt nach.
- Eine Änderung an einem alten Monat in YNAB vor dessen Übernahme bleibt unsichtbar.

### Neutral
- YNAB rechnet Aktivität, Verfügbar und Zu verteilen weiter. Wann BB das selbst rechnet, bleibt
  eine eigene spätere Entscheidung.
- Die Remoting-Schnittstelle tauscht den Monat weiter als "YYYY-MM-01" aus (README, Monat); das
  Format `YYYY-MM` gilt im Ereignismodell.
- budgeting-w3p7n, budgeting-q8m2v und budgeting-h9c3w sind nach diesem ADR neu einzuordnen.
- Sinnvoll vor dem Wechsel, aber kein Blocker: infrastructure-g4wy8 (Verbindungsabbrüche) und
  infrastructure-aaryk (alter Client gegen neuen Server).

## Alternatives considered

- **Stream je Kategorie** (`budgeting:category:<categoryId>`, das Beispiel aus ADR 0083 Punkt
  2): verworfen. Eine Verschiebung schriebe in zwei Streams, Targets anwenden in rund 100. Keine
  der heutigen Sperren passt dazu.
- **Stream je (Monat, Kategorie):** verworfen. Kein Aggregat schützt dort eine Invariante, und
  keine Sperre passt. Eine Verschiebung und eine Anwendung lägen wieder in mehreren Streams.
- **Eigener Typ `MoneyMovedInYnab`:** verworfen, siehe Punkt 2. BB kann eine Verschiebung in YNAB
  nicht von zwei einzelnen Zuweisungen unterscheiden.
- **Getrennte Typen `TargetCreated`, `TargetChanged`, `TargetKindChanged`:** verworfen. Die
  Unterscheidung folgt aus der vorigen Faltung.
- **Ein eigener Actor für die Übernahme:** verworfen. Der Typname sagt es schon.
- **`QueryHandlers.budgetMonthOf` schreibt die Beobachtung selbst:** verworfen. Das Lesen bliebe
  nicht rein, und ein Fehler beim Schreiben ließe das Lesen scheitern.
- **Knopf "Abgleichen":** verworfen. Er wird vergessen, und bis dahin rechnet BB mit einem
  veralteten Soll. Später möglich: Abgleich im Hintergrund über das Delta von `/budgets/{id}`,
  erst nach einem Spike.
- **Migration mit Lesestand (alte Tabellen bleiben eine Weile lesbar):** verworfen. Es gäbe zwei
  Historienquellen.
- **Übernahme aller Monate vor dem Umschalten** und **Übernahme nur beim ersten Lesen:** verworfen,
  siehe Punkt 5.
- **Laufendes Spiegeln der Targets** und **Goals in YNAB entfernen:** verworfen, siehe Punkt 6.
- **Befehls-Id nur für `moveMoney`:** verworfen. Auch ein wiederholtes absolutes Zuweisen kann
  einen späteren Wert zurücksetzen.