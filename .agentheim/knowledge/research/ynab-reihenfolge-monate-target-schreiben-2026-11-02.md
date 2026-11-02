---
topic: YNAB-API: Reihenfolge der Kategorien, Monatsgrenzen, Target ändern ohne Verlust von Tag und Rhythmus
date: 2026-11-02
requested_by: model
related_tasks: [budgeting-q7m2c]
---

# Research: Reihenfolge, Monate, Target schreiben

## Frage
Drei Fragen an YNAB entscheiden über den Zuschnitt der Bau-Tasks der Budget-Seite: Liefert die
API die Anzeigereihenfolge der Kategorien? Wie weit reichen die Monate nach hinten und vorn?
Überlebt eine Target-Änderung Tag und Rhythmus?

## Methode
OpenAPI-Spec v1.88.0 (`https://api.ynab.com/papi/open_api_spec.yaml`), dazu echte Aufrufe am
2026-11-02 mit `YNAB_TOKEN`, 107 Aufrufe. Lesend gegen Romans Budget und drei weitere Budgets
(nur GET). Schreibend nur gegen das Budget "Test", abgesichert durch einen Wächter im
Wegwerf-Skript, der jede Schreibung mit einer anderen Budget-Id verweigert. Die Kategorien in
Test tragen die englischen Namen der YNAB-Vorlage. Romans Kategorien bleiben hier ungenannt,
der Vergleich für das Auge liegt lokal und wird nie committet
(`.worktrees/.results/budgeting-q7m2c-reihenfolge.txt`).

## Ergebnis in Kürze

| Frage | Antwort |
|---|---|
| 1 Reihenfolge | `GET /months/{month}` ist ungeordnet und je Monat anders. `GET /categories` ist geordnet und über Aufrufe stabil, Anzeige-Gleichheit bestätigt Roman |
| 2 Monatsgrenzen | Nach hinten: `first_month`. Nach vorn: einen Monat hinter der letzten Zuweisung, `GET /months` zeigt genau das offene Fenster. Eine Zuweisung im letzten Monat öffnet den nächsten, auch nach Rücksetzen auf 0 |
| 3 Target schreiben | Nur `goal_target` senden erhält alles Beobachtete. `goal_frequency` ersetzt das Target: Das Datum geht immer verloren, der Tag beim Wechsel der Art. Jährlich mit Datum geht in zwei Schritten |

## 1. Reihenfolge

Die Spec kennt kein Sortierfeld: keine Treffer für sort, order, position oder priority. Die
Schemas tragen auch keinen Index. Maßgeblich ist also die Reihenfolge der Antwort.

Beobachtung an Romans Budget (167 Kategorien in 14 Gruppen, davon sichtbar 128 in 9 Gruppen):

| Prüfung | Ergebnis |
|---|---|
| Flache Reihenfolge `/categories` gleich `/months/current` | nein, 126 von 128 sichtbaren Positionen weichen ab |
| Innerhalb einer Gruppe gleich | in 0 von 9 sichtbaren Gruppen |
| Gruppen in `/months/current` zusammenhängend | nein: 117 Gruppenwechsel bei 167 Kategorien und 11 Gruppen mit Einträgen |
| Paarweise Vertauschungen zwischen beiden Reihenfolgen | 7180 von 13861, also etwa die Hälfte: keine erkennbare Beziehung |
| `/months` nach Name, Id oder Gruppen-Id sortiert | nein |
| `/months/2026-10-01` gleich `/months/2026-11-01` | nein, die Reihenfolge wechselt je Monat |
| Wiederholter Abruf desselben Monats, `current` gleich `2026-11-01` | gleich |
| `/categories` zweimal, `/budgets/...` gleich `/plans/...` | gleich |
| `GET /budgets/{id}`: `category_groups` gleich `/categories` | gleich, die flache Liste `categories` dort ist weder gleich `/months` noch nach Id sortiert |
| `/categories` nach Name oder Id sortiert | nein, weder die Gruppen noch die Kategorien innerhalb der 9 sichtbaren Gruppen |

Damit ist der Befund aus der Oberfläche erklärt: `toBudgetMonth` übernimmt die Reihenfolge aus
`GET /months/{month}`, und die ist keine Anzeigereihenfolge. Der Kommentar dort stimmt nicht.

`GET /categories` sieht von Hand sortiert aus: Die Gruppen sind nicht alphabetisch, ausgeblendete
Gruppen stehen mittendrin statt am Ende (Position 5 und 13 von 14), und die Kategorien stehen
nicht alphabetisch. Im Budget Test folgt die Antwort der Vorlage: Bills, Needs, Wants, innerhalb
von Bills Rent, Phone, Utilities. Beides passt zu einer gepflegten Anzeigereihenfolge. Einen
Beweis liefert die API nicht, **Roman hat die Anzeige am 2026-11-02 von Hand bestätigt.**

Monatsantwort und `/categories` tragen dieselbe Menge an Ids, die Zuordnung ist also verlustfrei.

**Empfehlung:** Die Budget-Seite ordnet Gruppen und Kategorien nach `GET /categories` und liest
die Monatswerte weiter aus `GET /months/{month}`, die Zusammenführung läuft über die Kategorie-Id;
eine eigene Erfassung der Reihenfolge in BB entfällt, falls Roman die Anzeige bestätigt.

Falls Roman widerspricht, ist die billigste Alternative die einmalige Erfassung in BB. Alphabetisch
wäre nicht billiger und gäbe seine Reihenfolge ebenfalls nicht wieder.

## 2. Monatsgrenzen

### Lesen

| Budget | `first_month` (Liste) | `last_month` (Liste) | letzter Monat in `GET /months` | erster 404 nach vorn | erster 404 nach hinten |
|---|---|---|---|---|---|
| Romans Budget | 2018-12 | 2026-11 | 2026-12 (97 Monate) | 2027-01 | 2018-11 |
| Test, vor der Probe | 2026-10 | 2026-11 | 2026-12 | 2027-01 | 2026-09 |
| weiteres Budget A | 2022-01 | 2025-12 | 2026-01 | nicht abgefragt | nicht abgefragt |
| weiteres Budget B | 2026-04 | 2026-11 | 2026-12 | nicht abgefragt | nicht abgefragt |
| weiteres Budget C | 2018-12 | 2024-03 | 2024-04 | nicht abgefragt | nicht abgefragt |

- Alle Fehler sind `404`, `resource_not_found`. Auch ungültige Daten: `2018-13-01` und die
  Monatsmitte `2026-12-15` geben 404.
- `GET /months` liefert immer genau einen Monat mehr als `last_month` der Budget-Liste, und jeder
  Monat aus der Liste liefert über `GET /months/{month}` 200. Die Liste beschreibt das Fenster also
  vollständig.
- `first_month` ist die harte Grenze nach hinten. Bei Roman sind 2018-12 bis heute lesbar, der Monat
  2018-12 mit 139 statt 167 Kategorien.
- Die Grenze nach vorn hängt nicht am Kalender: Budget A endet 2026-01, zehn Monate vor heute, und
  `months/current` gibt dort 404. Romans Budget wäre davon nur betroffen, wenn ein laufender Monat
  gar keine Zuweisung hätte und der Monat vorher auch nicht.

### Schreiben im Budget Test

| Schritt | Status | Fenster danach (`GET /months`) | `last_month` |
|---|---|---|---|
| Ausgang | | 2026-10 bis 2026-12 | 2026-11 |
| `PATCH` 2027-03 (`budgeted` 1000) | 404 | unverändert | 2026-11 |
| `PATCH` 2027-01, direkt hinter dem Fenster | 404 | unverändert | 2026-11 |
| `PATCH` 2026-09, vor dem ersten Monat | 404 | unverändert | 2026-11 |
| `PATCH` 2026-10 auf 1000, dann 0 (Vormonat) | 200, 200 | unverändert | 2026-11 |
| `PATCH` 2026-12 auf 0 (war schon 0) | 200 | unverändert | 2026-11 |
| `PATCH` 2026-12 auf 1000 | 200 | bis 2027-01 | 2026-12 |
| `PATCH` 2026-12 zurück auf 0 | 200 | bleibt bis 2027-01 | 2026-12 |
| `PATCH` 2027-01 auf 1000 | 200 | bis 2027-02, 2027-03 gibt 404 | |
| `PATCH` 2027-01 zurück auf 0 | 200 | bleibt bis 2027-02 | |

Regel aus diesen Daten: Das Fenster reicht bis einen Monat hinter dem letzten Monat, der je eine
Zuweisung ungleich 0 hatte. Der letzte Monat des Fensters ist schreibbar, und eine Zuweisung dort
schiebt die Grenze um genau einen Monat; das Zurücksetzen nimmt das nicht zurück. Das passt zur
Probe vom 2026-10-26: Damals war der November leer und der Dezember gab 404, heute hat der November
Zuweisungen und der Dezember antwortet. Das Fenster lässt sich schrittweise weiterschieben, ein
Sprung geht nicht. Eine Obergrenze wurde nicht gesucht.

Ein alter Monat im engeren Sinn war nur als Vormonat prüfbar: Test beginnt 2026-10, und gegen
Romans Budget wird nicht geschrieben. Für Monate wie 2019 ist `PATCH` also nicht belegt.

**Empfehlung:** Die Budget-Seite blättert genau über die Monate aus `GET /months` statt über
"dieser" und "nächster", behandelt 404 beim Lesen und Schreiben als "Monat nicht offen" und lässt
weiter entfernte Monate zu, sobald eine Zuweisung im letzten Monat sie geöffnet hat, ohne das
Fenster selbst mit Schein-Zuweisungen zu schieben.

## 3. Target schreiben

`goal_day`, `goal_cadence` und `goal_cadence_frequency` sind nicht schreibbar. Ein Body mit
`goal_target: 0, goal_day: 3, goal_cadence: 2, goal_cadence_frequency: 2` gab 200 und ignorierte
die drei Felder. Ein Body nur mit `goal_day` gab 400 (`param is missing or the value is empty or
invalid: category`). Die Spec sagt zu `goal_frequency` ausdrücklich: "replacing any existing target".

### Nur `goal_target` gesendet

| Fall | Vorher | Gesendet | Nachher |
|---|---|---|---|
| wöchentlich mit Wochentag (Transportation) | NEED, cadence 2, frequency 1, day 6, whole true, target 0 | `goal_target: 20000`, dann 0 | alles gleich, nur target |
| monatlich (Dining out) | NEED, cadence 1, frequency 1, whole true, 30000 | `goal_target: 35000` | alles gleich, nur target |
| monatlich, Refill (Dining out) | wie oben, whole false, 35000 | `goal_target: 36000` | whole bleibt false |
| mit Datum (Phone & Internet) | NEED, cadence 1, frequency 1, date 2027-06-01, 120000 | `goal_target: 130000` | Datum und Rhythmus bleiben |
| jährlich mit Datum (Utilities) | NEED, cadence 13, frequency 1, date 2027-06-01, 240000 | `goal_target: 250000` | Datum und Rhythmus bleiben |
| Zielstand (Vacation) | TB, target 0 | `goal_target: 50000`, dann 0 | bleibt TB |

Nur `goal_target_date` oder nur `goal_needs_whole_amount` zu senden ändert ebenfalls nur dieses
Feld. Der Oktober-Verlust von `goal_day` kam also vom Löschen und Neuanlegen, nicht vom Ändern
des Betrags.

Nebenwirkung: `goal_creation_month` ging bei Targets ohne diesen Wert mit der ersten Schreibung
auf den laufenden Monat (Transportation und Vacation von leer auf 2026-11). Welche Wirkung das
auf Vormonate hat, wurde nicht geprüft.

### Wechsel der Art

| Vorher | Gesendet | Nachher |
|---|---|---|
| wöchentlich, day 6 | `goal_target, goal_frequency: weekly` | day 6 bleibt |
| wöchentlich, day 6 | `goal_target, goal_frequency: monthly` | cadence 1, **day leer** |
| monatlich | `goal_target, goal_frequency: weekly` | cadence 2, day leer: Wochentag nicht wählbar |
| wöchentlich | `goal_target_date` | Datum kommt dazu, cadence 2 bleibt |
| mit Datum | `goal_target_date: null` | Datum weg, Rest bleibt |
| mit Datum | `goal_target, goal_frequency: monthly` | cadence 1, **Datum leer** |
| jährlich mit Datum | `goal_target, goal_frequency: yearly` (gleiche Art) | cadence 13, **Datum leer** |
| monatlich | `goal_target_date` | Datum kommt dazu, cadence 1 bleibt |

`goal_target: null` entfernt das Target vollständig, wie im Oktober.

### Jährlich mit Datum

Nicht in einem Aufruf: `goal_frequency: yearly` zusammen mit `goal_target_date` gibt 400
`goal frequency cannot be combined with goal_target_date`. In zwei Aufrufen geht es: erst
`goal_target, goal_frequency: yearly` (cadence 13, day leer), dann `goal_target_date`. Das Ergebnis
ist cadence 13 mit Datum, dieselbe Form wie Romans 28 Jahresziele. YNAB rechnet es: `goal_target`
250000 bis 2027-06-01 gab im November `goal_under_funded` 31250 und `goal_months_to_budget` 8.
Ohne Datum waren es 250000.

### Was offen bleibt

Diese Formen stehen in Romans Budget (101 Targets), lassen sich in Test aber über die API nicht
herstellen. "Bleibt erhalten" ist für sie nicht belegt:

| Form | Anzahl |
|---|---|
| NEED monatlich mit `goal_day` | 42 |
| NEED alle 3 Monate mit Datum (cadence 1, frequency 3) | 4 |
| MF in allen Varianten | 18 |

Die übrigen sind belegt: NEED monatlich ohne Tag 8, jährlich mit Datum 28, wöchentlich mit Tag 1.
Die wöchentliche Probe spricht dafür, dass auch der Tag am Monats-Target bleibt, das ist aber ein
Analogschluss. Belegen ließe es sich mit einem Target, das Roman in der YNAB-Oberfläche im Budget
Test von Hand anlegt, danach ein `PATCH` nur mit `goal_target`.

**Empfehlung:** BB sendet beim Ändern eines Targets nur die geänderten Felder, also beim Betrag
nur `goal_target`, setzt `goal_frequency` nur bei einem echten Wechsel der Art und warnt dort, dass
Tag und Datum verloren gehen, legt "jährlich mit Datum" in zwei Aufrufen an und prüft vor dem ersten
Schreiben in Romans Budget die Form "monatlich mit Tag" wie oben beschrieben.

## Budget Test nach dem Spike

Alle Zuweisungen stehen wieder auf 0, `to_be_budgeted` unverändert (2026-10: 0, ab 2026-11: -131500).
Dining out, Phone & Internet und Utilities sind wieder ohne Target. Vacation steht wieder bei
TB, target 0, Groceries wurde nur gelesen und behält als einziges Target einen Wochentag (day 6).

Geändert geblieben:

- **Transportation:** `goal_day` ist weg (vorher 6, jetzt leer), Folge der Probe zum Wechsel der Art.
  Nicht wiederherstellbar über die API. Damit gleich wie Entertainment seit Oktober.
- **Transportation, Vacation:** `goal_creation_month` steht auf 2026-11 (vorher leer).
- **Monatsfenster:** reicht jetzt bis 2027-02 (vorher 2026-12), `last_month` entsprechend weiter. Kein
  Weg zurück gefunden.

Romans Budget und die drei weiteren Budgets wurden nur gelesen.

## Quellen
- OpenAPI-Spec v1.88.0, `https://api.ynab.com/papi/open_api_spec.yaml` (Schema `SaveCategory`)
- Echte Aufrufe am 2026-11-02, Skripte und Rohdaten im Scratchpad, nicht im Repo
- Vorarbeit: `research/ynab-api-targets-zuweisung-2026-10-25.md`, Probe aus `budgeting-k7wmd`
