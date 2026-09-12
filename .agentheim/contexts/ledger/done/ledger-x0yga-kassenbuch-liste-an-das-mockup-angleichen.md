---
id: ledger-x0yga
title: "Kassenbuch-Liste an das Mockup angleichen: Zahlenformat, Betragsfarbe, Datum, Memo, Spaltenbreiten, Konten-Spalte"
status: done
type: bug
context: ledger
created: 2026-09-12
completed: 2026-09-12
depends_on: [design-system-001]
blocks: []
tags: [frontend, kassenbuch, styleguide, visuell]
related_adrs: [0029]
related_research: []
prior_art: [ledger-b1xwe]
---

## Why
`ledger-b1xwe` hat die seitenweise Liste, die Gesamtsicht und die Kontostände gebaut, alle
neun Akzeptanzkriterien erfüllt und die Verifikation bestanden. Das Ergebnis sieht trotzdem
nicht wie das Zielbild aus. Roman beim ersten Blick auf die laufende App: "es sieht halt
so aus."

Ursache ist der Ablauf, nicht der Worker: das Mockup lag nur in Claude Design, der Worker
hat den Fließtext des UX-Konzepts gelesen, und kein Schritt hat das gebaute Bild angesehen.
Festgehalten im design-system-README, Abschnitt "Mockups gehören ins Repo, und
Frontend-Arbeit hat ein visuelles Gate".

## What
Das Zielbild liegt jetzt im Repo:
**`docs/ux/kassenbuch/mockups/1.9-pc-ausgangszustand-zeile-markiert.png`**
(Claude Design, PC 1440 × 900, Konto-Sicht "ING Extra" mit Chip "Zu prüfen").

Anzugleichen sind die Punkte, die im Umfang von `ledger-b1xwe` lagen:

| | heute | Zielbild |
|---|---|---|
| Zahlenformat | `38,690.62 EUR` | `21.388,85 €` |
| Beträge | weiß | rot bei negativ, grün bei positiv |
| Datum | `04.09.2026` | `Heute`, `Gestern`, sonst `08.09.` |
| Memo | kompletter Bank-Rohtext, Zeile fünf Zeilen hoch | eine Zeile, gekürzt |
| Spalten | verschmolzen (`04.09.2026GIRO`, Kopf `KONTOPAYEE`), Betrag läuft rechts raus | feste, sinnvolle Breiten |
| Konten links | hohe Karten mit Farbstreifen | kompakte Liste, Name links, Betrag rechts |
| Tabellenkopf | klebt hinter dem Seitenkopf, `DATUM` angeschnitten | sitzt sauber über der ersten Zeile |

**Zum Zahlenformat:** `formatMoneyText` in `src/Client/Components/Ledger/View.fs` setzt
zwar `CultureInfo("de-DE")`, aber die .NET-Kultur greift in der nach JavaScript
übersetzten Fassung nicht. Die Formatierung muss anders gelöst werden, und zwar an einer
Stelle für die ganze App, nicht je View.

**Zur Betragsfarbe:** die Handy-Liste nutzt bereits `Money.transactionWithCurrency` und ist
damit richtig. Die PC-Tabelle und die Kontostände-Karten geben `formatMoneyText` als
reinen Text aus und verlieren dadurch Farbsemantik und Ziffernschrift. Beträge gehören
überall über die Money-Komponente.

**Nicht in dieser Aufgabe:** Filterleiste, Summenzeile, Status-Kennzeichen,
Auswahl-Kästchen und die Tastaturhilfe am unteren Rand. Die sind im Mockup zu sehen, weil
es für das Ziel entworfen wurde, und gehören zu `ledger-vgfw3`, `ledger-r1d2f` und
`ledger-kdnnz`.

## Acceptance criteria
- [x] Alle Beträge in der Kassenbuch-Seite laufen über die Money-Komponente des
      Design-Systems, auch in der PC-Tabelle und in den Kontostände-Karten.
- [x] Beträge erscheinen im deutschen Format mit Euro-Zeichen (`-48,12 €`), negativ rot,
      positiv grün. Die Formatierung liegt an genau einer Stelle und ist getestet, auch
      für Tausendertrennung und Null.
- [x] Das Datum steht als `Heute`, `Gestern` oder `08.09.` in der Liste; das volle Datum
      bleibt im Detail sichtbar.
- [x] Die Memo-Zelle belegt höchstens eine Zeile und schneidet längeren Text ab; eine
      Buchungszeile ist damit gleich hoch wie jede andere.
- [x] Die Tabellenspalten haben feste Anteile; kein Inhalt läuft aus dem sichtbaren
      Bereich, und keine zwei Spaltenköpfe stehen ohne Trennung nebeneinander.
- [x] Die Kontostände am PC stehen als kompakte Liste (Name links, Betrag rechts), nicht
      als hohe Karten; das aktive Konto bleibt hervorgehoben.
- [x] Der Tabellenkopf ist beim Scrollen vollständig sichtbar und wird nicht vom
      Seitenkopf überdeckt.
- [x] Ein headless aufgenommener Screenshot der Seite (1440 × 900 und 390 × 844) liegt
      unter `docs/ux/kassenbuch/screenshots/` und zeigt keine der obigen Abweichungen.
- [x] `dotnet build`, `dotnet test` und `npm run build` sind grün.

## Notes
Geschnitten am 2026-09-12 nach Romans erstem Blick auf die laufende App. Das visuelle Gate
aus dem design-system-README gilt für diese Aufgabe selbst: vor dem Integrieren wird die
gebaute Seite aufgenommen und neben das Mockup gelegt.

Ein wiederverwendbares Skript für solche Aufnahmen ist `design-system-wa6wz`; solange es
fehlt, nimmt der Conductor die Seite von Hand auf (Playwright liegt im Projekt,
`scripts/export-design-system.mjs` zeigt das Muster).

Während der Umsetzung ergänzte der Conductor ein vollständiges Claude-Design-Handoff
(`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`, später einmal korrigiert:
Konten-Spalte und Tastatur-Fußzeile bekamen doch keinen eigenen Hintergrund, `#0a0a1c`
kommt darin nicht mehr vor). Für die sieben Punkte dieser Aufgabe lieferte es
pixelgenaue Werte (Spaltenraster, Zeilenschriften, Konten-Zeilen-Zustände), die Vorrang
vor dem reinen Bildvergleich hatten; unverändert blieb der Umfang selbst.

## Outcome
Alle sieben im `## What` genannten Abweichungen behoben, ausschließlich innerhalb des
gelisteten Umfangs (Filterleiste, Summenzeile, Status-Kennzeichen, Auswahl-Kästchen,
Tastaturhilfe bleiben `ledger-vgfw3`/`ledger-r1d2f`/`ledger-kdnnz`).

**Zahlenformat** (`src/Client/DesignSystem/Money.fs`, `formatAmountText`, vormals
privat `formatAmount`): komplett neu geschrieben ohne jede `CultureInfo`/`ToString`-
Formatierung — beides erwies sich als kulturabhängig und lieferte auf einer
`dotnet test`-Umgebung mit deutscher Locale sogar ein noch kaputteres Ergebnis als in
Fable (aufgedeckt durch `MoneyFormatTests`, TDD Rot-Phase). Die Lösung rechnet in ganzen
Cent (`int64`) und baut die Tausenderpunkte per Substring-Arithmetik
(`groupThousands`) selbst; `currencySymbol` mappt `EUR` auf `€`. Das ist jetzt die
**eine** Stelle für die ganze App; `Money.view` und alle Convenience-Funktionen
(`transactionWithCurrency`, `available`, die neuen `accountBalance`/`accountListRow`)
laufen darüber.

**Betragsfarbe/Ziffernschrift**: `formatMoneyText` in `Components.Ledger.View` entfernt.
PC-Tabelle nutzt jetzt `Money.transactionWithCurrency` in einer rechtsbündigen
`Table.cell` (`tdMoney`), Kontostände (Handy-Karten wie PC-Liste) das neue
`Money.accountBalance` (kein Vorzeichen, sonst wie eine Buchung).

**Datum**: neue reine Funktion `formatEntryDate (today: DateTime) (d: DateTime)` in
`Components.Ledger.View` — `Heute`/`Gestern`/`TT.MM.` —, von `formatDate` an
`DateTime.Today` gebunden und in Liste (PC-Tabelle, Handy-Zeile, Datums-Gruppenkopf)
verwendet. Das volle Datum (`formatDateFull`, `TT.MM.JJJJ`) bleibt in der Historie/dem
Detail. Beide Funktionen bauen ihre `CultureInfo` jetzt lokal (`germanCulture()`) statt
über ein gemeinsames Modul-`let` — Grund siehe unten (Badge-Fund).

**Memo/Spalten**: PC-Tabelle mit `Table.thWidth`/`Table.headerCell` auf feste
Spaltenbreiten umgestellt (`FixedLayout = true`, Raster nach dem Handoff Screen 1.9:
`96px`/flex/`200px`/flex/`132px`/`128px` für Datum/Payee/Kategorie/Memo/Betrag/Status),
Payee/Kategorie/Memo über eine neue Zellen-Hilfsfunktion `tdTruncated` einzeilig
gekürzt.

**Konten-Spalte**: neue Design-System-Funktion `Money.accountListRow` (kompakte Zeile,
Name links, Betrag rechts, aktiv = `surface-elevated` + linke Teal-Kante wie
`Table.row`s `IsSelected`) ersetzt die PC-`Stats`-Karten; Handy-Kartenstreifen
unverändert (nur die Betrags-Darstellung repariert). Spaltenbreite `268px`, rechte Kante
`border-border-subtle`, kein eigener Hintergrund (Korrektur des Handoffs).

**Tabellenkopf**: `Table.wrapper`s `overflow-x-auto` macht den Wrapper-Div per
CSS-Spezifikation ungewollt zu einem zweiten Scroll-Container (ein explizites
`overflow-y-visible` ändert daran nichts — die Spec wandelt `visible` neben einem
Nicht-`visible`-Wert auf der anderen Achse zwingend in `auto` um). Ein `Sticky`-`thead`
positioniert sich dadurch relativ zu diesem nie scrollenden Wrapper statt zur Seite und
schwebt mitten in der Liste. Fix: `TableProps.Responsive = false` für die
Kassenbuch-Tabelle, Rand/Radius/Hintergrund direkt über `ClassName` auf `<table>`
reapplied; `Table.wrapper` selbst unverändert (Styleguide-Demo nutzt ihn weiter normal).
`.table-sticky-thead thead` (neu in `styles.css`) hält den Kopf bei `top: 64px`, exakt
unter der fixen Navbar — Muster wie `.filters`.

**Design-System-Erweiterungen** (alle auch in `standards/frontend/styleguide.md`-
konformen Tokens, keine Inline-Farben):
- `Money.fs`: `formatAmountText` (public), `accountBalance`, `accountListRow`.
- `Stats.fs`: `StatProps.ValueElement: ReactElement option` — überschreibt `Value`
  mit einem gerenderten Element, damit `Stats`-Karten (Handy-Kontostände) Farbsemantik
  behalten statt sie beim Formatieren zu Text zu verlieren.
- `Table.fs`: `TableProps.FixedLayout`, `Responsive`-Doku-Kommentar (Sticky +
  `overflow-x-auto` schließen sich aus), Sticky-Klasse auf `.table-sticky-thead`
  (CSS-Klasse statt Tailwind-Arbitrary-Variante) umgestellt.

**Gefundener, nicht behobener Fund** (siehe Backlog): `Client.DesignSystem.Badge`s
Modul-Initialisierung ruft eagerly eine Feliz-Prop-Konstruktion auf, die außerhalb von
Fable (`dotnet test`) eine `InvalidCastException` wirft. Umgangen, indem
`Components.Ledger.View` kein gemeinsames Modul-`let` mehr für `CultureInfo` hält
(jede Datums-Funktion baut ihre Instanz lokal) — das hätte sonst beim ersten
Feldzugriff das ganze Modul samt der bereits vorhandenen `Badge.view`-Werte
initialisiert. Backlog-Item `design-system-bq4tz`.

**Verifiziert**: `dotnet build`, `dotnet test` (819 grün, 7 übersprungen wie zuvor,
davon 14 neue Tests in `MoneyFormatTests.fs` und `LedgerViewTests.fs`), `npm run build`,
sowie headless Playwright-Screenshots gegen die echte Entwicklungsdatenbank
(`~/my_apps/budgetbuddy-dev/budgetbuddy.db`, nur lesend, kein Spiegel-Lauf) unter
`docs/ux/kassenbuch/screenshots/pc-1440x900-ausgangszustand.png` und
`.../handy-390x844-ausgangszustand.png`.

Schlüsseldateien: `src/Client/DesignSystem/Money.fs`, `src/Client/DesignSystem/Stats.fs`,
`src/Client/DesignSystem/Table.fs`, `src/Client/styles.css`,
`src/Client/Components/Ledger/View.fs`, `src/Tests/MoneyFormatTests.fs`,
`src/Tests/LedgerViewTests.fs`.


## Verifier note (iteration 1)

**VERDICT: FAIL** — zwei Akzeptanzkriterien verletzt. Die Formatierungsarbeit ist gut und
bleibt: Geldformat kulturunabhängig an einer Stelle, acht Tests dafür, Datum relativ mit
sechs Tests, 819 Tests grün, Bau grün, kein Scope-Verstoß.

**REASONS (blockierend):**

1. **AC4, zweiter Halbsatz verletzt** („eine Buchungszeile ist damit gleich hoch wie jede
   andere"). Im abgelieferten `screenshots/pc-1440x900-ausgangszustand.png` ist die zweite
   Zeile (04.09., „treffpunkt Landwehrviertel", Essen Arbeit) rund 1,6-mal so hoch wie
   alle anderen, weil in der Status-Spalte „in YNAB gelöscht" unter „YNAB-Spiegel"
   umbricht. Ursache ist die in diesem Diff neu gesetzte feste Breite
   `Table.thWidth "Status" "128"` (`src/Client/Components/Ledger/View.fs:512`) gegen den
   `flex flex-wrap`-Badge-Container derselben Datei. Die Memo-Kürzung selbst ist in
   Ordnung, die zugesicherte gleiche Zeilenhöhe nicht.

2. **AC8 verletzt für den Punkt „Spalten verschmolzen" aus `## What`.** Die dort genannten
   Beispiele (`04.09.2026GIRO`, Kopf `KONTOPAYEE`) sind weg, dieselbe Verschmelzung tritt
   im gebauten Bild aber zwischen Payee↔Kategorie und Memo↔Betrag auf, mit **0 px
   Abstand**: „REWE Jens Wechsler…Essen/Einkaufen", „REWE Jens Wechsler…-34,23 €",
   „PayPal (Europe) S.a r…Brettspiele". Im Mockup hat jede Spalte sichtbare Luft.

   **Wurzel (Vorbedingung, nicht vom Worker eingeführt):** `Table.view` stützt sich auf die
   Klassen `.table` / `.table-sm` (`src/Client/DesignSystem/Table.fs:98`), die aus DaisyUI
   stammen. **DaisyUI ist in diesem Projekt gar nicht geladen** (nicht in `package.json`,
   nicht in `node_modules`, `src/Client/styles.css` importiert nur `tailwindcss`). Die
   Zellen haben dadurch keinerlei waagerechtes Padding; mit `table-fixed` plus `truncate`
   stösst der gekürzte Text jetzt hart an die Nachbarspalte. Eigener Task dafür:
   `design-system-fq8dn`.

**Nicht blockierend, aber festzuhalten:**

- **AC7 ist durch kein Artefakt belegt.** Der einzige PC-Screenshot steht auf
  Scroll-Position 0, dort sitzt der Tabellenkopf ohnehin an seiner natürlichen Stelle.
  Nachvollziehbar ist nur die Mechanik (`.table-sticky-thead thead { top: 64px }` plus
  `Responsive = false`, passend zur fixen Navbar `h-16`). Ein zweiter Screenshot im
  gescrollten Zustand deckt das Kriterium wirklich ab.
- **Design-System-Eingriffe sind sauber additiv bis auf zwei bewusste
  Verhaltensänderungen:** `Money.formatAmountText` gibt jetzt app-weit „€" statt „EUR" und
  deutsche Gruppierung aus (jede Seite mit `Money.view` sieht anders aus, genau das Ziel
  von AC2, aber der Wirkungskreis geht über das Kassenbuch hinaus); `Table.Sticky = true`
  klebt jetzt bei `top: 64px` statt `top: 0` (derzeit nur ein Aufrufer).
  `Stats.ValueElement` und `Table.FixedLayout` sind rein additiv.
- **Styleguide nicht nachgezogen:** weder `standards/frontend/styleguide.md` noch die
  Route `src/Client/Components/Styleguide/View.fs` kennen `Money.accountBalance` und
  `Money.accountListRow`. Letztere ist eine vollständige neue Komponente (klickbare
  Kontozeile) und fehlt damit auch im Claude-Design-Export. Eigener Task:
  `design-system-t3vqr`.
- **Kategorie nicht violett** (`#a855f7`, Handoff 1.9) und **Handy-Liste zeigt Karten**
  statt flacher Zeilen mit Trennlinie. Beides steht in keinem Kriterium und in keinem der
  sieben Punkte aus `## What`: Folge-Task-Material (`ledger-w9skm`), kein FAIL-Grund.
- **Backlog `design-system-bq4tz` ist zutreffend beschrieben**, der Umgehungscharakter
  steht offen im Item. Der Worker hat das Problem umgangen (`germanCulture ()` statt
  Modul-`let`), nicht behoben; reine Funktionen in View-Modulen mit DS-Import bleiben
  damit eine Falle.

**SUGGESTED_FIX:** Waagerechtes Zellen-Padding im Tabellenraster herstellen (auf
`Table.cell` / `headerCell`, statt sich auf die nicht geladene DaisyUI-Klasse zu
verlassen) und die Status-Spalte so breit bzw. die Badges so setzen, dass zwei Kennzeichen
nicht umbrechen. Danach den PC-Screenshot neu aufnehmen, zusätzlich einen im gescrollten
Zustand für AC7.

**ITERATION_HINT:** likely-fixable

## Iteration 2 (2026-09-12)

Beide blockierenden Befunde behoben, Formatierungsarbeit aus Iteration 1 unangetastet.

**Befund 1 (0px Spaltenabstand):** `Table.cell`/`Table.headerCell`
(`src/Client/DesignSystem/Table.fs`) bekommen jetzt `px-3 py-2` direkt auf `<td>`/`<th>`,
statt sich auf die nicht geladene DaisyUI-Klasse `.table`/`.table-sm` zu verlassen. Ein
Kommentar bei `sizeToClass`/`variantToClass` hält fest, dass diese DaisyUI-Klassen
weiterhin inert sind (`design-system-fq8dn` bleibt die Grundsatzfrage). Geprüft gegen den
zweiten Aufrufer von `Table.view`/`Table.cell` im Repo (`Styleguide/View.fs`, Demo-Tabelle
Payee/Kategorie/Betrag): sieht danach besser aus, nicht schlechter — Screenshot dieser
Demo-Tabelle als Beleg lokal geprüft (nicht ins Repo übernommen, kein Artefakt-Bedarf
dafür).

**Befund 2 (Zeile mit zwei Kennzeichen bricht um):** Container der Status-Badges in
`entryTableRow` (`src/Client/Components/Ledger/View.fs`) von `flex flex-wrap` auf
`flex flex-nowrap` umgestellt, und die Status-Spaltenbreite von `128` auf eine neue named
Konstante `statusColumnWidth = "208"` angehoben (Kommentar erklärt, warum: der
Zwei-Kennzeichen-Fall kommt im Handoff nicht vor, 208px ist empirisch gegen einen
Screenshot mit „YNAB-Spiegel" + „in YNAB gelöscht" nebeneinander kalibriert, nicht aus dem
Handoff abgelesen). Die zusätzliche Breite geht zu Lasten der beiden flexiblen Spalten
(Payee/Memo, `1fr` je Spalte) — sichtbar, aber unauffällig, siehe Screenshot.

**Screenshots neu aufgenommen** (headless Playwright, echte Entwicklungsdatenbank
`~/my_apps/budgetbuddy-dev/budgetbuddy.db`, dafür in ein Scratch-Verzeichnis kopiert und
nur von dort gelesen — keine Schreiboperation gegen die Live-Datei; eigener
Server/Vite-Prozess auf freien Ports 5081/5191 gestartet und nach der Aufnahme wieder
beendet):
- `docs/ux/kassenbuch/screenshots/pc-1440x900-ausgangszustand.png` (ersetzt, Scroll-Position 0)
- `docs/ux/kassenbuch/screenshots/pc-1440x900-gescrollt.png` (neu, AC7: Tabellenkopf
  klebt sauber unter der Navbar, kein Überlappen)
- `docs/ux/kassenbuch/screenshots/handy-390x844-ausgangszustand.png` (neu aufgenommen,
  inhaltlich unverändert — Mobile-Ansicht nutzt `Table` nicht)

Beide gegen `docs/ux/kassenbuch/mockups/1.9-pc-ausgangszustand-zeile-markiert.png` gelegt
und visuell geprüft: Spaltenabstand vorhanden, alle Zeilen gleich hoch, Tabellenkopf beim
Scrollen vollständig sichtbar.

**Build/Tests:** `dotnet build`, `dotnet test` (819 grün, 7 übersprungen, unverändert
gegenüber Iteration 1 — keine neuen Tests, siehe TDD-Begründung unten), `npm run build`
alle grün.

**TDD-Begründung (Skip):** Beide Befunde sind reines CSS/Layout (Zellen-Padding,
Flex-Wrap-Verhalten, Spaltenbreite). `LedgerViewTests`/`SyncFlowViewTests` testen
bewusst nur reine Logik ohne DOM/React-Rendering, weil das unter `dotnet test`
(kein Fable/Browser-Kontext) nicht geht — bestehende Projektkonvention, siehe
Kommentarkopf von `LedgerViewTests.fs`. Es gibt kein Werkzeug im Projekt, das eine
gerenderte Tabellenzeile auf tatsächliches Spalten-Padding oder Zeilenhöhe prüfen
könnte; verifiziert stattdessen manuell per Screenshot gegen das Mockup (s. o.). Die
Lücke „Werkzeug für visuelle Regression" ist bereits als `design-system-wa6wz`
(wiederverwendbares Screenshot-Skript) im Backlog erfasst — kein neues Item nötig.

Schlüsseldateien (Iteration 2): `src/Client/DesignSystem/Table.fs`,
`src/Client/Components/Ledger/View.fs`,
`docs/ux/kassenbuch/screenshots/pc-1440x900-ausgangszustand.png`,
`docs/ux/kassenbuch/screenshots/pc-1440x900-gescrollt.png`,
`docs/ux/kassenbuch/screenshots/handy-390x844-ausgangszustand.png`.
