# Audit: Ruhige Listen (ADR 0049) im BudgetBuddy-Frontend

Stand: 2026-10-19, Branch `feature/kassenbuch`. Nur gelesen, keine Quelldatei geändert.
Grundlage: ADR 0049, `standards/frontend/styleguide.md` §2 „Ruhige Listen“, Referenz Styleguide 8a.

## Vorgehen
- Backend und Vite mit den Bausteinen aus `scripts/screenshot-route.mjs`, Server gegen eine Kopie der Entwicklungsdatenbank (danach gelöscht).
- Aufnahmen nur im sichtbaren Bereich, 1440x900 und 390x844.
- „Import prüfen“ mit 8 erfundenen Buchungen per Playwright-Route gemockt (`getCurrentSession`, `getTransactions`); Kategorien und Payees vom Backend.
- Beim Öffnen des Kassenbuchs lief ein Spiegel-Lauf gegen die Kopie (Toast in einigen PC-Bildern).
- Die Entwicklungsdatenbank enthält Test-Regeln („Test Rule“, „concurrent-17“).

## Befunde

### Kassenbuch
| # | Oberfläche | VP | Abweichung | Code | Ort |
|---|---|---|---|---|---|
| K1 | Filter Kategorie | PC | Namen 16px (Label von `Input.checkbox` ohne Größe), Umbruch im 320px-Popover | `Input.fs:230-272`, `CategoryMultiSelect.fs:39` | DS |
| K2 | Filter Kategorie | beide | `Money.available` neongrün/rot, semibold | `Money.fs:220-229`, `CategoryMultiSelect.fs:41` | DS |
| K3 | Filter Kategorie | PC | „Übernehmen (0)“ als `Button.primary` mit Glow, zweizeilig | `CategoryMultiSelect.fs:105`, `Button.fs:65-70,87` | DS |
| K4 | Filter Kategorie | beide | Suchfeld 48/44px, fest 16px | `Input.fs:31,66-81`, `CategoryMultiSelect.fs:57` | DS |
| K5 | Filter Kategorie | PC | Popover 320px, Handoff nannte 460px | `Ledger/View.fs:1047-1050` | Seite |
| K6 | alle Kategorie-Picker | beide | YNAB-interne Kategorien ganz oben | `CategoryMultiSelect.fs:46-50`, `BottomSheet.fs:223-241` | DS |
| K7 | Filter Zeitraum | beide | „Übernehmen“ mit Glow, Labels 16px, Datumsfelder im Browserformat | `Ledger/View.fs:923-969` | Seite |
| K8 | Filter Payee | beide | Zeilen 15/16px ohne `truncate`, „12116 Treffer“ ohne Tausenderpunkt | `Ledger/View.fs:991,997-1011` | Seite |
| K9 | Filter Memo, Betrag | beide | Felder 48/44px mit 16px, Labels 16px, kein Euro-Hinweis | `Ledger/View.fs:1021-1035`, `Input.fs:31` | DS |
| K10 | Summenzeile | beide | rot/grün in `Card.standard` (72px), beim Laden „0 Buchungen +0,00 €“ grün | `Ledger/View.fs:1158-1175`, `Money.fs:358-365` | Seite |
| K11 | Seitenkopf | Handy | Bug: Symbol-Knopf und „Jetzt spiegeln“ beide sichtbar (`hidden md:inline-flex` verliert gegen `inline-flex` in `Button.view`) | `Ledger/View.fs:1289-1303`, `Button.fs:87` | DS |
| K12 | Seitenkopf | PC | Untertitel 5px über „KONTEN“ | `PageHeader.fs:111` | DS |
| K13 | „Alle Konten“ | PC | Memo-Spalte etwa 40px | `Ledger/View.fs:787-795` | Seite |
| K14 | Historie | PC | Beschreibung 16px, sonst in Ordnung | `Ledger/View.fs:636-653` | gering |
| K15 | Tabelle, Handy-Liste, Konten, Datumsgruppen | beide | keine Abweichung | | gut |

### Gemeinsamer Kategorie-Picker (Import, Aufteilen, Quick Add, Styleguide)
| # | VP | Abweichung | Code | Ort |
|---|---|---|---|---|
| P1 | beide | `Money.available` grün/rot, bold, „grüne Wand“ | `BottomSheet.fs:395` | DS |
| P2 | beide | Zeilen 48px, Kopf doppelt („ALLE KATEGORIEN“ plus Gruppenname) | `styles.css:1723-1793`, `BottomSheet.fs:364-380` | DS |
| P3 | PC | Suchfeld mit doppeltem Teal-Ring, 16px | `styles.css:1760-1776` | DS |
| P4 | PC | zwei verschiedene Picker-Optiken (Sheet, Popover) | `BottomSheet.fs`, `CategoryMultiSelect.fs` | DS |

### Import prüfen (SyncFlow)
| # | VP | Abweichung | Code | Ort |
|---|---|---|---|---|
| S1 | beide | drei Teal-Akzente pro Zeile (Kante, Chip `.badge-ready`, gefüllte Checkbox) | `styles.css:1279-1289,1333-1352,1417-1421`, `TransactionRow.fs:43-58` | Seite |
| S2 | beide | englische Formate („3,113.30 €“, „10. OCTOBER 2026“), `ToString`/`CultureInfo` unter Fable | `TransactionRow.fs:76-93`, `TransactionList.fs:219,225` | Seite |
| S3 | beide | Datum doppelt in Zeile und Gruppenkopf | `TransactionRow.fs:16,334` | gering |
| S4 | PC | Karten-Zeilen, Breite 1280px | `View.fs:49-52` | Seite |
| S5 | beide | „Importieren“ grüner Verlauf mit Glow | `TransactionList.fs:100-110,295-305` | gering |
| S6 | beide | englischer Debug-Block „BudgetBuddy Duplicate Detection“ immer sichtbar, „Überspringen“ roter Chip | `TransactionRow.fs:100-200,498` | Seite |
| S7 | Handy | Aufteilen: Gesamt rot-fett, Rest grün mit Glow im rot umrandeten Kasten | `SplitSheet.fs:176,205` | Seite |

### Regeln
| # | VP | Abweichung | Code | Ort |
|---|---|---|---|---|
| R1 | beide | `PageHeader.gradientWithActions`, am Handy Aktualisieren-Knopf allein in einer Zeile | `Rules/View.fs:456-470,532-535` | Seite |
| R2 | PC | Breite 1280px, Karte pro Regel (64px) statt `Table.calmRow` | `Rules/View.fs:76-190,573`, `View.fs:49-52` | Seite |
| R3 | beide | gefüllte Teal-Checkbox in jeder Zeile, Signal umgekehrt | `Rules/View.fs:120-126` | Seite |
| R4 | Handy | roter Papierkorb dauerhaft, Kategorie gekürzt | `Rules/View.fs:157-190` | Seite |
| R5 | beide | `searchableSelect` 16px/48px, „Gruppe: Name“ als ein String, „Type to search…“, Liste im Modal abgeschnitten; „Please fill in: …“ | `Input.fs:705-752`, `Rules/View.fs:312-326`, `Form.fs:45,77,108` | DS |

### Übrige Seiten
| # | Befund | Code | Ort |
|---|---|---|---|
| E1 | Einstellungen: Werte in Neon-Teal Mono, auch „–“; Karte in Karte | `styles.css:1887-1892`, `Settings/View.fs:42-60,598` | gering |
| E2 | Quick Add: Formular in Ordnung, Picker siehe P1 bis P3 | `QuickAddPage.fs:115,256` | gut |
| E3 | Sync-Start und Status-Ansichten in Ordnung | `StatusViews.fs` | gut |
| E4 | Styleguide 8a korrekt; Picker-Demos folgen dem DS-Fix | `Styleguide/View.fs:822,1020` | DS |

## Erfasste Aufgaben (2026-10-19)
- `design-system-qz7b6` (todo): K11, doppelter Spiegel-Knopf
- `design-system-zahv9`: ruhige Bausteine für Auswahlen (K1-K4, K9, P1-P4, R5)
- `ledger-m23nz`: Kassenbuch-Filterleiste und Summenzeile (K5, K7, K8, K10, K12, K13)
- `categorization-wnxd6`: Regeln-Seite (R1-R4)
- `ynab-sync-aj5tv`: Import prüfen (S1-S7)
- `ledger-d218j`: interne YNAB-Kategorien (K6)

## Screenshots
In diesem Ordner, Namen `pc-1440x900-<name>.png` und `handy-390x844-<name>.png`:
ledger, ledger-allaccounts, ledger-filter-{Kategorie,Zeitraum,Payee,Memo,Betrag}, ledger-filtered, ledger-history, start, import-pruefen, import-pruefen-aufgeklappt, import-kategorie-picker, import-split-sheet (nur Handy), rules, rules-modal, rules-modal-category, settings, quickadd, quickadd-category, quickadd-payee, styleguide.
Nur aus dem Quelltext beurteilt: Aufteilen-Sheet am PC, Regel-Sheet aus Import prüfen, TAN- und Fertig-Ansicht, Popover „Ohne Kategorie“.
