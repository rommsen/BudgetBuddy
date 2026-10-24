# Design-Tafeln Kassenbuch (Archiv)

Sicherung aller sieben Ablauf-Tafeln aus dem Claude-Design-Projekt "Gelesen UI-Mockups"
(projectId `5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`), gezogen am 2026-10-24 per DesignSync.
Anlass: das eigenständige Claude Design (claude.ai/design) schließt am 14.12.2026; danach
sind die Tafeln dort nicht mehr erreichbar.

**Referenz, nicht Code.** Nachgebaut wird in F#/Fable/Elmish gegen
`src/Client/DesignSystem/*.fs`. Wo eine Tafel und eine spätere ADR auseinandergehen, gilt
die ADR (Beispiel: ADR 0053 nimmt der Kategorie-Zelle das eigene Klickziel aus Ablauf 4).

## Inhalt

| Datei | Inhalt |
|---|---|
| `Ablauf 1 - Durchsehen und pruefen.dc.html` bis `Ablauf 7 - Was nur BB kann.dc.html` | die Tafeln, unverändert |
| `Ablauf n - ….txt` | der Text jeder Tafel (Annahmen, Komponenten, Screens, Notizen), per Skript aus dem HTML gezogen, zum Durchsuchen mit grep |
| `support.js` | Laufzeit der Tafeln (dc-runtime), unverändert |
| `_ds/…/styles.css`, `_ds/…/_ds_bundle.js` | das gebundene Design-System-Bundle, unverändert |
| `_ds/…/fonts/` | die drei Schriften; byte-gleich mit `src/Client/public/fonts/` (per `cmp` geprüft), samt `LICENSE.md` |
| `projekt-CLAUDE.md` | die Projekt-Grundlage des Design-Projekts: feste Regeln, Farben, Maße, "Aus Ablauf n dazugekommen", offene Punkte. **Abschrift** aus der DesignSync-Antwort (die Datei kam inline zurück, nicht als Download); Reihenfolge der offenen Punkte wie im Original (15 steht hinter 20) |

Die Tafeln rendern offline: im Browser öffnen genügt (geprüft am Beispiel Ablauf 5, alle
drei Schriften geladen, keine Fehler).

## Stand der Abläufe (laut Projekt-Grundlage)

| Ablauf | Screens | Stand im Design-Projekt | Im Repo |
|---|---|---|---|
| 1 Durchsehen und prüfen | Handy 1.1 bis 1.8, PC 1.9 bis 1.12 | fertig, Übergabepaket | `../handoff-ablauf-1-durchsehen-und-pruefen.md` |
| 2 Buchungen finden | Handy 2.1 bis 2.8 und 2.15, PC 2.9 bis 2.14 und 2.16 | fertig, Übergabepaket | `../handoff-ablauf-2-buchungen-finden.md` |
| 3 Mehrfachauswahl mit Summe | Handy 3.1 bis 3.4, PC 3.5 bis 3.7 | zur Abnahme, kein Übergabepaket | `../handoff-ablauf-3-mehrfachauswahl-mit-summe.md` |
| 4 Kategorie und Split | PC 4.1 bis 4.8, Handy 4.9 bis 4.11 | zur Abnahme, kein Übergabepaket | `../handoff-ablauf-4-kategorie-und-split.md` (aus der Tafel abgeleitet) |
| 5 Flags | PC 5.1 bis 5.9, Handy 5.10 bis 5.12 | zur Abnahme, kein Übergabepaket | nur diese Tafel; Abgleich mit `ledger-xfjcp` und `ledger-y54gr` steht aus |
| 6 Konto wechseln | Handy 6.1 bis 6.6, PC 6.7 bis 6.10 | zur Abnahme, kein Übergabepaket | nur diese Tafel |
| 7 Was nur BB kann | Handy 7.1 bis 7.5, PC 7.6 bis 7.10 | zur Abnahme, kein Übergabepaket | `../handoff-ablauf-7-was-nur-bb-kann.md` |

Nicht gesichert: `Canvas.dc.html` (leere Projektvorlage), die Übergabe-Ordner
`design_handoff_kassenbuch_ablauf1/` und `…ablauf2/` (ihre READMEs liegen schon als Handoffs
im Repo; ob ihre Tafel-Kopien mit den Tafeln hier übereinstimmen, ist nicht geprüft) und `uploads/` (eine Zeichnung).
