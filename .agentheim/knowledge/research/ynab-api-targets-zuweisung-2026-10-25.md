---
topic: YNAB-API: Targets lesen, Fehlbetrag, Zuweisen schreiben, Verfügbar pro Monat, Targets schreiben
date: 2026-10-25
requested_by: model
related_tasks: [budgeting-r4tkq]
---

# Research: YNAB-API für "Targets anwenden"

## Frage
Kann BB die Targets aus YNAB lesen und den zugewiesenen Betrag pro Kategorie und Monat
nach YNAB schreiben (Stufe 1 des Budgets, ADR 0063)? Fünf Teilfragen: Targets lesen,
Fehlbetrag, Zuweisen schreiben, Verfügbar und Vortrag, Targets schreiben.

## Methode
OpenAPI-Spec v1.87.0 (`https://api.ynab.com/papi/open_api_spec.yaml`), dazu echte Aufrufe
mit `YNAB_TOKEN`. Lesend gegen Romans Budget "My Budget" (nur GET). Schreibend nur gegen das
Wegwerf-Budget "Test" (wie ADR 0058). Skripte und Rohdaten liegen im Scratchpad
(`spike-r4tkq/`), nicht im Repo.

Pfade: `/v1/budgets/{id}/...` (BBs bisheriger Code) und `/v1/plans/{id}/...` (Spec) liefern
beide Treffer, auch beim PATCH. Kein Umbau nötig.

## Ergebnis in Kürze

| Frage | Antwort |
|---|---|
| 1 Targets lesen | ja, mit einer Einschränkung bei "auffüllen bis" (siehe unten) |
| 2 Fehlbetrag von YNAB | ja: `goal_under_funded` pro Kategorie und Monat |
| 3 Zuweisen schreiben | ja, belegt: absolut, pro Kategorie und Monat, `to_be_budgeted` zieht sofort nach |
| 4 Zugewiesen, Aktivität, Verfügbar | ja: `budgeted`, `activity`, `balance` pro Kategorie und Monat |
| 5 Targets schreiben | teilweise: nur "Plan Your Spending" (NEED) und Löschen, belegt |

## 1. Targets lesen

Endpunkte: `GET /budgets/{id}/categories` (aktueller Monat), `GET /budgets/{id}/months/{month}`
(alle Kategorien dieses Monats im Feld `month.categories`), `GET /budgets/{id}/months/{month}/categories/{category_id}`.
Dieselben `goal_*`-Felder in allen drei.

Felder (Beträge in Milliunits): `goal_type` (TB, TBD, MF, NEED, DEBT, null),
`goal_needs_whole_amount`, `goal_cadence`, `goal_cadence_frequency`, `goal_day`,
`goal_target`, `goal_target_date`, `goal_creation_month`, `goal_snoozed_at`, dazu die
berechneten Felder aus Frage 2. `goal_target_month` ist veraltet.

Kadenz: `goal_cadence` 0 keine, 1 monatlich, 2 wöchentlich, 13 jährlich, 3 bis 12 alle
2 bis 11 Monate, 14 alle 2 Jahre; bei 0, 1, 2, 13 gilt `goal_cadence * goal_cadence_frequency`.
`goal_day` ist bei wöchentlich der Wochentag (0 Sonntag), sonst der Tag im Monat.

Zuordnung von Romans vier Arten, belegt an seinem echten Budget:

| Romans Art | Erkennung | Beleg in "My Budget" |
|---|---|---|
| Pro Woche ein Betrag | `goal_type=NEED`, `goal_cadence=2`, `goal_target` = Betrag pro Woche, `goal_day` = Wochentag | Haushaltshilfe: NEED, cadence 2, frequency 1, day 1 (Montag), target 50000 |
| Jeden Monat ein fester Betrag | `goal_type=NEED`, `goal_needs_whole_amount=true`, `goal_cadence=1`, `goal_cadence_frequency=1`, ohne `goal_target_date` | Essen/Einkaufen 1600000; Essen Arbeit 100000 (dazu 40 weitere) |
| Auffüllen bis | `goal_type=NEED`, `goal_needs_whole_amount=false` ("Refill up to") | Spiegel (cadence 1, day 1, 15960). Roman nutzt es laut Aufgabe nicht, die API hat es trotzdem |
| Bis zu einem Datum ansparen | `goal_type=NEED` mit `goal_target_date` und Jahres-/Mehrmonats-Kadenz (cadence 13, 1/3 usw.), bei neueren Targets; klassisch `TBD` | Auto: Steuern 330000 bis 2026-11-01 (cadence 13); Wohngebäudeversicherung 750000 (13, needs_whole=false). Kein Eintrag mit `TBD` im Budget |

Weitere Formen in Romans Budget: `MF` (Monthly Funding, ältere Monatsziele, 18 Kategorien,
teils mit `goal_target_date` als bloßem Startdatum aus 2019, teils ganz ohne Kadenz), `NEED`
mit `needs_whole_amount=null` (8, jährlich). `TB` ("Target Category Balance", ein Deckel auf
den Kontostand) kommt bei ihm nicht vor, existiert aber in der API und im Test-Budget
(`Emergency fund`, `Vacation`, Target 0).

Ein Target mit Deckel gibt es als `TB` (Zielstand) und als "Refill up to" (NEED, `needs_whole_amount=false`).
Die genaue Deckel-Semantik muss BB nicht nachbauen, solange `goal_under_funded` übernommen wird.

Einschränkung: Die Art ist nicht an einem einzelnen Feld ablesbar. `goal_type` allein
unterscheidet "pro Woche", "monatlich fest" und "bis Datum" nicht (alle `NEED`). Erst die
Kombination aus `goal_cadence`, `goal_cadence_frequency`, `goal_needs_whole_amount` und
`goal_target_date` trennt sie. Das ist für Targets anwenden nicht nötig (Frage 2), wohl aber
für eine Anzeige der Art und für Stufe 3.

## 2. Was YNAB schon ausrechnet

Ja. Jede Kategorie in `month.categories` trägt `goal_under_funded` (Betrag, der in diesem
Monat noch zugewiesen werden müsste, um auf Kurs zu bleiben), dazu `goal_overall_funded`,
`goal_overall_left`, `goal_percentage_complete`, `goal_months_to_budget`. Laut Spec
entspricht `goal_under_funded` dem "Underfunded" der Clients.

Beobachtungen am echten Budget:

- Haushaltshilfe, Oktober 2026: target 50000/Woche, day=Montag, `budgeted` 0, `goal_under_funded` 200000
  (Montage am 5., 12., 19., 26. = 4 Wochen). Für 2026-11-01 liefert dieselbe Abfrage 250000 (5 Montage). Das Feld gilt für den abgefragten Monat und
  rechnet die Wochen selbst.
- Test-Budget, Entertainment (NEED weekly, Samstag), Target nach PATCH auf 50000: `goal_under_funded`
  250000 (5 Samstage im Oktober).
- Für nicht unterfinanzierte Kategorien steht 0, für Kategorien ohne Target `null`.
- `goal_under_funded` ist kein Delta zum aktuellen `budgeted`: Es ist der Betrag, der zusätzlich zugewiesen werden muss (Haushaltshilfe: budgeted 0, under_funded 200000, also neuer Zuweisungsstand
  `budgeted + goal_under_funded`). Gleichung `neues budgeted = budgeted + goal_under_funded` ist
  nach Spec-Text plausibel, am echten Budget aber nur mit `budgeted=0` geprüft (offen: Fall mit
  teilweise zugewiesenem Betrag, beim ersten Feature gegen "Test" prüfen).
- Achtung: Haushaltshilfe trägt `goal_snoozed_at` (2026-10-20) und liefert trotzdem 200000.
  YNABs Oberfläche blendet snoozed Targets aus der "Underfunded"-Summe aus. BB muss entscheiden,
  ob es `goal_snoozed_at != null` überspringt.
- Summe `goal_under_funded` über alle Kategorien im aktuellen Monat: 250000.

## 3. Zuweisen schreiben (Probelauf)

Endpunkt: `PATCH /budgets/{id}/months/{month}/categories/{category_id}`, Body
`{"category":{"budgeted":<Milliunits>}}`. Pflichtfeld nur `budgeted`. `month` als
`YYYY-MM-01` oder `current`.

- Absolut, nicht Delta: Der Wert ersetzt den bisherigen Zuweisungsstand.
- Eine Kategorie und ein Monat pro Aufruf. In der Spec gibt es keinen Sammelaufruf für Zuweisungen
  (nur Kategorie-Einzel-PATCH). Für alle Kategorien am Monatsersten sind also N Aufrufe nötig (Romans Budget: 167 Kategorien im Monat,
  davon rund 100 mit Target); das Rate-Limit von YNAB (nach Dokumentation 200 Anfragen pro Stunde und Token) ist dabei knapp
  und muss berücksichtigt werden (nur Kategorien mit `goal_under_funded > 0` schreiben).
- Test-Budget, Kategorie "Groceries" (`6cd3482b-...`), Monat 2026-10-01, Ausgangszustand `budgeted=0`, `to_be_budgeted=0`:

| Schritt | Request-Body | Status | Antwort (Kategorie) | `to_be_budgeted` danach |
|---|---|---|---|---|
| Vorher | | | budgeted 0, balance 0 | 0 |
| 1 | `budgeted: 12345` | 200 | budgeted 12345, balance 12350 | -12350 |
| 2 | `budgeted: 5000` (zweiter Absolutwert) | 200 | budgeted 5000, balance 5000 | -5000 (also kein Delta addiert) |
| 3 | `budgeted: 0` (Rücksetzen) | 200 | budgeted 0 | 0 |
| Nov 1 | Monat 2026-11-01: `budgeted: 7000` | 200 | budgeted 7000 | Nov: -131500 auf -138500 (-7000), Okt unverändert 0 |
| Nov 2 | Monat 2026-11-01: `budgeted: 0` | 200 | budgeted 0 | Nov zurück auf -131500 |
| Pfad | `/plans/{id}/months/2026-10-01/categories/{id}` mit 1000, danach 0 | 200 | | 0 |

Wirkung auf "Zu verteilen": `to_be_budgeted` des betroffenen Monats sinkt um genau den
zugewiesenen Betrag (und steigt beim Zurücksetzen), gelesen über `GET /months/{month}`.
Zuweisen in einen späteren Monat verändert dessen `to_be_budgeted`, nicht den des Vormonats.
Der Wert im Test-Monat November war schon vorher negativ (-131500, Folge von Testdaten, nicht
von diesem Probelauf).

Stolperstein: 12345 Milliunits wurden zu 12350 in `balance` und `to_be_budgeted`
(Rundung auf Cent, EUR hat 2 Nachkommastellen) bei `budgeted` bleibt 12345 stehen. BB sollte nur auf 10 Milliunits
gerundete Beträge schreiben, `goal_under_funded` liefert solche Werte.

Rücksetzen: Groceries steht in beiden Monaten wieder auf `budgeted=0`, `to_be_budgeted` Okt 0, Nov -131500
(wie vorher), per GET bestätigt. Romans Budget wurde nie beschrieben.

## 4. Verfügbar und Vortrag

Ja. `GET /budgets/{id}/months/{month}` liefert pro Kategorie `budgeted` (Zugewiesen),
`activity` (Aktivität) und `balance` (Verfügbar, mit Vortrag aus Vormonaten, YNAB rechnet
ihn). Der Monat selbst trägt `income`, `budgeted`, `activity`, `to_be_budgeted`, `age_of_money`.
Der bereits angebundene Endpunkt `months/current` (`monthToBeBudgetedDecoder` in
`src/Server/YnabClient.fs`) liefert diese Daten schon mit, der Decoder liest nur `to_be_budgeted`.
Zusätzlich gibt es `GET /months/{month}/money_movements` und `money_movement_groups` (Geld
verschieben, für Stufe 1 relevant), die dieser Spike nicht geprüft hat.
Folge: BB kann Verfügbar und Vortrag übernehmen statt selbst zu rechnen; Selbst-Rechnen wird erst
nötig, wenn YNAB abgelöst wird.

## 5. Targets schreiben (nur festgestellt)

Teilweise. `PATCH /budgets/{id}/categories/{category_id}` und `POST /categories` akzeptieren
`goal_target` (Milliunits, `null` löscht das Target), `goal_target_date`,
`goal_needs_whole_amount` (NEED: true "Set aside another", false "Refill up to") und
`goal_frequency` (monthly, weekly, yearly). Damit lassen sich nur Targets vom Typ
NEED anlegen (Spec: ohne `goal_type` entsteht NEED, bei Kreditkartenzahlungs-Kategorien MF).
TB, TBD und MF sind nicht direkt schreibbar, keine Wahl von `goal_day`/`goal_cadence` außer
über `goal_frequency`.

Probelauf Test-Budget, Entertainment (NEED weekly Samstag, Target 0): `goal_target: 50000,
goal_frequency: weekly` gab 200, Target 50000, `goal_under_funded` 250000. `goal_target: null` gab 200 und entfernte das
Target ganz. Wiederherstellen per `goal_target: 0, goal_frequency: weekly,
goal_needs_whole_amount: true` gab 200 (Target 0, weekly); `goal_day` kam dabei nicht
zurück (vorher 6, danach leer). Das Test-Budget ist nicht exakt im Ursprungszustand, für die
Messung ohne Folgen.

## Empfehlung: Zuschnitt von "Targets anwenden"

Fehlbetrag von YNAB übernehmen, nicht in BB berechnen.

1. Pro Monat ein `GET /months/{month}` lesen. Daraus pro Kategorie `goal_under_funded`,
   `budgeted`, `goal_snoozed_at`.
2. Für jede Kategorie mit `goal_under_funded > 0` (und nicht snoozed, Entscheidung offen)
   `PATCH .../months/{month}/categories/{id}` mit `budgeted = budgeted + goal_under_funded`.
   Danach den Monat neu lesen und `to_be_budgeted` sowie die Summe der Zuweisungen anzeigen.
3. Reicht "Zu verteilen" nicht, vor dem Schreiben warnen (YNAB lässt negatives
   `to_be_budgeted` zu, wie der Probelauf zeigt).
4. Das ist deutlich weniger als eine eigene Berechnung, die Wochen zählen (Haushaltshilfe: 4 oder 5
   Montage), Jahres-Targets mit Datum und `TB`-Deckel nachbauen müsste.

Offen für das Feature: Rechengleichung bei teilweise zugewiesener Kategorie prüfen, Umgang mit
Snooze, Rate-Limit bei vielen Kategorien, Rundung auf Cent. Selbst-Berechnen ist erst für
Stufe 3 (Targets in BB pflegen) nötig.

## Quellen
- OpenAPI-Spec v1.87.0, `https://api.ynab.com/papi/open_api_spec.yaml` (Schemas `Category`, `SaveCategory`, `SaveMonthCategory`, Pfad `/plans/{plan_id}/months/{month}/categories/{category_id}`)
- Echte Aufrufe am 2026-10-25, Rohdaten im Scratchpad `spike-r4tkq/` (`cats-mybudget.json`, `month-current.json`, Skripte `p1.py` bis `p6.py`)
