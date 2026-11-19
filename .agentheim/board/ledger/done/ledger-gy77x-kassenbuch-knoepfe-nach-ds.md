---
id: ledger-gy77x
title: "Knöpfe im Kassenbuch nach DS: „+ Buchung“ als Button.fab, „Abrufen“ und „Jetzt spiegeln“ als Symbolknopf im Kopf, „Lena fragen“ auf der Karte wie im Detail, „Abbrechen“ ohne Füllung in „Betrag“ und im Split-Editor"
status: done
type: refactor
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-zf6zg]
tags: [bestandsreview, frontend, design-system]
related_adrs: [0056, 0087]
related_research: []
prior_art: [ledger-dn4vy, ledger-s1weh]
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G5, Teil 2 von 4 (Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`).
Gleiche Knöpfe sehen im Kassenbuch anders aus als auf der Übersicht und im Detail: „+ Buchung“ ist am Handy ein
gewöhnlicher Primärknopf statt des FAB, „Abrufen“ im Kopf hat auf beiden Seiten eine andere Form, die Karte beim
Durchgehen zeigt das gefüllte Flag als Schmuck und kürzt „Rückfrage zurücknehmen“, und „Abbrechen“ im Fuß steht mit
Fläche statt ohne Füllung (§2, §5).

## What
- **„+ Buchung“ am Handy** (`Ledger/View.fs:4515` `captureFab`): `Button.fab "ledger-capture-fab" captureOpenLabel
  captureTitle false (fun () -> dispatch OpenCapture)` in der bestehenden Hülle `lg:hidden ledger-capture-fab` (Lage aus
  `styles.css:2720`). `quiet = false`: am Handy hat das Kassenbuch sonst keine Hauptaktion (Kopf nur Symbolknöpfe,
  „n durchgehen“ erst ab 1024 px), §7.14. Der Abstandhalter `captureFabSpacer` (`:3993`, heute `h-16`) wird so hoch,
  dass der 52 px hohe FAB mit 16 px Abstand die letzte Zeile nicht verdeckt.
- **Symbolknöpfe im Kopf** (`Ledger/View.fs:4861` `FetchIcon`, `:4871` `MirrorIcon`): `PageHeader.iconButton "Abrufen"
  Icons.refresh tabs.Fetch` wie auf der Übersicht (`Overview/View.fs:66`). „Jetzt spiegeln“ braucht einen Laufzustand,
  den `iconButton` nicht hat: neue DS-Variante `PageHeader.iconButtonBusy (ariaLabel) (icon) (busy: bool) (onClick)`,
  gleiches Aussehen, während des Laufs Spinner statt Symbol, `aria-busy` und `aria-disabled`, kein `disabled`, der Fokus
  bleibt (wie `Button.IsBusy`). Die Klassen `lg:hidden` bzw. `xl:hidden` (`mirrorIconClass`) setzt eine Hülle um den Knopf.
- **„Jetzt spiegeln“ mit Text** (`:4882` `MirrorButton`): `IsBusy` statt `IsLoading`, damit der Fokus am PC bleibt.
  `StartMirror` tut während eines Laufs schon nichts (`Ledger/State.fs:1952`).
- **Karte beim Durchgehen** (`Ledger/View.fs:5252` bis `:5263`): Text `lena.Label` aus `lenaActionFor` („Lena fragen“
  bzw. „Rückfrage zurücknehmen“) statt „Rückfrage zurück“; Symbol in beiden Fällen `Icons.flag Icons.SM Icons.LenaFlag`
  wie im Detail (`:1617`) statt `Icons.flagSolid Icons.XS`.
- **„Abbrechen“ im Sheet und Dialog „Betrag“** (`Ledger/View.fs:2093` `amountFooter`): `Button.Outline`.
- **Split-Editor** (`DesignSystem/SplitEditor.fs`): `cancelButton` (`:257`) als `Button.Outline` über die volle Breite;
  das Knopfpaar in `footer` (`:283`) als `grid grid-cols-2 gap-3` wie `amountFooter` statt rechtsbündig; `saveButton`
  (`:244`) mit `IsBusy` statt `IsLoading`. Damit `IsBusy` greift, übergeben die Aufrufer `canSave` ohne `not saving`
  (`Ledger/View.fs:2147` und `:2226`), sonst gilt `IsBusy` mit `IsDisabled` als `disabled` ohne Spinner (`Button.fs:146`).
  `StartSaveSplits` sperrt einen zweiten Tipp schon über `whenRowIdle CategoryLock` (`Ledger/State.fs:3668`).
- **Plus an „Zeile hinzufügen“** (`DesignSystem/SplitEditor.fs:228`): `Icons.plus Icons.SM Icons.Default`.
- **Styleguide §5**: Zeile „Button“: `Outline` nennt die neuen Orte (Fuß von „Betrag“ und Split-Editor, „Neue Buchung“
  folgt in Teil 3); der Ausschluss „ein heutiges `secondary` auf `Outline` umstellen“ gilt außerhalb eines Formular-Fußes
  (§2 „Im Fuß eines Formulars neben der Hauptaktion steht „Abbrechen“ ohne Füllung“). Zeile „PageHeader“: `iconButtonBusy`.
  `/styleguide` zeigt `iconButtonBusy` im Laufzustand neben `iconButton` (`Styleguide/View.fs:2072`).

Geänderte DS-Bausteine und ihre anderen Nutzer:
- `PageHeader` (neue Funktion, `iconButton` bleibt): Übersicht (`Overview/View.fs:66`, `:67`), `/styleguide`.
- `SplitEditor` (`cancelButton`, `footer`, `saveButton`, `sheetFooter`, `lineActions`): außer dem Kassenbuch nur
  `/styleguide` (`Styleguide/View.fs:1966` bis `:1982`).

Entscheidungen Conductor 2026-11-19, nach Empfehlung:
- FAB nicht ruhig (`quiet = false`), Begründung oben.
- `iconButtonBusy` als eigene Funktion, damit die Aufrufer von `iconButton` unverändert bleiben.
- Kein `title` am Symbolknopf; der Name kommt über `aria-label` wie auf der Übersicht. Am PC zwischen 1024 und 1279 px
  auf dem Tab „Regeln“ fällt damit der Tooltip „Jetzt spiegeln“ weg (von Roman bestätigt, 2026-11-19).

Betroffene Dateien: `src/Client/Components/Ledger/View.fs` (`amountFooter` 2093, Split-Aufrufer 2147 und 2226,
`captureFabSpacer` 3993, `captureFab` 4515, Kopf 4847 bis 4920, Karte 5252), `src/Client/DesignSystem/PageHeader.fs`,
`src/Client/DesignSystem/SplitEditor.fs`, `src/Client/Components/Styleguide/View.fs`, `standards/frontend/styleguide.md`
(§5 Button, PageHeader), `scripts/layout-states.mjs`, `scripts/ledger-review-mark.test.mjs` (neuer `[fokus]`-Test neben dem
des Prüfknopfs) oder eine neue Datei `scripts/ledger-mirror.test.mjs`.

## Acceptance criteria
- [ ] „+ Buchung“ ist am Handy `Button.fab` mit `data-testid="ledger-capture-fab"`. Beleg: Diff; die Flows in
  `scripts/ledger-capture.test.mjs` finden den Knopf weiter über `.ledger-capture-fab`; Sweep `kassenbuch` bei den
  Handy-Breiten grün, Invariante 2 (48 px) und 5 (die letzte Zeile ist am Ende nicht verdeckt); der Gegenbeweis
  `scripts/layout-invariants.test.mjs:199` bleibt grün.
- [ ] Am Handy sind „Abrufen“ und „Jetzt spiegeln“ `PageHeader.iconButton` bzw. `iconButtonBusy`, 48 × 48 px. Beleg:
  Sweep `kassenbuch` und `kassenbuch-spiegel-laeuft` bei 320, 390 und 412 px, Invariante 2.
- [ ] PC: Während der Spiegel läuft, behält „Jetzt spiegeln“ den Fokus und trägt `aria-busy="true"` und
  `aria-disabled="true"`. Beleg: `[fokus]`-Test „PC: der Fokus bleibt an Jetzt spiegeln, solange der Spiegel läuft“
  mit zurückgehaltener Mock-Antwort (Vorbild `scripts/ledger-review-mark.test.mjs`, ledger-dn4vy), vor der Änderung rot
  (Fokus auf `body`). Dass ein zweiter Druck nichts schickt, belegt `StartMirror` in `update` schon.
- [ ] Karte beim Durchgehen: der Knopf heißt wie `lenaActionFor` („Rückfrage zurücknehmen“ bei einer gefragten Buchung)
  und trägt das Umriss-Flag in der Flag-Farbe. Text aus `lenaActionFor` ist getestet
  (`src/Tests/LedgerLenaFlagClientTests.fs:86`), kein neuer .NET-Test. Beleg für die Länge: neuer Sweep-Zustand
  `durchgehen-karte-lena` (Karte einer gefragten Buchung, 320 und 412 px), Invarianten 3 und 4: der Text bricht um,
  wird nicht abgeschnitten, „Passt, weiter“ bleibt erreichbar.
- [ ] „Abbrechen“ in „Betrag“ und im Split-Editor ist `Button.Outline`; das Knopfpaar im Split-Fuß am PC steht über die
  volle Breite. Beleg: Diff; Sweep `kassenbuch-betrag`, `kassenbuch-betrag-dialog`, `kassenbuch-aufteilen`,
  `kassenbuch-aufteilen-speicherfehler` grün.
- [ ] „Speichern“ im Split-Editor zeigt beim Speichern den Spinner und behält den Fokus (`IsBusy`); ohne speicherbaren
  Entwurf bleibt er gesperrt. Beleg: Diff; kein eigener Test (Doktrin Punkt 6, `IsBusy` ist im Baustein belegt).
- [ ] Das Plus an „Zeile hinzufügen“ steht in `Icons.Default`. Beleg: Diff.
- [ ] Styleguide §5 (Button, PageHeader) ist nachgezogen, `/styleguide` zeigt `iconButtonBusy`.
- [ ] `npm run guard:classes` und `npm run guard:tokens` grün.
- [ ] Romans Blick: am Handy (412 px, Touch) FAB, Kopf mit beiden Symbolknöpfen auch während eines Spiegels, Sheet
  „Betrag“, Split-Sheet, Karte beim Durchgehen mit „Lena fragen“ und „Rückfrage zurücknehmen“; am PC 1440 px Kopf,
  Dialog „Betrag“, Split-Editor im Panel. Vorher und nachher über `npm run screenshot:route`; Abweichungen nur die
  genannten. `[human-eye]`
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Urteile zu den Befunden dieses Teils:
- `Ledger/View.fs:4522` FAB: bestätigt. `Button.fab` (`Button.fs:780`) ist 52 px hoch, rund, gefüllt mit Glow; heute
  ein normaler Primärknopf. Der Abstandhalter `h-16` reicht für 52 + 16 px nicht mehr.
- `Ledger/View.fs:4861` und `:4871` Symbolknöpfe: bestätigt, mit Einschränkung. Ein reiner Wechsel auf `iconButton`
  verlöre den Spinner während des Spiegels und das Verstecken ab `lg`/`xl`; daher `iconButtonBusy` und eine Hülle.
  Heute `Button.Secondary` mit Fläche und Rand, Symbol 16 px; danach ohne Rand, Symbol 22 px wie auf der Übersicht.
- `Ledger/View.fs:5257` Flag: bestätigt. `flagSolid` ist laut §5 nur die Marke einer Rückfrage, nicht Schmuck an einem Knopf.
- `Ledger/View.fs:5252` „Rückfrage zurück“: bestätigt; Detail und Auswahl-Leiste sagen „Rückfrage zurücknehmen“.
  Hierher gezogen, weil es derselbe Knopf ist wie das Flag.
- `Ledger/View.fs:2093` „Abbrechen“ in „Betrag“: bestätigt.
- `DesignSystem/SplitEditor.fs:257` und `:283`: bestätigt. Dazu: `saveButton` mit `IsLoading` setzt `disabled`, der
  Fokus fällt beim Speichern auf `body`.
- `DesignSystem/SplitEditor.fs:228` Plus im Akzent: bestätigt, §2 gibt den Akzent nur Hauptaktion, Auswahl, Link, Fokus.

Entschieden (Roman, 2026-11-19): jeweils die Empfehlung.
- „Abbrechen“ ohne Füllung: §2 verlangt es im Fuß neben der Hauptaktion, §5 Zeile „Button“ verbietet aber, „ein heutiges
  `secondary` auf `Outline` umzustellen“. Empfehlung: §2 gilt für den Formular-Fuß, §5 wird entsprechend eingeschränkt
  (gilt auch für Teil 3, ledger-zf6zg).
- Der FAB im Kassenbuch sieht danach aus wie auf der Übersicht (rund, 52 px, Glow). Empfehlung: übernehmen.
- „Abrufen“ und „Jetzt spiegeln“ am Handy ohne Rand und Fläche, Symbol größer, wie auf der Übersicht. Am PC auf „Regeln“
  zwischen 1024 und 1279 px entfällt der Tooltip. Empfehlung: übernehmen; der Tooltip am Symbolknopf fehlt auf der
  Übersicht auch.

Kann-Sammlung ledger-k4n2s: Punkt 47 („+ Buchung“ als `Button.fab`) geht in dieser Task auf. Punkt 82 („Jetzt spiegeln“
setzt `disabled`, Fokus geht verloren) geht im Teil zu „Jetzt spiegeln“ auf; der Rest von Punkt 82 (Trefferflächen bei
aktivem Filter) bleibt dort. Punkt 1 nennt „Split-Sheet-Knöpfe“ als Nachbau mit zu kleiner Trefferfläche am Handy; das
ist der Handy-Fuß des Split-Sheets und bleibt dort.

Überschneidungen: Teil 3 (ledger-zf6zg) stellt „Abbrechen“ in „Neue Buchung“ um und braucht die §5-Regel von hier, daher
erst dieser Teil. Teil 1 (ledger-ef068) und Teil 4 (ledger-rb3xw) arbeiten an anderen Stellen.

## Outcome

Die Knöpfe im Kassenbuch sehen aus wie auf der Übersicht und im Detail. `./verify.sh`: `VERIFY: PASS` (dotnet 5175 grün, node 352 grün, dotnet build, npm run build, guard:classes, guard:tokens grün). Das README-Delta ist mit `npm run check:readme-delta -- --bc ledger` geprüft (Exit 0, „Ganz nachlesen: keine Einträge“).

- **„+ Buchung“ am Handy** (`Ledger/View.fs`, `captureFab`): `Button.fab "ledger-capture-fab" captureOpenLabel captureTitle false …` in der Hülle `lg:hidden ledger-capture-fab`. Gefüllt, weil der Kopf am Handy nur Symbolknöpfe trägt (§7.14). `captureFabSpacer` ist jetzt `h-[68px]` (52 px FAB + 16 px Abstand). `scripts/ledger-capture.test.mjs` und die Sweep-Zustände finden den Knopf weiter über `.ledger-capture-fab`; der Gegenbeweis `layout-invariants.test.mjs:199` bleibt grün.
- **Kopf** (`headerActions`): „Abrufen“ ist `PageHeader.iconButton "Abrufen" Icons.refresh tabs.Fetch`, „Jetzt spiegeln“ die neue DS-Variante `PageHeader.iconButtonBusy` (`DesignSystem/PageHeader.fs`): gleiches Aussehen wie `iconButton`, beim Lauf `proto-spinner` statt Symbol, `aria-busy` und `aria-disabled`, kein `disabled`, ein Tipp tut nichts. Beide stehen in einer Hülle mit `lg:hidden` bzw. `mirrorIconClass` (`xl:hidden` auf „Regeln“). Kein `title` mehr, der Tooltip auf „Regeln“ zwischen 1024 und 1279 px entfällt wie entschieden.
- **„Jetzt spiegeln“ mit Text**: `IsBusy` statt `IsLoading`. Beleg: `[fokus]` „PC: der Fokus bleibt an Jetzt spiegeln, solange der Spiegel läuft“ in der neuen Datei `scripts/ledger-mirror.test.mjs` (1440 px, `mirrorYnabBudget` zurückgehalten, prüft Fokus, `aria-busy`, `aria-disabled`, kein `disabled`, ein Aufruf). Gegenprobe: mit `IsLoading = model.MirrorRunning` lokal rot („der Fokus liegt auf BODY“), danach wiederhergestellt.
- **Karte beim Durchgehen**: Text `lena.Label` aus `lenaActionFor`, Symbol `Icons.flag Icons.SM Icons.LenaFlag` in beiden Fällen. Bei 320 px ist „zurücknehmen“ allein breiter als die halbe Karte (116 px Text auf 114 px Innenbreite) und lief über den Rand; der Knopf trägt deshalb zusätzlich `hyphens-auto` (Seite hat `lang="de"`), das Wort trennt sich („zurück-nehmen“). Neuer Sweep-Zustand `durchgehen-karte-lena` (320, 412) grün.
- **Folge davon, Ausnahme im Sweep**: auf der längsten Karte `durchgehen-amazon` (gefragte Buchung mit Vorschlägen, Bestell-Link, Lena-Hinweis) liegt „Passt, weiter“ bei 320 px durch die dritte Zeile 8 px unter dem Rand (Invariante 7). Eingetragen als `exceptions` (Zustand `durchgehen-amazon`, 320, inv 7, `[data-testid="review-page"] button "Passt, weiter"`), 412 px ist grün. Für die Kann-Sammlung ledger, siehe unten.
- **„Abbrechen“** in `amountFooter` (Sheet und Dialog „Betrag“) ist `Button.Outline`.
- **Split-Editor** (`DesignSystem/SplitEditor.fs`): `cancelButton` als `Button.Outline` mit `w-full`, das Knopfpaar in `footer` als `grid grid-cols-2 gap-3`, „Speichern“ mit `IsBusy` (gemeinsamer Helfer `saveButtonWith`, damit Panel und Sheet dieselbe Logik nutzen). Die Aufrufer in `Ledger/View.fs` übergeben `SplitLogic.canSave` ohne `not saving`. Das Plus an „Zeile hinzufügen“ steht in `Icons.Default`.
- **Styleguide** (`standards/frontend/styleguide.md` §5): Zeile „Button“: `Outline` nennt Fuß von Dialog, „Betrag“ und Split-Editor („Neue Buchung“ folgt mit ledger-zf6zg); `secondary` bleibt außer als „Abbrechen“ im Formular-Fuß; der Ausschluss gilt nur außerhalb eines Formular-Fußes. Zeile „PageHeader“: `iconButtonBusy`, kein `title` am Symbolknopf, Hülle für das Ausblenden. `/styleguide` (Abschnitt Navigation, „Kopf eines Orts“) zeigt `iconButtonBusy` ruhend und im Laufzustand.
- **Sweep** (`scripts/layout-states.mjs`): `kassenbuch-spiegel-laeuft` jetzt auch bei 320, 390, 412 px (Symbolknöpfe 48 × 48 px, Invariante 2); neu `durchgehen-karte-lena` (320, 412) mit `REVIEW_QUEUE.lena`, neu `kassenbuch-aufteilen-panel` (1440, Split-Fuß im Panel, „Speichern“ erreichbar). `kassenbuch`, `kassenbuch-betrag`, `kassenbuch-betrag-dialog`, `kassenbuch-aufteilen`, `kassenbuch-aufteilen-speicherfehler` grün.
- Kein .NET-Test neu: Text aus `lenaActionFor` ist in `LedgerLenaFlagClientTests.fs` belegt, `IsBusy` im Baustein (Doktrin Punkt 6).

**Romans Blick `[human-eye]`** (Bilder über die Sweep-Zustände, vorher mit den HEAD-Ständen der vier F#-Dateien lokal aufgenommen, danach wiederhergestellt; Daten erfunden):
- Kopf und FAB am Handy: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/vorher-kassenbuch-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-kassenbuch-412.png`
- Kopf während des Spiegels am Handy: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/vorher-kassenbuch-spiegel-laeuft-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-kassenbuch-spiegel-laeuft-412.png`
- Kopf am PC und während des Spiegels: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/vorher-kassenbuch-1280.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-kassenbuch-1280.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/vorher-kassenbuch-spiegel-laeuft-1280.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-kassenbuch-spiegel-laeuft-1280.png`
- Sheet „Betrag“: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/vorher-kassenbuch-betrag-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-kassenbuch-betrag-412.png`; Dialog „Betrag“ am PC: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/vorher-kassenbuch-betrag-dialog-1280.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-kassenbuch-betrag-dialog-1280.png`
- Split-Sheet: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/vorher-kassenbuch-aufteilen-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-kassenbuch-aufteilen-412.png`; Split-Editor im Panel: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/vorher-kassenbuch-aufteilen-panel-1280.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-kassenbuch-aufteilen-panel-1280.png`
- Karte „Lena fragen“: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/vorher-durchgehen-karte-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-durchgehen-karte-412.png`; Karte „Rückfrage zurücknehmen“: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/vorher-durchgehen-amazon-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-durchgehen-amazon-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-durchgehen-karte-lena-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/ledger-gy77x/nachher-durchgehen-karte-lena-320.png`
Aufgenommen bei 412 und 1280 px (Vorgabe des Conductors) statt 1440 px; mit Touch-Emulation am Handy, nicht auf dem Gerät. Abweichungen nur die genannten, dazu die Silbentrennung auf der Karte bei 320 px.

Für die Kann-Sammlung ledger:
- Durchgehen bei 320 px: auf der Karte `durchgehen-amazon` liegt „Passt, weiter“ 8 px unter dem Rand, weil „Rückfrage zurücknehmen“ drei Zeilen braucht (Ausnahme `durchgehen-amazon` @ 320, inv 7, `[data-testid="review-page"] button "Passt, weiter"`).
- Kann-Sammlung ledger-k4n2s: Punkt 47 ist erledigt, aus Punkt 82 der Teil „Jetzt spiegeln setzt disabled“.
