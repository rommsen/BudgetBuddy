---
id: categorization-t6j00
title: "Regeln ohne Toast (R3): gelöschte Regel als gestrichelte Zeile am Platz mit Rückgängig, gespeicherte Regel leuchtet kurz, Fehler an der Regel oder als Hinweis der Seite"
status: done
type: feature
context: categorization
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, design-system-gkded]
blocks: []
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, categorization-n3sxw]
---

## Why
Regel R3 (design-system-ytgnc, von Roman am 2026-11-19 freigegeben): am Handy kein Toast, jede Meldung hat einen
festen Platz. Auf dem Tab „Regeln“ liegt heute nach „Regel löschen“ der Toast „Regel gelöscht · Rückgängig“ unten über
der Navigation (Screenshot heute). Skizze Abschnitt 3 „Regeln: Regel gelöscht“: die Regel hinterlässt an ihrem Platz eine
schmale Zeile „„Regel drei“ gelöscht · Rückgängig“. Man sieht, wo sie stand, und holt sie dort zurück.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, freigegeben am
2026-11-19), Ansicht „Regeln: Regel gelöscht“ und Zeile „Regeln“ der Tabelle „Übrige Seiten“. Pfade unter Notes.

### 1. Gelöschte Regel als Zeile am Platz (Skizze 33)
- Nach „Regel löschen“ schließt das Formular (Sheet am Handy, Panel am PC) wie heute. An der Stelle, an der die Regel
  stand, steht sofort eine Zeile „„<Name>“ gelöscht“ mit „Rückgängig“ rechts. Solange `deleteRule` läuft, steht dort
  „„<Name>“ wird gelöscht …“ ohne „Rückgängig“; so springt die Liste nicht, wenn die Antwort kommt.
- Aussehen aus dem DS-Baustein „Zeile gelöscht am Platz“ von design-system-ytgnc (Skizze `.mk-ghost`: gestrichelter Rand
  in der Rahmenfarbe, Text in der sekundären Textrolle, „Rückgängig“ im Akzent, halbfett). Werte aus Token-Layer und
  Styleguide, keine eigenen.
- Die Zeile trägt keine Nummer; die Regeln danach rücken in der Nummer auf (Skizze: „2 Regel vier“).
- Der Name wird gekürzt mit „…“, „gelöscht“ und „Rückgängig“ nie (R3: die Aktion wird nie gekürzt).
- „Rückgängig“ ruft `restoreRule` mit Regel und Stelle (heute `RestoreRequested`). Während das läuft:
  „„<Name>“ wird wiederhergestellt …“. Danach steht die Regel wieder an ihrem Platz und leuchtet kurz (wie 2.).
- Scheitert das Wiederherstellen, wird die Zeile rot (Fehlerrolle): „„<Name>“ nicht wiederhergestellt“, darunter der
  Grund, rechts „Erneut“ statt „Rückgängig“ (R3: Fehler am selben Platz mit „Erneut“).
- Die Zeile verschwindet mit der nächsten Aktion auf dem Tab (Regel wählen, „Neue Regel“, Pfeil, ein weiteres Löschen,
  „Regel anlegen“ aus einer Buchung) und beim Verlassen des Tabs (`RulesTabLeft`). Kein ✕, kein Zeitlimit. Es gibt
  höchstens eine solche Zeile; ein zweites Löschen ersetzt sie, das erste ist dann endgültig.
- Wird die Liste durch das Löschen leer, steht die Zeile statt „Keine Regeln“; der leere Zustand kommt mit der nächsten
  Aktion.
- Am PC in der Tabelle: eine Zeile über alle Spalten mit demselben Inhalt. In der Karte (Handy und schmale Liste neben
  dem Panel) eine Zeile der Karte.
- Handy: die Zeile und „Rückgängig“ sind mindestens 48 px hoch (§7.15).

### 2. Regel gespeichert oder angelegt
- Aus dem Tab „Regeln“: das Formular schließt am Handy (am PC bleibt das Panel mit der Regel, heute `afterSave`), die
  gespeicherte Regel leuchtet in der Liste kurz (Motion aus design-system-ytgnc, mit `prefers-reduced-motion` ohne
  Bewegung). Eine neue Regel steht am Ende und wird ins Bild gerollt, falls sie außerhalb liegt. Kein Toast.
- Aus einer Buchung („Regel anlegen“ im Kassenbuch-Detail oder auf der Durchgehen-Karte, `FromEntry`): die Regel ist
  nicht zu sehen, die Meldung geht an den Meldungsplatz der Seite nach R3: Aktion „Regel angelegt“, darunter der Name
  der Regel (gekürzt). Im Kassenbuch ist das die Meldungsleiste (ledger-gwr9j), im Durchgehen die Zeile der letzten
  Aktion, falls ledger-pz5wv sie für fremde Meldungen öffnet, sonst die Leiste.

### 3. Fehler
- Löschen gescheitert: die Regel kehrt an ihren Platz zurück (heute), darunter rot „Nicht gelöscht: <Grund>“ bis zur
  nächsten Aktion.
- Reihenfolge nicht gespeichert: die Liste lädt neu (heute), unter der zuletzt verschobenen Regel rot „Reihenfolge nicht
  gespeichert: <Grund>“ bis zur nächsten Aktion. Fehlt die Regel nach dem Laden, steht der Satz als Hinweis über der Liste.
- Speichern oder Anlegen gescheitert, Formular noch offen: wie heute im Formular (`rule-form-error`). Formular schon zu
  (Tab gewechselt, andere Regel gewählt): bei einer bestehenden Regel rot unter ihr „Nicht gespeichert: <Grund>“, bei
  einer neuen Regel am Meldungsplatz der Seite als Fehler „Regel nicht angelegt“ mit dem Grund.
- Regeln nicht neu geladen (Liste steht schon): das ist ein Zustand der Seite (R3, Muster ledger-pfhdq). Hinweis über der
  Liste „Regeln nicht neu geladen, die Liste kann veraltet sein: <Grund>“ mit „Neu laden“; bleibt, bis ein Laden klappt.
- Kategorien nicht geladen: der Toast entfällt ersatzlos. Die Auswahl der Kategorie zeigt den Grund schon mit
  „Erneut versuchen“ (`categoryLoadState`, `CategoryPicker.PickerFailed`).

### Jeder heutige Toast und sein neuer Platz
| Heute (Datei:Zeile) | Text heute | Neu |
|---|---|---|
| `src/Client/State.fs:825` (`RuleDeletedUndoable`, Aktion `UndoDeleteRule`, `State.fs:132`) | „Regel gelöscht“ · Rückgängig | Zeile am Platz (1.) |
| `Rules/State.fs:283` (`saveAnswered`, Ok) | „Regel angelegt“ / „Regel gespeichert“ | Regel leuchtet (2.); aus einer Buchung Meldungsplatz der Seite |
| `Rules/State.fs:287` (Formular zu) | „Regel konnte nicht angelegt/gespeichert werden: …“ | rot unter der Regel bzw. Meldungsplatz (3.) |
| `Rules/State.fs:308` | „Regeln konnten nicht neu geladen werden: …“ | Hinweis über der Liste (3.) |
| `Rules/State.fs:321` | „Kategorien konnten nicht geladen werden: …“ | entfällt, steht in der Auswahl |
| `Rules/State.fs:372` | „Reihenfolge konnte nicht gespeichert werden: …“ | rot unter der verschobenen Regel (3.) |
| `Rules/State.fs:479` | „Regel konnte nicht gelöscht werden: …“ | rot unter der zurückgekehrten Regel (3.) |
| `Rules/State.fs:494` | „Regel konnte nicht wiederhergestellt werden: …“ | Zeile am Platz wird rot mit „Erneut“ (1.) |

### Funktionsabgleich Tab „Regeln“
- Liste mit Nummer, Name, „inaktiv“, Bedingung, Kategorie: bleibt.
- Prioritätspfeile je Regel am Handy und in der Tabelle: bleibt (fehlen in der Skizze, Roman hat sie nicht gestrichen).
- Hinweis „Regeln werden von oben nach unten geprüft …“ über der Liste: bleibt (fehlt in der Skizze).
- Gruppierte Karte mit Trennlinien am Handy: bleibt. Die Skizze zeigt Einzelkarten je Regel; BB behält die Karte, die
  Zeile „gelöscht“ steht darin als Zeile.
- „Neue Regel“ unter der Liste am Handy, Formular im Sheet bzw. Panel, Muster testen, Kategorie-Auswahl, Löschen im
  Formular, Tastatur am PC (`RulesKeyboard`, Tastenleiste): bleibt.
- „Regel gelöscht“ mit Rückgängig: ändert sich (Toast wird Zeile am Platz).
- „Regel gespeichert“, Fehlermeldungen: ändern sich (siehe Tabelle).
- Toast am Handy: gestrichen (Roman, 2026-11-19, R3). Toast am PC für Löschen, Speichern und Fehler am Tab: gestrichen
  (Roman, 2026-11-19).

### Am Code
- `src/Client/Components/Rules/Types.fs`: neuer Zustand der Zeile „gelöscht“ im `Model` (Regel, Stelle, Phase
  läuft / gelöscht / wird wiederhergestellt / Fehler mit Grund), Fehler an einer Regel (Id und Text), Hinweis „nicht neu
  geladen“, die zuletzt geleuchtete Regel. `ExternalMsg.RuleDeletedUndoable` entfällt; `ShowToast` bleibt nur für den
  Meldungsplatz bei `FromEntry`, umgestellt auf den Meldungsweg aus design-system-ytgnc.
- `src/Client/Components/Rules/State.fs`: `saveAnswered`, `RulesLoaded`, `CategoriesLoaded`, `RulesReordered`,
  `DeleteRequested`, `RuleDeleted`, `RestoreRequested`, `RuleRestored`; „nächste Aktion“ als reine Funktion.
- `src/Client/Components/Rules/View.fs`: `ruleCard`, `ruleTable`, `content` (Hinweis, Zeile am Platz, Fehlerzeile).
- `src/Client/State.fs`: Zweig `RulesMsg` (Zeile 820 bis 828) und `UndoDeleteRule` (Zeile 132) entfallen;
  `UndoDeleteRule` aus `ToastActionKind` in `src/Client/Types.fs` entfernen, falls nichts anderes es nutzt.
- `scripts/layout-states.mjs`: Zustand `regeln-geloescht-toast` wird `regeln-geloescht` (Breiten Handy, 1024, 1440),
  er löscht die Regel mit dem langen Namen und wartet auf die Zeile (`data-testid="rule-deleted-row"`) statt auf
  `[data-toast-action]`. Neuer Zustand `regeln-geloescht-fehler` (Handy): `restoreRule` antwortet mit Fehler, die rote
  Zeile mit „Erneut“ verdeckt nichts und bricht nicht.
- `scripts/regeln-tab.test.mjs`: Kopfkommentar (Zeilen 8 und 9) auf den neuen Zustand ziehen.

### Tests (Heimat-Ebene nach `.claude/rules/test-doctrine.md`)
- .NET in `src/Tests/RulesListTests.fs` (rein über `updateWith` mit Ports): Löschen setzt die Zeile an die Stelle, Ok
  macht sie „gelöscht“, Fehler bringt die Regel zurück mit Fehler an ihr; Wiederherstellen Ok entfernt die Zeile und
  lässt die Regel leuchten, Fehler macht die Zeile rot; jede Aktion aus der Liste „nächste Aktion“ entfernt Zeile und
  Fehler; ein zweites Löschen ersetzt die Zeile. Die bisherigen Erwartungen auf `ShowToast` und `RuleDeletedUndoable`
  (Zeilen 111, 172, 275, 292, 447, 559, 672, 709) werden umgeschrieben, nicht gelöscht.
- Die Nummerierung ohne die Zeile „gelöscht“ als reine Funktion mit Test, falls sie nicht trivial aus der Liste folgt.
- Kein neuer Browser-Flow: Layout prüft die Sweep, Verhalten die .NET-Tests.

## Acceptance criteria
- [ ] `grep -n "ShowToast" src/Client/Components/Rules/` trifft nur noch die Meldung bei `FromEntry`, und die geht über den
      Meldungsweg aus design-system-ytgnc (am Handy kein Toast, am PC Toast nur ohne Ort). `RuleDeletedUndoable` und `UndoDeleteRule` gibt es nicht mehr.
- [ ] Nach „Regel löschen“ steht an ihrem Platz die Zeile „„<Name>“ gelöscht“ mit „Rückgängig“, ohne Nummer, die Regeln
      darunter rücken auf; bis zur Antwort „„<Name>“ wird gelöscht …“. Langer Name gekürzt mit „…“, „gelöscht“ und
      „Rückgängig“ ganz lesbar (Sweep `regeln-geloescht` bei 320 und 412 px).
- [ ] „Rückgängig“ bringt die Regel an dieselbe Stelle zurück, sie leuchtet kurz; scheitert es, wird die Zeile rot mit
      Grund und „Erneut“ (.NET-Test und Sweep `regeln-geloescht-fehler`).
- [ ] Die Zeile verschwindet mit der nächsten Aktion auf dem Tab und beim Verlassen des Tabs, ohne Zeitlimit; es gibt
      höchstens eine (.NET-Test).
- [ ] Gespeicherte oder neue Regel leuchtet kurz in der Liste, mit `prefers-reduced-motion` ohne Bewegung; aus einer
      Buchung angelegt erscheint „Regel angelegt“ mit Namen am Meldungsplatz der Seite.
- [ ] Jeder Fehler aus der Tabelle oben steht an seinem neuen Platz mit dem dort genannten Text (.NET-Tests je Fall).
- [ ] Alle Funktionen aus dem Funktionsabgleich mit „bleibt“ funktionieren wie vorher (bestehende Tests grün).
- [ ] Am PC (1024, 1440 px) bricht nichts: Zeile „gelöscht“ in Tabelle und Karte neben dem Panel (Sweep), und es steht
      kein Toast nach Löschen, Speichern oder einem Fehler am Tab.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome, dazu PC 1280), Abweichungen nur die in dieser
      Task genannten (gruppierte Karte statt Einzelkarten, Pfeile, Hinweis über der Liste, 48 px statt 44 px).
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Entscheidungen Roman (2026-11-19)
- **Zeile am Platz auch am PC?** Ja (ADR 0098): die Zeile „gelöscht“, das Leuchten und die Fehler an der Regel gelten
  auf allen Breiten, ohne zusätzlichen Toast am PC. Folge: ein Weg für alle Breiten; der PC-Toast kommt nur für die
  Meldung bei `FromEntry`, wenn sie dort keinen Ort hat (Kassenbuch: am Handy die Leiste).

## Notes
Captured 2026-11-19 aus ledger-vf5h6. Verfeinert 2026-11-19.

Skizze und Screenshots (`…` = `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):
- Skizze: `…/vf5h6/durchgehen-handy.html`, Quelltext `…/vf5h6/skizze.src.html` (Regeln Zeilen 475 bis 490, CSS
  `.mk-ghost` und `.undo` Zeilen 293 bis 296, Tabelle „Übrige Seiten“ Zeile 517)
- Skizze 412 px: `…/vf5h6/skizze-shots/33-regeln-geloescht-zeile.png`
- Heute 412 px: `…/shots/vf5h6/heute-regeln-geloescht-toast-412.png`

Abhängigkeiten: DS-Baustein und Meldungsweg aus design-system-ytgnc (DS-Modul vorläufig `Feedback`, Name dort
festgelegt). Meldungsplatz bei `FromEntry`: ledger-gwr9j (Kassenbuch-Leiste) und ledger-pz5wv (Zeile der letzten Aktion
im Durchgehen). Dasselbe Muster „gelöscht am Platz“ nutzt ledger-gwr9j für „Buchung gelöscht“; den Baustein baut ytgnc,
nicht diese Task.


## Outcome

Im Tab „Regeln“ gibt es keinen Toast mehr (Regel R3, ADR 0098), auf allen Breiten.

**Modell und Ablauf** (`src/Client/Components/Rules/Types.fs`, `State.fs`)
- Neu im `Model`: `Removed: RemovedRule option` (Regel, Stelle, Phase `Removing | Removed | Restoring | RestoreFailed of reason`), `Fault: RuleFault option` (Regel-Id oder keine, Text), `StaleLoad: string option`, `Flashed: RuleId option`, `LastMoved: RuleId option`.
- Die „nächste Aktion“ ist die reine Funktion `isNextAction` (RuleChosen, NewRuleRequested, MoveRequested, DeleteRequested, EntryRuleRequested, RulesTabLeft). Am Anfang von `updateWith` nimmt sie Zeile und Fehler weg. Ein zweites Löschen ersetzt die Zeile, die Antwort des ersten ändert sie nicht mehr.
- `DeleteRequested` setzt die Zeile mit Phase `Removing`, `RuleDeleted Ok` macht sie zu `Removed`. Bei `Error` kommt die Regel an ihre Stelle zurück, darunter „Nicht gelöscht: <Grund>“.
- `RestoreRequested` hat keine Argumente mehr und nimmt Regel und Stelle aus `Model.Removed`. Es wirkt nur in den Phasen `Removed` und `RestoreFailed` („Erneut“).
- `RuleRestored (rule, Ok rules)` übernimmt die Liste des Servers und nimmt die Zeile weg, die Regel leuchtet. Bei `Error` wird die Zeile `RestoreFailed`. Ist die Zeile schon weg, geht „Regel nicht wiederhergestellt“ an den Meldungsplatz der Seite.
- Speichern: `RuleCreated` trägt jetzt `origin: PanelOrigin`, `RuleChanged` die `RuleId`.
  - Gelungen aus dem Tab: die Regel leuchtet (`flashDurationMs = 700`, `FlashEnded`, passend zu `feedbackFlash` in styles.css).
  - Aus einer Buchung: `ShowFeedback (FeedbackOk, "Regel angelegt", Some name)`.
  - Gescheitert bei geschlossenem Formular: unter einer bestehenden Regel „Nicht gespeichert: <Grund>“, bei einer neuen Regel `ShowFeedback (FeedbackError, "Regel nicht angelegt", Some grund)`.
  - Formular offen: wie bisher im Formular.
- Reihenfolge gescheitert: die Liste lädt neu, `Fault` an `LastMoved` „Reihenfolge nicht gespeichert: <Grund>“. Fehlt die Regel nach dem Laden, steht der Satz über der Liste (`faultAboveList`).
- Neuladen gescheitert bei stehender Liste: `StaleLoad`, bleibt bis `RulesLoaded Ok`.
- Kategorien gescheitert: keine Meldung mehr, der Grund steht schon in der Auswahl.
- `ExternalMsg` ist `NoOp | ShowFeedback of FeedbackTone * action * detail option`. `ShowToast` und `RuleDeletedUndoable` gibt es im Tab nicht mehr.
- Abweichung vom Wortlaut des ersten Kriteriums: `grep -n "ShowToast" src/Client/Components/Rules/` findet nichts. Die Meldung bei `FromEntry` heißt `ShowFeedback` und geht über `State.ShowFeedback` / `FeedbackSlot.post`. Am Handy erscheint sie in der Leiste, am PC als Toast, weil sie dort keinen Ort hat.
- Neben `FromEntry` nutzen zwei seltene Fälle ohne Platz in der Liste denselben Weg: „Regel nicht angelegt“ bei geschlossenem Formular (verlangt die Task) und „Regel nicht wiederhergestellt“, wenn die Zeile schon weg ist.

**Wurzel** (`src/Client/State.fs`, `src/Client/Types.fs`)
- `UndoDeleteRule` ist aus `ToastActionKind` und `toastActionMsg` entfernt, der Zweig `RuleDeletedUndoable` ebenso.
- Neu ist `FeedbackSlot.message id tone action detail`: Aktion und Detail getrennt, im PC-Toast mit „ · “ bzw. bei Fehlern „: “ verbunden.

**Ansicht** (`src/Client/Components/Rules/View.fs`)
- `listRows` (rein, in `State.fs`) setzt die Zeile „gelöscht“ ohne Nummer an die Stelle, die Regeln danach rücken auf.
- In Tabelle und Karte ist die Zeile `data-testid="rule-deleted-row"`. Fehler stehen unter der Regel über `ErrorDisplay.inline'` (`rule-fault`) oder über der Liste (`rules-fault`).
- Der Hinweis „Regeln nicht neu geladen“ ist `Detail.notice` gelb mit „Neu laden“ (`rules-stale-notice`). Abweichung im Text: Badge „Regeln nicht neu geladen“, darunter „Die Liste kann veraltet sein: <Grund>“, statt eines einzigen Satzes. So ist das Muster aus dem Kassenbuch (`order-stale-bar`).
- Eine leuchtende Zeile trägt `Feedback.flash` und rollt per `scrollIntoView({block:"nearest"})` ins Bild. Die Zeilen haben dafür einen transparenten Rand.
- Leer gelöschte Liste: die Zeile steht statt „Keine Regeln“.

**DS** (`src/Client/DesignSystem/Feedback.fs`)
- Neu: `RemovedPhase` (`Removing`, `Removed of Act`, `Restoring`, `RestoreFailed of reason * Act`), `removedRowText` und `removedRowIn name phase` (rot mit Grund darunter, `data-phase`, in jeder Phase 48 px).
- `removedRow name undo` bleibt mit derselben Signatur (ledger-gwr9j nutzt es).
- Im Styleguide zeigt `/styleguide` alle vier Phasen. `standards/frontend/styleguide.md` §5 (Zeile Feedback) und §7.13 (Gelöschtes in einer Liste) sind ergänzt.

**Tests**
- `src/Tests/RulesListTests.fs`, 8 neue Tests:
  - Reihenfolge-Fehler an der Regel bzw. über der Liste.
  - Ein überholtes `FlashEnded` ändert nichts.
  - Rückgängig wirkt nicht, solange etwas läuft.
  - Jede nächste Aktion nimmt Zeile und Fehler weg, Nicht-Aktionen nicht, ein zweites Löschen ersetzt die Zeile.
  - Gescheitertes Wiederherstellen ohne Zeile geht an die Seite.
  - `listRows` mit Nummerierung.
  - Der Root reicht Aktion und Detail an `State.ShowFeedback` weiter (PC-Text „Regel angelegt · Rewe“).
  - `RuleCreated` trägt die Herkunft.
- Umgeschrieben, nicht gelöscht, wurden die Erwartungen auf `ShowToast` und `RuleDeletedUndoable`: Neuladen, Anlegen und Ändern leuchten (mit `CmdTiming.expectAfter flashDurationMs`), Löschen, Wiederherstellen, Kategorien, Root, FromEntry sowie die Aufzählungen „Speichern in jeder Reihenfolge“ (jetzt Leuchten, Fehler an der Regel bzw. Meldungsplatz) und „Reihenfolge in jeder Reihenfolge“ (Fehler an der Regel statt Toast).
- `ClientCommandTests.fs`: Rückgängig schickt Regel und Stelle aus der Zeile.
- Gegenprobe: vier Mutanten in `State.fs` gesetzt, 6 Tests waren rot, danach wiederhergestellt. Die Mutanten: `RulesTabLeft` keine Aktion, Zeile immer am Ende, Fehler beim Wiederherstellen löscht die Zeile, kein Leuchten nach dem Speichern.
- Sweep (`scripts/layout-states.mjs`):
  - `regeln-geloescht-toast` heißt jetzt `regeln-geloescht` (320, 390, 412, 1024, 1440). Gelöscht wird die Regel mit dem langen Namen, dann wartet der Zustand auf `rule-deleted-row` in Phase `removed`.
  - Neu ist `regeln-geloescht-fehler` (320, 412): `restoreRule` antwortet mit `DatabaseError`, die rote Zeile mit „Erneut“ bricht nicht.
  - Neuer Helfer `deleteLongRule`. Kopfkommentar in `scripts/regeln-tab.test.mjs` nachgezogen.
- `./verify.sh`: `VERIFY: PASS` (dotnet 5250 grün, node 354 grün).

**Vergleich mit der Skizze** (Screenshots in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/result-shots/categorization-t6j00/`)
- `regeln-geloescht-412.png` neben `skizze-shots/33-regeln-geloescht-zeile.png`: gestrichelte Zeile „„Eine Regel mit einem sehr l…“ gelöscht“ mit „Rückgängig“ im Akzent rechts, ohne Nummer. Die Regel darunter trägt „2“.
  - Genannte Abweichungen: gruppierte Karte statt Einzelkarten (die Zeile steht als Zeile in der Karte), Pfeile, Hinweis über der Liste, 48 statt 44 px.
  - Weiter: Radius 12 statt 10 px (aus design-system-ytgnc). Die schließende Anführung fällt mit in die Kürzung des Namens („…“), wie im DS-Baustein.
- `regeln-geloescht-320.png` / `-390.png`: „gelöscht“ und „Rückgängig“ ganz lesbar.
- `regeln-geloescht-fehler-320.png` / `-412.png`: rote gestrichelte Zeile „„Ein…“ nicht wiederhergestellt“, darunter der Grund in drei Zeilen, rechts „Erneut“. Dafür gibt es keine Skizzenansicht.
- `regeln-geloescht-1280.png` und `-1440.png` (Tabelle, Zeile über alle Spalten, voller Name) sowie `-1024.png` (Karte neben dem Panel): kein Toast.
- [human-eye] Gesamteindruck und Leuchten bleiben für Roman.

**Kann-Sammlung categorization**
- Der Knopf „Neu laden“ am Hinweis zeigt keinen Lauf (`Busy = false`), weil die Liste beim Neuladen `Success` bleibt.
- Bei 320 px bleibt vom langen Namen in der roten Zeile nur „„Ein…“ stehen.
