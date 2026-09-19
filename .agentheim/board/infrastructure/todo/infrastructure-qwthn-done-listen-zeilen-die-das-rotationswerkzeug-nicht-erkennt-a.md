---
id: infrastructure-qwthn
title: "Done-Listen-Zeilen, die das Rotationswerkzeug nicht erkennt, auf die erwartete Form bringen"
status: todo
type: chore
context: infrastructure
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [board, buchhaltung, rotation]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Die INDEX-Done-Listen-Rotation (`lib/index-rotation.mjs`, aufgerufen am Ende jedes
`work`-Laufs) meldet in vier von sechs Kontexten Zeilen, die sie nicht parsen kann. Stand
2026-09-19, nach dem Work-Lauf mit neun Aufgaben:

| Kontext | Einträge in der Done-Liste | davon nicht erkannt |
|---|---|---|
| design-system | 12 | 7 |
| ynab-sync | 8 | 3 |
| categorization | 2 | 2 |
| infrastructure | 4 | 2 |
| ledger | 20 | 0 |
| banking-import | 0 | 0 |

Das Werkzeug lehnt deshalb nichts ab und schreibt nichts: `rotated: false`, `ok: true` für
jeden Kontext. Es blockiert also nichts, und genau deshalb fällt es sonst niemandem auf.

**Die Folge tritt erst später ein.** Die Rotation rollt abgeschlossene Monate aus der
Live-Liste in ein Archiv unter `done-archive/YYYY-MM.md`, sobald die Liste ihre Kappe
erreicht. Für eine Zeile, die sie nicht erkennt, kann sie das nicht tun. Erreicht eine der
Listen die Kappe, bleiben diese Einträge liegen, während die erkannten ausrollen. Das
Ergebnis wäre eine halb rotierte Liste, deren Lücken niemand erklären kann, weil die Ursache
Monate zurückliegt.

Dass `ledger` mit zwanzig Einträgen sauber durchläuft und `categorization` mit zwei Einträgen
beide verfehlt, deutet darauf hin, dass es an der Form einzelner Zeilen liegt und nicht an
der Menge.

## What
1. **Zuerst herausfinden, welche Form das Werkzeug erwartet.** Die Regel steht in
   `lib/index-rotation.mjs` im Plugin-Cache
   (`~/.claude/plugins/cache/agentheim/agentheim/<version>/lib/`), nicht in diesem Repo. Ohne
   diesen Schritt ist jede Korrektur geraten.
2. **Die vierzehn nicht erkannten Zeilen benennen**, nicht nur zählen. Das Werkzeug meldet
   bisher nur eine Anzahl je Kontext; welche Zeile gemeint ist, muss aus dem Vergleich
   hervorgehen.
3. **Auf die erwartete Form bringen**, ohne inhaltlich etwas zu verlieren. Eine Done-Zeile
   trägt Id, Titel und Datum; nichts davon darf beim Umformen verschwinden.

**Zu klären beim Verfeinern:** ob die betroffenen Zeilen überhaupt aus derselben Ursache
stammen. Es können mehrere sein (ein fehlendes Datum, ein abweichendes Datumsformat, ein
mehrzeiliger Eintrag), und dann braucht jede ihre eigene Behandlung.

## Acceptance criteria
- [ ] Die erwartete Zeilenform ist benannt, mit Verweis auf die Stelle in
      `lib/index-rotation.mjs`, die sie festlegt.
- [ ] `rotateAllIndexDoneLists` meldet für alle sechs Kontexte keinen `unmatched`-Wert mehr
      (bzw. `unmatched: 0`). Belegt durch einen tatsächlichen Lauf, nicht durch Lesen der
      geänderten Zeilen.
- [ ] Keine Done-Zeile ist dabei verloren gegangen: die Zahl der Einträge je Kontext ist
      vorher und nachher gleich (12, 8, 2, 4, 20, 0).
- [ ] Id, Titel und Datum jeder umgeformten Zeile sind unverändert erhalten.

## Notes
Gefunden bei den Rotations-Checks am Ende des `work`-Laufs vom 2026-09-19 (neun Aufgaben,
Commits `440086b` bis `a13357d`). Die Prüfung lief zweimal, beim verfrühten ersten
Sitzungsende und am tatsächlichen; beide Male dieselben Zahlen bis auf die in diesem Lauf
hinzugekommenen Done-Einträge.

**Zur Id dieses Tasks:** Sie lautet bewusst `infrastructure-qwthn` und nicht `infra-qwthn`
wie die übrigen Tasks dieses Kontexts. Grund: `deriveContext` schneidet den Token ab und
liefert für `infra-*` den Wert `infra`, für den es kein Board-Verzeichnis gibt.
`materializeTaskFile` nutzt diese Ableitung ohne Override-Möglichkeit und legte damit einen
neuen, falschen Ordner an; `capture` prüft den Frontmatter-Kontext gegen dieselbe Ableitung
und lehnt mit `context-mismatch` ab, auch wenn man ihm `{"context":"infrastructure"}`
mitgibt. Die bestehenden `infra-*`-Tasks stammen aus `modeling` und sind davon nicht
betroffen. Ob der Kontext umbenannt oder die Ids künftig ausgeschrieben werden, ist eine
eigene, offene Frage.
