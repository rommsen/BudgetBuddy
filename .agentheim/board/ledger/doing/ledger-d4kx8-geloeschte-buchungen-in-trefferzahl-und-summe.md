---
id: ledger-d4kx8
title: "Gelöschte Buchungen: Entwurf zählt sie in der Trefferzahl, aber nicht in der Summe; gebaut zählen sie in beidem"
status: doing
type: decision
context: ledger
created: 2026-09-18
completed:
depends_on: []
blocks: []
tags: [frontend, backend, kassenbuch, summenzeile, entwurf-gegen-code]
related_adrs: [0017]
related_research: []
prior_art: [ledger-p3n8k, ledger-b1xwe]
---

## Why
Beim Abgleich der Screens aus Claude Design mit dem Repo (2026-09-18) aufgefallen.

**Der Entwurf** (Ablauf 7): gelöschte Buchungen "zählen **in der Trefferzahl, nicht in der
Summe**" und nie im Kontostand. Als offener Punkt steht dort die Alternative daneben: sie
ganz aus Zähler und Summe nehmen, dann stimmt die Zahl aber nicht mehr mit den sichtbaren
Zeilen überein.

**Gebaut ist das nicht so.** Bei eingeblendeten gelöschten Buchungen
(`IncludeDeleted = true`) läuft dieselbe SQL-Klausel für beides:
- `src/Server/Ledger/Persistence.fs:538` (Trefferzahl) und `:584` (Beträge) bauen je
  `let deletedClause = if includeDeleted then "" else "AND deleted_in_ynab = 0"`.
- `src/Server/Ledger/QueryHandlers.fs:300-316` bildet daraus `TotalCount` und
  `TotalAmount = sumMoney "EUR" totalAmounts`; `sumMoney` (`src/Shared/Domain.fs:873-876`)
  summiert alles, was es bekommt.

Gelöschte zählen eingeblendet also in Zähler **und** Summe. Der Kontostand geht bewusst
einen anderen Weg (`ledgerBalancesByAccount` lässt gelöschte immer draußen), deshalb können
Summenzeile und Kontostand bei eingeschaltetem Schalter auseinanderlaufen. Genau diese
Übereinstimmung nennt der Doc-Kommentar in `Persistence.fs:579-580` als Zweck des
Standardfalls.

## What
Entscheiden, was die Summenzeile bei eingeblendeten gelöschten Buchungen zeigt, und die
gewählte Variante herstellen.

1. **Wie im Entwurf:** Zähler mit, Summe ohne. Die Summe bleibt dann mit dem Kontostand
   vergleichbar. Preis: Zähler und Summe beziehen sich auf verschiedene Mengen, das braucht
   eine Beschriftung, die das sagt.
2. **Wie gebaut:** beides mit. Ehrlich gegenüber der sichtbaren Liste (die Zeilen summieren
   sich zur Summe), weicht aber vom Kontostand ab.
3. **Dritte Variante aus dem Entwurf:** gelöschte ganz aus Zähler und Summe nehmen, obwohl
   sie sichtbar sind.

`ledger-p3n8k` hat den Standardfall (ausgeblendet) bereits sauber gelöst; hier geht es nur
um den eingeschalteten Schalter. Die Anzeige "N gelöschte Buchungen eingeblendet" steht
schon über der Liste und ist der natürliche Ort für den Hinweis.

## Entscheidung (Roman, 2026-09-18)
**Variante 2 gilt: eingeblendet zählen gelöschte Buchungen in Trefferzahl und Summe.** Der
gebaute Stand bleibt, der Entwurf wird angepasst. Begründung: so addieren sich die
sichtbaren Zeilen genau zur angezeigten Summe. Die Variante des Entwurfs hätte Zähler und
Summe über verschiedene Mengen gebildet, was ohne erklärenden Text nicht zu verstehen ist.

Was daraus folgt:
1. **Die Zeile über der Liste sagt es aus**, im Stil "13 Buchungen, davon 1 in YNAB
   gelöscht". Die bestehende Zeile "N gelöschte Buchungen eingeblendet" aus `ledger-p3n8k`
   ist der Ort dafür.
2. Der **Kontostand** lässt gelöschte weiterhin immer draußen
   (`ledgerBalancesByAccount`). Dass Summe und Kontostand bei eingeschaltetem Schalter
   auseinanderlaufen, ist damit gewollt und benannt, nicht mehr ein stiller Widerspruch.
3. **Am Code ändert sich nichts** an Trefferzahl und Summe; die Arbeit steckt in der
   Beschriftung und darin, dass die Summenzeile aus `ledger-vgfw3` dieselbe Regel trägt.
4. Im Design-Projekt steht die Gegenregel noch in der `CLAUDE.md` (Abschnitt "Aus Ablauf 7
   dazugekommen"). Sie muss dort weg, sonst kommt sie beim nächsten Zeichnen zurück.
   **Das kann kein Werkzeug von hier aus tun:** `CLAUDE.md` und `.claude/` sind im
   Design-Projekt reservierte Pfade und werden abgelehnt, weil sie den Design-Agenten
   steuern. Der einzufügende Text liegt fertig in
   `docs/ux/kassenbuch/design-projekt-nachtrag-2026-09-18.md`, Abschnitt 2; Roman gibt ihn
   in einen Chat des Design-Projekts.

## Acceptance criteria
- [ ] Am Code für Trefferzahl und Summe ändert sich nichts: `getLedgerEntriesPage` liefert
      bei `IncludeDeleted = true` weiterhin beide **einschließlich** gelöschter Buchungen
      (Variante 2). Ein Test hält das fest, damit die Regel nicht später still kippt.
- [ ] Die Zeile über der Liste sagt bei eingeblendeten gelöschten Buchungen, wie viele der
      gezählten gelöscht sind, im Stil "13 Buchungen, davon 1 in YNAB gelöscht". Ort ist die
      bestehende Zeile aus `ledger-p3n8k` (`deletedCountText` /
      `deletedBanner` in `src/Client/Components/Ledger/View.fs`).
- [ ] Sind gelöschte Buchungen ausgeblendet (Standard), bleibt die Zeile wie heute
      ("N gelöschte Buchungen ausgeblendet") — dieser Fall ist von `ledger-p3n8k` gelöst und
      wird nicht angefasst.
- [ ] Singular und Plural stimmen in beiden Zuständen; durch Tests der reinen
      Textfunktion belegt, nicht per Augenschein.
- [ ] Der Kontostand lässt gelöschte Buchungen weiterhin immer draußen
      (`ledgerBalancesByAccount`), auch bei eingeschaltetem Schalter. Ein Test hält fest,
      dass Summenzeile und Kontostand hier bewusst auseinanderlaufen dürfen.
- [ ] Die Regel steht im ledger-README, damit die Summenzeile aus `ledger-vgfw3` sie
      übernimmt statt sie neu zu entscheiden.
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

**Nicht Teil dieser Aufgabe:** Punkt 4 der Entscheidung (die Gegenregel in der `CLAUDE.md`
des Design-Projekts). Das kann kein Werkzeug von hier aus: `CLAUDE.md` und `.claude/` sind
dort reservierte Pfade. Der fertige Text liegt in
`docs/ux/kassenbuch/design-projekt-nachtrag-2026-09-18.md`, Abschnitt 2; Roman gibt ihn in
einen Chat des Design-Projekts.

## Notes
Erfasst am 2026-09-18 beim Abgleich mit dem Design-Projekt "Gelesen UI-Mockups"
(`5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`). Brief: `docs/ux/kassenbuch/briefs.md`, Ablauf 7.

Betrifft die Summenzeile aus `ledger-vgfw3` mit: sobald sie gebaut wird, muss die Regel
feststehen.
