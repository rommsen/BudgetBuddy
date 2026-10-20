---
id: ledger-mhe2s
title: "Prüf-Schaltfläche der PC-Zeile erst bei Hover oder Fokus und neutral grau: der grüne Haken neben \"zu prüfen\" liest sich wie \"erledigt\""
status: done
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, kassenbuch, pruefvermerk, statusspalte]
related_adrs: [0049, 0050]
related_research: []
prior_art: [ledger-r777c, ledger-qns4j, ledger-rjb8w]
---

## Why
In der Statusspalte der PC-Tabelle steht bei einer zu prüfenden Buchung das pinke Wort
"zu prüfen" und direkt daneben ein grüner Haken. Der Haken ist die Schaltfläche "als
geprüft markieren" (`reviewMarkButton`, ledger-r777c), wirkt neben dem Status aber wie
dessen Gegenteil: "zu prüfen, aber erledigt". Grün ist nach ADR 0049 ein Signal, kein
Aktionsschmuck. Roman: "so richtig Sinn macht es nicht, dass zu prüfen ein grünes
Checkmark hat" (2026-10-20).

## What
Entscheidung Roman 2026-10-20:
- Das Statuswort "zu prüfen" bleibt immer sichtbar.
- Die Schaltfläche (Haken für "Geprüft", Pfeil zurück für "Ungeprüft") ist am PC nur
  sichtbar, wenn die Zeile gehovert ist oder den Tastaturfokus trägt (ledger-rjb8w), oder
  die Schaltfläche selbst fokussiert ist. Sonst nimmt sie ihren Platz ein, ist aber
  unsichtbar (kein Springen der Spalte).
- Beide Symbole sind neutral (`text-secondary`, bei Hover `text-primary`), nicht Teal/Grün.
- Ein Tooltip (`title`) nennt die Aktion: "Als geprüft markieren" bzw. "Als ungeprüft
  markieren".
- Am Handy gibt es kein Hover: dort bleibt die Schaltfläche sichtbar, wird aber ebenfalls
  neutral. Der Wisch nach rechts bleibt die Hauptgeste.

## Acceptance criteria
- [ ] Am PC ist die Prüf-Schaltfläche einer Zeile ohne Hover und ohne Fokus unsichtbar
      (`opacity-0` o. ä.) und behält ihre Breite; mit Hover, Zeilenfokus oder Fokus auf der
      Schaltfläche ist sie sichtbar.
- [ ] Die Schaltfläche ist per Tastatur erreichbar und wird beim Fokussieren sichtbar
      (`focus-visible`); Taste G funktioniert unverändert.
- [ ] Haken und Rückgängig-Pfeil nutzen eine neutrale Farbe, nicht `NeonTeal`; der Test
      oder die Klassenprüfung zeigt das.
- [ ] Die Schaltfläche trägt `title` und den vorgelesenen Text mit der Aktion.
- [ ] Am Handy bleibt die Schaltfläche sichtbar (44px Trefferfläche), in neutraler Farbe.
- [ ] Neben "zu prüfen" liest sich die Zeile nicht mehr wie "erledigt". [human-eye]

## Notes
- Die Statusspalte ist 128px breit (`statusColumnWidth`); die Breite darf durch die
  Änderung nicht wachsen (ledger-qns4j hat sie mühsam eingehalten).
- Muster für Hover-Sichtbarkeit: Tailwind `group` auf der Zeile, `group-hover:` und
  `group-focus-within:` auf der Schaltfläche. Prüfen, ob `Table.RowProps` dafür eine
  Klasse an der Zeile erlaubt.

## Merge-conflict note (iteration 1)

- Geschwister: ledger-qh9qx (Spalten stufenweise ausblenden) und ledger-r72re (Sortierung im Lesepfad), beide auf main integriert.
- Neue Basis: 0b535e3 (main), danach 85bb351 (Batch-Start ledger-rfq5p).
- Auflösungsliste: `src/Tests/Tests.fsproj` (beide `<Compile>`-Zeilen behalten: `LedgerReviewButtonVisibilityTests.fs` und `LedgerVisibleColumnsTests.fs`).
- Zusätzlich aus dem Verifier-Bericht (kein FAIL-Grund): der bestehende `///`-Doc-Block von `reviewMarkButton` steht jetzt über `reviewMarkTooltip`; zurück vor `reviewMarkButton` setzen.

## Outcome

Die Prüf-Schaltfläche (`reviewMarkButton`, `src/Client/Components/Ledger/View.fs`) bekommt ihre Klassen aus der neuen reinen Funktion `reviewMarkButtonClassName hoverOnly rowFocused`. PC-Zeile (`hoverOnly = true`): `opacity-0` mit `group-hover:`, `group-focus-within:` und `focus-visible:opacity-100`; die Zeile des Tastaturcursors (`isFocused`, ledger-rjb8w) zeigt sie dauerhaft. Handy-Zeile (`hoverOnly = false`): immer sichtbar, 44px (`h-11 w-11`) wie zuvor. Haken und Rückgängig-Pfeil nutzen `Icons.Default` (`text-text-secondary`, Hover `text-text-primary`) statt NeonTeal. `title` kommt aus `reviewMarkTooltip` ("Als geprüft markieren" / "Als ungeprüft markieren"), der vorgelesene Text (`sr-only` Label) bleibt. Die PC-Zeile trägt `group` (Zeilenklasse in `entryRow`, `Table.RowProps` unverändert, `selectableRowClassName` unverändert). Merge-Auflösung: Konfliktmarker in `src/Tests/Tests.fsproj` entfernt (beide Testdateien `LedgerReviewButtonVisibilityTests.fs` und `LedgerVisibleColumnsTests.fs` bleiben), der Doc-Block von `reviewMarkButton` steht wieder direkt davor. Die Spaltenbreite wächst nicht (`opacity` statt `hidden`).

Tests: `src/Tests/LedgerReviewButtonVisibilityTests.fs` (8 Tests: Klassen für PC/Handy/Zeilenfokus, kein Teal, Tooltip, Quelltext-Zusicherungen Icons.Default und `group`). Neuer Playwright-Wächter `scripts/ledger-review-button-visibility.test.mjs` misst am echten Element: ohne Hover opacity 0 und Breite >= 27px, bei Zeilen-Hover opacity 1, neutrale Farbe, title; Tastaturfokus auf der Schaltfläche macht sie sichtbar. Der Wächter hält die Anfrage `mirrorYnabBudget` offen, weil der ohne Backend scheiternde Spiegel-Lauf die Liste ständig neu aufbaut. Bestehender Wächter `scripts/ledger-status-column.test.mjs` angepasst: die Suche nach dem Markiert-Symbol schließt jetzt `button` aus, da die Schaltfläche nun ein `title` trägt.

Zahlen: `dotnet build` 0 Warnungen, 0 Fehler. `dotnet test` 1559 bestanden nach dem Merge, 0 Fehler, 7 übersprungen. `npm run build` ok, `npm run guard:classes` ok, `npm run test:scripts` 142 von 142 (mit `--test-concurrency=1`) bestanden. Das Kriterium "liest sich nicht mehr wie erledigt" [human-eye] ist nicht maschinell prüfbar und bleibt für Romans Blick.

Hinweis für den Conductor: qh9qx ändert ebenfalls View.fs. Meine Änderungen liegen an `reviewMarkButton` (plus zwei neue öffentliche Funktionen davor), an den zwei Aufrufstellen und an der `ClassName`-Zeile in `entryRow`.
