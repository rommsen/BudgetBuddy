---
id: design-system-m7z9q
title: Deutsches Datumsfeld mit Kalender-Knopf im DS, eingesetzt im Zeitraum-Filter des Kassenbuchs
status: backlog
type: feature
context: design-system
created: 2026-10-19
completed:
depends_on: [ledger-m23nz]
blocks: []
tags: [frontend, ruhige-listen, kassenbuch, datum]
related_adrs: [0049]
related_research: []
prior_art: [design-system-zahv9]
---

## Why
Befund K7 der Bestandsaufnahme vom 2026-10-19 (`docs/ux/design-system/audit-2026-10-19/report.md`,
Bild `pc-1440x900-ledger-filter-Zeitraum.png`): Das native `<input type="date">` im Zeitraum-Filter
zeigt das Format der Browsersprache ("mm/dd/yyyy"). `lang="de"` am Element ändert das in Chrome nicht.

Entscheidung Roman (2026-10-19): deutsches Textfeld plus Kalender-Knopf. Das Feld ist immer
deutsch, der native Kalender bleibt über den Knopf erreichbar.

Abgetrennt aus `ledger-m23nz`, das das kompakte Layout des Zeitraum-Filters liefert.

## What
- **DS-Baustein** `Input.germanDate` (oder ein kleines Modul `DateField.fs`, Wahl des Workers;
  Einordnung in `Client.fsproj` vor den Nutzern):
  - Textfeld "TT.MM.JJJJ" in `Input.Small`-Maßen, Platzhalter "TT.MM.JJJJ".
  - Daneben ein Kalender-Symbolknopf. Er öffnet ein verstecktes natives `<input type="date">`
    über `showPicker()`; die Auswahl schreibt das Datum deutsch ins Textfeld.
    Wo `showPicker()` fehlt oder wirft, fällt der Knopf auf `focus()`/`click()` des nativen Felds
    zurück, und das Textfeld bleibt voll bedienbar.
  - Wert nach außen bleibt ISO `yyyy-MM-dd` (wie heute, `Types.fs` ~237), damit Modell, State und
    Filter-Logik unverändert bleiben.
  - Ungültige Eingabe: das Feld zeigt den Zustand `Error` mit kurzem deutschem Hinweis
    ("Datum als TT.MM.JJJJ") und meldet keinen Wert nach außen.
- **Reine Funktionen** (DS oder `Shared`, testbar unter .NET und Fable gleich):
  - `parseGermanDate: string -> DateTime option`: akzeptiert "07.10.2026" und "7.10.2026",
    lehnt "31.02.2026", "2026-10-07", "07.10.26" und "" ab. Kein `DateTime.TryParse` mit Kultur.
  - `formatGermanDate: DateTime -> string` liefert "07.10.2026".
  - `isoToGerman` / `germanToIso` für die Brücke zum ISO-Wert.
- **Einsatz** im Zeitraum-Filter des Kassenbuchs (`dateDraftInput` in `Ledger/View.fs` ~921),
  ersetzt dort das native Feld. Schnellwahl-Knöpfe bleiben.
- **Styleguide:** `/styleguide` zeigt das Feld in der Karte "Auswahl" (leer, gefüllt, Fehler);
  `standards/frontend/styleguide.md` §2 "Auswahl" bekommt einen Satz zum Datumsfeld.

## Acceptance criteria
- [ ] `parseGermanDate` und `formatGermanDate` haben Expecto-Tests für alle oben genannten Eingaben, dazu Rundlauf `formatGermanDate >> parseGermanDate` für Monatsanfang, Monatsende, 29.02.2028 und Jahreswechsel.
- [ ] `germanToIso "07.10.2026"` = `Some "2026-10-07"`, `isoToGerman "2026-10-07"` = `"07.10.2026"`, ungültige Werte ergeben `None` bzw. den Eingabetext unverändert. Tests.
- [ ] Der neue Pfad enthält kein `DateTime.TryParse`, `DateTime.Parse` und kein `CultureInfo` (Quelltext-Zusicherung).
- [ ] `Ledger/View.fs` enthält im Zeitraum-Filter kein `prop.type' "date"` mehr außerhalb des DS-Bausteins (Quelltext-Zusicherung); der Filter nutzt den DS-Baustein.
- [ ] Das Textfeld setzt kein Inline-`fontSize` (`Input.usesInlineFontSize` bleibt für Small falsch; bestehender Test bleibt grün).
- [ ] Playwright am Zeitraum-Filter bei 1440x900: Eingabe "01.10.2026" und "15.10.2026", Übernehmen, die Trefferliste ist auf den Zeitraum gefiltert (Netzwerk-Request oder Chip-Text "01.10. bis 15.10." o. ä.). Ergebnis im Diary.
- [ ] Playwright: Klick auf den Kalender-Knopf wirft keinen Konsolenfehler (Chrome).
- [ ] Nachher-Bilder `docs/ux/kassenbuch/filterleiste-ruhig/{pc-1440x900,handy-390x844}-ledger-filter-Zeitraum-datumsfeld.png` und die Styleguide-Karte.
- [ ] `npm run guard:classes`, `npm run build`, `npm run test:scripts`, `dotnet build` und `dotnet test` grün.
- [ ] Am Handy (iOS Safari) öffnet der Kalender-Knopf die Datums-Walze, und das gewählte Datum steht deutsch im Feld. [human-eye]

## Notes
- **Risiko iOS:** `showPicker()` gibt es in Safari erst ab 16.4 und nur bei Nutzergeste. Das Fallback muss das Textfeld bedienbar lassen. Das Handy-Kriterium ist deshalb ein Blick von Roman.
- **Nicht in dieser Aufgabe:** das Datumsfeld in Quick Add (`QuickAddPage.fs` ~210, ebenfalls nativ). Sobald der Baustein steht, ist der Wechsel dort ein kleiner Folgeschritt; als eigene Aufgabe erfassen, falls gewünscht.
- Kein ADR: der Baustein folgt den bestehenden Input-Regeln, die Entscheidung steht in dieser Datei.
- Größe S bis M.
