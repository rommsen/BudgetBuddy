# Handoff: Kassenbuch, Ablauf 3 „Mehrfachauswahl mit Summe"

> **Vorrang (2026-10-24):** Dieser Handoff legt den **Ablauf** fest: welche Elemente es
> gibt, welche Zustände, welche Reihenfolge, welche Texte. Die **Optik** (Farben, Flächen,
> Schriftgrößen, Abstände, Glow) kommt ausschließlich aus
> `standards/frontend/styleguide.md`, den Komponenten in `src/Client/DesignSystem/` und
> der Route `/styleguide`. Hex-Werte und Maße in diesem Dokument sind Stand der
> Design-Tafel und werden **nicht** in Code übernommen. Fehlt ein Baustein, entsteht er im
> Design-System aus Tokens und erscheint in `/styleguide`.

> **Herkunft.** Verfasst am 2026-09-18 im Repo, direkt aus der Design-Tafel
> `Ablauf 3 - Mehrfachauswahl mit Summe.dc.html` (Claude Design, Projekt
> „Gelesen UI-Mockups", `5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`). Anders als bei Ablauf 1
> und 2 liegt dort **kein** Übergabepaket `design_handoff_kassenbuch_ablauf3/`. Die
> beiden vorhandenen Handoffs im Repo sind wortgleiche Kopien ihrer Pakete, dieses hier
> ist aus den sieben Screens der Tafel abgelesen: Maße, Hex-Werte und Texte stammen aus
> dem HTML, die Verhaltensregeln aus den Screen-Notizen, dem Brief
> (`docs/ux/kassenbuch/briefs.md`, Ablauf 3) und der Backlog-Aufgabe
> `.agentheim/board/ledger/backlog/ledger-r1d2f-auswahl-modus-mit-summe.md`.
>
> Zwei bewusste Abweichungen zur Design-Fassung, wie in den Repo-Kopien der anderen
> Abläufe: die Verweise auf das mitgelieferte `_ds/`-Bundle sind gekürzt, und **DaisyUI
> ist gestrichen** (mit ADR 0031 aus dem Projekt entfernt; das Design-Projekt weiß davon
> nichts, beim nächsten Abgleich nicht zurückziehen).
>
> **Status: Referenz, nicht Code.** Die Werte hier sind verbindlich für die Umsetzung;
> die Quelle der Wahrheit für das Design-System selbst bleibt das Repo
> (`src/Client/DesignSystem/*.fs`, `src/Client/styles.css`, `standards/frontend/styleguide.md`).
> Dieses Dokument fließt nicht zurück nach Claude Design. Die Screens liegen bei Roman
> zur Abnahme.

## Überblick

Ablauf 3 ist die dritte lesende Scheibe des Kassenbuchs. Sie beantwortet eine Frage:
was macht eine Teilmenge des Treffers zusammen aus? Der Anlass ist die Steuererklärung.
Roman filtert auf eine Kategorie, markiert die letzten zwölf Buchungen und liest den
Gesamtbetrag ab.

Der häufige Fall braucht dafür gar keine Auswahl: die Summenzeile aus Ablauf 2 steht
schon über der Liste und rechnet über den **gesamten** Treffer, auch über die noch nicht
geladenen Seiten. Schritt 1 ist damit oft bereits das Ziel. Diese Scheibe ergänzt nur die
Teilmenge.

Der YNAB-Weg (Roman, 2026-09-11): im Web nach Kategorie filtern, Zeilen einzeln
ankreuzen, Summe unten ablesen. Am Handy geht das gar nicht. Genau das ist der Schmerz.

Zwei Geräte, gleichwertig:
- **Handy 390 × 844**: Screens 3.1 bis 3.4, Auswahl per langem Drücken und Tipp
- **PC 1440 × 900**: Screens 3.5 bis 3.7, Auswahl per Kästchen, Kopfzeilen-Kästchen und
  Shift-Klick

Ausgangszustand in allen sieben Screens ist das Ende von Ablauf 2: Gesamtsicht
„Alle Konten", aktive Chips „Kategorie: 1" (Kinder) und „Dieses Jahr", Summenzeile
**„34 Buchungen · -2.140,00 €"**.

Auswahl-Modus, Auswahl-Leiste und die Umschaltung der Summenzeile kommen unverändert aus
den Screens 2.15 und 2.16. Neu gegenüber Ablauf 2 sind drei Dinge: die Aktion „Alle" am
Handy, das Kopfzeilen-Kästchen am PC mit seinem Teilauswahl-Strich und die Spanne per
Shift-Klick mit sichtbarem Anker.

## Zu den Design-Dateien

Die Tafel ist eine **Design-Referenz in HTML**, ein Prototyp, der Aussehen und Verhalten
festhält. Sie ist **kein Produktionscode zum Übernehmen**.

Nachzubauen in der bestehenden Umgebung: F# / Fable / Elmish mit Tailwind 4 (**ohne
DaisyUI**, ADR 0031), mit den Komponenten aus `src/Client/DesignSystem/*.fs` und den
Tokens aus `src/Client/styles.css`. Nicht das HTML einbetten, sondern die vorhandenen
Komponenten benutzen. Die Tafel nennt selbst: `Table` mit Auswahlzeilen (`IsSelected`),
`Money` für Summen und Beträge, `Badge` für den Status, `Button` für die Leisten-Aktionen,
`Card` für die Konten, `Navigation` für Tab-Bar und Kopfzeile, `Primitives` für Kästchen
und Chips, `Icons`.

Mockups fließen **nie** zurück in den Code. Änderungen am Design-System entstehen im Repo
und werden per `npm run export:design-system` neu exportiert.

## Fidelity

**Hi-fi.** Farben, Typografie, Abstände, Zustände und Texte sind endgültig und entstammen
dem gebundenen BudgetBuddy Design System. Maße sind pixelgenau gemeint.

Die Tafel selbst wird **nicht** implementiert: Kopf, Screen-Beschriftungen („3.1" bis
„3.7"), die Notiztexte unter den Screens, die Karten „Annahmen" / „Komponenten aus dem
Design-System" / „Zusammengesetzt, weil Komponente fehlt" und die drei Schalter der Tafel
(`notizen`, `tastaturHinweis`, `geraete` mit „Beide" / „Nur Handy" / „Nur PC") sind
Dokumentation.

Auch der Geräterahmen ist Tafel: Handy `radius:30px`, PC `radius:14px`, je
`border:1px solid #2a2a4a`. Im Code gibt es keinen Rahmen.

## Grundlage, die für alle Screens gilt

**Aufbau von oben nach unten, beide Geräte:**
1. Kontostände (Handy: waagerecht scrollbare Karten-Leiste; PC: linke Spalte 268px)
2. Filterleiste aus Chips
3. Summenzeile, in zwei Zuständen
4. Liste (Handy) bzw. Tabelle mit stickyem Kopf (PC)
5. Unten eine Leiste: ohne Auswahl die Tab-Bar (Handy) bzw. der Tastatur-Hinweis (PC),
   mit Auswahl die Auswahl-Leiste

**Die Summenzeile hat zwei Zustände, und nur einen davon zur selben Zeit.** Ohne Auswahl
zeigt sie den Treffer (neutral: Grund `#111128`, Unterkante `#1c1c38`, Label
`#7a7a98`). Sobald eine Auswahl läuft, zeigt sie die Auswahl und trägt die Teal-Kante
(Unterkante `rgba(0,212,170,.3)`, `box-shadow:inset 2px 0 0 #00d4aa`, Label `#00d4aa`).
Der Text lautet dann „Auswahl: n von 34 Buchungen".

**Zahlen kommen vom Server.** Trefferzahl und Summe gelten über den gesamten Treffer,
nicht über die geladene Seite. Das gilt auch für die Summe der Auswahl: „Alle" am Handy
und das Kopfzeilen-Kästchen am PC ergeben 34 Buchungen und -2.140,00 €, obwohl nur zwölf
Zeilen geladen sind.

**Genau eine primäre Aktion je Screen.** In der Auswahl-Leiste ist das „Geprüft" in
Orange. Auf 3.1 und 3.5 gibt es kein Orange, weil es nichts zu tun gibt.

**Kein Zähler ohne Bezug.** Anzahl und Summe stehen immer als Paar, in der Summenzeile
wie in der Auswahl-Leiste. Die Leiste wiederholt die Zahl absichtlich: sie steht dort in
größerer Schrift (Betrag Mono 600 16px) und ist dort, wo der Finger ist.

**Chips.** Aktiv: `rgba(0,212,170,.12)`, Rand `rgba(0,212,170,.4)`, Inter 600 13px
`#00d4aa`, dazu ein „×" (13px, Strichstärke 2.6). Inaktiv: `#16162e`, Rand `#2a2a4a`,
Inter 500 13px `#7a7a98`. Am Handy `padding:12px 14px` mit `line-height:20px` (44px
Trefferfläche), am PC `padding:9px 13px`. „Alle Filter löschen" ist am PC ein Textknopf
am Ende der Leiste (`margin-left:auto`, Inter 600 13px `#00d4aa`), am Handy ein Chip mit
Grund `#16162e`, Rand `#2a2a4a` und Teal-Label.

Die Filter-Chips bleiben während der Auswahl unverändert. Ob eine laufende Auswahl einen
Konto- oder Filterwechsel übersteht, legt die Tafel nicht fest (siehe Offene Punkte).

### Verbindliche Entscheidungen

Diese fünf Punkte sind entschieden und nicht mehr zur Diskussion gestellt:

1. **Das Kopfzeilen-Kästchen am PC wählt den gesamten Treffer**, nicht die geladene
   Seite. Fachlich heißt das: die Auswahl ist eine Menge über den Treffer, keine Liste
   geladener Ids.
2. **Bei Teilauswahl zeigt das Kopfzeilen-Kästchen einen Teal-Strich statt des Hakens**
   (9 × 2,5px, `radius:2px`, `#00d4aa`, im Kästchen `background:rgba(0,212,170,.18)`,
   Rand `#00d4aa`).
3. **Die Shift-Klick-Spanne markiert ihren Anker** mit
   `box-shadow:inset 2px 0 0 #00d4aa` auf der Zeile.
4. **Der Ausstieg heißt überall „Auswahl aufheben", nie „Fertig".** Auf beiden Geräten,
   als Textknopf ohne Grund und ohne Rand, Inter 600 14px `#7a7a98`.
5. **Am Handy ist „Alle" die vierte Aktion**, in der dritten Reihe der Leiste: Flag,
   Alle, Auswahl aufheben. **Es gibt keine Aktion „Kopieren"** (war eine Erfindung im
   Brief, von Roman am 2026-09-14 gestrichen; Roman liest die Summe ab). Am PC fehlt
   „Alle" in der Leiste, weil das Kopfzeilen-Kästchen dieselbe Aufgabe erledigt.

## Screens Handy

Gemeinsames Gerüst von 3.1 bis 3.4, 390 × 844, Grund `#08081a`:

| Block | Maße | Inhalt |
|---|---|---|
| Statusleiste | `padding:12px 24px 4px` | „9:41" Mono 600 13px `#7a7a98`; rechts zwei Anzeigen (16 × 9px und 22 × 10px, Rand `#4a4a68`, Akku zu 70 % `#00ff88`) |
| Kopf | `padding:4px 16px 12px` | Overline „KASSENBUCH" Mono 600 10px `letter-spacing:.16em` `#4a4a68`; Titel **„Alle Konten"** Space Grotesk 600 22px `#e8e8f0` + Chevron 16px `#00d4aa` (Konto-Wechsler); rechts zwei Knöpfe 44 × 44, `radius:12px`, `#16162e`, Rand `#2a2a4a` (Lupe, Filter), Symbole 19px `#7a7a98` Strichstärke 1.8 |
| Kontostände | `padding:0 16px 14px`, `gap:10px` | Karten 116px, `radius:12px`, `padding:9px 11px`. Aktiv („Alle Konten"): `#1a1a36`, Rand `rgba(0,212,170,.45)`, `box-shadow:0 0 18px rgba(0,212,170,.12)`, Name Inter 600 11px `#00d4aa`, Betrag Mono 600 13px `tabular-nums` `#00ff88` mit `text-shadow:0 0 12px rgba(0,255,136,.35)`, `margin-top:5px`. Inaktiv: `#111128`, Rand `#2a2a4a`, Name Inter 500 11px `#7a7a98` |
| Chips | `padding:0 16px 12px`, `gap:8px`, Unterkante `#1c1c38` | sichtbar sind „Kategorie: 1" ×, „Dieses Jahr" ×, „Alle Filter löschen" |
| Summenzeile | `padding:11px 16px`, Grund `#111128` | zwei Zustände, siehe unten |
| Datumskopf | `padding:14px 16px 6px` | Mono 600 10px `letter-spacing:.16em` `#4a4a68` |
| Listenzeile | `padding:13px 16px`, Unterkante `#1c1c38` | Zeile 1 (`align-items:baseline`, `gap:10px`): Payee Inter 600 15px `#e8e8f0` + Betrag Mono 600 15px `tabular-nums` `#ff3b5c`. Zeile 2 (`margin-top:4px`, `gap:8px`): Konto-Kürzel-Pill Mono 500 11px `padding:1px 6px` `radius:999px` `#16162e` Rand `#2a2a4a` `#7a7a98` · Kategorie Inter 500 12px `#a855f7` (Kurzform „Kinder") · Memo Inter 400 12px `#4a4a68` |

Die Karten-Leiste steht auf der Tafel in der Reihenfolge Alle Konten · ING Extra ·
Girokonto Comdirect · Bar. Am PC ist die Konten-Spalte anders sortiert (Girokonto vor
ING Extra), und Geldmarkt-ETF fehlt am Handy ganz. Ob die Karten-Leiste bewusst nach
Betrag sortiert und der ETF nur aus dem sichtbaren Bereich gescrollt ist, steht nicht auf
der Tafel.

Die Chip-Leiste zeigt am Handy nur die drei genannten Chips. „Zu prüfen (7)", Payee,
Betrag und Memo liegen außerhalb (`overflow:hidden`), passend zur Regel aus Ablauf 2:
aktive Chips wandern am Handy nach vorn.

**Auswahl-Modus, Zeile und Kästchen.** Im Auswahl-Modus wird die Zeile zu
`display:flex; align-items:center; gap:12px` mit dem Kästchen links: 20 × 20,
`radius:5px`. Ungesetzt `#16162e`, Rand `#2a2a4a`. Gesetzt: Grund und Rand `#00d4aa`,
Häkchen 13px `#08081a` Strichstärke 3.5. Gewählte Zeile: Grund `rgba(0,212,170,.07)`.
Eine Zeilenhöhe nennt die Tafel nicht, sie ergibt sich aus `padding:13px` und zwei
Textzeilen (in Ablauf 2 waren dafür 70px angesetzt).

**Auswahl-Leiste Handy**, ersetzt die Tab-Bar: `position:absolute`, Boden,
`padding:12px 16px 22px`, `#1a1a36`, Oberkante `#2a2a4a`,
`box-shadow:0 -14px 40px rgba(0,0,0,.45)`, `flex-direction:column`, `gap:10px`.

| Reihe | Inhalt |
|---|---|
| 1 | „n ausgewählt" Inter 600 14px `#e8e8f0` · Betrag Mono 600 16px `tabular-nums` `#ff3b5c` (`justify-content:space-between`) |
| 2 | **„Geprüft"** primär (`linear-gradient(135deg,#ff6b2c,#e55a1f)`, `#fff`, `radius:10px`, `box-shadow:0 0 20px rgba(255,107,44,.5)`) + „Kategorie ändern" sekundär (`#16162e`, Rand `#2a2a4a`, `#e8e8f0`), je `flex:1`, `min-height:44px` |
| 3 | „Flag" (`flex:0 0 74px`, sekundär) + **„Alle"** (`flex:0 0 74px`, Label `#00d4aa`) + „Auswahl aufheben" (`flex:1`, Text `#7a7a98`), je `min-height:44px`, `gap:8px` |

„Alle" trägt den Aktivzustand als Chip-Farbe: inaktiv `#16162e` mit Rand `#2a2a4a`,
aktiv `rgba(0,212,170,.12)` mit Rand `rgba(0,212,170,.4)`. Nochmal tippen hebt die
Auswahl auf.

Die Notiz auf der Tafel gibt der Leiste **165px** Höhe (drei Reihen à 44px). Als
CSS-Wert steht die Höhe nicht im HTML, sie ergibt sich aus Polstern, Reihen und Abständen
(gerechnet etwa 163px). Die Backlog-Aufgabe rechnet mit 165px und 411px Rest für die
Liste; kommen die zwei Flag-Arten aus Ablauf 5 dazu, wächst die Leiste auf 197px.

### 3.1 Treffer mit Summe, ohne Auswahl

**Zweck:** Schritt 1. Die Summe steht da, ohne dass Roman etwas ankreuzt.

Summenzeile im Treffer-Zustand: Unterkante `#1c1c38`, links „34 Buchungen" Inter 500
12px `#7a7a98`, rechts **„-2.140,00 €"** Mono 600 14px `tabular-nums` `#ff3b5c`.

Unten die **Tab-Bar**: `position:absolute`, Boden, `padding:10px 8px 22px`,
`background:rgba(8,8,26,.92)`, Oberkante `#1c1c38`, `justify-content:space-around`.
Vier Einträge, je `min-width:64px`, `min-height:44px`, `gap:5px`, Symbol 21px, Label
Inter 600 10px: Kassenbuch (aktiv, `#00d4aa`) · Quick Add · Sync · Mehr (`#4a4a68`).

**Liste:** 09.09. Kita-Beitrag Stadt `GIRO` Kinder -125,00 € · 05.09. Amazon Marketplace
`GIRO` Kinder Turnbeutel -42,80 € · 01.09. Bauhaus `GIRO` Kinder Regal Kinderzimmer
-118,00 € · 28.08. Apotheke am Markt `BAR` Kinder -23,60 €.

Vier der 34 Treffer sind sichtbar, der Rest kommt beim Scrollen. Kein Orange auf diesem
Screen.

### 3.2 Auswahl-Modus, eine Zeile

**Zweck:** zeigen, wie der Modus beginnt und was er mit der Summenzeile macht.

**Langes Drücken** auf eine Zeile startet den Modus. Kästchen erscheinen vor jeder Zeile,
die Summenzeile springt vom Treffer auf die Auswahl, die Auswahl-Leiste ersetzt die
Tab-Bar.

Summenzeile: „Auswahl: 1 von 34 Buchungen" Inter 600 12px `#00d4aa`, rechts
**„-125,00 €"** Mono 600 14px `#ff3b5c`.

Gewählt ist 09.09. Kita-Beitrag Stadt -125,00 €. 05.09. und 01.09. tragen leere Kästchen.
Leiste: „1 ausgewählt · -125,00 €", „Alle" inaktiv.

Drei Zeilen sind sichtbar, die vierte liegt hinter der Leiste.

### 3.3 „Alle", 34 ausgewählt

**Zweck:** der Beweis, dass „Alle" den gesamten Treffer greift.

Summenzeile: „Auswahl: 34 von 34 Buchungen" · **„-2.140,00 €"**. Die Summe der Auswahl
stimmt mit der Trefferzahl und der Treffersumme aus 3.1 überein, obwohl 22 der 34
Buchungen nicht geladen sind.

Alle drei sichtbaren Zeilen sind gesetzt (Grund `rgba(0,212,170,.07)`, Häkchen).
Leiste: „34 ausgewählt · -2.140,00 €". Der Knopf „Alle" trägt jetzt den Aktivzustand in
Teal.

### 3.4 Teilmenge, 12 ausgewählt

**Zweck:** derselbe Steuerfall wie am PC (3.7), nur per Tipp zusammengesucht.

Summenzeile: „Auswahl: 12 von 34 Buchungen" · **„-780,00 €"**.

Der Screen zeigt das untere Ende der Auswahl, damit die Grenze sichtbar ist:

| Datumskopf | Zeile | Kästchen |
|---|---|---|
| 08.07. | Amazon Marketplace `GIRO` Kinder -55,00 € | gesetzt |
| 02.07. | Deutsche Bahn `GIRO` Kinder Fahrt Oma -61,30 € | gesetzt (die zwölfte und letzte) |
| 25.06. | Bauhaus `GIRO` Kinder -48,20 € | leer, steht außerhalb der Auswahl |

Leiste: „12 ausgewählt · -780,00 €", „Alle" inaktiv. Am Handy gibt es keine Spanne wie am
PC, die zwölf kommen per Tipp zusammen. „Auswahl aufheben" führt zurück auf 3.1, die
Summenzeile zeigt dann wieder -2.140,00 €.

## Screens PC

Gemeinsames Gerüst von 3.5 bis 3.7, 1440 × 900, Grund `#08081a`:

**Kopfleiste** 64px, `padding:0 24px`, `rgba(8,8,26,.85)`, Unterkante `#1c1c38`:
Logo 40 × 40 `radius:12px` `linear-gradient(135deg,#ff6b2c,#00d4aa)` mit „B" Space
Grotesk 700 20px `#fff`, Wortmarke „BudgetBuddy" Space Grotesk 700 18px; rechts `gap:6px`
Kassenbuch (aktiv, `rgba(0,212,170,.1)`, Inter 600 14px `#00d4aa`, `padding:10px 14px`,
`radius:10px`) · Sync · Regeln · Einstellungen (Inter 500 14px `#4a4a68`).

**Konten-Spalte** 268px, `#08081a`, rechte Kante `#1c1c38`, `padding:18px 14px`,
`gap:4px`: Overline „KONTEN" Mono 600 10px `letter-spacing:.16em` `#4a4a68`
(`padding:0 10px 10px`), Zeilen `padding:11px 12px`, `radius:10px`, Name Inter 500 13px
`#7a7a98` (ellipsis, `nowrap`), Betrag Mono 600 13px `tabular-nums` `#00ff88` (`nowrap`).
**„Alle Konten" zuoberst und aktiv**: `#1a1a36`, `box-shadow:inset 2px 0 0 #00d4aa`,
Name Inter 600 13px `#e8e8f0`, Betrag mit `text-shadow:0 0 12px rgba(0,255,136,.35)`.
Dann Girokonto Comdirect · ING Extra · Bar · Geldmarkt-ETF.

**Chip-Leiste** `padding:14px 20px`, `gap:8px`, Unterkante `#1c1c38`, Reihenfolge fest:
Zu prüfen (7) inaktiv · **Kategorie: 1** aktiv + × · **Dieses Jahr** aktiv + × · Payee ·
Betrag · Memo · „Alle Filter löschen" rechts. Ablauf 2 nannte am PC acht Chips; die Tafel
zu Ablauf 3 zeigt nur diese sechs, „Flag" und „Gelöschte zeigen" fehlen. Ob das eine
Kürzung für die Tafel ist oder eine Entscheidung, steht dort nicht.

**Tabelle**, Spaltenraster `44px 96px 104px 1fr 196px 1fr 132px 128px` = Kästchen ·
Datum · Konto · Payee · Kategorie · Memo · Betrag (rechts) · Status (rechts).
- Sticky Kopf 42px, `#1a1a36`, Unterkante `#2a2a4a`, Mono 600 10px
  `letter-spacing:.12em` `#4a4a68`, `padding:0 20px`
- Zeilen 52px, Unterkante `#1c1c38`, `padding:0 20px`: Datum Mono 500 13px `#7a7a98`,
  Konto Mono 500 12px `#7a7a98` („Girokonto", „Bar"), Payee Inter 600 14px `#e8e8f0`,
  Kategorie Inter 500 13px `#a855f7` **voll qualifiziert** („Familie: Kinder"), Memo
  Inter 400 13px `#7a7a98`, Betrag Mono 600 14px `tabular-nums` `#ff3b5c`
- Kästchen je Zeile 17 × 17, `radius:5px`; ungesetzt `#16162e` Rand `#2a2a4a`, gesetzt
  Grund und Rand `#00d4aa` mit Häkchen 11px `#08081a` Strichstärke 3.5
- Gewählte Zeile: Grund `rgba(0,212,170,.07)`
- Status-Badges `padding:3px 9px`, `radius:999px`, Inter 500 11px: **„Geprüft"**
  (`rgba(0,255,136,.1)`, Rand `rgba(0,255,136,.3)`, `#00ff88`) · **„Nicht in YNAB"**
  (`rgba(255,107,44,.1)`, Rand `rgba(255,107,44,.3)`, `#ff6b2c`)

**Die zwölf geladenen Zeilen** sind auf allen drei PC-Screens dieselben, alle mit
Kategorie „Familie: Kinder":

| # | Datum | Konto | Payee | Memo | Betrag | Status |
|---|---|---|---|---|---|---|
| 1 | 09.09. | Girokonto | Kita-Beitrag Stadt | ohne | -125,00 € | Geprüft |
| 2 | 05.09. | Girokonto | Amazon Marketplace | Turnbeutel | -42,80 € | Geprüft |
| 3 | 01.09. | Girokonto | Bauhaus | Regal Kinderzimmer | -118,00 € | Geprüft |
| 4 | 28.08. | Bar | Apotheke am Markt | ohne | -23,60 € | Nicht in YNAB |
| 5 | 21.08. | Girokonto | Amazon Marketplace | ohne | -34,90 € | Geprüft |
| 6 | 12.08. | Girokonto | Kita-Beitrag Stadt | ohne | -125,00 € | Geprüft |
| 7 | 06.08. | Girokonto | REWE Mitte | Pausensnacks | -27,90 € | Geprüft |
| 8 | 30.07. | Girokonto | Amazon Marketplace | ohne | -38,40 € | Geprüft |
| 9 | 22.07. | Bar | Apotheke am Markt | ohne | -31,60 € | Geprüft |
| 10 | 15.07. | Girokonto | Bauhaus | ohne | -96,50 € | Geprüft |
| 11 | 08.07. | Girokonto | Amazon Marketplace | ohne | -55,00 € | Geprüft |
| 12 | 02.07. | Girokonto | Deutsche Bahn | Fahrt Oma | -61,30 € | Geprüft |

**Die Summe dieser zwölf ergibt genau -780,00 €.** Nachgerechnet, sie geht auf.

**Auswahl-Leiste PC** (3.6 und 3.7), ersetzt den Tastatur-Hinweis: `padding:14px 20px`,
`#1a1a36`, Oberkante `#2a2a4a`, `box-shadow:0 -14px 40px rgba(0,0,0,.45)`, `gap:16px`.
Links „n ausgewählt" Inter 600 14px `#e8e8f0` und der Betrag Mono 600 16px `tabular-nums`
`#ff3b5c`. Rechts (`margin-left:auto`, `gap:10px`): „Geprüft" primär
(`padding:11px 18px`, `radius:10px`, Orange-Verlauf, Glow, `#fff`) · „Kategorie ändern"
und „Flag" sekundär (`padding:11px 16px`, `#16162e`, Rand `#2a2a4a`, `#e8e8f0`) ·
„Auswahl aufheben" als Text (`padding:11px 14px`, `#7a7a98`). Kein „Alle": das
Kopfzeilen-Kästchen erledigt das.

### 3.5 Treffer mit Summe, ohne Auswahl

**Zweck:** Schritt 1 am PC. Kein Kästchen ist gesetzt, die Summe steht trotzdem da.

Summenzeile im Treffer-Zustand: `padding:12px 20px`, `#111128`, Unterkante `#1c1c38`,
links „34 Buchungen" Inter 500 13px `#7a7a98`, rechts **„-2.140,00 €"** Mono 600 15px
`tabular-nums` `#ff3b5c`.

Die Summenzeile nennt hier **nur** Anzahl und Summe. Ablauf 2 hatte am PC die
ausgeschriebene Variante („312 Buchungen · alle Konten · Lebensmittel, Restaurants").
Welche der beiden gilt, klärt die Tafel nicht (siehe Offene Punkte).

Kopfzeilen-Kästchen leer: 17 × 17, `#16162e`, Rand `#2a2a4a`.

Alle zwölf geladenen Zeilen sind sichtbar, die übrigen 22 kommen beim Scrollen. Unten der
**Tastatur-Hinweis** als Fußzeile: `padding:12px 20px`, `#08081a`, Oberkante `#1c1c38`,
`gap:16px`, Inter 500 12px `#4a4a68`, Tasten Mono `#7a7a98`: `↑ ↓` bewegen ·
`Leertaste` auswählen · `G` geprüft · `Enter` Detail öffnen.

### 3.6 Kopfzeilen-Kästchen, alle 34

**Zweck:** die Entscheidung sichtbar machen, dass das Kopfzeilen-Kästchen den ganzen
Treffer wählt.

Kopfzeilen-Kästchen gesetzt: Grund und Rand `#00d4aa`, Häkchen 11px `#08081a`
Strichstärke 3.5.

Summenzeile im Auswahl-Zustand: Unterkante `rgba(0,212,170,.3)`,
`box-shadow:inset 2px 0 0 #00d4aa`, links „Auswahl: 34 von 34 Buchungen" Inter 600 13px
`#00d4aa`, rechts **„-2.140,00 €"** Mono 600 15px.

Alle sichtbaren Zeilen tragen Grund `rgba(0,212,170,.07)` und ein gesetztes Kästchen.
Die Auswahl-Leiste ersetzt den Tastatur-Hinweis und zeigt „34 ausgewählt · -2.140,00 €".
Elf Zeilen sind sichtbar, die zwölfte (02.07., -61,30 €) liegt hinter der Leiste, die
Summe rechnet sie mit.

### 3.7 Spanne per Shift-Klick, 12 von 34

**Zweck:** der Steuerfall. Klick auf das erste Kästchen, Shift-Klick auf das zwölfte.

Kopfzeilen-Kästchen im **Teilauswahl-Zustand**: `background:rgba(0,212,170,.18)`, Rand
`1px solid #00d4aa`, darin ein Strich 9 × 2,5px, `radius:2px`, `#00d4aa`. Kein Haken.

Summenzeile: „Auswahl: 12 von 34 Buchungen" · **„-780,00 €"**.

**Der Anker der Spanne** (Zeile 1, 09.09.) trägt zusätzlich zum Auswahl-Grund
`box-shadow:inset 2px 0 0 #00d4aa`, dieselbe Teal-Kante wie das aktive Konto in der
Spalte und die Summenzeile im Auswahl-Zustand. Die übrigen elf gewählten Zeilen tragen
nur den Grund.

Auswahl-Leiste: „12 ausgewählt · -780,00 €". Elf der zwölf gewählten Zeilen sind
sichtbar, die zwölfte (02.07., -61,30 €) liegt hinter der Leiste. „Auswahl aufheben"
führt zurück auf 3.5, die Summenzeile zeigt wieder -2.140,00 €.

## Interaktionen und Verhalten

**Handy**

| Geste | Wirkung |
|---|---|
| Langes Drücken auf eine Zeile | startet den Auswahl-Modus, Kästchen erscheinen links, Summenzeile springt auf die Auswahl, Auswahl-Leiste ersetzt die Tab-Bar |
| Kästchen oder Zeile antippen | nimmt die Zeile in die Auswahl oder heraus; Anzahl und Summe rechnen mit |
| „Alle" | wählt den gesamten Treffer, auch die nicht geladenen Seiten; der Knopf wird Teal |
| „Alle" nochmal | hebt die Auswahl auf |
| „Auswahl aufheben" | beendet den Modus, die Tab-Bar kommt zurück, die Summenzeile zeigt wieder den Treffer |
| Liste scrollen | lädt nach; die Summe bleibt die des Treffers bzw. der Auswahl |

Ob ein Tipp auf die Zeile (nicht auf das Kästchen) im Auswahl-Modus auswählt oder das
Detail öffnet, legt die Tafel nicht fest. Die Zeile ist im HTML als Ganzes gestaltet,
ohne getrennte Trefferflächen.

**PC**

| Eingabe | Wirkung |
|---|---|
| Klick auf ein Zeilen-Kästchen | wählt die Zeile; Auswahl-Leiste erscheint unten und ersetzt den Tastatur-Hinweis |
| Shift-Klick auf ein zweites Kästchen | wählt die Spanne dazwischen; der Anker behält die Teal-Kante |
| Klick auf das Kopfzeilen-Kästchen | wählt den gesamten Treffer (34 von 34) |
| Kopfzeilen-Kästchen bei laufender Teilauswahl | zeigt den Teal-Strich; der Klick darauf ist auf der Tafel nicht festgelegt (aufheben oder auf alle erweitern) |
| `↑ ↓` / `Leertaste` / `G` / `Enter` | wie in Ablauf 1 und 2 |
| „Auswahl aufheben" | leert die Auswahl, Summenzeile zeigt wieder den Treffer, Tastatur-Hinweis kommt zurück |

**Zustände, die jeder Screen braucht** und die die Tafel nicht zeichnet: Ladezustand beim
Nachladen (Loading), Fehler (ErrorDisplay, Text benennt was passiert ist), Leerzustand
ohne Treffer. Sie gelten unverändert aus Ablauf 2.

**Schreibende Aktionen** („Geprüft", „Kategorie ändern", „Flag") sind in der Leiste
gezeichnet, gehören aber **nicht** in diese Scheibe: sie hängen an der v2-Entscheidung
(siehe `ledger-r1d2f`). In dieser Scheibe bleibt es beim Lesen. Wenn sie kommen, gilt die
Regel aus Ablauf 2: eine Mehrfachaktion ergibt **einen** Toast und **eine** Rücknahme.

## Zustand

- `totals`: `{ count, sum }` vom Server über den gesamten Treffer, unabhängig von der
  geladenen Seite. Speist die Summenzeile im Treffer-Zustand
- `selection`: die Auswahl als **Menge über den Treffer**, nicht als Liste geladener
  Ids. Die Tafel verlangt das, weil „Alle" und das Kopfzeilen-Kästchen 34 Buchungen
  wählen, während zwölf geladen sind. Praktisch heißt das: ein Zustand „alles gewählt,
  außer diesen Ids" muss darstellbar sein
- `selectionTotal`: `{ count, sum }` der Auswahl, ebenfalls vom Server, sobald die
  Auswahl über die geladenen Zeilen hinausgeht
- `selectionAnchor`: die Zeile, von der die letzte Spanne ausging; trägt die Teal-Kante
  und ist der Bezugspunkt des nächsten Shift-Klicks
- `selectionMode` (Handy): ob Kästchen und Auswahl-Leiste sichtbar sind; wird durch
  langes Drücken gesetzt und durch „Auswahl aufheben" geräumt
- Abgeleitet: der Zustand des Kopfzeilen-Kästchens ist leer / Strich / Haken aus
  `selection.count` gegen `totals.count`
- Solange `selection` gefüllt ist, zeigt die Summenzeile die Auswahl statt des Treffers
- `filters`, `page`, `hasMore`, `selectedAccountId` unverändert aus Ablauf 2

Wie sich die Mengen-Auswahl mit Nachladen und Shift-Klick-Spannen verträgt, gehört in die
Verfeinerung der Backlog-Aufgabe. Ebenso, ob eine laufende Auswahl einen Konto- oder
Filterwechsel übersteht.

## Design-Tokens

Alle Werte stammen aus dem Design-System-Bundle. Im HTML stehen sie als Literale, damit
die Screens sofort zeichnen. **Im Code sind die Tokens zu verwenden.**

**Flächen:** `#08081a` App und Konten-Spalte · `#111128` Summenzeile und inaktive
Konto-Karte · `#1a1a36` elevated (Tabellenkopf, Auswahl-Leiste, aktive Konto-Karte,
aktive Konto-Zeile) · `#16162e` Knöpfe, Kästchen, inaktive Chips · Ränder `#2a2a4a`
normal, `#1c1c38` zart.

**Neon**

| Farbe | Wert | Bedeutung in Ablauf 3 |
|---|---|---|
| Orange | `#ff6b2c` → `#e55a1f` | primäre Aktion „Geprüft"; Status-Badge „Nicht in YNAB" |
| Teal | `#00d4aa` | die Auswahl: gesetzte Kästchen, Teilauswahl-Strich, Kante der Summenzeile, Anker der Spanne, aktives Konto, aktive Chips, „Alle" im Aktivzustand |
| Green | `#00ff88` | positive Beträge (Kontostände), Status „Geprüft" |
| Red | `#ff3b5c` | negative Beträge, Treffersumme, Auswahl-Summe |
| Purple | `#a855f7` | Kategorie |

Pink (`#ff1f8f`, Status „Zu prüfen") kommt auf dieser Tafel nicht vor, weil alle zwölf
geladenen Zeilen geprüft sind oder „Nicht in YNAB" tragen.

**Teal in einer Rolle.** Auf dieser Tafel bedeutet Teal fast überall dasselbe: Auswahl
oder aktiver Filter. Die Kante `box-shadow:inset 2px 0 0 #00d4aa` trägt drei Dinge (das
aktive Konto, die Summenzeile im Auswahl-Zustand, den Anker der Spanne), immer mit der
Bedeutung „das hier ist der Bezugspunkt".

**Text:** `#e8e8f0` primär · `#7a7a98` sekundär · `#4a4a68` tertiär und Overlines.

**Typografie:** Space Grotesk 600/700 (Titel 22px Handy, Wortmarke 18px, „B" im Logo
20px) · Inter 400/500/600 (10 bis 15px) · JetBrains Mono 400/500/600 mit `tabular-nums`
für **alle** Zahlen, Beträge, Daten, Zähler, Overlines, Tastenkürzel.

**Radien:** 5px Kästchen · 10px Knopf und Navigation · 12px Konto-Karte und Symbolknopf ·
999px Chips, Badges, Konto-Pill · 14px und 30px nur am Geräterahmen der Tafel.

**Glow, sparsam:** `0 0 18px rgba(0,212,170,.12)` aktive Konto-Karte ·
`text-shadow:0 0 12px rgba(0,255,136,.35)` Betrag des aktiven Kontos ·
`0 0 20px rgba(255,107,44,.5)` primärer Knopf.

**Schatten:** `0 -14px 40px rgba(0,0,0,.45)` Auswahl-Leiste, beide Geräte.

**Trefferflächen:** am Handy nie unter 44px. Chips 44px über `padding:12px 14px` mit
`line-height:20px`, Leisten-Knöpfe `min-height:44px`, Symbolknöpfe 44 × 44, Tab-Einträge
`min-width:64px` / `min-height:44px`. Das Kästchen in der Zeile ist mit 20 × 20 kleiner
als 44px; seine Trefferfläche ist die Zeile.

## Neu zu bauende Komponenten

| Teil | Zusammengesetzt aus | Anmerkung |
|---|---|---|
| **Summenzeile in zwei Zuständen** | Card + Money | Treffer-Zustand neutral, Auswahl-Zustand mit Teal-Kante und Teal-Label. Ein Bauteil, zwei Zustände, nie beide zugleich |
| **Auswahl-Leiste** | Card + Money + Button | Am Handy dreizeilig mit 44px-Reihen, ersetzt die Tab-Bar; am PC einzeilig, ersetzt den Tastatur-Hinweis. Genau eine primäre Aktion, Ausstieg immer „Auswahl aufheben" |
| **Kopfzeilen-Kästchen mit drei Zuständen** | Primitives | leer / Teilauswahl-Strich / Haken, abgeleitet aus Auswahl und Trefferzahl. Wählt den gesamten Treffer |
| **Tabellenzeile mit `IsSelected` und Anker** | Table | Auswahl-Grund `rgba(0,212,170,.07)`, Anker zusätzlich mit Teal-Kante; Shift-Klick spannt vom Anker |
| **Listenzeile Handy im Auswahl-Modus** | Table bzw. Card + Primitives | Kästchen 20 × 20 links, `gap:12px`, Zeile als Trefferfläche |
| Filter-Chip-Leiste mit Zähler und „×" | Primitives-Chip | aus Ablauf 2 unverändert; Handy sticky und scrollbar mit aktiven Chips vorn, PC feste Reihenfolge |
| Konto-Stände-Leiste (Handy) / Konten-Spalte (PC) | Card | „Alle Konten" zuoberst und in der Gesamtsicht aktiv |
| Konto-Kürzel-Pill (Handy) | Badge | `GIRO`, `BAR`, Mono 500 11px |

## Assets

Keine Bilder. Alle Symbole sind Inline-SVG, `stroke`-basiert, `stroke-linecap:round`,
Strichstärke 1.7 bis 3.5. Im Code durch die `Icons`-Komponente zu ersetzen. Gebraucht
werden: Chevron (Konto-Wechsler), Lupe, Filter, Häkchen, „×", die vier Tab-Symbole
(Liste, Plus, Sync, Mehr). Der Teilauswahl-Strich ist kein SVG, sondern ein `div`
(9 × 2,5px, `radius:2px`).

Schriften liegen als Variable Fonts im Bundle (Subset „latin", SIL OFL 1.1).

## Erfundene Daten

Nur diese Werte verwenden.

**Konten:** Girokonto Comdirect 2.418,37 € · ING Extra 6.902,10 € · Bar 63,50 € ·
Geldmarkt-ETF 12.004,88 € · Summe „Alle Konten" 21.388,85 €. Unverändert aus Ablauf 1
und 2.

**Filter im Ausgangszustand:** „Kategorie: 1" (Familie: Kinder) und „Dieses Jahr".
Chip „Zu prüfen" steht auf 7.

**Treffer:** 34 Buchungen · **-2.140,00 €**. Davon zwölf geladen (siehe Tabelle unter
„Screens PC"), Summe der zwölf **-780,00 €**, Rest 22 Buchungen über -1.360,00 €. Die
Kita-Beitrag-Annahme der Tafel lautet 125,00 € im Monat, damit beide Summen aufgehen.

**Payees im Treffer:** Kita-Beitrag Stadt, Amazon Marketplace, Bauhaus, Apotheke am
Markt, REWE Mitte, Deutsche Bahn.

**Eine dreizehnte Buchung** erscheint nur am Handy in 3.4, als erste Zeile außerhalb der
Auswahl: 25.06. Bauhaus `GIRO` Kinder -48,20 €.

Zwei Beobachtungen zu den Zahlen, damit sie beim Nachbauen nicht überraschen: die zwölf
Zeilen ergeben nachgerechnet genau -780,00 €, aber der Kita-Beitrag steht in den zwölf
nur zweimal (09.09. und 12.08.), im Juli fehlt er. Der monatliche Rhythmus aus der
Annahme ist in den geladenen Zeilen also nicht sichtbar. Und die Konto-Kürzel am Handy
(`GIRO`, `BAR`) stehen am PC ausgeschrieben als „Girokonto" und „Bar", nicht als
„Girokonto Comdirect" wie in der Konten-Spalte.

## Voice

Deutsch, direkt, knapp, ohne Füllwörter. Du-Form. Zahlen und Zustände werden benannt,
nicht kommentiert: „34 Buchungen", „Auswahl: 12 von 34 Buchungen", „12 ausgewählt".
Knöpfe als Verb oder Verb+Objekt: „Geprüft", „Kategorie ändern", „Auswahl aufheben".
„Alle" ist die einzige Ausnahme, ein Ein-Wort-Knopf, weil in der Reihe 74px Platz sind.
Beträge sprechen über Vorzeichen und Farbe, Format `-2.140,00 €`. Der Ausstieg heißt
immer „Auswahl aufheben".

## Offene Punkte für Roman

1. **Summenzeile am PC.** 3.5 zeigt nur „34 Buchungen · -2.140,00 €". Ablauf 2 hatte am
   PC den Filter im Klartext („312 Buchungen · alle Konten · Lebensmittel,
   Restaurants"). Beides ist vertretbar, es sollte aber eine Form gelten. Am Handy steht
   in beiden Abläufen die kurze Form.
2. **Klick auf das Kopfzeilen-Kästchen bei laufender Teilauswahl.** Der Strich zeigt den
   Zustand, aber die Tafel sagt nicht, was der Klick darauf tut: auf alle 34 erweitern
   oder die Auswahl leeren. Üblich ist erweitern, mit dem zweiten Klick leeren.
3. **Tipp auf die Zeile am Handy im Auswahl-Modus.** Auswählen oder Detail öffnen? Solange
   der Modus läuft, spricht alles für Auswählen, dann fehlt aber der Weg ins Detail.
4. **Auswahl bei Filter- oder Kontowechsel.** Die Tafel lässt die Chips unverändert und
   sagt nichts weiter. Die Backlog-Aufgabe verschiebt es in die Verfeinerung. Der
   einfache Weg: Wechsel leert die Auswahl, weil sie sich auf einen Treffer bezieht.
5. **Chips „Flag" und „Gelöschte zeigen"** fehlen in der PC-Chip-Leiste dieser Tafel,
   Ablauf 2 hatte sie. Kürzung für die Tafel oder Entscheidung?
6. **Reihenfolge der Konto-Karten am Handy** (Alle Konten, ING Extra, Girokonto, Bar)
   weicht von der Konten-Spalte am PC ab (Girokonto vor ING Extra), und Geldmarkt-ETF
   fehlt am Handy. Vermutlich nur der sichtbare Ausschnitt, sollte aber festgelegt sein.
7. **Schreibende Aktionen in der Leiste.** „Geprüft", „Kategorie ändern" und „Flag" sind
   gezeichnet, gehören laut Backlog aber nicht in diese Scheibe. Bis zur
   v2-Entscheidung: mitzeichnen und deaktiviert zeigen, oder in der Scheibe weglassen?
