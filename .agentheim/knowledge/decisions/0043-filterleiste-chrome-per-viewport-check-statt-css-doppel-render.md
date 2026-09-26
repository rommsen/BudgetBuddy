---
id: ADR-0043
title: Popover-vs-BottomSheet-Chrome der Filterleiste per Viewport-Check, nicht per CSS-Doppel-Render
scope: ledger
status: accepted
date: 2026-09-25
supersedes: []
superseded_by: []
related_tasks: [ledger-j8xq4]
related_adrs: [0005]
related_research: []
---

# ADR 0043: Popover-vs-BottomSheet-Chrome der Filterleiste per Viewport-Check, nicht per CSS-Doppel-Render

## Context

`ledger-j8xq4` baut für jeden Filter-Chip (Kategorie, Zeitraum, Payee, Memo, Betrag)
denselben Inhalt zweimal verpackt: als `FilterPopover` am PC, als `BottomSheet` am
Handy. Die Kassenbuch-Seite löst "zwei Fassungen fürs Gerät" sonst durchgängig über
das CSS-Doppel-Render-Muster (`Primitives.Responsive.mobileOnly`/`desktopOnly`):
beide Fassungen werden gerendert, nur eine ist per `md:hidden`/`hidden md:block`
sichtbar (siehe `entryListMobile`/`entryListDesktop`). Für dieses eine Bauteil
funktioniert das Muster nicht: `BottomSheet.view` (`Client.DesignSystem.BottomSheet`)
portalt seinen Inhalt über `createPortal` immer nach `document.body`, unabhängig
davon, ob der Aufruf-Ort selbst hinter einem `hidden`-Wrapper sitzt. Ein Doppel-Render
hätte am PC (matchMedia `min-width: 768px` wahr) zwei tatsächliche Overlays für
denselben `OpenPopover`-Zustand erzeugt: das sichtbare `FilterPopover` UND ein
unsichtbar gewähntes, aber tatsächlich im DOM aktives `BottomSheet` daneben; beide
horchen auf dieselbe Öffnen/Schließen-Nachricht. Im ersten Anlauf ist genau das
passiert und fiel erst beim Browsertest auf (`scripts/ledger-filter-bar.test.mjs`,
zwei Overlays für einen Chip).

## Decision

`Components.Ledger.View.filterChrome` entscheidet zur Renderzeit **einmal** über
`Client.DesignSystem.Viewport.isDesktopViewport()` (neu, `window.matchMedia
("(min-width: 768px)")`, Fable-Interop analog zum bestehenden `isFinePointer()`),
welche der beiden Komponenten tatsächlich gerendert wird, nie beide. Dieselbe
Entscheidung bestimmt auch die Chip-Reihenfolge (`orderChipsForDisplay isDesktop`),
damit Chrome und Reihenfolge nie auseinanderlaufen.

`isDesktopViewport()` wird, wie `isFinePointer()` bereits vorher, **einmal pro
Render** gelesen, nicht über einen live aktualisierenden Media-Query-Hook: eine
Fenstergröße ändert sich während einer offenen Filter-Interaktion in der Praxis so
gut wie nie, und Elmish rendert die ganze Seite ohnehin bei jeder Zustandsänderung neu:
ein Rotieren des Geräts mitten in einer offenen Auswahl zeigt die alte Chrome bis
zum nächsten Dispatch, statt "live" umzuspringen. Das folgt genau der schon
bestehenden Kompromissregel aus ADR 0005/`isFinePointer()`.

## Consequences

### Positive
- Nie zwei Overlays für denselben Zustand, unabhängig vom Gerät.
- Ein Muster (`filterChrome`) für alle fünf Filter-Popover/-Sheets, statt einer
  Fallunterscheidung je Filtertyp.

### Negative
- Ein Fenster-Resize während einer offenen Filter-Auswahl ändert die Chrome nicht
  sofort (siehe oben), in der Praxis kein beobachteter Fall (PWA, ein Gerät je
  Sitzung), aber ein bewusster Kompromiss, kein Zufall.
- `isDesktopViewport()` ist eine zweite, zum bestehenden CSS-Breakpoint parallele
  JS-Quelle der Wahrheit für "mobil vs. Desktop"; beide müssen denselben Wert (768px)
  tragen, sonst laufen Chip-Reihenfolge und sichtbares Layout auseinander.

## Nachtrag (dritte Iteration): sticky Versatz misst die reale Leistenhöhe

Getrennt von der Chrome-Frage oben, aber am selben Bauteil: der sticky Tabellenkopf
der PC-Tabelle (`Table.fs`) braucht einen Versatz, der die sticky Filterleiste
darüber nicht überlappt. Der erste Versuch (zweite Iteration) setzte dafür eine
feste globale Schätzung (`:root { --ledger-filter-bar-height: 44px }`), die weder
bei 1440px (Leiste tatsächlich 38px hoch, sichtbare Lücke) noch bei 1024px mit
umbrechenden Chips (Leiste tatsächlich 62px hoch, Überlappung) stimmte, und die
zudem den dokumentierten Vertrag von `Table.Sticky` global veränderte: jede
zukünftige sticky Tabelle ohne eigene Filterleiste hätte denselben Versatz geerbt.

Die dritte Iteration ersetzt die Schätzung durch eine Messung: ein
`ResizeObserver` auf der Filterleiste selbst (`Components.Ledger.View.FilterBarColumn`)
schreibt ihre tatsächliche Rand-zu-Rand-Höhe (`getBoundingClientRect().height`,
bewusst nicht `contentRect.height`, das die Padding-Differenz von `ChipBar.bar`
verschluckt hätte) als Inline-Style auf die Kassenbuch-Spalte, `styles.css` liest
die Variable mit Fallback `0px`. Der Vertrag von `Table.Sticky` bleibt damit für
jede Tabelle ohne eine solche Spalte unverändert: der Versatz ist `64px`, nicht
`108px`.

## Nachtrag (2026-09-26): die Höhenmessung folgt dem Knoten per Callback-Ref

Der Verifier der dritten Iteration fand einen weiteren Fall, den die dritte Iteration nicht deckte:
während eines laufenden Spiegel-Laufs maß der `ResizeObserver` den falschen Knoten.
Ursache: der Observer band sich in einem `useEffect` mit leeren Abhängigkeiten einmalig
an den Knoten, den `barRef` (ein `React.useRef`) beim allerersten Mount hielt. Die
Kinder von `FilterBarColumn` sind eine keylose Liste, an deren erster Stelle bedingt
der Hinweis "Spiegel läuft" steht (`Loading.inlineWithText`, ebenfalls ein `div`).
Wird `model.MirrorRunning` NACH dem ersten Mount wahr, wechselt React am selben
Listenplatz vom Leisten-`div` auf den Hinweis-`div` (beide Host-Typ "div", React
verwendet den vorhandenen Knoten deshalb weiter) und legt für die eigentliche Leiste,
jetzt eine Position weiter hinten, einen neuen Knoten an. `barRef.current` zeigt
danach zwar korrekt auf diesen neuen Knoten (React aktualisiert Objekt-Refs bei jedem
Wechsel), aber der bereits einmal gelaufene `useEffect` bemerkt das nicht: er bleibt
für die Lebensdauer der Komponente an den zuerst beobachteten, jetzt als Hinweis
weiterverwendeten Knoten gebunden. Gemessen mit veraltetem
`getLastMirrorRun` und offenem Spiegel-Lauf: 18px Überlappung des Tabellenkopfs bei
1440x900, 42px bei 1024x900 mit aktivem Filter.

Romans Entscheidung: ein Callback-Ref an `ChipBar.bar`, kein Objekt-Ref mit
begleitendem `useEffect`. React ruft einen Callback-Ref bei jedem Knotenwechsel neu
auf, mit dem neuen Knoten oder mit `null` beim Unmount, nicht nur einmal beim Mount.
`FilterBarColumn` merkt sich die laufende Observer-Trennfunktion in einem
`React.useRef`; der Callback trennt sie zuerst, dann beobachtet er, falls ein Knoten
da ist, den neuen. Zwei weitere verworfene Alternativen dazu unten unter
"Alternatives considered".

Neue Browsertests in `scripts/ledger-filter-bar.test.mjs`: ein veralteter
`getLastMirrorRun`-Stand löst den automatischen Lauf aus (ADR 0014, absichtlich um
300ms verzögert, damit die Spalte nachweislich zuerst mit `MirrorRunning = false`
mountet), `mirrorYnabBudget` bleibt danach absichtlich offen
(`mockRunningMirror`, `scripts/ledger-filter-bar.mjs`), ein Test misst Leiste und
Tabellenkopf bei 1024x900, während der Hinweis "Spiegel läuft" sichtbar ist. Ein
zweiter Test lässt den Lauf enden und prüft denselben Abstand, nachdem der Hinweis
wieder verschwunden ist. **Falsifiziert:** `ChipBar.bar` und `FilterBarColumn`
testweise auf den vorherigen Objekt-Ref/`useEffect [||]`-Stand zurückgesetzt (Edit-
Tool, kein Git), neu gebaut: genau der erste der beiden neuen Tests schlägt fehl
(`theadTop=84, barBottom=102`, 18px Überlappung, exakt der vom Verifier gemeldete
Fall), der zweite bleibt grün, weil React den ursprünglichen Leisten-Knoten nach
Ende des Laufs wieder an derselben Listenposition verwendet und die Messung dadurch
zufällig wieder stimmt, unabhängig vom Fix. Danach die Mutation zurückgesetzt und
neu gebaut. `node --test scripts/ledger-filter-bar.test.mjs` fünfmal hintereinander
frisch gelaufen: 70/70 grün (14 Tests je Lauf), keine Flakiness.

## Alternatives considered
- **CSS-Doppel-Render wie beim Rest der Seite**: verworfen, erzeugt nachweislich
  zwei aktive Overlays am selben Chip (siehe Context), weil `BottomSheet` seinen
  Sichtbarkeits-Zustand nicht aus einer CSS-Klasse am Aufruf-Ort, sondern aus einem
  portalten, eigenen DOM-Ast bezieht.
- **Ein React-Hook mit `matchMedia`-`addEventListener` für einen live
  aktualisierenden Zustand**: verworfen für diese Aufgabe, mehr Komplexität
  (Subscription, Cleanup) für einen Fall (Resize während offener Auswahl), der in der
  Praxis nicht auftritt; `isFinePointer()` hat dieselbe Vereinfachung bereits
  vorgemacht.
- **Eine feste globale Schätzung für den sticky Versatz** (dritte Iteration): verworfen,
  siehe Nachtrag oben; passte auf keiner der beiden gemessenen Breiten und weichte
  den `Table.Sticky`-Vertrag global auf.
- **Bar und Tabellenkopf in einem gemeinsamen sticky Container**: verworfen, `thead`
  steckt in einer `<table>`, die Filterleiste in einem eigenen `div`; eine gemeinsame
  Sticky-Hülle hätte die bestehende `Table.view`/`ChipBar.bar`-Trennung aufgebrochen,
  für einen Fall, den eine Messung ohne DOM-Umbau löst.
- **Den Spiegel-Lauf-Hinweis hinter die Leiste verschieben** (2026-09-26, vierte
  Iteration): verworfen, hätte die sichtbare Reihenfolge geändert (der Hinweis stünde
  dann unter statt über der Leiste), was die Aufgabe nicht verlangt und ohne
  fachlichen Grund von der bisherigen Optik abgewichen wäre.
- **Stabile Keys für die Kinder der Spalte, ohne Callback-Ref** (2026-09-26, vierte
  Iteration): verworfen als alleinige Lösung, schützt nur so lange, wie niemand
  künftig eine weitere bedingte Zeile ohne Key einfügt; ein Callback-Ref bleibt
  robust unabhängig vom Aufbau der Kinderliste. Stabile Keys dürfen zusätzlich
  gesetzt werden, ersetzen den Callback-Ref aber nicht.

## References
- `src/Client/DesignSystem/Viewport.fs` (`isDesktopViewport`, `isFinePointer`)
- `src/Client/Components/Ledger/View.fs` (`filterChrome`, `filterChipBar`,
  `FilterBarColumn`, `observeHeight`, der Callback-Ref `barCallbackRef`)
- `src/Client/DesignSystem/FilterPopover.fs`, `BottomSheet.fs`, `ChipBar.fs` (`bar`s
  `elementRef`-Parameter, seit der vierten Iteration ein Callback-Ref)
- `src/Client/DesignSystem/Table.fs` (`Sticky`-Vertrag)
- `src/Client/styles.css` (`.ledger-filter-bar`, `.table-sticky-thead thead`,
  `--ledger-filter-bar-height`)
- `scripts/ledger-filter-bar.test.mjs` (Browserbeleg für PC-Popover- UND
  Handy-BottomSheet-Verhalten, zweite Iteration; Kollisionstests bei 1440px und
  1024px mit umbrechenden Chips, dritte Iteration; Kollisionstests während und nach
  einem laufenden Spiegel-Lauf, vierte Iteration)
- `scripts/ledger-filter-bar.mjs` (`mockRunningMirror`, vierte Iteration)
