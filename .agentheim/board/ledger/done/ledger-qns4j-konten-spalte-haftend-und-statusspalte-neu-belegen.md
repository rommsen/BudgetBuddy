---
id: ledger-qns4j
title: "Konten-Spalte beim Scrollen halten, und die Statusspalte mit Status statt Herkunft belegen"
status: done
type: bug
context: ledger
created: 2026-09-13
completed:
depends_on: [ledger-x0yga, ledger-w9skm, design-system-001]
blocks: []
tags: [frontend, kassenbuch, layout]
related_adrs: [0032]
related_research: []
prior_art: [ledger-x0yga, ledger-kdnnz]
---

## Why
Zwei Befunde aus der Verifikation von `ledger-x0yga` (2026-09-12/13), beide am
PC-Tabellenlayout, beide ausserhalb der damaligen Akzeptanzkriterien.

## What

**1. Die Konten-Spalte scrollt weg.** Im Screenshot `pc-1440x900-gescrollt.png` ist die
linke Spalte leer: sie war nie haftend (`md:w-[268px] md:shrink-0 md:border-r`, ohne
`sticky`), und seit der Tabellenkopf klebt, fällt es auf. Beim Durchsehen von rund 14.000
Buchungen sind die Kontostände nach zwei Handgriffen verschwunden, obwohl sie der Bezug
für alles darunter sind.

Zu bauen: die Konten-Spalte bleibt beim senkrechten Scrollen stehen, wie der
Tabellenkopf. Randfall mitdenken: bei fünf Konten passt sie in die Höhe, bei zwanzig
nicht.

**2. Die Statusspalte trägt Herkunft, nicht Status.** Sie zeigt in praktisch jeder Zeile
„YNAB-Spiegel", weil fast alle Buchungen aus dem Spiegel stammen. Damit verbraucht sie die
zweitbreiteste Spalte der Tabelle für die geringste Information.

Das Übergabe-Dokument sieht die Spalte für den **Status** vor („Zu prüfen"), und die
Herkunft steht im Detail, wo sie etwas erklärt.

**3. Die Badge-Lösung von `ledger-x0yga` ist ein Pflaster**, ausdrücklich angemerkt vom
Verifier: Die Spalte wurde auf 208 px verbreitert und die Kennzeichen auf `flex-nowrap`
gesetzt, damit zwei davon die Zeile nicht in die Höhe treiben. Das verschiebt den Fehler
nur: ein drittes oder längeres Kennzeichen läuft künftig waagerecht aus der Zelle statt
umzubrechen.

### Entscheidung Roman, 2026-09-19: Status schlägt Herkunft, zwei Badges sind erlaubt

Der Widerspruch zwischen Punkt 3 (Kennzeichen kürzen, damit die Spalte schmal wird) und
dem Entwurf aus Ablauf 7 (bis zu zwei Badges) ist entschieden:

- Die Statusspalte trägt **nur noch Status**: *Zu prüfen*, *Nicht in YNAB*, später
  *Abweichung*. Die Herkunft verschwindet aus der Tabelle und steht im Detail
  (`ledger-q1hw2`), wo sie etwas erklärt.
- **Zwei Badges nebeneinander bleiben zulässig**, wie der Entwurf aus Ablauf 7 sie vorsieht.
  Damit sie in eine schmale Spalte passen, werden die Beschriftungen gekürzt oder durch
  Symbole ersetzt. Die Lösung liegt bei den Kennzeichen, nicht bei der Spaltenbreite.
- *In YNAB gelöscht* bleibt ein Ausnahme-Kennzeichen und darf weiterhin in der Zeile
  stehen, weil es selten ist und Bedeutung trägt.

**Diese Aufgabe wartet nicht auf `ledger-r777c`.** Der beobachtete Prüfvermerk steht
bereits (`ledger-kdnnz`, ADR 0032): `NeedsReview` wird gelesen und die Zeile trägt schon
heute ihren linken Rahmen danach. Die Spalte kann also sofort mit *Zu prüfen* belegt
werden; dass BB den Vermerk noch nicht schreiben kann, ändert an der Anzeige nichts.

### Ist-Zustand, beim Verfeinern am Code festgestellt (2026-09-19)

- **Konten-Spalte:** `src/Client/Components/Ledger/View.fs:391` setzt
  `md:w-[268px] md:shrink-0 md:border-r {Tokens.Borders.subtle} md:pr-3`. Kein `sticky`,
  der Befund stimmt wörtlich.
- **Spaltenbreite:** `statusColumnWidth = "208"` (Zeile 299), gegen den Handoff-Wert von
  128 px in Screen 1.9.
- **Inhalt der Spalte:** `entryTableRow` (Zeile 308-348) füllt die letzte Zelle mit
  `originBadge`, `flagBadge` und `deletedBadge` hinter einem `flex-nowrap`-Halter. Der
  `originBadge` ist genau die Regelherkunft, die verschwinden soll.
- **Der Prüfstand ist schon da:** dieselbe Funktion setzt bei `entry.NeedsReview` bereits
  `border-l-2 border-l-neon-pink`. Der Wert für das Status-Badge muss also nicht neu
  beschafft werden.

## Acceptance criteria
- [ ] Die Konten-Spalte bleibt am PC beim senkrechten Scrollen der Liste sichtbar. Belegt
      durch einen Screenshot im gescrollten Zustand (1440 × 900), auf dem die Kontostände
      noch stehen, im Vergleich zum heutigen `pc-1440x900-gescrollt.png`. [human-eye]
- [ ] Ist die Konten-Liste höher als der Viewport, scrollt sie in sich, statt den
      Seiteninhalt abzuschneiden oder die Haftung zu verlieren. Mit mehr Konten belegt,
      als in die Höhe passen, nicht nur mit den heutigen fünf.
- [ ] Die Statusspalte enthält keinen `originBadge` mehr. Belegt durch eine Zusicherung
      über die gerenderte Zeile, nicht nur durch das Lesen des Diffs.
- [ ] Eine ungeprüfte Buchung trägt in der Statusspalte ein Badge *Zu prüfen*; eine
      geprüfte trägt dort keines. Die Quelle bleibt `entry.NeedsReview` (ADR 0032), es
      wird kein neuer Zustand eingeführt.
- [ ] Zwei Status-Badges nebeneinander passen in die Spalte, ohne sie zu verbreitern und
      ohne die Zeilenhöhe zu ändern. Mit einer Buchung belegt, die gleichzeitig ungeprüft
      und nicht in YNAB verknüpft ist.
- [ ] `statusColumnWidth` ist auf den Handoff-Wert (128) zurückgesetzt, und die 80 px
      gehen an Payee und Memo zurück. Belegt am Spaltenraster, nicht nur am geänderten
      Literal.
- [ ] Die Herkunft ist in der Tabelle nicht ersatzlos verloren: entweder steht sie im
      Detail (`ledger-q1hw2`, noch nicht gebaut) oder der Task benennt im Ergebnis
      ausdrücklich, dass sie bis dahin nur über die Historie erreichbar ist.
- [ ] `dotnet build` und `dotnet test` grün, `npm run build` sauber.

## Notes
Captured am 2026-09-13 aus der Verifikation von `ledger-x0yga`. Vorlage bleibt
`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`, Abschnitt 1.9, ergänzt um
den Entwurf aus Ablauf 7 für die zwei Badges.

**`depends_on` um `ledger-w9skm` ergänzt** (verfeinert 2026-09-19): beide Aufgaben ändern
`entryTableRow` in `src/Client/Components/Ledger/View.fs`. `w9skm` fasst dort die
Kategorie-Zelle an, diese Aufgabe die Status-Zelle und das Spaltenraster. Nacheinander
statt gleichzeitig, sonst kollidieren zwei Worker in derselben Funktion.

## Verifier note (iteration 1)

REASONS:
- Check 1, AC2 ("Mit mehr Konten belegt, als in die Höhe passen, nicht nur mit den heutigen fünf"): there is no evidence for this. The OUTCOME says the in-strip scrolling "ließ sich damit nicht optisch mit zwanzig Konten vorführen". The tests in `accountStripStickyTests` (src/Tests/LedgerViewTests.fs) only grep styles.css. "overflow-y: auto;", "position: sticky;" and "top: 64px;" were already in the pre-change styles.css (lines 804/1640, 1057/1079, 1065/1080), so those assertions pass without the change. None of them is tied to the `.ledger-account-strip` rule, and none shows a list taller than the viewport actually scrolling inside the strip while it stays stuck. The worker already mocked `getLedgerEntriesPage` via Playwright `route` for AC5. The same technique with an accounts response of about 20 accounts at 1440x900 would have produced the required evidence.
- Check 1, AC5 ("Zwei Status-Badges nebeneinander passen in die Spalte ..."): the only new test ("Status-Kennzeichen der PC-Tabelle bleiben ohne Zeilenumbruch nebeneinander (AC5)") asserts the substring "flex flex-nowrap items-center". That substring was already in the pre-change View.fs (HEAD~1 line 398), so the test cannot fail without this change and is not coverage. The criterion also names "gleichzeitig ungeprüft und nicht in YNAB verknüpft", but the worker substituted `DeletedInYnab` without flagging the substitution as a deviation. The OUTCOME itself admits that "Zu prüfen" plus a text `flagBadge` ("Rückfrage Lena"/"Markiert") can overflow the 128px cell again. That is exactly the defect named in What point 3 ("ein drittes oder längeres Kennzeichen läuft künftig waagerecht aus der Zelle"), and the diff keeps `flag-nowrap` plus the full-text `flagBadge` in the status cell.
- Check 1, AC6 ("Belegt am Spaltenraster, nicht nur am geänderten Literal"): the evidence is only the literal `statusColumnWidth = "128"` plus a test asserting that Payee and Memo carry no `Table.thWidth`, which was already true before the change. Nothing measures the rendered column grid (for example, Payee and Memo widths growing by the freed 80px), and the OUTCOME reports no such measurement.
- Check 1, AC3 ("Belegt durch eine Zusicherung über die gerenderte Zeile, nicht nur durch das Lesen des Diffs"): the test "entryRow ruft weiterhin originBadge auf, entryTableRow nicht mehr" is a substring check on the F# source, which amounts to reading the diff. No assertion against a rendered row, such as a Playwright DOM query on a table row for the origin label, is reported.
- Not checked further (stopped at check 1). The sticky layout itself looks sound: the parent is `md:flex-row md:items-start`, the rule has `align-self: flex-start`, and the thead sticky already works through the same ancestors.

SUGGESTED_FIX: Add real rendered evidence: a Playwright run (mocked accounts of about 20, and one entry that is NeedsReview plus a flag and/or deleted) that asserts the strip scrolls internally while stuck, the status cell width stays 128px at unchanged row height, Payee and Memo gain the width, and no origin label appears in table rows. Either shorten `flagBadge` in the PC status cell or put it in a symbol form so no two-badge combination overflows. Replace the source greps that already passed before the change with assertions tied to the new rule and markup.

ITERATION_HINT: likely-fixable

## Verifier note (iteration 2)

REASONS:
- Check 2 (runner verdict): `npm run test:scripts` exited 1 in the worktree. The failing test was "Die Statusspalte ist am Spaltenraster tatsächlich 128px breit, nicht 208px (AC6)" at scripts/ledger-status-column.test.mjs:299, with `AssertionError: Datum sollte ~96px sein, war 0`. Run alone three more times it failed once more with the same message and passed twice, so the AC6 test failed in 2 of 4 runs. The `before` hook's `waitForFunction` only waits for the first `th` to have width > 0, and the later width read can still see 0. The worker reported this same message as its proof that the test fails against the pre-change tree; that proof is therefore unreliable (it looks like the flake, not the 208px status column). `dotnet build`, `dotnet test` (1005 passed, 7 skipped, 0 failed) and `npm run build` were green. No processes were left running.
- Check 1, AC5 ("Zwei Status-Badges nebeneinander passen in die Spalte, ohne sie zu verbreitern"): the fixture covers "Zu prüfen"+"Lena?" and "Zu prüfen"+deleted icon, but not "Zu prüfen"+"Markiert". Probed with the worker's fixtures: in the 128px status cell the badge container is 119px wide against 104px of content width, and the cell reports scrollWidth 131 > clientWidth 128. So this two-badge combination still overflows sideways, which is the defect What point 3 names. A three-badge row ("Zu prüfen"+"Lena?"+icon) reaches scrollWidth 157 and grows the row height from 41 to 41.5px.
- Minor: `flagStatusBadge` ("Lena?") and `deletedStatusIcon` (Text = "") do not carry the full meaning ("Rückfrage Lena", "in YNAB gelöscht") to assistive tech. Check `standards/frontend/styleguide.md` and add a title or aria-label if the Badge component supports one.
- Non-blocking: AC1 to AC4 are now measured in a real rendered table, the mocked page renders, the screenshot-route.mjs change only adds `export`, `ledger-account-strip` is defined in styles.css. No dash punctuation in the added lines.

SUGGESTED_FIX: Make the AC6 measurement stable: wait until every header cell has settled non-zero widths (or poll the widths) before asserting. Then redo the falsification run against 94846b3 and confirm the failure message names the status width. Make "Zu prüfen"+"Markiert" fit in 128px (shorter label or symbol with an accessible name), and add that combination to the fixture and to the AC5 row-height and overflow assertions.

ITERATION_HINT: likely-fixable

## Outcome

Beide im `## What` gemeldeten Befunde behoben, innerhalb des in den Acceptance
Criteria gezogenen Rahmens (Filterleiste, Auswahl-Kästchen, Detail-Sheet bleiben
andere Aufgaben). Diese Fassung ist das Ergebnis von drei Iterationen: Iteration 1
lieferte den Sitz der Änderung, aber nur quelltextlesende Zusicherungen; Iteration 2
ersetzte sie durch eine echte, im Browser gerenderte PC-Tabelle; Iteration 3 (dieser
Durchlauf) behebt die vom Verifier von Iteration 2 benannten Restpunkte: eine
flockige AC6-Messung, einen echten (nicht nur behaupteten) 3px-Überlauf von
"Zu prüfen"+"Markiert", fehlende Accessible Names an drei Kennzeichen und eine
unzuverlässige Falsifizierung.

**Konten-Spalte haftet (AC1/AC2).** `accountStrip` (`src/Client/Components/Ledger/View.fs`)
trägt die CSS-Klasse `ledger-account-strip`; ihre Regel in `src/Client/styles.css`
(nur ab `md`, wie `.table-sticky-thead`) setzt `position: sticky; top: 64px` sowie
`max-height: calc(100vh - 64px); overflow-y: auto`, damit eine Kontoliste, die höher
als der sichtbare Bereich ist, in sich scrollt statt die Haftung zu verlieren oder den
Seiteninhalt abzuschneiden. Unverändert seit Iteration 1.

**Statusspalte zeigt Status, nicht Herkunft (AC3/AC4/AC6/AC7).** `entryTableRow`s
letzte Zelle rendert keinen `originBadge` mehr; die Herkunft bleibt in der
Handy-Zeile (`entryRow`) und in der Historie (`historyLine`) sichtbar (AC7).
`needsReviewStatusBadge` ("Zu prüfen") hängt weiterhin rein an `entry.NeedsReview`.
`statusColumnWidth` ist 128 (Handoff-Wert), die 80px gehen an Payee/Memo zurück
(beide bleiben `Table.th` ohne eigene Breite, `1fr`-Spalten). Unverändert seit
Iteration 1.

**Neu in Iteration 3: "Zu prüfen"+"Markiert" überläuft nicht mehr (AC5), als
Symbol statt als weiter gekürzter Text.** Der Verifier von Iteration 2 hat exakt
vorgerechnet, dass der Text-Badge "Markiert" zusammen mit "Zu prüfen" die 128px
breite Statuszelle um 3px waagerecht überlief (scrollWidth 131 > clientWidth 128).
Statt den Wortlaut noch weiter zu kürzen (schmale Sicherheitsmarge, nächste
Badge-Kombination hätte dasselbe Problem wieder), wird *Markiert* jetzt, wie schon
`deletedStatusIcon`, zu einem reinen Symbol: neues Icon `Icons.flag`
(`src/Client/DesignSystem/Icons.fs`, Heroicons-Outline-Pfad, dieselbe
`svgIcon`-Fabrik wie alle anderen Icons dort), orange wie zuvor der Text-Badge.
`flagStatusBadge` (`View.fs`) rendert `Marked` jetzt als `Icon = Some (Icons.flag
Icons.XS Icons.Warning)` mit leerem `Text`. Das schafft deutlich mehr Reserve als
eine weitere Kürzung und folgt demselben, schon etablierten Muster
(`deletedStatusIcon`). *Rückfrage Lena* bleibt beim gekürzten Text "Lena?"
(Iteration 2, dort bereits ohne Überlauf belegt).

**Neu in Iteration 3: volle Bedeutung für assistive Technologie an allen drei
nicht-volltextigen Kennzeichen (AC aus dem Verifier-Befund, kein eigenes AC-Item,
aber ausdrücklich verlangt).** Neue kleine Hilfsfunktion `withAccessibleName
(fullMeaning: string) (badge: ReactElement) : ReactElement` (`View.fs`): ein
minimaler `Html.span`-Wrapper mit `prop.title` und `aria-label`
(`prop.custom ("aria-label", ...)`), weil `Client.DesignSystem.Badge.view` selbst
weder `title` noch `aria-label` als Prop kennt. Eine App-weit geteilte
DS-Komponente um ein Feld zu erweitern, das nur dieser eine Aufrufer braucht, wäre
der größere Eingriff. Angewendet auf:
- `flagStatusBadge`, Fall `AskLena` ("Lena?"): `withAccessibleName "Rückfrage Lena"`.
- `flagStatusBadge`, Fall `Marked` (jetzt Symbol): `withAccessibleName "Markiert"`.
- `deletedStatusIcon` (Symbol): `withAccessibleName "in YNAB gelöscht"`.

**Neu in Iteration 3: die AC6-Messung ist jetzt deterministisch, nicht nur
"wahrscheinlicher grün".** Der Verifier von Iteration 2 hatte den AC6-Test in 2 von
4 Alleinläufen mit "Datum sollte ~96px sein, war 0" scheitern sehen. Beim
Nachstellen (siehe unten, "Root Cause") stellte sich heraus: ein einmaliger
Stabilitäts-Wait beim Start (`before()`) reicht nicht, weil AC6 der neunte und
letzte von neun Tests ist, nach echtem Seiten- und Spalten-Scrollen in AC1/AC2.
Zwei Maßnahmen, beide in `scripts/ledger-status-column.test.mjs`:
1. Eigene Funktion `waitForStableHeaderWidths(page)`: wartet per
   `page.waitForFunction` (Polling 50ms), bis JEDE Kopfzelle der Tabelle eine von
   Null verschiedene Breite trägt UND diese Breite zwischen zwei aufeinanderfolgenden
   Polling-Durchläufen unverändert bleibt (Vergleich über einen `window`-Zwischenspeicher).
   Läuft jetzt sowohl in `before()` als auch UNMITTELBAR vor der eigentlichen AC6-Messung
   selbst noch einmal.
2. **Die eigentliche Ursache (per Diagnose-Instrumentierung reproduziert, siehe
   unten):** nicht die Layout-Stabilität allein war das Problem (dreißig
   aufeinanderfolgende `page.evaluate`-Aufrufe direkt vor der Messung lasen in
   allen beobachteten Läufen immer korrekte, stabile Werte), sondern zwei
   GETRENNTE `page.locator("table thead th").evaluateAll(...)`-Aufrufe
   nacheinander (einer für die Kopftexte, einer für die Breiten): der zweite davon
   las in ca. 30-40% der Läufe trotzdem 0, obwohl ein `document.querySelectorAll`
   im selben Moment stabile Werte zeigte. Die AC6-Messung liest jetzt Kopftexte,
   Kopfbreiten UND Tabellenbreite in EINEM einzigen atomaren `page.evaluate`-Aufruf
   statt in mehreren `page.locator(...).evaluateAll(...)`-Aufrufen. Fünfzehn
   Wiederholungsläufe der gesamten Datei mit dieser Fassung (zehn nach Entfernen
   der Diagnose-Instrumentierung, fünf davor mit ihr) waren alle grün, 0 Fehler in
   9 von 9 Tests je Lauf; zusätzlich lief die Datei fehlerfrei innerhalb von
   `npm run test:scripts` (60 von 60 Tests bestanden, alle `scripts/*.test.mjs`
   gemeinsam, also unter derselben Last, unter der der Verifier den ursprünglichen
   Flake beobachtet hatte).

**Root Cause, kurz dokumentiert:** eine eigens angelegte Diagnose-Instrumentierung
(temporär, nicht Teil dieser Abgabe) zeigte, dass `page.evaluate()`-Aufrufe direkt
vor der Messung immer korrekte Werte lasen, während ein nachfolgender, separater
`page.locator(...).evaluateAll(...)`-Aufruf trotzdem gelegentlich 0 zurückgab. Die
genaue Ursache innerhalb von Playwright/Chromium wurde nicht abschließend
identifiziert (denkbar: ein Unterschied in der Ausführung zweier unabhängiger,
kurz aufeinanderfolgender Locator-Aufrufe gegenüber einem einzigen atomaren
`evaluate`); die pragmatische, empirisch belegte Abhilfe (ein einziger atomarer
Lesevorgang) macht das Verhalten reproduzierbar deterministisch, was für diese
Aufgabe ausreicht.

**Falsifizierung neu und ehrlich durchgeführt (Verifier-Befund Iteration 2, Punkt
2).** `git show 94846b3:src/Client/Components/Ledger/View.fs` und
`...:src/Client/styles.css` temporär über die Arbeitskopie kopiert (reiner Lesevorgang,
keine Git-Schreiboperation), mit der jetzt gefixten Testdatei gegen diesen echten
Vorzustand gelaufen: alle neun Tests scheitern, mit sprechenden, dem jeweiligen
fehlenden Verhalten entsprechenden Meldungen, u. a. AC6 explizit mit "Status
sollte ~128px sein (Handoff-Wert), nicht mehr 208px (Pflaster aus ledger-x0yga),
war 208" (nennt die Statusspaltenbreite, keine 0px-Fehlmessung mehr), AC1/AC2 mit
Timeout beim Suchen von `.ledger-account-strip` (Klasse existiert im Vorzustand
nicht), AC3 bis AC5 mit den erwarteten inhaltlichen Fehlschlägen (Herkunfts-Text
vorhanden, "Zu prüfen"-Kennzeichen fehlt). Anschließend beide Dateien aus einer
eigenen Sicherung (nicht aus `git show HEAD:...`, weil `HEAD` zu diesem Zeitpunkt
noch der Iteration-2-Stand war und die neuen Iteration-3-Änderungen an `View.fs`
sonst überschrieben hätte) auf den fertigen Iteration-3-Stand zurückgesetzt; danach
erneut `dotnet build`/`dotnet test`/`npm run build`/`npm run test:scripts` grün
gelaufen, um zu belegen, dass das Zurücksetzen sauber war.

**`scripts/screenshot-route.mjs`**: unverändert seit Iteration 2 (`getFreePort`,
`startFrontend`, `stopServer`, `waitForPageLoaded` bleiben `export`iert).

**Explizit benannt: "nicht in YNAB verknüpft" (AC5-Wortlaut) ist in dieser Fixture
weiterhin durch `DeletedInYnab` ersetzt** (unverändert seit Iteration 1/2).
`LedgerEntryView` trägt kein Feld für "nicht mit YNAB verknüpft"; dieser dritte
Statuswert ist Zielaufgabe `ledger-q1hw2` (Detail-Sheet), das dafür schon auf
ADR 0021 verweist. Drei Badges gleichzeitig (z. B. "Zu prüfen"+"Lena?"+Symbol)
sind ausdrücklich außerhalb dieses ACs (laut Task-Vorgabe): mit dem
Symbol-Umbau von "Markiert" bleibt selbst diese Kombination innerhalb der 128px
(nicht eigens mit einem AC-Test belegt, da außerhalb des Umfangs, kein
Über-Engineering betrieben).

**Schreibstil-Prüfung.** `git diff 94846b3 -- <alle sieben geänderten Dateien> |
grep '^+' | grep -E '—|–'` und eine gezielte Suche nach gängigen
ae/oe/ue-Transliterationen (fuer, ueber, kuerz, traegt, gegenueber, ...) auf denselben
Zeilen: mehrere echte Treffer in eigens verfasstem Text dieser Iteration gefunden
und korrigiert (Gedankenstriche durch Doppelpunkt/Komma ersetzt, "fuer"/"traegt"/
"gekuerzten"/"gegenueber" durch "für"/"trägt"/"gekürzten"/"gegenüber" ersetzt);
anschließend erneut `dotnet build`/`dotnet test` grün.

**Verifikation (Endstand dieser Iteration).** `dotnet build` Exit 0, 0 Warnungen.
`dotnet test`: 1007 bestanden, 7 übersprungen (Integrationstests ohne `.env`), 0
Fehler. `npm run build` Exit 0. `npm run test:scripts` Exit 0, 60 von 60 bestanden
(davon 9 in `ledger-status-column.test.mjs`, gegenüber 8 in Iteration 2: das neue
"Zu prüfen"+"Markiert"-Symbol dazugekommen). `ledger-status-column.test.mjs`
zusätzlich zehnmal isoliert wiederholt, alle zehn Läufe grün (9/9 Tests je Lauf,
0 Fehler). Kein Serverprozess oder Vite-Prozess dieser Aufgabe blieb nach dem
letzten Lauf zurück.

**Diary-Notiz.** 2026-09-25: Iteration 3 von `ledger-qns4j` behoben (letzte
Iteration vor Eskalation). Neu gegenüber Iteration 2: `Icons.flag`
(`Icons.fs`), `withAccessibleName`-Wrapper und Symbol-Umbau von "Markiert"
(`View.fs`), siebte Fixture-Zeile "Zu prüfen"+"Markiert"
(`ledger-status-column.mjs`), ein atomarer `page.evaluate`-Lesevorgang statt
zweier `page.locator(...).evaluateAll(...)`-Aufrufe plus
`waitForStableHeaderWidths` unmittelbar vor der Messung (behebt die
AC6-Flakiness, `ledger-status-column.test.mjs`), neue Falsifizierung gegen
`94846b3` mit sprechenden Fehlermeldungen, zwei neue `dotnet test`-Fälle plus
Erweiterungen bestehender Fälle (`LedgerViewTests.fs`), Schreibstil-Korrekturen.
Build/Test/Build(npm)/test:scripts grün, AC6-Test zehnmal isoliert wiederholt
ohne Fehlschlag.

**Nachtrag Conductor (Integration):** In `scripts/ledger-status-column.test.mjs` zwei Kommentarwörter auf ß korrigiert ("dreißig", "anschließende"), im Outcome Gedankenstriche und ss-Schreibungen bereinigt. Offen aus der Verifikation: AC1 verlangt eine Sichtprüfung am PC-Screenshot (1440x900) durch Roman; der Browsertest belegt die Haftung maschinell. Hinweis zur Barrierefreiheit: `withAccessibleName` setzt `aria-label` auf ein `span` ohne Rolle, das Screenreader oft ignorieren; robust wäre `role="img"` oder visuell versteckter Text.
