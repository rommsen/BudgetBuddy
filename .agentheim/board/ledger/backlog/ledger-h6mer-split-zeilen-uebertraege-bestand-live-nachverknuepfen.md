---
id: ledger-h6mer
title: "Split-Zeilen-Überträge: Bestandsfälle auf der Live-Datenbank nachverknüpfen"
status: backlog
type: chore
context: ledger
created: 2026-10-02
completed:
depends_on: [ledger-h4nxz]
blocks: []
tags: [ledger, spiegel, uebertrag, split, deploy]
related_adrs: [0014, 0046]
related_research: []
prior_art: [ledger-h4nxz]
---

## Why

`ledger-h4nxz` (ADR 0046) baut `EntrySplitTransferCounterpartObservedInYnab` und den
Nachlauf `linkSplitTransferCounterparts`, der neue Beobachtungen verknüpft. Die 197
historischen Split-Zeilen-Überträge in Romans Live-Kassenbuch (195 Girokonto, 1 ING
Girokonto, 1 Kreditkarte als Quelle, 206 Übertrags-Zeilen, 190 Richtung "Bar", 16 Richtung
"Depot") sind aber größtenteils seit Jahren unverändert. Ein normaler Spiegel-Lauf mit dem
gespeicherten `server_knowledge` holt sie nie erneut als Beobachtung vorbei, also entsteht
für sie nie ein Verknüpfungs-Ereignis, solange niemand den Vollabruf auslöst (ADR 0046 Punkt
7).

## What

Nach dem Deploy von `ledger-h4nxz`, einmalig, auf der Live-Datenbank:

1. Container stoppen (Volume unter `~/my_apps/budgetbuddy/`, wie bei `deploy-rules.sh`).
2. Den Settings-Schlüssel `ledger_mirror_server_knowledge_<budgetId>` löschen (Tabelle
   `settings`, Schlüsselmuster aus `Ledger.CommandHandlers.serverKnowledgeKey`).
3. Container starten, einen Spiegel-Lauf auslösen (löst wegen des fehlenden
   `server_knowledge` einen Vollabruf aus, `since_date=1900-01-01`, nicht die
   rund-12-Monate-Grenze, siehe ADR 0046 Punkt 7).
4. Lauf-Ergebnis prüfen.

Ein kleines Skript unter `scripts/` (Muster `deploy-rules.sh`) ist sinnvoll für Schritt 2+3,
kein dauerhafter Button und kein API-Endpunkt: ein wiederkehrender Vollabgleich ist nach ADR
0014 abgelehnt, das hier ist ein einmaliger, bewusst manuell ausgelöster Schritt.

## Acceptance criteria
- [ ] Nach dem Lauf: Zahl der neuen `EntrySplitTransferCounterpartObservedInYnab`-Ereignisse
      liegt bei ungefähr 206 (abzüglich Split-Zeilen, deren Gegenbuchung nicht gemirrort ist,
      z. B. weil das Zielkonto zwischenzeitlich geschlossen wurde): Soll-Wert im Outcome mit
      der tatsächlichen Zahl abgleichen und Abweichungen erklären.
- [ ] Zahl der neuen `EntryTransferCounterpartObservedInYnab`-Ereignisse auf den
      Gegenbuchungen (Bar/Depot) liegt in derselben Größenordnung.
- [ ] Der Lauf meldet `changedEntries = 0` oder jede Abweichung ist im Outcome einzeln
      erklärt (ein Vollabruf nach Jahren kann echte, bisher verpasste Veränderungen aufdecken,
      das ist kein Fehler dieses Tasks, aber zu dokumentieren).
- [ ] Stichprobe: das Beispielpaar aus ADR 0046 (Girokonto-Buchung
      `f668dd4a-4f88-4bbb-9413-f9c1a462a540`, Bar-Buchung
      `8466b531-94a6-4a52-a14d-655113cef732`) trägt nach dem Lauf auf beiden Seiten die
      Verknüpfung (`SplitTransferCounterparts` bzw. `TransferEntryId`).
- [ ] Kein Datenverlust: vor dem Löschen des Settings-Schlüssels ein Backup des Volumes
      ziehen (Pfad im Outcome nennen).
- [ ] Ausgeführt gegen die Live-Datenbank, nicht gegen die Entwicklungsdatenbank-Kopie: das
      ist hier ausdrücklich der Zweck des Tasks, anders als bei allen übrigen Tasks dieser
      Gruppe.

## Notes

Dieser Task fasst die Live-Datenbank an und kann erst nach dem Deploy von `ledger-h4nxz`
laufen. Deshalb als eigener, abhängiger Task geschnitten statt als Teil von `ledger-h4nxz`
(Begründung und vollständige Modellierung in ADR 0046, Punkt 7, und im Scratchpad-Ergebnis
`tactical-split-transfer.md` der Sitzung vom 2026-10-02, Abschnitt "Entscheidung 6").

**Vermutlich nicht an einen Worker dispatchbar.** Das Vorgehen ähnelt `ledger-v8n4t` und dem
dort genannten Präzedenzfall `infrastructure-qwthn`: es ist eine Operation auf der
Produktionsumgebung (Container, Volume, Settings-Tabelle), kein Worktree-Checkout. Ob der
Conductor das selbst übernimmt oder ein Worker mit Produktions-Zugriff existiert, ist bei der
nächsten Verfeinerung zu klären.
