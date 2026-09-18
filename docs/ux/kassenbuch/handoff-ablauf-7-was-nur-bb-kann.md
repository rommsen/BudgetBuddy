# Handoff: Kassenbuch „Was nur BB kann" (Ablauf 7)

> **Herkunft.** Grundlage ist die Tafel `Ablauf 7 - Was nur BB kann.dc.html` aus dem
> Claude-Design-Projekt „Gelesen UI-Mockups" (`5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`).
> Zu Ablauf 7 gibt es dort **kein** Übergabepaket `design_handoff_kassenbuch_ablauf7/`,
> anders als bei Ablauf 1 und 2. Dieses Dokument ist deshalb am 2026-09-18 im Repo direkt
> aus der Tafel verfasst: alle Maße, Hex-Werte, Abstände und Texte sind aus dem HTML der
> zehn Screens abgelesen. Wo ein Wert im HTML nicht eindeutig steht, ist das im Text
> gesagt, statt geraten zu werden. **DaisyUI ist gestrichen** (mit ADR 0031 aus dem
> Projekt entfernt; das Design-Projekt weiß davon nichts, beim nächsten Abgleich nicht
> zurückziehen). Die Verweise der Tafel auf ihr mitgeliefertes Design-System-Bundle sind
> hier weggelassen.
>
> **Status: Referenz, nicht Code.** Die Werte hier sind verbindlich für die Umsetzung;
> die Quelle der Wahrheit für das Design-System selbst bleibt das Repo
> (`src/Client/DesignSystem/*.fs`, `src/Client/styles.css`, `standards/frontend/styleguide.md`).
> Dieses Dokument fließt nicht zurück nach Claude Design.

## Überblick

Das Kassenbuch ist die Buchungsliste von BudgetBuddy (YNAB-Companion). Ablauf 7 zeigt zu
einer einzelnen Buchung, woher sie stammt, was sich wann und durch wen geändert hat, ob
sie in YNAB gelöscht wurde und ob sie überhaupt schon in YNAB angekommen ist.

**YNAB-Referenzweg: keiner.** Es gibt in YNAB nichts Vergleichbares. Dieser Ablauf ist
die Begründung des Produkts, nicht die Verbesserung eines bestehenden Weges. Roman hat zu
Ablauf 7 bisher nicht gesagt, was er am Handy anders erwartet als am PC (siehe Offene
Punkte).

Zwei Geräte, gleichwertig:
- **Handy 390 × 844**, Screens 7.1 bis 7.5, Detail als BottomSheet
- **PC 1440 × 900**, Screens 7.6 bis 7.10, Detail als **Seitenpanel rechts, 420px**

Ausgangszustand in allen Screens: Konto **„Girokonto Comdirect"**, Liste mit gemischten
Herkünften.

Der Weg durch die Tafel: Liste (7.1 / 7.6) → Detail einer Buchung, die noch nicht in YNAB
ist (7.2 / 7.7) → Detail mit dreistufiger Historie und Abweichung (7.3 / 7.8) → Chip
„Gelöschte zeigen" (7.4 / 7.9) → Detail einer gelöschten Buchung (7.5 / 7.10).

Fachlicher Hintergrund: `docs/ux/kassenbuch/briefs.md`, Ablauf 7, und die Backlog-Aufgabe
`.agentheim/board/ledger/backlog/ledger-q1hw2-detail-herkunft-historie-noch-nicht-in-ynab.md`.

**Diese Scheibe ist lesend.** „Erneut senden", „YNAB übernehmen" und „BB durchsetzen"
sind schreibende Vorgänge und gehören nicht dazu. In der Tafel sind sie nur der Knopf,
ohne Lauf dahinter. Ein „Wiederherstellen" für gelöschte Buchungen gibt es gar nicht.

## Zu den Design-Dateien

Die Tafel ist eine **Design-Referenz in HTML**, ein Prototyp, der Aussehen und Verhalten
festhält. Sie ist **kein Produktionscode zum Übernehmen**.

Nachzubauen in der bestehenden Umgebung: F# / Fable / Elmish mit Tailwind 4 (**ohne
DaisyUI**, ADR 0031), mit den Komponenten aus `src/Client/DesignSystem/*.fs` und den
Tokens aus `src/Client/styles.css`. Nicht das HTML einbetten, sondern die vorhandenen
Komponenten benutzen: `Badge` (Herkunft, Kennzeichen, Status), `BottomSheet` (Detail am
Handy), `Card` (Panel, Hinweisleisten, Konten-Spalte), `Button` (Orange primär, Ghost),
`Icons` (Schließen, Chevron), `Money`, `Table`, `Navigation`, `Primitives` (Chips,
Kästchen, Schalter), `PageHeader`, `Loading`, `ErrorDisplay`.

Die Tafel nennt selbst, was sie aus Bausteinen zusammensetzen musste, weil die Komponente
fehlt: Herkunft/Historie-Zeitleiste, Hinweisleiste mit Grund und Aktion, Filter-Chip-Leiste,
Summenzeile, Geprüft-Schalter, Konto-Stände-Leiste (Handy), Konten-Spalte (PC). Diese
Teile gehören als echte Komponenten ins Repo (siehe „Neu zu bauende Komponenten").

Mockups fließen **nie** zurück in den Code.

## Fidelity

**Hi-fi.** Farben, Typografie, Abstände, Zustände und Texte sind endgültig. Maße sind
pixelgenau gemeint. Die Tafel selbst (Kopf, Screen-Beschriftungen, Notizen unter den
Screens, Annahmen-Karten) ist Dokumentation und wird **nicht** implementiert. Die drei
Schalter der Tafel (`notizen`, `tastaturHinweis`, `geraete` mit „Beide" / „Nur Handy" /
„Nur PC") steuern nur die Tafel-Ansicht.

## Grundlage, die für alle Screens gilt

**Vier Herkunft-Arten.** Comdirect-Import, Regel, YNAB-Spiegel und später Handeingabe aus
Quick Add. Im Detail steht die Art, die die Buchung ins Kassenbuch gebracht hat, als
eigene **HERKUNFT**-Zeile mit Teal-Badge, **direkt über der Historie**. Die Zeile ist eine
gewöhnliche Feldzeile: `padding:14px 18px`, Unterkante `#1c1c38`, links Overline
„HERKUNFT" Mono 600 10px `letter-spacing:.16em` `#4a4a68`, rechts das Badge
`padding:3px 9px`, `radius:999px`, `rgba(0,212,170,.1)`, Rand `rgba(0,212,170,.3)`,
Inter 500 11px `#00d4aa`.

**Hinweisleiste mit Grund und Aktion**, immer **direkt unter dem Kopf** des Sheets bzw.
Panels, `padding:12px 18px`, Unterkante `#1c1c38`. Aufbau: Badge, darunter der Grund
(`margin-top:7px`, Inter 400 12px/1.5 `#7a7a98`), darunter die Aktionen
(`margin-top:10px`, `gap:8px`). Drei Färbungen:

| Fall | Fläche | Badge | Aktionen |
|---|---|---|---|
| Noch nicht in YNAB | `rgba(255,107,44,.07)` | „Noch nicht in YNAB" `rgba(255,107,44,.12)`, Rand `rgba(255,107,44,.35)`, `#ff6b2c` | „Erneut senden" primär Orange |
| Abweichung (**v2**) | `rgba(255,31,143,.07)` | „Abweichung" `rgba(255,31,143,.12)`, Rand `rgba(255,31,143,.35)`, `#ff1f8f`, dahinter „ab v2" Inter 400 11px `#4a4a68` | „YNAB übernehmen" primär Orange, „BB durchsetzen" Ghost (`#16162e`, Rand `#2a2a4a`, Inter 600 14px `#e8e8f0`) |
| In YNAB gelöscht | `rgba(255,59,92,.07)` | „in YNAB gelöscht" `rgba(255,59,92,.12)`, Rand `rgba(255,59,92,.35)`, `#ff3b5c` | keine |

Knopfhöhe: am Handy `min-height:44px`, am PC `min-height:36px`, beide `padding:0 16px`,
`radius:10px`. Primär ist `linear-gradient(135deg,#ff6b2c,#e55a1f)`, `#fff`,
`box-shadow:0 0 20px rgba(255,107,44,.5)`.

**„Noch nicht in YNAB" erscheint nie ohne Grund.** Der Grund nennt, was passiert ist,
wann, und welche Fehlerart: Push gescheitert am 10.09. 18:02, Netzwerkfehler.

**Punktfarbe der Zeitleiste nach Urheber.** Teal `#00d4aa` Import, Violett `#a855f7`
Regel, Grau `#7a7a98` YNAB-Spiegel, Rot `#ff3b5c` Fehler und Löschung, Green `#00ff88`
eigene Prüfung. Punkt 8 × 8px, `radius:999px`, `margin-top:4px`, Abstand zum Text
`gap:11px`, Abstand zwischen Einträgen `padding-bottom:14px`. Der oberste Eintrag trägt
den Text in `#e8e8f0`, alle älteren in `#7a7a98`; Text Inter 500 13px/1.4, Zeitstempel
`margin-top:2px` Mono 400 11px `#4a4a68` im Format `10.09.2026 17:55 · Import`.

**Gelöschte Buchungen sind ein Lesezustand.** Betrag grau (`#7a7a98` im Kopf, `#4a4a68`
in der Zeile) statt rot, Payee und Kategorie in `#4a4a68`, Felder **ohne Chevron**, **kein
Geprüft-Schalter**, **keine primäre Aktion**. Das Kästchen der Tabellenzeile bleibt
anklickbar.

**Status-Spalte am PC trägt bis zu zwei Badges**, gestapelt
(`flex-direction:column; align-items:flex-end; gap:3px`): „Zu prüfen" plus „Nicht in
YNAB" bzw. plus „Abweichung". Mehr steht nicht in der Zeile. Herkunft und Historie
gehören ins Panel, sonst wird die Tabelle unlesbar.

**Genau eine primäre Aktion je Screen**, und sie sitzt **im Hinweis**, nicht am Fuß des
Panels: die Aktion gehört zum Grund.

**Zahlen kommen vom Server.** Trefferzahl und Summe gelten über den gesamten Treffer,
nicht über die geladenen Zeilen. Kontostände kommen aus dem Zustand, nie aus der Liste.

## Zwei Abweichungen vom Entwurf, entschieden am 2026-09-18

Der Entwurf zeigt es anders. Gebaut wird die entschiedene Variante.

### 1. Historie-Reihenfolge: neueste Zeile oben (`ledger-h9tq2`)

Die Anzeige zeigt die **neueste Zeile oben**, so wie die Tafel es zeichnet. Die Abfrage
bleibt **aufsteigend**: `getEntryHistory` liefert weiter `ORDER BY sequence`, damit
laufende Nummer und Replay unberührt bleiben. Gedreht wird nur in der Anzeige. Die
Backlog-Aufgabe `ledger-q1hw2` verlangt in ihrer Fassung „älteste zuerst"; das ist mit
dieser Entscheidung überholt.

### 2. Gelöschte Buchungen zählen in Trefferzahl und Summe (`ledger-d4kx8`)

Sind gelöschte Buchungen eingeblendet, zählen sie in **Trefferzahl und Summe**, damit die
sichtbaren Zeilen sich zur angezeigten Summe addieren. Der Entwurf wollte sie aus der
Summe nehmen; das gilt nicht.

- Die Zeile über der Liste sagt es aus, im Stil **„13 Buchungen, davon 1 in YNAB
  gelöscht"**. Der Entwurf zeigt dort nur „13 Buchungen".
- Die Summe ändert sich damit gegenüber der Tafel: gezeichnet ist **-712,26 €** ohne die
  gelöschten -9,80 €, gebaut wird **-722,06 €** bei denselben erfundenen Daten.
- Der Hinweistext im Detail einer gelöschten Buchung verliert seine zweite Hälfte. Statt
  „Zählt nicht zum Kontostand und nicht zur Summe." steht dort nur noch, dass sie nicht
  zum Kontostand zählt.
- **Der Kontostand lässt gelöschte Buchungen weiterhin immer draußen.**

Im Design-Projekt steht die Gegenregel noch als Annahme („Gelöschte Buchungen zählen in
der Trefferzahl, nicht in der Summe und nie im Kontostand"). Beim nächsten Abgleich nicht
zurückziehen.

## Screens Handy

### 7.1 Liste mit gemischten Herkünften

**Zweck:** Ausgangszustand. Nur eine Zeile trägt ein Kennzeichen, alles andere sieht aus
wie eine gewöhnliche Liste. Was BB über Herkunft und Historie weiß, steht im Detail.

**Layout:** Spalte, 390 × 844, Grund `#08081a`, Rand `1px solid #2a2a4a`, `radius:30px`.

| Block | Maße | Inhalt |
|---|---|---|
| Statusleiste | `padding:12px 24px 4px` | „9:41" Mono 600 13px `#7a7a98`, rechts zwei Symbole (Rahmen `#4a4a68`, Akkufüllung `#00ff88`) |
| Kopf | `padding:4px 16px 12px` | Overline „KASSENBUCH" Mono 600 10px `letter-spacing:.16em` `#4a4a68`; Titel **„Girokonto"** Space Grotesk 600 22px `#e8e8f0` + Chevron 16px `#00d4aa` Strichstärke 2 (Konto-Wechsler); rechts zwei Knöpfe 44 × 44, `radius:12px`, `#16162e`, Rand `#2a2a4a`, Symbole 19px Strichstärke 1.8 `#7a7a98` (Suche, Filter) |
| Kontostände | `padding:0 16px 14px`, `gap:10px` | Karten 116px, `radius:12px`, `padding:9px 11px`, Betrag `margin-top:5px` Mono 600 13px `tabular-nums` `#00ff88`. Aktiv (Girokonto Comdirect): `#1a1a36`, Rand `rgba(0,212,170,.45)`, `box-shadow:0 0 18px rgba(0,212,170,.12)`, Name Inter 600 11px `#00d4aa`, Betrag mit `text-shadow:0 0 12px rgba(0,255,136,.35)`. Inaktiv: `#111128`, Rand `#2a2a4a`, Name Inter 500 11px `#7a7a98` |
| Chips | `padding:0 16px 12px`, `gap:8px`, Unterkante `1px solid #1c1c38` | `padding:12px 14px` + `line-height:20px` = 44px Trefferfläche, `radius:999px`. Inaktiv `#16162e`, Rand `#2a2a4a`, Inter 500 13px `#7a7a98`. Sichtbar: Zu prüfen (9) · Kategorie · Zeitraum · Gelöschte zeigen |
| Summenzeile | fehlt | kein Filter aktiv |
| Datumskopf | `padding:14px 16px 6px` | „16.09." / „15.09." / „12.09." Mono 600 10px `letter-spacing:.16em` `#4a4a68` |
| Listenzeile | `padding:13px 16px`, Unterkante `1px solid #1c1c38` | Zeile 1: Ungeprüft-Punkt + Payee Inter 600 15px `#e8e8f0` (ellipsis) + Betrag Mono 600 15px `tabular-nums` `#ff3b5c`, `gap:10px`. Zeile 2 (`margin-top:4px`, `gap:8px`): Kategorie **Kurzform** Inter 500 12px `#a855f7`, Memo Inter 400 12px `#4a4a68`. Zeile 3 (`margin-top:6px`, `gap:6px`): Kennzeichen-Badge |
| Tab-Bar | `position:absolute`, Boden, `padding:10px 8px 22px`, `rgba(8,8,26,.92)`, Oberkante `1px solid #1c1c38` | Vier Ziele je `min-width:64px; min-height:44px`, Symbol 21px, Label Inter 600 10px: Kassenbuch (aktiv `#00d4aa`) · Quick Add · Sync · Mehr (`#4a4a68`) |

**Ungeprüft-Punkt:** hier **inline** vor dem Payee, 7 × 7px, `radius:999px`, `#ff1f8f`,
`box-shadow:0 0 8px rgba(255,31,143,.7)`, `gap:8px` zum Namen. Ablauf 1 hatte ihn als
6 × 6px absolut am linken Rand; die Tafel zu Ablauf 7 zeichnet die Inline-Fassung.

**Badge „noch nicht in YNAB" in der Liste:** `padding:3px 8px`, `radius:999px`,
`rgba(255,107,44,.12)`, Rand `rgba(255,107,44,.35)`, Inter 500 11px `#ff6b2c`,
Kleinschreibung. Das Badge im Detail heißt großgeschrieben „Noch nicht in YNAB".

**Liste:** 16.09. Bauhaus · Möbel · „Dübel und Leisten" · -84,90 € (ungeprüft, Badge
„noch nicht in YNAB") · 16.09. REWE Mitte · Lebensmittel · -48,12 € (ungeprüft) · 15.09.
Stadtwerke Musterstadt · Nebenkosten · -142,00 € (ungeprüft) · 15.09. Kita-Beitrag Stadt ·
Kinder · -125,00 € · 12.09. Apotheke am Markt · Apotheke · -18,20 € · 12.09. Netflix ·
Streaming · -12,99 €.

Sechs Zeilen sind sichtbar, drei davon ungeprüft. Der Chip nennt neun zu prüfende
Buchungen; die restlichen kommen beim Scrollen. Die Notiz der Tafel spricht von „fünf der
neun ungeprüften plus vier geprüfte"; das passt nicht zu den sechs gezeichneten Zeilen
(siehe Offene Punkte).

### 7.2 Detail: noch nicht in YNAB

**Zweck:** nicht nur sagen, dass die Buchung fehlt, sondern warum und seit wann.

Abdunklung `position:absolute; inset:0; background:rgba(5,5,16,.72)`.
Sheet: unten angeschlagen, Höhe **620px**, `#1a1a36`, Oberkante `1px solid #2a2a4a`,
`border-radius:22px 22px 0 0`, `box-shadow:0 -24px 60px rgba(0,0,0,.65)`.
Griff 40 × 4px, `radius:999px`, `#2a2a4a`, `margin:10px auto 6px`.

**Kopf** (`padding:8px 18px 12px`, Unterkante `#1c1c38`):
- Betrag „-84,90 €" Mono 600 30px `tabular-nums` `#ff3b5c`
- Payee „Bauhaus" Space Grotesk 600 17px `#e8e8f0`, `margin-top:6px`
- „Girokonto Comdirect · 10.09.2026" Inter 400 12px `#7a7a98`, `margin-top:3px`
- Schließen 36 × 36, `radius:10px`, `#16162e`, Rand `#2a2a4a`, X 14px Strichstärke 2.5 `#7a7a98`

**Hinweisleiste Orange** direkt darunter: Badge „Noch nicht in YNAB", Grund „Push
gescheitert am 10.09. 18:02, Netzwerkfehler. BB versucht es beim nächsten Lauf erneut.",
Aktion „Erneut senden" (`min-height:44px`). Der Entwurf setzt an der Stelle nach „18:02"
einen Gedankenstrich; im Repo gilt Komma oder Doppelpunkt.

**Felder**, je `min-height:56px`, `padding:0 18px`, Unterkante `#1c1c38`, Label links
Inter 500 13px `#7a7a98`:
- **Kategorie** → „Möbel" Inter 500 13px `#a855f7` + Chevron 15px Strichstärke 2 `#4a4a68`
- **Memo** → „Dübel und Leisten" Inter 400 13px `#e8e8f0`, kein Chevron
- **Geprüft** → eigene Zeile, `min-height:60px`, Grund `#16162e`, Label Inter 600 14px
  `#e8e8f0`, Schalter aus: 50 × 30px, `radius:999px`, Bahn `#111128`, Rand `#2a2a4a`,
  `padding:3px`, Knauf 22px `#4a4a68`

Ein **Split-Feld fehlt am Handy**; am PC (7.7) ist es vorhanden. Die Bahn des Schalters
ist hier `#111128`, in Ablauf 1 war sie `#16162e`.

**HERKUNFT:** Badge „Comdirect-Import".

**HISTORIE** (`flex:1`, `padding:14px 18px 0`, scrollt):
1. Punkt `#ff3b5c` · „Push nach YNAB gescheitert: Netzwerkfehler" · 10.09.2026 18:02 · Sync
2. Punkt `#00d4aa` · „Comdirect-Import: Buchung erfasst" · 10.09.2026 17:58 · Import

Das Sheet deckt die Liste vollständig ab, die Kopfzone bleibt sichtbar.

### 7.3 Detail: Historie und Abweichung

**Zweck:** drei Herkünfte in einer Buchung. Comdirect hat sie gebracht, eine Regel hat die
Kategorie gesetzt, YNAB hat sie später überschrieben.

Sheet **620px**, gleicher Rahmen wie 7.2.

**Kopf:** „-48,12 €" Mono 600 30px `#ff3b5c`, „REWE Mitte", „Girokonto Comdirect ·
10.09.2026".

**Hinweisleiste Pink (v2):** Badge „Abweichung" + „ab v2"; Text „BB sagt **Lebensmittel**,
YNAB sagt **Haushalt**." mit beiden Kategorien in `#a855f7` `font-weight:500`; Aktionen
„YNAB übernehmen" (primär) und „BB durchsetzen" (Ghost), je `min-height:44px`.

**Felder:** nur **Kategorie** („Lebensmittel" + Chevron) und **Geprüft** (Schalter aus).
Memo und Split fehlen in diesem Screen.

**HERKUNFT:** Badge „Regel".

**HISTORIE:**
1. Punkt `#7a7a98` · „YNAB-Spiegel: Kategorie in YNAB geändert: Lebensmittel → Haushalt" · 11.09.2026 08:12 · Sync
2. Punkt `#a855f7` · „Regel: Kategorie Lebensmittel gesetzt" · 10.09.2026 17:55 · Regel „REWE Mitte"
3. Punkt `#00d4aa` · „Comdirect-Import: Buchung erfasst" · 10.09.2026 17:55 · Import

Solange die Abweichung offen ist, bleibt sie auch in der Liste sichtbar.

### 7.4 Gelöschte zeigen

**Zweck:** der Chip „Gelöschte zeigen" holt die ausgeblendeten Buchungen zurück
(`ledger-p3n8k`).

**Chips:** aktive Chips wandern am Handy **nach vorn**. Aktiv: „Gelöschte zeigen" ×,
„Zeitraum: Dieser Monat" ×, dahinter inaktiv „Zu prüfen (9)", am Ende „Alle Filter
löschen".
Aktiv-Chip: `rgba(0,212,170,.12)`, Rand `rgba(0,212,170,.4)`, Inter 600 13px/20px
`#00d4aa`, `gap:8px`, dazu ein „×" 13px Strichstärke 2.6.
„Alle Filter löschen": Fläche `#16162e`, Rand `#2a2a4a`, Text Inter 600 13px `#00d4aa`.

**Summenzeile** (`padding:11px 16px`, Grund `#111128`, Unterkante `#1c1c38`): links
„13 Buchungen" Inter 500 12px `#7a7a98`, rechts „-712,26 €" Mono 600 14px `tabular-nums`
`#ff3b5c`.
**Abweichung 2 greift hier:** links steht „13 Buchungen, davon 1 in YNAB gelöscht", rechts
**-722,06 €**.

**Gelöschte Zeile:** 15.09. Café Lotte · Restaurants · -9,80 €, Payee, Kategorie und
Betrag in `#4a4a68`, Badge „in YNAB gelöscht" (`rgba(255,59,92,.12)`, Rand
`rgba(255,59,92,.35)`, Inter 500 11px `#ff3b5c`). Sie steht an ihrem Platz in der
Datumsfolge, nicht am Ende.

Fünf Zeilen passen ins Bild, acht folgen beim Scrollen.

### 7.5 Detail einer gelöschten Buchung

**Zweck:** in YNAB ist die Buchung weg, in BB bleibt nachvollziehbar, dass es sie gab.

Sheet Höhe **576px** (setzt unter der Summenzeile an), gleicher Rahmen wie 7.2.

**Kopf:** Betrag „-9,80 €" Mono 600 30px **`#7a7a98`** statt Rot, Payee „Café Lotte" Space
Grotesk 600 17px `#e8e8f0`, „Girokonto Comdirect · 15.09.2026".

**Hinweisleiste Rot:** Badge „in YNAB gelöscht", Text „Gelöscht in YNAB am 11.09.2026
08:20. Zählt nicht zum Kontostand und nicht zur Summe.", **keine Aktion**. Mit Abweichung 2
endet der Text nach „Zählt nicht zum Kontostand."

**Felder ohne Chevron:** Kategorie „Restaurants" Inter 500 13px `#4a4a68`, Memo „mit Lena"
Inter 400 13px `#4a4a68`. **Kein Geprüft-Schalter.**

**HERKUNFT:** Badge „Comdirect-Import".

**HISTORIE:**
1. Punkt `#ff3b5c` · „YNAB-Spiegel: in YNAB gelöscht" · 11.09.2026 08:20 · Sync
2. Punkt `#a855f7` · „Regel: Kategorie Restaurants gesetzt" · 15.09.2026 19:40 · Regel „Café Lotte"
3. Punkt `#00d4aa` · „Comdirect-Import: Buchung erfasst" · 15.09.2026 19:40 · Import

Die Zeitstempel der erfundenen Daten widersprechen sich: die Löschung (11.09.) liegt vor
dem Import (15.09.). Dazu fehlt hier der Geprüft-Eintrag, den das PC-Panel (7.10) hat.
Beides in den Offenen Punkten.

## Screens PC

### 7.6 Liste mit Status und Kennzeichen

**Layout:** 1440 × 900, Spalte, Grund `#08081a`, `radius:14px`.

**Kopfleiste** 64px, `padding:0 24px`, `rgba(8,8,26,.85)`, Unterkante `#1c1c38`: Logo
40 × 40, `radius:12px`, `linear-gradient(135deg,#ff6b2c,#00d4aa)` mit „B" Space Grotesk
700 20px `#fff`, Wortmarke „BudgetBuddy" Space Grotesk 700 18px; rechts Navigation je
`padding:10px 14px`, `radius:10px`: Kassenbuch (aktiv, `rgba(0,212,170,.1)`, Inter 600
14px `#00d4aa`) · Sync · Regeln · Einstellungen (Inter 500 14px `#4a4a68`).

**Konten-Spalte** 268px, `#08081a`, rechte Kante `#1c1c38`, `padding:18px 14px`,
`gap:4px`: Overline „KONTEN" (`padding:0 10px 10px`), Zeilen `padding:11px 12px`,
`radius:10px`, Name Inter 500 13px `#7a7a98` (ellipsis), Betrag Mono 600 13px
`tabular-nums` `#00ff88`, `white-space:nowrap`.
Reihenfolge: Alle Konten 21.388,85 € · **Girokonto Comdirect 2.418,37 € (aktiv:** `#1a1a36`,
`box-shadow:inset 2px 0 0 #00d4aa, 0 0 18px rgba(0,212,170,.12)`, Name Inter 600 13px
`#e8e8f0`, Betrag mit Glow**)** · ING Extra 6.902,10 € · Bar 63,50 € · Geldmarkt-ETF
12.004,88 €.

**Fuß der Konten-Spalte**, neu gegenüber Ablauf 1 und 2: `margin-top:auto`,
`padding:12px 12px 0`, Oberkante `#1c1c38`, `gap:3px`, Overline „ZULETZT GESPIEGELT" +
„heute 21:40" Mono 500 13px `#7a7a98`.

**Chip-Leiste** `padding:14px 20px`, `gap:8px`, Unterkante `#1c1c38`: Chips
`padding:9px 13px`, `radius:999px`, `#16162e`, Rand `#2a2a4a`, Inter 500 13px `#7a7a98`.
Alle acht sichtbar: Zu prüfen (9) · Kategorie · Zeitraum · Payee · Betrag · Memo · Flag ·
Gelöschte zeigen. „Alle Filter löschen" mit `margin-left:auto`, ohne Fläche und Rand,
Inter 600 13px `#00d4aa`.

**Tabelle**, Spaltenraster `44px 96px 1fr 196px 1fr 132px 150px` = Kästchen · Datum ·
Payee · Kategorie · Memo · Betrag (rechts) · **Status (rechts, 150px)**.
- Kopf `height:40px`, `padding:0 20px`, Grund **`#111128`**, Unterkante `#2a2a4a`, Labels
  Mono 600 10px `letter-spacing:.14em` `#4a4a68`. Ablauf 1 und 2 nannten 42px und
  `#1a1a36`; diese Tafel zeichnet 40px und `#111128`.
- Zeilen `height:52px`, Unterkante `#1c1c38`; Kästchen 17 × 17, `radius:5px`, `#16162e`,
  Rand `#2a2a4a`; Datum Mono 500 13px `#7a7a98`; Payee Inter 600 14px `#e8e8f0`;
  Kategorie Inter 500 13px `#a855f7` **voll qualifiziert** („Wohnen: Möbel"); Memo
  Inter 400 13px **`#4a4a68`** (Ablauf 2 nannte `#7a7a98`); Betrag rechts Mono 600 14px
  `tabular-nums` `#ff3b5c`.
- Status-Zelle: `flex-direction:column; align-items:flex-end; gap:3px`, bis zu zwei
  Badges. „Zu prüfen" und „Abweichung" `padding:3px 8px`, `rgba(255,31,143,.12)`, Rand
  `rgba(255,31,143,.35)`, `#ff1f8f` · „Nicht in YNAB" `padding:3px 8px`,
  `rgba(255,107,44,.12)`, Rand `rgba(255,107,44,.35)`, `#ff6b2c` · „Geprüft"
  `padding:3px 9px`, `rgba(0,255,136,.1)`, Rand `rgba(0,255,136,.3)`, `#00ff88` ·
  „In YNAB gelöscht" `padding:3px 8px`, `rgba(255,59,92,.12)`, Rand `rgba(255,59,92,.35)`,
  `#ff3b5c`.

**Tastatur-Hinweis** als Fußzeile, `padding:12px 20px`, `#08081a`, Oberkante `#1c1c38`,
`gap:16px`, Inter 500 12px `#4a4a68`, Tasten in Mono `#7a7a98`: `↑ ↓` bewegen ·
`Leertaste` auswählen · `G` geprüft · `Enter` Detail öffnen.

**Zwölf Zeilen:**

| Datum | Payee | Kategorie | Memo | Betrag | Status |
|---|---|---|---|---|---|
| 16.09. | Bauhaus | Wohnen: Möbel | Dübel und Leisten | -84,90 € | Zu prüfen + Nicht in YNAB |
| 16.09. | REWE Mitte | Alltag: Lebensmittel | | -48,12 € | Zu prüfen + Abweichung |
| 15.09. | Stadtwerke Musterstadt | Wohnen: Nebenkosten | | -142,00 € | Zu prüfen |
| 15.09. | Kita-Beitrag Stadt | Familie: Kinder | | -125,00 € | Geprüft |
| 12.09. | Apotheke am Markt | Gesundheit: Apotheke | | -18,20 € | Geprüft |
| 12.09. | Netflix | Abos: Streaming | | -12,99 € | Geprüft |
| 10.09. | Deutsche Bahn | Mobilität: Bahn | | -29,90 € | Zu prüfen |
| 09.09. | REWE Mitte | Alltag: Lebensmittel | | -52,30 € | Geprüft |
| 08.09. | Bäckerei Krause | Alltag: Lebensmittel | | -4,80 € | Geprüft |
| 05.09. | Amazon Marketplace | Alltag: Haushalt | Kabeltrommel | -37,45 € | Zu prüfen |
| 03.09. | Café Lotte | Alltag: Restaurants | | -14,60 € | Geprüft |
| 01.09. | Stadtwerke Musterstadt | Wohnen: Nebenkosten | | -142,00 € | Geprüft |

Kein Filter aktiv, deshalb keine Summenzeile.

### 7.7 Seitenpanel: noch nicht in YNAB

**Panel** rechts, Breite **420px**, `flex:none`, linke Kante `1px solid #2a2a4a`, Grund
`#111128`, `overflow:hidden`. Die Liste bleibt sichtbar.

**Kopf** (`padding:18px 18px 16px`, Unterkante `#1c1c38`): Betrag „-84,90 €" Mono 600
**28px** `tabular-nums` `#ff3b5c`; Payee „Bauhaus" Space Grotesk 600 17px; „Girokonto
Comdirect · 10.09.2026" Inter 400 12px `#7a7a98`; Schließen 36 × 36 wie am Handy.

**Hinweisleiste Orange** wie in 7.2, Knopf „Erneut senden" mit `min-height:36px`.

**Felder** je `min-height:56px`, `padding:0 18px`, Unterkante `#1c1c38`:
- **Kategorie** → „Wohnen: Möbel" `#a855f7` + Chevron
- **Split** → „Kein Split" Inter 400 13px `#4a4a68` + Chevron. Split-Bearbeitung passiert
  nur hier.
- **Memo** → „Dübel und Leisten" `#e8e8f0`, kein Chevron
- **Geprüft** → `min-height:60px`, Grund `#16162e`, Schalter aus

**HERKUNFT** „Comdirect-Import", **HISTORIE** mit den zwei Einträgen aus 7.2.

**Liste bei offenem Panel:** Spaltenraster wird
`40px 82px 1fr 168px 118px 140px`, die **Memo-Spalte fällt weg**; die Chip-Leiste verliert
„Memo" und „Flag" und ist laut Notiz waagerecht scrollbar (im HTML steht dazu nur
`overflow:hidden`, keine Scroll-Angabe). Die angeklickte Zeile ist markiert: Grund
`#1e1e3c`, **ohne** den Teal-Ring, den Ablauf 1 mit
`box-shadow:inset 0 0 0 1px rgba(0,212,170,.45)` festgelegt hat.

**Tastatur-Hinweis bei offenem Panel:** `↑ ↓` bewegen · `G` geprüft · `Esc` Panel
schließen. `Leertaste` und `Enter` fallen weg.

### 7.8 Seitenpanel: Historie und Abweichung

Markierte Zeile ist „REWE Mitte" (`#1e1e3c`).

**Kopf:** „-48,12 €" Mono 600 28px `#ff3b5c`, „REWE Mitte", „Girokonto Comdirect ·
10.09.2026".

**Hinweisleiste Pink (v2):** Badge „Abweichung" + „ab v2"; Text „BB sagt **Alltag:
Lebensmittel**, YNAB sagt **Alltag: Haushalt**." Am PC steht die Kategorie **voll
qualifiziert**, auch im Hinweis. Aktionen „YNAB übernehmen" (primär) und „BB durchsetzen"
(Ghost), je `min-height:36px`.

**Felder:** Kategorie „Alltag: Lebensmittel" + Chevron · Split „Kein Split" + Chevron ·
Memo **„Memo hinzufügen"** als Platzhalter Inter 400 13px `#4a4a68`, kein Chevron ·
Geprüft-Schalter aus.

**HERKUNFT** „Regel", **HISTORIE** mit den drei Einträgen aus 7.3. Die Zeitleiste ist der
eigentliche Inhalt dieses Panels: drei Einträge, drei Urheber, drei Punktfarben.

### 7.9 Gelöschte zeigen

**Chips:** aktiv „Gelöschte zeigen" ×, aktiv „Zeitraum: Dieser Monat" ×, dann inaktiv
Zu prüfen (9) · Kategorie · Payee · Betrag · Memo · Flag, rechts „Alle Filter löschen".
Aktiv-Chip `padding:9px 13px`, `rgba(0,212,170,.12)`, Rand `rgba(0,212,170,.4)`, Inter 600
13px `#00d4aa`, „×" 13px Strichstärke 2.6.
Die Notiz sagt, am PC bleibe die Chip-Reihenfolge fest. Gezeichnet stehen die beiden
aktiven Chips vorn (siehe Offene Punkte).

**Summenzeile** `padding:11px 20px`, Grund `#111128`, Unterkante `#1c1c38`: „13 Buchungen"
Inter 500 12px `#7a7a98`, „-712,26 €" Mono 600 **14px** `#ff3b5c` (Ablauf 1 und 2 nannten
am PC 15px).
**Abweichung 2 greift hier:** „13 Buchungen, davon 1 in YNAB gelöscht" und **-722,06 €**.

Spaltenraster wieder `44px 96px 1fr 196px 1fr 132px 150px`, weil kein Panel offen ist.

**Gelöschte Zeile** an ihrem Platz: 15.09. · Café Lotte · Alltag: Restaurants · „mit
Lena" · -9,80 €, alle vier Zellen in `#4a4a68`, Badge **„In YNAB gelöscht"**. Am Handy
heißt dasselbe Badge „in YNAB gelöscht"; die Groß- und Kleinschreibung ist im Entwurf
nicht einheitlich. Das Kästchen bleibt anklickbar.

Elf Zeilen sichtbar, zwei weitere beim Scrollen.

### 7.10 Seitenpanel einer gelöschten Buchung

Markierte Zeile „Café Lotte" (`#1e1e3c`, Inhalt grau).

**Kopf:** Betrag „-9,80 €" Mono 600 28px **`#7a7a98`**, Payee „Café Lotte" Space Grotesk
600 17px `#e8e8f0`, „Girokonto Comdirect · 15.09.2026".

**Hinweisleiste Rot** wie in 7.5, **kein Knopf**.

**Felder ohne Chevron:** Kategorie „Alltag: Restaurants" `#4a4a68` · Split „Kein Split"
`#4a4a68` · Memo „mit Lena" `#4a4a68`. **Kein Geprüft-Schalter, keine primäre Aktion.**

**HERKUNFT** „Comdirect-Import".

**HISTORIE**, die längste des Ablaufs, vier Einträge:
1. Punkt `#ff3b5c` · „YNAB-Spiegel: in YNAB gelöscht" · 11.09.2026 08:20 · Sync
2. Punkt `#00ff88` · „Roman in BB: geprüft" · 16.09.2026 07:12 · BudgetBuddy
3. Punkt `#a855f7` · „Regel: Kategorie Restaurants gesetzt" · 15.09.2026 19:40 · Regel „Café Lotte"
4. Punkt `#00d4aa` · „Comdirect-Import: Buchung erfasst" · 15.09.2026 19:40 · Import

Ein „Wiederherstellen" gibt es nicht. Das wäre ein Schreibvorgang nach YNAB und liegt
hinter v2.

## Interaktionen und Verhalten

**Handy**

| Geste | Wirkung |
|---|---|
| Tipp auf eine Zeile | öffnet das Detail als BottomSheet |
| Tipp auf eine gelöschte Zeile | öffnet das Lese-Sheet (7.5): keine Chevrons, kein Schalter, keine Aktion |
| Tipp auf Chip „Gelöschte zeigen" | blendet gelöschte Buchungen ein, aktive Chips wandern nach vorn, Summenzeile erscheint |
| Tipp auf „×" am Chip | entfernt genau diesen Filter |
| „Alle Filter löschen" | setzt alle Filter zurück, Summenzeile verschwindet |
| Tipp auf Kategorie-Feld im Sheet | öffnet den Kategorie-Picker (eigener Ablauf) |
| Tipp auf Schließen oder Griff | schließt das Sheet |
| Historie scrollen | die Zeitleiste im Sheet scrollt, wenn mehr Einträge dazukommen |

**PC**

| Eingabe | Wirkung |
|---|---|
| Klick auf eine Zeile oder `Enter` | öffnet das Seitenpanel rechts, Liste bleibt sichtbar, Zeile wird markiert |
| `↑` `↓` | Markierung bewegen, das offene Panel folgt zur nächsten Buchung |
| `G` | markierte Zeile prüfen |
| `Esc` | Panel schließen |
| `Leertaste` | Zeile aus- oder abwählen (nur bei geschlossenem Panel im Hinweis genannt) |
| Klick auf Kategorie- oder Split-Zeile im Panel | öffnet Picker bzw. Split-Bearbeitung |
| Panel offen | Memo-Spalte fällt weg, Chip-Leiste verliert „Memo" und „Flag" |

**Zustände, die jeder Screen braucht:** Ladezustand beim Nachladen (`Loading`), Fehler
(`ErrorDisplay`, Text benennt was passiert ist), Leerzustand ohne Treffer (sagt, was als
Nächstes zu tun ist). Die Historie braucht einen eigenen Leerzustand für Buchungen ohne
Einträge sowie einen Ladezustand, weil sie je Buchung nachgeladen wird; die Tafel zeigt
beides nicht.

## Zustand

- `openDetail`: Buchung im Sheet (Handy) bzw. Seitenpanel (PC); am PC folgt es
  `keyboardCursor`
- `keyboardCursor`: markierte Zeile am PC, Grund `#1e1e3c`
- `entryOrigin`, die Herkunft-Art der Buchung: Comdirect-Import, Regel, YNAB-Spiegel,
  später Handeingabe (Quick Add)
- `entryHistory`: Liste aus `getEntryHistory` (ADR 0013), **aufsteigend geliefert**
  (`ORDER BY sequence`) und **in der Anzeige gedreht**, je Eintrag Satz, Zeitstempel,
  Urheber und daraus die Punktfarbe. Als `RemoteData`, weil je Buchung nachgeladen
- `ynabLink`: vorhanden oder fehlend; fehlt sie, trägt der Zustand **Pflicht-Grund**
  (Push ausstehend oder gescheitert, Zeitstempel, Fehlerart, ADR 0021)
- `divergence`: offene Abweichung zwischen BB und YNAB je Feld (**v2**); solange offen,
  auch in der Liste als Badge
- `deletedInYnab`: Lesezustand samt Löschzeitpunkt
- `filters.showDeleted`: steuert Einblenden; eingeblendete gelöschte Buchungen zählen in
  `totals.count` **und** `totals.sum`
- `totals`: `{ count, sum, deletedCount }` vom Server über den gesamten Treffer;
  `deletedCount` trägt den Zusatz „davon 1 in YNAB gelöscht"
- `accountBalances`: aus dem Zustand, gelöschte Buchungen **immer** draußen
- `lastMirroredAt`: für den Fuß der Konten-Spalte („heute 21:40")

Diese Scheibe schreibt nichts. „Erneut senden", „YNAB übernehmen" und „BB durchsetzen"
sind Knöpfe ohne Wirkung, bis die schreibenden Scheiben da sind.

## Design-Tokens

Alle Werte stammen aus dem Design-System. Im HTML stehen sie als Literale, damit die
Screens sofort zeichnen. **Im Code sind die Tokens zu verwenden.**

**Flächen**

| Wert | Verwendung in Ablauf 7 |
|---|---|
| `#08081a` | App, Geräte-Grund, Konten-Spalte, Fußzeile |
| `#111128` | Card, Summenzeile, **Seitenpanel**, Tabellenkopf, Schalterbahn |
| `#1a1a36` | elevated: BottomSheet, aktive Konto-Karte und aktive Konto-Zeile |
| `#16162e` | Input, Knöpfe, inaktive Chips, Kästchen, Geprüft-Zeile, Ghost-Knopf |
| `#1e1e3c` | markierte Tabellenzeile |
| `#2a2a4a` | Rand normal, Griff des Sheets |
| `#1c1c38` | Rand zart, Zeilentrenner |

**Neon**

| Farbe | Wert | Bedeutung in Ablauf 7 |
|---|---|---|
| Orange | `#ff6b2c` (Verlauf nach `#e55a1f`) | primär: „Erneut senden", „YNAB übernehmen"; Kennzeichen „noch nicht in YNAB" und Status „Nicht in YNAB"; Hinweisfläche `rgba(255,107,44,.07)`; Punktfarbe **nicht** belegt |
| Teal | `#00d4aa` | aktiv: Chips, aktives Konto, „Alle Filter löschen"; **Herkunft-Badge**; Punkt **Import** |
| Green | `#00ff88` | positive Beträge, Status „Geprüft", Punkt **eigene Prüfung** |
| Red | `#ff3b5c` | negative Beträge; „in YNAB gelöscht" (Badge, Hinweisfläche `rgba(255,59,92,.07)`); Punkt **Fehler und Löschung** |
| Purple | `#a855f7` | Kategorie, auch im Abweichungstext; Punkt **Regel** |
| Pink | `#ff1f8f` | Status „Zu prüfen", Ungeprüft-Punkt in der Liste, Badge und Fläche der **Abweichung** |
| Grau | `#7a7a98` | Punkt **YNAB-Spiegel**; Betrag einer gelöschten Buchung im Kopf |

**Text:** `#e8e8f0` primär · `#7a7a98` sekundär und ältere Historie-Einträge · `#4a4a68`
tertiär, Overlines, Zeitstempel und alle Werte einer gelöschten Buchung.

**Typografie**
- Space Grotesk 600/700: Titel 22px (Handy), Wortmarke 18px, Payee im Sheet und Panel 17px, Logo-„B" 20px
- Inter 400/500/600: Fließtext und Bedienelemente, 10px bis 15px
- JetBrains Mono 400/500/600 mit `tabular-nums`: **alle** Zahlen, Beträge, Daten,
  Zeitstempel, Zähler, Overlines, Tastenkürzel. Detail-Betrag 30px (Handy) bzw. 28px (PC)

**Radien:** 5px Kästchen · 10px Knopf, Navigation, Schließen-Knopf · 12px Konto-Karte,
Symbolknopf, Logo · 14px PC-Rahmen · 22px Sheet oben · 30px Handy-Rahmen · 999px Chips,
Badges, Schalter, Historie-Punkte.

**Glow, sparsam:** `0 0 18px rgba(0,212,170,.12)` aktive Karte und aktive Konto-Zeile ·
`text-shadow:0 0 12px rgba(0,255,136,.35)` aktiver positiver Betrag ·
`0 0 20px rgba(255,107,44,.5)` primärer Knopf · `0 0 8px rgba(255,31,143,.7)`
Ungeprüft-Punkt.

**Schatten:** `0 -24px 60px rgba(0,0,0,.65)` BottomSheet. Das Seitenpanel am PC hat keinen
Schatten, nur die linke Kante `1px solid #2a2a4a`.

**Trefferflächen:** am Handy nie unter 44px, also Chips über `padding:12px 14px` +
`line-height:20px`, Sheet-Felder `min-height:56px`, Geprüft-Zeile 60px, Knöpfe im Hinweis
`min-height:44px`, Symbolknöpfe 44 × 44. Am PC sind die Knöpfe im Hinweis 36px.

## Neu zu bauende Komponenten

Im HTML aus vorhandenen Bausteinen zusammengesetzt, weil es sie im Design-System noch
nicht gibt. Diese gehören als echte Komponenten ins Repo:

| Teil | Zusammengesetzt aus | Anmerkung |
|---|---|---|
| **Herkunft-Zeile** | Badge + Overline | eine Zeile über der Historie, Teal-Badge, vier Arten |
| **Historie-Zeitleiste** | Text + farbige Punkte | Punktfarbe nach Urheber, oberster Eintrag hell, neueste Zeile oben, scrollbar im Sheet |
| **Hinweisleiste mit Grund und Aktion** | Card + Badge + Button | drei Färbungen (Orange, Pink, Rot), immer direkt unter dem Kopf, Aktion sitzt im Hinweis |
| **Seitenpanel (PC)** | Card + Feldzeilen | 420px, `#111128`, linke Kante; Liste bleibt sichtbar, Memo-Spalte weicht, `Esc` schließt |
| **Status-Zelle mit zwei Badges** | Badge, gestapelt | `align-items:flex-end`, `gap:3px`, höchstens zwei |
| **Lesezustand für gelöschte Buchungen** | Variante von Sheet und Panel | Betrag grau, Felder ohne Chevron, kein Schalter, keine primäre Aktion |
| **Geprüft-Schalter** | Primitives | 50 × 30px, Bahn `#111128`, Knauf 22px |
| Filter-Chip-Leiste mit Zähler und „×" | Primitives-Chip + Aktivzustand Teal | Handy: aktive Chips nach vorn. PC: feste Reihenfolge |
| Summenzeile mit Zusatz | Card + Money | „13 Buchungen, davon 1 in YNAB gelöscht" plus Summe |
| Konto-Stände-Leiste (Handy) / Konten-Spalte (PC) | Card | dazu der Fuß „ZULETZT GESPIEGELT" |

## Assets

Keine Bilder. Alle Symbole sind Inline-SVG, `stroke`-basiert, `stroke-linecap:round`,
Strichstärke 1.7 bis 2.6, im Code durch die `Icons`-Komponente zu ersetzen: Chevron unten
(Konto-Wechsler, 16px), Chevron rechts (Feldzeile, 15px), X (Schließen 14px, Chip 13px),
Lupe und Filter (je 19px), Tab-Bar-Symbole (21px). Schriften liegen als Variable Fonts vor
(Subset „latin", SIL OFL 1.1).

## Erfundene Daten

Nur diese Werte verwenden.

**Konten:** Girokonto Comdirect 2.418,37 € (aktiv) · ING Extra 6.902,10 € · Bar 63,50 € ·
Geldmarkt-ETF 12.004,88 € · Summe „Alle Konten" 21.388,85 €. Zuletzt gespiegelt: heute
21:40.

**Buchungen im Ablauf**

| Datum | Payee | Kategorie | Betrag | Herkunft | Besonderheit |
|---|---|---|---|---|---|
| 16.09. | Bauhaus | Wohnen: Möbel | -84,90 € | Comdirect-Import | noch nicht in YNAB, Push gescheitert 10.09. 18:02, Netzwerkfehler |
| 16.09. | REWE Mitte | Alltag: Lebensmittel | -48,12 € | Regel „REWE Mitte" | Abweichung: BB Lebensmittel, YNAB Haushalt (v2) |
| 15.09. | Stadtwerke Musterstadt | Wohnen: Nebenkosten | -142,00 € | | zu prüfen |
| 15.09. | Kita-Beitrag Stadt | Familie: Kinder | -125,00 € | | geprüft |
| 15.09. | Café Lotte | Alltag: Restaurants | -9,80 € | Comdirect-Import, Regel „Café Lotte" | in YNAB gelöscht am 11.09.2026 08:20 |
| 12.09. | Apotheke am Markt | Gesundheit: Apotheke | -18,20 € | | geprüft |
| 12.09. | Netflix | Abos: Streaming | -12,99 € | | geprüft |
| 10.09. | Deutsche Bahn | Mobilität: Bahn | -29,90 € | | zu prüfen |
| 09.09. | REWE Mitte | Alltag: Lebensmittel | -52,30 € | | geprüft |
| 08.09. | Bäckerei Krause | Alltag: Lebensmittel | -4,80 € | | geprüft |
| 05.09. | Amazon Marketplace | Alltag: Haushalt | -37,45 € | | zu prüfen, Memo „Kabeltrommel" |
| 03.09. | Café Lotte | Alltag: Restaurants | -14,60 € | | geprüft |
| 01.09. | Stadtwerke Musterstadt | Wohnen: Nebenkosten | -142,00 € | | geprüft |

**Memos:** „Dübel und Leisten" (Bauhaus) · „mit Lena" (Café Lotte, gelöscht) ·
„Kabeltrommel" (Amazon Marketplace).

**Zähler und Summen:** Chip „Zu prüfen (9)". Mit „Gelöschte zeigen" und „Zeitraum: Dieser
Monat" zeigt der Entwurf **13 Buchungen · -712,26 €**; gebaut wird **13 Buchungen, davon 1
in YNAB gelöscht · -722,06 €** (Abweichung 2).

**Zeitstempel** (alle erfunden): Import Bauhaus 10.09.2026 17:58, Push gescheitert
10.09.2026 18:02 · Import und Regel REWE Mitte 10.09.2026 17:55, YNAB-Spiegel 11.09.2026
08:12 · Import und Regel Café Lotte 15.09.2026 19:40, geprüft 16.09.2026 07:12, in YNAB
gelöscht 11.09.2026 08:20.

## Voice

Deutsch, direkt, knapp, ohne Füllwörter. Du-Form. Knöpfe als Verb oder Verb plus Objekt:
„Erneut senden", „YNAB übernehmen", „BB durchsetzen", „Alle Filter löschen". Beträge
sprechen über Vorzeichen und Farbe, Format `-48,12 €`.

Die Historie spricht in ganzen Sätzen und nennt immer Urheber und Wirkung:
„Comdirect-Import: Buchung erfasst", „Regel: Kategorie Lebensmittel gesetzt",
„YNAB-Spiegel: Kategorie in YNAB geändert: Lebensmittel → Haushalt", „Push nach YNAB
gescheitert: Netzwerkfehler", „Roman in BB: geprüft".

Hinweise nennen Grund, Zeitpunkt und Folge. Kein Kennzeichen ohne Grund.

Der Entwurf trennt in zwei Hinweistexten den Zeitstempel von der Fehlerart mit einem
Gedankenstrich. Im Repo gilt dort Komma oder Doppelpunkt.

## Offene Punkte für Roman

1. **Status-Spalte und `ledger-qns4j`.** Die Spalte trägt hier 150px und bis zu zwei
   Badges. `ledger-qns4j` will dieselbe Spalte schmaler machen. Das steht quer und ist
   hier nicht aufgelöst.
2. **„Abweichung" und „Zu prüfen" tragen dasselbe Pink** (`#ff1f8f`, gleiche Fläche,
   gleicher Rand). In der Zeile „REWE Mitte" stehen dadurch zwei Badges in identischer
   Farbe übereinander und sind nur am Text zu unterscheiden.
3. **Zeitstempel der gelöschten Buchung widersprechen sich.** Import und Regel stehen auf
   15.09.2026 19:40, die Prüfung auf 16.09.2026 07:12, die Löschung auf 11.09.2026 08:20,
   also vor dem Import. Die Notiz zu 7.10 beschreibt die Reihenfolge „geprüft, dann in
   YNAB gelöscht", die Zahlen sagen etwas anderes. Entweder Löschung auf 16.09. oder
   Import früher legen.
4. **Handy und PC zeigen zur gelöschten Buchung unterschiedlich viel Historie.** Am Handy
   (7.5) drei Einträge, am PC (7.10) vier, der Geprüft-Eintrag fehlt am Handy. Wenn beide
   dasselbe zeigen sollen, fehlt am Handy ein Eintrag.
5. **Chip-Reihenfolge am PC.** Die Notiz zu 7.9 sagt, aktive Chips wandern am PC nicht nach
   vorn; gezeichnet stehen „Gelöschte zeigen" und „Zeitraum: Dieser Monat" trotzdem
   zuerst. Ablauf 2 hat die feste Reihenfolge am PC festgelegt.
6. **Badge-Schreibweise „in YNAB gelöscht" gegen „In YNAB gelöscht"** (Handy gegen PC) und
   „noch nicht in YNAB" in der Liste gegen „Noch nicht in YNAB" im Detail. Eine Schreibung
   festlegen.
7. **Drei Maße weichen von Ablauf 1 und 2 ab:** Tabellenkopf 40px und `#111128` statt 42px
   und `#1a1a36`, Memo-Spalte `#4a4a68` statt `#7a7a98`, Summe am PC 14px statt 15px.
   Dazu fehlt der markierten Zeile der Teal-Ring aus Ablauf 1. Entweder Ablauf 7 anpassen
   oder die früheren Angaben nachziehen.
8. **Felder je Screen sind ungleich besetzt.** Am Handy hat 7.2 Kategorie und Memo, 7.3 nur
   Kategorie, 7.5 Kategorie und Memo; am PC haben alle drei zusätzlich Split. Wenn das
   Panel immer dieselben Felder zeigen soll, gilt die PC-Fassung, und am Handy kommt Split
   dazu.
9. **Chip „Zu prüfen (9)" gegen die Liste.** In 7.6 sind zwölf Zeilen sichtbar, davon fünf
   mit „Zu prüfen". Der Zähler gilt über das ganze Konto; ob das so gemeint ist, sagt die
   Tafel nicht.
10. **Zählung in der Notiz zu 7.1.** Dort steht „fünf der neun ungeprüften Zeilen plus vier
    geprüfte sind sichtbar", gezeichnet sind sechs Zeilen (drei ungeprüft, drei geprüft).
11. **Ungeprüft-Punkt am Handy** ist hier 7 × 7px inline vor dem Payee, in Ablauf 1
    6 × 6px absolut am linken Rand. Eine Variante festlegen.
12. **Historie braucht Lade- und Leerzustand.** Sie wird je Buchung nachgeladen; die Tafel
    zeigt weder das Laden noch eine Buchung ohne Einträge.
