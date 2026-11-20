---
id: design-system-gkded
title: "Rückmeldung am Handy ohne Toast (Regel R3), Teil 2: ein Meldungsplatz in der App statt Toast-Liste, Leiste unter 1024 px mit „+ Buchung“, Toast am PC höchstens einer, gleiche Wische zusammengefasst, alte Toasts übergangsweise in der Leiste, Sweep-Invariante mit Gegenbeweis"
status: done
type: feature
context: design-system
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc]
blocks: [ledger-pz5wv, ledger-gwr9j, budgeting-gtrgk, budgeting-sx9v5, budgeting-p3sk0, categorization-t6j00, banking-import-vwhfm, banking-import-ns4re]
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, design-system-ytgnc, design-system-vf94m]
---

## Why
Regel R3 (ADR 0098, Roman 2026-11-19): am Handy kein Toast, immer nur eine Meldung an einem festen Platz. Heute hält
die App eine Liste (`State.Model.Toasts`), jede Meldung ist ein eigener Toast, am Handy oben oder unten je nach
Aktion, nach 5 s weg. Romans Screenshots: zwei Toasts zugleich, drei gestapelt über Liste und „+ Buchung“.
design-system-ytgnc liefert den Baustein `Feedback`; diese Task verdrahtet ihn, damit nach ihr **nie mehr zwei
Rückmeldungen gleichzeitig** stehen und am Handy keine etwas verdeckt. Die Seiten setzen danach die genaueren Plätze
(am Feld, im Sheet, Zeile „gelöscht“) in ihren Tasks.

## What

Vorlage: Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, freigegeben
2026-11-19), Abschnitt 2 „Eine Meldung, ein Platz“ und Abschnitt 3 „Übersicht“. Pfade lokal in den Notes.
Grenze Handy/PC: **unter 1024 px die Leiste, ab 1024 px der Toast** (Begründung in design-system-ytgnc). Der Toast am
PC ist nur für Meldungen ohne Ort (Roman, 2026-11-19); Rückmeldungen am Ort stellen die Folge-Tasks auch am PC um, ohne
zusätzlichen Toast. Bis dahin zeigt der PC-Toast die heutigen Meldungen der Seiten (Übergang).

### 1. Ein Platz im Modell (`src/Client/Types.fs`, `src/Client/State.fs`)
`Model.Toasts: Toast list` wird `Model.Feedback: FeedbackMessage option`. Vorschlag (Typen in `Types.fs` neben
`ToastAction`, Modul `FeedbackSlot` statt `module Toast`):

```fsharp
type FeedbackTone = FeedbackOk | FeedbackWarning | FeedbackError | FeedbackRunning | FeedbackInfo
/// Gleichartige Aktionen, die zu einer Meldung zusammenlaufen. Heute nur „geprüft“ per Wisch.
type FeedbackGroup = ReviewedBySwipe
type FeedbackMessage = {
    Id: Guid
    Tone: FeedbackTone
    Action: string                         // obere Zeile, nie gekürzt
    Detail: string option                  // untere Zeile
    Act: ToastAction option                // „Rückgängig“, „Erneut senden“, „Neu laden“ (Label und Kind wie heute)
    Group: (FeedbackGroup * int) option    // Gruppe und Zahl der zusammengefassten Aktionen
    ToastHidden: bool                      // nur PC: Toast nach 5 s oder ✕ ausgeblendet; die Leiste zeigt weiter
    ToastExiting: bool                     // nur PC: Abgangsanimation läuft
}
module FeedbackSlot =
    val post : current: FeedbackMessage option -> incoming: FeedbackMessage -> FeedbackMessage
    val ofToast : id: Guid -> text: string -> ToastType -> ToastAction option -> FeedbackMessage
    val splitText : string -> string * string option
```

- **`post` (rein, Zusammenfassen):** Tragen aktuelle und neue Meldung dieselbe Gruppe und lassen sich ihre `Act`
  vereinen, wird daraus eine Meldung mit Zahl n+1, Text der Gruppe („{n} geprüft“), ohne Detail, `Act` mit allen
  Rückgängig-Schritten: `UndoReviewMark a` + `UndoReviewMark b` → `UndoReviewMarkBatch [a; b]`, eine Batch plus ein
  weiterer Wisch hängt an. „Rückgängig“ läuft dann über den vorhandenen Weg `StartUndoReviewMarkBatch`
  (`Components/Ledger/State.fs` Zeile 4048, lädt die Liste neu). Sonst ersetzt die neue Meldung die alte. Die
  Gruppe setzt `ofToast` aus der Aktion: `UndoReviewMark` (nur der Wisch setzt sie, `Ledger/State.fs` Zeile 2444) →
  `ReviewedBySwipe`, Zahl 1. Ein Wisch, dessen Ergebnis kein Rückgängig hat (Rückschreiben gescheitert, Fehler), hat
  keine Gruppe und ersetzt.
- **`splitText` (rein, Übergang):** teilt einen heutigen Toast-Text am ersten „: “ oder „ · “ in Aktion und Detail
  („Prüfvermerk nicht in YNAB: Grund“ → „Prüfvermerk nicht in YNAB“ / „Grund“; „Buchung angelegt · Konto · Betrag“ →
  „Buchung angelegt“ / „Konto · Betrag“). Ohne Trenner ist alles die Aktion (ganz lesbar, bricht um).
- **`ofToast`:** `ToastSuccess` → Ok, `ToastWarning` → Warnung, `ToastError` → Fehler, `ToastInfo` → Info.

### 2. Wie Seiten Meldungen setzen
- Die Komponenten bleiben bei ihren Nachrichten `ShowToast`/`ShowToastWithAction` (`Components/*/Types.fs`; Budget 44,
  Ledger 103, Settings 27, Rules 8, Fetch 2 Treffer), die Wurzel mappt sie. Keine Komponente ändert sich in dieser
  Task, ihre Client-Tests (`ShowToast (…)` als Ergebnis in `src/Tests/*ClientTests.fs`) bleiben gültig.
- Wurzel `State.fs`: `ShowToast`/`ShowToastWithAction` (Zeilen 61, 62, 672 bis 676) setzen
  `Feedback = Some (FeedbackSlot.post model.Feedback (FeedbackSlot.ofToast …))`. Neu `ShowFeedback of FeedbackMessage`
  (ohne `splitText`), damit die Folge-Tasks Aktion und Detail getrennt setzen können; die Komponenten bekommen den Weg
  dort, nicht hier.
- Übrige Stellen der Wurzel, die heute Toasts erzeugen, laufen über denselben Weg: Fetch (Zeilen 565, 571), Settings
  (712), Rules (824, 826 „Regel gelöscht“), Ledger (911 bis 913), Pins (972, 986), Budget (1043 bis 1045).
- `ToastActionClicked` (Zeile 678) wird `FeedbackActClicked of Guid`: nur wenn die Id die aktuelle Meldung ist, leert
  er den Platz und schickt `toastActionMsg` (bleibt, reine Funktion, viele Tests nutzen sie). Eine ältere Id tut nichts.
- **Bleibt bis zur nächsten Aktion oder bis man die Seite verlässt:** `UrlChanged` leert den Platz, wenn sich
  `pageKey` ändert (Kassenbuch, Buchungs-Detail und Regeln teilen „Ledger“, dort bleibt die Meldung, etwa nach
  „Buchung löschen“ oder „Regel löschen“). Am Handy kein Zeitlimit und kein ✕.
- **PWA-Hinweis** „Neue Version verfügbar.“ mit „Neu laden“ (`updateNoticeToast`, Zeilen 416 bis 448) wird eine
  Info-Meldung im selben Platz, ohne Zeitlimit auch am PC (wie heute). Ersetzt eine andere Meldung ihn oder leert ein
  Seitenwechsel den Platz, gilt das als Schließen (`noticeClosed`: `NoticeShown = false`), der nächste saubere
  Vordergrund kann ihn wieder auslösen (`Pwa.decide`), wie heute nach ✕.

### 3. Zwei Ansichten aus demselben Zustand (`src/Client/View.fs`)
- Statt `Toast.renderItems` (Zeile 241) rendert die App beides aus `model.Feedback`, per CSS getrennt (kein
  Breiten-Lesen im Code): die Leiste `Feedback.bar` in einem `lg:hidden`-Behälter, den Toast in einem
  `hidden lg:flex`-Behälter.
- **Toast ab 1024 px:** höchstens einer, unten rechts wie heute (über Auswahl- und Tastenleiste, `toast-above-bar`),
  Ebene `toast-layer` (90) über Panel und Dialog wie heute. Aus der Meldung: Text „Aktion · Detail“ bzw.
  „Aktion: Detail“ wie heute, Variante aus dem Ton (Läuft als Info), Aktion als Knopf. Das Verhalten am PC bleibt das
  von heute: nach 5 s oder ✕ geht er sanft ab (`ToastExiting`, dann `ToastHidden`, 220 ms, `Toast.exitDurationMs`
  bleibt), außer Läuft und dem PWA-Hinweis. Die Zeitgeber tragen die Id der Meldung, ein Zeitgeber einer schon
  ersetzten Meldung tut nichts. Der Toast-Behälter für das Handy (`container` mit `top-16 inset-x-4`) und
  `actionContainer` mit `.toast-above-nav` entfallen in `Toast.fs` und `styles.css`; `renderItems`/`renderList`
  werden eine Funktion für einen Toast (oder entfallen, wenn die Styleguide-Demo sie nicht braucht).
- **Leiste unter 1024 px:** `Feedback.bar` mit der Meldung und, auf Kassenbuch und Übersicht, „+ Buchung“:
  - Kassenbuch (`isLedgerPage`, Zeile 222): der Knopf öffnet `OpenCapture`, `HideFrom` lg, sichtbar nach
    `captureFabVisible` (nicht bei offenem Formular, nicht im Auswahl-Modus). `Components.Ledger.View.captureFab`
    (Zeile 4560), der Abstandshalter `captureFabSpacer` (4043) und `.ledger-capture-fab` in `styles.css` (2720)
    entfallen.
  - Übersicht (Zeile 226): der Knopf `CaptureFromOverview`, `HideFrom` md (ab 768 px trägt der Kopf „Buchung“),
    `Quiet = captureQuiet model.Overview`, nicht bei offenem Erfassen. `Components.Overview.View.captureFab` (745) und
    `.overview-capture-fab` entfallen.
  - Andere Seiten: Leiste nur mit Meldung (`Feedback.barVisible`).
  - Lage: direkt über der Navigation unten; ohne sie (768 bis 1023 px mit Rail, Durchgehen, Auswahl-Modus) am unteren
    Rand, im Auswahl-Modus direkt über der Auswahl-Leiste (`selectionBars`). Von 768 bis 1023 px beginnt sie rechts
    der Rail (`NavWidths`).
  - **Platz freihalten:** die Leiste schreibt ihre gemessene Höhe nach `--feedback-bar-height`
    (`Viewport.mirrorChildHeight`, Muster `--ledger-bottom-bar-height`), ohne Leiste 0. Der untere Innenabstand der
    Seitenhülle (`Navigation.fs` `shellOffset`, `pb-24`) rechnet sie mit ein, sodass das Ende jeder Seite über der
    Leiste steht, auch wenn die Aktion umbricht.
  - Ebene unter Sheets: ein offenes Sheet deckt die Leiste. Im Übergang bis ledger-gwr9j gewollt (Entscheidungen
    Roman unten).

### 4. Sweep-Invariante und Regressionstest (`scripts/layout-invariants.mjs`, `layout-states.mjs`, `layout-invariants.test.mjs`)
- **Neue Invariante 12 „Rückmeldung“:** unter 1024 px steht kein sichtbares Element in `.toast-layer` (kein Toast am
  Handy) und höchstens eine Leiste `[data-feedback="bar"]`; die Leiste überdeckt kein sichtbares Bedienelement und
  keinen Text außerhalb ihrer selbst (außer in einer offenen Ebene darüber), oben und am Ende gescrollt; ihre Knöpfe
  (`[data-feedback-action]`, „+ Buchung“) sind das oberste Element, solange keine Ebene offen ist; sie liegt mit ihrer
  Unterkante auf der Oberkante der Navigation unten bzw. der Auswahl-Leiste (ein Pixel Toleranz). Ab 1024 px steht
  höchstens ein Toast. Die Liste der Invarianten im Kopfkommentar von `layout-invariants.mjs` bekommt Punkt 12, bei
  Invariante 5 und 11 entfällt dort „und der Toast“ für das Handy.
- **Gegenbeweise** in `layout-invariants.test.mjs` (je einer, `[layout]`): (a) ohne den freigehaltenen Platz
  (`--feedback-bar-height` auf 0 per CSS) meldet die Invariante die letzte Zeile als verdeckt; (b) ein am Handy
  sichtbar gemachter Toast meldet „Toast am Handy“; (c) **Regression design-system-vf94m** (Toast verdeckt „+ Buchung“):
  im Zustand „Meldung mit rundem +“ schiebt CSS die Meldung über den Knopf (etwa negativer Rand), die Invariante meldet
  den Knopf als verdeckt. Nach Doktrin Punkt 8 zusätzlich: den Fehler von vf94m (Toast mit Aktion unten über dem
  schwebenden Knopf) lokal herstellen, zeigen, dass die Invariante rot wird, Ergebnis im Outcome.
- **Zustände** (`layout-states.mjs`): neu „kassenbuch-meldung“ (nach einem Wisch geprüft, Leiste mit Meldung und rundem
  „+“, Breiten 320, 390, 412, 900), „kassenbuch-3-geprueft“ (drei Wische, eine Meldung „3 geprüft“, Breite 412),
  „uebersicht-meldung“ (390, 412). Anpassen: Zustände, die auf `[data-toast-action="true"]` warten (Zeilen 477,
  1694, 1710, 1951), warten unter 1024 px auf `[data-feedback-action="true"]` in der Leiste, ab 1024 px wie heute;
  „kassenbuch-kategorie-toast“ (1681, Regression c819eeb: Toast hinter dem Detail-Sheet) prüft die Meldung am Handy
  nach dem Schließen des Sheets in der Leiste und bekommt eine PC-Breite (1280), an der der Toast über dem Panel liegt;
  der Gegenbeweis (`layout-invariants.test.mjs` Zeile 178) läuft dann bei 1280. Die Zustände, die „+ Buchung“ über
  `.ledger-capture-fab` öffnen (Zeilen 1480 bis 1591), nehmen `[data-testid="ledger-capture-fab"]`. Der Gegenbeweis
  „letzte Buchung unter + Buchung“ (Zeile 196, `ledger-capture-fab-spacer`) geht in Gegenbeweis (a) auf; Doktrin
  Punkt 8 beim Umzug: Fix aus ledger-at63w zurückdrehen und zeigen, dass (a) rot wird.

### 5. Übrige Tests
- `src/Tests/ToastLifecycleTests.fs` wird `FeedbackSlotTests.fs` (Eintrag in `Tests.fsproj`): `post` (ersetzen,
  zusammenfassen 1 → 2 → 3 mit vereintem Rückgängig, andere Aktion nach Gruppe ersetzt, Gruppe nach anderer Aktion
  beginnt bei 1, Wisch ohne Rückgängig ersetzt), `splitText` (beide Trenner, erster gewinnt, ohne Trenner), Abgang des
  PC-Toasts (Zeitgeber einer ersetzten Meldung tut nichts, doppeltes ✕ plant nichts doppelt), `FeedbackActClicked` mit
  alter Id tut nichts, Seitenwechsel leert, gleicher `pageKey` nicht, PWA-Hinweis ersetzt setzt `NoticeShown = false`.
  Wurzel-`update` unter .NET, wo kein `Client.Api` berührt wird; Zeitgeber nach `src/Tests/CmdTiming.fs`.
- Browser-Tests, die am Handy einen Toast suchen, suchen die Leiste: `ledger-capture`, `ledger-category-change`,
  `ledger-memo`, `ledger-lena`, `ledger-lena-batch`, `ledger-payee`, `ledger-split-editor`, `regeln-tab`,
  `durchgehen`, `move-money` (`grep -il toast scripts/*.test.mjs`); am PC bleibt `data-toast-action`. Keine neuen
  Flow-Tests. Die Exporte des DS (`export-design-system*.test.mjs`) nachziehen, wenn sie `renderItems` nennen.

### Funktionsabgleich (heute in BB → mit dieser Task)
| Funktion heute | Stand |
|---|---|
| Toast ohne Aktion am Handy oben (`top-16`) | gestrichen (Roman, 2026-11-19): Leiste |
| Toast mit Aktion am Handy unten über der Navigation | gestrichen (Roman, 2026-11-19): Leiste |
| Mehrere Toasts zugleich, gestapelt | ändert sich: eine Meldung, die neue ersetzt, gleiche Wische fassen zusammen |
| Auto-Dismiss nach 5 s, ✕ | am Handy gestrichen (Roman, 2026-11-19); am PC bleibt |
| Sanfter Abgang (220 ms) | am PC bleibt; am Handy wird die Meldung ersetzt, Eingang `msgIn` |
| Toast über offenem Sheet (`toast-layer` 90, Regression c819eeb) | am PC bleibt; am Handy ändert sich (Leiste unter dem Sheet, bis ledger-gwr9j, Entscheidung Roman) |
| „Rückgängig“ an jedem Toast mit `ToastActionKind` (16 Arten), „Erneut senden“ (`RetryBudgetMove`) | bleibt, in der Leiste bzw. im Toast |
| Toast am PC über Auswahl- und Tastenleiste (`toast-above-bar`) | bleibt |
| PWA-Hinweis „Neue Version verfügbar.“ mit „Neu laden“, ohne Zeitlimit | bleibt, im selben Platz; eine neue Meldung ersetzt ihn (gilt als Schließen) |
| Schwebender Knopf „+ Buchung“ Kassenbuch bis 1023 px, Übersicht bis 767 px, ausgeblendet bei Formular und Auswahl | ändert sich: in der Leiste, gleiche Breiten, gleiche Bedingungen, rund bei Meldung |
| Übersicht: „+ Buchung“ ruhig, wenn „Zu tun“ Einträge hat (§7.14) | bleibt (`Quiet`) |
| Abstand unter der Liste für den schwebenden Knopf (`captureFabSpacer`) | ändert sich: `--feedback-bar-height` |
| Toast überlebt einen Seitenwechsel bis 5 s | ändert sich: Seitenwechsel leert den Platz (R3) |

### Abweichungen von der Skizze (mit Grund)
- Bis jede Seite umgestellt ist, stehen ihre alten Texte in der Leiste, geteilt nach `splitText` (etwa „REWE geprüft“
  statt „Geprüft“ / „REWE“). Grund: Übergang, die Texte setzen die Folge-Tasks.
- „Rückgängig“ nach einem zurückgenommenen Schritt meldet noch „… zurückgesetzt“ (Skizze: entfällt). Grund: kommt aus
  den Komponenten, die Folge-Tasks streichen es.
- Detail-Sheet am Handy: Meldung erst nach dem Schließen sichtbar (Skizze: am Feld). Grund: Platz 1 baut ledger-gwr9j.

## Entscheidungen Roman (2026-11-19)
- **Meldung bei offenem Sheet im Übergang:** Bis ledger-gwr9j das Kassenbuch-Detail umstellt, liegt die Leiste unter
  einem offenen Sheet (nach „Kategorie ändern“ im Detail sieht man die Meldung erst nach dem Schließen). Antwort: in
  Ordnung, ledger-gwr9j wird direkt danach gebaut. Folge: die Leiste liegt nie über einem Sheet, die Abweichung oben
  bleibt bis gwr9j.
- **PC-Toast nach 5 s weg oder bis zur nächsten Aktion?** Antwort: höchstens einer, geht nach 5 s, mit ✕, wie heute.
  Folge: der Abgang des PC-Toasts bleibt wie unter 3. beschrieben.
- **Rückmeldung am Ort am PC:** Rückmeldungen, deren Ergebnis man am Ort sieht, stehen auch am PC am Ort und ohne
  zusätzlichen Toast; der PC-Toast gilt nur für Meldungen ohne Ort (ADR 0098). Folge: diese Task ändert die Quellen
  nicht, die Folge-Tasks nehmen ihre Meldungen am Ort auch am PC aus dem Toast.

## Acceptance criteria
- [ ] `Model.Toasts` ist `Model.Feedback: FeedbackMessage option`; jede heutige Toast-Quelle der Wurzel (Zeilen oben)
      setzt den Platz über `FeedbackSlot.post`; `ShowFeedback` existiert.
- [ ] `FeedbackSlot.post` fasst gleiche Wische zusammen („2 geprüft“, „3 geprüft“, ein Rückgängig für alle über
      `UndoReviewMarkBatch`), jede andere Meldung ersetzt; reine Funktion mit Tests (Fälle unter 5.), Gegenprobe:
      ohne den Zusammenfass-Zweig wird ein Test rot.
- [ ] `splitText` und `ofToast` mit Tests; Klick auf die Aktion einer alten Id tut nichts; Seitenwechsel leert, gleicher
      `pageKey` nicht; PWA-Hinweis bleibt ohne Zeitlimit und gilt beim Ersetzen als geschlossen.
- [ ] Unter 1024 px kein Toast; die Leiste zeigt die Meldung bis zur nächsten Meldung oder zum Seitenwechsel, ohne ✕
      und ohne Zeitlimit; ab 1024 px höchstens ein Toast unten rechts, 5 s und ✕ wie heute (Entscheidung Roman).
- [ ] „+ Buchung“ steht auf Kassenbuch (bis 1023 px) und Übersicht (bis 767 px) in der Leiste, breit ohne Meldung,
      rund mit Meldung, mit `data-testid` `ledger-capture-fab` bzw. `overview-capture-fab`; die schwebenden Knöpfe,
      ihr Abstandshalter und ihre CSS-Klassen sind entfernt; Übersicht ruhig wie heute.
- [ ] Das Ende jeder Seite steht über der Leiste (`--feedback-bar-height`), auch bei umbrechender Aktion.
- [ ] Sweep-Invariante 12 mit den Gegenbeweisen (a) bis (c); Regression design-system-vf94m und Umzug des Gegenbeweises
      aus ledger-at63w nach Doktrin Punkt 8 belegt (Fix zurückgedreht, rot gesehen, im Outcome); neue und angepasste
      Zustände wie unter 4.; Sweep grün bei allen Breiten.
- [ ] Angepasste Browser-Tests grün, keine neuen Flow-Tests.
- [ ] Kassenbuch und Übersicht neben der Skizze verglichen: Screenshots bei 412 px (Ruhe, nach einem Wisch, nach drei
      Wischen, Übersicht mit Meldung) neben `skizze-shots/20-…`, `22-…`, `23-…`, `30-uebersicht-leiste.png`, dazu PC
      1280 mit einem Toast; Abweichungen nur die oben genannten. [human-eye] für den Gesamteindruck.
- [ ] Styleguide §6 (Zwei-Phasen-Abgang nur noch für den PC-Toast, Zustand `Model.Feedback`) und §7.13 (Übergang)
      nachgezogen.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Verfeinert 2026-11-19 als Teil 2 von design-system-ytgnc. Übernimmt aus der ersten Fassung von ytgnc die Kriterien
„Zusammenfassen gleicher Aktionen als reine Funktion mit Test“ und „Sweep-Invariante mit Gegenbeweis (Regressionstest
aus design-system-vf94m)“. Abgrenzung zu den Folge-Tasks: hier der Platz, der Übergang, „+ Buchung“ in der Leiste und
das Zusammenfassen der Wische; dort die Texte („Geprüft“ / Payee, „Angelegt“, „Abrufen läuft …“), Platz 1 und 2 und
die Zeile „gelöscht“.

Skizze lokal (`…` = `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):
- `…/vf5h6/durchgehen-handy.html`, Quelltext `…/vf5h6/skizze.src.html` (Leiste: CSS Zeilen 235 bis 249, JS 995 bis
  1110 mit `setMsg`, `paintMsg` und dem Zusammenfassen in `bindRow`, Zeilen 1104 bis 1108)
- `…/vf5h6/skizze-shots/20-kassenbuch-ruhe.png`, `22-kassenbuch-geprueft.png`, `23-kassenbuch-3-geprueft.png`,
  `25-kassenbuch-abgerufen.png`, `26-kassenbuch-abrufen-fehler.png`, `27-kassenbuch-angelegt.png`, `30-uebersicht-leiste.png`
- heute: `…/shots/vf5h6/heute-toast-412.png`, `heute-toast-150-412.png`, `heute-toast-600-412.png`,
  `heute-kassenbuch-wisch-412.png`, `heute-uebersicht-412.png`

Hinweis Verifier design-system-ytgnc (2026-11-20): das Muster-Bullet der design-system-README nennt noch „Toast … Mobile oben über der Bottom-Nav“ (ADR 0007); mit dieser Task nachziehen.


## Outcome

Ein Meldungsplatz statt der Toast-Liste (Regel R3, ADR 0098). Die Seiten senden weiter `ShowToast`, die Wurzel setzt daraus den Platz.

**Modell und Wurzel**
- `src/Client/Types.fs`: `FeedbackTone`, `FeedbackGroup` (`ReviewedBySwipe`), `FeedbackMessage` und `module FeedbackSlot` mit `post`, `ofToast`, `splitText`, `toastText`, `groupText`, `isUpdateNotice` und `autoDismisses`. Der alte `Toast`-Record und `markExiting`/`isExiting`/`remove` sind entfernt. `Toast.exitDurationMs` bleibt.
- Abweichung von der Signatur in der Task: `FeedbackMessage` hat zusätzlich das Feld `ToastSeparator` („ · “ oder „: “). Grund: Der PC-Toast soll seinen Text wie heute zeigen, also „Aktion · Detail“ bzw. „Aktion: Detail“. `splitText` allein verliert den Trenner. `splitText` hat die Signatur aus der Task.
- `src/Client/State.fs`: `Model.Feedback: FeedbackMessage option`. Alle Quellen der Wurzel laufen über `ShowToast`/`ShowToastWithAction` und damit über `postFeedback` → `FeedbackSlot.post`: Fetch, Settings, Rules mit „Regel gelöscht“, Ledger, Pins und Budget. Das gilt auch für den PWA-Hinweis.
- Neue Nachrichten: `ShowFeedback`, `FeedbackActClicked of Guid` (wirkt nur für die aktuelle Id, leert den Platz) sowie `StartDismissToast`/`ToastExited` für den PC-Toast (`ToastExiting`, dann `ToastHidden`; die Meldung bleibt für die Leiste).
- `UrlChanged` leert den Platz nur, wenn sich der `pageKey` ändert.
- Der PWA-Hinweis hat kein Zeitlimit. Wird er ersetzt, wechselt die Seite oder wird ✕/Klick gewählt, gilt er als geschlossen (`NoticeShown = false`).

**Zwei Ansichten** (`src/Client/View.fs`, CSS-getrennt)
- `Feedback.bar BarPlacement.Docked` sitzt in der Hülle `FeedbackDock`. Diese misst die Höhe nach `--feedback-bar-height` (`Viewport.mirrorChildHeight`).
- Lage über die Klassen `feedback-dock-nav`, `feedback-dock-edge` (Durchgehen) und `feedback-dock-selection` (über der Auswahl-Leiste am Handy). Die mobile Auswahl-Leiste wird dafür nach `--ledger-mobile-bar-height` gemessen (`MeasuredBar` in `Components/Ledger/View.fs`).
- Von 768 bis 1023 px beginnt die Leiste rechts der Rail und steht über `--ledger-bottom-bar-height`.
- „+ Buchung“: Kassenbuch mit `captureFabVisible` und `FromLg`, Übersicht mit `FromMd` und `Quiet = captureQuiet`. Die `data-testid` bleiben `ledger-capture-fab`/`overview-capture-fab`. Ab 768 px blendet die Hülle auf der Übersicht ohne Meldung mit `md:hidden` aus.
- Entfernt: `captureFab` (Ledger und Overview), `captureFabSpacer`, `.ledger-capture-fab`/`.overview-capture-fab`, `.toast-above-nav` sowie in `Toast.fs` `container`, `containerDefault`, `actionContainer`, `renderItems`, `renderList` und `ToastPosition`.
- Neu ist `Toast.single`: `hidden lg:flex`, unten rechts, `toast-above-bar`, `toast-layer`.
- `Navigation.fs`: `shellOffset` und `pageContentFlush` rechnen `--feedback-bar-height` ein.
- Die Styleguide-Demo „17. Toast“ zeigt einen Toast im Fluss der Seite, damit der Export bei 880 px ihn misst.

**Tests**
- `src/Tests/FeedbackSlotTests.fs` ersetzt `ToastLifecycleTests.fs` (29 Tests). Abgedeckt:
  - `post`: ersetzen; 1 → 2 → 3 mit `UndoReviewMarkBatch`; Id der neuen; andere Aktion ersetzt; Gruppe beginnt bei 1; Wisch ohne Rückgängig ersetzt.
  - `splitText` (beide Trenner, erster gewinnt, ohne Trenner), `ofToast`, `toastText`.
  - Wurzel: Ersetzen, Zusammenfassen über `ShowToastWithAction`, `ShowFeedback`, Läuft ohne Zeitgeber, Abgang mit `CmdTiming.expectAfter 220`, doppeltes ✕, Zeitgeber einer ersetzten Meldung, Klick aktuell und alt, Seitenwechsel leert, gleicher `pageKey` behält, PWA-Hinweis ersetzt bzw. Seitenwechsel schließt, Erneut senden bei laufender Schlange.
- `PwaTests.fs` angepasst. Alle Root-Modelle in den Tests haben jetzt `Feedback = None`.
- Gegenprobe: Ohne den Zusammenfass-Zweig in `post` (`when false && …`) waren 2 Tests rot („1 → 2 → 3“ und „fasst Wische über post zusammen“). Danach wiederhergestellt.

**Sweep** (`scripts/layout-invariants.mjs`)
- Invariante 12 „Rückmeldung“ ist im Kopfkommentar ergänzt. Bei 5 und 11 gilt der Toast nur ab 1024 px.
- Sie läuft oben und am Ende gescrollt. Sie zählt nur die feste Leiste `.feedback-bar-docked[data-testid="feedback-bar"]`, die Styleguide-Vorschauen stehen im Fluss. Die Task nennt `[data-feedback="bar"]`, design-system-ytgnc hat `data-testid="feedback-bar"` gebaut.
- Gegenbeweise in `layout-invariants.test.mjs`:
  - (a) `--feedback-bar-height: 0` meldet die letzte Zeile als verdeckt. Er ersetzt den Gegenbeweis `ledger-capture-fab-spacer`.
  - (b) Ein sichtbarer Toast meldet „Toast am Handy“.
  - (c) Eine Meldung über dem runden + meldet „Knopf der Leiste verdeckt“.
- Neue Zustände: `kassenbuch-meldung` (320, 390, 412 per Wisch; 900 per Kategorieänderung im Sheet, weil es dort keinen Wisch gibt und G am Ort meldet), `kassenbuch-3-geprueft` (412) und `uebersicht-meldung` (390, 412, 1280).
- Angepasste Zustände:
  - Warten auf `[data-feedback-action]` unter 1024 px (`feedbackAction`).
  - `kassenbuch-kategorie-toast`: Am Handy wird die Leiste nach dem Schließen geprüft, dazu kommt 1280.
  - `.ledger-capture-fab` → `getByTestId("ledger-capture-fab")`.
  - `closeLedgerSheet` klickt erneut, falls `Viewport.swallowNextClick` (350 ms nach einer Picker-Wahl) den ersten Klick schluckt. Vorher hat die Einblend-Animation des Toasts das verdeckt.
- Regression c819eeb: Der Gegenbeweis läuft jetzt bei `uebersicht-meldung` @ 1280, mit Toast über dem offenen Dialog „Angepinnt“. Bei `kassenbuch-kategorie-toast` @ 1280 liegt nichts über dem Toast, das Panel ist keine Ebene darüber. Dort kann der Gegenbeweis nicht anschlagen. Am Handy gibt es den Fall nicht mehr (Entscheidung Roman: Leiste unter dem Sheet).
- Doktrin Punkt 8 (lokal zurückgedreht, rot gesehen, wiederhergestellt):
  - ledger-at63w (Abstandshalter unter der Liste, heute `--feedback-bar-height` in `shellOffset`): `pb-24 md:pb-8` ohne die Variable eingesetzt. `kassenbuch` @ 390 meldete Invariante 5 und 12: „Füllbuchung 40 … am Ende gescrollt: von der Meldungsleiste verdeckt“.
  - design-system-vf94m (Toast mit Aktion unten über dem schwebenden Knopf): `Toast.single` am Handy sichtbar gemacht (`flex … inset-x-4 bottom-[94px]`). `kassenbuch-meldung` @ 412 meldete Invariante 12: „div "Bauhaus geprüftRückgängig": Toast am Handy“.

**Browser-Tests**
- Am Handy wird die Leiste gesucht: `ledger-capture`, `ledger-payee`, `move-money` (Erneut senden), `kategorie-anpinnen` und `target-check`.
- `ledger-payee` und `move-money` schließen zuerst das Sheet, weil die Leiste bis ledger-gwr9j unter dem Sheet liegt (Entscheidung Roman).
- Am PC suchen `ledger-lena`, `ledger-lena-batch`, `ledger-memo` und `ledger-split-editor` den Text im `.toast-layer`, weil die versteckte Leiste denselben Text trägt.
- Keine neuen Flow-Tests.

**Styleguide** (`standards/frontend/styleguide.md`)
- §6: zweiphasiger Abgang nur für den PC-Toast, Zustand `Model.Feedback`.
- §7.13: neue Punkte „Ein Zustand für beide“ und „Übergang“.
- §5: Zeile Toast mit `single` statt `renderList`.

**Vergleich mit der Skizze** (Screenshots in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/result-shots/design-system-gkded/`)
- `kassenbuch-ruhe-412.png` neben `skizze-shots/20-kassenbuch-ruhe.png`: Die Leiste über der Navigation trägt das breite „+ Buchung“ rechts, mit Rand oben. Seitenkopf, Tabs und Suche sind Stand von BB und nicht Teil dieser Task.
- `kassenbuch-geprueft-412.png` neben `22-…`: ✓, Meldung, „Rückgängig“ im Akzent, rundes +. Abweichung (genannt): „Bauhaus geprüft“ in einer Zeile statt „Geprüft“ / „Bauhaus“ (Übergang `splitText`).
- `kassenbuch-3-geprueft-412.png` neben `23-…`: „✓ 3 geprüft · Rückgängig“ und rundes +, gleich.
- `uebersicht-meldung-412.png` neben `30-uebersicht-leiste.png`: Meldung links (hier Fehler rot mit Grund), „+ Buchung“ rechts ruhig. Unterschied zur Skizze: Skizze 30 zeigt „+ Buchung“ mit Meldung breit, die Task verlangt rund mit Meldung (`Feedback.addShape`). Umgesetzt ist die Task, bitte mit Roman klären.
- `kassenbuch-toast-1280.png` (PC 1280, nach dem Kategoriewechsel): Detail im Panel, keine Leiste. Der Toast steht unten rechts. Auf dem Bild ist er noch in der Einblendung oder schon abgelaufen: das Bild entstand nach `settle`, Lage und Oberstes prüft der Sweep (Invariante 5).
- [human-eye] Gesamteindruck bleibt für Roman.

**Gate**: `./verify.sh` → `VERIFY: PASS` (dotnet 5242, node 354).

**Kann-Sammlung design-system**
- Ausnahme `kassenbuch-kategorie-toast` @ 1280, Invariante 5, `[data-testid="detail-history-toggle"]`: Der PC-Toast steht 5 s über „Historie“ am Ende des Details. Gleiche Lage wie vor dieser Task; die Folge-Tasks nehmen Meldungen mit Ort aus dem Toast.
- `scripts/browser-usage.mjs` `FLOWS` nennt bei `verschiebung-erneut-senden` noch „über den Toast“.
