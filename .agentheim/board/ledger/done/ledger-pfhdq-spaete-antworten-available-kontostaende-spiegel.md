---
id: ledger-pfhdq
title: "Späte und gescheiterte Antworten im Kassenbuch: Available, Kontostände und Spiegel-Status tragen eine Lade-Nummer, ältere Antworten fallen weg, ein gescheitertes Nachladen der Kontostände bleibt mit Hinweis sichtbar (Components/Ledger/State.fs CategoriesLoaded, BalancesLoaded, MirrorStatusLoaded)"
status: done
type: bug
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-pz028]
tags: [bestandsreview, frontend, tests]
related_adrs: [0014, 0055]
related_research: []
prior_art: [ledger-ct3cx, ledger-kwwxz, ledger-vq2g8]
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G3 (Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`), Teil 2 von 4.
Teil 1 ist ledger-y8xrr (Liste behält Stand nach eigenen Aktionen), Teil 3 ledger-pz028 (Aufräumen), Teil 4
ledger-awf5b (Betragsfilter und Update-Neustart).

Drei Abrufe im Kassenbuch laden nach, ohne `Loading` zu setzen, und tragen keine Lade-Nummer
(`standards/frontend/overview.md` „Späte Antworten“):
- **Available**: nach einer Kategorieänderung schreibt `shiftAvailable` den Betrag lokal fort. Kommt danach die Antwort
  eines Kategorien-Abrufs an, der vorher gestartet war, setzt sie das alte Available zurück. Roman sieht in der Karte
  „Budget nach dem Wechsel“ und im Picker falsche Zahlen.
- **Kontostände**: `LoadBalances` lässt `Success` stehen. Scheitert das Nachladen (nach Anlegen, Löschen, Spiegel-Lauf),
  schluckt `RemoteData.lateFailure` den Fehler, weil `Success` steht. Die Leiste zeigt alte Stände ohne Hinweis. Eine
  ältere erfolgreiche Antwort kann zudem eine neuere überschreiben.
- **Spiegel-Status**: `LoadMirrorStatus` setzt `Loading`, „Gespiegelt …“ verschwindet bei jedem Laden kurz; ein Fehler
  ersetzt einen gelesenen Stand. Kommt eine ältere Antwort nach der neueren, steht die alte Zeit da, und ist sie älter als
  5 Minuten, startet ein unnötiger zweiter Spiegel-Lauf.

## What
- **Kategorien** (`Categories`, Antwort `CategoriesLoaded`, Abruf im Root `src/Client/State.fs:819`): neue Lade-Nummer
  `CategoriesGeneration` im Kassenbuch-Modell. Sie steigt bei jedem Start eines Abrufs (Root-Zweig `LoadCategories`)
  und bei jeder lokalen Fortschreibung von Available (jede Stelle, die `shiftAvailable` auf `Categories` anwendet,
  heute nur `State.fs:651`; der Sammelweg „Kategorie setzen“ schreibt nicht fort, er startet in 1171 einen Abruf).
  Läuft schon ein Abruf, ersetzt ein neuer Start ihn (seine Antwort fällt weg). `CategoriesLoaded` trägt die Nummer seines
  Starts; eine Antwort mit anderer Nummer setzt weder `Categories` noch `CategoriesLoadedAt`, beendet aber den laufenden
  Abruf, wenn sie zu ihm gehört.
- `CategoriesRefreshing: bool` (Types.fs:1516) entfällt zugunsten eines Zustands, der den laufenden Abruf samt Nummer
  trägt (etwa `CategoriesFetch: CategoriesIdle | CategoriesFetching of generation`, Vorbild `RefreshState`).
  `shouldReloadCategories` und `categoriesReloadDue` lesen ihn.
- `CategoriesLoaded (Error _)` (State.fs:2005) nimmt `RemoteData.lateFailure` statt der eigenen Kopie.
- **Kontostände** (`Balances`, `LoadBalances` State.fs:1633, `BalancesLoaded` 1647): Lade-Nummer `BalancesGeneration`,
  `BalancesLoaded` trägt sie. Eine ältere Antwort (Erfolg wie Fehler) fällt weg. Scheitert die aktuelle Antwort, während
  Stände stehen (`Success`), bleiben die Beträge stehen, und das Modell merkt sich den Fehler (neues Feld, etwa
  `BalancesRefreshError: string option`). Über der Kontostände-Leiste steht dann der Hinweis aus „Sichtbar“ unten. Die
  nächste erfolgreiche Antwort löscht ihn; ein neuer Start löscht ihn nicht (sonst blinkt er bei „Erneut laden“).
- `syncNavAccounts` (`src/Client/State.fs:536`) zieht Seitenleiste und Übersicht nur mit der aktuellen Antwort mit (die
  ältere fällt auch dort weg); ein gescheitertes Nachladen lässt dort die Stände wie heute stehen.
- **Spiegel-Status** (`LoadMirrorStatus` 1923, `MirrorStatusLoaded` 1932): Lade-Nummer `MirrorStatusGeneration`. Beim
  Start bleibt ein `Success` stehen (nur ohne Stand `Loading`), ein Fehler ersetzt einen gelesenen Stand nicht
  (`lateFailure`), eine ältere Antwort fällt weg und startet keinen Spiegel-Lauf.
- `RemoteData.lateFailure` (`src/Client/Types.fs:48`): der Kommentar „Jede Abfrage setzt vorher `Loading`“ stimmt nicht
  mehr. Neu formulieren: der Helfer lässt einen vorhandenen Stand bei einem Fehler stehen; ob der Fehler sichtbar wird,
  entscheidet der Aufrufer.

**Sichtbar** (Kontostände, von Roman entschieden 2026-11-19, Variante A): über der Kontostände-Leiste, an derselben Stelle wie
der Hinweis nach einem gescheiterten ersten Laden (`balancesNoticeView`, View.fs:945, auf allen Breiten über der Liste),
derselbe Baustein `ErrorDisplay.cardCompactWith` (Styleguide §5 ErrorDisplay, `cardCompact`) mit dem Text
„Kontostände konnten nicht aktualisiert werden“ und dem Knopf „Erneut laden“ (`LoadBalances`). Die Karten zeigen
weiter die alten Beträge. Der Text steht als Konstante neben `balancesFailedText` (View.fs:214); `balancesNotice`
liefert ihn aus dem neuen Zustand (reine Funktion, testbar unter .NET).

Entscheidungen Conductor 2026-11-19, nach Empfehlung:
- Eine Lade-Nummer je Abruf, kein gemeinsamer Zähler (Vorbild `HistoryGeneration`, `EntriesGeneration`).
- Der Spiegel-Status zeigt nach einem gescheiterten Nachlesen den alten Stand ohne Hinweis. „Gespiegelt …“ ist eine
  Auskunft, kein Geldbetrag; ein gescheiterter Lauf selbst meldet sich weiter per Toast und Lauf-Status (ADR 0014).
- Dass YNAB nach einer Kategorieänderung den Push noch nicht verarbeitet hat und ein danach gestarteter Abruf altes
  Available liefert, ist nicht Teil dieser Task (README „Available frisch halten“: der nächste Abruf überschreibt).

## Acceptance criteria
- [ ] Regressionstest, vor dem Fix rot (Expecto über das echte Root-`update` mit `ports.GetCategories` als Funktionswert,
  Datei `src/Tests/LedgerCategoriesFreshnessTests.fs`): GIVEN Kategorien geladen, Available von „Lebensmittel“ 100 €,
  ein Abruf gestartet (`LedgerMsg LoadCategories`), WHEN danach eine Kategorieänderung einer Ausgabe von 30 € auf
  „Lebensmittel“ im laufenden Monat bestätigt wird (`shiftAvailable` setzt 70 €) und dann die Antwort des Abrufs mit
  100 € kommt, THEN steht 70 €, `CategoriesLoadedAt` ist unverändert und kein Abruf läuft mehr. Intakt: ein nach der
  Änderung gestarteter Abruf, dessen Antwort 70 € oder 65 € bringt, gilt.
- [ ] Reihenfolgen Kategorien (Doktrin Punkt 20, Aufzählung nach Vorbild `moveWays` in `src/Tests/MoveMoneyClientTests.fs`):
  Nachrichten Abruf-Start A, Kategorieänderung (Fortschreibung), Abruf-Start B, Antwort A, Antwort B, je Antwort `Ok`
  oder `Error`, alle zulässigen Reihenfolgen (Antwort nach ihrem Start). Orakel, berechnet aus der Nachrichtenfolge:
  Available ist der Wert der letzten erfolgreichen Antwort, deren Start nach der letzten Fortschreibung lag, sonst der
  fortgeschriebene Wert; ein Fehler ändert keinen geladenen Stand; nach der letzten Antwort läuft kein Abruf.
- [ ] Regressionstest, vor dem Fix rot (Expecto über `update`, `src/Tests/LedgerPageTests.fs` bei den Kontostand-Tests):
  GIVEN Kontostände geladen, WHEN `LoadBalances` und danach die Antwort mit `Error "Http error (500)"`, THEN bleiben die
  Beträge stehen und `balancesNotice` liefert „Kontostände konnten nicht aktualisiert werden“ mit „Erneut laden“ und
  `LoadBalances`. Intakt: ein gescheitertes erstes Laden liefert weiter „Kontostände konnten nicht geladen werden“;
  die nächste erfolgreiche Antwort nimmt den Hinweis weg; ein erneuter Start lässt ihn bis zur Antwort stehen.
- [ ] Reihenfolgen Kontostände (Doktrin Punkt 20): zwei Starts, zwei Antworten je `Ok` (verschiedene Beträge) oder
  `Error`, alle zulässigen Reihenfolgen. Orakel: die Leiste zeigt die Beträge der letzten erfolgreichen Antwort auf den
  zuletzt gestarteten Abruf, sonst die vorher stehenden; der Hinweis steht genau dann, wenn die Antwort auf den zuletzt
  gestarteten Abruf gescheitert ist und Beträge stehen. Dieselbe Aufzählung prüft die Stände der Übersicht nach
  `syncNavAccounts` (Root-`update`).
- [ ] Regressionstest, vor dem Fix rot (Expecto über `update`): GIVEN Spiegel-Status „vor 1 Minute“ geladen, WHEN
  `LoadMirrorStatus`, THEN bleibt `LastMirrorRun` `Success` mit demselben Stand. WHEN danach die Antwort `Error`, THEN
  bleibt der Stand. Reihenfolgen (Doktrin Punkt 20): zwei Starts, Antwort A „vor 10 Minuten“, Antwort B „vor 1 Minute“,
  beide Reihenfolgen. Orakel: es steht die Antwort des zuletzt gestarteten Abrufs, und `RunMirror` steht genau dann im
  Cmd, wenn sie älter als 5 Minuten ist und kein Lauf läuft.
- [ ] `CategoriesRefreshing` gibt es nicht mehr (`grep -rn CategoriesRefreshing src/` leer); die Tests, die es setzen
  oder lesen (heute zehn Fundstellen in Client und Tests), ziehen auf den neuen Zustand um und bleiben inhaltlich gleich.
- [ ] README ledger: **Available frisch halten** (eine Antwort, die vor der letzten lokalen Fortschreibung gestartet
  war, fällt weg) und **Kontostand** (Absatz zur Kontostände-Leiste: nach einem gescheiterten Nachladen bleiben die
  Stände mit dem Hinweis „Kontostände konnten nicht aktualisiert werden“ und „Erneut laden“) nachziehen. Delta vor der
  Integration durch `npm run check:readme-delta`.
- [ ] Kein Skizzenvergleich: es gibt keine Skizze; Ort und Baustein sind die des bestehenden Hinweises.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Urteile (geprüft an HEAD f98a8ce6):
- Befund 2 (State.fs:1997 `CategoriesLoaded` ohne Lade-Nummer): bestätigt. Verschärft durch `State.fs:1171`: der
  Sammelweg „Kategorie setzen“ schickt `LoadCategories`, und der Root-Zweig (`src/Client/State.fs:819`) prüft nicht, ob
  schon ein Abruf läuft; es können zwei Abrufe zugleich laufen.
- Befund 3 (State.fs:1650 `LoadBalances` lässt `Success`, `lateFailure` schluckt): bestätigt, Zeilen 1633 bis 1651.
  Angepasst: `lateFailure` steht in `src/Client/Types.fs:48`, nicht in `Components/Ledger/Types.fs`. Dazu fehlt eine
  Lade-Nummer, eine ältere erfolgreiche Antwort kann die neuere überschreiben (mehrere `LoadBalances` nach Anlegen,
  Spiegel-Lauf und Löschen kurz hintereinander).
- Befund 4 (State.fs:1923 `LoadMirrorStatus` setzt `Loading`): bestätigt (1930), `Error` überschreibt in 1947.
  `lastRunSubtitle` (View.fs:967) zeigt bei `Loading` nichts. Ergänzt: die fehlende Lade-Nummer kann einen zweiten
  Spiegel-Lauf auslösen.
- Befund 8 (State.fs:2005 `lateFailure` inline): bestätigt, gleichwertig zum Helfer; hier, weil derselbe Zweig ohnehin
  umgebaut wird.
- Befund 10, Anteil `CategoriesRefreshing` (Types.fs:1516): bestätigt, hier mit erledigt. `LoadingMore` geht in
  ledger-pz028, `MirrorRunning` entfällt (siehe dort).
- Weitere Antworten mit `lateFailure` nach einem Nachladen ohne `Loading`: `AccountsRefreshed` (1625),
  `LenaFlagSettingsLoaded` (2685), `PayeeChoices` (3148). Sie lassen einen Fehler still stehen; Konten und Flag-Farbe
  ändern sich selten, keine Geldzahl. Nicht Teil dieser Task; bei Bedarf eine Zeile in der Kann-Sammlung.

Entschieden (Roman, 2026-11-19): jeweils die Empfehlung.
- Wie sollen Kontostände nach einem gescheiterten Nachladen aussehen?
  A) alte Beträge bleiben, darüber der rote Hinweis „Kontostände konnten nicht aktualisiert werden“ mit „Erneut laden“
  (derselbe Baustein und Ort wie heute beim ersten Laden, nur ein anderer Satz).
  B) wie beim ersten Laden: Beträge werden zum Strich, Hinweis „Kontostände konnten nicht geladen werden“. Klar, aber
  die zuletzt bekannten Zahlen sind weg.
  C) alte Beträge bleiben, nur ein Toast (Warnung) „Kontostände konnten nicht aktualisiert werden: <Grund>“. Nach fünf
  Sekunden ist der Hinweis weg, die Zahlen stehen wieder ohne Hinweis da.
  Empfehlung A. Roman hat A gewählt.

## Outcome

Drei Abrufe im Kassenbuch tragen jetzt je eine eigene Lade-Nummer (eine je Abruf, Vorbild `HistoryGeneration`).

- **Kategorien**: `CategoriesGeneration` steigt bei jedem Start (Root-Zweig `LedgerMsg LoadCategories` in `src/Client/State.fs`) und bei jeder Fortschreibung durch `shiftAvailable` (`applyCategoryResult` in `src/Client/Components/Ledger/State.fs`). `CategoriesRefreshing` ist ersetzt durch `CategoriesFetch = CategoriesIdle | CategoriesFetching of generation` (`src/Client/Components/Ledger/Types.fs`). Ein neuer Start ersetzt den laufenden Abruf. `CategoriesLoaded of int * Result` setzt Stand und `CategoriesLoadedAt` nur bei passender Nummer und beendet den Abruf nur, wenn sie zu ihm gehört. Der Fehlerzweig nimmt `RemoteData.lateFailure`. `grep -rn CategoriesRefreshing src/` ist leer.
- **Kontostände**: `BalancesGeneration` und `BalancesLoaded of int * Result`. Eine ältere Antwort fällt weg, Erfolg wie Fehler. Scheitert die aktuelle Antwort, während Stände stehen, bleiben die Beträge und `BalancesRefreshError` merkt sich den Grund. Ein neuer Start löscht ihn nicht, die nächste erfolgreiche Antwort schon. `balancesNotice balances refreshError` (`src/Client/Components/Ledger/View.fs`) liefert dann `balancesRefreshFailedText` „Kontostände konnten nicht aktualisiert werden“ mit „Erneut laden“ (`LoadBalances`), über `ErrorDisplay.cardCompactWith` an derselben Stelle wie der Hinweis zum ersten Laden. `syncNavAccounts` bekommt das Kassenbuch-Modell vor der Nachricht und lässt ältere Antworten auch für Seitenleiste und Übersicht weg.
- **Spiegel-Status**: `MirrorStatusGeneration` und `MirrorStatusLoaded of int * Result`. Beim Start bleibt ein `Success` stehen (nur ohne Stand `Loading`), ein Fehler ersetzt den gelesenen Stand nicht (`lateFailure`), eine ältere Antwort fällt weg und startet keinen `RunMirror`. Der Aufruf liegt jetzt hinter einem Lambda, damit `update` unter .NET läuft (vorher warf der Proxy schon im Reducer).
- `RemoteData.lateFailure` (`src/Client/Types.fs`): Kommentar neu formuliert (der Helfer lässt einen Stand stehen, ob der Fehler sichtbar wird, entscheidet der Aufrufer).

Tests (Expecto):
- `src/Tests/LedgerCategoriesFreshnessTests.fs`, Liste „späte Antworten der Kategorien (ledger-pfhdq)“: Regression über das echte Root-`updateWith` mit `GetCategories` als Port (Antwort ist die Nachricht, die der Port wirklich schickt). Vor dem Fix rot (`expected Some 70M, actual Some 100M`). Dazu der Intakt-Fall (nach der Änderung gestartet, 70 € und 65 €) und die Aufzählung aller 15 zulässigen Reihenfolgen von Start A, Fortschreibung, Start B, Antwort A, Antwort B mit je `Ok`/`Error` (60 Läufe), Orakel aus der Nachrichtenfolge.
- `src/Tests/LedgerPageTests.fs`: Regression Kontostände (vor dem Fix rot: kein Hinweis) samt Intakt-Fällen (erstes Laden bleibt bei „konnten nicht geladen werden“, Erfolg nimmt den Hinweis weg, ein neuer Start lässt ihn stehen). Regression Spiegel-Status (vor dem Fix rot: der Reducer setzte `Loading` und warf unter .NET am Proxy). Liste „Reihenfolgen der Kontostände und des Spiegel-Status“: drei zulässige Reihenfolgen zweier Starts und Antworten mal vier Ergebnis-Paare über das Root-`update` (Leiste und Übersicht nach `syncNavAccounts`, Hinweis genau nach gescheiterter letzter Antwort), Spiegel-Status mit zwei Starts, beiden Antwort-Reihenfolgen und beiden Zuordnungen von „vor 10 Minuten“/„vor 1 Minute“ (RunMirror genau, wenn die Antwort des zuletzt gestarteten Abrufs älter als 5 Minuten ist).
- Bestehende Tests auf die neuen Signaturen umgezogen, inhaltlich gleich: `LedgerCategoriesFreshnessTests`, `LedgerCategoriesAwaitSettingsTests`, `LedgerPageTests`, `LedgerViewTests`, `OverviewTests`, `LedgerBackgroundRefreshTests`.
- Layout-Sweep: neuer Zustand `kassenbuch-kontostaende-veraltet` (412 und 1280 px) in `scripts/layout-states.mjs`. Erste Antwort der Stände kommt, der automatische Spiegel-Lauf endet, das Nachladen scheitert mit 500. Keine Verletzung.

Bilder für Romans Blick (aus dem Sweep-Zustand): /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-pfhdq/kontostaende-veraltet-412.png und /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-pfhdq/kontostaende-veraltet-1280.png. Die alten Beträge stehen (Seitenleiste und Kontofeld), darüber der rote Hinweis mit „Erneut laden“.

Kein Skizzenvergleich: es gibt keine Skizze, Ort und Baustein sind die des bestehenden Hinweises.

README-Delta (Kontostand, Available frisch halten) mit `npm run check:readme-delta -- --bc ledger` geprüft: Exit 0, „Ganz nachlesen: keine Einträge“, kein `--keep`.

Kein ADR: die Lade-Nummer folgt dem Standard „Späte Antworten“ (`standards/frontend/overview.md`) und dem Vorbild `HistoryGeneration`. Die sichtbare Variante A hat Roman am 2026-11-19 in der Task entschieden, sie steht im README.

Orakel der Kategorien-Reihenfolgen: „die letzte erfolgreiche Antwort, deren Start nach der letzten Fortschreibung lag“ ist so gelesen, dass nach ihrem Start auch kein neuer Start kam. Das folgt aus der Regel „ein neuer Start ersetzt den laufenden Abruf“ in „What“.

`./verify.sh`: VERIFY: PASS (dotnet 5189 grün, node 351 grün, dotnet build, npm run build, guard:classes, guard:tokens grün).
