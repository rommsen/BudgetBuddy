# Styleguide — Visuelle Sprache, Tokens, Komponenten, Muster

> **Das ist das Gate.** Dieses Dokument ist die Single Source of Truth für alle
> UI-Entscheidungen in BudgetBuddy. Jeder neue Frontend-/UI-Task in **jedem** BC wird
> daran gemessen; abweichender View-Code (hartkodierte Farben, inline-Feliz statt
> DS-Komponente, eigene Muster) gilt als **Drift** und gehört korrigiert.
> Es kodifiziert *retroaktiv*, was bereits im Code lebt — es erfindet keine neue Sprache.
>
> **Quelle, nicht Kopie:** Implementierungsdetails (genaue Klassen, Props, Hex-Werte)
> stehen im Code und sind dort autoritativ. Dieses Dokument verlinkt die Quellen und
> beschreibt Bedeutung und Regeln — es dupliziert keine Details, die veralten.
>
> **Verwandt:** [`overview.md`](overview.md) (MVU-Architektur),
> [`src/Client/DesignSystem/`](../../src/Client/DesignSystem/) (Komponenten-API in den
> Komponenten und ihren Doc-Kommentaren).
>
> **Lebendes visuelles Gegenstück:** die In-App-Route **`/styleguide`** (erreichbar in
> der App, dezenter Link in den Einstellungen) rendert die **echten** DS-Komponenten und
> Tokens als Galerie — gegliedert wie dieses Dokument. Dieses Markdown bleibt der
> geschriebene Begleiter (Farbsemantik, „wann nicht", Voice, ADR-Muster); die Route
> zeigt das Aussehen live und kann per Konstruktion nicht driften
> (`src/Client/Components/Styleguide/View.fs`).

---

## 1. Visuelle Sprache: neon-on-dark

Das Look-and-Feel ist **neon-on-dark** — ein dunkler Grund mit hellen Neon-Akzenten und
dezentem Glow. Es trägt direkt das Vision-Ziel "Roman fühlt den Workflow als angenehmer
als YNAB-Web": die Oberfläche soll fokussiert, schnell und ein bisschen "cyber" wirken,
nicht wie eine generische Tabelle.

Die drei tragenden Mittel:

- **Dunkler Grund.** Flächen sind dunkel (`bg-surface-app` / `-card` / `-elevated` /
  `-input`), gestaffelt nach Tiefe. Neon-Farben wirken nur auf dunklem Grund.
- **Glow als Hervorhebung, nicht als Deko.** Glow-Shadows (`shadow-glow-*`,
  `text-glow-*`) markieren das Aktive/Wichtige — primäre CTAs, positive Beträge,
  fokussierte Karten. Glow ist sparsam: alles glüht = nichts glüht.
- **Mono-Font für Zahlen und Daten.** Beträge, Counts, technische Werte stehen in
  JetBrains Mono mit `tabular-nums`, damit Ziffern in Spalten sauber untereinander
  stehen. Fließtext in Outfit (`font-sans`), große Header optional in Orbitron
  (`font-display`).

Quelle der Tokens: [`src/Client/DesignSystem/Tokens.fs`](../../src/Client/DesignSystem/Tokens.fs)
(Modul-Header dort: *"Design tokens for the Neon Glow Dark Mode Theme"*). Die Tokens
spiegeln die CSS-Custom-Properties in `src/Client/styles.css` und die Tailwind-Config.

---

## 2. Farbsemantik

Jede Neon-Farbe hat **eine feste Bedeutung**. Farbe wird nie rein dekorativ gewählt —
sie kommuniziert Zustand/Rolle. Belegt durch `Tokens.fs` (Modul `Colors`, `Glows`,
`Borders`, `Backgrounds`) und die jeweiligen Token-Kommentare:

| Farbe | Bedeutung | Token-Beispiele (`Tokens.fs`) |
|-------|-----------|-------------------------------|
| **Orange** | primär / CTA / Action / Energie; auf der Seite Targets prüfen zusätzlich der Titel "Gibst du mehr aus?" und die Monatsbalken über dem Target (Ausnahme, Nachtrag ADR 0075 vom 2026-11-06) | `Colors.neonOrange`, `Glows.orangeLg` (Hero-CTAs), `Borders.orange` |
| **Teal** | Ausgewähltes und Navigation (ADR 0075); auf der Seite Targets prüfen zusätzlich der Titel "Passt" und die Zusätze einer Zeile darunter (alle aus `fitsNoteText`, etwa "neu gesetzt am …" und "neu seit …", Ausnahme, Nachtrag ADR 0075 vom 2026-11-06) | `Colors.neonTeal`, `Glows.teal`, `Presets.glowCard` (teal-Border) |
| **Green** | Erfolg / positiv / Wachstum | `Colors.neonGreen`, `Glows.green`, `Presets.moneyPositive` |
| **Red** | Fehler / Gefahr / negativ | `Colors.neonRed`, `Presets.moneyNegative`, `Borders.red` |
| **Purple** | Akzent / "special/premium"; Kategorie nur außerhalb von Listen (ADR 0049) | `Colors.neonPurple`, `Backgrounds.purpleSubtle` |
| **Violett hell** | nur der Titel "Gibst du nie aus?" auf der Seite Targets prüfen (Ausnahme, Nachtrag ADR 0075 vom 2026-11-06). Heller als `neonPurple`, das auf `bg-hover` unter 4,5:1 fällt; kontrastiert 9,21:1 auf `bg-card`, 8,40:1 auf `bg-elevated`, 8,00:1 auf `bg-hover` | `Colors.calmViolet` |
| **Pink** | Akzent / "attention needed", nicht auf der Budget-Seite (ADR 0075) | `Colors.neonPink`, `Backgrounds.pinkSubtle` |
| **Gelb** | offen, kein Fehler: `fehlt` und "Passt das Target noch?" (ADR 0075). Kein Neon, kontrastiert 9,03:1 auf `bg-card`, 8,23:1 auf `bg-elevated`, 7,84:1 auf `bg-hover` | `Colors.calmYellow` |
| **Blau** | nur der Titel "Gibst du weniger aus?" auf der Seite Targets prüfen (Nachtrag ADR 0075, budgeting-c4ve4). Ruhig, kein Neon, kontrastiert 8,93:1 auf `bg-card`, 8,14:1 auf `bg-elevated`, 7,75:1 auf `bg-hover` | `Colors.calmBlue` |

Die Token-Kommentare in `Tokens.fs` sind die kanonische Begründung (z. B. *"Primary:
Neon Green — Success, Growth, Positive"*, *"Accent: Cyber Teal - Ausgewähltes und
Navigation (ADR 0075)"*).

**Praktische Konsequenzen:**

- Geld als **Einzelbetrag mit Gewicht** (Hero-Saldo, Kennzahl, Detail-Kopf): positiv
  Green mit Green-Glow (`Presets.moneyPositive`), negativ Red (`Presets.moneyNegative`).
  `Money.view` setzt das automatisch anhand des Vorzeichens. Nicht manuell überschreiben.
- Geld **in Listen und Tabellen**: neutral, siehe „Ruhige Listen“ unten (ADR 0049).
- Primäre Aktion eines Screens: Orange (`Button.primary` / `Button.hero`). Pro
  Screen-Kontext höchstens eine primäre Aktion.
- Sekundär/Abbrechen: neutral mit grauem Rand (`Button.secondary`, ADR 0075), auch im Hover.
- Navigation und Ausgewähltes: Teal (aktiver Nav-Tab, aktiver Tab, gewählte Zeile).
- Löschen/Zerstörendes: Red (`Button.danger`, `Modal.confirmDanger`).
- Text-Tonwerte über `Colors.textPrimary/Secondary/Muted` — nicht über Neon-Farben.

### Ruhige Listen (ADR 0049)

Gilt für Listen und Tabellen mit wiederkehrenden Zeilen (Kassenbuch, Import, Regeln, die Bewegungslisten
der Budget-Historie).
Vorbilder sind das Kassenbuch und der Abschnitt 8a in `/styleguide`. In jeder Zeile ist Farbe ein Signal, keine
Dekoration: höchstens ein farbiger Akzent pro Zeile, und der meldet einen Zustand.

**Ausnahme Budget-Seite (ADR 0075 Punkt 4):** Rot und Gelb dürfen in einer Budget-Zeile
zusammenstehen, Rot am Verfügbar-Betrag im Minus, `fehlt` in Gelb. Die Ausnahme gilt nur dort.

| Element | PC | Handy |
|---------|----|-------|
| Payee | Inter 500 14px `text-primary` | Inter 500 15px `text-primary` |
| Betrag | Mono 500 14px `tabular-nums` `text-primary`, rechtsbündig; positiv Teal | Mono 500 15px, sonst gleich |
| Datum | Mono 400 12px `text-muted` | nur im Gruppenkopf |
| Kategorie | 12px `text-secondary`; „Keine Kategorie“ Orange | gleich |
| Memo | 12px `text-muted`, eine Zeile | gleich |
| Zu prüfen | 3px-Kante Pink links, dazu kleines Wort „zu prüfen“ 11px Pink | 3px-Kante Pink links |
| Datumskopf | Mono 600 10px, `letter-spacing:.16em`, `text-muted`, Tagessumme rechts | gleich |
| Spaltenkopf | Mono 600 10px, `letter-spacing:.12em`, `text-muted`, Höhe 42px | entfällt |
| Zeilenhöhe | 44px | zweizeilig, `padding:11px 16px` |
| Kontostand | `text-primary` (aktiv) oder `text-secondary`; negativ Red | gleich |

Zwei Ausnahmen, beide nur für die Budget-Seite:

- **Dritte Zeile am Handy:** Die Kategorie-Zeile trägt das Target als dritte Zeile. Sie hat dieselbe
  Klasse wie die zweite (`ListRoles.mobileLine2`) und fehlt, wenn es weder Target noch `fehlt` gibt.
  Das Target steht in `truncate min-w-0`, der Status (fehlt, pausiert) in `shrink-0 whitespace-nowrap`:
  wird es eng, kürzt das Target und nie der Status. Target und pausiert stehen in
  `ListRoles.secondary`, `fehlt` in `ListRoles.missing`. Die Beträge der zweiten Zeile tragen
  `tabular-nums`. "erfüllt" steht nicht in der Liste, nur im Tab Target.
- **Dichte Zeile am PC:** Die Zeile misst 40 px (`ListRoles.denseRowHeight`), der Gruppenkopf höchstens
  32 px. Die Spalte Target bleibt einzeilig: Betrag in Mono, Rhythmus als eigener Span in Inter,
  dahinter pausiert in `ListRoles.secondary`. Der Name kürzt zuerst. Erst wenn ihm neben `fehlt`
  weniger als 80 px blieben, rückt `fehlt` in eine zweite Zeile, die Zeile wird dann höher.

**Bewegungslisten der Budget-Historie** (Tab Historie im Inspektor, allgemeine Historie): Der Betrag
steht neutral in `text-primary` mit Vorzeichen ("+20,00 €", "-20,00 €", `Money.signedListAmount`),
auch positiv ohne Teal. Das Stand-Badge ist der einzige Farbakzent der Zeile.

#### Auswahl (Picker, `searchableSelect`)

Auswahllisten sind wiederkehrende Zeilen und folgen derselben Ruhe (design-system-zahv9).
Gemeinsame Bausteine in `DesignSystem/PickerParts.fs`, benutzt von `CategoryMultiSelect`
(Popover) und `BottomSheet.categoryPicker` (Sheet).

| Element | Regel |
|---------|-------|
| Zeile | einzeilig, Name `ListRoles.primary` (15px Handy, 14px PC, 500) mit `truncate`, Betrag rechts in fester Breite |
| Gruppenkopf | `ListRoles.groupHeader`; „Alle Kategorien“ steht im Sheet nur über „Vorgeschlagen“ oder „Zuletzt verwendet“, nicht bei Suche |
| Verfügbar-Betrag | `Money.available`: Mono 500, `tabular-nums`, kein Glow; positiv `text-primary`, 0 `text-muted`, negativ Red |
| Zu verteilen | Optional erste Zeile im Kategorie-Picker (`ToBeBudgeted`), mit Betrag rechts in `Money.available`; die Suche findet sie. Gewählt liefert sie `ChoseToBeBudgeted`. Wer die Prop nicht setzt, sieht die Zeile nicht |
| Suchfeld | `Input.Small`: 16px unterhalb `md`, 14px ab `md`, 36px hoch, ein Fokusrand |
| Footer | „Zurücksetzen“ Ghost, „Übernehmen“ kompakt ohne Glow, einzeilig; Zahl nur bei Auswahl („Übernehmen (3)“) |

`searchableSelect` gliedert Optionen der Form „Gruppe: Name“ nach Gruppen (Kopf wie oben,
Zeile nur mit dem Namen); die Suche findet Gruppe und Name. Texte deutsch („Suchen…“,
„Keine Treffer“).

Kategorie-Picker mit Einfachauswahl (`DesignSystem/CategoryPicker.fs`, design-system-gffps): ein
Inhalt, zwei Hüllen. Am PC `CategoryPicker.popover` (Mechanik von `FilterPopover`, 460px breit),
am Handy `CategoryPicker.sheet` bzw. `sheetLayered` (über dem Detail). Aufbau von oben: Suchfeld,
Zeile „Ohne Kategorie“, Abschnitt „Vorgeschlagen“ zum Payee (höchstens drei, kein „Zuletzt
verwendet“), „Alle Kategorien“ nur darunter und nicht bei Suche, Gruppen mit Available rechts
(`Money.available`), „Versteckt (n)“ eingeklappt, optional eine Stand-Zeile. Ein Klick übernimmt
sofort, es gibt kein „Übernehmen“. Pfeile bewegen, Enter wählt, Esc schließt nur den Picker.
Ohne Kategorien-Liste ersetzt ein Hinweis die Zeilen (`LoadState`): „Kategorien werden geladen“ mit
Spinner, nach einem Fehler „Kategorien konnten nicht geladen werden“ mit dem Knopf „Erneut versuchen“.
Beim Nachladen mit vorhandener Liste bleibt der Picker unverändert.
Für die Konten gilt dasselbe über `AccountsLoad` (ledger-vq2g8): im Abschnitt „Übertrag auf Konto“ steht
„Übertrag-Ziele werden geladen“ mit Spinner oder „Übertrag-Ziele konnten nicht geladen werden“; die Kategorien bleiben.
Ziel einer Split-Zeile (design-system-bym4m): derselbe Picker mit `Accounts` (Kassenbuch und Import). Unter
den Kategorien steht der Abschnitt „Übertrag auf Konto“ mit Zeilen „→ Name“, die Suche läuft über beides,
das Präfix „Konto:“ zeigt nur die Konten. Konten stehen nie unter „Vorgeschlagen“ oder „Zuletzt verwendet“,
ein Ladehinweis lässt sie wählbar. Ohne `Accounts` zeigt der Picker keine Konten. Es gibt keinen eigenen
Konto-Picker und kein Sync-Symbol am Ziel-Feld.
`CategoryPicker.CategoryChangeCard.view` ist die Karte „Budget nach dem Wechsel“ (alter Wert
durchgestrichen und gedämpft, Pfeil, neuer Wert), eine leere Liste rendert nichts. Beide stehen
in `/styleguide` unter „26. Kategorie-Picker (Einfachauswahl)“.
Im Buchungs-Detail öffnet `Detail.pickerArrow` (Pfeil rechts an der Kategorie-Zeile, `Detail.fieldRowWith`
macht die ganze Zeile klickbar) den Picker: am PC `CategoryPicker.popoverInPanel` (Breite des
420-px-Panels abzüglich Rand), am Handy `sheetLayered`. Sitzt der Auslöser im Scrollbereich des
Panels (Tab Geld), nimmt man `CategoryPicker.popoverInPanelRevealed`: es rückt beim Öffnen ins Bild und
nimmt die volle Breite des schon eingerückten Inhalts. Der Pfeil ist bei gesperrten Buchungen
gedämpft (`opacity-40`), ohne Wirkung und nennt den Grund im Tooltip. Beide Zeilen stehen in
`/styleguide` bei „Detail: Felder“.
Für die Auswahl-Leiste (Aktion „Kategorie setzen“, ledger-w29y6) gibt es zwei Varianten ohne
markierte Zeile, denn mehrere Buchungen haben keine gemeinsame aktuelle Kategorie: der Picker
öffnet immer ohne Haken, auch bei „Ohne Kategorie“. Am PC `CategoryPicker.selectionPopover`
(`FilterPopover.viewAbove`: öffnet über dem Auslöser, rechtsbündig mit ihm, weil die Leiste
fest am unteren Bildschirmrand sitzt), am Handy `CategoryPicker.selectionSheet` mit der
Unterzeile „n Buchungen · Summe“. Beide stehen in `/styleguide` unter „26. Kategorie-Picker
(Einfachauswahl)“.

Datumsfeld (`DesignSystem/DateField.fs`, design-system-m7z9q): Textfeld „TT.MM.JJJJ“ in
`Input.Small`-Maßen, immer deutsch, daneben ein Kalender-Knopf für den nativen Kalender.
Ungültige Eingabe zeigt den Fehlerzustand mit dem Hinweis „Datum als TT.MM.JJJJ“ und sperrt
„Übernehmen“ (`PickerParts.applyButtonWhen`). `ReadOnly` (budgeting-g2ty9) entspricht `Input.ReadOnly`:
Textfeld `readOnly`, Kalender-Knopf `aria-disabled`, beide behalten den Fokus, gedämpft
(`opacity-60 cursor-default`), etwa solange ein Speichern läuft. `Disabled` setzt `disabled`, gedämpft
(`opacity-50 cursor-not-allowed`). In beiden Zuständen hat der Kalender-Knopf kein Hover. Zu sehen in
`/styleguide` Abschnitt 8a.

- Kein Glow in Listen, keine violetten Kategorien, keine roten Ausgaben.
- Herkunfts- oder Spiegel-Badges gehören in Detail und Historie, nicht in die Zeile.
- Listen-Seiten bekommen einen schlanken Kopf ohne Gradient-Titel und füllen die Breite.
- Rückmeldungen auf Aktionen (Wisch-Geste, Toast) dürfen leuchten.

> **Harte Regel (siehe `.claude/rules/design-tokens.md`):** keine Standard-Tailwind-Farben
> (`red-*`, `green-*`, `blue-*`, `gray-*`, …), keine hartkodierten Hex-Werte
> (`bg-[#…]`, `text-[#…]`), keine inline `style.color`. Immer die semantischen Tokens.
>
> **Durchsetzung:** `npm run guard:tokens` (`scripts/token-guard.mjs`, läuft in
> `npm run test:scripts`; baut den Client einmal, `scripts/run-guards.mjs`) scheitert bei Hex- und rgb-Arbitrary-Werten, Standard-Tailwind-Farben,
> `style.color`/`backgroundColor`/`borderColor`/`fontSize` und `text-[Npx]` in jeder `.fs` unter
> `src/Client/`. Nur die Definitionsschicht `src/Client/DesignSystem/` darf Treffer haben, und
> nur mit Eintrag samt Grund in `scripts/token-guard-exceptions.json`. Feature-Code hat keine
> Ausnahme: fehlt ein Wert, kommt er zuerst als Token ins Design-System.

### Monatsbalken

`Stats.monthBars` (Tab Historie): Der Betrag unter dem Balken steht immer in `text-secondary`. Überwiegen in
einem Monat die Erstattungen, steht er mit Plus da ("+85") und nicht in Rot: Rot heißt auf der Budget-Seite
"im Minus". Über den Balken liegt eine Wertzeile fester Höhe in `font-mono`. Maus, Tipp oder Tab-Taste wählen
einen Balken, die Zeile zeigt dann Monat und genauen Betrag über die ganze Breite (läuft nie über den Rand,
nimmt keinen Tipp an), der Balken wird etwas heller und sein Monatsname steht in `text-primary`, damit auch
ein flacher Balken die Wahl zeigt. Kein Fokus-Ring, die Wahl zeigt den Fokus. Ohne Auswahl bleibt die Zeile
leer. Das `li` ist ein reines Listenelement, den Fokus trägt ein inneres Element mit `role "img"` und
`aria-label`; vorgelesen wird das Label mit Komma statt "·". Die Vorrangregel (Maus vor Fokus vor
Vorwahl) steht in `styles.css` an einer Stelle.

`Stats.sparkBars` (Mini-Balken, Seite Targets prüfen, budgeting-c4ve4): 12 Balken ohne Beschriftung in einer
Listenzeile, die Reihe füllt die Höhe ihres Elternelements (am PC 32 px in einer Spalte von 132 px, am Handy
28 px über die volle Breite), Ecken 2 px (`rounded-[2px]`, nicht `rounded-sm`, das hier 8 px ist), Abstand am Handy
4 px und ab einem Container von 720 px (dieselbe Grenze, an der die Zeile auf vier Spalten wechselt) 3 px wie in
der Skizze, jeder Balken mindestens 2 px hoch. Die Höhe ist relativ zum größten
Wert aus Monatsausgaben und Monatswert des Targets (`checkBars` in `Shared.Budgeting`). Farben: über dem
Monatswert `bg-neon-orange` (ADR 0082 Punkt 6, ohne Spielraum), gezählt und nicht darüber gedämpft
`bg-text-muted/45`, vor der letzten Änderung (nicht gezählt) dunkler `bg-text-muted/15`. Bei einem Target liegt
eine gestrichelte Linie in `text-primary/60` auf Höhe des Monatswerts. Für den Screenreader ist die Reihe ein Bild
mit zusammengefasstem `aria-label` ("Ausgaben der letzten 12 Monate, 11 über dem Target"), die Balken sind
verborgen, es gibt keinen Tab-Stopp. Orange auf einer Listenzeile ist eine Ausnahme von §2 "Ruhige Listen", sie
gilt nur für diese Seite (Nachtrag ADR 0075). `Stats.sparkBarsLabelled` ist dieselbe Reihe groß im Panel (56 px
Balken, Ecken 3 px), darunter die Monatskürzel in Mono klein. `Stats.sparkBarsPlaceholder` steht beim Laden an ihrer Stelle, mit denselben Ecken von 2 px.

---

## 3. Token-Layer — Tokens statt hartkodierter Klassen

UI referenziert **benannte Tokens** aus `Tokens.fs`, nicht rohe Tailwind-Klassen. Der
Token-Layer ist die Indirektionsschicht über Tailwind 4 (kein DaisyUI, ADR 0031): ändert
sich der Look, ändert sich der Token — nicht hunderte Call-Sites.

Module in [`Tokens.fs`](../../src/Client/DesignSystem/Tokens.fs):

- `Colors`, `Backgrounds`, `Borders`, `Glows`, `TextGlows` — Farbsemantik (s. o.).
- `Fonts` (`sans`/`display`/`mono`), `FontSizes` (mobile-first, z. B.
  `pageTitle = "text-xl md:text-4xl"`), `FontWeights`.
- `Spacing`, `Padding`, `Margin` — alle mobile-first (z. B.
  `Padding.cardMobile = "p-4 md:p-6"`).
- `Radius`, `ZIndex`, `TouchTargets` (`minSize = "min-h-[48px] min-w-[48px]"`; `minHeight` 48 px nur am Handy;
  `mobileMinHeight` und `mobileSquare` als Handy-Wert, den PC-Wert setzt der Aufrufer mit `md:` dahinter;
  `mobileOverlap` für kleine Knöpfe in einer festen Zeile), `Breakpoints`.
- `Animations`, `StaggerDelays`, `Transitions` — siehe Motion (§6).
- `Presets` — fertige Kombinationen für wiederkehrende Muster.

**Presets sind der bevorzugte Einstieg** (Beispiele aus `Tokens.fs`):

- `Presets.card`, `Presets.glassCard`, `Presets.glowCard` — Karten-Looks.
- `Presets.pageHeader`, `Presets.sectionHeader` — Header-Typografie.
- `Presets.monoNumber` (`"font-mono font-semibold tabular-nums"`) — Zahlen.
- `Presets.moneyPositive` / `Presets.moneyNegative` — Geld-Farbe inkl. Glow.
- `Presets.gradientText` — Teal→Green→Orange-Verlaufstext für Hero-Titel.

**Reihenfolge der Wahl:**
1. Gibt es eine **DS-Komponente** für das Ding? → Komponente nutzen (§5).
2. Sonst: gibt es ein **Preset**? → Preset nutzen.
3. Sonst: einzelne **Tokens** kombinieren.
4. Rohe Tailwind-Klassen oder Hex-Werte sind die Ausnahme und gelten als Drift.

**Lage eines `FilterPopover`** gehört zur Komponente, nicht in Klassen am Aufrufer. Neben
`view` (unter dem Auslöser, linksbündig) und `viewAbove` gibt es `viewEnd` (rechtsbündig, für
einen Auslöser am rechten Seitenrand, etwa das Menü der Regelseite) und `viewRevealed` (rückt
beim Öffnen ins Bild, für einen Auslöser im Scrollbereich eines Dialogs). Der Kategorie-Picker
nutzt die zweite Variante über `CategoryPicker.popoverInDialog`.

---

## 4. Typografie- und Spacing-Skala

- **Mobile-first.** Token-Größen tragen die Responsiveness in sich
  (`md:`-Prefix bereits eingebaut, z. B. `FontSizes.body = "text-[15px] md:text-base"`).
  Keine eigenen `text-[Npx]` setzen — das umgeht die Skala (Drift, siehe Rule).
  Zwischen `xs` (12px) und `sm` (14px) liegt `FontSizes.compact` (13px), für Kopf und Werte der Leiste
  Monatserster auf der Budget-Seite (Skizze, budgeting-m3x7f); `FontSizes.microPlus` (11px) trägt dort die Bezeichnungen.
- **Gekürzter Text** auf einer Zeile über `Truncate.text` (oder `Truncate.textProps` und `Truncate.props`): setzt
  `truncate min-w-0` und `title` mit dem ganzen Text zusammen, damit er beim Zeigen lesbar bleibt.
- **Touch-Targets ≥ 48px** auf Mobile (`TouchTargets.minSize`). Tappables bekommen
  außerdem `touch-action: manipulation`. Am PC dürfen Zeilen dichter sein (§7.15).
- **Je Ansicht** höchstens drei Größen aus dieser Skala (§7.14).
- **Zahlen** immer `tabular-nums` (über `Presets.monoNumber` / `Money`).

---

## 5. Komponenten-Inventar (23 DS-Komponenten mit eigener Zeile)

Alle Komponenten liegen unter
[`src/Client/DesignSystem/`](../../src/Client/DesignSystem/). Konkrete API/Props stehen dort in den
Komponenten und ihren Doc-Kommentaren, Live-Beispiele zeigt die Route `/styleguide`. Hier
steht nur **wann welche / wann nicht**. Weitere Bausteine wie `ChipBar`, `DateField`, `Detail`,
`FilterPopover`, `MultiSelect`, `CategoryMultiSelect`, `PickerParts` und `SplitEditor` haben noch keine
eigene Zeile; §2 und §7 nennen sie, wo sie gelten.

> **Drift-Definition:** Inline-Feliz für etwas, das eine dieser Komponenten abdeckt,
> ist Drift. Erst Komponente suchen, dann bauen.

| Komponente | Wann nutzen | Wann **nicht** |
|------------|-------------|----------------|
| **Button** | Jede klickbare Aktion (dazu `amountStep` für Schritte neben einem Feld, hält den Fokus; `sideRow` und `sideRowFixed` für die Zeilen `von` und `nach`): `primary` (Hauptaktion, gefüllt im Akzent, ADR 0087), `secondary` (neutral mit grauem Rand, ADR 0075), `ghost`, `danger`, `hero`/`heroTeal` (große CTAs), `*Loading`, `IsBusy` (der eigene Aufruf läuft auf einem Knopf, der den Fokus haben kann: Spinner, `aria-disabled` und `aria-busy`, kein `disabled`, der Fokus bleibt), `*WithIcon`, `group`, in Listen `selectableListRow` (Zeile, die etwas wählt, gewählt mit `aria-current` und Akzent), `stepArrow`/`stepArrowGap` (`‹ ›`, der Platz bleibt am Rand frei), `homeLabel` (Name, der nach Hause führt und im Akzent steht, wenn man woanders ist; `homeLabelCompact` schmal für ein kurzes Wort wie „Nov 2026“ im Kopf der Budget-Seite am Handy), `listName` (Name einer Zeile als Knopf, sieht aus wie der Name, am Handy 48 px Trefferfläche ohne höhere Zeile) `listRow` (ganze zweizeilige Listenzeile als Knopf) und `disclosureListRow` (Zeile, die einen Block darunter aufklappt: Chevron in `text-muted` rechts, dreht sich beim Öffnen, `aria-expanded` und `aria-controls`, der Block bleibt zu `hidden` im DOM), `tile` (Kachel mit Wert in Mono 13 px und Bezeichnung 11 px, Radius 10 px, mindestens 52 px hoch, Zustand offen, erledigt oder aktiv mit Rand im Akzent; Leiste Monatserster) und `quietText` (leiser Textknopf 12 px gedämpft, „Ausblenden“) | Navigation zwischen Seiten → `Navigation`; Link nach extern → eigener Anker mit `Icons.externalLink` |
| **Card** | Inhaltsblöcke: `standard`, `glass`, `glow` (featured), `withAccent`, `emptyState`, plus `header`/`body`/`footer` | Volle Seiten-Layouts → `Primitives`; modale Inhalte → `Modal` |
| **Badge** | Kompakter Status/Label: semantisch (`success`/`warning`/`error`/`info`, `notice` in Gelb für „braucht dich, kein Fehler“, immer mit Wort, ADR 0087 Punkt 3), Domänen-Status (`imported`, `pendingReview`, `autoCategorized`, `uncategorized`), `count` | Klickbare Filter → `Button.ghost`/Pills; lange Texte |
| **Input** | Alle Formularelemente: `text`/`password`/`email`/`number`, `textarea`, `select`/`selectSimple`, `checkbox`, `checkboxNative` (die native Checkbox des Browsers ohne eigene Gestalt, in einem `label` mit der Zeile; Vorschau Targets anwenden nach der Skizze), `toggle`, `searchableSelect`, plus `group`/`groupWithError`/`formSection` | Reine Anzeige; mobiles Kategorie-/Konto-Auswählen → `BottomSheet` (§7) |
| **Modal** | Zentrierte Dialoge auf **Desktop**, nur für eine kurze Frage oder eine Auswahl (§7.6): `simple`, `custom`, `fullScreen`, `confirm`/`confirmDanger` (beide mit "Abbrechen"), `alert`, `loading`. Ein offenes Modal bekommt die Tasten zuerst, Esc schließt nur es (§7.8) | Anzeigen, Bearbeiten oder Neuanlegen eines Dings aus einer Liste → Panel (§7.6); mobile Auswahl-Flows → `BottomSheet` (keyboard-fest); persistente Inhalte |
| **Toast** | Flüchtiges Feedback nach Aktion: `success`/`error`/`warning'`/`info'`, `renderList` | Blockierende Fehler → `ErrorDisplay`/`Modal`; persistenter Status → `Badge` |
| **Stats** | KPI-/Kennzahl-Kacheln: `withIcon`, `withTrend`, `transactionCount`, `syncCount`, `grid*`, `withAccent` (Akzentfarbe wählen, inkl. `NoAccent` für gar keinen Streifen, z. B. Kontostände-Karten); Monatsbalken `monthBars`, `monthBarsChosen` (vorgewählter Balken, nur im Styleguide) und `monthBarsPlaceholder` (beim Laden, gleich hoch); Mini-Balken `sparkBars`, `sparkBarsLabelled` und `sparkBarsPlaceholder` (Seite Targets prüfen); Regeln in §2 Monatsbalken | Vollständige Tabellen → `Table`; einzelner Betrag im Fließtext → `Money` |
| **Money** | **Jede** Geldanzeige, setzt Vorzeichen-Farbe + Glow automatisch: `available` (Auswahl, §2), `afterAmount` (Verfügbar danach: fett, rot unter 0, grün bei genau 0), `simple`, `large`, `hero`, `noSign`, `fromMoney`, `accountBalance` (Kontostand ohne führendes Plus, für Kontostände-Leiste und -Liste), `accountListRow` (klickbare Kontozeile: Name links, farbcodierter Betrag rechts, aktiver Zustand mit Akzentlinie), `accountListRowWith` (dieselbe Zeile mit eigenem Wert, etwa `Loading.amountSkeleton` beim Laden oder `balanceUnavailable`, ein gedämpfter Strich nach einem Ladefehler) | Nicht-monetäre Zahlen → `Presets.monoNumber` |
| **Table** | Dichte tabellarische Daten (Desktop), inkl. `headerCell`/sortierbar/`sticky` | Mobile Listen mit Zeilen-Aktionen → Card-Liste + `Swipe`; einfache Key-Value-Paare → `Card` |
| **Loading** | Ladezustände: `spinner`, `centered`, `neonPulse`, Skeletons (`txListSkeleton`, `tableSkeleton`, `statsGridSkeleton`, `cardSkeleton`, `amountSkeleton` für einen Betrag) | Inhalt da → echten Inhalt zeigen; Fehler → `ErrorDisplay` |
| **ErrorDisplay** | Fehlerdarstellung: `inline'` (Feldfehler), `cardCompact`/`card`, `hero` (große Operation), `fullPage`, `forRemoteData`, `warning`, `notice` (Gelb, braucht dich, kein Fehler, ADR 0087 Punkt 3) | Kurzes Erfolgs-/Info-Feedback → `Toast`; Pflichtfeld-Hinweis → `Input.group*` |
| **Icons** | SVG-Icons mit `Size`×`Color`-Tokens (`Icons.sync MD Teal`, …) | Eigene rohe `<svg>` inline (Drift) — fehlt ein Icon, hier ergänzen |
| **Navigation** | Top-/Bottom-Nav, App-Wrapper, Page-Content-Container — einmal in der Haupt-`View.fs` | Aktionen innerhalb einer Seite → `Button` |
| **PageHeader** | Seitentitel: `simple`, `withSubtitle`, `gradient*`, `withActions`, `gradientWithActions` | Karten-Titel → `Card.header`; Abschnittstitel → `Card.headerSimple`/`Presets.sectionHeader` |
| **Primitives** | Layout-Bausteine: `Container`, `Stack`/`HStack`, `Grid`, `Spacer`, responsive Sichtbarkeit (`mobileOnly`/`desktopOnly`) | Visuell gestylte Blöcke → `Card`; Token-Klassen reichen für Spacing |
| **Tokens** | Quelle aller Klassen-Konstanten (§3) — überall referenzieren | Niemals umgehen: rohe Tailwind-/Hex-Klassen sind Drift |
| **Form** | Submit-Buttons mit Required-Field-Gating: `submitButton(WithIcon/Secondary)` deaktivieren bei fehlenden Pflichtfeldern | Reine Felder → `Input`; freie Buttons → `Button` |
| **BottomSheet** | **Mobile** Auswahl-/Picker-Flows (keyboard-aware): `view`, `inspector` (immer gerendert, fährt über `IsOpen` ein und aus und hält dabei seinen Inhalt, feste Höhe 500 px, fester Bereich zwischen Kopf und Inhalt, der ab 55 % Höhe in sich scrollt, wird mit Tastatur kleiner), `categoryPicker`, `categoryPickerLayered`, `chipButton`, `sectionTitle`, `confirmDanger` (Rückfrage vor einer zerstörenden Aktion als Block im Sheet, Gegenstück zu `Modal.confirmDanger`: Titel, Text, "Abbrechen" und der rote Knopf, `role "alertdialog"` mit `aria-labelledby`, ohne `aria-modal`, weil sie im Fluss des Sheets steht und den Fokus nicht fängt, beim Öffnen hat "Abbrechen" den Fokus, Escape bricht ab, nach dem Schließen geht der Fokus über `ReturnFocus` auf den Knopf, der sie geöffnet hat: nach "Abbrechen" wieder bedienbar, nach dem Bestätigen mit dem laufenden Aufruf (`IsBusy`). Das nur, wenn der Fokus beim Schließen noch in der Rückfrage lag oder auf `body` gefallen ist (Abbrechen, Escape, Bestätigen); schließt sie, weil im Feld darüber getippt wurde, bleibt der Fokus im Feld; höchstens eine Rückfrage zugleich, solange sie offen ist, warten die übrigen Aktionen des Tabs), `confirm` (dasselbe mit dem Knopf der Hauptaktion vor einer Aktion mit Folgen, Gegenstück zu `Modal.confirm`, etwa der Wechsel der Art eines Targets im Tab Target und im Sheet der Seite Targets prüfen). Tasten: ein offenes Sheet bekommt sie zuerst und verbraucht sie, Esc und die Zurück-Geste schließen nur das Sheet (§7.8); eine Auswahl im Sheet folgt §7.9, am Handy ohne Autofokus | Desktop-Dialog → `Modal`; einfaches Dropdown → `Input.select`; ein zweites Sheet über einem offenen Sheet (§7.15) |
| **CategoryPicker** | Jede Wahl genau einer Kategorie, als Split-Ziel mit Konten (§2 „Auswahl“): am PC `popover` und seine Varianten, am Handy `sheet`. Tasten nach §7.9: am PC steht der Fokus beim Öffnen in der Suche, ↑↓ bewegen die Markierung ohne Neuaufbau der Liste, Enter wählt, Esc schließt nur den Picker; am Handy kein Autofokus. Ein Klick übernimmt sofort (§7.1) | Mehrere Kategorien wählen → `CategoryMultiSelect`; Freitext für eine Kategorie (§7.2); über einem offenen Sheet gestapelt (`sheetLayered`, Abweichung bis Etappe 3, §7.15) |
| **Tabs** | Gleich breite Segmente mit `role=tablist`, aktiv im Akzent, am Handy 48 px Trefferfläche, Pfeiltasten (am Rand bleiben sie stehen) und roving `tabindex`: `bar`, dazu `panel` für `role=tabpanel`. Fokus: Tabs zeigen Auswahl und Tastatur-Fokus mit demselben Strich im Akzent, ohne Ring und ohne Fläche, Filter-Chips (`ChipBar`) zeigen den Fokus als Ring direkt auf dem Rand der Pille, ohne Abstand (beides ungelayert in `styles.css`) | Filter über einer Liste → `ChipBar` |
| **Segmented** | Umschalter zwischen Ansichten derselben Sache, nur für eine feste Menge von höchstens vier Optionen (§7.5): Knöpfe mit `aria-pressed`, aktiv im Akzent (ADR 0087), 48 px, erster Nutzer ist der Tab Geld, dazu der Rhythmus im Tab Target. Der Wochentag im Tab Target hat heute sieben Knöpfe und weicht damit ab (Fahrplan Etappe 3). `Segment.Disabled` sperrt einen Knopf (`disabled`, `opacity-50 cursor-not-allowed`, kein Hover), etwa den Rhythmus, solange das Speichern läuft | Fünf oder mehr Optionen und alles, was der Nutzer anlegt (Konten, Kategorien, Gruppen, Flags) → Auswahlfeld (§7.5); Bereiche mit eigenem Panel → `Tabs`; zwei Tab-Leisten übereinander stören sich bei den Pfeiltasten |
| **Swipe** | Wischbare Listenzeilen mit enthüllter Aktion (mobile) | Statische Zeilen → Card/`Table`; Desktop-Hover-Aktionen → `Button.iconButton` |
| **Viewport** | Infrastruktur für mobile Sheets (visualViewport→CSS-Vars, Scroll-Lock, Click-Swallow, Haptik) — von `BottomSheet` genutzt | Direkt nur, wenn du ein **neues** Sheet/Overlay baust (dann die Muster aus §7 erben) |

---

## 6. Motion

Bewegung ist subtil, schnell und respektiert Nutzerpräferenzen. Quelle:
`Tokens.fs` (Module `Animations`, `Transitions`, `StaggerDelays`) und
[`src/Client/styles.css`](../../src/Client/styles.css).

**Regeln:**

- **Standard-Transitions** über `Transitions.fast/normal/slow` (150/200/300ms, `ease-out`).
- **Spring-Einfahrt** für Sheets/Overlays über `linear()`-Easing, **hinter
  `@supports`** (Safari 17.2+ / Chrome 113+), mit Fallback auf die bestehende Kurve.
  Token: `--sf-spring-out` in `styles.css` (`@supports (transition-timing-function:
  linear(0, 1))`). Sheet-Einfahrt nutzt diesen Federwert statt mechanischem `ease`.
- **`prefers-reduced-motion: reduce` wird global respektiert** (mehrere
  `@media`-Blöcke in `styles.css`) — Animationen werden dort entschärft/abgeschaltet.
  Neue Animationen müssen das mit abdecken.
- **Skeletons statt Spinner**, wo die Zielform bekannt ist: `Loading.txListSkeleton`
  zeigt transaktionszeilenförmige Platzhalter → **kein Layout-Sprung**, wenn echte
  Zeilen eintreffen. Generischer `spinner`/`neonPulse` nur, wo die Form unbekannt ist.
  (Beleg: `infrastructure/done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs`.)
- **Stagger** für sequentielle Einblendungen über `StaggerDelays.forIndex i`.
- **Toasts — sanfter Ein- *und* Abgang (Zwei-Phasen-Removal).** Eingang:
  `.animate-toast-in` (Slide-up + leichtes Scale), bevorzugt den geteilten Federwert
  `--sf-spring-out` hinter `@supports`, Fallback `--transition-spring`. Abgang:
  `.animate-toast-out` (Fade + Slide-down, `ease-in`, 220 ms) — Toasts verschwinden
  **nie abrupt**. Die Lebenszyklus-Mechanik liegt in der App-State (`State.fs`):
  `StartDismissToast` markiert den Toast als *exiting* (Exit-Animation startet),
  `ToastExited` entfernt ihn nach `Types.Toast.exitDurationMs` (= CSS-Dauer). Auto-Dismiss
  **und** manuelles Schließen laufen über denselben Pfad; ein Doppel-Dismiss ist durch
  `Toast.isExiting` abgesichert (kein Timer-Leak / keine Doppel-Entfernung). Die
  Animationsklassen sitzen **nur auf dem inneren Toast-Element**, nie auf dem fixen
  Container (`css-animation-safety`). (design-system-004.)
- **Toast-Platzierung — zwei bewusste Fälle (mobil ≠ Desktop), nicht halb-geerbt.**
  Mobile: kompakter, **symmetrisch eingerückter Streifen** unter dem Header
  (`top-16 inset-x-4`, nur `top` — kein `bottom`); so wird die linke Neon-Border/Glow
  nicht am Rand abgeschnitten und der fixe Container spannt nicht über die volle Höhe.
  Desktop (`md:`): Insets zurücksetzen, `md:max-w-sm`, dann der Anker unten rechts
  (`md:bottom-4 md:right-4`). `positionToClasses` liefert **nur `md:`-Anker** — mobil
  wird daraus nichts geerbt. Die innere Toast-Fläche ist **voll deckend**
  (`bg-surface-card`, nicht `/95`), damit der helle Sync-Hero-Gradient nicht
  durchscheint; `backdrop-blur` bleibt für den Glas-Charakter. Container ist
  `fixed … pointer-events-none`; nur die Toasts selbst sind klickbar.
  (ADR 0007; mobiler Sitz korrigiert in design-system-005.)
- `neonPulse`/`text-glow-*` sparsam — Glow markiert, es dekoriert nicht (§1).

---

## 7. Muster (projektweite Interaktions-Konventionen)

Muster stehen **oberhalb** einzelner Komponenten: verbindliche Konventionen, die jede
neue Overlay-/Auswahl-UI erbt.

Die Abschnitte 7.4 bis 7.15 setzen ADR 0087 (Punkte 9 bis 24) und die Layout-Regeln der Recherche
`seitenlayout-liste-detail-2026-11-07` (Abschnitt 6) um. Jeder hat eine Regel, ein „Wann“, ein „Wann
nicht“ und eine Quelle. Sie gelten für jede neue und jede umgebaute Ansicht. Wo der Code heute
abweicht, steht das als Zeile im Fahrplan Etappe 3 (`.agentheim/knowledge/fahrplan.md`); diese
Abweichungen sind bekannt und kein Grund, die Regel aufzuweichen. Vorlage für Form und Verhalten ist
der Prototyp (Version 14), den Roman am Handy und am PC freigegeben hat.

„Akzent“ meint in §5 und §7 den Akzent aus ADR 0087 Punkt 2, nicht die Zeilen Purple und Pink in §2. Bis
design-system-dak16 (Palette 2 im Token-Layer) heißt das Orange für die Hauptaktion und Teal für Auswahl
und Navigation. Gelb steht nach ADR 0087 Punkt 3 für „braucht dich, kein Fehler“; wo §2 Pink dafür nennt,
hat ADR 0087 Vorrang.

### 7.1 Visual-Viewport-Sheet + Click-Commit (ADR 0005)

Das zentrale Mobile-Muster für **alle** Bottom Sheets / Overlays. Quelle:
[`Viewport.fs`](../../src/Client/DesignSystem/Viewport.fs),
[`BottomSheet.fs`](../../src/Client/DesignSystem/BottomSheet.fs),
`styles.css`, `index.html`. Verbindlich:

1. **Visual-Viewport-Anker.** `Viewport.fs` spiegelt `visualViewport.height/offsetTop`
   in die CSS-Variablen `--vvh` / `--vv-top`. Sheets ankern mit
   `top: calc(var(--vv-top) + var(--vvh))` + `translateY(-100%)` (Fallback `100dvh`).
   `interactive-widget=resizes-content` steht im Viewport-Meta (`index.html`) für
   Android. → Sheet bleibt bei offener Tastatur vollständig sichtbar.
2. **Click-Commit.** Auswahl committet auf echtem `click`, **nie** auf
   `pointerdown`/`touchend`. `onMouseDown → preventDefault()` hält Fokus/Tastatur.
   Beim Schließen einmaliger Click-Swallow-Guard (`Viewport.swallowNextClick`), damit
   der nachgelagerte synthetische Click nicht auf das Element hinter dem geschlossenen
   Sheet durchschlägt (der "Ghost-Click").
3. **Flankierend verbindlich:** gezählter Body-Scroll-Lock (iOS-fest via
   `position: fixed`); Suchfelder ≥ 16px unterhalb `md` (ab `md` sind 14px erlaubt, der
   iOS-Zoom betrifft nur Touch-Geräte) und **oberhalb** der Liste gepinnt (bleibt beim
   Scrollen sichtbar); `touch-action: manipulation` auf
   Tappables; expliziter Close-Button; `autoFocus` **nur bei feinem Pointer**
   (`Viewport.isFinePointer()`).
4. **Stacking:** Ziel sind Sheets ohne Stapel (§7.15). Wo heute noch ein Sheet über einem
   anderen liegt (`categoryPickerLayered`, `CategoryPicker.sheetLayered`), läuft das nur über die
   Layer-Klasse (`.layer-2`, z-index 70/80) und höchstens eine Ebene tief; es wird in Etappe 3
   abgebaut.

> Baust du ein neues Sheet/Overlay, baue es über `BottomSheet`/`Viewport` — erfinde den
> Anker- und Commit-Mechanismus nicht neu. Begründung & Root-Causes: **ADR 0005**;
> Beleg: `categorization/done/2026-06-11-mobile-category-picker-keyboard-ghostclick`.

### 7.2 Picker-Auswahl statt Freitext (ADR 0004)

Im Quick-Add-Flow (und analogen Flows) wird **strukturiert ausgewählt**, nicht frei
getippt, wo es eine endliche Menge gibt:

- **Konto:** echter Picker (z. B. über `BottomSheet`/`Input.select`/`searchableSelect`),
  kein Freitextfeld. (ADR 0004 begründet das fachlich: ein konfiguriertes
  Quick-Add-Konto, kein geratener Fallback.)
- **Payee:** **optional** — leere optionale Felder werden weggelassen, nicht leer
  abgesendet. UI macht klar, dass Payee weggelassen werden darf.

> UI-relevanter Teil von ADR 0004; die Backend-/Domain-Details (kein ImportId,
> `uncleared`, eigenes `ynab_quickadd_account_id`) stehen in der ADR und werden hier
> **nicht** dupliziert.

### 7.3 Sticky-Filter über Listen

Filter-Pills bleiben **sticky** unter dem Header (Backdrop-Blur), Date-/Section-Header
darunter neu verankert. Filterwechsel braucht kein Hochscrollen. Beleg:
`infrastructure/done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs`;
Positionierung in `styles.css`. Sticky-Tabellenköpfe analog über `Table` (`Sticky=true`).

Der Kopf der Budget-Seite ist eine sticky Zeile wie die Skizze (budgeting-m3x7f): `‹ Monat ›`, der Knopf
Historie (Icon-Knopf `Icons.clock`, `aria-label` "Historie"), der Knopf Monatserster (Icon-Knopf `Icons.calendar`,
`aria-label` und `title` "Monatserster", `aria-expanded` sagt, ob die Leiste steht) und rechts Zu verteilen. Den Knopf
Monatserster gibt es im laufenden und in kommenden Monaten, im vergangenen bleibt er `invisible`, der Kopf springt beim
Blättern nicht. Ab 768 px steht der Kopf in einer Zeile, am Handy bei 390 px auch (Monat kurz über
`Button.homeLabelCompact`, Zu verteilen ohne €, beide Icon-Knöpfe 48 px). Darunter steht die Leiste Monatserster (Karte
mit vier `Button.tile` und `Button.quietText` "Ausblenden"), darunter mit 10 px Abstand die Suchzeile: am PC die Suche
(256 px) und die Chips in einer Zeile, am Handy nur die Suche, die Chips liegen in der Liste. Ohne Leiste steht die
Suchzeile direkt unter dem Kopf. Von selbst steht die Leiste im laufenden Monat, solange Targets prüfen, Targets
anwenden oder Überzogene decken offen ist und sie für den Monat nicht ausgeblendet ist; über den Kopfknopf steht sie im
laufenden und in kommenden Monaten jederzeit. Der Weg zu Targets prüfen ist Feld 1 der Leiste, der zu Targets anwenden
Feld 2, im Kopf gibt es für beide keinen Knopf. Der Spaltenkopf haftet unter dem Kopf. Der Titel entfällt, die
Navigation nennt die Seite, die `h1` bleibt für den Screenreader (`PageHeader.screenReaderTitle`). Die Seite läuft über
die volle Breite mit dem üblichen Seitenrand, der Zuwachs geht an die Spalte Kategorie, das Panel bleibt 340 px. Am PC
scrollen Leiste, Suche und Chips mit. Am Handy haften der Kopf und die Chips, die Suche scrollt mit.

Die Seite Targets prüfen `#/budget/target-check` hat am PC als Kopf eine Karte mit Zurück, Titel, Erklärung und den Zählern "Hinweise" und
"Passt", am Handy Zurück, den Titel groß und eine Zeile mit Monat und Zählern. Die Gruppen sind Karten, jede klappt
auf und zu (`Button.disclosureListRow` in einer `h2`), beim Betreten offen, "Passt" zu. Eine Zeile steht ab 720 px
Breite der Liste (Container-Abfrage) in vier Spalten wie die Skizze (Name und Target, Mini-Balken 132 px, Ø und
Monate, Knöpfe; Verhältnis 1,3 zu 1), schmaler wie am Handy untereinander. "<Betrag> übernehmen" und "Anpassen …"
sind neutrale Knöpfe (`Button.Secondary`, ADR 0075 Punkt 3). "Anpassen …" öffnet das Target-Formular ab 1024 px als
Panel rechts, 360 px breit wie die Skizze (`.target-check-panel`), darunter als `BottomSheet.inspector`. In der Budget-Historie `#/budget/history` stehen die Filter nach Art
heute als `ChipBar.barWithTestId` in einer eigenen, haftenden Zeile unter Zurück und Titel (Ziel: §7.11). Die Höhe des Kopfs ist
gemessen (`ResizeObserver`, `--budget-month-bar-height`), weil er umbricht. Ein Halter mit
`overflow-hidden` nimmt dem Spaltenkopf das Haften: dort `overflow-clip`.


### 7.4 Drei Orte und Navigation

**Regel:** BB hat drei Orte: Übersicht, Kassenbuch, Budget. Die Leiste unten am Handy und die
Seitenleiste links am PC zeigen nur diese Orte, keine Aktionen; ein Ort darf einen Zähler tragen
(offene Buchungen an der Übersicht). Die Einstellungen liegen hinter einem Symbol (Zahnrad). Regeln
sind ein Tab im Kassenbuch neben den Buchungen. Quick Add ist kein Ort; sein Versprechen (wenige
Klicks, kluge Vorbelegung) gilt für den Knopf „+ Buchung“ im Kassenbuch und in der Übersicht.
Weitere Seiten hängen unter einem Ort und haben im Kopf „Zurück“ (etwa Targets prüfen unter Budget).

Was an den Orten steht:

- **Übersicht (Variante A):** zuerst „Zu tun“ als geordnete Liste mit genau einem Hauptknopf, dann
  der Monat mit fester Höhe, dann Angepinnt (wächst mit), am Handy danach die Konten.
- **Durchgehen:** Neue Buchungen geht man von der Übersicht aus durch, Karte für Karte.
- **Import direkt:** Abgerufene Umsätze gehen sofort ins Kassenbuch und nach YNAB, markiert als „zu
  prüfen“. Nur unsichere Duplikate warten; sie erscheinen beim Durchgehen als eigene Karte. Abrufen
  geht jederzeit von Hand.
- **Angepinnt:** Angepinnte Kategorien stehen auf der Übersicht. Anpinnen und Lösen geht dort und im
  Kategorie-Detail. Das Anpinnen wird als Zustand gespeichert (ADR 0083).

**Wann:** jede neue Seite, jeder Eintrag in der Navigation, jeder Einstieg in einen Ablauf.

**Wann nicht:** Aktionen (Erfassen, Abrufen, Sync) stehen als Knopf in der Ansicht und haben in der
Navigation keinen Platz. Ein vierter Ort braucht eine neue ADR.

**Quelle:** ADR 0087 Punkte 9, 10, 11, 12 und 14; Recherche Abschnitt 6, Handy 1 und 10.

### 7.5 Auswahl

**Regel:**

- Ein Umschalter (`Segmented`) steht nur für eine feste Menge von höchstens vier Optionen, die das
  Programm vorgibt (Monat, Woche, Jahr; Zuweisen, Holen, Abgeben).
- Alles, was der Nutzer anlegt (Konten, Kategorien, Gruppen, Flags), wählt man in einem Auswahlfeld
  mit Liste (`Input.searchableSelect`, `CategoryPicker`, am Handy im `BottomSheet`). Das Feld zeigt
  den gewählten Wert, eine Kontowahl zusätzlich den Saldo.
- Höchstens eine seitlich wischende Leiste je Ansicht, nur für Zweitrangiges (etwa die Vorschläge
  „Zuletzt“ beim Erfassen). Ihr Anfang ist nie abgeschnitten, das Ende darf angeschnitten sein, das
  letzte Element ist mit drei bis vier Wischern erreichbar.
- Was den Zustand einer Liste bestimmt (Konto, gesetzte Filter), ist ohne Öffnen eines Menüs
  sichtbar. Gesetzte Filter stehen als Chips mit „×“ über der Liste; eine Zahl am Filter-Knopf allein
  reicht nicht.
- **Kassenbuch am Handy:** oben die Kontowahl als Auswahlfeld mit Saldo, darunter die Suche und ein
  Filter-Knopf, der ein Sheet öffnet; gesetzte Filter als Chips. Das Kassenbuch hat am Handy keine
  seitlich wischende Leiste.

**Wann:** jede Wahl aus einer Menge, jede Filterzeile über einer Liste.

**Wann nicht:** Fünf oder mehr feste Optionen (etwa sieben Wochentage) sind ein Auswahlfeld.
Bereiche mit eigenem Inhalt sind `Tabs` (§5). Freitext, wo es eine endliche Menge gibt, ist Drift
(§7.2).

**Quelle:** ADR 0087 Punkte 13 und 15; Recherche Abschnitt 6, Handy 4 und 5.

### 7.6 Liste mit Panel (PC)

**Regel:** Ab 1024 px Breite (`lg`) stehen Liste und Panel nebeneinander, darunter nur eins von beiden. Das
Panel ist eine feste Spalte rechts mit rund 30 % der Breite (im Prototyp 30 % der Fensterbreite,
mindestens 380 px, höchstens 520 px). Was ein Ding der Liste zeigt, bearbeitet oder neu anlegt, steht
im Panel: Buchung, Regel, Kategorie, Target, die neue Buchung. Nach dem Anlegen zeigt das Panel das
neue Ding. Ein Dialog (`Modal`) ist nur eine kurze Frage (Rückfrage vor dem Löschen) oder eine Auswahl
(Kategorie, Konto, Targets anwenden).

**Wann:** jede Liste am PC, deren Zeilen ein Detail haben (Kassenbuch mit Buchungen und Regeln,
Budget, Targets prüfen, Kategorien).

**Wann nicht:** Unter 1024 px öffnet das Detail als `BottomSheet` (§7.1). Eine Rückfrage oder eine
Auswahl wird kein Panel.

**Quelle:** ADR 0087 Punkt 16; Recherche Abschnitt 6, PC 1 und 2. Die Schwelle 1024 px folgt dem Prototyp (Roman, 2026-11-07); die Recherche nannte 840 px.

### 7.7 Mehrfachauswahl

**Regel:** Am PC wählt Umschalt-Klick einen Bereich ab der zuletzt gewählten Zeile, Strg- oder
Cmd-Klick nimmt eine Zeile dazu oder heraus, Umschalt+↑↓ erweitert die Auswahl um die nächste Zeile,
die Leertaste schaltet die aktuelle Zeile. Am Handy startet langes Drücken auf eine Zeile die
Auswahl, danach schaltet Tippen; die Leiste unten zeigt anstelle der Navigation Anzahl, Summe und die
Sammelaktionen.

Ab zwei Einträgen wird das Panel zur Zusammenfassung: Kopf „n Buchungen ausgewählt“ mit einem Knopf
zum Aufheben, die Summe groß, darunter Ausgaben und Einnahmen, die Aufteilung nach Kategorie (Splits
nach ihren Zeilen) und die Sammelaktionen. Eine Sammelaktion ist die Hauptaktion (etwa „Alle
geprüft“), die übrigen (Kategorie setzen, Lena fragen) sind neutral (§7.14).

**Wann:** Listen, auf deren Zeilen eine Aktion für viele Einträge zugleich sinnvoll ist (Kassenbuch).

**Wann nicht:** Listen ohne Sammelaktion bekommen keine Mehrfachauswahl. Bei einer einzigen gewählten
Zeile zeigt das Panel ihr Detail.

**Quelle:** ADR 0087 Punkt 17.

### 7.8 Tastatur

**Regel** (am PC, für jede Liste mit Panel; Vorbild ist `pageKeyAction` in
[`Inspector.fs`](../../src/Client/Components/Budget/Inspector.fs)):

1. **Pfeile gehören der Liste**, auch wenn der Fokus im Suchfeld der Liste steht: ↑↓ wechseln die
   Zeile, ←→ den Tab im Panel. Ohne Auswahl wählt ↓ die erste und ↑ die letzte sichtbare Zeile;
   ←→ bleiben dann beim Cursor im Suchfeld.
2. In allen anderen Feldern (Betrag, Zugewiesen, Text) gehören die Pfeile dem Cursor.
3. Mit Alt, Strg oder Cmd gehört der Pfeil dem Browser (Alt+← zurück). Umschalt+↑↓ erweitert in
   Listen mit Mehrfachauswahl die Auswahl (§7.7), sonst markiert es im Feld.
4. Ein Druck ist ein Schritt. Hat `Tabs` die Taste schon genommen (`defaultPrevented`), reagiert die
   Seite nicht noch einmal.
5. Ein druckbares Zeichen, während kein Feld den Fokus hat, geht in die Suche der Liste
   (`typesIntoSearch`); die Leertaste bleibt beim Knopf mit Fokus und beim Scrollen.
6. **Oberster Dialog zuerst:** Ist ein Dialog, ein Picker oder ein Sheet offen, bekommt er die Tasten
   zuerst und verbraucht sie; die Liste darunter reagiert nicht (`pageKeyActionFor`). Esc schließt nur
   den obersten Dialog und führt dorthin zurück, wo man herkam: der Fokus geht auf den Auslöser, aus
   dem Picker zurück ins Detail. Ohne Dialog hebt Esc zuerst eine Mehrfachauswahl auf und schließt
   danach das Panel.

**Wann:** Kassenbuch (Buchungen und Regeln), Budget, Targets prüfen, Kategorien, jede neue Liste
mit Panel.

**Wann nicht:** Am Handy gibt es keine Seitentasten; dort schließt Esc nur ein offenes Sheet.

**Quelle:** ADR 0087 Punkte 18 und 19; Code `pageKeyAction`, `pageKeyActionFor` und
`typesIntoSearch` in `src/Client/Components/Budget/Inspector.fs`.

### 7.9 Auswahl-Dialog

**Regel:** Ein Auswahl-Dialog (Kategorie, Konto, Filter, Gruppe, Flag) hat oben ein Suchfeld. Am PC
steht der Fokus beim Öffnen sofort in der Suche. ↑↓ bewegen die Markierung durch die Optionen, ohne
die Liste neu aufzubauen; die markierte Option rückt ins Bild. Enter wählt die markierte Option, Esc
schließt nur den Dialog (§7.8). Ein Klick übernimmt sofort (Click-Commit, §7.1). Am Handy gibt es
keinen Autofokus, die Tastatur kommt erst beim Tippen ins Suchfeld (`Viewport.isFinePointer()`).

**Wann:** `CategoryPicker`, `BottomSheet.categoryPicker`, `Input.searchableSelect` und jede neue
Auswahl aus einer Liste.

**Wann nicht:** Ein Formular mit mehreren Feldern ist kein Auswahl-Dialog und steht im Panel (§7.6).

**Quelle:** ADR 0087 Punkt 20; ADR 0005.

### 7.10 Bewegung

**Regel:** Eine Animation hängt am Öffnen: Sheet und Dialog fahren einmal ein, wenn sie aufgehen.
Zeichnet die Seite neu (neuer Wert, Tippen in der Suche, Markierung bewegt), läuft keine Animation
erneut. Die Scrollposition eines offenen Dialogs bleibt beim Neuzeichnen erhalten, ebenso die der
Liste neben dem Panel.

**Wann:** jede Einblendung von Sheet, Dialog, Panel-Inhalt und Rückmeldung.

**Wann nicht:** Eine Animationsklasse, die bei jedem Rendern neu gesetzt wird, ist Drift. Kurven,
Dauer und `prefers-reduced-motion` regelt §6.

**Quelle:** ADR 0087 Punkt 21.

### 7.11 Historie

**Regel:** Buchung und Kategorie zeigen ihre Historie in derselben Form, je Eintrag eine Zeile mit
drei Spalten: links der Zeitpunkt (`TT.MM. HH:MM`, Mono, gedämpft), in der Mitte die Herkunft als
neutrales Kürzel (etwa ROMAN, BOB, YNAB; Mono, Großbuchstaben, dünner Rand, ohne Farbe) und der Text,
rechts der Betrag, falls es einen gibt. Der neueste Eintrag steht oben. Ein Filter nach Art ist ein
Auswahlfeld („Zeigen: Alle“).

**Wann:** jede Ansicht, die zeigt, was mit einem Ding passiert ist (Buchung, Kategorie,
Budget-Historie).

**Wann nicht:** Farbe für die Herkunft; eine eigene Form je Seite.

**Quelle:** Prototyp Version 14 (Grundlage von ADR 0087, Abschnitt Context), Historie in Buchung und
Kategorie.

### 7.12 Leerer Zustand

**Regel:** Ein leerer Zustand hat einen kurzen Kopf und einen Satz, was hier erscheinen wird, und
bietet höchstens eine Aktion an. Beispiele aus dem Prototyp: „Keine Buchungen“ mit „Buchungen
erscheinen hier nach dem nächsten Import.“ und „Abrufen“; nach Suche oder Filter „Kein Treffer“ mit
„Keine Buchung passt zu Suche und Filtern.“ und „Filter löschen“; das leere Panel „Buchung wählen, um
Details zu sehen.“ ohne Aktion.

**Wann:** jede Liste, jedes Panel und jede Suche ohne Ergebnis (`Card.emptyState`).

**Wann nicht:** Ein Ladefehler ist kein leerer Zustand (`ErrorDisplay`); beim Laden steht ein
Skeleton (§6).

**Quelle:** Prototyp Version 14 (Grundlage von ADR 0087, Abschnitt Context); §8 Empty States.

### 7.13 Rückmeldung

**Regel:** Eine Rückmeldung (`Toast`) verdeckt nie ein Bedienelement. Am Handy steht sie oben, auch
bei offenem Sheet. Am PC steht sie unten rechts; liegt dort eine Leiste (Auswahl-Leiste, Fußzeile),
steht sie darüber. Lässt sich die Änderung zurücknehmen, bietet die Rückmeldung „Rückgängig“. Der
Text sagt knapp, was passiert ist („3 Buchungen geprüft“).

**Wann:** nach jeder Aktion, deren Ergebnis man sonst nicht sofort sieht oder die man zurücknehmen
können soll.

**Wann nicht:** Blockierende Fehler gehören in `ErrorDisplay` oder `Modal` (§5). Platzierung im Detail
und Animation regelt §6.

**Quelle:** ADR 0087 Punkt 22.

### 7.14 Seitenaufbau

**Regel:**

- **Eine Hauptaktion je Ansicht**, als gefüllter Knopf im Akzent; alles andere ist neutral.
- Höchstens drei Schriftgrößen je Ansicht und höchstens zwei große Elemente (etwa Seitentitel und
  Saldo).
- **Feste Höhe vor variabler Höhe:** Blöcke mit fester Höhe stehen über Blöcken, die wachsen, damit
  ein fester Block immer an derselben Stelle steht.
- Gleiches steht nur einmal da: eine Kontowahl je Ansicht (Auswahlfeld oder Seitenleiste, nie beides).
- Das Wichtigste steht oben und links. Zahlen, die eine Entscheidung tragen, sind größer als Zahlen,
  die nur informieren.

**Wann:** jede Ansicht, auch Panel und Sheet für sich genommen.

**Wann nicht:** eine zweite Hauptaktion „für den Fall“; ein großer Wert ohne Entscheidung dahinter.

**Quelle:** ADR 0087 Punkte 23 und 24; Recherche Abschnitt 6, Handy 3, 6 und 8, PC 9.

### 7.15 Layout-Regeln Handy und PC

**Regel am Handy** (unter 768 px, `md`):

- Trefferfläche jeder Zeile und jedes Knopfs mindestens 48 × 48 px, 8 px Abstand zwischen Zielen
  (`TouchTargets.minSize`, §4).
- Seitenrand 16 px.
- Sheets haben einen sichtbaren Schließen-Knopf, die Zurück-Geste schließt das Sheet. Sheets werden
  nicht gestapelt: eine Auswahl aus einem Sheet heraus ersetzt dessen Inhalt und führt nach der Wahl
  dorthin zurück.

**Regel am PC** (ab 768 px, `md`; das Panel kommt ab 1024 px, §7.6):

- Freie Breite geht an Spalten und Panel, die Ränder bleiben gleich. Ab etwa 1600 px bekommt die
  Liste eine Höchstbreite oder es kommt eine Spalte dazu.
- Was am Handy hinter einem Menü liegt, steht am PC offen da: die Konten in der Seitenleiste, die
  Filter als sichtbare Zeile über der Liste. Die Funktion bleibt gleich.
- Ab 768 px steht die Navigation links als Seitenleiste oder Rail.
- Die Mindesthöhe aus §4 gilt am PC nicht, weil der Zeiger kleinere Ziele trifft; Untergrenze ist die
  Lesbarkeit.
- Fließtext im Panel hat höchstens 75 Zeichen je Zeile.
- Gruppen trennt man über Leerraum und Nähe; Rahmen und Karten nur, wo Leerraum nicht reicht.

**Wann:** jede Seite, beim Bauen und beim Vergleich mit Skizze und Prototyp.

**Wann nicht:** Die dichteren Zeilen des PCs gelten unter 768 px nicht, dort bleiben es 48 px.

**Quelle:** Recherche `seitenlayout-liste-detail-2026-11-07` Abschnitt 6, Handy 2, 7 und 9, PC 3 bis
8; ADR 0005 für die Sheets.

---

## 8. Voice / Tonalität (Microcopy)

Die UI spricht **Deutsch, direkt, knapp, ohne Füllwörter** — wie Roman selbst
("Neue Kategorienauswahl ist gut."). Kurzregeln:

- **Deutsch.** UI-Texte auf Deutsch (Code/Token-Namen bleiben Englisch).
- **Knapp.** Buttons als Verb oder Verb+Objekt: "Speichern", "Sync starten",
  "Kategorie wählen". Keine Höflichkeitsfloskeln ("Bitte klicken Sie hier, um …").
- **Direkt & ehrlich.** Fehlertexte benennen, was passiert ist und was zu tun ist —
  kein Beschönigen, kein generisches "Etwas ist schiefgelaufen", wenn die Ursache
  bekannt ist.
- **Beträge sprechen für sich.** Vorzeichen + Farbe (Green/Red) tragen die Bedeutung;
  kein "Sie haben X ausgegeben" drumherum nötig.
- **Empty States** sagen, was als Nächstes zu tun ist (`Card.emptyState` mit
  Aktion-Button), statt nur "Keine Daten".
- **Konsistente Begriffe.** Ubiquitous Language der BCs verwenden (Import, Kategorie,
  Sync, Split, Transfer) — nicht synonym variieren.

---

## Quellen (autoritativ — bei Konflikt gewinnt der Code)

- **Design-Tafeln und Handoffs** (`docs/ux/kassenbuch/tafeln/`, `docs/ux/kassenbuch/handoff-ablauf-*.md`)
  legen nur den Ablauf fest (Elemente, Zustände, Reihenfolge, Texte). Ihre Farben und Maße
  sind Stand September 2026 und werden nicht übernommen; die Optik kommt aus diesem
  Styleguide, den DS-Komponenten und `/styleguide`. Mechanisch abgesichert durch den
  Token-Wächter in `npm run test:scripts`.

- [`src/Client/DesignSystem/Tokens.fs`](../../src/Client/DesignSystem/Tokens.fs) — Farb-/Glow-/Spacing-/Preset-Tokens.
- [`src/Client/DesignSystem/`](../../src/Client/DesignSystem/): die 23 Komponenten aus §5.
- [`src/Client/styles.css`](../../src/Client/styles.css) / [`index.html`](../../src/Client/index.html) — Spring-Easing, `prefers-reduced-motion`, visualViewport-Anker, Viewport-Meta.
- Route `/styleguide`: die echten DS-Komponenten als Live-Beispiele, die API steht in deren Doc-Kommentaren.
- `.claude/rules/design-tokens.md` — die harte Token-only-Regel + Grep-Checks.
- ADR 0005 (visual-viewport-Sheets + Click-Commit), ADR 0004 (Quick-Add-Picker).
- Prior art: `categorization/done/2026-06-11-mobile-category-picker-keyboard-ghostclick`,
  `infrastructure/done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs`.

> Drift-Audit und Code-Konsolidierung gegen diesen Styleguide: `design-system-002`.
