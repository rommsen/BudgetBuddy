---
id: budgeting-w9yt4
title: "Angepinnte Kategorien: Block „Angepinnt“ auf der Übersicht, Sheet zum Anpinnen, Lösen und Ordnen, Pin-Knopf im Kategorie-Detail, gespeichert als Zustand"
status: backlog
type: feature
context: budgeting
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, ledger-s1weh]
blocks: []
tags: [etappe-3, frontend, backend, uebersicht]
related_adrs: [0083,0087,0088]
related_research: []
prior_art: [budgeting-sn2sn]
---

## Why
Roman will die Kategorien, auf die er im Monat achtet, ohne Suche sehen. Die Übersicht (ledger-s1weh) hat dafür
einen Platz zwischen Monat und Konten, der heute leer ist.

## What
Angepinnte Kategorien stehen auf der Übersicht als eigener Block „Angepinnt“, unter dem Monat. Anpinnen, Lösen und
die Reihenfolge setzt Roman in einem Sheet, das „Bearbeiten“ im Kopf des Blocks öffnet. Im Kategorie-Detail
(Inspektor der Budget-Seite) pinnt und löst ein Knopf im Kopf. Die Pins sind Zustand in BB, kein Ereignis (ADR 0083),
wie Reihenfolge und Deaktivieren (ADR 0088 Punkte 2 und 3). YNAB erfährt davon nichts.

Vorlage: Prototyp Version 17, Funktionen `pinBlock`, Sheet `pins` in `sheetHtml`, Kopf des Sheets `bcat`,
Aktionen `pinToggle`, `pinUp`, `openCat`. Screenshots im Scratchpad (siehe Notes).

**Block „Angepinnt“ auf der Übersicht**
- Lage: am Handy nach dem Monat, vor den Konten. Am PC in der rechten Spalte nach dem Monat, vor „Neueste Buchungen“.
- Kopf wie die anderen Abschnitte (`sec-h`, Mono 11 px, Versalien, gedämpft): links „Angepinnt“, rechts „Bearbeiten“
  als leiser Textknopf im Akzent. „Bearbeiten“ fehlt, solange nichts angepinnt ist.
- Je Kategorie eine Zeile in einer Karte, in der gespeicherten Reihenfolge, als ganzer Knopf:
  - Zeile 1: Name links (500). Rechts Verfügbar in Mono, rot bei Verfügbar unter null. Davor „fehlt <Betrag>“ in
    Gelb (12 px), wenn der Kategorie im laufenden Monat Geld zum Target fehlt (dieselbe Größe wie „Targets anwenden“
    in Zu tun; pausiertes Target zählt nicht).
  - Zeile 2: Balken 5 px hoch, Radius 3 px, Grund `border`, Füllung `text-secondary`, rot bei Verfügbar unter null.
    Anteil = ausgegeben / (Verfügbar minus Aktivität), begrenzt auf 0 bis 100 %. Ausgegeben ist die Aktivität mit
    umgekehrtem Vorzeichen.
  - Zeile 3: „<ausgegeben> von <Verfügbar minus Aktivität> ausgegeben“ in 12 px gedämpft.
  - Antippen öffnet die Kategorie im Budget des laufenden Monats, Tab Geld: am Handy die Budget-Seite mit offenem
    Inspektor-Sheet, ab 1024 px die Budget-Seite mit der Kategorie im Panel.
- Leer: eine Karte als Knopf mit Pin-Symbol (gedämpft) und „Kategorien anpinnen, die du hier immer sehen willst“
  (13 px, `text-secondary`). Sie öffnet das Sheet.
- Werte aus dem laufenden Monat, den die Übersicht schon lädt (`Overview.Types.Month`). Kein zweiter Abruf.

**Sheet „Angepinnt“** (unter 1024 px `BottomSheet`, ab 1024 px `Modal`, wie „Buchung“ auf der Übersicht,
Styleguide §7.6 mit der Ausnahme aus ledger-s1weh)
- Titel „Angepinnt“, Untertitel „Erscheinen auf der Übersicht, in dieser Reihenfolge“, Schließen oben rechts.
- Suchfeld „Kategorie suchen…“, Fokus beim Öffnen in der Suche (§7.5).
- Ohne Suchbegriff und mit Pins: Abschnitt „Angepinnt (<n>)“ mit je einer Zeile: Pin-Symbol gefüllt im Akzent,
  Name, Pfeil hoch (eine Stelle nach oben) und Lösen (×). Beim ersten Eintrag bleibt der Platz des Pfeils frei
  (`orderArrow` wie auf der Seite Kategorien).
- Darunter alle aktiven Kategorien nach Gruppe (Gruppen und Reihenfolge wie im Budget, ADR 0088), je Zeile Häkchen,
  Name und Verfügbar in Mono (rot unter null). Antippen pinnt an oder löst. Neu angepinnte kommen ans Ende.
  Die Suche filtert diese Liste.
- Fuß: „Fertig“ (Primary, volle Breite) schließt.
- Am PC: Pfeile bewegen die Markierung in der Liste, Enter schaltet die markierte Zeile um, Esc schließt.
- Im Sheet kein Toast je Umschalten.

**Pin-Knopf im Kategorie-Detail**
- Im Kopf des Inspektors (Sheet und Panel) zwischen den Schrittpfeilen und Schließen ein Symbolknopf: Umriss in
  `text-secondary` mit `aria-label` „Auf der Übersicht anpinnen“, angepinnt gefüllt im Akzent mit „Von der Übersicht
  lösen“. Ein Klick schaltet um und zeigt den Toast „<Name> angepinnt“ oder „<Name> gelöst“.
- Die Schrittpfeile `˄ ˅` bleiben (BB-Stand; der Prototyp hat `‹ ›`, BB gilt).

**Zustand**
- Eine geordnete Liste von Kategorie-Ids in BBs Datenbank, gelesen und geschrieben über eigene Remoting-Aufrufe
  im Budgeting-API (lesen; ganze Liste setzen). Kein Ereignis.
- Eine angepinnte Kategorie, die in BB deaktiviert, in YNAB versteckt oder gelöscht ist, erscheint weder im Block
  noch im Abschnitt „Angepinnt (n)“. Die Id bleibt gespeichert; wird die Kategorie wieder aktiv, steht sie wieder
  an ihrem Platz.

## Acceptance criteria
- [ ] GIVEN keine Pins WHEN die Übersicht lädt THEN zeigt „Angepinnt“ die Leer-Karte und keinen Knopf „Bearbeiten“; Antippen öffnet das Sheet.
- [ ] GIVEN Lebensmittel, Restaurant, Kinderkram angepinnt in dieser Folge WHEN die Übersicht lädt THEN stehen drei Zeilen in dieser Folge mit Verfügbar, Balken und „<ausgegeben> von <Betrag> ausgegeben“ aus dem laufenden Monat.
- [ ] GIVEN eine angepinnte Kategorie mit Verfügbar unter null THEN sind Betrag und Balken rot; GIVEN eine mit Fehlbetrag zum Target (nicht pausiert) THEN steht „fehlt <Betrag>“ in Gelb vor dem Betrag.
- [ ] Die Rechnung von Anteil, „ausgegeben“ und „von“ ist eine reine Funktion mit Tests: Erstattung mindert, Aktivität 0 ergibt 0 %, Anteil über 100 % wird 100 %, „von“ höchstens null mit Ausgaben ergibt 100 %.
- [ ] GIVEN eine angepinnte Kategorie WHEN Roman die Zeile antippt THEN öffnet das Budget im laufenden Monat mit dieser Kategorie im Tab Geld, unter 1024 px im Sheet, ab 1024 px im Panel.
- [ ] GIVEN das Sheet „Angepinnt“ WHEN Roman eine Kategorie ankreuzt, eine nach oben schiebt und eine löst THEN zeigt die Übersicht nach „Fertig“ und nach Neuladen genau diese Folge.
- [ ] GIVEN ein Suchbegriff im Sheet THEN fehlt der Abschnitt „Angepinnt (n)“ und die Liste zeigt nur passende aktive Kategorien.
- [ ] GIVEN das Kategorie-Detail einer nicht angepinnten Kategorie WHEN Roman den Pin-Knopf drückt THEN ist sie angepinnt (gefüllt, Akzent, Label „Von der Übersicht lösen“), der Toast sagt „<Name> angepinnt“, und sie steht am Ende des Blocks auf der Übersicht. Erneut drücken löst sie mit „<Name> gelöst“.
- [ ] GIVEN eine angepinnte Kategorie wird deaktiviert THEN fehlt sie im Block und im Sheet; nach dem Reaktivieren steht sie wieder an ihrem Platz.
- [ ] Speichern und Lesen der Pins sind gegen In-Memory-SQLite getestet; ein Schreibfehler kommt im Client als Toast „Nicht gespeichert: <Grund>“ an (Ergebnis einer Aktion, kein Eingabefehler, §5), die Anzeige springt auf den vorigen Stand zurück.
- [ ] Validierung an der API-Grenze (`src/Server/Validation.fs`): doppelte Ids und unbekannte Kategorie-Ids werden mit konkreter Meldung abgelehnt; `src/Tests/RemotingEndpointNegativeTests.fs` hat die Zeilen für die neuen Aufrufe.
- [ ] Ladefehler der Pins: der Block zeigt „Angepinnte Kategorien konnten nicht geladen werden“ mit „Erneut versuchen“; Zu tun, Monat und Konten bleiben. Während des Ladens steht ein Skeleton in Zielform, kein Spinner.
- [ ] Pin-Symbol (Umriss und gefüllt) liegt als neues Icon in `src/Client/DesignSystem/Icons.fs` und steht auf `/styleguide`. Wird für die Block-Zeile eine neue DS-Variante nötig, steht sie ebenfalls dort.
- [ ] Am PC im Sheet: Fokus in der Suche, Pfeile bewegen die Markierung, Enter schaltet um, Esc schließt.
- [ ] Neue Pflichtfelder in Records aus `src/Shared/` sind in den Fixtures von `scripts/*.test.mjs` nachgezogen; der Layout-Sweep (`scripts/layout-invariants.mjs`) hat einen Zustand der Übersicht mit drei Pins.
- [ ] Browser-Test `[flow:kategorie-anpinnen]` (in `FLOWS` von `scripts/browser-usage.mjs`): am Handy aus dem Kategorie-Detail anpinnen, auf der Übersicht sehen, im Sheet lösen. Die übrigen Tests der Übersicht bleiben, die Seite wird nicht umgebaut.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten (Schrittpfeile `˄ ˅` statt `‹ ›`; Sheet am PC als `Modal`). Screenshots von BB bei 412 px und 1280 px neben den Prototyp-Bildern, Pfade im Outcome.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Block, Sheet und Pin-Knopf sind am Handy und am PC angesehen. [human-eye]

## Notes
Schnitt 3 von Etappe 3 (Fahrplan, Roman 2026-11-11). Styleguide §7.4 „Angepinnt“, §7.5, §7.6, §7.14.

Verfeinert 2026-11-11. Abgleich mit BB: BB hat heute keine Pins, im Inspektor-Kopf stehen Schrittpfeile und
Schließen. Keine heutige Funktion geht verloren.

Prototyp und Screenshots (Conductor gibt die Pfade an Worker, Verifier und `design-quality`):
- HTML: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/prototyp-v17.html`
- Übersicht mit Pins: `…/scratchpad/e3/p-uebersicht-angepinnt-handy-412.png`, `…/e3/p-uebersicht-angepinnt-pc-1280.png`
- Sheet: `…/e3/p-pins-sheet-handy-412.png`, `…/e3/p-pins-sheet-pc-1280.png`
- Kategorie-Detail mit Pin-Knopf: `…/e3/p-kategorie-detail-handy-412.png`, `…/e3/p-kategorie-detail-pc-1280.png`

Speicherort: neben `CategoryLayout` (ADR 0088) als eigener Zustand, Wahl der Tabelle beim Worker. Leseweg nach dem
Muster von `bbCategoryOrderWith`, Schreibweg nach `setCategoryOrderWith` (`src/Server/Budgeting/Categories.fs`).
