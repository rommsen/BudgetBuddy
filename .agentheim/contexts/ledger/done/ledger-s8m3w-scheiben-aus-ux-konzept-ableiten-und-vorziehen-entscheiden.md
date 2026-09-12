---
id: ledger-s8m3w
title: Bau-Scheiben aus dem Kassenbuch-UX-Konzept ableiten und über das Vorziehen schreibender Scheiben vor v2 entscheiden
status: done
type: decision
context: ledger
created: 2026-09-11
completed: 2026-09-12
depends_on: [ledger-xbrht]
blocks: []
tags: [frontend, ux, kassenbuch, scheiben, vision]
related_adrs: [0012, 0013, 0021, 0028]
related_research: []
prior_art: [ledger-q5w9c, ledger-kt0wq]
---

## Why
`ledger-xbrht` entwirft das Kassenbuch als Arbeitsfläche für das ganze Ziel und liefert
einen klickbaren Prototyp. Gebaut wird in Scheiben, zuerst die lesenden. Welche Scheiben
es gibt, in welcher Reihenfolge, und ob schreibende Abläufe (geprüft, Kategorie, Split,
Flags) schon vor v2 kommen, lässt sich erst sinnvoll festlegen, wenn Roman den Prototyp
durchgespielt hat. Deshalb ein eigener Task (Entscheidung Roman, 2026-09-11, bei der
Verfeinerung von `ledger-xbrht`).

## What
1. **Scheiben ableiten** aus Konzept und Prototyp von `ledger-xbrht`, als Backlog-Tasks im
   `ledger` (bzw. `ynab-sync`, wo der YNAB-Verkehr betroffen ist). Voraussichtliche
   Reihenfolge, beim Ableiten zu prüfen:
   - lesend: Gesamtsicht mit Kontoständen, Filter, Mehrfachauswahl mit Summe,
     Herkunft/Historie und "noch nicht in YNAB" in der neuen Liste;
   - Spiegel-Erweiterung: `approved` und Flags in Snapshot und Beobachtungs-Ereignissen,
     damit "noch nicht geprüft" und das Rückfrage-Flag überhaupt lesend angezeigt werden
     können;
   - schreibend: geprüft markieren, Kategorie ändern, Split ändern, Flags setzen, jeweils
     mit Rückschreibe-Weg nach YNAB und Abweichungs-Sicht.
2. **`ledger-p3n8k`** (gelöschte Buchungen ausblenden) in eine Scheibe aufgehen lassen oder
   ausdrücklich als eigenständig bestätigen.
3. **Entscheidung mit Roman:** werden schreibende Scheiben vor v2 gebaut? Wenn ja, Vision
   (`.agentheim/vision.md`, Stufen v1/v2) anpassen und die Entscheidung als ADR festhalten.

## Acceptance criteria
- [x] Jede Scheibe ist als Backlog-Task erfasst, mit Styleguide-Gate
      (`design-system-001` in `depends_on`) und Verweis auf ihren Prototyp-Screen bzw. den
      Screenshot unter `docs/ux/kassenbuch/`.
- [x] Die Scheiben stehen in einer Reihenfolge mit `depends_on`, lesende vor schreibenden;
      die Spiegel-Erweiterung für `approved` und Flags ist eine eigene Scheibe vor jeder
      Anzeige, die sie braucht.
- [x] `ledger-p3n8k` ist in einer Scheibe aufgegangen (und per DISMISS oder REFINE
      bereinigt) oder ausdrücklich als eigenständig bestätigt.
- [x] Ob schreibende Scheiben vor v2 gebaut werden, ist mit Roman entschieden und als ADR
      festgehalten; wenn ja, ist die Vision angepasst.

## Notes
- **"Geprüft" ist fachlich Romans Prüfvermerk.** Technisch ist es vermutlich YNABs
  `approved` (Direktimport-Buchungen wie ING kommen unbestätigt an); `cleared` trägt für
  Roman keine Information, weil importierte Buchungen immer schon bei der Bank sind. Beim
  Modellieren der schreibenden Scheibe festzurren.
- **Der Spiegel kennt heute weder `approved` noch Flags.** Der Ereignis-Katalog
  (`ledger/README.md`, "Ereignisse") beobachtet Details, Kategorisierung, Cleared und
  Löschung. Schon eine lesende Anzeige "noch nicht geprüft" oder "Rückfrage-Flag" braucht
  eine Erweiterung von Snapshot und Beobachtungs-Ereignissen (Katalog-Änderung, mit Roman).
- Schreibende Scheiben heißen "BB entscheidet, YNAB wird nachgezogen" (ADR 0012): BB
  braucht dafür den Rückschreibe-Weg nach YNAB (Update bestehender Buchungen) und die
  Abweichungs-Sicht. Das ist Vision-Stufe v2 und größer als eine UI-Scheibe.
- Läuft wie `ledger-xbrht` in der Hauptsession mit Roman (Modeling-Arbeit, kein Code).
  Bleibt im Backlog, bis der Prototyp aus `ledger-xbrht` durchgespielt ist; die genaue
  Form hängt von dessen Ergebnis ab.

## Outcome

**Sieben Scheiben geschnitten**, lesend vor schreibend, jede mit Styleguide-Gate
(`design-system-001`) und Verweis auf ihren Brief in `docs/ux/kassenbuch/briefs.md`:

| Scheibe | Inhalt | depends_on |
|---|---|---|
| `ledger-b1xwe` | Liste seitenweise, Gesamtsicht, Kontostände-Leiste | design-system-001 |
| `ledger-vgfw3` | Filter (Kategorie mehrfach, Zeitraum, Payee, Betrag, Memo) mit Summenzeile | b1xwe |
| `ledger-r1d2f` | Auswahl-Modus mit Summe der Auswahl | vgfw3 |
| `ledger-q1hw2` | Detail: Herkunft, Historie, "noch nicht in YNAB" | b1xwe |
| `ledger-kdnnz` | Spiegel um Prüfvermerk und Flags erweitern (lesend), Chip "Zu prüfen" | b1xwe |
| `ledger-kwwxz` | Available-Betrag je Kategorie spiegeln und zeigen | design-system-001 |
| `ledger-r777c` | Prüfvermerk setzen und nach YNAB zurückschreiben (schreibend) | kdnnz |

`ledger-b1xwe` ist das Fundament: ohne serverseitige, seitenweise Abfrage gibt es weder
Filter noch Summenzeile noch Gesamtsicht. Sie wurde am 2026-09-12 zusätzlich verfeinert
(neun Akzeptanzkriterien) und als erste gebaut.

**`ledger-p3n8k` bleibt eigenständig.** Klein, gut verfeinert, betrifft genau eine Stelle
der Liste; ihn in `ledger-b1xwe` aufgehen zu lassen hätte diese Scheibe nur vergrößert. Er
hängt jetzt an `ledger-b1xwe`.

**Die v2-Entscheidung ist gefallen: ADR 0028.** Der Prüfvermerk ist die einzige
schreibende Fähigkeit, die vor v2 nach BB wandert. Begründung: Romans häufigster Ablauf am
Handy ist schreibend, ein rein lesendes v1 läuft damit in das Scheiterkriterium der Vision.
Der Prüfvermerk ist zugleich die kleinste denkbare schreibende Fähigkeit (ein Feld,
idempotent, keine Budget-Wirkung, triviale Konfliktauflösung). Kategorie, Split und
Lena-Zuweisung bleiben v2, weil sie den allgemeinen Rückschreibe-Weg und die
Abweichungs-Sicht brauchen. Die Vision ist entsprechend angepasst (v1 bekommt den
Prüfvermerk als benannte Ausnahme).

**Was offen bleibt:** die schreibenden v2-Scheiben (Kategorie, Split, Lena-Zuweisung) sind
bewusst noch nicht geschnitten. Sie hängen am allgemeinen Rückschreibe-Weg und an der
Abweichungs-Sicht, die beide noch nicht modelliert sind. Das gehört in eine eigene
Modeling-Runde, wenn v1 steht.
