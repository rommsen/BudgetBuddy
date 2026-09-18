# Handoff: Kassenbuch – Ablauf 2 „Buchungen finden"

> **Herkunft.** Erzeugt in Claude Design (Projekt „Gelesen UI-Mockups",
> `5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`, Datei
> `design_handoff_kassenbuch_ablauf2/README.md`), am 2026-09-18 ins Repo übernommen. Die
> zugehörige Tafel mit allen sechzehn Screens liegt dort als
> `Ablauf 2 - Buchungen finden.dc.html`. Zwei bewusste Abweichungen zur Design-Fassung:
> die Verweise auf das mitgelieferte `_ds/`-Bundle sind gekürzt, und **DaisyUI ist
> gestrichen** (mit ADR 0031 aus dem Projekt entfernt; das Design-Projekt weiß davon
> nichts, beim nächsten Abgleich nicht zurückziehen).
>
> **Status: Referenz, nicht Code.** Die Werte hier sind verbindlich für die Umsetzung;
> die Quelle der Wahrheit für das Design-System selbst bleibt das Repo
> (`src/Client/DesignSystem/*.fs`, `src/Client/styles.css`, `standards/frontend/styleguide.md`).
> Dieses Dokument fließt nicht zurück nach Claude Design.

## Überblick

Das Kassenbuch ist die Buchungsliste von BudgetBuddy (YNAB-Companion). Ablauf 2 deckt
das Suchen ab: Roman filtert, in 70 bis 80 % der Fälle **nach Kategorie**, seltener
nach Zeitraum (15 %), Payee, Betrag oder Memo (je 5 %). **Mehrere Kategorien zugleich**
sind der Normalfall, nicht die Ausnahme.

Der bestehende YNAB-Weg: Kategorie ins Suchfeld tippen, warten bis die richtige
gefunden ist, „Filtern" klicken — **nur eine Kategorie je Filter**, versteckte
Kategorien tauchen auf und werden ungewollt gewählt, am Handy praktisch unbenutzbar.
Das ist der Schmerz, den dieser Ablauf ablöst.

Zwei Geräte, gleichwertig:
- **Handy 390 × 844** — Screens 2.1 bis 2.8, Kategorienauswahl als BottomSheet, Auswahl-Modus 2.15
- **PC 1440 × 900** — Screens 2.9 bis 2.14, Kategorienauswahl als Popover unter dem Chip, Auswahl 2.16

Ausgangszustand in allen Screens: Gesamtsicht **„Alle Konten"**, kein Filter aktiv.

## Zu den Design-Dateien

Die Dateien im Ursprungspaket sind **Design-Referenzen in HTML** — Prototypen, die
Aussehen und Verhalten festhalten. Sie sind **kein Produktionscode zum Übernehmen**.

Nachzubauen in der bestehenden Umgebung: F# / Fable / Elmish mit Tailwind 4 (**ohne
DaisyUI**, ADR 0031), mit den Komponenten aus `src/Client/DesignSystem/*.fs` und den
Tokens aus `src/Client/styles.css`. Nicht das HTML einbetten, sondern die vorhandenen Komponenten
benutzen: `Kategorienauswahl` (components-categorypicker), `BottomSheet`, `Modal`,
`Input`, `Badge`, `Money`, `Table`, `Card`, `Navigation`, `Button`, `Icons`,
`Primitives`, `Loading`, `ErrorDisplay`.

Mockups fließen **nie** zurück in den Code. Änderungen am Design-System entstehen im
Repo und werden per `npm run export:design-system` neu exportiert.

## Fidelity

**Hi-fi.** Farben, Typografie, Abstände, Zustände und Texte sind endgültig und
entstammen dem gebundenen BudgetBuddy Design System. Maße sind pixelgenau gemeint. Die Tafel selbst (Kopf, Screen-Beschriftungen, Notizen,
Annahmen-Karten) ist Dokumentation und wird **nicht** implementiert.

## Grundlage, die für alle Screens gilt

**Aufbau von oben nach unten, beide Geräte:**
1. Kontostände — Handy: waagerecht scrollbare Karten-Leiste; PC: linke Spalte
2. Sticky Filterleiste aus Chips
3. Summenzeile — **nur wenn ein Filter aktiv ist**
4. Liste

**Gesamtsicht.** „Alle Konten" ist in der Konten-Spalte (PC) bzw. als erste Karte
(Handy) aktiv, Teal mit Glow. Jede Zeile trägt ihr Konto: **Kürzel** am Handy
(`ING`, `GIRO`, `BAR`, `ETF`, Mono 500 11px in einem `#16162e`-Pill), **Konto-Spalte**
am PC (104px, Mono 500 12px `#7a7a98`).

**Zahlen kommen vom Server.** Trefferzahl und Summe gelten über den **gesamten**
Treffer, nicht über die geladenen 100. Kontostände kommen aus dem Zustand, nie aus der
Liste. Filterwechsel springt nach oben, Chips bleiben sichtbar.

**Chips.** Ohne Filter zeigt „Zu prüfen (7)" als erster Chip nur seinen Zähler und ist
inaktiv. Aktiv: `rgba(0,212,170,.12)`, Rand `rgba(0,212,170,.4)`, Inter 600 13px
`#00d4aa`, dazu ein „×" (13px, Strichstärke 2.6) zum einzelnen Entfernen.
Inaktiv: `#16162e`, Rand `#2a2a4a`, Inter 500 13px `#7a7a98`.
Am Handy 44px Trefferfläche über `padding:12px 14px` + `line-height:20px`, am PC
`padding:9px 13px`.

**Chip-Reihenfolge.** Am Handy wandern aktive Chips **nach vorn**, weil die Leiste
waagerecht scrollt und der aktive Filter sichtbar bleiben muss. Am PC bleibt die
Reihenfolge fest (alle Chips sind ohnehin sichtbar), nur die Farbe wechselt.

**Chip-Beschriftung im Aktivzustand**
| Filter | Chip zeigt |
|---|---|
| Kategorie (Mehrfachauswahl) | `Kategorie: 2` + × |
| Zeitraum | Kurzform der Schnellwahl, z. B. `12 Monate` + × |
| Payee | den Namen, z. B. `Café Lotte` + × |

**„Alle Filter löschen"** sitzt am **Ende** der Leiste (PC: `margin-left:auto`,
Textknopf Inter 600 13px `#00d4aa`; Handy: letzter Chip in der scrollbaren Leiste).
Einzelne Filter fallen über das „×" am Chip.

**Click-Commit.** Mehrfachauswahl (Kategorie) wirkt erst auf „Übernehmen"
(Styleguide 7.1). Einzelauswahl (Payee, Zeitraum-Schnellwahl setzt Von/Bis) filtert
sofort bzw. bestätigt mit „Übernehmen", wenn Von/Bis mitgeht.

**Genau eine primäre Aktion je Screen** — hier immer „Übernehmen" in Orange.

## Screens Handy

### 2.1 Gesamtsicht ohne Filter

**Zweck:** Ausgangszustand, aus dem jeder Filter startet.

| Block | Maße | Inhalt |
|---|---|---|
| Statusleiste | `padding:12px 24px 4px` | „9:41" Mono 600 13px `#7a7a98` |
| Kopf | `padding:4px 16px 12px` | Overline „KASSENBUCH" Mono 600 10px `letter-spacing:.16em` `#4a4a68`; Titel **„Alle Konten"** Space Grotesk 600 22px `#e8e8f0` + Chevron 16px Teal (Konto-Wechsler); rechts zwei Knöpfe 44 × 44, `radius:12px`, `#16162e`, Rand `#2a2a4a` |
| Kontostände | `padding:0 16px 14px`, `gap:10px` | Karten 116px, `radius:12px`, `padding:9px 11px`. Aktiv („Alle Konten"): `#1a1a36`, Rand `rgba(0,212,170,.45)`, `box-shadow:0 0 18px rgba(0,212,170,.12)`, Name Inter 600 11px `#00d4aa`, Betrag mit `text-shadow:0 0 12px rgba(0,255,136,.35)`. Inaktiv: `#111128`, Rand `#2a2a4a` |
| Chips | `padding:0 16px 12px`, Unterkante `#1c1c38` | Zu prüfen (7) · Kategorie · Zeitraum · Payee (weitere durch Scrollen) |
| Summenzeile | — | **fehlt**, weil kein Filter aktiv ist |
| Datumskopf | `padding:14px 16px 6px` | „HEUTE" / „GESTERN" / „08.09." Mono 600 10px `#4a4a68` |
| Listenzeile | `padding:13px 16px`, Unterkante `#1c1c38` | Zeile 1: Payee Inter 600 15px `#e8e8f0` + Betrag Mono 600 15px `tabular-nums` `#ff3b5c`. Zeile 2 (`margin-top:4px`, `gap:8px`): **Konto-Kürzel** Pill · Kategorie Inter 500 12px `#a855f7` (Kurzform) · Memo Inter 400 12px `#4a4a68` |
| Tab-Bar | `position:absolute`, Boden, `padding:10px 8px 22px` | Kassenbuch (aktiv Teal) · Quick Add · Sync · Mehr |

**Liste:** HEUTE REWE Mitte `ING` Lebensmittel Wocheneinkauf -48,12 € · HEUTE Bäckerei
Krause `GIRO` Lebensmittel -6,40 € · GESTERN Deutsche Bahn `ING` Bahn Ticket Köln
-29,90 € · GESTERN Café Lotte `BAR` Restaurants -9,80 € · 08.09. Stadtwerke
Musterstadt `GIRO` Nebenkosten Abschlag September -142,00 € · 08.09. Netflix `GIRO`
Streaming -13,99 €.

Die Liste läuft hinter der Tab-Bar aus; keine Zeile wird mittig angeschnitten
(Budget 766px bis zur Kante, Chrome 225px ohne Summenzeile, Zeile 66px, Datumskopf 34px).

### 2.2 Kategorienauswahl offen (BottomSheet)

Abdunklung `position:absolute; inset:0; background:rgba(5,5,16,.72)`.
Sheet: Höhe **744px**, unten angeschlagen, `#1a1a36`, Oberkante `1px solid #2a2a4a`,
`border-radius:22px 22px 0 0`, `box-shadow:0 -24px 60px rgba(0,0,0,.65)`.
Griff 40 × 4px `#2a2a4a`, `margin:10px auto 6px`.

Aufbau von oben:
1. **Kopf** `padding:6px 18px 14px`, Unterkante `#1c1c38`: „Kategorie" Space Grotesk
   600 17px; rechts Schließen 36 × 36, `radius:10px`, `#16162e`
2. **Suchfeld** `padding:12px 18px`: Höhe 44px, `radius:10px`, `#16162e`, Rand
   `#2a2a4a`, Lupe 16px `#4a4a68`, Platzhalter „Kategorie suchen" Inter 400 14px
3. **„ZULETZT VERWENDET"** Overline + Chips (Lebensmittel, Restaurants, Nebenkosten,
   Bahn), `padding:9px 12px`, `radius:999px` — Abkürzung für die häufigen Fälle
4. **Gruppenliste**, scrollbar: Gruppen-Overline Mono 600 10px `#4a4a68`
   (`padding:12px 18px 6px`), Zeilen `min-height:48px`, `padding:0 18px`, `gap:13px`:
   Kästchen 20 × 20, `radius:5px`, `#16162e`, Rand `#2a2a4a`; Label Inter 500 14px
   `#e8e8f0`. Reihenfolge Alltag (Lebensmittel, Haushalt, Restaurants) · Wohnen
   (Nebenkosten, Möbel) · Mobilität (Bahn, Auto) · Familie (Kinder, Geschenke) ·
   Abos (Streaming) · Gesundheit (Apotheke)
5. **„Versteckt (1)"** als eingeklappte Zeile am Ende, `min-height:52px`, `#16162e`,
   Chevron rechts — versteckte Kategorien (`Umzug 2023`) sind **nie** versehentlich
   erreichbar
6. **Fuß** `padding:14px 18px 22px`: links „Zurücksetzen" als Text, rechts
   „Übernehmen" über die restliche Breite, Höhe 48px. **Ohne Auswahl inaktiv**
   (`#16162e`, Rand `#2a2a4a`, Label `#4a4a68`)

### 2.3 Zwei Kategorien gewählt

Gesetztes Kästchen: Grund und Rand `#00d4aa`, Häkchen 13px `#08081a` Strichstärke 3.5.
Gewählte Zeile: Grund `rgba(0,212,170,.07)`, Label Inter 600.
Dieselben Kategorien leuchten oben in „Zuletzt verwendet" mit (Teal-Chip) — ein Zustand,
zwei Orte.
Kopf trägt das Badge „2 gewählt" (`rgba(0,212,170,.12)`, Rand `rgba(0,212,170,.4)`,
Mono 600 11px `#00d4aa`).
Fuß: **„Übernehmen (2)"** primär, `linear-gradient(135deg,#ff6b2c,#e55a1f)`, `#fff`,
`radius:10px`, `box-shadow:0 0 20px rgba(255,107,44,.5)`.

### 2.4 Treffer nach Kategorie

Chip „Kategorie: 2" + × steht **vorn**, „Zu prüfen (7)" rutscht dahinter.
Summenzeile erscheint: „312 Buchungen" Inter 500 12px `#7a7a98` · „-9.874,20 €"
Mono 600 14px `#ff3b5c`.

**Liste:** HEUTE REWE Mitte `ING` -48,12 € · HEUTE Bäckerei Krause `GIRO` -6,40 € ·
GESTERN Café Lotte `BAR` -9,80 € · GESTERN Bäckerei Krause `GIRO` -4,20 € ·
08.09. Café Lotte `BAR` -12,60 € mit Badge **„Noch nicht in YNAB"**
(`rgba(255,107,44,.1)`, Rand `rgba(255,107,44,.3)`, Inter 500 11px `#ff6b2c`,
`margin-top:5px` als dritte Zeile, Zeilenhöhe dadurch 75px).

Kennzeichen stehen **nur, wenn sie abweichen** — geprüfte Zeilen tragen keins.

### 2.5 Zeitraum wählen (BottomSheet)

Sheet Höhe **432px**, gleicher Rahmen wie 2.2.
- Overline „SCHNELLWAHL", darunter 2 × 2-Raster, `gap:10px`, Knöpfe Höhe 48px,
  `radius:999px`: Dieser Monat · Letzter Monat · Dieses Jahr · **Letzte 12 Monate**
  (gewählt, Teal)
- „VON" / „BIS" Overlines, zwei Felder Höhe 46px, `radius:10px`, `#16162e`, Werte
  Mono 500 14px `tabular-nums`: `13.09.2025` / `13.09.2026`. **Die Schnellwahl füllt
  Von/Bis mit**, damit sichtbar ist, was gefiltert wird
- Fuß: „Zurücksetzen" + „Übernehmen" (primär)

### 2.6 Kategorie und Zeitraum

Zwei aktive Chips: „Kategorie: 2" ×, „12 Monate" ×, dahinter Payee, Betrag.
Summenzeile „148 Buchungen · -4.610,55 €". Die neuesten Treffer bleiben dieselben,
nur Zahl und Summe fallen.

### 2.7 Payee aus den Treffern (BottomSheet)

Sheet Höhe **452px**.
- Suchfeld aktiv: Rand `rgba(0,212,170,.4)`, Lupe Teal, Text „Caf" + Cursor
  (1 × 18px Teal)
- Overline **„VORSCHLÄGE AUS DEN 148 TREFFERN"** — die Vorschläge kommen aus dem
  aktuellen Treffer, nicht aus allen Buchungen
- Zeilen `min-height:56px`: Name links Inter 500/600 15px, **Anzahl** rechts Mono 500
  13px `tabular-nums`. Café Lotte 9 (hervorgehoben, `rgba(0,212,170,.07)`) ·
  REWE Mitte 78 · Bäckerei Krause 52
- Fußnote Inter 400 12px `#4a4a68`: „Ein Tipp auf den Vorschlag filtert sofort — Payee
  ist immer nur einer." **Kein „Übernehmen"**, weil Einzelauswahl
- Suche mit kurzer Verzögerung (Debounce)

### 2.8 Drei Filter, neun Treffer

Chips: „Café Lotte" ×, „Kategorie: 2" ×, dann **„Alle Filter löschen"**.
Summenzeile „9 Buchungen · -163,40 €".

**Liste:** GESTERN Café Lotte `BAR` -9,80 € · 08.09. Café Lotte `BAR` -12,60 € +
„Noch nicht in YNAB" · 02.09. Café Lotte `GIRO` Mittag -18,40 € · 27.08. Café Lotte
`BAR` -11,20 €. Vier von neun sind sichtbar, der Rest kommt beim Scrollen.

### 2.15 Auswahl im Treffer mit Summe

**Zweck:** mehrere Treffer zugleich anfassen und sehen, was sie zusammen ausmachen.

**Langes Drücken** auf eine Zeile startet den Auswahl-Modus. Jede Zeile bekommt links
ein Kästchen 20 × 20, `radius:5px`, `#16162e`, Rand `#2a2a4a`, `gap:13px` zum Inhalt;
gesetzt: Grund und Rand `#00d4aa`, Häkchen 13px `#08081a` Strichstärke 3.5.
Gewählte Zeile: Grund `rgba(0,212,170,.07)`.

**Summenzeile zeigt die Auswahl statt des Filters**: „Auswahl: 3 von 9 Buchungen"
Inter 600 12px `#00d4aa`, Unterkante `rgba(0,212,170,.3)`,
`box-shadow:inset 2px 0 0 #00d4aa`, rechts **„-39,40 €"** Mono 600 14px `#ff3b5c`.

**Auswahl-Leiste** ersetzt die Tab-Bar: `position:absolute`, Boden,
`padding:12px 16px 22px`, `#1a1a36`, Oberkante `#2a2a4a`,
`box-shadow:0 -14px 40px rgba(0,0,0,.45)`, `gap:10px`.
- Zeile 1: „3 ausgewählt" Inter 600 14px `#e8e8f0` · „-39,40 €" Mono 600 16px `#ff3b5c`
- Zeile 2: **„Geprüft"** primär (Orange-Verlauf, `radius:10px`, Glow) + „Kategorie ändern"
  sekundär, je `flex:1`, `min-height:44px`
- Zeile 3: „Flag" sekundär + „Auswahl aufheben" als Text `#7a7a98`, je `flex:1`, 44px

Gewählt sind Café Lotte Gestern -9,80 € · 02.09. -18,40 € · 27.08. -11,20 € =
**-39,40 €**. Die Mehrfachaktion ergibt **einen** Toast und **eine** Rücknahme.

Der Screen zeigt diese drei Zeilen vollständig oberhalb der Auswahl-Leiste; die nicht
gewählte Zeile 08.09. (-12,60 €, Badge „Noch nicht in YNAB", 99px hoch statt 70px) ist
im Prototyp weggelassen, weil sie sonst hinter der Leiste angeschnitten würde.
**Budget in 2.15:** Chrome mit Summenzeile 267px, Auswahl-Leiste 165px, also 411px für
Datumsköpfe (34px) und Zeilen (70px, mit Badge 99px).

## Screens PC

### 2.9 Gesamtsicht ohne Filter

**Kopfleiste** 64px, `padding:0 24px`, `rgba(8,8,26,.85)`, Unterkante `#1c1c38`:
Logo 40 × 40 `radius:12px` `linear-gradient(135deg,#ff6b2c,#00d4aa)` mit „B"
Space Grotesk 700 20px, Wortmarke Space Grotesk 700 18px; rechts Kassenbuch (aktiv,
`rgba(0,212,170,.1)`, Inter 600 14px `#00d4aa`) · Sync · Regeln · Einstellungen.

**Konten-Spalte** 268px, `#08081a`, rechte Kante `#1c1c38`, `padding:18px 14px`:
Overline „KONTEN", Zeilen `padding:11px 12px`, `radius:10px`, Name Inter 500 13px
(ellipsis), Betrag Mono 600 13px `tabular-nums` `#00ff88`, `white-space:nowrap`.
**„Alle Konten" zuoberst und hier aktiv**: `#1a1a36`,
`box-shadow:inset 2px 0 0 #00d4aa`, Name Inter 600 13px `#e8e8f0`, Betrag mit Glow.
Dann Girokonto Comdirect · ING Extra · Bar · Geldmarkt-ETF.

**Chip-Leiste** `padding:14px 20px`, `gap:8px`: alle acht Chips sichtbar
(Zu prüfen (7) · Kategorie · Zeitraum · Payee · Betrag · Memo · Flag · Gelöschte zeigen).

**Tabelle**, Spaltenraster
`44px 96px 104px 1fr 196px 1fr 132px 128px` = Kästchen · Datum · **Konto** · Payee ·
Kategorie · Memo · Betrag (rechts) · Status (rechts).
- Sticky Kopf 42px, `#1a1a36`, Unterkante `#2a2a4a`, Mono 600 10px
  `letter-spacing:.12em` `#4a4a68`
- Zeilen 52px, Unterkante `#1c1c38`; Datum Mono 500 13px `#7a7a98`, Konto Mono 500
  12px `#7a7a98`, Payee Inter 600 14px `#e8e8f0`, Kategorie Inter 500 13px `#a855f7`
  **voll qualifiziert** („Alltag: Lebensmittel"), Memo Inter 400 13px `#7a7a98`,
  Betrag Mono 600 14px `tabular-nums` `#ff3b5c`
- Kästchen 17 × 17, `radius:5px`, `#16162e`, Rand `#2a2a4a`
- Markierte Zeile: `#1e1e3c`, `box-shadow:inset 0 0 0 1px rgba(0,212,170,.45)`
- Status-Badges: „Zu prüfen" Pink (`rgba(255,31,143,.1)`, Rand `rgba(255,31,143,.3)`,
  `#ff1f8f`) · „Geprüft" Green (`rgba(0,255,136,.1)`, Rand `rgba(0,255,136,.3)`,
  `#00ff88`) · **„Nicht in YNAB"** Orange (`rgba(255,107,44,.1)`, Rand
  `rgba(255,107,44,.3)`, `#ff6b2c`)

**Tastatur-Hinweis** als Fußzeile, `padding:12px 20px`, `#08081a`, Oberkante
`#1c1c38`: `↑ ↓` bewegen · `Leertaste` auswählen · `G` geprüft · `Enter` Detail öffnen.

Zehn Zeilen: REWE Mitte · Bäckerei Krause · Deutsche Bahn · Café Lotte · Stadtwerke
Musterstadt · Netflix · Café Lotte (Nicht in YNAB) · Amazon Marketplace (Badge „Keine
Kategorie") · Apotheke am Markt · Kita-Beitrag Stadt.

### 2.10 Kategorienauswahl als Popover

**Popover** `position:absolute; left:408px; top:126px` — also unter seinem Chip —,
Breite **460px**, `#1a1a36`, Rand `#2a2a4a`, `radius:14px`,
`box-shadow:0 24px 60px rgba(0,0,0,.65)`. Die Liste dahinter bleibt sichtbar
(im Prototyp `opacity:.5` als Fokus-Hinweis, im Code kein Dimmen nötig).

Inhalt identisch zum Sheet in 2.2, nur kompakter:
- Kopf `padding:14px 16px`: „Kategorie" Space Grotesk 600 15px + Badge „2 gewählt";
  rechts Hinweis „Esc schließt" Inter 500 12px `#4a4a68`
- Suchfeld Höhe 40px; „ZULETZT VERWENDET" mit Chips `padding:7px 11px`
- Gruppenliste Höhe 286px, Zeilen 40px, Kästchen 18 × 18
- „Versteckt (1)" Zeile 44px, `#16162e`
- Fuß rechtsbündig: „Zurücksetzen" + „Übernehmen (2)" Höhe 40px, `padding:0 20px`, Orange

**Esc schließt ohne zu filtern.** Klick außerhalb ebenso.

### 2.11 Treffer nach zwei Kategorien

Chips bleiben in Reihenfolge, „Kategorie: 2" × ist Teal, „Alle Filter löschen" rechts.
Summenzeile nennt den Filter im Klartext: „312 Buchungen · alle Konten · Lebensmittel,
Restaurants" + „-9.874,20 €" Mono 600 15px.
Zehn Zeilen, gemischte Konten, Status Pink/Green/Orange wie in 2.9.

### 2.12 Zeitraum als Popover

`left:538px; top:126px`, Breite 372px. Schnellwahl als 2 × 2-Raster (Knöpfe 38px),
Von/Bis darunter (Felder 38px, Mono 500 13px), Fuß rechtsbündig mit „Übernehmen".
Gleiches Muster wie 2.10 — ein Popover-Typ für alle Filter.

### 2.13 Payee mit Vorschlägen

`left:659px; top:126px`, Breite 392px. Suchfeld aktiv („Caf" + Cursor), Overline
„VORSCHLÄGE AUS DEN 148 TREFFERN", Zeilen 44px mit Anzahl rechts
(Café Lotte 9 · REWE Mitte 78 · Bäckerei Krause 52), Fußnote „Payee ist immer nur
einer — der Klick filtert sofort". Kein „Übernehmen".
Summenzeile dahinter: „148 Buchungen · alle Konten · Lebensmittel, Restaurants ·
letzte 12 Monate · -4.610,55 €".

### 2.14 Drei Filter, neun Treffer

Chips „Kategorie: 2" ×, „12 Monate" ×, „Café Lotte" ×, rechts „Alle Filter löschen".
Summenzeile „9 Buchungen · alle Konten · Café Lotte · letzte 12 Monate · -163,40 €".
Alle neun Treffer passen auf eine Seite; **die Summe der Zeilen ergibt genau die
-163,40 €** der Summenzeile:

| Datum | Konto | Memo | Betrag | Status |
|---|---|---|---|---|
| Gestern | Bar | — | -9,80 € | Geprüft |
| 08.09. | Bar | — | -12,60 € | Nicht in YNAB |
| 02.09. | Girokonto | Mittag | -18,40 € | Geprüft |
| 27.08. | Bar | — | -11,20 € | Geprüft |
| 19.08. | Girokonto | Kuchen | -24,60 € | Geprüft |
| 05.08. | Bar | — | -8,90 € | Geprüft |
| 21.07. | Girokonto | — | -16,30 € | Geprüft |
| 30.06. | Bar | — | -14,70 € | Geprüft |
| 12.06. | Girokonto | Frühstück mit Lena | -46,90 € | Geprüft |

Alle Zeilen Payee „Café Lotte", Kategorie „Alltag: Restaurants".

### 2.16 Auswahl im Treffer mit Summe

Dieselbe Auswahl und dieselbe Summe wie am Handy (2.15), damit beide Geräte dieselbe
Rechnung zeigen.

- Gesetztes Kästchen 17 × 17: Grund und Rand `#00d4aa`, Häkchen 11px `#08081a`
  Strichstärke 3.5; ausgewählte Zeile `rgba(0,212,170,.07)`
- **Summenzeile** wie am Handy: „Auswahl: 3 von 9 Buchungen" Inter 600 13px `#00d4aa`,
  `box-shadow:inset 2px 0 0 #00d4aa`, rechts „-39,40 €" Mono 600 15px
- **Auswahl-Leiste** ersetzt den Tastatur-Hinweis: `padding:14px 20px`, `#1a1a36`,
  Oberkante `#2a2a4a`, `box-shadow:0 -14px 40px rgba(0,0,0,.45)`. Links „3 ausgewählt"
  Inter 600 14px und „-39,40 €" Mono 600 16px; rechts „Geprüft" (primär, Orange),
  „Kategorie ändern", „Flag" (sekundär `#16162e`), „Auswahl aufheben" als Text
- Auswahl über Kästchen, `Leertaste` oder Shift-Klick für eine Spanne

Die Filter-Chips bleiben unverändert — eine laufende Auswahl übersteht Konto- und
Filterwechsel nicht mehr als in Ablauf 1 festgelegt.

## Interaktionen und Verhalten

**Handy**
| Geste | Wirkung |
|---|---|
| Tipp auf Chip „Kategorie" | öffnet die Kategorienauswahl als BottomSheet |
| Kästchen antippen | wählt aus, Zähler im Kopf und „Übernehmen (n)" rechnen mit |
| „Übernehmen" | filtert; Sheet schließt, Chip wandert nach vorn, Summenzeile erscheint |
| Tipp auf „×" am Chip | entfernt genau diesen Filter |
| „Alle Filter löschen" | setzt alle Filter zurück, Summenzeile verschwindet |
| Tipp auf „Versteckt (1)" | klappt versteckte Kategorien auf |
| Chips waagerecht wischen | weitere Chips; Leiste bleibt sticky unter dem Kopf |
| Langes Drücken auf eine Zeile | startet den Auswahl-Modus; Summenzeile zeigt die Auswahl |
| Kästchen antippen | Zeile zur Auswahl hinzunehmen oder herausnehmen |
| „Auswahl aufheben" | beendet den Auswahl-Modus, Tab-Bar kommt zurück |

**PC**
| Eingabe | Wirkung |
|---|---|
| Klick auf Chip | öffnet das Popover unter dem Chip, Liste bleibt sichtbar |
| `Esc` oder Klick außerhalb | schließt ohne zu filtern |
| Klick auf Payee-Vorschlag | filtert sofort (Einzelauswahl) |
| Klick auf „×" am Chip | entfernt diesen Filter |
| `↑ ↓` / `Leertaste` / `G` / `Enter` | wie in Ablauf 1 |
| Kästchen oder Shift-Klick | Auswahl bzw. Spanne; Auswahl-Leiste erscheint unten |

**Zustände, die jeder Screen braucht:** Ladezustand beim Nachladen (Loading),
Fehler (ErrorDisplay, Text benennt was passiert ist), **Leerzustand ohne Treffer**
— sagt, was als Nächstes zu tun ist, z. B. „Kein Treffer. Filter lockern oder alle
Filter löschen."

**Debounce** für Kategorie-, Payee-, Memo- und Betragssuche.

## Zustand

- `filters.categoryIds` — Set, **Mehrfachauswahl**; im Picker als Entwurf (`draft`)
  gehalten und erst auf „Übernehmen" übernommen (Click-Commit)
- `filters.dateRange` — `{ quick, from, to }`; `quick` schreibt `from`/`to` mit
- `filters.payeeId` — genau einer oder keiner
- `filters.amount`, `filters.memo`, `filters.flag`, `filters.showDeleted`
- `totals` — `{ count, sum }` vom Server über den gesamten Treffer
- `payeeSuggestions` — Namen **plus Anzahl**, berechnet innerhalb des aktuellen
  Treffers, nicht über alle Buchungen
- `categoryTree` — Gruppen; `hidden` getrennt und eingeklappt
- `page`, `hasMore` — 100 je Seite, Nachladen beim Scrollen
- `selectedAccountId` — Konto ist ein Filter; „Alle Konten" ist die Gesamtsicht
- `selection` — Set von Buchungs-IDs plus `selectionTotal` (`{ count, sum }`); solange
  gefüllt, zeigt die Summenzeile die Auswahl statt des Filters. Eine Mehrfachaktion
  ergibt **einen** Toast und **eine** Rücknahme

Filterwechsel setzt `page` zurück und scrollt nach oben; aktive Chips bleiben sichtbar.

## Design-Tokens

Alle Werte stammen aus dem Design-System-Bundle. Im HTML stehen sie als Literale, damit
die Screens sofort zeichnen — **im Code sind die Tokens zu verwenden**.

**Flächen:** `#08081a` App · `#111128` Card und Summenzeile · `#1a1a36` elevated
(Sheet, Popover, Tabellenkopf) · `#16162e` Input, Knöpfe, inaktive Chips · `#1e1e3c`
markierte Tabellenzeile · Ränder `#2a2a4a` normal, `#1c1c38` zart.

**Neon**
| Farbe | Wert | Bedeutung in Ablauf 2 |
|---|---|---|
| Orange | `#ff6b2c` → `#e55a1f` | primär: „Übernehmen"; Info-Badges „Noch nicht in YNAB" und „Keine Kategorie" |
| Teal | `#00d4aa` | aktive Chips, gesetzte Kästchen, aktives Konto, „Alle Filter löschen" |
| Green | `#00ff88` | positive Beträge, Status „Geprüft" |
| Red | `#ff3b5c` | negative Beträge |
| Purple | `#a855f7` | Kategorie |
| Pink | `#ff1f8f` | Status „Zu prüfen" |

**Text:** `#e8e8f0` primär · `#7a7a98` sekundär · `#4a4a68` tertiär und Overlines.

**Typografie:** Space Grotesk 600/700 (Titel 22px Handy, Sheet-/Popover-Kopf 17/15px,
Wortmarke 18px) · Inter 400/500/600 (10–15px) · JetBrains Mono 400/500/600 mit
`tabular-nums` für **alle** Zahlen, Beträge, Daten, Zähler, Overlines, Tastenkürzel.

**Radien:** 5px Kästchen · 10px Knopf, Input, Navigation · 12px Konto-Karte und
Symbolknopf · 14px Popover und Toast · 22px Sheet oben · 30px Geräterahmen · 999px
Chips, Badges.

**Glow, sparsam:** `0 0 18px rgba(0,212,170,.12)` aktive Karte ·
`text-shadow:0 0 12px rgba(0,255,136,.35)` aktiver positiver Betrag ·
`0 0 20px rgba(255,107,44,.5)` primärer Button.

**Schatten:** `0 -24px 60px rgba(0,0,0,.65)` Sheet · `0 24px 60px rgba(0,0,0,.65)`
Popover.

**Trefferflächen:** am Handy nie unter 44px — Chips 44px, Picker-Zeilen 48px,
Sheet-Knöpfe 48px, Symbolknöpfe 44 × 44.

## Neu zu bauende Komponenten

| Teil | Zusammengesetzt aus | Anmerkung |
|---|---|---|
| **Kategorienauswahl mit Mehrfachauswahl** | bestehende `Kategorienauswahl` + Kästchen je Zeile | neue Variante der bestehenden Karte: Kästchen, Zähler im Kopf, „Übernehmen (n)", „Versteckt (n)" eingeklappt |
| Filter-Popover (PC) | Card + Kategorienauswahl / Input | hängt unter seinem Chip, `Esc` schließt; ein Muster für Kategorie, Zeitraum, Payee |
| Filter-Chip-Leiste mit Zähler und „×" | Primitives-Chip + Aktivzustand Teal | Handy: sticky, scrollbar, aktive Chips nach vorn. PC: feste Reihenfolge, „Alle Filter löschen" am Ende |
| Summenzeile | Card + Money | nur bei aktivem Filter; am PC mit Filter im Klartext; zeigt die Auswahl, sobald eine läuft |
| Auswahl-Leiste | Card + Money + Button | beide Geräte; Anzahl, Summe der Auswahl, genau eine primäre Aktion. Am Handy ersetzt sie die Tab-Bar (Aktionen auf zwei Zeilen, je 44px), am PC den Tastatur-Hinweis |
| Schnellwahl Zeitraum | Primitives-Chip im 2 × 2-Raster + Input für Von/Bis | Schnellwahl schreibt Von/Bis |
| Payee-Vorschlagsliste | Input + Table-Zeilen + Money-Zähler | Anzahl je Payee aus dem aktuellen Treffer |
| Konto-Kürzel-Pill (Handy) | Badge | `ING`, `GIRO`, `BAR`, `ETF`, Mono 500 11px |
| Konto-Stände-Leiste (Handy) / Konten-Spalte (PC) | Card | „Alle Konten" zuoberst und in der Gesamtsicht aktiv |

## Assets

Keine Bilder. Alle Symbole sind Inline-SVG, `stroke`-basiert, `stroke-linecap:round`,
Strichstärke 1.8 bis 3.5 — im Code durch die `Icons`-Komponente zu ersetzen.
Schriften liegen als Variable Fonts im Bundle (Subset „latin", SIL OFL 1.1).

## Erfundene Daten

Nur diese Werte verwenden.

**Konten:** Girokonto Comdirect 2.418,37 € · ING Extra 6.902,10 € · Bar 63,50 € ·
Geldmarkt-ETF 12.004,88 € · Summe „Alle Konten" 21.388,85 €.

**Payees:** REWE Mitte, Bäckerei Krause, Stadtwerke Musterstadt, Deutsche Bahn, Amazon
Marketplace, Apotheke am Markt, Kita-Beitrag Stadt, Netflix, Bauhaus, Café Lotte.

**Kategorien:** Alltag (Lebensmittel, Haushalt, Restaurants) · Wohnen (Nebenkosten,
Möbel) · Mobilität (Bahn, Auto) · Familie (Kinder, Geschenke) · Abos (Streaming) ·
Gesundheit (Apotheke). Versteckt: „Umzug 2023".

**Trefferzahlen im Ablauf:** kein Filter → Kategorie (2) **312 · -9.874,20 €** →
\+ letzte 12 Monate **148 · -4.610,55 €** → \+ Payee Café Lotte **9 · -163,40 €**.
Payee-Vorschläge innerhalb der 148: Café Lotte 9 · REWE Mitte 78 · Bäckerei Krause 52.
Auswahl in 2.15 und 2.16: drei Café-Lotte-Buchungen -9,80 € + -18,40 € + -11,20 € =
**-39,40 €**.

## Voice

Deutsch, direkt, knapp, ohne Füllwörter. Du-Form. Buttons als Verb oder Verb+Objekt:
„Übernehmen", „Zurücksetzen", „Alle Filter löschen". Beträge sprechen für sich über
Vorzeichen und Farbe, Format `-48,12 €`. Fehlertexte benennen, was passiert ist.
Leerzustände sagen, was als Nächstes zu tun ist.

## Offene Punkte für Roman

1. **Zeitraum-Chip** trägt die Kurzform „12 Monate". Alternative wäre der Klartext
   „13.09.2025 – 13.09.2026", der aber die scrollbare Leiste am Handy dominiert.
2. **„Noch nicht in YNAB"** ist als Orange-Badge umgesetzt, wie „Keine Kategorie" aus
   Ablauf 1. Orange ist sonst der primären Aktion vorbehalten — falls das stört, käme
   Teal in Frage.
3. **Payee ist Einzelauswahl.** Mehrere Payees zugleich wären denkbar (dann dasselbe
   Kästchen-Muster wie bei Kategorie), sind aber im Brief nicht gefordert.
4. **Chip-Reihenfolge** wandert nur am Handy. Falls Roman auch am PC die aktiven Chips
   vorn will, ist das eine Einzeiler-Änderung.
