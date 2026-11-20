---
id: budgeting-sx9v5
title: "Targets und Kategorien verwalten am Handy ohne Toast (R3): Ergebnis am Target im Sheet und in der Zeile der Prüfung, Kategorien verwalten mit leuchtender Zeile und Fehler am Feld, Hinweis „nicht neu gelesen“ auf der Seite"
status: done
type: feature
context: budgeting
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, design-system-gkded]
blocks: []
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, budgeting-gtrgk, budgeting-c4ve4, budgeting-sn2sn, budgeting-e2z7v]
---

## Why
Regel R3 (design-system-ytgnc), Skizze ledger-vf5h6 Abschnitt 3, Tabelle „Übrige Seiten“, Zeilen „Budget, Targets“ und
„Budget, Kategorien verwalten“. Roman am 2026-11-19: am Handy kein Toast. Abgespalten aus budgeting-gtrgk.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am
2026-11-19 freigegeben). Für diese beiden Bereiche zeigt die Skizze kein eigenes Handy, nur die Tabellenzeilen:
- Targets: „am Target im Sheet bzw. in der Zeile der Prüfung“.
- Kategorien verwalten: „am Ort: neue Zeile leuchtet kurz; Fehler am Feld“.

Meldungen gehen über den Meldungsweg aus design-system-ytgnc (DS-Modul vorläufig `Feedback`). Sie stehen am hier
genannten Ort, am PC (ab 1024 px) genauso wie am Handy und ohne zusätzlichen Toast (Entscheidung Roman 2026-11-19,
ADR 0098). Nur Meldungen ohne Ort (am Handy die Leiste) kommen am PC als Toast unten rechts, höchstens einer, 5 s, mit ✕. Die Zeile im Sheet und
das kurze Leuchten sind dieselben Bausteine wie in budgeting-gtrgk (Skizze `.mk-msg`, `.flash-row`, `.flash-val`).

### Targets (Tab Target im Inspektor, Seite Targets prüfen)
| Heute (Toast, Datei:Zeile) | Mit R3 |
|---|---|
| `targetAnswerToast` Erfolg von der Seite: „Essen auswärts: Target jetzt 175 € monatlich.“ / „Target entfernt.“ (`TargetCheck.fs:582/583`, `targetWrittenText`, `targetWrittenUnnamedText`) | die Zeile der Kategorie in der Prüfung leuchtet kurz und zeigt den neuen Wert; darunter in der Zeile „✓ Target jetzt 175 € monatlich“ bzw. „✓ Target entfernt“ bis zur nächsten Aktion auf der Seite. Fällt die Kategorie dadurch aus ihrer Gruppe (kein Vorschlag mehr), steht die Bestätigung an ihrem bisherigen Platz als schmale Zeile „Essen auswärts: Target jetzt 175 € monatlich“ (Muster „Gelöschtes als Zeile am Platz“, ytgnc) |
| `takeAnswerToast` „inzwischen geändert“ / Ablehnung (`TargetCheck.fs:539/540`, `588/589`) | gelb bzw. rot in der Zeile der Kategorie in der Prüfung, Text unverändert |
| `targetCallLostToast` (`TargetCheck.fs:605`, `targetCallLostText`) | Seite Targets prüfen: in der Zeile der Kategorie; sonst Zeile im Sheet, Sheet zu: Leiste, am PC Toast |
| `targetAnswerToast` Ablehnung außerhalb der Seite, Formular zu (`TargetCheck.fs:588/589` Zweig `_`) | Zeile im Sheet, wenn das Sheet offen ist (anderer Tab), sonst Leiste, am PC Toast |
| `applyTargetRetried` „Target in YNAB geschrieben“ / „Target weiter nicht in YNAB: …“ (`TargetEdit.fs:361/362`) | am Target im Tab Target: die Markierung „noch nicht in YNAB“ verschwindet und der Target-Wert leuchtet kurz (Erfolg, kein Text); Fehler rot unter der Markierung mit dem Grund, die Markierung behält „Erneut senden“ |
| `applyTargetCheckReloaded` „Prüfung nicht neu gelesen, sie kann veraltet sein: …“ (`TargetCheck.fs:90`) | Hinweis oben auf der Seite Targets prüfen, gleiche Form wie „Beträge nicht neu gelesen“ in budgeting-gtrgk, mit „Neu laden“, bleibt bis ein Neu-Lesen gelingt (Zustand, alle Breiten) |

Antworten bei offenem Formular zeigt das Formular schon heute selbst (`formOpen`): **bleibt**.

### Kategorien verwalten (`#/budget/categories`, `Categories.fs`, `CategoriesView.fs`)
| Heute (Toast) | Mit R3 |
|---|---|
| `CategorySaved` Erfolg „<Name> angelegt“ / „Gespeichert“ / Notice (`Categories.fs:443`) | Formular schließt (wie heute), die neue bzw. geänderte Zeile leuchtet kurz in der Liste und wird ins Bild gerollt; kein Text |
| `GroupSaved` „Gruppe <Name> angelegt“ (`Categories.fs:456`) | zurück im Kategorie-Formular (wie heute) steht die neue Gruppe im Feld Gruppe, das Feld leuchtet kurz |
| `GroupSaved` „Gruppe gespeichert“ (`Categories.fs:459`) | der Kopf der Gruppe leuchtet kurz |
| `ActiveSaved` deaktiviert: „<Name> deaktiviert. In YNAB noch sichtbar, dort ausblenden.“ (`Categories.fs:490`, `deactivatedNotice`) | die Zeile steht im Bereich „Deaktiviert“, leuchtet kurz; darunter der Satz „In YNAB noch sichtbar, dort ausblenden.“ bis zur nächsten Aktion (der Hinweis ist Inhalt, er geht nicht verloren) |
| `ActiveSaved` / `Reactivated` aktiviert „<Name> ist wieder aktiv“ (`Categories.fs:490`, `540`) | die Zeile steht wieder in ihrer Gruppe und leuchtet kurz; kein Text |
| `CategorySaved`/`GroupSaved`/`ActiveSaved` Fehler ohne eigenes Formular „Nicht gespeichert: …“ (`Categories.fs:446/462/498`) | Formular offen: am Feld (`Input.groupWithError`, Regel `.claude/rules/task-ui-vorgaben.md`); sonst rot an der betroffenen Zeile |
| `OrderSaved` Fehler „Reihenfolge nicht gespeichert: …“ (`Categories.fs:525`) | rot unter dem Kopf der Gruppe, deren Reihenfolge scheiterte, bis zur nächsten Aktion |
| `Reactivated` Fehler „<Name> nicht aktiviert: …“ (`Categories.fs:543`) | rot an der Zeile im Bereich „Deaktiviert“ |
| `Reloaded` Fehler „Liste nicht neu geladen: …“ (`Categories.fs:367`) | Hinweis oben auf der Seite mit „Neu laden“, bleibt bis es klappt (Zustand) |

Die Weiterleitung `Categories.ShowToast` in `Budget/State.fs:435` entfällt bzw. wird der Meldungsweg aus ytgnc.

## Funktionsabgleich
- Target ändern, anlegen, entfernen, übernehmen (Tab und Seite), Erneut senden am Target, Pause: **bleiben**.
- Bestätigung per Toast: am Handy und am PC **gestrichen** (Roman, 2026-11-19), ersetzt wie oben. Am PC **bleibt** der
  Toast nur für Meldungen ohne Ort (Sheet bzw. Panel zu).
- Satz „In YNAB noch sichtbar, dort ausblenden.“ nach dem Deaktivieren: **bleibt**, am Ort.
- Kategorien anlegen, umbenennen, Gruppe anlegen, aktiv setzen, Reihenfolge, Bereich „Deaktiviert“: **bleiben**.

## Abweichungen von der Skizze
- Die Skizze nennt für Targets und Kategorien nur den Ort, keinen Text. Die Texte oben sind die heutigen, ohne Namen,
  wo die Zeile den Namen schon zeigt.
- Hinweise „nicht neu gelesen“ in allen Breiten (Zustand, kein Ergebnis), wie in budgeting-gtrgk (Entscheidung Roman
  2026-11-19).

## Acceptance criteria
- [ ] Keine Stelle in `TargetCheck.fs`, `TargetEdit.fs`, `Categories.fs` ruft mehr einen Toast für eine Meldung mit
      Ort; jede Tabellenzeile oben steht an ihrem Ort, am Handy und am PC gleich. Tests auf die reinen Funktionen, die heute den Toast bauen
      (`targetAnswerToast`, `takeAnswerToast`, `targetCallLostToast`, `applyTargetRetried`, `applyTargetCheckReloaded`,
      `Categories.update` je Fall): Ort und Text.
- [ ] Ab 1024 px nur die Meldungen ohne Ort (Sheet bzw. Panel zu) als Toast unten rechts, höchstens einer, 5 s, mit ✕.
- [ ] Leuchten der Zeile, des Felds, des Gruppenkopfs mit `prefers-reduced-motion: reduce` ohne Animation; `[human-eye]` für
      das Leuchten selbst.
- [ ] Fehler bei offenem Formular am Feld (`Input.groupWithError` oder `ErrorDisplay.inline'`), nie in der Leiste.
- [ ] Sweep: neue Zustände `targets-pruefen-uebernommen` (Zeile mit Bestätigung), `targets-pruefen-nicht-neu-gelesen`,
      `kategorien-nicht-neu-geladen`, `kategorien-fehler-reihenfolge` in `scripts/layout-states.mjs`, je bei 412 und
      1280 (am PC kein Toast); bestehende
      `budget-target`, `targets-pruefen`, `targets-pruefen-anpassen`, `kategorien*` bleiben grün.
- [ ] `[flow:targets-pruefen]` (`scripts/target-check.test.mjs:134`) prüft nach „übernehmen“ die Bestätigung in der Zeile
      und dass kein Toast im DOM steht.
- [ ] README budgeting: Stellen mit „Toast“ in den Bullets zu Targets prüfen, Target nach YNAB und Kategorien verwalten im
      README_DELTA nachgezogen.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome: Targets prüfen nach „übernehmen“, Tab Target
      nach Erneut senden, Kategorien verwalten nach Anlegen; dazu PC 1280 mit denselben Rückmeldungen am Ort, ohne
      Toast), Abweichungen nur die in dieser Task genannten.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Entscheidungen Roman (2026-11-19)
- **Rückmeldung am Ort am PC** (ADR 0098): Leuchten, Bestätigung in der Zeile und Fehler am Feld stehen auch am PC ohne
  zusätzlichen Toast; der Hinweis „nicht neu gelesen“ steht in allen Breiten auf der Seite (wie budgeting-gtrgk).
  Folge: der PC-Toast bleibt nur für Meldungen ohne Ort.

## Notes
Abgespalten 2026-11-19 aus budgeting-gtrgk.

**Skizze:** `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze.src.html`,
Tabelle „Übrige Seiten“ Zeilen um 509 bis 518; Formen `.mk-msg`, `.flash-row`, `.mk-hint` (Zeilen 270 bis 296). Kein
eigener Screenshot dieser Seiten in der Skizze; Vorbild für Zeile und Hinweis:
`.../scratchpad/vf5h6/skizze-shots/31-budget-sheet-geld.png` und `32-budget-stand-hinweis.png`, für die Zeile am Platz
`33-regeln-geloescht-zeile.png`.

**Code:** `src/Client/Components/Budget/TargetCheck.fs` (90, 535 bis 606), `TargetEdit.fs` (356 bis 362),
`Categories.fs` (110 `ShowToast`, 367, 443 bis 543), `CategoriesView.fs`, `TargetCheckView.fs`, `TargetEditView.fs`,
`Budget/State.fs` (435, 589, 605).


## Outcome

Regel R3 (ADR 0098) für Targets (Tab Target, Seite Targets prüfen) und die Seite Kategorien umgesetzt: jede Rückmeldung mit Ort steht an ihrem Ort, am Handy und am PC gleich, ohne Toast. `./verify.sh`: `VERIFY: PASS` (dotnet 5369, node 354).

**Targets prüfen** (`src/Client/Components/Budget/TargetCheck.fs`, `TargetCheckView.fs`, `Types.fs`)
- Statt `targetAnswerToast`, `takeAnswerToast`, `targetCallLostToast` jetzt `targetAnswerFeedback` und `targetCallLostFeedback` mit dem Ort als Typ `TargetFeedback` (`NoFeedback`, `InCheckRow`, `FeedbackLine`) und `takeAnswerNote`. `State.placeTargetFeedback` setzt `InCheckRow` als `CheckRowNote` an die Zeile (`noteInCheckRow`, mit Name und bisherigem Platz aus dem Modell vor der Antwort) und `FeedbackLine` als `ShowLine` (Zeile im Sheet, Sheet zu: Leiste, am PC Toast über `routeSheetLine`).
- Texte: in der Zeile ohne Namen „✓ Target jetzt 175 € monatlich“ / „✓ Target entfernt“ (`targetWrittenRowText`), gelb „Target nicht gespeichert: …“ für inzwischen geändert, rot für abgelehnt, rot `targetCallLostText` für Abbruch. Verlässt die Kategorie die Gruppe, steht die Rückmeldung am bisherigen Platz als schmale Zeile „Essen: Target jetzt 150 € monatlich“ (`checkNoteMovedText`, `checkNotePlace`: `NoteInRow`, `NoteAt`, `NoteBefore` wenn die Gruppe ganz wegfiel, `NoteHidden`). `targetWrittenText` und `targetWrittenUnnamedText` sind entfallen; lädt die Prüfung beim Antworten, steht die Bestätigung danach in der Zeile.
- Lebensdauer `endsCheckNote`: jede Aktion auf der Seite; Aufklappen, Laden und Antworten nicht. Leuchten der Zeile: `TargetCheckPage.Flash` (Zählung, Zeitgeber `State.checkFlashCmd`, 900 ms → `CheckFlashEnded`), nur bei geschriebenem Target.
- „Prüfung nicht neu gelesen“: `TargetCheckPage.Stale` (`ReadNotice`), `applyTargetCheckReloaded` gibt nur noch das Modell zurück, `startCheckReread`/`RereadTargetCheck` lesen still einmal, ein gelesener Stand nimmt den Hinweis weg. Ansicht `Feedback.stateHint "target-check-stale"` unter dem Kopf, alle Breiten. `SheetLineTone`, `ReadNotice`, `AvailableFlash` stehen dafür in `Types.fs` vor `TargetCheckPage`.
- Antworten bei offenem Formular zeigt das Formular selbst (unverändert).

**Tab Target, Erneut senden** (`TargetEdit.fs`, `InspectorView.fs`, `TargetFormView.fs`, `TargetEditView.fs`)
- `applyTargetRetried`: Tab Target der Kategorie offen → gelungen `TargetFlash` (Wert leuchtet, kein Text, Zeitgeber `State.targetFlashCmd` → `TargetFlashEnded`), gescheitert `TargetRetryFailed` (rot „Target weiter nicht in YNAB: …“ unter der Markierung über `ErrorDisplay.inline'`, `data-testid="target-ynab-retry-error"`, die Markierung behält „Erneut senden“). Sonst `ShowLine` (Zeile im Sheet bzw. Leiste). Ein neuer Versuch und jede Aktion aus `endsSheetLine` nehmen den Fehler weg.
- Das Leuchten des Betrags sitzt auf einer Hülle um das Feld (`TargetFormProps.AmountFlash`, `data-testid="target-amount-value"`), ohne Schlüssel je Leuchten, damit das Feld den Fokus behält; im Nur-Lese-Monat leuchtet `target-goal`.

**Kategorien verwalten** (`Categories.fs`, `CategoriesView.fs`)
- `Mark` (`CategoryLit`, `Deactivated`, `GroupLit`, `GroupFieldLit`, `ReactivateFailed`, `OrderFailed`) mit `MarkCount`/`MarkLit` und Zeitgeber `MarkFlashEnded` (`markFlashMs` 900); `endsMark` beendet sie mit der nächsten Aktion (nicht: Abschnitt aufklappen, Auswahl der Gruppe, Antworten). `update` ist jetzt `endsMark` + `transition`.
- Angelegt/gespeichert/wieder aktiv: Zeile leuchtet und wird ins Bild gerollt (`Revealed`), neue und geänderte Kategorien stehen sofort in der Liste (`withCategory`), sonst hätte das Leuchten vor dem stillen Neuladen geendet. Deaktiviert: Bereich „Deaktiviert“ klappt auf, Zeile leuchtet, darunter „In YNAB noch sichtbar, dort ausblenden.“ (`DeactivatedNotice`). Neue Gruppe aus dem Kategorie-Formular: das Feld Gruppe leuchtet. Gruppe gespeichert: Kopf leuchtet. Reihenfolge abgelehnt: rot unter dem Kopf (`categories-order-error`). Aktivieren abgelehnt: zurück in „Deaktiviert“, rot an der Zeile „Nicht aktiviert: …“ (`Reactivated` trägt jetzt die Kategorie-Id statt des Namens). Liste nicht neu geladen: `Stale` mit `Feedback.stateHint "categories-stale"`, `RereadList`.
- Fehler bei offenem Formular am Feld: Name und Gruppe über `Input.groupWithError` am Feld Name, Deaktivieren über `ErrorDisplay.inline'` unter dem Schalter (`Form.FailedCall`; `Form.Notice` ersetzt durch `Form.Deactivated`). Neu: während des Speicherns öffnet kein anderes Formular (`NewCategory`, `EditCategory`, `NewGroup`, `RenameGroup`), damit die Antwort ihr Formular findet. Ohne Formular (nur nach „Zurück“) bleibt die Antwort eine Meldung ohne Ort (Leiste, PC-Toast), Abweichung von „sonst rot an der betroffenen Zeile“ mit Grund: die Zeile ist dann nicht zu sehen.
- `styles.css`: in einer Karte mit `overflow-clip` leuchtet eine Zeile innen (`feedbackFlashInset`, Skizze `.flash-row`), der Ring außen wäre abgeschnitten; mit `prefers-reduced-motion` steht der Ring ohne Animation. [human-eye] für das Leuchten selbst.

**Tests** (12 neu, bestehende umgestellt)
- `src/Tests/TargetCheckClientTests.fs`: Ort und Text je Antwort und Abbruch (`targetAnswerFeedback`, `takeAnswerNote`, `targetCallLostFeedback`, `targetWrittenRowText`, `checkNoteMovedText`), neue Liste „Rückmeldung am Ort“ (Lebensdauer, Leuchten mit Zeitgeber, Platz, Hinweis „nicht neu gelesen“ mit Neu laden). `TargetToYnabClientTests.fs`: Erneut senden im offenen Tab und bei anderem Tab. `TargetChangeClientTests.fs`: Ablehnung und Abbruch bei offenem Sheet in der Zeile, geschlossen in der Leiste. `CategoryAdminClientTests.fs`: neue Liste „Rückmeldung am Ort“ (Gruppe umbenannt, Lebensdauer, Zeitgeber, Neu laden, Fehler am Feld, kein zweites Formular beim Speichern), bestehende Toast-Erwartungen umgestellt.
- Gegenprobe (lokal, wiederhergestellt): `endsCheckNote` immer false, Erfolg von der Seite ohne Rückmeldung, `marked` ohne Leuchten, `applyTargetRetried` nie im Tab: 14 Tests rot.
- Browser: `[flow:targets-pruefen]` prüft nach „übernehmen“ die schmale Zeile „Strom q4v8n: Target jetzt 150 € monatlich“ in „Gibst du mehr aus?“, kein Element in `.toast-layer` und keine Meldung in der Leiste. Sweep: neue Zustände `targets-pruefen-uebernommen`, `targets-pruefen-nicht-neu-gelesen`, `kategorien-nicht-neu-geladen`, `kategorien-fehler-reihenfolge` je bei 412 und 1280 mit Prüfung „kein Toast“, ohne neue Ausnahme; `budget-target`, `budget-target-anlegen`, `targets-pruefen`, `targets-pruefen-anpassen`, `kategorien*` grün.

**README_DELTA**: geprüft mit `npm run check:readme-delta -- --bc budgeting` (Exit 0, „Ganz nachlesen: keine Einträge“). Ersetzt: „Targets prüfen“, „Seite Kategorien“, „Nicht in YNAB (Target)“.

**Echte Namen** statt der vorläufigen der Task: Baustein `Feedback.sheetLine` (Zeile, `data-testid="feedback-sheet-line"`, in der Prüfung umhüllt von `target-check-row-line` bzw. `target-check-moved-line`), `Feedback.flash`, `Feedback.stateHint`; die Meldung ohne Ort geht über `ShowLine`/`routeSheetLine` bzw. `Categories.ShowToast` in den Meldungsplatz `Model.Feedback`.

**Skizzenvergleich** (Bilder in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/result-shots/budgeting-sx9v5/`; die Skizze zeigt für diese Seiten nur die Tabellenzeilen, Vorbilder `31-budget-sheet-geld.png`, `32-budget-stand-hinweis.png`, `33-regeln-geloescht-zeile.png` in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/vf5h6/skizze-shots/`)
- `targets-pruefen-uebernommen-412.png` neben `31-…` und `33-…`: in der Karte „Gibst du mehr aus?“ steht an Essens bisherigem Platz „✓ Essen: Target jetzt 150 € monatlich“ grün halbfett wie `.mk-msg`. Unterschied (in der Task genannt): Zeile am Platz mit Namen statt gestrichelter Zeile „gelöscht“, weil nichts gelöscht ist. `-1280.png`: dieselbe Zeile in der breiten Liste, kein Toast.
- `targets-pruefen-nicht-neu-gelesen-412.png` neben `32-…`: gelb getönter Hinweis „! Prüfung nicht neu gelesen, sie kann veraltet sein.“, Grund kleiner darunter, „Neu laden“ im Akzent rechts; in Essens Zeile rot der Satz des Abbruchs. Unterschied wie in budgeting-gtrgk: Grund in zweiter Zeile. `-1280.png`: gleich, kein Toast.
- `budget-target-erneut-gesendet-412.png`/`-1280.png`: das Feld Target mit Rand und Ring im Akzent (Leuchten), kein Text, kein Toast; die Markierung bleibt im Bild, weil der Mock den Monat unverändert liefert. `budget-target-erneut-fehler-412.png`/`-1280.png`: unter „Nicht in YNAB … Erneut senden“ rot „Target weiter nicht in YNAB: …“.
- `kategorien-angelegt-412.png`/`-1280.png`: „Haustier“ steht am Ende von „Alltag“ mit Rahmen im Akzent (Leuchten innen), kein Text, kein Toast. `kategorien-nicht-neu-geladen-412.png`/`-1280.png`: Hinweis „! Liste nicht neu geladen, sie kann veraltet sein.“ mit „Neu laden“, „Essen gehen“ schon in der Liste. `kategorien-fehler-reihenfolge-412.png`/`-1280.png`: rot mit Symbol „Reihenfolge nicht gespeichert: …“ unter dem Kopf „Alltag“.
- Abweichungen nur die in der Task genannten (Texte ohne Namen in der Zeile, Hinweise in allen Breiten) und die oben begründete (Antwort ohne Formular nach „Zurück“ als Meldung ohne Ort).

**Kann-Sammlung budgeting**
- Kommt die Antwort auf „übernehmen“, nachdem man Targets prüfen verlassen hat, steht sie an der Zeile der verlassenen Seite und ist beim nächsten Betreten weg (vorher kam ein Toast). Die Seite weiß nicht, ob sie gezeigt wird.
- Nach einem Abbruch verspricht der rote Satz in der Zeile „Der Stand wird gleich neu gelesen.“ auch dann noch, wenn das Neu-Lesen gescheitert ist (der Hinweis oben sagt es).
- Eine Ablehnung aus dem Tab Target, während das Sheet einer anderen Kategorie offen ist, steht als Zeile in deren Sheet (wie die Pause aus budgeting-gtrgk).
