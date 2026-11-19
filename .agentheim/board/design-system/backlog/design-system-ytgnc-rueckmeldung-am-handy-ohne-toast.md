---
id: design-system-ytgnc
title: "Rückmeldung am Handy ohne Toast (Regel R3), Teil 1: DS-Baustein `Feedback` (Meldungsleiste mit „+ Buchung“, Zeile im Sheet, Rückgängig am Feld, Zeile „gelöscht“) auf `/styleguide`, Styleguide §7.13, §5 und §6 neu, ADR 0098"
status: backlog
type: feature
context: design-system
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: [design-system-gkded, ledger-pz5wv, ledger-gwr9j, ledger-p5y9s, ledger-enjex, ledger-k68sd, budgeting-gtrgk, budgeting-sx9v5, budgeting-p3sk0, categorization-t6j00, banking-import-vwhfm, banking-import-ns4re]
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, design-system-vf94m]
---

## Why
Roman am 2026-11-19 (Design-Exploration ledger-vf5h6): Toasts stehen im Weg und sind hässlich. Am Handy liegen heute
zwei Toasts gleichzeitig auf dem Bildschirm (einfache oben über `Toast.container`, solche mit „Rückgängig“ unten über
`actionContainer`), mehrere Wische stapeln drei Toasts über Liste und „+ Buchung“, im Durchgehen liegt der Toast auf
„Passt, weiter“. Entscheidung: **am Handy kein Toast** (R3, ADR 0098). Löst design-system-vf94m (Toast verdeckt
„+ Buchung“) ab.

Diese Task liefert den Baustein und die Regel im Styleguide. Die App-Verdrahtung (ein Platz im Modell, Leiste statt
Toast-Liste, „+ Buchung“ in der Leiste, Zusammenfassen, Sweep-Invariante) ist **design-system-gkded**. Die Seiten
stellen danach ihre Meldungen um (ledger-pz5wv, ledger-gwr9j, budgeting-gtrgk, categorization-t6j00,
banking-import-vwhfm).

## What

Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am
2026-11-19 freigegeben). Worker und Verifier können sie nicht öffnen; lokal liegen Quelltext und Screenshots (Notes).
Die CSS-Werte der Skizze stehen in `skizze.src.html`, Zeilen 235 bis 249 (Leiste `.kb-bar`, `.kb-msg`, `.kb-add`),
94 bis 98 (Zeile unter dem Hauptknopf `.ph-last`, `.undo`), 153 bis 156 (Feld `.field.flash`, Bestätigung `.cap`),
187 bis 190 (Fehler `.err`), 270 und 281 (Leuchten `.flash-row`, `.flash-val`), 284 bis 296 (Zeile im Sheet `.mk-msg`,
gelöschte Zeile `.mk-ghost`, Hinweis `.mk-hint`); Verhalten in JS Zeilen 1038 bis 1050 (`setMsg`, `paintMsg`).

### Regel R3 (ADR 0098, schon geschrieben)
Plätze in dieser Reihenfolge:
1. **Ergebnis am Ort sichtbar:** die Stelle bestätigt selbst (Feld leuchtet kurz, darunter Bestätigung mit „Rückgängig“).
2. **Sheet offen:** Zeile im Sheet über dem Hauptknopf, nie darüber.
3. **Sonst:** Meldungsleiste über der Navigation. Auf Seiten mit „+ Buchung“ trägt sie den Knopf (der schwebende Knopf
   entfällt, die Liste endet über der Leiste); kommt eine Meldung, wird der Knopf zum runden „+“ und die Meldung steht
   links zweizeilig: oben die Aktion (nie gekürzt), darunter Name oder Grund (gekürzt mit „…“), rechts „Rückgängig“
   bzw. „Erneut“. Seiten ohne „+ Buchung“ zeigen die Leiste nur, solange es eine Meldung gibt.
- Immer nur eine Meldung. Gleiche Aktionen hintereinander werden eine („3 geprüft“, Rückgängig nimmt alle zurück),
  eine andere Aktion ersetzt sie. Sie bleibt bis zur nächsten Aktion oder bis man die Seite verlässt: kein ✕, kein Zeitlimit.
- Fehler stehen rot am selben Platz und bleiben bis zur nächsten Aktion. Ein Zustand der Seite (etwa „nicht neu
  gelesen“) ist keine Meldung, sondern ein Hinweis auf der Seite (Muster wie ledger-pfhdq).
- Gelöschtes in einer Liste hinterlässt eine Zeile am Platz („„Regel drei“ gelöscht · Rückgängig“), bis zur nächsten Aktion.
- Am PC (ab 1024 px) bleibt der Toast unten rechts (§7.13), höchstens einer zugleich.

### Handy und PC: Grenze 1024 px
„Handy“ heißt in dieser Regel **unter 1024 px**, „PC“ ab 1024 px (wie in ADR 0098 und `Viewport.isInspectorPanelViewport`).
Die Leiste gilt also auch von 768 bis 1023 px (Rail links, keine Navigation unten): dort steht sie am unteren Rand
rechts der Rail. Grund: das Kassenbuch zeigt den schwebenden Knopf heute bis 1023 px (`lg:hidden` in
`Components/Ledger/View.fs` `captureFab`), und ein Toast unten rechts läge dort auf ihm, also genau der Fehler aus
design-system-vf94m.

### Der Baustein `Feedback` (`src/Client/DesignSystem/Feedback.fs`, neu)
Ein Modul mit reinen Typen und Views, ohne App-Typen (es wird vor `Types.fs` kompiliert, Eintrag in
`src/Client/Client.fsproj` nach `Toast.fs`). Vorschlag der Form (Namen darf der Worker schärfen, die Teile nicht):

```fsharp
module Client.DesignSystem.Feedback

/// Ton der Meldung: ok (grünes ✓), Warnung (gelbes !), Fehler (rotes !), läuft (ohne Zeichen, Text gedämpft),
/// Info (ohne Zeichen, Text normal; die Skizze hat keine Info, BB kennt sie heute als `ToastInfo`).
type Tone = Ok | Warning | Error | Running | Info

/// Der Knopf einer Meldung: „Rückgängig“, „Erneut“, „Erneut senden“, „Neu laden“.
type Act = { Label: string; OnClick: unit -> unit }

/// Eine Meldung: obere Zeile die Aktion, untere Zeile Name oder Grund.
type Message = { Tone: Tone; Action: string; Detail: string option; Act: Act option }

/// „+ Buchung“ in der Leiste. `Quiet` wie `Button.fab` (Übersicht mit Einträgen in „Zu tun“, §7.14).
/// `HideFrom`: ab welcher Breite der Knopf aus der Leiste verschwindet (Kassenbuch ab 1024 px, Übersicht ab 768 px,
/// wie heute der schwebende Knopf; darüber trägt der Seitenkopf den Knopf).
type AddBreakpoint = FromMd | FromLg
type Add = { TestId: string; Label: string; AriaLabel: string; Quiet: bool; OnClick: unit -> unit; HideFrom: AddBreakpoint }

val bar : message: Message option -> add: Add option -> ReactElement   // Leiste über der Navigation, unter 1024 px
val barVisible : message: Message option -> hasAdd: bool -> bool      // rein: Leiste steht nur mit Meldung oder Knopf
val addShape : message: Message option -> AddShape                     // rein: Wide („+ Buchung“) oder Round („+“)
val sheetLine : Message -> ReactElement                                // Zeile im Sheet über dem Hauptknopf
val fieldNote : text: string -> act: Act option -> ReactElement        // Bestätigung unter einem Feld
val flash : string                                                     // Klasse: Feld bzw. Wert leuchtet kurz
val removedRow : text: string -> undo: Act -> ReactElement             // „„Regel drei“ gelöscht · Rückgängig“
```

Aussehen, aus der Skizze (Farben als Rolle, Werte aus dem Token-Layer, nie erfunden; §3 und §4):
- **Leiste** (`.kb-bar`): volle Breite des Inhalts, Höhe mindestens 64 px, Innenabstand links 16, rechts 12 px, Abstand
  8 px zwischen Meldung und Knopf, Fläche `surface-app`, oben ein Strich `border-subtle`. Lage `fixed` direkt über der
  Navigation unten (`--bottom-nav-height` plus Safe-Area), ohne Navigation unten (Rail, Durchgehen, Auswahl-Modus) am
  unteren Rand mit Safe-Area. Ebene unter Sheets und Dialogen (wie `ZIndex.fixed'`, 30): ein offenes Sheet deckt die
  Leiste, die Leiste deckt nie ein Sheet (Platz 2 gilt dort). Die Lage am Rand rechnet die App (design-system-gkded); der
  Baustein nimmt sie als Klasse oder Parameter, nicht als feste Zahl im Modul.
- **Meldung** (`.kb-msg`, Zeile mit Zeichen, zwei Zeilen Text, Knopf): Zeichen „✓“ in `success` bei Ok, „!“ in
  `attention` bei Warnung, „!“ in `error` bei Fehler, kein Zeichen bei Läuft und Info. Obere Zeile die Aktion,
  halbfett, bei Fehler in `error`, bei Warnung in `attention`, bei Läuft in `text-secondary`; **nie gekürzt**, sie bricht
  um, und die Leiste wächst mit (darum „mindestens“ 64 px). Untere Zeile Name oder Grund in `text-secondary`, kleiner,
  eine Zeile mit „…“ über `Truncate.text` (ganzer Text im `title`). **Ausnahme Fehler und Warnung:** dort bricht auch
  der Grund um (höchstens drei Zeilen), weil am Handy kein `title` lesbar ist und ein gekürzter Fehlergrund nirgends
  sonst steht. Knopf rechts in `accent`, halbfett, nicht umbrechend, Trefferfläche 48 px (`TouchTargets`), Attribut
  `data-feedback-action="true"`. Die Meldung trägt `aria-live="polite"`, Fehler `role="alert"`.
- **„+ Buchung“ in der Leiste** (`.kb-add`): ohne Meldung breit mit Text „+ Buchung“ (Symbol `plus` wie heute in
  `Button.fab`), Höhe 48 px, Innenabstand 18 px, ganz rund, gefüllt `accent` mit Schrift `on-accent`; `Quiet` wie
  `Button.fab` heute (Grund `surface-elevated`, Rand und Schrift `accent`, ohne Glow). Mit Meldung rund 48 × 48 px, nur
  „+“, `aria-label` bleibt „Neue Buchung“ (bzw. der heutige `captureTitle`/`captureAriaLabel`). Die `data-testid` des
  Knopfs nimmt der Aufrufer mit (`ledger-capture-fab`, `overview-capture-fab` bleiben, die Browser-Tests finden ihn so).
- **Zeile im Sheet** (`.mk-msg`): eine Zeile, mindestens 36 px hoch, Zeichen, Aktion halbfett gekürzt mit „…“ (hier
  ist die Aktion zugleich der Name, etwa „50,00 € nach Reise“), Knopf rechts wie in der Leiste. Fehler in `error`
  (Vorbild `.err`: Fläche `error` mit 10 % Deckkraft, Radius 8 px, Text bricht um, Knopf in `text-primary`).
- **Bestätigung am Feld** (`.cap`): klein (12 px), `text-muted`, Text und Knopf in einer umbrechenden Zeile, Knopf
  „Rückgängig“ in `accent` (am Handy Trefferfläche 48 px, auch wenn die Skizze 28 px zeigt: §4 Touch-Targets geht vor).
- **Leuchten** (`flash`, `.field.flash`, `.flash-row`, `.flash-val`): Rand `accent` und ein Ring `accent` mit
  geringer Deckkraft, rund 0,7 s, dann zurück. Ohne Bewegung (`prefers-reduced-motion`): kein Übergang, der Ring steht
  bis zum Ende der Zeit (§6).
- **Zeile „gelöscht“** (`.mk-ghost`): Höhe 44 px (am Handy 48 px, §4), Rand gestrichelt `border-default`, Radius 10 px
  (nächster Radius aus dem Token-Layer), Text `text-secondary` links („„Regel drei“ gelöscht“, Name gekürzt mit „…“),
  „Rückgängig“ rechts in `accent`.
- **Bewegung**: die Meldung kommt mit Einblenden und 6 px von unten in 180 ms (`msgIn`, `ease-out`) und geht ohne
  Abgang (sie wird ersetzt). Ohne Bewegung keine Animation. Eintrag in §6 und in den `prefers-reduced-motion`-Block in
  `styles.css`. Die Animationsklasse sitzt auf dem inneren Element, nie auf dem `fixed` Behälter (§6).
- Schriftgrößen aus der Skala (§4, keine eigenen `text-[Npx]`): Aktion und Knopf `FontSizes.compact` (13 px, Skizze
  13,5), Name oder Grund `FontSizes.xs` (12 px, Skizze 12,5), Zeichen `FontSizes.body`. Abweichung von der Skizze um
  einen halben Pixel, Grund: die Skala hat keine Halbwerte.

### Styleguide-Route `/styleguide`
Ein Abschnitt „Rückmeldung“ in `src/Client/Components/Styleguide/View.fs` mit den echten Bausteinen (wie `ToastDemo`
per `useState`, keine neuen App-Nachrichten): Leiste in Ruhe mit „+ Buchung“ (gefüllt und ruhig), Leiste mit Meldung
in jedem Ton (Ok „Geprüft“ / „REWE Markt GmbH Filiale Leipzig-Gohlis“ / Rückgängig; Ok „3 geprüft“; Läuft „Abrufen
läuft …“; Ok „Abgerufen“ / „3 neu, 1 geändert“; Fehler „Abrufen gescheitert“ / „Comdirect antwortet nicht“ / Erneut;
Warnung „Prüfvermerk nicht in YNAB“ / Grund; Info), eine Leiste ohne Knopf mit Meldung, eine mit sehr langer Aktion
(bricht um), Zeile im Sheet („✓ 50,00 € nach Reise · Rückgängig“ und rot „Nicht verschoben: …“ mit „Erneut senden“),
Feld mit Leuchten und Bestätigung („Bahn · 84,20 € verfügbar · Rückgängig“), Zeile „„Regel drei“ gelöscht ·
Rückgängig“. Ein Knopf „Meldung“ in der Demo setzt nacheinander die Töne (zeigt Wechsel, Ersetzen und die Bewegung).
In der Route stehen die Leisten im Fluss der Seite (Vorschau wie `Navigation.bottomBarPreview`), nicht `fixed`.

### Styleguide-Text
- **§7.13 Rückmeldung** neu nach der Regel oben, mit „Wann / Wann nicht“, den Plätzen 1 bis 3, Grenze 1024 px,
  „eine Meldung“, Zusammenfassen, kein ✕ und kein Zeitlimit am Handy, Fehler rot am Platz, Zustand der Seite ist ein
  Hinweis (`ErrorDisplay.notice`), Zeile „gelöscht“; PC: Toast unten rechts, höchstens einer. Quelle: ADR 0098 und
  ADR 0087 Punkt 22. Der alte Satz „Am Handy steht sie oben, auch bei offenem Sheet“ entfällt.
- **§5:** neue Zeile `Feedback` (wann: jede Rückmeldung unter 1024 px, nach den Plätzen in §7.13; wann nicht: Zustand
  der Seite → `ErrorDisplay.notice`, blockierende Fehler → `ErrorDisplay`/`Modal`). Zeile `Toast`: „nur ab 1024 px,
  höchstens einer, aus demselben Zustand wie die Leiste (§7.13)“. Zähler in der Überschrift von §5 (26) nachziehen.
  Zeile `ErrorDisplay`: „Kurzes Erfolgs-/Info-Feedback → `Toast`“ wird „→ `Feedback` (§7.13)“.
- **§6 Motion:** der Absatz „Toast-Platzierung, zwei bewusste Fälle (mobil ≠ Desktop)“ wird ersetzt: Toast nur ab
  1024 px unten rechts; am Handy die Leiste. Die Zwei-Phasen-Mechanik des Toasts beschreibt dann design-system-gkded
  (sie ändert sich dort). Neuer Eintrag: Eingang der Meldung (`msgIn`) und Leuchten, beide mit `prefers-reduced-motion`.
- §2 Zeile 215 („Rückmeldungen auf Aktionen (Wisch-Geste, Toast) dürfen leuchten“) um „Leiste, Feld“ ergänzen.

### Funktionsabgleich (betroffen: Styleguide-Route, Baustein `Toast`)
| Heute | Mit dieser Task |
|---|---|
| `/styleguide` zeigt `ToastDemo` (Toast ohne und mit „Rückgängig“, sanfter Abgang) | bleibt (der Toast gilt am PC weiter) |
| Baustein `Toast` mit `renderItems`, `renderList`, `container`, `actionContainer` | bleibt in dieser Task unverändert; `actionContainer` und der Handy-Streifen oben fallen in design-system-gkded |
| Kein Baustein für Leiste, Zeile im Sheet, Bestätigung am Feld, Zeile „gelöscht“ | neu: `Feedback` |
| Schwebender Knopf `Button.fab` | bleibt in dieser Task; die Leiste übernimmt ihn in design-system-gkded |

Gestrichen hat Roman (2026-11-19): Toasts am Handy. Sonst entfällt nichts.

## Acceptance criteria
- [ ] ADR 0098 „Rückmeldung am Handy ohne Toast“ liegt in `.agentheim/knowledge/decisions/` (beim Verfeinern
      geschrieben) und ist im Styleguide als Quelle von §7.13 genannt.
- [ ] `src/Client/DesignSystem/Feedback.fs` mit Leiste (Ruhe mit „+ Buchung“, gefüllt und ruhig; Meldung mit rundem „+“;
      ohne Knopf nur mit Meldung), Zeile im Sheet, Bestätigung am Feld mit Leuchten, Zeile „gelöscht“; Töne Ok,
      Warnung, Fehler, Läuft, Info wie unter What beschrieben. Nur Klassen aus dem Token-Layer (`guard:classes`,
      `guard:tokens` grün).
- [ ] Reine Funktionen `barVisible` und `addShape` (Namen nach Wahl) mit Tests in `src/Tests` (neue Datei
      `FeedbackDesignTests.fs` oder bei `DesignSystemStartupTests.fs`): ohne Meldung und ohne Knopf keine Leiste; mit
      Knopf ohne Meldung breit; mit Meldung rund; jede Meldung zeigt den Knopf der Meldung nur, wenn `Act` gesetzt ist.
      Gegenprobe: vertauschte Zweige machen die Tests rot.
- [ ] Die Aktion einer Meldung wird nie gekürzt (bricht um), der Name eine Zeile mit „…“ und `title`, bei Fehler und
      Warnung bricht auch der Grund um. Belegt durch einen Zustand im Layout-Sweep (`scripts/layout-states.mjs`,
      Route `/styleguide`, Abschnitt „Rückmeldung“, Breiten 320, 390, 412): Invarianten 2 (48 px), 4 (abgeschnitten)
      und 5 (Überdeckung) grün.
- [ ] `/styleguide` zeigt den Abschnitt „Rückmeldung“ mit allen Beispielen aus What; die Demo „Meldung“ wechselt die Töne.
- [ ] Bewegung: Eingang `msgIn` 180 ms und Leuchten rund 0,7 s, beide im `prefers-reduced-motion`-Block abgeschaltet.
- [ ] Styleguide §7.13, §5 (`Feedback`, `Toast`, `ErrorDisplay`, Zähler), §6 und §2 nachgezogen wie in What.
- [ ] `/styleguide` neben der Skizze verglichen: Screenshot des Abschnitts bei 412 px im Outcome
      (`npm run screenshot:route`), neben `skizze-shots/22-kassenbuch-geprueft.png`, `23-…-3-geprueft.png`,
      `24-…-abrufen-laeuft.png`, `26-…-abrufen-fehler.png`, `31-budget-sheet-geld.png`, `33-regeln-geloescht-zeile.png`;
      Abweichungen nur die in dieser Task genannten (Schrift 13/12 statt 13,5/12,5 px, Trefferflächen 48 px statt 28
      bis 44 px, Grund bei Fehler und Warnung umbrechend, Ton Info zusätzlich). [human-eye] für den Gesamteindruck.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Captured 2026-11-19 aus ledger-vf5h6, verfeinert 2026-11-19 und geteilt: Teil 2 ist design-system-gkded
(App-Verdrahtung, Zusammenfassen, Übergang der alten Toasts, Sweep-Invariante mit dem Regressionstest aus
design-system-vf94m). Die Kriterien „Zusammenfassen gleicher Aktionen als reine Funktion mit Test“ und
„Sweep-Invariante“ aus der ersten Fassung stehen jetzt dort, weil das Zusammenfassen die App-Typen
(`ToastActionKind`) braucht und die Invariante die verdrahtete App.

Name des Bausteins: `Feedback` (Modul `Client.DesignSystem.Feedback`). Der App-Zustand in design-system-gkded heißt
`FeedbackSlot`, damit `open Client.DesignSystem` und `open Types` in `View.fs` nicht zwei Module `Feedback` mischen.

Skizze lokal (`…` = `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):
- ganze Skizze: `…/vf5h6/durchgehen-handy.html`, Quelltext `…/vf5h6/skizze.src.html`
- Leiste: `…/vf5h6/skizze-shots/20-kassenbuch-ruhe.png`, `22-kassenbuch-geprueft.png`, `23-kassenbuch-3-geprueft.png`,
  `24-kassenbuch-abrufen-laeuft.png`, `25-kassenbuch-abgerufen.png`, `26-kassenbuch-abrufen-fehler.png`,
  `27-kassenbuch-angelegt.png`, `30-uebersicht-leiste.png`
- Zeile im Sheet: `31-budget-sheet-geld.png`; Zeile unter dem Hauptknopf: `07-nach-passt-letzte-aktion.png`,
  `10-fehler-unter-knopf.png`; Feld: `06-kategorie-aus-vorschlag.png`; Hinweis der Seite: `32-budget-stand-hinweis.png`
- gelöscht: `33-regeln-geloescht-zeile.png`; gespeichert am Abschnitt: `34-einstellungen-gespeichert.png`
- heute in BB: `…/shots/vf5h6/heute-toast-412.png`, `heute-toast-150-412.png`, `heute-toast-600-412.png`,
  `heute-kassenbuch-wisch-412.png`, `heute-regeln-geloescht-toast-412.png`, `heute-uebersicht-412.png`

Code heute: `src/Client/DesignSystem/Toast.fs` (Handy-Streifen oben in `container`, `actionContainer` über der
Navigation), `src/Client/DesignSystem/Button.fs` `fab` (Zeile 780), `styles.css` `.toast-above-nav` (Zeile 537),
`.ledger-capture-fab`/`.overview-capture-fab` (Zeile 2720), `.toast-layer` (Zeile 2614), Styleguide-Demo
`Components/Styleguide/View.fs` `ToastDemo` (Zeile 1320).
