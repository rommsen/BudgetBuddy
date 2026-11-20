---
id: design-system-k8s2m
title: "Kann-Sammlung design-system"
status: backlog
type: chore
context: design-system
created: 2026-11-07
completed:
depends_on: []
blocks: []
tags: [review, design, a11y, styleguide]
related_adrs: [0087]
related_research: []
prior_art: []
---

## Why
Kann-Sammlung des BC nach der Regel Backlog-Disziplin in CLAUDE.md. Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler kommen als Zeile hierher. Sie wird nie automatisch gebaut.

## What
Geschnitten und priorisiert wird im Refinement.

## Aus dem Review-Lauf nach design-system-bcdqy und infrastructure-nx14s (2026-11-07)
- Fokusring des Schalters „Geprüft“ (`src/Client/DesignSystem/Detail.fs`, `reviewedSwitch`): am Handy liegt er auf dem unsichtbaren 48-px-Quadrat statt auf der Schiene. Abhilfe: Klasse an der Schiene und `button:focus-visible > .switch-track`.
- Styleguide §4 und §7.14 „höchstens drei Schriftgrößen je Ansicht“: die Budget-Seite hat mit `FontSizes.compact`, `microPlus`, Titel und Text wohl mehr. Prüfen und, falls ja, als Zeile in Etappe 3 des Fahrplans.
- Untere Navigation bei 320 px: zwischen den sechs Einträgen hängt der Abstand an `justify-around`, die 8 px aus §7.15 misst der Sweep nicht. Erledigt sich mit „Navigation nur mit den drei Orten“ (Etappe 3).
- `grid-cols-[1fr_2fr]` im Tab Target (`TargetFormView.fs`) ist ein freier Wert; bei 320 px bleiben dem Betragsfeld rund 90 px. Am Bildschirm prüfen.
- `src/Client/DesignSystem/ErrorDisplay.fs:355` zeigt den sichtbaren Text „Dismiss“, englisch (Styleguide §8). Aufgefallen beim Review-Lauf, außerhalb seines Rahmens.
- Rohe 48-px-Klassen außerhalb von `TouchTargets` bestehen noch in `Button.fs:75`, `:76`, `:489`, `Input.fs:36` und `Components/Budget/TargetsView.fs:34` (`min-h-12 md:min-h-10`). Auf `TouchTargets.mobileMinHeight` umstellen (Review-Lauf nach jc1y1).

## Aus design-system-dak16 (2026-11-08, Palette 2)
- PC-Statusspalte im Kassenbuch: „zu prüfen“ plus ein zweites Kennzeichen stößt an den Betrag (Zeilen „Rückfrage und Prüfung“, „Markiert und zu prüfen“ im Sweep-Zustand `kassenbuch` bei 1440); der Sweep meldet es nicht. Schon vorher so eng.
- `Loading.neonPulse` und `Loading.progressBar` glühen noch; nach ADR 0087 Punkt 6 nur Hauptaktion und Rückmeldung.
- Styleguide §1 („Glow … positive Beträge, fokussierte Karten“, „Neon-Akzente“) auf ADR 0087 bringen.
- App-Icons (`src/Client/public/icon-master.svg`, `favicon.svg`, Raster unter `public/icons/`) tragen noch den alten Verlauf Teal→Grün→Orange.
- `Detail.amount` und `MultiSelect.amountClassName` haben keinen eigenen Test auf die neutrale Betragsfarbe (Verifier dak16).

## Aus dem Review-Lauf nach design-system-dak16 (2026-11-08)
- Settings „Nicht konfiguriert“: roter Punkt mit gelbem Text, zwei Rollen für einen Zustand (ADR 0087).
- Settings: Ist Comdirect nicht eingerichtet, steht dessen Formular offen, und „Zugangsdaten speichern“ ist primär, auch wenn eine andere Karte bearbeitet wird (zwei Hauptaktionen).
- `TextGlowTokenTests.fs` ist mit dem Modul `TextGlows` gelöscht (Bugbezug design-system-tg2wn); ein Rückdreh-Beleg nach Doktrin Punkt 8 fehlt, weil es den Code nicht mehr gibt.
- Review-Lauf 2026-11-10 (design-system-ak97z): `KeepsFocus` sitzt je `Segment`, an allen Nutzern hat aber die ganze Leiste denselben Wert; ein Parameter je Leiste (`Segmented.viewKeepingFocus`) ließe sich nicht halb setzen. Erst sinnvoll, wenn mehr Leisten mit `KeepsFocus` dazukommen.
- Review-Lauf 2026-11-10 (design-system-ak97z): Das Warten auf das Ende von `:active` am Knopf "Speichern" (`scripts/layout-states.mjs`, Zustand `kassenbuch-betrag-feldfehler`) gehört allgemein in `settle` (`scripts/layout-invariants.mjs`): vor jeder Messung warten, bis kein Element mehr `:active` ist (`active:scale-*` verkleinert die Trefferfläche kurz).
- Review-Lauf 2026-11-10 (design-system-ak97z): Die Fokus-Prüfungen im Layout-Sweep laufen unter dem Testnamen `[layout] Sweep: …`, sind der Aussage nach `[fokus]`.
- design-system-k6kfw (2026-11-11): Styleguide-Galerie und Export-Bundle laden ohne Backend einen Aufruf für die Konten der Seitenleiste, der still scheitert.
- design-system-k6kfw (2026-11-11): Die Budget-Liste wechselt neben Seitenleiste und Panel bei 1024 bis rund 1150 px auf Zeilen wie am Handy; eine Tabelle mit weniger Spalten könnte das ersetzen.
- design-system-k6kfw (2026-11-11): Eine DS-Knopfvariante „klein, 10 px Radius, 36 px hoch“ (Prototyp `.btn.sm`) für „Abrufen“ am PC; heute ist es `Button` `Secondary`/`Small` als Pille.
- Review-Lauf 2026-11-11 (design-system-k6kfw): Antworten der Übersicht (`AccountsLoaded`, `BalancesLoaded`, Monat) tragen keine Version; eine ältere Antwort kann eine neuere aus `syncNavAccounts` überschreiben. Risiko gering (dieselbe Quelle), eine Generation wie im Budget wäre billig.
- Review-Lauf 2026-11-11 (design-system-k6kfw): `Components/Overview` greift auf Geschwistermodule zu (`Components.Budget.Month`/`Types`, `Components.Ledger.Types`/`View`), gegen `.claude/rules/elmish-state.md`. Geteilte Helfer (`filterCount`, `buildAccountCards`, `formatEntryDate`, `categoryLabel`, `isWithoutCategory`) in ein gemeinsames Modul ziehen oder die Ausnahme in der Regel festhalten.
- Review-Lauf 2026-11-11 (design-system-k6kfw): Unterseiten im Budget (Kategorien, Historie, Targets prüfen) haben das neue „Zurück“, aber den Titel im alten Stil; Einstellungen, Regeln und Styleguide nutzen `PageHeader.top` mit Display-Schrift. Angleichen.
- banking-import-d0fey (2026-11-12): `ErrorDisplay.noticeWithLead` hat ein Warnsymbol, der TAN-Hinweis im Prototyp nicht. Eine Variante ohne Symbol, falls die Skizze genau gelten soll.
- Review-Lauf 2026-11-12: `/styleguide` zeigt `Card.sheetMessageBusy` und `BottomSheet.viewHeld` noch nicht als Beispiel.
- Review-Lauf 2026-11-12: Der Layout-Sweep erkennt keinen Text, der mit `white-space: nowrap` über einen Knopf hinausläuft (gemessen an `durchgehen-duplikat` bei 320 px mit dem alten `Button.Choice`: Text 155 px im 140 px breiten Knopf, keine Verletzung). Neue Invariante mit Gegenbeweis.
- Review-Lauf 2026-11-12: Die Namen in `Tokens.Radius` passen nicht zu `styles.css` (`Radius.md` = `rounded-lg` = 16 px, `Radius.lg` = `rounded-xl` = 24 px, die Kommentare sagen 8 und 12 px). Kommentare oder Zuordnung angleichen; die Karte des Durchgehens nutzt deshalb `rounded-[14px]` und `rounded-[12px]`.
- Review-Lauf 2026-11-12: `rounded-[5px]` an `kbd` (ReviewCard) und sechsmal `text-base md:text-sm` roh: als Tokens `Radius.kbd` und `FontSizes.value` (mittlere Stufe aus §7.14) benennen.
- Review-Lauf 2026-11-13 (categorization-n3sxw): Microcopy „Bitte ausfüllen: …“ an allen `Form.submitButton*` ist eine Höflichkeitsfloskel (§8); knapper „Fehlt: Muster, Kategorie“. Betrifft alle Formulare.
- Review-Lauf 2026-11-13 (categorization-n3sxw): Panelbreite steht mehrfach roh (`md:pr-[420px]`, `lg:pr-[444px]` in `Components/Rules/View.fs` und `Components/Ledger/View.fs` neben `Detail.panel`). Mit design-system-gcpws (30 % für alle Panels) eine gemeinsame Konstante in `Detail` oder `Tokens`.
- Review-Lauf 2026-11-13 (categorization-n3sxw): Neue Tokens `ListRoles.selectedFill`, `selectedEdge`, `selectedRow` (gewählte Zeile wie die Buchung). Die Buchungszeile in `src/Client/Components/Ledger/View.fs` (~506, 520, 523, 776, 880, 881) nutzt dieselben Klassen noch roh; auf die Tokens umstellen. `/styleguide` zeigt `ListRoles.selectedRow` und `Loading.cardListSkeleton` noch nicht.
- Aus design-system-taytp (aufgegangen in ledger-ea70s, 2026-11-13): ADR 0011 Decision 3 nennt positive Available-Beträge grün, `Money.fs:225` und Styleguide §2 färben sie neutral. Nachtrag zu ADR 0011 oder Code angleichen.
- Review-Lauf 2026-11-14 (ledger-ea70s): `/styleguide` Abschnitt 19 „Kategorienauswahl“ zeigt nach dem Wegfall von `BottomSheet.categoryPicker` dasselbe `CategoryPicker.sheet` wie Abschnitt 26; er bleibt nur wegen der Export-Karte `components-categorypicker`. Karte in Abschnitt 26 verlegen, 19 streichen.
- Review-Lauf 2026-11-14 (ledger-hg5m5): `Modal.CloseOnEscape` wird nirgends ausgewertet; jeder Aufrufer muss Esc selbst verdrahten (für das Regel-Formular im Durchgehen ist es jetzt über `reviewKeyRoute` gelöst). Entweder im `Modal` auswerten oder das Feld entfernen.
- Review-Lauf 2026-11-14 (ledger-ea70s): Für den `CategoryPicker` sichert nach dem Aufräumen in `AuswahlTests.fs` nur noch `PickerParts.row` (`CategoryPickerTests.fs` ~:316) ab; Gruppenkopf und „kein `Money.available`“ sind für ihn nicht mehr zugesichert.
- work-Lauf 2026-11-15 (design-system-gcpws): verbliebene seitenweise Tastentests in den Tasten-Sweep ziehen, etwa Esc mit Fokus-Rückkehr für Payee (`ledger-payee` 157) und Angepinnt (`kategorie-anpinnen` 81) als weitere `DIALOGS` in `scripts/key-invariants.mjs`. Die Kann-Zeile zu `Modal.CloseOnEscape` (Review 2026-11-14) ist jetzt Ticket design-system-m4e7q.
- Review-Lauf 2026-11-15 (design-system-gcpws): kurze Auswahl (`PickerParts.shortChoiceRow`, auch `Money.accountListRowWith` in „Neue Buchung“) meldet die gewählte Option mit `aria-current`; besser `role="radiogroup"` am Container und `role="radio"` mit `aria-checked` an den Zeilen, der Fokus-Selektor hängt dann an `aria-checked`.
- Review-Lauf 2026-11-15 (design-system-gcpws): `CategoryMultiSelect.fs:~98` rechnet die Zeilen-Indizes zweimal (als `rows` und über einen veränderlichen `counter` beim Rendern); einmal mit `List.indexed` vorberechnen und je Abschnitt filtern.
- Review-Lauf 2026-11-15 (design-system-gcpws): `ListKeysTests.fs:104` („Esc schließt erst das Panel …“) prüft nur, was die Aufzählung schon enthält; kann weg.
- Work-Lauf 2026-11-19 (ledger-zf6zg): `Money.accountListRow` und `accountListRowWith` haben außer `/styleguide` keinen Nutzer mehr, seit „Neue Buchung“ `Money.accountChoiceRow` nutzt; entfernen oder als Variante begründen.
- Review-Lauf 2026-11-19 (ledger-zf6zg): §5 „Detail“ nennt für `titleHead` und `formHead` „Titel 17 px halbfett“, der Code nimmt `FontSizes.lg` (16 px, ab md 18 px). Doku oder Token angleichen.
- Work-Lauf 2026-11-20 (design-system-x045q): der Layout-Sweep ist ein einzelner Test mit rund 300 Läufen und braucht jetzt eine eigene Höchstdauer (360 s in `scripts/layout-invariants.test.mjs`). In zwei Tests teilen (Handy, PC), damit er unter Last wieder unter die gemeinsame Höchstdauer passt.
- Work-Lauf 2026-11-20 (design-system-ytgnc): der Hinweistext `note` der Styleguide-Galerie (`text-text-muted/80`) hat 3,99:1 und steht deshalb als Ausnahme der Invariante 10 im Zustand `styleguide-rueckmeldung`.
- Work-Lauf 2026-11-20 (design-system-gkded): Ausnahme `kassenbuch-kategorie-toast` @ 1280, Invariante 5, `[data-testid="detail-history-toggle"]`: der PC-Toast steht 5 s über „Historie“ am Ende des Details (gleiche Lage wie vorher). Fällt weg, sobald ledger-p5y9s die Kategorie am Feld bestätigt; dann die Ausnahme streichen.
- Work-Lauf 2026-11-20 (design-system-gkded): `scripts/browser-usage.mjs` `FLOWS` nennt bei `verschiebung-erneut-senden` noch „über den Toast“.
