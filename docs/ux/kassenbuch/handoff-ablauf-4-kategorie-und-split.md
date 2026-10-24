# Handoff Ablauf 4: Kategorie ändern, Split festlegen oder ändern

> **Herkunft:** Tafel `Ablauf 4 - Kategorie und Split.dc.html` im Claude-Design-Projekt
> "Gelesen UI-Mockups" (projectId `5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`), Screens PC 4.1
> bis 4.8 und Handy 4.9 bis 4.11, gelesen am 2026-10-24. Dazu der Abschnitt "Aus Ablauf 4
> dazugekommen" der Projekt-`CLAUDE.md`. Ein Übergabepaket (`design_handoff_…/README.md`)
> gibt es für diesen Ablauf im Design-Projekt nicht; die Tafel steht dort noch auf "bei
> Roman zur Abnahme". Dieses Dokument ist aus der Tafel abgeleitet.
>
> **Referenz, nicht Code.** Nachzubauen in F#/Fable/Elmish mit Tailwind 4 gegen
> `src/Client/DesignSystem/*.fs` und die Tokens aus `src/Client/styles.css`. Brief:
> `docs/ux/kassenbuch/briefs.md`, Abschnitt "Ablauf 4".
>
> **Vorrang:** Wo die Tafel und eine spätere ADR auseinandergehen, gilt die ADR. Die
> Abweichungen stehen unten unter "Abgleich mit dem Repo-Stand".

## Ziel und Ausgangszustand

Roman korrigiert die Kategorie einer Buchung und teilt sie auf. PC ist das Hauptgerät, das
Handy der Nachweis, dass beides im Detail-Sheet geht. Buchung Bauhaus -81,90 €, Girokonto
Comdirect, Chip "Zu prüfen (4)" aktiv, Kategorie "Alltag: Haushalt", Memo "Werkzeug + Lampe".
Ziel-Split: 23,40 € Möbel ("Lampe") und 58,50 € Haushalt ("Werkzeug").

## Feste Regeln aus der Tafel

- Kategoriewechsel prüft nie mit und sortiert nie um: die Zeile bleibt an ihrem Platz und
  bleibt "Zu prüfen".
- Split-Bearbeitung nur im Detail (Panel am PC, Sheet am Handy), nie in der Tabelle.
- Rücknahme über den Toast ("Kategorie geändert: Möbel · Rückgängig", "Aufgeteilt in 2
  Zeilen · Rückgängig"). Eine Aktion, ein Toast, eine Rücknahme.
- Genau eine primäre Aktion (Orange) je Screen; im Editor ist das "Speichern".

## Screens

### PC (1440 × 900)

| Screen | Inhalt |
|---|---|
| 4.1 | Ausgangszustand. Bauhaus-Zeile ist die Tastatur-Fokuszeile (Teal-Rahmen). Kein Orange. |
| 4.2 | Kategorienauswahl als Popover unter der Kategorie-Zelle (Popover-Muster aus Ablauf 2). Suchfeld "Kategorie suchen…", Abschnitt **VORGESCHLAGEN** (Vorschlag zum Payee: "Wohnen: Möbel", "Wohnen: Nebenkosten"), dann **ALLE KATEGORIEN** nach Gruppe, "Versteckte Kategorien (1)" eingeklappt. Je Kategorie rechts der Available-Betrag (Green positiv, Red negativ, tertiär bei 0,00 €). Aktuelle Kategorie mit Haken. Fußzeile "Esc schließt ohne zu ändern · Klick wählt sofort" (Klick-Commit, kein "Übernehmen"). |
| 4.3 | "Möbel" gewählt: Zelle zeigt "Wohnen: Möbel", Zeile bleibt oben und "Zu prüfen". An der Stelle des Popovers kurz die Karte **BUDGET NACH DEM WECHSEL**: "Alltag: Haushalt ~~412,10 €~~ → 494,00 €", "Wohnen: Möbel ~~120,00 €~~ → 38,10 €". Toast "Kategorie geändert: Möbel · Rückgängig". |
| 4.4 | Seitenpanel rechts (420 px). Kopf Betrag, Payee, "Girokonto Comdirect · 12.09.2026". Hinweisleiste "Änderung noch nicht in YNAB · Rückschreiben kommt mit v2". Felder: Kategorie "Wohnen: Möbel" (mit Chevron), **Split "Kein Split" mit Knopf "Aufteilen"**, Memo, "Lena fragen", Geprüft-Schalter, HERKUNFT, HISTORIE ("Kategorie geändert: Haushalt → Möbel · Roman in BB"). Bei offenem Panel weicht die Memo-Spalte, die Chip-Leiste scrollt waagerecht. Fußhinweis "↑ ↓ bewegen · Leertaste auswählen · Esc Panel schließen". |
| 4.5 | Split-Editor im Panel, Zeile 1 (Möbel, 23,40 €, Memo "Lampe"). Rest 58,50 € in Pink, "Speichern" inaktiv. |
| 4.6 | Zeile 2 über "Zeile hinzufügen", Kategorie Haushalt, "Rest" füllt 58,50 €. Rest 0,00 € in Green, "Speichern" aktiv, "Zeile hinzufügen" ausgegraut. |
| 4.7 | Gespeichert. Tabellenzeile zeigt Badge **"Aufgeteilt (2)"** statt einer Kategorie. Panel: Kategorie "Aufgeteilt (2)", Abschnitt SPLIT mit Knopf **"Ändern"** und den Zeilen "Wohnen: Möbel · Lampe · -23,40 €", "Alltag: Haushalt · Werkzeug · -58,50 €". Historie "Aufgeteilt in Möbel 23,40 €, Haushalt 58,50 € · Roman in BB". Toast "Aufgeteilt in 2 Zeilen · Rückgängig". Buchung bleibt ungeprüft. |
| 4.8 | Rückweg über "Ändern": Zeile Möbel entfernt, auf der verbleibenden Zeile "Rest" → 81,90 €. Speichern macht daraus wieder eine einzelne Kategorie "Alltag: Haushalt", das Badge verschwindet, die Historie bekommt eine Zeile. |

### Handy (390 × 844)

| Screen | Inhalt |
|---|---|
| 4.9 | Detail als BottomSheet (Layer 1). Kategorie kurz ("Haushalt"), Split "Kein Split" mit "Aufteilen", beide Trefferflächen 44 px. Herkunft und Historie unten. |
| 4.10 | Kategorienauswahl als zweites Sheet über dem Detail (Layer 2, Rand von Layer 1 oben sichtbar), Titel "Kategorie wählen", Unterzeile "Bauhaus · -81,90 €". Gleiche Reihenfolge wie am PC (Vorschläge, Gruppen, versteckte eingeklappt), gleiche Available-Beträge. Ein Tipp wählt und schließt. Sheet 630 px hoch. |
| 4.11 | Split-Editor im selben Sheet, Titel "Aufteilen", Unterzeile "Bauhaus · 12.09.2026". Zeilen untereinander, "Rest" je Zeile, "Speichern" klebt unten (einzige primäre Aktion). Sheet 740 px hoch, damit beide Zeilen über der Speichern-Leiste enden. Am Handy kein "Abbrechen"-Knopf, Rückweg über das Sheet. |

## Split-Editor im Detail (Maße aus 4.6)

- Abschnittskopf `AUFTEILEN`: JetBrains Mono 600 10 px, Laufweite .16em, `#4a4a68`.
- Kopfzeilen "Gesamtbetrag" und "Rest": Label Inter 500 13 px `#7a7a98`, Wert JetBrains Mono
  600 14 px tabular-nums. Gesamtbetrag Red `#ff3b5c`; Rest Pink `#ff1f8f` bei ≠ 0, Green
  `#00ff88` bei 0,00 €.
- Je Zeile eine Karte: Padding 12 px, Radius 10 px, Fläche `#16162e`, Rand `#2a2a4a`, Abstand
  10 px innen, 12 px zwischen den Karten.
  - Kopf: "ZEILE n" (Overline wie oben) und rechts ein Entfernen-Knopf 26 × 26 px, Radius
    8 px, Fläche `#1a1a36`, Rand `#2a2a4a`, X-Icon 12 px `#7a7a98`.
  - Kategorie-Feld: 40 px hoch, Radius 10 px, Fläche `#1a1a36`, Rand `#2a2a4a`, Text Inter
    600 13 px in Purple `#a855f7`, Chevron rechts `#4a4a68`. Öffnet die Kategorienauswahl
    (PC: Popover am Feld; Handy: Layer 2).
  - Betrag: Feld 40 px, rechtsbündig, JetBrains Mono 600 14 px `#e8e8f0`, **positiv
    eingegeben**, das Vorzeichen kommt vom Gesamtbetrag (Projekt-Notiz 7). Fokus-Rand Teal
    `rgba(0,212,170,.45)`. Daneben Knopf "Rest" 74 × 40 px, Rand `rgba(0,212,170,.4)`, Text
    Teal Inter 600 12 px; nach Benutzung Fläche `rgba(0,212,170,.12)`.
  - Memo: Feld 40 px, Inter 400 13 px.
- "Zeile hinzufügen": 44 px, Radius 10 px, gestrichelter Rand `#2a2a4a`, Inter 600 13 px;
  ausgegraut (`#4a4a68`), solange der Rest 0,00 € ist.
- Fußleiste (PC): Rand oben `#1c1c38`, Fläche `#111128`, Padding 14/18/18 px. "Abbrechen"
  120 px breit, 44 px, sekundär; "Speichern" füllt den Rest, Orange-Verlauf
  `#ff6b2c → #e55a1f`, Glow `0 0 20px rgba(255,107,44,.5)`, inaktiv solange Rest ≠ 0.

## Abgleich mit dem Repo-Stand (2026-10-24)

1. **Klick auf die Kategorie-Zelle (4.1 bis 4.3) gilt nicht mehr.** ADR 0053 nimmt den Zellen
   die eigenen Klickziele; ein Klick auf die Zeile öffnet das Detail, der Picker öffnet aus
   dem Pfeil an der Kategorie im Detail (`ledger-sfab0`) und aus "Kategorie setzen" in der
   Leiste (`ledger-w29y6`). Das Popover-Muster selbst (Suche, Gruppen, Available, Klick-Commit,
   Esc) bleibt gültig, angesetzt am Feld im Panel.
2. **Hinweisleiste "Änderung noch nicht in YNAB · Rückschreiben kommt mit v2" (4.4 bis 4.8)
   gilt nicht mehr.** Seit ADR 0054/0055 schreibt BB die Kategorie nach YNAB; die Leiste
   erscheint nur bei einem gescheiterten Push, mit Grund und "Erneut senden" (Muster
   Prüfvermerk, ADR 0050).
3. **Rücknahme über den Toast.** Die Tafel (und die feste Projektregel) sieht "Rückgängig" im
   Toast vor, für die Kategorie wie für den Split. `ledger-sfab0` sagte bisher "Kein
   Rückgängig in dieser Scheibe". **Entschieden (Roman, 2026-10-24):** Rückgängig im Toast für
   Kategorie und Split; es setzt den alten Stand über denselben Schreibweg zurück (zweites
   `EntryRecategorized`, zweiter Push, Historie zeigt beide). Sammel-Variante: eine Rücknahme
   für alle (`ledger-sfab0`, `ledger-w29y6`, `ledger-pafkp`).
4. **Budget-Hinweis alt → neu.** Die Tafel zeigt ihn als eigene Karte an der Stelle des
   Popovers, getrennt vom Toast. `ledger-sfab0`/`ledger-kwwxz` legten ihn in den Toast.
   **Entschieden (Roman, 2026-10-24):** eigene Karte "Budget nach dem Wechsel" wie Tafel 4.3,
   kurz sichtbar am Picker bzw. im Panel unter dem Kategorie-Feld, am Handy unter dem
   Kategorie-Feld im Detail-Sheet. Der Toast trägt nur "Kategorie geändert: <Name> · Rückgängig"
   (`design-system-gffps` baut die Karte, `ledger-kwwxz` liefert die Zeilen, `ledger-sfab0`
   zeigt sie).
5. **VORGESCHLAGEN gegen "Zuletzt verwendet".** Die Tafel zeigt oben Vorschläge zum Payee
   (Bauhaus → Möbel). `design-system-gffps` sieht "Zuletzt verwendet" vor. Das sind zwei
   verschiedene Dinge (Vorschlag aus Payee-Historie oder Regeln gegen eigene letzte
   Wahl). **Entschieden (Roman, 2026-10-24):** oberer Abschnitt "Vorgeschlagen" zum Payee:
   Kategorien, die der Payee im Kassenbuch bisher hatte, höchstens drei, häufigste zuerst, ohne
   die aktuelle Kategorie. "Zuletzt verwendet" entfällt. Der Baustein rendert nur die
   übergebene Liste (`design-system-gffps`); die Berechnung ist eine Server-Abfrage über die
   Projektion (`ledger-sfab0`).
6. **"Aufteilen" bei einer unaufgeteilten Buchung** ist auf der Tafel fester Bestandteil des
   Detail-Felds Split ("Kein Split · Aufteilen"); `ledger-pafkp` nannte es "optional".
   **Entschieden (Roman, 2026-10-24):** fester Bestandteil, nicht optional. Split-Beträge im
   Editor positiv, das Vorzeichen kommt vom Gesamtbetrag.
7. **Available-Beträge** stammen auf der Tafel "aus dem Spiegel"; gebaut kommen sie live über
   `getCategories` (`ledger-kwwxz`, ADR 0055). Für die Darstellung ohne Belang.
8. **Panelbreite 420 px** und das Weichen der Memo-Spalte sind mit `ledger-q1hw2` schon so
   gebaut (`Detail.panelWidthPx`).

## Offene Punkte aus dem Design-Projekt, die diesen Ablauf betreffen

- Notiz 6: Available-Beträge auf der Tafel sind erfunden.
- Notiz 7: Split-Zeilen im Editor positiv, in der Übersicht negativ.
- Notiz 10: Zeile bleibt bei Filter-Nichttreffer ausgegraut an ihrem Platz (aus Ablauf 5);
  `ledger-sfab0` löste denselben Fall mit der Leiste "Neu laden" (`OrderStale`).
  **Entschieden (Roman, 2026-10-24):** fällt eine Buchung nach der Änderung aus dem aktiven
  Filter, bleibt sie ausgegraut an ihrem Platz ("passt nicht mehr zum Filter"), Trefferzahl und
  Summe gehen sofort mit (`LocallyRemoved` +1, ADR 0055 Nachtrag). `OrderStale` und die Leiste
  "Neu laden" bleiben nur für den Fall, dass sich der Schlüssel der aktiven Sortierung ändert.
