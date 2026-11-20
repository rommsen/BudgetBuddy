---
id: banking-import-r28hx
title: "Alle Konten per FinTS selbst abrufen (zuerst ING), damit YNABs eigener Abruf und Abgleich wegfallen"
status: backlog
type: feature
context: banking-import
created: 2026-11-13
completed:
depends_on: [banking-import-y7m4r]
blocks: []
tags: [captured, fints, ing]
related_adrs: [0093]
related_research: [ing-fints-anbindung-2026-11-06]
prior_art: [banking-import-d0fey, banking-import-pknvp]
---

## Why
Roman, 2026-11-13, nach einer doppelten Buchung im Kassenbuch (ledger-nkwqb): Solange YNAB die ING selbst abruft und Überträge
auf der Gegenseite anlegt, arbeiten zwei Importe gegeneinander. „Langfristig ist wirklich das Beste: wir machen diesen
FinTS-Call, sodass ich alle Konten selber holen kann und die ganze Logik in BB liegt.“

## What
Abruf der ING (und weiterer Konten) per FinTS in BB, Dedup wie beim Comdirect-Import. Grundlage: Recherche
`knowledge/research/ing-fints-anbindung-2026-11-06.md` (MT940, Verfahren 999, Produktregistrierung bei der DK).

## Acceptance criteria
- [ ] To be defined during refinement, nach dem Befund aus banking-import-y7m4r (FinTS-Sonde gegen die ING).

## Notes
Captured raw, unrefined. Vision nennt FinTS als spätere Ausbaustufe; Einordnung in den Fahrplan beim Verfeinern.

Produktregistrierung (2026-11-17): Formular 1.0.4 für „BudgetBuddy“ an registrierung@hbci-zka.de abgeschickt, Kategorie
„Web-Server“, Hersteller Roman als Privatperson. Die DK nennt 10 bis 15 Werktage bis zur Nummer, also etwa bis 08.12.2026.
- Die Nummer kommt aus `.env` in `AppSettings` und steht nie im Code, in Tests, in der Doku oder in `AppSettingsView`. Das
  Repo ist öffentlich, und der DK wurde zugesagt, dass die Nummer nur für Romans eigene Installation gilt und nicht im
  Quellcode veröffentlicht wird. Wer BB selbst betreibt, registriert eine eigene Nummer.
- Sie geht bei jeder Dialoginitialisierung in das Element „Produktbezeichnung“. „Produktversion“ bleibt Pflicht, gehört aber
  nicht zur Registrierung (fints.org, FAQ Produktregistrierung, Stand 28.06.2023).
- Die Standardnummer von python-fints ist eine Bibliotheksnummer. Solche Nummern sind laut FAQ nur für interne Tests
  erlaubt, im Betrieb nicht.

Verfeinerung 2026-11-20 (Roman):
- Produktregistrierungsnummer am 2026-11-20 erhalten, sie liegt in `.env` als `FINTS_PRODUCT_ID`.
- Zuerst der Spike banking-import-y7m4r (FinTS-Sonde gegen die ING), danach wird diese Task verfeinert. Die Vision führt
  FinTS für Romans eigene Konten seit heute als Ziel.
- Umfang: Girokonto und Extra-Konto. Das Depot klärt nur der Spike, gebaut wird es hier nicht.
- YNABs eigene ING-Verbindung bleibt eine Probezeit lang an. In dieser Zeit muss BB YNABs Importe des Girokontos als
  Duplikat erkennen (heute `PossibleDuplicate` über Betrag, Tag ±1 und Payee, ADR 0093). Danach trennt Roman die
  Verbindung in YNAB, BB ist dann die einzige Quelle wie bei Comdirect.
- Bibliothek: entscheidet der Architect nach dem Befund des Spikes; ein Python-Sidecar ist erlaubt.
- Beim Verfeinern zu klären: Abruf je Konto im selben Ablauf wie Comdirect (`fetchAndBook`, automatisch abrufen) oder
  eigener Takt; Anzeige „ING · Import <Stand>“; Meldung, wenn der 90-Tage-Log-in bei der ING fällig ist.
