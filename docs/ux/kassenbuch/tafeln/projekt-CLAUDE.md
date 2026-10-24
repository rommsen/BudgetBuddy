# BudgetBuddy Kassenbuch — Projekt-Grundlage

Gilt für alle Chats in diesem Projekt. Roman liefert je Chat einen **Design Brief**
(„Ablauf n") mit Ziel, Gerät, Ausgangszustand und Schrittfolge. Diese Datei hält
fest, was für **alle** Abläufe gilt, damit ein Brief nicht alles wiederholen muss.

Quelle: Konzept-Skizze, mit Roman abgestimmt am 2026-09-11.

## Was BudgetBuddy ist

YNAB-Companion: spiegelt Comdirect-Umsätze nach YNAB. Das **Kassenbuch** ist die
Buchungsliste. Roman ist der Nutzer, Lena seine Partnerin (Rückfragen gehen an sie).

## Sprache und Ton

Deutsch, Du-Form, direkt und knapp, ohne Füllwörter. Buttons als Verb oder
Verb+Objekt: „Speichern", „Sync starten", „Geprüft", „Kategorie ändern",
„Auswahl aufheben". Fehlertexte benennen, was passiert ist. Leerzustände sagen, was
als Nächstes zu tun ist. Beträge sprechen für sich über Vorzeichen und Farbe,
Format `-48,12 €` (Komma als Dezimaltrenner, Punkt als Tausendertrenner, Leerzeichen
vor dem €).

## Geräte

- **Handy 390 × 844** — Hauptfall Durchsehen und Prüfen
- **PC 1440 × 900** — Schwerpunkt Nachbearbeiten

Beide gleichwertig und beide sehr gut bedienbar. Filter und Summen gehören auf beide.
Trefferflächen am Handy nie unter 44px.

## Aufbau, der auf beiden Geräten gilt

1. **Kontostände** — Handy: waagerecht scrollbare Karten-Leiste; PC: linke Spalte,
   „Alle Konten" zuoberst
2. **Sticky Filterleiste** aus Chips; **erster Chip ist immer „Zu prüfen (n)"**
3. **Summenzeile** — nur wenn ein Filter aktiv ist: Trefferzahl und Gesamtbetrag über
   den **gesamten** Treffer, nicht über den geladenen Teil
4. **Liste**

**Einstieg:** das zuletzt gewählte Konto (Entscheidung Roman). Gesamtsicht über den
Konto-Wechsler.

**Konto ist ein Filter.** Konto-Wechsel lässt andere Filter und eine laufende Auswahl
stehen. In der Gesamtsicht trägt jede Zeile ein Konto-Kürzel (Handy) bzw. es gibt eine
Konto-Spalte (PC).

**Chips:** Zu prüfen · Kategorie · Zeitraum · Payee · Betrag · Memo · Flag ·
Gelöschte zeigen. Kategorie öffnet die Kategorienauswahl mit Mehrfachauswahl,
versteckte Kategorien bleiben eingeklappt.

**Filtern (Entscheidungen aus Ablauf 2):** Kategorienauswahl am Handy als BottomSheet,
am PC als **Popover unter seinem Chip** (Liste bleibt sichtbar, Esc schließt ohne zu
filtern) — ein Popover-Muster für Kategorie, Zeitraum und Payee. Mehrfachauswahl wirkt
erst auf „Übernehmen (n)" (Click-Commit), Einzelauswahl (Payee) filtert sofort und hat
kein „Übernehmen". Aktiver Chip trägt Zähler oder Namen plus „×" („Kategorie: 2",
„12 Monate", „Café Lotte"); **am Handy wandern aktive Chips nach vorn**, am PC bleibt
die Reihenfolge fest. „Alle Filter löschen" sitzt am Ende der Leiste. Zeitraum:
Schnellwahl (Dieser Monat · Letzter Monat · Dieses Jahr · Letzte 12 Monate) schreibt
Von/Bis mit. Payee-Vorschläge kommen mit Anzahl **aus dem aktuellen Treffer**, nicht
aus allen Buchungen. Konto-Kürzel am Handy: `ING`, `GIRO`, `BAR`, `ETF`.
„Noch nicht in YNAB" ist ein Orange-Badge wie „Keine Kategorie".

**Liste Handy:** Datum als Gruppenkopf; je Zeile Payee, Kategorie, Betrag; darunter
klein Memo und **nur abweichende** Kennzeichen („noch nicht in YNAB", „Rückfrage Lena",
„in YNAB gelöscht"). Wisch nach rechts = geprüft, **ein einziger Wisch**
(Entscheidung Roman). Wisch nach links bleibt unbelegt, damit die Geste eindeutig
bleibt. Tipp auf die Zeile öffnet das Detail als BottomSheet. Langes Drücken startet
den Auswahl-Modus.

**Liste PC:** Tabelle mit Sticky-Kopf: Auswahl-Kästchen, Datum, Konto (nur
Gesamtsicht), Payee, Kategorie, Memo, Betrag, Status. Klick auf die Zeile öffnet ein
**Seitenpanel rechts** (Entscheidung Roman), die Liste bleibt sichtbar. Klick auf die
Kategorie-Zelle öffnet den Kategorie-Picker direkt. Tastatur: `↑ ↓` bewegen,
`Leertaste` wählt aus, `G` markiert geprüft, `Enter` öffnet das Detail.

**Auswahl-Modus:** Auswahl-Leiste unten mit Anzahl, Summe der Auswahl und den
Aktionen „Geprüft", „Kategorie ändern", „Flag", „Auswahl aufheben". Solange eine
Auswahl läuft, zeigt die Summenzeile die Auswahl statt des Filters. **Gilt auf beiden
Geräten** (Entscheidung Roman): am Handy startet langes Drücken den Modus, Kästchen
stehen links in jeder Zeile, die Leiste ersetzt die Tab-Bar und legt die Aktionen auf
zwei Zeilen (je 44px); am PC wählen Kästchen, Leertaste oder Shift-Klick, die Leiste
ersetzt den Tastatur-Hinweis. Screens: 2.15 (Handy) und 2.16 (PC).

**Detail** (Sheet am Handy, Seitenpanel am PC): Kopf mit Betrag, Payee, Konto, Datum.
Felder Kategorie (Picker), Split als Zeilenliste mit Restbetrag (**Split-Bearbeitung
nur hier**, Entscheidung Roman), Memo, Flag (Lena, temporär), Geprüft-Schalter.
Darunter **Herkunft und Historie als Zeitleiste** — das ist das, was nur BB zeigt.
„Noch nicht in YNAB" erscheint als Hinweis **mit Grund**.

**Gelöschte Buchungen** sind ausgeblendet (`ledger-p3n8k`) und kommen über den Chip
„Gelöschte zeigen" zurück, ausgegraut mit Kennzeichen.

**Große Listen** (rund 14.000 Buchungen): seitenweise vom Server, 100 je Seite,
Nachladen beim Scrollen. Kontostände kommen aus dem Zustand, **nie** aus der geladenen
Liste. Filterwechsel springt nach oben, Chips bleiben sichtbar. Payee-, Memo- und
Betragssuche mit kurzer Verzögerung.

## Feste Regeln, die in jedem Ablauf gelten

- **Prüfen sortiert nie um.** Ohne Filter bleiben geprüfte Zeilen an ihrem Platz.
- **Prüfen hängt nicht an der Kategorie.** Eine Buchung ohne Kategorie lässt sich prüfen.
- **Kategoriewechsel prüft nie mit** und sortiert nie um.
- **Rücknahme läuft über den Toast**, nicht über eine zweite Geste. Eine
  Mehrfachaktion ergibt **einen** Toast und **eine** Rücknahme.
- **Genau eine primäre Aktion je Screen**, in Orange.

## Erfundene Daten — nur diese verwenden

**Konten**

| Konto | Art | Stand |
|---|---|---|
| Girokonto Comdirect | Bank | 2.418,37 € |
| ING Extra | Bank | 6.902,10 € |
| Bar | Bar | 63,50 € |
| Geldmarkt-ETF | Wertpapierkonto mit Budget-Geld | 12.004,88 € |

Summe „Alle Konten": **21.388,85 €**

Kita-Beitrag Stadt liegt bei **125,00 € im Monat** (festgelegt in Ablauf 3, damit 34
Kinder-Buchungen auf -2.140,00 € und die obersten zwölf auf -780,00 € aufgehen).

**Payees:** REWE Mitte, Bäckerei Krause, Stadtwerke Musterstadt, Deutsche Bahn,
Amazon Marketplace, Apotheke am Markt, Kita-Beitrag Stadt, Netflix, Bauhaus, Café Lotte.

**Kategorien (Gruppen):** Alltag (Lebensmittel, Haushalt, Restaurants) · Wohnen
(Nebenkosten, Möbel) · Mobilität (Bahn, Auto) · Familie (Kinder, Geschenke) · Abos
(Streaming) · Gesundheit (Apotheke). Versteckt: „Umzug 2023".

Am PC steht die Kategorie voll qualifiziert („Alltag: Lebensmittel"), am Handy kurz
(„Lebensmittel").

## Design-System

Gebunden unter `_ds/budgetbuddy-design-system-3a764a23-efa6-4d3a-8a66-e0325666d0c3/`.
Das Bundle liefert **Tokens und Klassen, keine mountbaren Komponenten** — Screens
werden aus den Tokens nachgebaut, Werte 1:1.

**Pflicht-Komponenten** (so benennen, damit die Übergabe die vorhandenen Bausteine
trifft): Navigation (Desktop und Mobile), Card, Money, Badge, Table, BottomSheet,
Kategorienauswahl (`components-categorypicker`), Swipe, Button, Stats, Input, Toast,
Icons, PageHeader, Modal, Primitives, Loading, ErrorDisplay, Form.

**Fehlt im Design-System** und wird aus vorhandenen Bausteinen zusammengesetzt — im
Screen benennen: Filter-Chip-Leiste (Primitives-Chip + Aktivzustand Teal) ·
Summenzeile (Card + Money) · Auswahl-Leiste (Card + Money + Button) ·
Geprüft-Schalter (Primitives) · Herkunft/Historie-Zeitleiste (Badge + Text) ·
Konto-Stände-Leiste Handy (Card) · Konten-Spalte PC (Card-Liste).

### Farben — nur diese Werte

Flächen nach Tiefe: `#08081a` App · `#111128` Card · `#1a1a36` elevated ·
`#16162e` Input · `#1e1e3c` markierte Tabellenzeile · Ränder `#2a2a4a` normal,
`#1c1c38` zart. **Keine Flächen erfinden** — nichts außerhalb dieser Staffelung.

Text: `#e8e8f0` primär · `#7a7a98` sekundär · `#4a4a68` tertiär und Overlines.

| Farbe | Wert | Bedeutung |
|---|---|---|
| Orange | `#ff6b2c` (Verlauf nach `#e55a1f`) | primär / CTA, höchstens eine je Screen |
| Teal | `#00d4aa` | sekundär / aktiv / Navigation / Auswahl / „Rückgängig" |
| Green | `#00ff88` | Erfolg, positive Beträge, Geprüft |
| Red | `#ff3b5c` | negative Beträge, Löschen |
| Purple | `#a855f7` | Kategorie |
| Pink | `#ff1f8f` | attention: Status „Zu prüfen", Ungeprüft-Punkt |

Geld: positiv immer Green mit Glow, negativ immer Red.

### Typografie

- **Space Grotesk** 600/700 — Screen-Titel 22px (Handy), Wortmarke 18px, Payee im Sheet 17px
- **Inter** 400/500/600 — Fließtext und Bedienelemente, 10px bis 15px
- **JetBrains Mono** 400/500/600 mit `font-variant-numeric:tabular-nums` — **alle**
  Zahlen, Beträge, Daten, Zeitstempel, Overlines, Tastenkürzel

### Radien

5px Kästchen · 8px kleiner Knopf · 10px Knopf und Navigation · 12px Konto-Karte und
Symbolknopf · 14px Toast und Notizkarte · 22px Sheet oben · 30px Geräterahmen ·
999px Chips, Badges, Schalter.

### Glow — sparsam, nur für Aktives

`0 0 18px rgba(0,212,170,.12)` aktive Karte ·
`text-shadow:0 0 12px rgba(0,255,136,.35)` aktiver positiver Betrag ·
`0 0 20px rgba(255,107,44,.5)` primärer Button ·
`0 0 12px rgba(0,255,136,.6)` Schalterknauf an ·
`0 0 8px rgba(255,31,143,.7)` Ungeprüft-Punkt.

## So sind die Screens gebaut

Eine Design-Component-Datei je Ablauf, benannt
`Ablauf n - <Kurztitel>.dc.html`. **`Ablauf 1 - Durchsehen und pruefen.dc.html` ist die
Vorlage** — Rahmen, Kopfzone, Tab-Bar, Tabellenraster und Notiz-Muster von dort
übernehmen, nicht neu erfinden.

Aufbau der Tafel:
- Kopf: Badge „ABLAUF n", Titel, ein Satz zum Ziel
- Drei Karten: **Annahmen** · **Komponenten aus dem Design-System** ·
  **Zusammengesetzt, weil Komponente fehlt**
- Abschnitt **Handy**, dann Abschnitt **PC**, Screens als wrappende Reihe
- Je Screen: Nummern-Badge `n.m` + Kurztitel, der Screen im Geräterahmen, darunter eine
  Notiz mit den Links zum nächsten Schritt (`<a href="#sn-m">`)
- Anker-Schema `id="sn-m"` (`s1-1` bis `s1-12`)
- `<meta name="design_doc_mode" content="canvas">` im Helmet, damit die Tafel frei
  schwenk- und zoombar ist
- Tweaks am Dokument: Notizen ein/aus, Tastatur-Hinweis ein/aus, Geräte-Filter
  (Beide / Nur Handy / Nur PC)

**Technische Fallen, die schon aufgetreten sind:**
- Kopfzone, Datumsköpfe und Listenzeilen brauchen `flex:none`, sonst schrumpfen sie
  in vollen Screens und die Abstände weichen zwischen Screens ab.
- Die mobile Tab-Bar liegt `position:absolute` am Rahmenboden (nicht `margin-top:auto`).
  Die Liste läuft dahinter aus dem Bild — dabei darf **keine Zeile mittig
  angeschnitten** werden: Inhalt so kürzen, dass die letzte Zeile oberhalb der
  Leistenkante endet (Budget: 766px bis zur Kante, Chrome 267px mit Summenzeile bzw.
  225px ohne, **gemessen 2026-09-18:** Listenzeile ohne Kennzeichen **67,5px**, Zeile mit
  Konto-Kürzel-Pill **71,5px**, Datumskopf **33,5px**, 70px im Auswahl-Modus, **Zeile mit
  Badge 99px**). Im Auswahl-Modus ersetzt die Auswahl-Leiste die Tab-Bar und ist am
  Handy 165px hoch — dann bleiben nur 411px für die Liste. Fehlende
  Zeilen in der Notiz benennen.
- Beträge in der PC-Kontenspalte brauchen `white-space:nowrap`, der Kontoname
  Ellipsis.
- Tafelhintergrund auf `html,body`, sonst bleibt ein heller Rand.

## Übergabe an Entwicklung

Je Ablauf ein Ordner `design_handoff_<feature>/` mit selbsttragendem `README.md`
(Maße, Farben, Zustände, Interaktionen, Tokens, offene Punkte), der Screen-Datei,
`support.js` und dem `_ds/`-Bundle inklusive Fonts. Vorlage:
`design_handoff_kassenbuch_ablauf1/README.md`. Bei Änderungen an einem Screen die
Kopie im Handoff-Ordner mitziehen.

Das README muss sagen: die HTML-Dateien sind **Design-Referenzen**, nachzubauen in
F# / Fable / Elmish mit Tailwind 4 und DaisyUI gegen `src/Client/DesignSystem/*.fs`
und die Tokens aus `src/Client/styles.css`. Mockups fließen **nie** zurück in den Code;
Änderungen am Design-System entstehen im Repo und werden per
`npm run export:design-system` neu exportiert.

## Stand der Abläufe

| Ablauf | Datei | Screens | Stand |
|---|---|---|---|
| 1 — Durchsehen und als geprüft markieren | `Ablauf 1 - Durchsehen und pruefen.dc.html` | Handy 1.1–1.8, PC 1.9–1.12 | fertig, Übergabepaket liegt in `design_handoff_kassenbuch_ablauf1/` |
| 2 — Buchungen finden | `Ablauf 2 - Buchungen finden.dc.html` | Handy 2.1–2.8 + 2.15, PC 2.9–2.14 + 2.16 | fertig, Übergabepaket liegt in `design_handoff_kassenbuch_ablauf2/` |
| 3 — Mehrfachauswahl mit Summe | `Ablauf 3 - Mehrfachauswahl mit Summe.dc.html` | Handy 3.1–3.4, PC 3.5–3.7 | bei Roman zur Abnahme, Übergabepaket fehlt noch |
| 4 — Kategorie ändern, Split | `Ablauf 4 - Kategorie und Split.dc.html` | PC 4.1–4.8, Handy 4.9–4.11 | bei Roman zur Abnahme, Übergabepaket fehlt noch |
| 5 — Flags: Lena fragen, temporär markieren | `Ablauf 5 - Flags.dc.html` | PC 5.1–5.9, Handy 5.10–5.12 | bei Roman zur Abnahme, Übergabepaket fehlt noch |
| 6 — Konto wechseln, Gesamtsicht als Suchhilfe | `Ablauf 6 - Konto wechseln.dc.html` | Handy 6.1–6.6, PC 6.7–6.10 | bei Roman zur Abnahme, Übergabepaket fehlt noch |
| 7 — Was nur BB kann: Herkunft, Historie, Abweichung | `Ablauf 7 - Was nur BB kann.dc.html` | Handy 7.1–7.5, PC 7.6–7.10 | bei Roman zur Abnahme, Übergabepaket fehlt noch |

**Aus Ablauf 7 dazugekommen** (gilt ab jetzt für alle Abläufe): Es gibt **vier Herkunft-Arten** – `Comdirect-Import`, `Regel`, `YNAB-Spiegel` und später Handeingabe (Quick Add); im Detail steht die Art als eigene **HERKUNFT**-Zeile (Teal-Badge) direkt über der Historie · Die **Historie ist eine Zeitleiste von neu nach alt**, je Eintrag Urheber, Wirkung und Zeitstempel, Punktfarbe nach Urheber (Teal Import · Violett Regel · Grau YNAB-Spiegel · Rot Fehler und Löschung · Green eigene Prüfung) · **Hinweisleiste mit Grund und Aktion** in drei Färbungen, immer direkt unter dem Kopf: Orange „Noch nicht in YNAB“ (+ Orange-CTA „Erneut senden“), Pink „Abweichung“ (v2, + „YNAB übernehmen“ Orange / „BB durchsetzen“ Ghost), Rot „in YNAB gelöscht“ (ohne Aktion) · **„Noch nicht in YNAB“ erscheint nie ohne Grund** (Push gescheitert, Zeitstempel, Fehlerart) · **Gelöschte Buchungen** sind ein Lesezustand: Betrag grau statt rot, Felder ohne Chevron, kein Geprüft-Schalter, keine primäre Aktion; sie zählen **in der Trefferzahl, nicht in der Summe** und nie im Kontostand · Die Status-Spalte am PC trägt **bis zu zwei Badges** („Zu prüfen“ + „Nicht in YNAB“ bzw. + „Abweichung“) · **Gemessen 2026-09-18:** Kopfzone Handy **224px** ohne Summenzeile, **263,5px** mit; Tab-Bar-Kante bei **767px**; Listenzeile mit Punkt 67,5px, ohne Punkt 66px, **mit Badge 95,5px**; Detail-Sheet **620px** (setzt genau an der Kopfzone an) bzw. **576px** über einer Summenzeile · Die Historie im Sheet **scrollt**, die Zeitleiste im PC-Panel hat Platz.

**Aus Ablauf 6 dazugekommen** (gilt ab jetzt für alle Abläufe): Der **Konto-Wechsel ist der Ablauf**,
die Gesamtsicht ist **Suchhilfe** für „in welchem Konto liegt diese Buchung?" und kein eigener
Arbeitsplatz (Zuschnitt korrigiert 2026-09-12 nach Romans YNAB-Referenz) · Am Handy wechselt entweder
die Kontostände-Leiste oder der **Chevron im Titel**, der dieselbe Auswahl als BottomSheet öffnet
(Zeilen 56px, aktives Konto Teal mit Haken, **kein „Übernehmen"** — ein Tipp wechselt und schließt);
am PC ist die **Konten-Spalte** der Wechsler · Der Wechsel hat **keinen Ladebildschirm und keinen
Seitenwechsel**, die Kopfzone bleibt stehen, aktive Filter bleiben und zählen neu · Am PC steht unten
in der Konten-Spalte der **Lauf-Status des Spiegels** („Zuletzt gespiegelt: heute 21:40") · Die
Tabelle gewinnt in der Gesamtsicht die **Konto-Spalte** (116px, „ING Extra" · „Girokonto" · „Bar" ·
„Geldmarkt-ETF"), in der Konto-Sicht entfällt sie · **Zu prüfen je Konto** (erfunden): ING 12
(-625,38 €) · Girokonto 9 (-334,79 €) · Bar 3 (-35,80 €) · ETF 3 (+310,70 €), zusammen 27 · -685,27 €
· Der **Geldmarkt-ETF** trägt eine Orange-Hinweisleiste „in YNAB noch nicht angelegt" samt
`ledger-nb4tv`, Status „Nicht in YNAB" statt „Zu prüfen", keine Kategorie; seine
Bewertungs-Buchungen („Kursgewinn Juli/Juni") sind das, was nur BB zeigt · Ungeprüft am Handy ist ein
**Pink-Punkt vor dem Payee**.

**Aus Ablauf 5 dazugekommen** (gilt ab jetzt für alle Abläufe): Es gibt **zwei Flag-Arten**
mit klaren Namen statt „Flag setzen": die Zuweisung **„Rückfrage Lena"** (Violett `#a855f7`,
Sprechblasen-Icon, Aktion „Lena fragen", Zustände offen und erledigt, Rücknahme über
„Erledigt") und die **temporäre Markierung** (Orange, Fähnchen-Icon, Aktion „Markieren",
Badge „Markiert", Filter „Temporär", Sammelausstieg „Alle Markierungen aufheben" am Ende
der Filterleiste) · Im Detail steht unter den Feldern der Bereich **„Zuweisen"** mit zwei
Ghost-Buttons; bei offener Zuweisung ersetzt der Status-Block „Zugewiesen an Lena, offen"
+ „Erledigt" die Knöpfe · Die Aktion **„Flag" in der Auswahl-Leiste wird zu „Lena fragen"
und „Markieren"** — am Handy sind das drei Reihen à 44px, die Leiste ist dort **197px**
hoch und lässt 450px für die Liste · Der **Flag-Chip** öffnet das Popover-Muster mit den
zwei Arten und ihrer Anzahl aus dem aktuellen Treffer, Einzelauswahl filtert sofort ·
Historie-Zeilen „Roman in BB: Lena gefragt" und „Roman in BB: Rückfrage erledigt" ·
Zuweisen prüft nicht mit und sortiert nie um · Passt eine Zeile nach einer Änderung nicht
mehr zum aktiven Filter, **bleibt sie ausgegraut an ihrem Platz** („passt nicht mehr zum
Filter"), Trefferzahl und Summe gehen sofort mit · Zeile mit Kennzeichen am Handy: 99px ·
Backlog „Für mich"-Sicht und Push für Lena: `ledger-xfjcp`.

**Aus Ablauf 4 dazugekommen** (gilt ab jetzt für alle Abläufe): Jede Kategorie in der
Kategorienauswahl trägt rechts ihren **Available-Betrag** (Money, Green/Red/tertiär bei
0,00 €) · **Kategoriezuweisung am PC** läuft über dasselbe Popover-Muster wie die Filter,
angesetzt an der Kategorie-Zelle, Einzelauswahl mit Klick-Commit, Esc schließt ohne zu
ändern · nach dem Wechsel steht an der Popover-Stelle kurz der **Budget-Hinweis**
(alter Wert durchgestrichen → neuer Wert), Rücknahme über den Toast ·
**Split-Editor** (nur im Detail): Gesamtbetrag und Rest über den Zeilen, je Zeile
Kategorie-Picker, Betragsfeld, Memo und „Rest"-Knopf; „Speichern" wird erst bei Rest
0,00 € aktiv, Rest ≠ 0 steht in Pink, Rest 0,00 € in Green · Tabellenzeile einer
aufgeteilten Buchung trägt das Badge **„Aufgeteilt (n)"** statt einer Kategorie ·
Bei offenem Seitenpanel (420px) **weicht die Memo-Spalte** am PC und die Chip-Leiste
wird waagerecht scrollbar · Änderungen in BB tragen bis v2 den Orange-Hinweis
**„Änderung noch nicht in YNAB"** mit Grund direkt unter dem Panel-Kopf.

**Aus Ablauf 3 dazugekommen** (gilt ab jetzt für alle Abläufe): „Alle" als vierte
Aktion in der Auswahl-Leiste am Handy (dritte Reihe: Flag · Alle · Auswahl aufheben) ·
Kopfzeilen-Kästchen am PC wählt den **gesamten Treffer**, nicht die geladene Seite, und
zeigt bei Teilauswahl einen Teal-Strich statt des Hakens · Shift-Klick-Spanne markiert
den Anker mit `box-shadow:inset 2px 0 0 #00d4aa` · Ausstieg heißt überall „Auswahl
aufheben", nicht „Fertig" · **kein „Kopieren"** (Entscheidung Roman 2026-09-14, war eine
Erfindung im Brief).

## Offene Punkte für Roman

1. **Auswahl-Summe PC (Screen 1.11):** Brief nannte „3 ausgewählt · -186,10 €"; mit den
   erfundenen Beträgen geht keine Dreierkombination darauf auf. Umgesetzt sind
   -190,10 € (Stadtwerke -142,00 + Deutsche Bahn -29,90 + Apotheke -18,20).
2. **Wisch nach links** bleibt unbelegt; später denkbar für „Rückfrage Lena".
3. **Ungeprüft-Kennzeichen** ist Pink (Punkt am Handy, Badge am PC). Orange wäre die
   Alternative, ist aber für die primäre Aktion reserviert.
4. **Zeitraum-Chip** trägt die Kurzform („12 Monate") statt des Klartext-Datumsbereichs.
5. **Kategorie-Chip bei genau einer Kategorie** trägt „Kategorie: 1" (Zähler-Muster aus
   Ablauf 2). „Kinder" als Klartext wäre lesbarer, bricht aber das Muster.
6. **Available-Beträge** sind erfunden (Haushalt 412,10 €, Möbel 120,00 €, …) und
   hängen an der Spiegel-Klärung `ledger-s8m3w`.
7. **Split-Zeilen im Editor** zeigen positive Betragsfelder (23,40 €) und in der
   Übersicht negative Beträge (-23,40 €) — Vorzeichen kommt vom Gesamtbetrag.
8. **Payee bleibt Einzelauswahl** — Mehrfachauswahl wäre mit demselben Kästchen-Muster
   möglich, ist aber nicht gefordert.
9. **Orange für „Markiert"** (Badge und Fähnchen) ist die einzige Stelle außerhalb der
   primären Aktion, an der Orange auftritt; auf den betroffenen Screens gibt es deshalb
   keinen Orange-CTA. Teal wäre die Alternative, ist aber für „aktiv" belegt.
10. **Zeile bleibt bei Filter-Nichttreffer stehen** (Screen 5.8, ausgegraut mit Hinweis) —
    neu entschieden in Anlehnung an „Prüfen sortiert nie um". Alternative: sofort aus der
    Liste nehmen.
11. **Auswahl-Leiste am Handy wächst auf 197px**, weil sechs Aktionen auf drei Reihen
    gehen (Abstände dort enger als in Ablauf 3). Alternative: „Markieren" am Handy
    weglassen (es ist der seltene Fall).
12. **„Lena" ist ein fester Name** in der Oberfläche („Lena fragen"). Ein konfigurierbarer
    Name aus dem YNAB-Flag wäre sauberer, ist aber nicht gefordert.
13. **Zu-prüfen-Zahlen je Konto** sind erfunden (ING 12, Girokonto 9, Bar 3, ETF 3) und ergeben in der
    Gesamtsicht 27 · -685,27 €. Wenn echte Zahlen vorliegen, ziehen alle Screens von Ablauf 6 mit.
14. **Die drei ETF-Zeilen** („Kursgewinn Juli/Juni", „Umbuchung von ING Extra") sind erfunden;
    „Umbuchung" ist kein Payee aus der Liste, sondern eine interne Buchung. Der monatliche Abgleich
    des ETF liegt als `ledger-nb4tv` im Backlog.
16. **Gelöschte Zeile zählt in der Trefferzahl, nicht in der Summe** (13 Buchungen · -712,26 €). Alternative: sie ganz aus Zähler und Summe nehmen – dann stimmt die Zahl nicht mit den sichtbaren Zeilen.
17. **Abweichung ist Pink**, weil Orange für die primäre Aktion reserviert ist. „YNAB übernehmen“ ist die Orange-Aktion, „BB durchsetzen“ der Ghost – welche der beiden Roman häufiger braucht, ist offen (v2).
18. **„Erneut senden“ ist im Prototyp nur der Knopf.** Ob der Push sofort läuft oder der nächste Sync-Lauf ihn mitnimmt, ist offen.
19. **Kein „Wiederherstellen“** bei gelöschten Buchungen – das wäre ein Schreibvorgang nach YNAB (v2).
20. **Zeitstempel, die Regel „REWE Mitte“, der Bauhaus-Betrag -84,90 € und die 13 September-Treffer** sind erfunden.
15. **Suchhilfe-Screen bleibt halb leer** (6.9, fünf Treffer auf 1440 × 900). Das ist die Wahrheit des
    Treffers; eine Alternative wäre ein Hinweis „5 von 14.031 Buchungen" unter der Liste.
