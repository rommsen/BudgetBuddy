---
id: ledger-x0yga
title: "Kassenbuch-Liste an das Mockup angleichen: Zahlenformat, Betragsfarbe, Datum, Memo, Spaltenbreiten, Konten-Spalte"
status: doing
type: bug
context: ledger
created: 2026-09-12
completed:
depends_on: [design-system-001]
blocks: []
tags: [frontend, kassenbuch, styleguide, visuell]
related_adrs: [0029]
related_research: []
prior_art: [ledger-b1xwe]
---

## Why
`ledger-b1xwe` hat die seitenweise Liste, die Gesamtsicht und die Kontostände gebaut, alle
neun Akzeptanzkriterien erfüllt und die Verifikation bestanden. Das Ergebnis sieht trotzdem
nicht wie das Zielbild aus. Roman beim ersten Blick auf die laufende App: "es sieht halt
so aus."

Ursache ist der Ablauf, nicht der Worker: das Mockup lag nur in Claude Design, der Worker
hat den Fließtext des UX-Konzepts gelesen, und kein Schritt hat das gebaute Bild angesehen.
Festgehalten im design-system-README, Abschnitt "Mockups gehören ins Repo, und
Frontend-Arbeit hat ein visuelles Gate".

## What
Das Zielbild liegt jetzt im Repo:
**`docs/ux/kassenbuch/mockups/1.9-pc-ausgangszustand-zeile-markiert.png`**
(Claude Design, PC 1440 × 900, Konto-Sicht "ING Extra" mit Chip "Zu prüfen").

Anzugleichen sind die Punkte, die im Umfang von `ledger-b1xwe` lagen:

| | heute | Zielbild |
|---|---|---|
| Zahlenformat | `38,690.62 EUR` | `21.388,85 €` |
| Beträge | weiß | rot bei negativ, grün bei positiv |
| Datum | `04.09.2026` | `Heute`, `Gestern`, sonst `08.09.` |
| Memo | kompletter Bank-Rohtext, Zeile fünf Zeilen hoch | eine Zeile, gekürzt |
| Spalten | verschmolzen (`04.09.2026GIRO`, Kopf `KONTOPAYEE`), Betrag läuft rechts raus | feste, sinnvolle Breiten |
| Konten links | hohe Karten mit Farbstreifen | kompakte Liste, Name links, Betrag rechts |
| Tabellenkopf | klebt hinter dem Seitenkopf, `DATUM` angeschnitten | sitzt sauber über der ersten Zeile |

**Zum Zahlenformat:** `formatMoneyText` in `src/Client/Components/Ledger/View.fs` setzt
zwar `CultureInfo("de-DE")`, aber die .NET-Kultur greift in der nach JavaScript
übersetzten Fassung nicht. Die Formatierung muss anders gelöst werden, und zwar an einer
Stelle für die ganze App, nicht je View.

**Zur Betragsfarbe:** die Handy-Liste nutzt bereits `Money.transactionWithCurrency` und ist
damit richtig. Die PC-Tabelle und die Kontostände-Karten geben `formatMoneyText` als
reinen Text aus und verlieren dadurch Farbsemantik und Ziffernschrift. Beträge gehören
überall über die Money-Komponente.

**Nicht in dieser Aufgabe:** Filterleiste, Summenzeile, Status-Kennzeichen,
Auswahl-Kästchen und die Tastaturhilfe am unteren Rand. Die sind im Mockup zu sehen, weil
es für das Ziel entworfen wurde, und gehören zu `ledger-vgfw3`, `ledger-r1d2f` und
`ledger-kdnnz`.

## Acceptance criteria
- [ ] Alle Beträge in der Kassenbuch-Seite laufen über die Money-Komponente des
      Design-Systems, auch in der PC-Tabelle und in den Kontostände-Karten.
- [ ] Beträge erscheinen im deutschen Format mit Euro-Zeichen (`-48,12 €`), negativ rot,
      positiv grün. Die Formatierung liegt an genau einer Stelle und ist getestet, auch
      für Tausendertrennung und Null.
- [ ] Das Datum steht als `Heute`, `Gestern` oder `08.09.` in der Liste; das volle Datum
      bleibt im Detail sichtbar.
- [ ] Die Memo-Zelle belegt höchstens eine Zeile und schneidet längeren Text ab; eine
      Buchungszeile ist damit gleich hoch wie jede andere.
- [ ] Die Tabellenspalten haben feste Anteile; kein Inhalt läuft aus dem sichtbaren
      Bereich, und keine zwei Spaltenköpfe stehen ohne Trennung nebeneinander.
- [ ] Die Kontostände am PC stehen als kompakte Liste (Name links, Betrag rechts), nicht
      als hohe Karten; das aktive Konto bleibt hervorgehoben.
- [ ] Der Tabellenkopf ist beim Scrollen vollständig sichtbar und wird nicht vom
      Seitenkopf überdeckt.
- [ ] Ein headless aufgenommener Screenshot der Seite (1440 × 900 und 390 × 844) liegt
      unter `docs/ux/kassenbuch/screenshots/` und zeigt keine der obigen Abweichungen.
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

## Notes
Geschnitten am 2026-09-12 nach Romans erstem Blick auf die laufende App. Das visuelle Gate
aus dem design-system-README gilt für diese Aufgabe selbst: vor dem Integrieren wird die
gebaute Seite aufgenommen und neben das Mockup gelegt.

Ein wiederverwendbares Skript für solche Aufnahmen ist `design-system-wa6wz`; solange es
fehlt, nimmt der Conductor die Seite von Hand auf (Playwright liegt im Projekt,
`scripts/export-design-system.mjs` zeigt das Muster).
