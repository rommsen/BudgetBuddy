---
id: ledger-mas5g
title: "Spike: Lässt die YNAB-API zu, die Split-Zeilen einer bestehenden Buchung zu setzen, zu ändern und aufzuheben, und mit welchem Aufruf"
status: todo
type: spike
context: ledger
created: 2026-10-24
completed:
depends_on: []
blocks: [ledger-kxme4, ledger-pafkp]
tags: [backend, ynab, split, spike, v2]
related_adrs: [0006, 0028, 0054, 0055]
related_research: []
prior_art: [ledger-k2rbn]
---

## Why
Roman will Split-Buchungen in BB bearbeiten (Entscheidung 2026-10-24). Der Schreibweg nach
YNAB ist dafür offen: Nach der YNAB-OpenAPI ist nach meiner Erinnerung das Ändern der
`subtransactions` einer bestehenden Buchung nicht vorgesehen (das Feld gilt beim Anlegen).
Das ist nicht belegt. Davon hängt ab, ob `ledger-kxme4` und `-6` überhaupt wie geplant
gebaut werden können oder ob ein anderer Weg nötig ist (Buchung löschen und neu anlegen,
Splits nur in BB halten).

## What
Zu klären, jeweils mit Beleg (OpenAPI-Text, Antwort der API):
1. Nimmt `PUT /budgets/{id}/transactions/{tid}` oder `PATCH /budgets/{id}/transactions`
   `subtransactions` an, um (a) eine unaufgeteilte Buchung aufzuteilen, (b) die Zeilen einer
   Split-Buchung zu ändern, (c) einen Split aufzuheben? Welche Fehler kommen zurück?
2. Falls nein: Was ist der kleinste sichere Ersatz? Kandidaten: Buchung in YNAB löschen und
   mit gleicher `import_id` neu anlegen (Folgen für `approved`, `cleared`, Verknüpfung,
   Übertrags-Gegenbuchungen und die YNAB-Id im Kassenbuch), oder Splits nur in BB führen
   und die Abweichung zu YNAB sichtbar machen (Abweichungs-Sicht, ADR 0028).
3. Übertrags-Zeilen im Split (`transfer_account_id` je Subtransaction): schreibbar?

Der Spike läuft gegen die OpenAPI-Beschreibung und, wo nötig, gegen ein Wegwerf-Budget in
YNAB (nie gegen Romans echtes Budget). Ergebnis ist eine kurze Notiz in der Task und, wenn
sich eine Richtung ergibt, ein ADR-Entwurf; die Folge-Tasks `ledger-kxme4` und `-6`
werden danach angepasst.

## Acceptance criteria
- [ ] Zu jeder der drei Fragen aus Punkt 1 steht ein Ergebnis mit Beleg (Zitat aus der
      OpenAPI oder aufgezeichnete Antwort).
- [ ] Ist Punkt 1 verneint: ein empfohlener Ersatz mit Folgen für `approved`, `cleared`,
      Verknüpfung und Übertrags-Gegenbuchungen.
- [ ] `ledger-kxme4` und `-6` sind auf das Ergebnis angepasst oder als hinfällig
      markiert; Roman ist über die Wahl informiert, wenn sie eine Abweichung berührt.
- [ ] Es wurde keine Buchung in Romans echtem Budget verändert.

## Notes
Entworfen am 2026-10-24 (Sammel-Modeling). Ziel-Lage: todo, kann sofort starten. Rahmen: ein
Arbeitstag. Ist die Lösung mitten im Spike schon bekannt und billig, halte sie fest und hör
auf. Der Spike schreibt keinen Produktivcode. Dateien: keine im Quellbaum (höchstens ein
Wegwerf-Skript im Scratchpad).
