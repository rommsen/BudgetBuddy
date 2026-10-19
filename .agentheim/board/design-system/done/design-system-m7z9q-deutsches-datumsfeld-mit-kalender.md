---
id: design-system-m7z9q
title: Deutsches Datumsfeld mit Kalender-Knopf im DS, eingesetzt im Zeitraum-Filter des Kassenbuchs
status: done
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
  - Schnittstelle: Eingang `DateTime option` (der heutige `DateDraft.From`/`To`, Ledger
    `Types.fs` ~294), Ausgang ein Eintrag `DateEntry = EmptyDate | ValidDate of DateTime |
    InvalidDate of string` (Name Wahl des Workers). Korrektur beim Refine 2026-10-19: der Entwurf
    ist kein ISO-String, sondern `DateTime option`.
  - Eigener Textzustand im Baustein (React-Hook, Vorbild `Input.fs` ~570). Ändert sich der
    Eingangswert von außen (Schnellwahl "Dieser Monat", "Zurücksetzen", Öffnen des Popovers),
    zieht der Text nach. Eigene Tippschritte ("07.1") überschreibt der Eingangswert nicht.
  - Ungültige Eingabe: das Feld zeigt den Zustand `Error` mit kurzem deutschem Hinweis
    ("Datum als TT.MM.JJJJ") und meldet `InvalidDate`.
- **Übernehmen gesperrt bei Fehler** (Entscheidung Roman, 2026-10-19): Der Kassenbuch-Entwurf
  merkt sich je Grenze, ob die Eingabe ungültig ist (z. B. `FromInvalid`/`ToInvalid` im Entwurf
  oder ein eigenes Feld im Modell, Wahl des Workers). Solange eine Grenze ungültig ist, ist
  "Übernehmen" deaktiviert, und `ApplyDateFilter` ändert den Filter nicht. `PickerParts.applyButton`
  (~86) hat heute keinen deaktivierten Zustand; der Worker ergänzt ihn im DS.
  Schnellwahl und "Zurücksetzen" heben den Fehlerzustand auf.
- **Reine Funktionen** (DS oder `Shared`, testbar unter .NET und Fable gleich):
  - `parseGermanDate: string -> DateTime option`: akzeptiert "07.10.2026" und "7.10.2026",
    lehnt "31.02.2026", "2026-10-07", "07.10.26" und "" ab. Kein `DateTime.TryParse` mit Kultur.
  - `formatGermanDate: DateTime -> string` liefert "07.10.2026".
  - `isoToGerman` / `germanToIso` für die Brücke zum versteckten nativen Feld (dessen Wert ist ISO).
  - `classifyDateText: string -> DateEntry` (leer, gültig, ungültig), auf `parseGermanDate` gebaut.
- **Einsatz** im Zeitraum-Filter des Kassenbuchs (`dateDraftInput` in `Ledger/View.fs` ~961),
  ersetzt dort das native Feld samt `DateTime.TryParse`. Schnellwahl-Knöpfe bleiben.
  Die heutigen Klassen `text-base md:text-sm` schützen am Handy vor dem iOS-Zoom (Schrift ≥ 16 px);
  das neue Feld muss am Handy ebenfalls mindestens 16 px Schrift haben.
- **Styleguide:** `/styleguide` zeigt das Feld in der Karte "Auswahl" (leer, gefüllt, Fehler);
  `standards/frontend/styleguide.md` §2 "Auswahl" bekommt einen Satz zum Datumsfeld.

## Acceptance criteria
- [ ] `parseGermanDate` und `formatGermanDate` haben Expecto-Tests für alle oben genannten Eingaben, dazu Rundlauf `formatGermanDate >> parseGermanDate` für Monatsanfang, Monatsende, 29.02.2028 und Jahreswechsel.
- [ ] `germanToIso "07.10.2026"` = `Some "2026-10-07"`, `isoToGerman "2026-10-07"` = `"07.10.2026"`, ungültige Werte ergeben `None` bzw. den Eingabetext unverändert. Tests.
- [ ] `classifyDateText` liefert `EmptyDate` für "" und Leerzeichen, `ValidDate` für "7.10.2026", `InvalidDate` für "31.02.2026". Tests.
- [ ] Ledger-State: `SetDateDraftFrom`/`To` mit `InvalidDate` setzt den Fehlerzustand; `ApplyDateFilter` lässt `Filters.DateRange` dann unverändert; `SetDateQuick` und `ResetDateDraft` heben den Fehler auf. Expecto-Tests am `update`.
- [ ] Die Ansicht des Zeitraum-Filters rendert "Übernehmen" deaktiviert, solange eine Grenze ungültig ist (reine Funktion oder Test am Button-Zustand; der neue deaktivierte Zustand von `PickerParts.applyButton` hat einen Test).
- [ ] Playwright: nach Klick auf "Dieser Monat" stehen in Von/Bis die deutschen Daten des Monats (z. B. "01.10.2026" und "31.10.2026"); nach Eingabe "31.02.2026" ist "Übernehmen" deaktiviert und der Hinweis "Datum als TT.MM.JJJJ" sichtbar.
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
- **Refine 2026-10-19:** Zeilenangaben am Stand nach `ledger-m23nz` geprüft (`dateDraftInput` ~961, Aufrufe ~998/999, `applyButton` ~1004). Das Kriterium "kein Inline-`fontSize`" und das 16-px-Kriterium am Handy schließen sich nicht aus: die Schrift kommt über Klassen (`text-base md:text-sm`), nicht über `style.fontSize`.
- **Fable:** `parseGermanDate` per Hand zerlegen (Split auf '.', Ziffernprüfung, `DateTime.DaysInMonth`), damit .NET und Fable gleich rechnen; kein `CultureInfo` (siehe `.claude/rules/build-verification.md`).
- Größe S bis M.

## Outcome

Deutsches Datumsfeld im Design-System und Einsatz im Zeitraum-Filter des Kassenbuchs.

- `src/Client/DesignSystem/DateField.fs` (neu, in `Client.fsproj` nach `Input.fs`): reine Funktionen `parseGermanDate` (per Hand zerlegt, 31.02., "2026-10-07", "07.10.26", "" werden abgelehnt), `formatGermanDate`, `isoToGerman`, `germanToIso`, `classifyDateText`, `syncedText` (Eingangswert zieht den Text nach, eigene Tippschritte bleiben stehen) und der React-Baustein `DateField.view` mit eigenem Textzustand, Kalender-Knopf (`showPicker()`, Rückfall `focus()`/`click()`), Fehlerzustand und Hinweis "Datum als TT.MM.JJJJ". `Icons.calendar` neu.
- `PickerParts.applyButtonWhen`, `applyButtonClassFor`, `applyButtonDisabledClass`: gesperrter Übernehmen-Knopf (gedämpft, `disabled`, ohne Hover/Druck).
- Ledger: Model-Felder `DateDraftFromInvalid`/`DateDraftToInvalid` direkt hinter `DateDraft`, `dateDraftHasError`, `SetDateDraftFrom/To` tragen jetzt `DateEntry`. `ApplyDateFilter` ändert bei Fehler nichts; `SetDateQuick`, `ResetDateDraft` und das Öffnen des Popovers heben den Fehler auf. `dateDraftInput` nutzt `DateField.view`, am PC stehen Von und Bis untereinander (`md:grid-cols-1`, in 320px Popoverbreite wurde "01.10.2026" sonst abgeschnitten).
- Styleguide: `/styleguide` Karte "Auswahl" zeigt leer, gefüllt, Fehler; `standards/frontend/styleguide.md` hat einen Absatz zum Datumsfeld.
- Befund: `.input-field` in `src/Client/styles.css` setzt `font-size: 14px` ohne Layer und schlägt damit `text-base` aus `Input.Small` am Handy (gemessen 14px statt 16px). Das Datumsfeld umgeht das lokal mit `text-base! md:text-sm!` (`DateField.textSizeClass`, gemessen 16px Handy, 14px PC). `Input.Small` selbst ist nicht angefasst, Folgeaufgabe unten gemeldet.
- Tests: 33 Expecto-Tests in `AuswahlTests.fs` (Parse, Format, Rundlauf, ISO-Brücke, classify, syncedText, Quelltext-Zusicherungen, gesperrter Knopf), 6 Reducer-Tests in `LedgerPageTests.fs`, 2 angepasste Quelltext-Tests in `LedgerViewTests.fs`. Playwright in `scripts/ledger-filter-bar.test.mjs`: Schnellwahl "Dieser Monat" zeigt deutsche Daten, "31.02.2026" sperrt Übernehmen und zeigt den Hinweis, Zurücksetzen hebt auf, Eingabe 01.10.2026 bis 15.10.2026 und Übernehmen sendet DateFrom 2026-10-01 und DateTo 2026-10-15 an `getLedgerEntriesPage` (Chip bleibt bei Handeingabe "Zeitraum", der Chip-Text kommt nur von Schnellwahlen; der Entwurf zeigt den Zeitraum beim Wiederöffnen deutsch), Schriftgröße 16px/14px, Kalender-Knopf ohne Konsolenfehler.
- Läufe: `dotnet build` 0 Warnungen; `dotnet test` 1321 bestanden, 7 übersprungen (Integration, .env), 0 Fehler; `npm run build` grün; `npm run guard:classes` grün; `npm run test:scripts` 124 von 124 bestanden.
- Bilder: `docs/ux/kassenbuch/filterleiste-ruhig/{pc-1440x900,handy-390x844}-ledger-filter-Zeitraum-datumsfeld.png` (PC mit Fehlerfall und gesperrtem Übernehmen, Handy mit Schnellwahl) und `pc-1440x900-styleguide-auswahl-datumsfeld.png`.
- Offen: das Kriterium "iOS Safari öffnet die Datums-Walze" [human-eye] braucht einen Blick von Roman.
