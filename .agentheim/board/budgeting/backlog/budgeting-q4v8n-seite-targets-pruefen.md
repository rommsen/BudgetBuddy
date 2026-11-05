---
id: budgeting-q4v8n
title: "Seite Targets prüfen #/budget/target-check: Knopf im Kopf der Budget-Seite, Gruppen mit Balken, Vorschlag übernehmen und Anpassen … im Panel oder BottomSheet"
status: backlog
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [design-system-001, budgeting-p2zr6, budgeting-r7k2m, budgeting-h7q4v]
blocks: [budgeting-m3x7f]
tags: [budget, target, pruefen, client]
related_adrs: [0072, 0075, 0080, 0082]
related_research: []
prior_art: [budgeting-h7q4v, budgeting-dyn8g, budgeting-k8wq3]
---

## Why
Roman prüft gelegentlich, ob seine Targets zu den Ausgaben passen, und will ein unpassendes Target in einem Schritt korrigieren (Entscheidung 2026-11-05, ADR 0082). budgeting-p2zr6 liefert Regel und Daten, budgeting-r7k2m das herausgelöste Formular. Diese Task baut die Seite.

## What
Regel und Texte der Gruppen: ADR 0082. Vorbild für Route, Knopf, Laden und Rückkehr: die allgemeine Historie (budgeting-h7q4v, `History.fs`, `HistoryView.fs`, `Model.GeneralHistory`).

### Seite
- Route `#/budget/target-check`, Navigation zeigt den Tab Budget. Der Zustand liegt auf der Budget-Seite wie `GeneralHistory` (Daten als `RemoteData<TargetCheckData>` mit Generation, Panel, Aufklappen von "Passt"), damit die Rückkehr Monat und Auswahl behält.
- Knopf "Targets prüfen" im Kopf der Budget-Seite neben dem Knopf Historie, als Icon-Knopf wie dieser (Icon aus `Icons` nachschlagen, nicht erfinden; `aria-label` und `Title` "Targets prüfen"), am Handy im Kopf neben Historie. In jedem gezeigten Monat sichtbar, denn die Seite gilt immer für den laufenden.
- Kopf der Seite: Zurück, Titel "Targets prüfen", der laufende Monat ("November 2026"). Zurück wie bei der Historie: über den Knopf geöffnet ein Schritt zurück mit erhaltenem Monat und erhaltener Auswahl, per Link geöffnet beginnt die Budget-Seite von vorn. Die Budget-Seite liest den Monat nach der Rückkehr still neu (`KeptPage`), weil ein Übernehmen den Fehlbetrag ändert.
- Laden über `getTargetCheck` mit `Retry.reading`; Fehler als Fehlerkarte mit Wiederholen.
- Gruppen in der Reihenfolge aus `targetCheckGroups`, Kopf je Gruppe mit dem Satz und der Zahl. Nur "Passt" klappt auf und zu (`Button.disclosureListRow`, `aria-expanded`), beim Betreten zu. Die anderen Gruppen stehen offen. Sind die vier ersten Gruppen leer, steht über "Passt" "Nichts zu prüfen."
- Zeile: Name, darunter klein die Kategoriegruppe. Werte: Target als Monatswert ("Target 216,67 €/Monat"), bei anderem Rhythmus dahinter klein der gelesene Betrag mit Rhythmus wie in der Budget-Liste; "Ø 260,00 €"; "8 von 12 Monaten darüber" (ohne Target und bei "nie": "1 von 12 Monaten mit Ausgaben"). Darunter die Balken der gezählten Monate über `spendingBars` und `Stats.monthBars` (alle vorbei, kein "läuft"). Bei "Passt" mit Zusatz ("neu gesetzt am …", "entfernt am …", "Sparziel, wird nicht geprüft", "wird nicht geprüft") steht nur der Zusatz, ohne Werte und Balken.
- Knöpfe je Zeile: "<Betrag> übernehmen" (`Button.secondary`; Orange ist die eine Hauptaktion, ADR 0075 Punkt 3, und stünde hier in Dutzenden Zeilen) nur bei "mehr", "weniger" und "Kein Target" mit Vorschlag, der Betrag im Rhythmus mit Rhythmus wie in der Liste ("50,00 €/Woche übernehmen"). "Anpassen …" (`Button.secondary`) in jeder Zeile.
- Große Liste: Gruppen und Zeilenwerte einmal vor der Schleife berechnen, die zugeklappte Gruppe "Passt" rendert ihre Zeilen nicht (CLAUDE.md).

### Übernehmen
- Schickt `changeCategoryTarget` mit `TargetCheckData.Month`, `Shown` = gelesenes Target, `Wanted` = `Suggestion.Draft` aus budgeting-p2zr6. Der Auftrag entsteht in einer reinen Funktion (Vorbild `buildQuickAddRequest`), der Cmd mit `Client.Api` liegt hinter einem Lambda.
- Antwort `TargetWritten t`: `withWrittenTarget` setzt Target und letzte Änderung, die Zeile steht bei "Passt" mit "neu gesetzt am <Today>". Ist der laufende Monat auf der Budget-Seite geladen, bekommt er das Target ebenfalls (`withTarget`).
- `TargetChangedInYnab t`: Target aus der Antwort, ohne neue letzte Änderung, Zeile neu eingeordnet, Warn-Toast mit dem Satz aus `targetNoticeText TargetChangedNotice`.
- `Error grund`: Zeile unverändert, Fehler-Toast `targetRejectedAwayText grund` ("Target nicht gespeichert: …").
- Abgebrochener Aufruf (Netzfehler des Proxys): Fehler-Toast, die Prüfung lädt still neu, weil der Ausgang offen ist.

### Anpassen … (Panel)
- Öffnet ab 1024 px ein Panel rechts neben der Liste, darunter ein BottomSheet (dieselbe Grenze und dieselben Bausteine wie der Inspektor), mit Name der Kategorie und dem herausgelösten Formular aus budgeting-r7k2m. Monat ist `TargetCheckData.Month`.
- Vorbelegt mit dem Vorschlag: Betragsfeld = Betrag im Rhythmus, Rhythmus und Datum wie gelesen; ohne Target Rhythmus monatlich. Ohne Vorschlag ("nie", "Passt", Ø ≤ 0) steht das gelesene Target ohne Eingabe.
- Warnung beim Wechsel der Art und Bestätigung von "Target entfernen" wie im Tab Target.
- Speichern mit `TargetWritten`: Panel schließt, Zeile rutscht nach "Passt" ("neu gesetzt am …" bzw. nach Entfernen "entfernt am …").
- `TargetPartlyWritten`: Panel bleibt offen mit dem gelben Hinweis wie im Tab, die Zeile nimmt das Target nach Schritt 1 und rutscht nach "Passt".
- `TargetChangedInYnab`: Panel bleibt offen mit dem Hinweis und dem frischen Stand, die Zeile wird neu eingeordnet.
- `Error`: Panel bleibt offen, Meldung im Formular, Eingabe bleibt.
- Abbrechen schließt ohne Senden. "Anpassen …" einer anderen Zeile ersetzt den Inhalt, die Eingabe ist verworfen. Ohne "Anpassen …" ist das Panel zu.

### Sperre
- Es läuft höchstens ein Target-Aufruf zugleich, gemeinsam mit dem Tab Target über `TargetCall` (budgeting-n7h3x). Solange er läuft: ein zweiter Tipp auf denselben Knopf sendet nichts, alle anderen "übernehmen" sind gesperrt, Speichern und Entfernen im Panel warten mit "Wartet, bis das Target bei <Kategorie> gespeichert ist." (`targetWaitsForTargetText`). "Anpassen …" und Abbrechen bleiben bedienbar. Der Spinner steht nur am auslösenden Knopf.
- Die Antwort wirkt nur auf ihre Kategorie und auch dann, wenn die Seite inzwischen verlassen wurde.

**Festgelegt (Tactical-Modeler):** Route `#/budget/target-check`; beide Zeilenknöpfe neutral; nur "Passt" klappbar; Zusatzzeilen ohne Werte; "Anpassen …" auch in "Passt" (dort ohne Vorschlag); Entfernen bleibt im Panel, weil es das bestehende Formular ist und die Antwort auf "Gibst du nie aus?" sein kann; `TargetPartlyWritten` lässt das Panel offen, weil sonst die Meldung zum fehlenden Datum verloren ginge; die Sperre ist die globale `TargetCall` statt einer eigenen, damit Tab und Seite nicht gegeneinander schreiben.

## Acceptance criteria
Client-`update` unter .NET (Doktrin Punkt 1, Cmds mit `Client.Api` hinter Lambdas):
- [ ] Route `#/budget/target-check` wird geparst und erzeugt; der Knopf führt über das echte App-`update` zu `NavigateTo` dieser Route, Zurück zur Budget-Seite mit erhaltenem Monat und erhaltener Auswahl; per Link geöffnet und woandershin verlassen beginnt die Budget-Seite von vorn.
- [ ] Laden: Antwort mit fremder Generation wird verworfen; Fehler zeigt Wiederholen, Wiederholen lädt neu und behält Aufklappen.
- [ ] "Passt" ist beim Betreten zu, ein Tipp klappt auf, ein zweiter zu.
- [ ] Der Auftrag von "übernehmen" (reine Funktion): mit Target `Shown` = gelesenes Target, `Wanted` = Vorschlag im Rhythmus mit gelesenem Datum und `KeepRhythm`; bei "Kein Target" `Shown = None`, `SetRhythm Monthly`, ohne Datum; Monat = `TargetCheckData.Month`.
- [ ] Antworten auf "übernehmen": `TargetWritten` setzt die Zeile nach "Passt" mit "neu gesetzt am <Today>" und das Target im geladenen laufenden Monat der Budget-Seite; `TargetChangedInYnab` ordnet mit dem frischen Target neu ein und meldet per Warn-Toast; `Error` lässt die Zeile stehen und meldet "Target nicht gespeichert: <Grund>"; ein abgebrochener Aufruf meldet und lädt die Prüfung neu.
- [ ] Panel: "Anpassen …" öffnet mit vorbelegtem Vorschlag (Betragsfeld im Rhythmus), ohne Vorschlag mit dem gelesenen Target; Abbrechen schließt ohne Aufruf; "Anpassen …" einer anderen Zeile ersetzt den Inhalt; `TargetWritten` schließt und setzt die Zeile nach "Passt"; Entfernen mit `TargetWritten None` setzt "entfernt am <Today>"; `TargetPartlyWritten`, `TargetChangedInYnab` und `Error` lassen das Panel offen mit Hinweis bzw. Meldung.
- [ ] Zustandsregel als Aufzählung aller Reihenfolgen durch das echte `update` (Doktrin Punkt 20, Vorbild `moveWays`): Nachrichten "übernehmen A" (zweimal), "übernehmen B", "Anpassen … B" mit Speichern, Antwort A, Antwort B, Abbrechen. Orakel unabhängig vom Code: höchstens ein Aufruf zugleich, kein zweiter Aufruf für A vor der Antwort A, jede Antwort ändert nur ihre Kategorie, nach allen Antworten ist kein Aufruf offen.
- [ ] Die Sichtbarkeit der Knöpfe (übernehmen nur bei Vorschlag in "mehr", "weniger", "Kein Target"; gesperrt und Spinner je Lage) kommt aus einer reinen Funktion, die View ruft sie nur auf; getestet je Gruppe und Lage.
- [ ] `getTargetCheck` steht im Retry-Inventar von `src/Tests/RetryTests.fs`.

Browser-Wächter (`scripts/*.test.mjs`, API per `page.route` gemockt, nur mit Kennung):
- [ ] Zeile `"targets-pruefen"` in `FLOWS` (`scripts/browser-usage.mjs`) und genau ein Test `[flow:targets-pruefen]`: am Handy über den Knopf im Kopf die Seite öffnen, in "Gibst du mehr aus?" "<Betrag> übernehmen" tippen, die Zeile steht danach bei "Passt" mit "neu gesetzt am …", Zurück führt zur Budget-Seite.
- [ ] `[layout]` bei 375 px: Kopf der Budget-Seite mit beiden Icon-Knöpfen ohne Überlauf, Trefferflächen 48 px; eine Zeile mit Werten, 12 Balken und beiden Knöpfen ohne Abschneiden, auch mit "1.234,00 €/Jahr übernehmen".
- [ ] `[layout]`: "Anpassen …" öffnet ab 1024 px ein Panel rechts, bei 375 px ein BottomSheet.
- [ ] Die bestehenden Fixtures in `scripts/*.test.mjs` bleiben unverändert (kein Shared-Record mit neuem Pflichtfeld); neue Fixtures für `getTargetCheck` nur mit erfundenen Namen und Beträgen.

Sonst:
- [ ] Neue DS-Varianten nur, falls `Stats.monthBars` mit 12 Balken in der Zeile nicht passt: dann als Variante in `src/Client/DesignSystem/Stats.fs` mit Eintrag im Styleguide (§2 Monatsbalken) und auf `/styleguide`. Werte aus dem Styleguide, nicht erfunden.
- [ ] README budgeting: **Targets prüfen** (Seite, Knopf, Zeile, übernehmen, Anpassen …, Sperre) als README-Delta, geprüft mit `npm run check:readme-delta -- --bc budgeting`.
- [ ] `[human-eye]` Lesbarkeit der 12 Balken je Zeile am Handy und Dichte der Seite mit rund 100 Zeilen, per Screenshot (`npm run screenshot:route`) angesehen und im Outcome vermerkt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Skizze, von Roman am 2026-11-05 als Vorlage freigegeben: https://claude.ai/artifact/Twwr9mKz6vFU9YK2waFNn3 (PC mit Panel, Handy mit Bottom Sheet, erfundene Zahlen). Abweichungen der Task von der Skizze sind gewollt: "übernehmen" ist neutral statt gefüllt (ADR 0075, neutrale Knöpfe), wöchentliche Vorschläge runden auf volle Euro statt auf 5 € (ADR 0082 Punkt 9), "Anpassen …" gibt es auch bei "Passt".

Abhängigkeiten: design-system-001 (Styleguide-Gate) und budgeting-h7q4v liegen in done. budgeting-p2zr6 (Regel, Endpoint) und budgeting-r7k2m (Formular herausgelöst) sind backlog und müssen vorher fertig sein.

Der Weg von der Leiste am Monatsersten zu dieser Seite entsteht in budgeting-m3x7f, das deshalb auf diese Task wartet.

Nach dem Lauf vorschlagen: `frontend-architecture`, `design-quality`, `qa-milestone-reviewer`.
