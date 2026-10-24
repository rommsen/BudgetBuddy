# Handoff: Kassenbuch – Ablauf 1 „Durchsehen und als geprüft markieren"

> **Vorrang (2026-10-24):** Dieser Handoff legt den **Ablauf** fest: welche Elemente es
> gibt, welche Zustände, welche Reihenfolge, welche Texte. Die **Optik** (Farben, Flächen,
> Schriftgrößen, Abstände, Glow) kommt ausschließlich aus
> `standards/frontend/styleguide.md`, den Komponenten in `src/Client/DesignSystem/` und
> der Route `/styleguide`. Hex-Werte und Maße in diesem Dokument sind Stand der
> Design-Tafel und werden **nicht** in Code übernommen. Fehlt ein Baustein, entsteht er im
> Design-System aus Tokens und erscheint in `/styleguide`.

> **Herkunft.** Erzeugt in Claude Design (Projekt „Gelesen UI-Mockups",
> `5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`, Datei
> `design_handoff_kassenbuch_ablauf1/README.md`), am 2026-09-12 ins Repo übernommen. Zwei
> bewusste Abweichungen zur Design-Fassung: die Verweise auf das mitgelieferte
> `_ds/`-Bundle sind gekürzt, und **DaisyUI ist gestrichen** (mit ADR 0031 aus dem Projekt
> entfernt; das Design-Projekt weiß davon nichts, beim nächsten Abgleich nicht
> zurückziehen). Die zugehörige Tafel mit allen zwölf Screens liegt dort als
> `Ablauf 1 - Durchsehen und pruefen.dc.html`. Ein Ausschnitt (Screen 1.9) liegt als Bild
> unter `mockups/1.9-pc-ausgangszustand-zeile-markiert.png`.
>
> **Status: Referenz, nicht Code.** Die Werte hier sind verbindlich für die Umsetzung;
> die Quelle der Wahrheit für das Design-System selbst bleibt das Repo
> (`src/Client/DesignSystem/*.fs`, `src/Client/styles.css`, `standards/frontend/styleguide.md`).
> Dieses Dokument fließt nicht zurück nach Claude Design.

## Überblick

Das Kassenbuch ist die Buchungsliste von BudgetBuddy (YNAB-Companion). Ablauf 1 deckt
den häufigsten Fall ab: Roman geht neue, ungeprüfte Buchungen durch und markiert jede
als geprüft — **eine Geste je Buchung, keine zweite Geste, kein Umweg über ein Konto**.

Der bestehende YNAB-Weg braucht zwei Gesten an zwei Orten mit unterschiedlicher
Belegung (approve per Wisch in der Review-Liste, cleared per Wisch im Konto). Das ist
der Schmerz, den dieser Ablauf ablöst.

Zwei Geräte, gleichwertig:
- **Handy 390 × 844** — Hauptfall Durchsehen und Prüfen (Screens 1.1 bis 1.8)
- **PC 1440 × 900** — Schwerpunkt Nachbearbeiten, Tastatur und Mehrfachauswahl (1.9 bis 1.12)

## Zu den Design-Dateien

Die Dateien im Ursprungspaket sind **Design-Referenzen in HTML** — Prototypen, die
Aussehen und Verhalten festhalten. Sie sind **kein Produktionscode zum Übernehmen**.

Die Aufgabe ist, diese Entwürfe **in der bestehenden Umgebung des Ziel-Codebase
nachzubauen** — bei BudgetBuddy also F# / Fable / Elmish mit Tailwind 4 (**ohne DaisyUI**,
ADR 0031), mit den Komponenten aus `src/Client/DesignSystem/*.fs` und den Tokens aus
`src/Client/styles.css`. Nicht das HTML einbetten, sondern die vorhandenen
Komponenten benutzen: `Card`, `Money`, `Badge`, `Table`, `Navigation`, `BottomSheet`,
`Swipe`, `Button`, `Stats`, `Input`, `Toast`, `Icons`, `PageHeader`, `Primitives`.

Wo der Entwurf etwas zeigt, das es im Design-System noch nicht als Komponente gibt
(siehe „Neu zu bauende Komponenten"), ist es im HTML aus vorhandenen Bausteinen
zusammengesetzt. Diese Teile gehören als echte Komponenten ins Design-System — die
Quelle der Wahrheit bleibt das Repo, dieses Paket fließt nicht automatisch zurück.

## Fidelity

**Hi-fi.** Farben, Typografie, Abstände, Zustände und Texte sind endgültig und
entstammen dem gebundenen BudgetBuddy Design System. Maße sind pixelgenau
gemeint. Die einzige Ausnahme: die Tafel selbst (Kopfzeile, Screen-Beschriftungen,
Notizen unter den Screens, Annahmen-Karten) ist Dokumentation und wird **nicht**
implementiert.

## Grundlage: Aufbau, der für alle Screens gilt

**Einstieg:** Das Kassenbuch öffnet im zuletzt gewählten Konto. In allen Screens ist
das „ING Extra". Die Gesamtsicht („Alle Konten") ist über den Konto-Wechsler
erreichbar.

**Aufbau von oben nach unten, beide Geräte:**
1. Kontostände — Handy: waagerecht scrollbare Leiste; PC: linke Spalte, „Alle Konten" zuoberst
2. Sticky Filterleiste aus Chips; **erster Chip ist immer „Zu prüfen (n)"**
3. Summenzeile, **nur wenn ein Filter aktiv ist**
4. Liste

**Konto ist ein Filter.** Ein Konto-Wechsel lässt andere Filter und eine laufende
Auswahl stehen. In der Gesamtsicht trägt jede Zeile ein Konto-Kürzel (Handy) bzw. es
gibt eine Konto-Spalte (PC) — in Ablauf 1 nicht sichtbar, weil eine Konto-Sicht aktiv ist.

**Prüfen sortiert nie um.** Ohne Filter bleiben geprüfte Zeilen an ihrem Platz
(Screen 1.8). Mit aktivem Chip „Zu prüfen" verlässt die Zeile die Trefferliste.

**Prüfen hängt nicht an der Kategorie.** Amazon Marketplace ohne Kategorie lässt sich
prüfen (Screen 1.4). Kategorisieren ist ein eigener Ablauf.

**Zahlen kommen vom Server.** Trefferzahl und Summe gelten über den gesamten Treffer,
nicht über den geladenen Teil (100 je Seite, Nachladen beim Scrollen, rund 14.000
Buchungen). Kontostände kommen aus dem Zustand, nie aus der geladenen Liste.

## Screens

### 1.1 Handy: Ausgangszustand

**Zweck:** Roman öffnet das Kassenbuch und sieht sofort, was zu prüfen ist.

**Layout:** Spalte, 390 × 844, Grund `#08081a`.

| Block | Höhe / Maße | Inhalt |
|---|---|---|
| Statusleiste | `padding:12px 24px 4px` | „9:41" JetBrains Mono 600 13px `#7a7a98`, rechts zwei Symbole |
| Kopf | `padding:4px 16px 12px` | Overline „KASSENBUCH" Mono 600 10px, `letter-spacing:.16em`, `#4a4a68`; Titel „ING Extra" Space Grotesk 600 22px `#e8e8f0` mit Chevron 16px in Teal (Konto-Wechsler); rechts zwei Knöpfe 44 × 44, `radius:12px`, `#16162e`, Rand `#2a2a4a` (Suche, Filter) |
| Kontostände | `padding:0 16px 14px`, `gap:10px`, waagerecht scrollbar | Karten je 116px breit, `radius:12px`, `padding:9px 11px`. Inaktiv: `#111128`, Rand `#2a2a4a`, Name Inter 500 11px `#7a7a98`. Aktiv (ING Extra): `#1a1a36`, Rand `rgba(0,212,170,.45)`, `box-shadow:0 0 18px rgba(0,212,170,.12)`, Name Inter 600 11px `#00d4aa`, Betrag mit `text-shadow:0 0 12px rgba(0,255,136,.35)`. Beträge Mono 600 13px, `tabular-nums`, `#00ff88` |
| Chips | `padding:0 16px 12px`, `gap:8px`, Unterkante `1px solid #1c1c38` | Höhe über `padding:12px 14px` + `line-height:20px` = 44px Trefferfläche. Aktiv: `rgba(0,212,170,.12)`, Rand `rgba(0,212,170,.4)`, Inter 600 13px `#00d4aa`. Inaktiv: `#16162e`, Rand `#2a2a4a`, Inter 500 13px `#7a7a98`. Reihenfolge: Zu prüfen (7) · Kategorie · Zeitraum · Payee · Betrag · Memo · Flag · Gelöschte zeigen |
| Summenzeile | `padding:11px 16px`, Grund `#111128` | Links „7 Buchungen" Inter 500 12px `#7a7a98`; rechts „-296,06 €" Mono 600 14px `tabular-nums` `#ff3b5c` |
| Datums-Gruppenkopf | `padding:14px 16px 6px` | „HEUTE" / „GESTERN" / „08.09." / „07.09." Mono 600 10px `letter-spacing:.16em` `#4a4a68` |
| Listenzeile | `padding:13px 16px`, Unterkante `1px solid #1c1c38` | Zeile 1: Payee Inter 600 15px `#e8e8f0` (ellipsis) + Betrag Mono 600 15px `tabular-nums` `#ff3b5c`. Zeile 2, `margin-top:4px`, `gap:8px`: Kategorie Inter 500 12px `#a855f7`, Memo Inter 400 12px `#4a4a68` |
| Tab-Bar | `padding:10px 8px 22px`, Grund `rgba(8,8,26,.92)`, Oberkante `1px solid #1c1c38` | Vier Ziele je `min-width:64px; min-height:44px`: Kassenbuch (aktiv, Teal `#00d4aa`), Quick Add, Sync, Mehr (`#4a4a68`). Symbol 21px, Label Inter 600 10px |

**Liste in 1.1 (Chip „Zu prüfen (7)" aktiv):**

| Gruppe | Payee | Kategorie | Memo | Betrag |
|---|---|---|---|---|
| HEUTE | REWE Mitte | Lebensmittel | Wocheneinkauf | -48,12 € |
| HEUTE | Bäckerei Krause | Lebensmittel | — | -6,40 € |
| GESTERN | Deutsche Bahn | Bahn | Ticket Köln | -29,90 € |
| GESTERN | Netflix | Streaming | — | -13,99 € |
| 08.09. | Stadtwerke Musterstadt | Nebenkosten | Abschlag September | -142,00 € |
| 08.09. | Amazon Marketplace | Badge „Keine Kategorie" | — | -37,45 € |
| 07.09. | Apotheke am Markt | Apotheke | — | -18,20 € |

Badge „Keine Kategorie": `padding:2px 8px`, `radius:999px`, `rgba(255,107,44,.1)`,
Rand `rgba(255,107,44,.3)`, Inter 500 11px `#ff6b2c`.

### 1.2 Handy: während des Wischs

**Zweck:** die Geste zeigen.

Die Zeile „REWE Mitte" liegt in einem `position:relative; overflow:hidden`-Halter.
Dahinter, `position:absolute; inset:0`, die Aktionsfläche: Grund
`rgba(0,255,136,.14)`, `padding-left:22px`, Häkchen-Symbol 22px Strichstärke 2.4 in
`#00ff88`, Label „Geprüft" Inter 600 14px `#00ff88` mit
`text-shadow:0 0 12px rgba(0,255,136,.4)`.

Der Zeileninhalt ist um `translateX(132px)` verschoben, Grund `#08081a`,
`box-shadow:-12px 0 24px rgba(0,0,0,.45)` als Kante.

**Regeln:** Ein einziger Wisch nach rechts prüft. **Wisch nach links bleibt
unbelegt** — kein Löschen, kein Flag —, damit die Geste eindeutig bleibt. Später ist
dort „Rückfrage Lena" denkbar; das entscheidet Roman beim Durchgang.

### 1.3 Handy: nach dem Wisch

Zeile ist aus der Trefferliste verschwunden. Chip steht auf „Zu prüfen (6)",
Summenzeile auf „6 Buchungen · -247,94 €".

**Toast:** `position:absolute; left:16px; right:16px; bottom:98px` (also über der
Tab-Bar), `padding:13px 14px`, `radius:14px`, Grund `#1a1a36`, Rand `#2a2a4a`,
`box-shadow:0 12px 40px rgba(0,0,0,.6)`. Inhalt: Häkchen 18px `#00ff88`, Text
„REWE Mitte geprüft" Inter 500 13px `#e8e8f0`, rechts „Rückgängig" Inter 600 13px
`#00d4aa`. **Die Rücknahme läuft über den Toast, nicht über einen zweiten Wisch.**

### 1.4 Handy: Buchung ohne Kategorie geprüft

Gleiche Geste auf „Amazon Marketplace". Chip „Zu prüfen (5)", Summe „-210,49 €",
Toast „Amazon Marketplace geprüft".

Die Zeile „Deutsche Bahn" ist angetippt: Grund `#111128` und
`box-shadow:inset 2px 0 0 #00d4aa` als Teal-Kante links.

### 1.5 Handy: Detail als BottomSheet

**Zweck:** eine einzelne Buchung ansehen und per Schalter prüfen.

Abdunklung `position:absolute; inset:0; background:rgba(5,5,16,.72)`.

Sheet: unten angeschlagen, Höhe 660px, Grund `#1a1a36`, Oberkante `1px solid #2a2a4a`,
`border-radius:22px 22px 0 0`, `box-shadow:0 -24px 60px rgba(0,0,0,.65)`.
Griff: 40 × 4px, `radius:999px`, `#2a2a4a`, `margin:10px auto 6px`.

**Kopf** (`padding:8px 18px 16px`, Unterkante `#1c1c38`):
- Betrag „-29,90 €" Mono 600 30px `tabular-nums` `#ff3b5c`
- Payee „Deutsche Bahn" Space Grotesk 600 17px `#e8e8f0`
- „ING Extra · Gestern, 10.09.2026" Inter 400 12px `#7a7a98`
- Schließen-Knopf 36 × 36, `radius:10px`, `#16162e`, Rand `#2a2a4a`

**Felder**, je `min-height:56px`, `padding:0 18px`, Unterkante `#1c1c38`, Label links
Inter 500 13px `#7a7a98`:
- **Kategorie** → „Mobilität: Bahn" Inter 600 14px `#a855f7` + Chevron; öffnet den Picker
- **Split** → „Kein Split" + Knopf „Split anlegen" (`#16162e`, Rand `#2a2a4a`, Inter 600 12px `#00d4aa`). **Split-Bearbeitung passiert nur hier.**
- **Memo** → „Ticket Köln" Inter 400 14px `#e8e8f0`
- **Lena fragen** → Schalter aus
- **Markieren (temporär)** → Schalter aus
- **Geprüft** → eigene Zeile, `min-height:60px`, Grund `#16162e`, Label Inter 600 14px `#e8e8f0`

Schalter aus: 50 × 30px, `radius:999px`, `#16162e`, Rand `#2a2a4a`, Knauf 22px `#4a4a68`.
Schalter an: `rgba(0,255,136,.16)`, Rand `rgba(0,255,136,.5)`, Knauf `#00ff88` mit
`box-shadow:0 0 12px rgba(0,255,136,.6)`, rechts ausgerichtet.

**Herkunft** (`padding:14px 18px`): Overline „HERKUNFT" + Badge „YNAB-Spiegel"
(`rgba(0,212,170,.1)`, Rand `rgba(0,212,170,.3)`, Inter 500 11px `#00d4aa`).

**Historie** als Zeitleiste (`padding:14px 18px 20px`): je Eintrag Punkt 8px
(`margin-top:4px`) + Text Inter 500 13px + Zeitstempel Mono 400 11px `#4a4a68`.
Neuester Eintrag Punkt `#00d4aa` und Text `#e8e8f0`, ältere Punkt `#2a2a4a` und Text
`#7a7a98`. In 1.5: „Aus YNAB gespiegelt: Betrag, Payee, Kategorie · 10.09.2026 06:12 ·
Sync" und „Kategorie ‚Bahn' von YNAB übernommen · 10.09.2026 06:12 · YNAB".

**Herkunft und Historie sind das, was nur BB zeigt** — YNAB hat das nicht.

Eine Buchung ohne YNAB-Verknüpfung trägt zusätzlich den Hinweis „noch nicht in YNAB"
samt Grund. In Ablauf 1 kommt das nicht vor, weil alle Buchungen aus dem Spiegel
stammen.

### 1.6 Handy: Geprüft-Schalter an

Schalter an (Green mit Glow). Im Kopf erscheint neben dem Payee das Badge „Geprüft"
(`rgba(0,255,136,.1)`, Rand `rgba(0,255,136,.3)`, Inter 500 11px `#00ff88`).

Die Historie bekommt einen neuen obersten Eintrag: „Geprüft von Roman · Heute 09:41 ·
Roman in BB", Punkt `#00ff88` mit Glow.

Chip und Summenzeile hinter dem Sheet stehen bereits auf „Zu prüfen (4)" /
„4 Buchungen · -180,59 €" — der Schalter wirkt sofort, nicht erst beim Schließen.

### 1.7 Handy: nach dem Sheet

Sheet geschlossen, „Deutsche Bahn" ist aus der Liste verschwunden. Drei Buchungen sind
geprüft, vier bleiben: Bäckerei Krause, Netflix, Stadtwerke, Apotheke.

### 1.8 Handy: Chip abgewählt, alle Buchungen

**Zweck:** zeigen, dass Prüfen nicht umsortiert.

Chip „Zu prüfen (4)" ist inaktiv (`#16162e`, Rand `#2a2a4a`, `#7a7a98`).
**Keine Summenzeile**, weil kein Filter aktiv ist.

Alle Buchungen des Kontos in unveränderter Reihenfolge. Geprüfte Zeilen tragen **kein**
Kennzeichen. Ungeprüfte Zeilen tragen einen Punkt am linken Rand:
`position:absolute; left:6px; top:50%; transform:translateY(-50%)`, 6 × 6px,
`radius:999px`, `#ff1f8f`, `box-shadow:0 0 8px rgba(255,31,143,.7)`.

Der Screen zeigt den oberen Ausschnitt; die Liste läuft nach unten weiter (07.09.
Apotheke am Markt, 06.09. Café Lotte · Restaurants · -11,80 €).

### 1.9 PC: Ausgangszustand, Zeile mit Tastatur markiert

**Layout:** 1440 × 900, Spalte.

**Kopfleiste**, Höhe 64px, `padding:0 24px`, Grund `rgba(8,8,26,.85)`, Unterkante
`#1c1c38`: links Logo 40 × 40 `radius:12px`
`linear-gradient(135deg,#ff6b2c,#00d4aa)` mit „B" Space Grotesk 700 20px, Wortmarke
„BudgetBuddy" Space Grotesk 700 18px; rechts Navigation Kassenbuch (aktiv,
`rgba(0,212,170,.1)`, Inter 600 14px `#00d4aa`) · Sync · Regeln · Einstellungen
(Inter 500 14px `#4a4a68`), je `padding:10px 14px`, `radius:10px`.

**Konten-Spalte** links, Breite 268px, Grund `#08081a`, rechte Kante `#1c1c38`,
`padding:18px 14px`: Overline „KONTEN", dann Zeilen `padding:11px 12px`,
`radius:10px`, Name Inter 500 13px `#7a7a98` (ellipsis), Betrag Mono 600 13px
`tabular-nums` `#00ff88`, `white-space:nowrap`.
Reihenfolge: **Alle Konten 21.388,85 €** · ING Extra 6.902,10 € (aktiv: Grund
`#1a1a36`, `box-shadow:inset 2px 0 0 #00d4aa`, Name Inter 600 13px `#e8e8f0`, Betrag
mit Glow) · Girokonto Comdirect 2.418,37 € · Bar 63,50 € · Geldmarkt-ETF 12.004,88 €.

**Chip-Leiste**, `padding:14px 20px`, `gap:8px`, Chips `padding:9px 13px`,
`radius:999px` — gleiche Farben wie am Handy, alle acht Chips sichtbar.

**Summenzeile**, `padding:12px 20px`, Grund `#111128`: links „7 Buchungen · ING Extra ·
zu prüfen" Inter 500 13px `#7a7a98`, rechts „-296,06 €" Mono 600 15px `#ff3b5c`.

**Tabelle**, Spaltenraster
`44px 96px 1fr 200px 1fr 132px 128px` = Kästchen · Datum · Payee · Kategorie · Memo ·
Betrag (rechts) · Status (rechts). In der Gesamtsicht kommt eine Konto-Spalte dazu.

- Sticky Kopf: Höhe 42px, Grund `#1a1a36`, Unterkante `#2a2a4a`, Mono 600 10px
  `letter-spacing:.12em` `#4a4a68`
- Zeilen: Höhe 52px, Unterkante `#1c1c38`, Payee Inter 600 14px `#e8e8f0`, Datum Mono
  500 13px `#7a7a98`, Kategorie Inter 500 13px `#a855f7` (voll qualifiziert, z. B.
  „Alltag: Lebensmittel"), Memo Inter 400 13px `#7a7a98`, Betrag Mono 600 14px
  `tabular-nums` `#ff3b5c`
- Kästchen: 17 × 17px, `radius:5px`, `#16162e`, Rand `#2a2a4a`
- Status-Badge „Zu prüfen": `rgba(255,31,143,.1)`, Rand `rgba(255,31,143,.3)`,
  Inter 500 11px `#ff1f8f`
- Markierte Zeile (Tastatur): Grund `#1e1e3c`,
  `box-shadow:inset 0 0 0 1px rgba(0,212,170,.45)`

**Tastatur-Hinweis** als Fußzeile, `padding:12px 20px`, Grund `#08081a`, Oberkante
`#1c1c38`, Inter 500 12px `#4a4a68`, Tasten in Mono `#7a7a98`:
`↑ ↓` bewegen · `Leertaste` auswählen · `G` geprüft · `Enter` Detail öffnen.

### 1.10 PC: nach Taste G

Status und Zählung folgen sofort: Chip „Zu prüfen (6)", Summenzeile „6 Buchungen ·
ING Extra · zu prüfen · -247,94 €". REWE Mitte ist weg, die Markierung ist auf die
nächste Zeile (Bäckerei Krause) gewandert.

Toast rechts unten: `position:absolute; right:24px; bottom:78px`, `padding:14px 16px`,
`radius:14px`, `#1a1a36`, Rand `#2a2a4a`, `box-shadow:0 16px 48px rgba(0,0,0,.6)`.

### 1.11 PC: Mehrfachauswahl mit Summe

Drei Kästchen gesetzt: Stadtwerke Musterstadt (-142,00 €), Deutsche Bahn (-29,90 €),
Apotheke am Markt (-18,20 €) = **-190,10 €**.

> **Offener Punkt:** Der Brief nannte „3 ausgewählt · -186,10 €". Mit den erfundenen
> Beträgen geht keine Dreierkombination auf diesen Wert auf. Umgesetzt ist -190,10 €.
> Roman entscheidet, ob eine andere Auswahl gemeint war.

- Gesetztes Kästchen: Grund und Rand `#00d4aa`, Häkchen 11px in `#08081a`,
  Strichstärke 3.5
- Ausgewählte Zeile: Grund `rgba(0,212,170,.07)`
- **Summenzeile zeigt die Auswahl statt des Filters**: „Auswahl: 3 von 6 Buchungen"
  Inter 600 13px `#00d4aa`, Unterkante `rgba(0,212,170,.3)`,
  `box-shadow:inset 2px 0 0 #00d4aa`, rechts „-190,10 €"
- **Auswahl-Leiste** unten statt des Tastatur-Hinweises: `padding:14px 20px`, Grund
  `#1a1a36`, Oberkante `#2a2a4a`, `box-shadow:0 -14px 40px rgba(0,0,0,.45)`. Links
  „3 ausgewählt" Inter 600 14px `#e8e8f0` und „-190,10 €" Mono 600 16px `#ff3b5c`;
  rechts **„Geprüft" als einzige primäre Aktion** (`linear-gradient(135deg,#ff6b2c,#e55a1f)`,
  `#fff`, `radius:10px`, `padding:11px 18px`, `box-shadow:0 0 20px rgba(255,107,44,.5)`),
  dann „Kategorie ändern" und „Flag" sekundär (`#16162e`, Rand `#2a2a4a`, `#e8e8f0`),
  dann „Auswahl aufheben" als reiner Text `#7a7a98`

### 1.12 PC: nach „Geprüft" für die Auswahl

Auswahl aufgelöst, Chip „Zu prüfen (3)", Summenzeile „3 Buchungen · ING Extra · zu
prüfen · -57,84 €". Übrig: Bäckerei Krause, Netflix, Amazon Marketplace.
Ein Toast für die ganze Auswahl: „3 Buchungen geprüft · Rückgängig" — **eine**
Rücknahme, nicht drei. Tastatur-Hinweis ist zurück.

## Interaktionen und Verhalten

**Handy**

| Geste | Wirkung |
|---|---|
| Wisch nach rechts auf einer Zeile | prüft die Buchung, einmalig; Zeile verlässt die gefilterte Liste, Chip und Summe rechnen nach, Toast mit „Rückgängig" |
| Wisch nach links | unbelegt (bewusst frei gehalten) |
| Tipp auf eine Zeile | öffnet das Detail als BottomSheet |
| Langes Drücken | startet den Auswahl-Modus |
| Tipp auf Chip | schaltet den Filter um, Liste springt nach oben, Chips bleiben sichtbar |
| Tipp auf Konto-Karte oder Chevron im Titel | wechselt das Konto; andere Filter und laufende Auswahl bleiben |

**PC**

| Eingabe | Wirkung |
|---|---|
| `↑` `↓` | Markierung bewegen |
| `Leertaste` | Zeile aus- oder abwählen |
| `G` | markierte Zeile prüfen |
| `Enter` | Detail im Seitenpanel rechts öffnen (Liste bleibt sichtbar) |
| Klick auf Zeile | Seitenpanel rechts |
| Klick auf Kategorie-Zelle | öffnet den Kategorie-Picker direkt, ohne Umweg über das Panel |
| Kästchen oder Shift-Klick | Auswahl; Auswahl-Leiste erscheint unten |

**Zustände, die jeder Screen braucht:** Ladezustand beim Nachladen (Loading), Fehler
beim Prüfen (ErrorDisplay, Text benennt was passiert ist), Leerzustand wenn kein
Treffer bleibt (sagt, was als Nächstes zu tun ist).

**Payee-, Memo- und Betragssuche** mit kurzer Verzögerung (Debounce), nicht bei jedem
Tastendruck.

## Zustand

- `selectedAccountId` — zuletzt gewähltes Konto, überlebt das Schließen
- `filters` — aktive Chips inkl. Kategorien-Mehrfachauswahl, Zeitraum, Payee, Memo,
  Betrag, Flag, `showDeleted`
- `page`, `hasMore` — seitenweises Laden, 100 je Seite
- `totals` — `{ count, sum }` vom Server über den gesamten Treffer
- `accountBalances` — aus dem Zustand, **nicht** aus der geladenen Liste
- `selection` — Set von Buchungs-IDs; solange gefüllt, zeigt die Summenzeile die Auswahl
- `keyboardCursor` — markierte Zeile am PC
- `openDetail` — Buchung im Sheet (Handy) oder Seitenpanel (PC)
- `pendingUndo` — letzte Aktion für den Toast, inkl. Mehrfachaktion als **eine** Rücknahme

Prüfen ist optimistisch: Zeile verschwindet sofort, Chip und Summe rechnen sofort,
Serverfehler rollt zurück und zeigt eine Fehlermeldung.

## Design-Tokens

Alle Werte stammen aus dem Design-System-Bundle. Im HTML stehen sie als Literale, damit
die Screens sofort zeichnen — **im Code sind die Tokens zu verwenden**.

**Flächen nach Tiefe**

| Wert | Verwendung |
|---|---|
| `#08081a` | surface-app, Geräte-Grund, Konten-Spalte, Fußzeilen |
| `#111128` | surface-card, Summenzeile |
| `#1a1a36` | surface-elevated, Sheet, Auswahl-Leiste, Tabellenkopf, Toast |
| `#16162e` | surface-input, Knöpfe, inaktive Chips |
| `#1e1e3c` | markierte Tabellenzeile |
| `#2a2a4a` | Rand normal |
| `#1c1c38` | Rand zart, Zeilentrenner |

**Neon**

| Farbe | Wert | Bedeutung im Ablauf |
|---|---|---|
| Orange | `#ff6b2c` (Verlauf nach `#e55a1f`) | primär, genau eine je Screen: „Geprüft" in der Auswahl-Leiste; außerdem Badge „Keine Kategorie" |
| Teal | `#00d4aa` | aktiv, Navigation, aktives Konto, Auswahl, „Rückgängig" |
| Green | `#00ff88` | Erfolg, positive Beträge, Swipe-Aktion, Geprüft-Schalter |
| Red | `#ff3b5c` | negative Beträge |
| Purple | `#a855f7` | Kategorie |
| Pink | `#ff1f8f` | attention: Status „Zu prüfen", Punkt an ungeprüften Zeilen |

**Text:** `#e8e8f0` primär · `#7a7a98` sekundär · `#4a4a68` tertiär und Overlines.

**Typografie**
- Space Grotesk 600/700 — Screen-Titel 22px (Handy), Wortmarke 18px, Payee im Sheet 17px
- Inter 400/500/600 — Fließtext und Bedienelemente, 10px bis 15px
- JetBrains Mono 400/500/600 mit `font-variant-numeric:tabular-nums` — **alle Zahlen,
  Beträge, Daten, Zeitstempel, Overlines, Tastenkürzel**

**Radien:** 5px Kästchen · 8px kleiner Knopf · 10px Knopf und Navigation · 12px
Konto-Karte und Symbolknopf · 14px Toast und Notizkarte · 22px Sheet oben · 30px
Geräterahmen · 999px Chips, Badges, Schalter.

**Glow, sparsam und nur für Aktives:**
`0 0 18px rgba(0,212,170,.12)` aktive Karte · `text-shadow:0 0 12px rgba(0,255,136,.35)`
aktiver positiver Betrag · `0 0 20px rgba(255,107,44,.5)` primärer Button ·
`0 0 12px rgba(0,255,136,.6)` Schalterknauf an · `0 0 8px rgba(255,31,143,.7)`
Ungeprüft-Punkt.

**Schatten:** `0 12px 40px rgba(0,0,0,.6)` Toast Handy · `0 16px 48px rgba(0,0,0,.6)`
Toast PC · `0 -24px 60px rgba(0,0,0,.65)` Sheet · `0 -14px 40px rgba(0,0,0,.45)`
Auswahl-Leiste · `-12px 0 24px rgba(0,0,0,.45)` gewischte Zeile.

**Trefferflächen:** am Handy nie unter 44px — Chips über `padding:12px 14px` +
`line-height:20px`, Tab-Ziele `min-width:64px; min-height:44px`, Sheet-Felder
`min-height:56px`, Symbolknöpfe 44 × 44.

## Neu zu bauende Komponenten

Im HTML aus vorhandenen Bausteinen zusammengesetzt, weil es sie im Design-System noch
nicht gibt. Diese gehören als echte Komponenten ins Repo:

| Teil | Zusammengesetzt aus | Anmerkung |
|---|---|---|
| Filter-Chip-Leiste | Primitives `chip` + Aktivzustand in Teal | sticky, waagerecht scrollbar, erster Chip immer „Zu prüfen (n)" |
| Summenzeile | Card + Money | nur bei aktivem Filter; zeigt die Auswahl, sobald eine läuft |
| Auswahl-Leiste | Card + Money + Button | genau eine primäre Aktion |
| Geprüft-Schalter | Primitives | Green mit Glow im An-Zustand |
| Herkunft und Historie | Badge + Text als Zeitleiste | das Unterscheidungsmerkmal gegenüber YNAB |
| Konto-Stände-Leiste (Handy) | Card, waagerecht scrollbar | aktives Konto in Teal mit Glow |
| Konten-Spalte (PC) | Card-Liste | „Alle Konten" zuoberst, Beträge `nowrap` |

## Assets

Keine Bilder. Alle Symbole sind Inline-SVG, `stroke`-basiert, `stroke-linecap:round`,
Strichstärke 1.7 bis 2.6 — im Code durch die `Icons`-Komponente des Design-Systems zu
ersetzen. Schriften liegen als Variable Fonts im Bundle (Subset „latin", SIL OFL 1.1).

## Erfundene Daten

Alle Screens benutzen nur diese Werte — keine weiteren erfinden.

**Konten:** Girokonto Comdirect (Bank) 2.418,37 € · ING Extra (Bank) 6.902,10 € · Bar
(Bar) 63,50 € · Geldmarkt-ETF (Wertpapierkonto mit Budget-Geld) 12.004,88 € ·
Summe „Alle Konten" 21.388,85 €.

**Payees:** REWE Mitte, Bäckerei Krause, Stadtwerke Musterstadt, Deutsche Bahn, Amazon
Marketplace, Apotheke am Markt, Kita-Beitrag Stadt, Netflix, Bauhaus, Café Lotte.

**Kategorien:** Alltag (Lebensmittel, Haushalt, Restaurants) · Wohnen (Nebenkosten,
Möbel) · Mobilität (Bahn, Auto) · Familie (Kinder, Geschenke) · Abos (Streaming) ·
Gesundheit (Apotheke). Versteckt: „Umzug 2023".

**Zählungen im Ablauf:** 7 (-296,06 €) → 6 (-247,94 €) → 5 (-210,49 €) →
4 (-180,59 €) am Handy; 7 → 6 → Auswahl 3 (-190,10 €) → 3 (-57,84 €) am PC.

## Voice

Deutsch, direkt, knapp, ohne Füllwörter. Du-Form. Buttons als Verb oder Verb+Objekt:
„Geprüft", „Kategorie ändern", „Auswahl aufheben", „Split anlegen", „Rückgängig".
Beträge sprechen für sich über Vorzeichen und Farbe, Format `-48,12 €`.
Fehlertexte benennen, was passiert ist. Leerzustände sagen, was als Nächstes zu tun ist.

## Offene Punkte für Roman

1. **Auswahl-Summe am PC**: -190,10 € statt der im Brief genannten -186,10 € (siehe 1.11).
2. **Wisch nach links** bleibt unbelegt; später denkbar für „Rückfrage Lena".
3. **Ungeprüft-Kennzeichen** ist Pink (Punkt am Handy, Badge am PC). Orange wäre die
   Alternative, ist aber für die primäre Aktion reserviert.
