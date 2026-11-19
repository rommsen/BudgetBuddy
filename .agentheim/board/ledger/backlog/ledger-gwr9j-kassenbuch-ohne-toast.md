---
id: ledger-gwr9j
title: "Kassenbuch am Handy ohne Toast (R3), Teil 1 Liste: Texte der Leiste für Wische, Spiegeln, Neue Buchung, Mehrfachauswahl und Ladefehler, „Spiegeln läuft“, Rückgängig nach n Wischen ohne Sprung der Liste"
status: backlog
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, design-system-gkded]
blocks: [ledger-p5y9s, ledger-enjex, ledger-k68sd, ledger-pz5wv]
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, ledger-th36h, ledger-at63w, ledger-gy77x]
---

## Why
Romans Screenshots vom 2026-11-19 aus dem echten Kassenbuch: Spiegel-Toast oben und Wisch-Toast unten gleichzeitig, nach
mehreren Wischen drei Toasts übereinander über Liste und „+ Buchung“. Regel R3 (design-system-ytgnc): am Handy kein
Toast, ein fester Meldungsplatz je Seite.

Die ursprüngliche Task ist in vier Teile geteilt (Verfeinerung 2026-11-19):
- **ledger-gwr9j** (diese): Liste und Leiste. Danach gibt es am Handy auf der Liste keinen Toast mehr.
- **ledger-p5y9s**: Detail I, „Rückgängig am Feld“ als Mechanik, Kategorie, Übertrag, Aufteilen, Schließen des Details.
- **ledger-enjex**: Detail II, Memo, Betrag, Payee, Lena, Erneut senden; Schlussprüfung „kein Toast im Kassenbuch am Handy“.
- **ledger-k68sd**: Gelöschte Buchung als Zeile am Platz.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am
2026-11-19 freigegeben), Abschnitt 2 „Kassenbuch“ und Anhang „Alle Toasts“. Pfade unter Notes.

„Handy“ heißt hier wie in R3 unter 1024 px (dort, wo heute der FAB steht, `lg:hidden`). Am PC (ab 1024 px) bleibt der
Toast unten rechts, höchstens einer, mit denselben Texten wie heute.

### Leiste mit „+ Buchung“
Die Leiste selbst baut design-system-gkded (Abgleich der Verfeinerung 2026-11-19): `Feedback.bar` mit „+ Buchung“ statt
`captureFab`, Wegfall von `captureFabSpacer` und `.ledger-capture-fab`, Platz über `--feedback-bar-height`, Leiste fehlt
bei offenem Formular, im Auswahl-Modus nur mit Meldung über `selectionBars`, eine Meldung bis zur nächsten Aktion, das
Zusammenfassen gleicher Wische (`FeedbackSlot.post`). Nach gkded landen die heutigen Toasts des Kassenbuchs schon in der
Leiste, mit den alten Texten. Diese Task setzt darauf auf:
- die Texte je Meldung nach der Tabelle unten (Aktion vorn, nie gekürzt; darunter Name oder Grund) über `ShowFeedback`
  statt `ShowToast`,
- den neuen Zustand „Spiegeln läuft …“ in der Leiste,
- Kriterium: die letzte Zeile der Liste ist am Ende gescrollt ganz zu sehen, mit Leiste in Ruhe und mit zweizeiliger
  Meldung (gkded liefert die Höhe, hier wird es für das Kassenbuch geprüft).
- Wo die Meldung im Auswahl-Modus steht, ist offene Frage 4; bis zur Antwort gilt die Empfehlung.

### Zuordnung je Toast (Zeilen in `src/Client/Components/Ledger/State.fs`, Stand d6e9cb95)
Spalten wie im Anhang der Skizze: heute, Vorschlag, mit R3.

| Zeile | Heute | Anhang | Mit R3 (Handy) |
|---|---|---|---|
| 2438 | „Prüfvermerk nicht in YNAB: …“ nach Wisch, rot | bleibt | Leiste gelb: „Geprüft, nicht in YNAB“ / Grund (Abweichung im Ton: heute rot, R3 kennt für „BB hat es, YNAB nicht“ gelb, wie im Anhang Zeile „… nicht in YNAB“) |
| 2441 | „<Payee> geprüft“ mit Rückgängig | bleibt, Leiste | „✓ Geprüft“ / Payee („ohne Payee“ wie heute) / „Rückgängig“ (Skizze 22) |
| 2441 | mehrere Wische hintereinander: je ein Toast | bleibt, zusammengefasst | „✓ 3 geprüft“ ohne zweite Zeile, „Rückgängig“ nimmt alle zurück (Skizze 23) |
| 2451 | „Prüfvermerk nicht gesetzt: …“ | anders | Leiste rot: „Nicht geprüft“ / Grund; die Zeile bleibt „zu prüfen“ |
| 2472 | „Prüfvermerk nicht in YNAB: …“ nach Rückgängig | bleibt | Leiste gelb wie 2438 |
| 2479 | „Rückgängig nicht möglich: …“ | anders | Leiste rot: „Rückgängig nicht möglich“ / Grund |
| 2035 | „Spiegel-Lauf fertig: 3 neu, 1 geändert, 0 gelöscht“ | bleibt, Leiste | „✓ Gespiegelt“ / „3 neu, 1 geändert“ (Texte: offene Frage 1) |
| (neu) | während des Laufs nur der Spinner am Knopf | Skizze 24 | „Spiegeln läuft …“ ohne Symbol, gedämpft, bis zum Ergebnis |
| 2042 | „Spiegel-Lauf gescheitert: …“ | bleibt, rot | Leiste rot: „Spiegeln gescheitert“ / Grund / „Erneut“ (= `StartMirror`, Skizze 26) |
| 3561 | „Buchung angelegt · Konto · Betrag“ mit Rückgängig | bleibt, Leiste | „✓ Angelegt“ / zweite Zeile nach offener Frage 3 / „Rückgängig“ (= `UndoCapturedBooking`, Skizze 27) |
| 3566 | „Buchung angelegt · …, noch nicht in YNAB: …“ gelb, ohne Rückgängig | bleibt | Leiste gelb: „Angelegt, nicht in YNAB“ / Grund, ohne Rückgängig (wie heute) |
| 3574 | Fehler beim Anlegen, wenn das Formular schon zu ist | bleibt | Leiste rot: „Nicht angelegt“ / Grund (bei offenem Formular steht er wie heute im Formular) |
| 3600 | „Buchung verworfen“ nach Rückgängig | entfällt | keine Meldung: die Zeile ist weg, die Leiste wird leer |
| 3604 | „Rückgängig nicht möglich: …“ | anders | Leiste rot wie 2479 |
| 4036/4040 | „n Buchungen geprüft (…)“, mit Rückgängig wenn etwas geändert | bleibt, Leiste | Aktion „n geprüft“, zweite Zeile die Zusätze aus `reviewBatchToastText` („2 ohne Änderung, 1 nicht in YNAB“), „Rückgängig“ (= `UndoReviewMarkBatch`) |
| 4046 | „Prüfvermerk nicht gesetzt: …“ | anders | Leiste rot |
| 4076 | „Prüfvermerk nicht in YNAB: …“ nach Sammel-Rückgängig | bleibt | Leiste gelb |
| 4082 | „Rückgängig nicht möglich: …“ | anders | Leiste rot |
| 4138/4140 | Kategorie für n, mit Rückgängig | bleibt, Leiste | Aktion und Zusätze aus `categoryBatchToastText` getrennt wie oben, „Rückgängig“ (= `UndoCategoryBatch`) |
| 4146 | „Kategorie nicht geändert: …“ | anders | Leiste rot |
| 4196/4197/4201 | Sammel-Rückgängig Kategorie: Fehler, Warnung, „Kategorie bei n … zurückgesetzt“ | bleibt | Leiste rot, gelb bzw. „✓ Zurückgesetzt“ / „Kategorie bei n Buchungen“ (Anhang: bleibt) |
| 4240/4242 | Lena für n, mit Rückgängig | bleibt, Leiste | wie 4138 mit `flagBatchToastText`, „Rückgängig“ (= `UndoFlagBatch`) |
| 4248 | „Flag nicht geändert: …“ | anders | Leiste rot |
| 4273/4275/4281 | Sammel-Rückgängig Flag | bleibt | wie 4196 ff. |
| 1811 | „Weitere Buchungen konnten nicht geladen werden: …“ | bleibt, rot | Leiste rot: „Nicht nachgeladen“ / Grund / „Erneut“ (= `LoadMoreEntries`) |
| 1868 | „Liste konnte nicht aktualisiert werden: …“ gelb | bleibt | Leiste gelb (Ton wie heute, Abweichung vom Anhang „rot“: die Liste steht, es ist eine Warnung) / Grund / „Erneut“ (= `RefreshEntries`) |
| 3873 | „Buchung nicht gefunden“ | bleibt | Leiste gelb |
| 3876 | „Buchung nicht geladen: …“ | bleibt | Leiste rot / Grund |
| 4386/4393 | Grund, warum Taste L nicht geht | (nicht im Anhang) | unverändert: nur am PC per Tastatur erreichbar, bleibt Toast |

Die Zeilen 1038, 2391 bis 2399, 2567 bis 2993, 3090 bis 3358, 3633 bis 3664, 3748 bis 3826 und die des Durchgehens (4548 bis 4715 und die
Zweige `isReviewCard`) gehören zu den anderen Teilen bzw. zu ledger-pz5wv. Bis dahin bleiben sie Toast.

### Wische zusammengefasst
- Das Zusammenfassen selbst (`FeedbackSlot.post`, `UndoReviewMark` wird `UndoReviewMarkBatch`) kommt aus
  design-system-gkded. Hier gilt: „Rückgängig“ nach n Wischen nimmt alle n Prüfvermerke zurück über
  `StartUndoReviewMarkBatch`. Bedingung: die Liste bleibt an ihrer Stelle stehen (heute baut
  `ReviewMarkBatchRestored` sie über `rebuildEntries` neu auf, das darf den Scrollstand nicht verlieren), jede Zeile
  trägt danach wieder „zu prüfen“.
- Eine andere Aktion dazwischen (etwa Spiegeln) beginnt die Zählung neu.
- Die Wisch-Geste selbst (`Swipe.SwipeableRowRight`, `DesignSystem/Swipe.fs`) bleibt. Die Aktionsfläche zeigt
  „✓ Geprüft“ wie Skizze 21 (heute nur „Geprüft“, `swipeActionLabel`); das Häkchen als Symbol aus `Icons`, falls es eins
  gibt, sonst Zeichen. Die Zeile ändert sich nach dem Wisch nur leise: Kante und „zu prüfen“ fallen weg, kein Leuchten.

### Funktionsabgleich Liste (heute in BB)
| Funktion | Stand |
|---|---|
| „+ Buchung“ öffnet „Neue Buchung“ (vorbelegt mit dem Konto des Filters) | bleibt, Knopf in der Leiste statt schwebend |
| Kopf: „Abrufen“ (⤓) und „Jetzt spiegeln“ (⟳) als Symbolknöpfe, „Gespiegelt …“ darunter | bleibt; die Skizze zeigt nur ⟳, BB behält beide |
| Tabs Buchungen / Regeln, Konto, Suche, Filter, Chips, Summen je Tag | bleibt (Skizze vereinfacht) |
| Hinweis „Spiegel läuft. Die Liste bleibt so lange stehen.“ an der Liste (View.fs ~4708) | bleibt, bis Roman ihn streicht (offene Frage 2) |
| Wisch nach rechts prüft eine „zu prüfen“-Zeile | bleibt; geprüfte Zeilen bleiben nicht wischbar (Abweichung: Skizze zeigt „schon geprüft“ mit 40 px Weg, das war Demo der Skizze und ist nicht Teil von R3) |
| Rückgängig nach Wisch | bleibt, in der Leiste |
| Langes Drücken: Auswahl-Modus, Sammelaktionen, Rückgängig je Sammelaktion | bleibt, Rückmeldung in der Leiste |
| Nachladen beim Scrollen, Nachladen im Hintergrund | bleibt, Fehler in der Leiste |
| Route `#/ledger/entry/<id>` | bleibt, Fehler in der Leiste |
| Toasts am Handy | gestrichen (Roman, 2026-11-19, R3) |

## Acceptance criteria
- [ ] Am Handy (412 px) gibt es auf der Kassenbuch-Liste keinen schwebenden Knopf mehr; die Leiste über der Navigation trägt
      „+ Buchung“ und öffnet „Neue Buchung“ wie bisher (Konto des Filters vorbelegt).
- [ ] Mit Meldung: runder Knopf „+“ (zugänglicher Name „Neue Buchung“), Meldung links zweizeilig, Aktion nie gekürzt,
      zweite Zeile mit „…“ gekürzt, rechts „Rückgängig“ bzw. „Erneut“; Farben als Rollen aus dem Baustein von ytgnc
      (ok, Warnung, Fehler, läuft), keine eigenen Werte.
- [ ] Jede Zeile der Tabelle „Zuordnung je Toast“ ist umgesetzt: am Handy die Leiste mit dem genannten Text, am PC derselbe
      Toast wie heute (Text, Ton, Aktion). Client-Tests in `src/Tests` (heute u. a. `LedgerReviewMarkClientTests.fs`,
      `LedgerReviewMarkBatchClientTests.fs`, `LedgerCategoryBatchClientTests.fs`, `LedgerLenaFlagBatchClientTests.fs`,
      `LedgerCaptureClientTests.fs`, `LedgerBackgroundRefreshTests.fs`, `LedgerPageTests.fs`) prüfen je Weg die Meldung
      (Aktion, zweite Zeile, Ton, Aktion „Rückgängig“/„Erneut“ und welche Nachricht sie auslöst).
- [ ] Drei Wische hintereinander: eine Meldung „✓ 3 geprüft“; ein Tipp auf „Rückgängig“ macht alle drei wieder „zu
      prüfen“, die Liste bleibt an ihrer Stelle. Reiner Test für das Sammeln der Rücknahmen; Browser-Test
      `[flow:buchung-pruefen]` (Handy-Teil in `scripts/ledger-review-mark.test.mjs`) wartet auf die Leiste statt auf
      `innerText.includes("geprüft")`.
- [ ] Spiegeln am Handy: während des Laufs „Spiegeln läuft …“ in der Leiste, danach Ergebnis bzw. Fehler mit „Erneut“
      (startet den Lauf neu). Texte nach Antwort auf offene Frage 1.
- [ ] Die Meldung bleibt ohne Zeitlimit stehen, bis eine andere Aktion sie ersetzt oder die Seite verlassen wird.
- [ ] Die Liste endet über der Leiste. Sweep-Invariante „am Ende gescrollt verdeckt“ bleibt grün; der Gegenbeweis in
      `scripts/layout-invariants.test.mjs` (heute über `ledger-capture-fab-spacer`, Zeile ~199) zeigt auf die Leiste.
- [ ] Sweep-Zustände in `scripts/layout-states.mjs`: alle, die heute `.ledger-capture-fab` antippen (~1480 bis ~1640), tippen
      „+ Buchung“ in der Leiste; neue Zustände `kassenbuch-leiste-geprueft`, `kassenbuch-leiste-3-geprueft`,
      `kassenbuch-leiste-spiegel-fehler` bei `HANDY_WIDTHS`. `kassenbuch-spiegel-laeuft` zeigt am Handy die Leiste.
- [ ] `[flow:buchung-erfassen]` (`scripts/ledger-capture.test.mjs`) tippt „+ Buchung“ in der Leiste und findet danach
      „Angelegt“ mit „Rückgängig“ in der Leiste, nicht im Toast.
- [ ] Am PC (1280) unverändert: kein Leisten-Element sichtbar, Toast unten rechts, höchstens einer.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome für Ruhe, Wisch eingerastet, Geprüft,
      3 geprüft, Spiegeln läuft, Spiegeln gescheitert, Angelegt; dazu PC 1280), Abweichungen nur die in dieser Task
      genannten.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Offene Fragen an Roman
1. **Wort für den Spiegel-Lauf in der Leiste.** Die Skizze sagt „Abrufen läuft …“ / „Abgerufen“ am Knopf ⟳. In BB ist ⟳
   „Jetzt spiegeln“ und „Abrufen“ (⤓) der Comdirect-Abruf (banking-import-vwhfm). Empfehlung: „Spiegeln läuft …“,
   „✓ Gespiegelt“ / „3 neu, 1 geändert“ (Teile mit 0 weglassen, alles 0: „nichts Neues“), Fehler „Spiegeln gescheitert“.
   Grund: zwei Knöpfe, zwei Wörter, sonst verwechselt man den Bankabruf mit dem Spiegel.
2. **Hinweis „Spiegel läuft. Die Liste bleibt so lange stehen.“** steht heute über der Liste; mit der Leiste stünde der Lauf
   zweimal da. Empfehlung: behalten, bis du ihn streichst; er sagt zusätzlich, dass die Liste stehen bleibt.
3. **Zweite Zeile nach „Angelegt“.** Skizze: „Payee · Betrag“. Heute: „Konto · Betrag“. Empfehlung: „Payee · Betrag“, dahinter
   „· Konto“, wenn die Buchung nicht im gerade gezeigten Konto liegt (dann sieht man sie in der Liste nicht). Ohne Payee
   „Konto · Betrag“.
4. **Meldung im Auswahl-Modus.** Empfehlung: Leiste ohne Knopf direkt über der Auswahl-Leiste, die Liste endet über beiden.
   Alternative: Meldung wartet, bis der Auswahl-Modus endet (dann sähe man einen Spiegel-Fehler zu spät).

## Notes
Captured 2026-11-19 aus ledger-vf5h6, verfeinert und geteilt 2026-11-19.

Schnittstelle: die Seite meldet über die Schnittstelle aus design-system-ytgnc (Name dort, vorläufig `Feedback`); der Root
entscheidet Handy (Leiste) gegen PC (Toast). Heute läuft alles über `ExternalMsg.ShowToast`/`ShowToastWithAction`
(`Components/Ledger/Types.fs` ~3011) und `toastActionMsg` in `src/Client/State.fs` (~110). Die Undo-Arten dieser Task
(`UndoReviewMark`, `UndoReviewMarkBatch`, `UndoCapturedBooking`, `UndoCategoryBatch`, `UndoFlagBatch`) müssen über die
Leiste dieselbe Nachricht auslösen wie heute über den Toast. Texte stehen in `Components/Ledger/Types.fs`
(`swipeReviewedToastText` ~531, `swipeActionLabel`, `reviewBatchToastText` ~1986, `categoryBatchToastText` ~2062,
`flagBatchToastText` ~2136, `mirrorSummaryText` ~3017) und `Capture.fs` (`createdText` ~226). Die Aufteilung in Aktion und
zweite Zeile als reine Funktionen neben den heutigen Texten (Test-Doktrin: reine Funktion zuerst, Heimat `src/Tests`).

Root: `captureFab` und `selectionBars` rendert `src/Client/View.fs` (~221) außerhalb der Seitenhülle; die Leiste kommt
dorthin oder an den Platz, den ytgnc vorgibt. Die Übersicht hat einen eigenen FAB (`overview-capture-fab`), der gehört zu
budgeting-gtrgk.

Skizze (lokal, Worker können das Artifact nicht öffnen):
- HTML: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/durchgehen-handy.html
- Quelltext: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze.src.html (CSS `.kb-bar`, `.kb-msg`, `.kb-add` ~Zeile 235; Verhalten `KB.prototype` ~Zeile 996; Anhang „Alle Toasts“ ~Zeile 561)
- Skizze 412 px: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/20-kassenbuch-ruhe.png, 21-kassenbuch-wisch-eingerastet.png, 22-kassenbuch-geprueft.png, 23-kassenbuch-3-geprueft.png, 24-kassenbuch-abrufen-laeuft.png, 25-kassenbuch-abgerufen.png, 26-kassenbuch-abrufen-fehler.png, 27-kassenbuch-angelegt.png (alle im selben Ordner)
- Heute: /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/vf5h6/heute-kassenbuch-wisch-412.png und /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/review-fixes/kassenbuch-412.png
