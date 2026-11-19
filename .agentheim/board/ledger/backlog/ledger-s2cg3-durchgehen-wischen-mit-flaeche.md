---
id: ledger-s2cg3
title: "Durchgehen am Handy: Wischen mit Fläche darunter (A1), Karte füllt den Platz bis zu den Knöpfen, Seite ohne Scrollen"
status: backlog
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-pz5wv, ledger-wg42w]
tags: [design-exploration, durchgehen, wischen, frontend]
related_adrs: [0087]
related_research: []
prior_art: [ledger-vf5h6, ledger-th36h, ledger-v6s7c]
---

## Why
Roman am 2026-11-19: Wischen fühlt sich nicht nach Wischen an. Heute rückt die Karte nur 60 % des Fingerwegs mit
(`SwipeableReviewCard` in `Ledger/View.fs`), wird blasser, darunter ist nichts. Dazu: die Karte soll immer den vollen
Platz einnehmen, und die Seite soll ohne Scrollen aufs Handy passen. Freigabe der Skizze: ledger-vf5h6, Abschnitt „Freigabe“
(Wischen A1, „Karte füllt den Platz bis zu den Knöpfen, die Seite passt ohne Scrollen“).

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am
2026-11-19 freigegeben). Worker können sie nicht öffnen; Dateien und Screenshots stehen unter „Notes“. Verhalten in
`skizze.src.html`, Funktionen `bindSwipe`, `paintUnder`, `needHint`, `leave`, `confirm`, CSS `.ph-stage`, `.ph-under`,
`.card`, `.ph-foot`. Farben, Maße, Kurven und Dauer kommen aus Token-Layer und DS (Styleguide §3, §5, §6), nie aus den
CSS-Werten der Skizze abgeschrieben (etwa `#2a2a50`).

Gilt am Handy (unter 1024 px, heute `not desk` in `ReviewPage`). Am PC (Spalte 620 px, Tastenleiste, kein Wischen) bleibt
alles, wie es ist.

### 1. Wischen (Skizze 02 bis 05)
- **Karte folgt dem Finger 1:1** (heute `dx * 0.6`), ohne Blasser-Werden. Die Geste gehört der Karte erst nach 12 px
  überwiegend waagrechtem Weg; davor und bei senkrechtem Weg scrollt der Browser (der Karteninhalt scrollt jetzt innen,
  Abschnitt 3). Pointer-Ereignisse mit `touch-action: pan-y` und `setPointerCapture` wie `Swipe.fs`
  (`SwipeRowCore`), nur für `pointerType = "touch"`. Nach einem Wisch löst der Tipp darunter nichts aus (Klick unterdrücken
  wie `onClickCapture` in `Swipe.fs`): ein Wisch, der auf dem Kategorie-Feld, einem Vorschlag, dem Payee oder „Details“
  beginnt, öffnet nichts.
- **Fläche unter der Karte** über die ganze Karte, Radius wie die Karte, in Ruhe unsichtbar, sichtbar sobald die Karte
  sich bewegt. Rechts gewischt: links auf der Fläche Haken über dem Wort „Passt“, Fläche grün getönt, Symbol und Wort grün
  (Rolle `success`). Links gewischt: rechts auf der Fläche Uhr über dem Wort „Später“, Fläche neutral (Rolle
  `surface-hover`, Symbol und Wort `text-secondary`; Überspringen ist kein Fehler, ADR 0087 Punkt 4). Nur das Symbol der
  gewischten Richtung ist sichtbar. Symbol im Kreis 34 px mit 2 px Rand in der Schriftfarbe, Wort darunter klein, Spalte
  56 px breit mit 16 px Rand zur Kante: bei 90 px Weg ganz zu sehen. Symbole aus `Icons` (`check`, `clock`).
- **Einrasten** bei derselben Grenze, an der `reviewSwipeOf` entscheidet (`reviewSwipeThresholdPx` = 90, keine zweite
  Schwelle): Fläche kräftiger (rechts stärker getönt, links eine Stufe heller mit `text-primary`), Kreis gefüllt in der
  Schriftfarbe, Symbol in der Farbe des App-Hintergrunds, Spalte leicht größer (Skizze: 1,08). Beim Übergang zu
  eingerastet ein kurzer Vibrationsimpuls (`Viewport.vibrate`, wo erlaubt), nur einmal je Übergang.
- **Loslassen** unter der Grenze: die Karte federt zurück (Federkurve aus §6, etwa `--sf-spring-out`), die Fläche
  verschwindet. Über der Grenze fliegt die Karte zur Seite hinaus (über ihre Breite hinaus, kurz, `ease-in`).
  - „Später“: die nächste Karte rückt sofort nach (kein Aufruf).
  - „Passt“: der Prüfvermerk läuft wie heute (`ReviewConfirm`, `Confirming`); solange er läuft, steht die eingerastete
    Fläche ohne Karte da, die nächste Karte rückt nach, wenn `ReviewConfirmed Ok` kommt. Scheitert er, federt die Karte
    zurück und bleibt unverändert (Skizze, Abschnitt Fehler). Die Fehlermeldung bleibt in dieser Task der heutige Toast
    „Nicht geprüft: …“; die rote Zeile unter dem Knopf baut ledger-pz5wv.
  - Die neue Karte erscheint mit kurzer Einblendung (Skizze `.card.enter`: aus 10 px tiefer, leicht kleiner, 200 ms).
- **Knöpfe und Tasten bewegen die Karte genauso:** „Passt, weiter“ und Enter fliegen nach rechts mit eingerasteter
  grüner Fläche, „Später“ und S nach links mit eingerasteter neutraler Fläche, „Lena fragen“ und L nach links ohne Fläche
  (Skizze `leave`, `paintUnder(..., armedForce)` außer bei Lena). „Rückfrage zurücknehmen“ lässt die Karte stehen.
- **Ohne Kategorie** (`reviewHasTarget` falsch), Wisch nach rechts: auf der Fläche nur ein gelbes „!“ im Kreis ohne Wort,
  Fläche gelb getönt (Rolle `attention`). Die Karte folgt bis etwa 72 px 1:1, danach zäh (Skizze `eff`: 72 + Rest × 0,12),
  sie rastet nie ein und vibriert nicht. Solange der Wisch läuft, steht in der Zeile unter dem Hauptknopf „Erst Kategorie
  wählen“ in `attention`, halbfett, und das Kategorie-Feld trägt Rand und Ring in `attention` (Skizze `.field.need`).
  Beim Loslassen federt die Karte zurück, Zeile und Feld werden wie vorher. Der Toast `reviewNeedsTargetText`
  (`State.fs` Zeile 4715) entfällt samt Text in `Types.fs`. Wisch nach links ohne Kategorie ist normales „Später“.
- **`prefers-reduced-motion`:** kein Flug, kein Zurückfedern, keine Einblendung (die globale Regel in `styles.css` setzt
  Dauer auf 0,01 ms, neue Animationen müssen darunter fallen). Fläche, Einrasten und „!“ erscheinen trotzdem, weil sie dem
  Finger folgen. Knöpfe, Enter und S bleiben der Weg ohne Wischen.
- Die Duplikat-Karte wischt weiter nicht (heute so, `reviewDuplicateCard`).

Reine Funktionen in `Types.fs` neben `reviewSwipeOf` (Test-Doktrin: reine Funktion zuerst):
`reviewSwipeOffset (hasTarget: bool) (dx: float) : float` (1:1, ohne Ziel nach rechts zäh ab 72 px) und
`reviewSwipeArmed (hasTarget: bool) (dx: float) (dy: float) : bool` (eingerastet genau dann, wenn `reviewSwipeOf` eine
Entscheidung träfe, nach rechts nur mit Ziel). `ReviewSwiped` ohne Ziel nach rechts gibt `NoOp` zurück.

Der Wisch-Baustein ist eine neue DS-Variante (Styleguide: ein DS-Baustein ersetzt ein Element der Skizze nur, wenn er
gleich aussieht): die Kassenbuch-Zeile (`Swipe.SwipeableRowRight`) hat weder Symbol noch Einrasten. Vorschlag:
`ReviewCard.swipeStage` (oder eigene Funktion in `Swipe.fs`) mit Fläche, Symbolen und Zuständen ruhe / rechts / links /
eingerastet / ohne Ziel; CSS neben `.tx-swipe-*` in `styles.css`. Eintrag in §5 (Zeile ReviewCard oder Swipe) und auf
`/styleguide` mit den Zuständen als statische Beispiele.

### 2. Zeile unter dem Hauptknopf (Skizze 01, 05)
- Unter „Passt, weiter“ steht eine Zeile mit fester Höhe (Skizze 54 px inklusive Abstand), einzeilig, ohne Umbruch, mittig:
  „Wischen: rechts „Passt“, links „Später““ (ersetzt `reviewSwipeHintText` „Nach rechts wischen heißt …“ und
  `ReviewCard.hint`). Die Höhe ist dieselbe, ob Hinweis, „Erst Kategorie wählen“ oder (ab ledger-pz5wv) die letzte
  Aktion darin steht: die Knöpfe springen nie. Kennung `data-testid="review-foot"`.
- Die Zeile steht auch unter der Duplikat-Karte, dort leer (heute „keine Hinweiszeile“): gleiche Lage der Knöpfe über alle
  Kartenarten, und ledger-pz5wv braucht den Platz nach „Verwerfen“ und „Ist neu“.
- Schrift 12 px (`FontSizes.xs`) in `text-secondary`, nicht 13 px wie die Skizze: §7.14 erlaubt drei Schriftgrößen je
  Ansicht, die Karte nutzt schon 20, 16 und 12 px.

### 3. Karte füllt den Platz, Seite ohne Scrollen (Skizze 01, 07, 08)
- Die Seite ist am Handy genau so hoch wie der sichtbare Bereich (`dvh`, mit Abstand für `safe-area-inset-top/bottom`):
  Kopf, Fortschritt, Karte, Knopfzeile „Lena fragen“/„Später“, ggf. Sperrgrund von „Lena fragen“, „Passt, weiter“, Zeile.
  Die Karte nimmt den Rest (flex 1, `min-height: 0`), auch wenn ihr Inhalt kurz ist (Skizze 08: viel leere Karte).
- „Regel anlegen“ und „Details“ stehen am unteren Rand der Karte (heute direkt unter dem Inhalt; `linksWithDetails`,
  `detailsLink` mit `margin-top: auto`).
- Ist der Inhalt länger als die Karte, scrollt er in der Karte (`overflow-y: auto`), Knöpfe und Zeile bleiben fest. Senkrecht
  scrollen in der Karte und waagrecht wischen stören sich nicht (Abschnitt 1, Geste erst nach 12 px waagrecht).
- Der Seitenrahmen trägt heute `pb-24` für die untere Navigation (`Navigation.shellOffset` über `pageContent`), obwohl sie
  im Durchgehen ausgeblendet ist (`hideBottomNav`). Das Durchgehen bekommt am Handy einen Rahmen ohne diesen Abstand
  (Variante in `Navigation.fs` oder eigene Wahl in `View.fs` `pageShell`), sonst scrollt die Seite um 96 px.
- Gilt auch für die Duplikat-Karte. Skeleton beim Laden und Endansicht bleiben, wie sie sind.
- Nebenfolge: Kann-Sammlung ledger Punkt 150 erledigt sich. „Rückfrage zurücknehmen“ bricht bei 320 px weiter auf drei
  Zeilen um, die Knopfzeile wird höher, die Karte kürzer, „Passt, weiter“ bleibt im Bild. §5 `ButtonSize.Choice` wird
  auf den Ist-Stand gezogen („bei 320 px bis zu drei Zeilen, etwa „Rückfrage zurücknehmen““), statt das Flag unter 360 px
  wegzulassen: kein sichtbarer Unterschied, und der Text bleibt derselbe wie im Detail.

### Funktionsabgleich (Seite Durchgehen am Handy)
| Funktion heute | Stand |
|---|---|
| „Passt, weiter“ (Knopf, Enter) | bleibt; neu fliegt die Karte nach rechts mit Fläche |
| Wisch rechts ab 90 px | ändert sich: 1:1, Fläche, Einrasten, Vibration, Flug |
| „Später“ (Knopf, S), Wisch links | bleibt; Wisch und Knopf mit neutraler Fläche und Flug |
| „Lena fragen“ / „Rückfrage zurücknehmen“ (Knopf, L), Sperrgrund darunter | bleibt; „Lena fragen“ fliegt nach links ohne Fläche |
| Wisch rechts ohne Kategorie: Toast „Erst eine Kategorie wählen“ | ändert sich: „!“, zäh, Satz in der Zeile, Feld gelb; Toast entfällt |
| Wisch-Hinweis unter dem Knopf | ändert sich: neuer Text, feste Höhe |
| Karte: Meta, Payee (Tipp öffnet Auswahl), Betrag, Kategorie-Feld, „aus Bestellnummer“, Vorschläge, Bestell-Link, Memo-Zeile, Lena-Hinweis | bleibt; im Scrollbereich der Karte (Memo-Zeile ändert ledger-wg42w) |
| „Regel anlegen“, „Details“ | bleibt; am unteren Kartenrand |
| Fortschritt, „i von n“, „Zurück“ | bleibt |
| Duplikat-Karte mit „Ist neu“, „Ist doppelt, verwerfen“ | bleibt; füllt den Platz, leere Zeile darunter |
| Kategorie-Sheet, Detail-Sheet, Regel-Sheet, Endansicht, Skeleton | bleibt |
| PC: Spalte, Tastenleiste, kein Wischen | bleibt |

### Abweichungen von der Skizze (mit Grund)
- Payee 20 px halbfett, Betrag in DS-Größe statt 18/19 px: DS `ReviewCard.payee`/`amount` und §7.14 (drei Größen).
- Zeile unter dem Knopf 12 px statt 13 px: §7.14.
- Farben aus Token-Rollen statt der Skizzen-Werte (`#2a2a50` wird eine Token-Rolle; fehlt sie, neuer Token in §3).
- Die Pille oben (Skizze 09, `.ph-pill`) wird nicht gebaut: Rest der verworfenen Option B2, Regel R3 kennt am Handy keinen Toast.
- „Von vorn (Skizze)“ in der Endansicht ist nur ein Bedienteil der Skizze.

## Acceptance criteria
- [ ] Wischen nach Abschnitt 1: Karte 1:1, Fläche mit Symbol über Wort je Richtung, Einrasten bei der Grenze von
      `reviewSwipeOf` mit Vibration, Zurückfedern darunter, Rausfliegen darüber, nächste Karte rückt nach; bei „Passt“ erst
      nach `ReviewConfirmed Ok`, bei Fehler federt die Karte zurück. Knöpfe und Tasten bewegen die Karte gleich.
- [ ] Ohne Kategorie: gelbes „!“, zäh ab etwa 72 px, kein Einrasten, „Erst Kategorie wählen“ in der Zeile, Feld gelb;
      `reviewNeedsTargetText` und sein Toast sind weg.
- [ ] Ein Wisch, der auf Kategorie-Feld, Vorschlag, Payee oder „Details“ beginnt, öffnet nichts; senkrechtes Scrollen in
      der Karte löst keinen Wisch aus.
- [ ] `prefers-reduced-motion`: kein Flug, kein Federn, Fläche und Einrasten sichtbar.
- [ ] Reine Funktionen `reviewSwipeOffset` und `reviewSwipeArmed` mit Tests in `src/Tests/LedgerReviewClientTests.fs`
      (Grenzen 72 und 90 px, beide Richtungen, ohne Ziel, schräger Weg); der Test „Wisch nach rechts ohne Ziel meldet es und
      die Karte bleibt“ prüft jetzt: Karte bleibt, kein externer Befehl.
- [ ] Zeile unter dem Hauptknopf mit fester Höhe und neuem Text, auch unter der Duplikat-Karte (leer); Knöpfe stehen
      bei Hinweis, „Erst Kategorie wählen“ und leerer Zeile an derselben Stelle (Messung im `[layout]`-Test).
- [ ] Karte füllt den Platz, „Regel anlegen“ und „Details“ am unteren Kartenrand, langer Inhalt scrollt in der Karte;
      die Seite scrollt bei 412 × 844 und 320 × 844 nicht (`[layout]`-Test in `scripts/durchgehen.test.mjs`:
      `document.scrollingElement.scrollHeight <= innerHeight`, Karte mit `REVIEW_QUEUE.amazon` und langem Memo hat
      `scrollHeight > clientHeight`).
- [ ] Sweep `scripts/layout-states.mjs`: die Ausnahme bei `durchgehen-amazon` (320, Invariante 7) ist gestrichen und der
      Sweep grün; `durchgehen-karte`, `durchgehen-karte-lena`, `durchgehen-amazon`, `durchgehen-duplikat` tragen `reach`
      auf `[data-testid="review-foot"]` (Zeile ohne Scrollen sichtbar); neuer Zustand `durchgehen-amazon-niedrig`
      (412 × 600, `height` im Zustand): Karte scrollt innen, „Passt, weiter“ und Zeile im Bild. Die Zustände bei 1280 bleiben grün.
- [ ] `[flow:durchgehen]` in `scripts/durchgehen.test.mjs` wischt zusätzlich zuerst eine Karte ohne Kategorie nach rechts
      (Karte bleibt, „Erst Kategorie wählen“ in `review-foot` während des Wischs, kein Toast) und danach wie heute mit
      Kategorie; der Satz in `FLOWS` (`scripts/browser-usage.mjs`) ist nachgezogen. Kein zweiter Flow-Test.
- [ ] DS: neue Wisch-Variante mit Eintrag in §5 und Beispielen auf `/styleguide`; §5 `ButtonSize.Choice` auf „bis zu drei
      Zeilen bei 320 px“ gezogen; neue Kurven oder Farben nur als Token (§3, §6). `guard:classes` und `guard:tokens` grün.
- [ ] Kann-Sammlung ledger Punkt 150 im Outcome als erledigt genannt (Conductor trägt es ein).
- [ ] Seite neben der Skizze verglichen: Screenshots bei 412 px im Outcome für Ruhe (01), rechts unter der Grenze (02),
      rechts eingerastet (03), links eingerastet (04), ohne Kategorie (05), zweite Karte mit kurzem Inhalt (08), dazu
      320 px Ruhe mit `REVIEW_QUEUE.amazon` und PC 1280 unverändert; Abweichungen nur die genannten.
- [ ] Gefühl der Geste (Mitgehen, Einrasten, Vibration, Flug) am echten Handy geprüft. [human-eye]

## Notes
Captured 2026-11-19 aus ledger-vf5h6. Verfeinert 2026-11-19.

**Skizze** (Basis `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):
- ganze Skizze: `.../scratchpad/vf5h6/durchgehen-handy.html`, Quelltext `.../scratchpad/vf5h6/skizze.src.html`
- `.../scratchpad/vf5h6/skizze-shots/01-durchgehen-ruhe.png`
- `.../scratchpad/vf5h6/skizze-shots/02-wisch-rechts-unter-schwelle.png`
- `.../scratchpad/vf5h6/skizze-shots/03-wisch-rechts-eingerastet.png`
- `.../scratchpad/vf5h6/skizze-shots/04-wisch-links-eingerastet.png`
- `.../scratchpad/vf5h6/skizze-shots/05-wisch-ohne-kategorie.png`
- `.../scratchpad/vf5h6/skizze-shots/08-zweite-karte-langer-payee.png` (Karte füllt den Platz bei kurzem Inhalt)
- `.../scratchpad/vf5h6/skizze-shots/13-endansicht.png` (bleibt in dieser Task)
- heute: `.../scratchpad/shots/vf5h6/heute-karte-412.png`, `heute-wisch-412.png`, Vorbild `heute-kassenbuch-wisch-412.png`

**Am Code:**
- `src/Client/Components/Ledger/View.fs`: `SwipeableReviewCard` (um Zeile 5154, Touch-Ereignisse, `dx * 0.6`, Opacity),
  `reviewNormalCard` (Zeile 5208 ff., `ReviewCard.hint reviewSwipeHintText` Zeile 5327), `reviewDuplicateCard`,
  `reviewSwipeHintText` (Zeile 5076), `ReviewPage` (Zeile 5450 ff., `relative flex min-h-full flex-col`).
- `src/Client/Components/Ledger/Types.fs`: `reviewSwipeThresholdPx`, `ReviewSwipe`, `reviewSwipeOf` (um Zeile 1284),
  `reviewNeedsTargetText` (Zeile 1342).
- `src/Client/Components/Ledger/State.fs`: `ReviewSwiped` (Zeile 4710, Toast Zeile 4715), `ReviewConfirm`/
  `ReviewConfirmed` (Zeile 4519 ff.; Flug und Rückkehr hängen an `Confirming`).
- `src/Client/DesignSystem/ReviewCard.fs` (`card`, `hint`, `linksWithDetails`, `detailsLink`), `Swipe.fs` (Vorbild Geste),
  `Navigation.fs` (`shellOffset`, `pageContent`), `src/Client/View.fs` (`pageShell`), `styles.css` (`.tx-swipe-*`, Zeile 1407 ff.;
  `prefers-reduced-motion` Zeile 735).
- Tests: `src/Tests/LedgerReviewClientTests.fs` („die Wischschwelle liegt über 90 Pixeln …“, „nur ein waagrechter Wisch …“,
  „Wisch nach rechts ohne Ziel …“), `scripts/durchgehen.test.mjs` (`swipe` per CDP-Touch erzeugt auch Pointer-Ereignisse),
  `scripts/layout-states.mjs` Zeile 2013 ff.

**Für ledger-pz5wv:** die Zeile `review-foot` ist der Platz der letzten Aktion; diese Task liefert sie mit Hinweis und
„Erst Kategorie wählen“, pz5wv füllt sie mit der letzten Aktion und ersetzt den Fehler-Toast durch die rote Zeile.
