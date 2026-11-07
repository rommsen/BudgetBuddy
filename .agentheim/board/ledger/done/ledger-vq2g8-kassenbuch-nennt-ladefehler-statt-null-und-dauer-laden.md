---
id: ledger-vq2g8
title: "Kassenbuch zeigt bei gescheitertem Abruf 0,00 € oder lädt endlos; es soll Laden und Fehler sichtbar nennen"
status: done
type: bug
context: ledger
created: 2026-11-07
completed:
depends_on: []
blocks: []
tags: [ledger, client, remote-data, kontostand, fehlerzustand, bestandsreview]
related_adrs: [0056, 0049]
related_research: []
prior_art: [ledger-k9tq7, ledger-dmw83, ledger-b1xwe, ledger-xfjcp]
---

## Why
Bestandsreview ledger vom 2026-11-06, Gruppe 3 (`.agentheim/knowledge/reviews/2026-11-06-ledger.md`, Abschnitt 3, höchste Schwere hoch). Roman hat am 2026-11-07 entschieden, die Gruppe aufzunehmen, ohne den Rundungsbefund (siehe Notes).

Was heute passiert:
- **Kontostände 0,00 €.** Bricht der Abruf der Kontostände ab (etwa im schwachen WLAN am Handy), zeigt die Leiste für jedes Konto und für "Alle Konten" 0,00 €. Roman hält das für einen echten Stand. Ursache: `src/Client/Components/Ledger/View.fs:3359` macht aus `model.Balances` per `RemoteData.toOption |> Option.defaultValue []` eine leere Liste, `buildAccountCards` (`View.fs:164`) setzt dann je Konto Null. Ein Test schreibt das sogar fest (`src/Tests/LedgerViewTests.fs`, "Ein Konto ohne geladenen Stand zeigt Null, keinen Fehler").
- **Kategorien laden endlos.** Sind die Settings da, aber ohne YNAB oder ohne Standard-Budget, und wartet das Kassenbuch auf sie (`CategoriesAwaitSettings`), bleibt `Ledger.Categories` für immer `Loading`: `src/Client/State.fs:736` und `:737` geben das Modell unverändert zurück. Der Weg über `SettingsLoaded` (`State.fs:634`, `:643`) endet im selben Zweig. Die Kategorienauswahl dreht sich.
- **"Lena fragen" nennt den falschen Grund.** Scheitert das Laden der Lena-Einstellung, verwirft `LenaFlagSettingsLoaded (Error _)` (`src/Client/Components/Ledger/State.fs:1854`) den Fehler. Die Aktion ist gesperrt mit "In den Settings die Flag-Farbe für Lena wählen", obwohl die Farbe gewählt ist.
- **Übertrag-Ziele fehlen still.** `transferTargetsFor` (`View.fs:1735`) liefert bei `TransferTargets = Failure` eine leere Liste; im Split-Editor gibt es dann keine Übertrag-Ziele und keinen Hinweis.
- **Englisches Zahlenformat in den Settings.** Die Auswahl des Quick-Add-Kontos zeigt den Kontostand per `sprintf "%s (%.2f %s)"` als "Bar (1234.50 EUR)" (`src/Client/Components/Settings/View.fs:297`).

## What
Am Code vom 2026-11-07 (main 7423fb0) geprüft.

1. **Kontostand-Leiste**: Die Karten bekommen einen Zustand je Betrag statt eines erfundenen Nullwerts: geladen (Betrag), lädt (Platzhalter), gescheitert (kein Betrag, Hinweis). Die Entscheidung steckt in einer reinen Funktion (Umbau von `buildAccountCards` oder eine neue daneben); `accountStrip` (`View.fs:1000`) und die Handy-Leiste (`View.fs:3312`) rufen sie nur auf. Bei Fehler ein Hinweis mit "Erneut laden" (`LoadBalances`). Aussehen siehe "Entscheidung Roman". Nur Bausteine aus `src/Client/DesignSystem/` (etwa `Loading.skeleton`, `ErrorDisplay`), Werte aus `standards/frontend/styleguide.md`.
2. **Kategorien ohne YNAB-Einrichtung**: In beiden Zweigen von `State.fs:736`/`:737` wird `Ledger.Categories` zu `Failure` mit konkretem Satz (Vorschlag: "YNAB ist in den Settings nicht eingerichtet" bzw. "Kein Standard-Budget in den Settings gewählt") und `CategoriesAwaitSettings = false`. Der Picker zeigt Failure schon heute (ledger-dmw83).
3. **Lena-Einstellung**: Der Ladefehler wird ein eigener Zustand (`RemoteData` oder gleichwertig), und `lenaActionFor` (`src/Client/Components/Ledger/Types.fs:516`) und die Auswahl-Leiste (`Types.fs:1299`) nennen dann einen eigenen Grund (Vorschlag: "Die Lena-Farbe konnte nicht geladen werden"). Fehlt die Farbe wirklich, bleibt `lenaColorMissingReason`.
4. **Übertrag-Ziele**: Bei `Failure` zeigt der Übertrag-Picker im Split-Editor einen Hinweis (Vorschlag: "Übertrag-Ziele konnten nicht geladen werden"), bei `Loading` den Ladezustand des Pickers. Die Entscheidung als reine Funktion neben `transferTargetsFor`.
5. **Settings**: Der Kontostand in der Konto-Auswahl nutzt `Money.formatAmountText` (`src/Client/DesignSystem/Money.fs:115`), also "1.234,50 €".

## Acceptance criteria
- [x] Kaputt (Kontostände), reine Funktion unter .NET: GIVEN `Balances = Failure`, WHEN die Karten gebildet werden, THEN trägt keine Karte einen Betrag 0,00 €, sondern den Zustand "gescheitert". Vor dem Fix rot. Der bisherige Test "Ein Konto ohne geladenen Stand zeigt Null, keinen Fehler" wird ersetzt, der Kommentar des neuen Tests nennt den Bug.
- [x] Kaputt (Kontostände): GIVEN `Balances = Loading` oder `NotAsked`, THEN Zustand "lädt" je Karte, kein Betrag. Vor dem Fix rot.
- [x] Intakt: GIVEN `Balances = Success`, THEN "Alle Konten" zuerst mit der Summe, danach jedes Konto mit seinem Stand (bestehender Test bleibt sinngemäß); ein Konto ohne Eintrag in einer erfolgreichen Antwort zeigt 0,00 € (ein Konto ohne Buchungen hat den Stand Null).
- [x] Client-`update` unter .NET: "Erneut laden" an der Leiste schickt `LoadBalances`, und `BalancesLoaded (Error _)` setzt `Balances = Failure` (Test, falls noch nicht vorhanden).
- [x] Kaputt (Kategorien), Client-`update` unter .NET: GIVEN `CategoriesAwaitSettings = true` und `Categories = Loading`, WHEN Settings `Success` ohne `Ynab` ankommen, THEN `Categories = Failure` mit Satz und `CategoriesAwaitSettings = false`. Ebenso für Settings mit YNAB, aber ohne `DefaultBudgetId`. Beide vor dem Fix rot.
- [x] Intakt (Kategorien): Settings mit YNAB und Budget starten den Abruf wie bisher; `SettingsLoaded (Error _)` beim Warten ergibt weiter `Failure "Einstellungen konnten nicht geladen werden"` (bestehende Tests in `LedgerCategoriesAwaitSettingsTests.fs` grün).
- [x] Kaputt (Lena), reine Funktion und `update` unter .NET: GIVEN die Lena-Einstellung ist gescheitert, THEN nennt `lenaActionFor` den Ladefehler, nicht `lenaColorMissingReason`. Vor dem Fix rot. Intakt: ohne gespeicherte Farbe bleibt `lenaColorMissingReason`; mit Farbe ist "Lena fragen" frei; "Rückfrage zurücknehmen" braucht die Farbe weiter nicht (bestehende Tests in `LedgerLenaFlagClientTests.fs` grün).
- [x] Kaputt (Übertrag-Ziele), reine Funktion unter .NET: GIVEN `TransferTargets = Failure`, THEN liefert die Entscheidung den Fehlerhinweis statt einer stillen leeren Liste. Vor dem Fix rot. Intakt: `Success` liefert die offenen Budget-Konten ohne das eigene Konto wie bisher.
- [x] Settings: der Kontostand in der Konto-Auswahl wird mit `Money.formatAmountText` gebildet; Test an der Formatierungsstelle (reine Funktion, etwa "Bar (1.234,50 €)" aus erfundenen Werten), vor dem Fix rot.
- [x] Die Views rufen die reinen Funktionen nur auf (Doktrin Punkt 1); kein neuer Quelltext-Test und kein Browser-Test für diese Logik.
- [x] Keine Änderung an Records in `src/Shared` (die Typen liegen im Client). Falls doch nötig: Fixtures in `scripts/*.test.mjs` im selben Schritt nachgezogen.
- [x] README ledger: Bullet zur Kontostand-Leiste (Kontostand, ledger-b1xwe) nennt Lade- und Fehlerzustand statt Null; README-Delta mit `npm run check:readme-delta -- --bc ledger` geprüft.
- [x] Screenshots der Leiste am PC und am Handy in den Zuständen "lädt" und "gescheitert" im Outcome (API per `page.route` gemockt oder `npm run screenshot:route`), Darstellung nur aus DS-Bausteinen.
- [x] `./verify.sh` zeigt `VERIFY: PASS`.

## Entscheidung Roman (2026-11-07)
Roman wählte am 2026-11-07 A (AskUserQuestion): Die Karten bleiben als Kontofilter stehen, Platzhalter beim Laden, Strich bei Fehler, darüber eine Zeile mit "Erneut laden".

Die Optionen, wie sie zur Wahl standen:
**Wie sieht die Kontostand-Leiste aus, solange die Stände laden oder wenn der Abruf scheitert?**

- A: Die Karten bleiben mit Kontonamen stehen und lassen sich weiter als Kontofilter antippen. Beim Laden steht statt des Betrags ein Platzhalter, bei Fehler ein Strich an der Stelle des Betrags, darüber eine Zeile "Kontostände konnten nicht geladen werden" mit "Erneut laden".
- B: Bei Fehler ersetzt eine einzelne Fehlerzeile mit "Erneut laden" die ganze Leiste; die Kontowahl ist bis zum nächsten Laden weg.

Empfehlung: A. Die Karten sind am PC und am Handy auch der Kontofilter; mit B kann Roman bei einem Netzaussetzer das Konto nicht wechseln, obwohl die Buchungsliste geladen ist.

## Nicht dazu
- **Rundung der Geldanzeige im Fable-Kompilat** (`src/Client/DesignSystem/Money.fs:126`, `src/Client/Types.fs:385`, `src/Client/Components/Ledger/SplitEditor.fs:45`): wird die eigene Task `infrastructure-k84s2` für Budget und Kassenbuch gemeinsam. Diese Task stellt die Settings nur auf `Money.formatAmountText` um; die Rundung darin behebt k84s2.
- **Kategorie-Vorschläge im Picker** (`Components/Ledger/State.fs:1712`): Ein Fehler lässt den Abschnitt weg, das ist bewusst so, am Code kommentiert und in der README (Kategorie ändern im Detail) beschrieben. Kein Umbau.
- Fehlertexte vom Server (Stacktrace, YNAB-Rohtext): infrastructure-vp308.
- Trefferflächen und Farbrollen (Gruppe 6, Kann-Sammlung ledger-k4n2s, Punkt 1).

## Notes
- Keine Skizze; Aussehen nach Styleguide und der Antwort auf "Entscheidung Roman". `design-quality` im Review-Lauf danach.
- Test-Ebene: reine Funktionen und Client-`update` unter .NET. Cmds mit `Client.Api` hinter ein Lambda (Doktrin Punkt 1), der Fable.Remoting-Proxy wirft unter .NET.
- Die Tests in `LedgerViewTests.fs` und `LedgerCategoriesAwaitSettingsTests.fs`, die diese Task anfasst, liegen auch im Aufräum-Lauf ledger-r8h19. Wer zuerst läuft, räumt die berührten Tests auf; der zweite gleicht ab.
- Keine echten Beträge oder Kontonamen in Tests, Doku oder Commits.

## Outcome

Fünf Stellen, an denen ein gescheiterter oder laufender Abruf still falsch aussah, nennen ihren Zustand jetzt selbst. Gebaut nach "Entscheidung Roman" Variante A.

**Kontostand-Leiste** (`src/Client/Components/Ledger/View.fs`): `AccountCard.Balance` ist jetzt `CardBalance = BalanceShown of Money | BalancePending | BalanceUnavailable`. `buildAccountCards` nimmt das `RemoteData` direkt: nur `Success` liefert Beträge (ein Konto ohne Eintrag hat dort weiter Stand Null), `Loading`/`NotAsked` ergibt "lädt", `Failure` ergibt "gescheitert". Die Karten bleiben in jedem Zustand als Kontofilter stehen. Die reine Funktion `balancesNotice` liefert bei `Failure` die Zeile "Kontostände konnten nicht geladen werden" mit "Erneut laden" (`Retry = LoadBalances`). `accountStrip`, Handy-Karten und Konto-Wechsler rufen beides nur auf (`cardBalanceElement`). DS-Bausteine: `Money.accountListRowWith` (Zeile mit eigenem Wert, `accountListRow` delegiert), `Money.balanceUnavailable` (gedämpfter Strich, aria-label "Kontostand nicht geladen"), `Money.accountBalanceFor` jetzt öffentlich, `Loading.amountSkeleton`, `ErrorDisplay.cardCompactWith` (eigene Knopf-Beschriftung, `cardCompact` delegiert). Alle in `/styleguide` (Money-Abschnitt) zu sehen, `standards/frontend/styleguide.md` nennt sie.

**Kategorien ohne YNAB-Einrichtung** (`src/Client/State.fs`): `ledgerCategoriesSetupGap` nennt den Grund ("YNAB ist in den Settings nicht eingerichtet", "Kein Standard-Budget in den Settings gewählt"). Beide Zweige in `LedgerMsg LoadCategories` und der Weg über `SettingsLoaded (Ok)` beim Warten setzen `Categories = Failure <Grund>` und `CategoriesAwaitSettings = false`.

**Lena-Einstellung** (`Components/Ledger/Types.fs`, `State.fs`): `Model.LenaFlagColor` ist jetzt `RemoteData<string option>` (`Success None` heißt: keine Farbe gewählt). `LoadLenaFlagSettings` setzt `Loading` (eine geladene Farbe bleibt), `LenaFlagSettingsLoaded (Error m)` setzt per `lateFailure` `Failure m`. `lenaColorBlockReason` entscheidet für `lenaActionFor` und `selectionLenaDisabledReason`: Ladefehler ergibt `lenaColorLoadFailedReason` ("Die Lena-Farbe konnte nicht geladen werden"), sonst ohne Farbe `lenaColorMissingReason`. "Rückfrage zurücknehmen" braucht die Farbe weiter nicht. `lenaColorOf` liefert die Farbe für den Flag-Zustand nach dem Befehl.

**Übertrag-Ziele**: `transferTargetsLoadState` neben `transferTargetsFor` macht aus `TransferTargets` einen `PickerLoadState`. `CategoryPickerProps` hat dafür das neue Feld `AccountsLoad`, das Layout `AccountsNotice` (`accountsNoticeFor`, gleiche Regel wie `noticeFor`). Der Abschnitt "Übertrag auf Konto" zeigt "Übertrag-Ziele werden geladen" mit Spinner oder "Übertrag-Ziele konnten nicht geladen werden"; die Kategorien bleiben. Alle anderen Picker-Aufrufer setzen `AccountsLoad = PickerReady`.

**Settings**: `Components.Settings.View.accountOptionLabel` bildet "Bar (1.234,50 €)" über `Money.formatAmountText`.

Keine Änderung an `src/Shared`.

**Tests** (12 neu, alle als Regressionstest kommentiert; per Gegenprobe vor dem Fix rot: die Mutanten "Failure wie Success []", "Failure-Zweig der Lena-Sperre entfernt", "Lena-Fehler verworfen", "Kategorien-Zweige unverändert", "accountsNoticeFor = None", "transferTargetsLoadState immer Ready", "sprintf im Settings-Label" ließen jeweils die neuen Tests rot werden): `LedgerViewTests.fs` (buildAccountCards: gescheitert, lädt/NotAsked, Null bei erfolgreicher Antwort ohne Eintrag, Zeile mit Erneut laden schickt `LoadBalances`; der alte Test "Ein Konto ohne geladenen Stand zeigt Null" ist ersetzt; der bestehende Quelltext-Test "Kontostände laufen über die ruhigen Money-Bausteine" zeigt jetzt auf `cardBalanceElement`), `LedgerCategoriesAwaitSettingsTests.fs` (3), `LedgerLenaFlagClientTests.fs` (2), `CategoryPickerTests.fs` (1), `LedgerSplitEditorClientTests.fs` (1), neue Datei `SettingsViewTests.fs` (1). `BalancesLoaded (Error _)` setzt `Failure`: bestehender Test in `LedgerPageTests.fs` ("Ein spätes Scheitern ersetzt fertige Kontostände nicht"). Die Lena-Tests in `LedgerLenaFlagClientTests.fs` und `LedgerLenaFlagBatchClientTests.fs` sind auf den neuen Typ umgestellt (`Success (Some …)`/`Success None`), sonst unverändert grün.

**Gate**: `./verify.sh` ergibt `VERIFY: PASS` (dotnet 4276 grün, 13 übersprungen aus den erlaubten Listen; node 316 grün; build, guard:classes, guard:tokens grün). Ein Lauf davor war rot in `[layout] Kopf: ab 768 px …` (`scripts/budget-page.test.mjs`, Budget-Seite, von dieser Task nicht berührt). Die Datei allein war 34/34 grün, der nächste volle Lauf war PASS. Der Test schwankt unter Last.

**README-Delta** mit `npm run check:readme-delta -- --bc ledger` geprüft (Exit 0).

**Screenshots** (API per `page.route` gemockt, erfundene Konten "Testkonto 1..3", Toasts der nicht gemockten Endpoints ausgeblendet):
- lädt, PC: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/01e5b14d-920c-4666-a681-f51df6913f4b/scratchpad/vq2g8/leiste-laedt-pc.png
- lädt, Handy: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/01e5b14d-920c-4666-a681-f51df6913f4b/scratchpad/vq2g8/leiste-laedt-handy.png
- gescheitert, PC: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/01e5b14d-920c-4666-a681-f51df6913f4b/scratchpad/vq2g8/leiste-gescheitert-pc.png
- gescheitert, Handy: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/01e5b14d-920c-4666-a681-f51df6913f4b/scratchpad/vq2g8/leiste-gescheitert-handy.png
Skript: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/01e5b14d-920c-4666-a681-f51df6913f4b/scratchpad/vq2g8/shots.mjs

**Nebenbefund (Kandidat für die Kann-Sammlung)**: `Components/Settings/State.fs:219` zeigt beim Ladefehler der Lena-Einstellung einen englischen Toast "Failed to load Lena flag colour: …" auch auf der Kassenbuch-Seite (in den Screenshots sichtbar, bevor die Toasts ausgeblendet wurden).
