---
id: 0021
title: BB-geführte Buchungen entstehen zuerst im Kassenbuch, der YNAB-Push folgt; die Verknüpfung ist ein eigenes Ereignis
scope: ledger
status: accepted
date: 2026-09-11
supersedes: []
superseded_by: []
related_tasks: [ledger-cscw1, categorization-gdrx8, ledger-qvt84, ledger-tdnpk]
related_adrs: [0004, 0013, 0015]
related_research: []
---

## Context
Die Vision (v1-Kriterium) verlangt, dass das Kassenbuch sich merkt, was BB selbst
entschieden hat. Heute landet eine Comdirect-Buchung nach dem Push nur über den Spiegel
im Kassenbuch, mit Herkunft "YNAB-Spiegel" statt "Comdirect-Import"/"Regel". Roman hat am
2026-09-11 entschieden, dass eine BB-geführte Buchung "beim Push" entsteht (Comdirect-
Import) bzw. für Quick Add ausdrücklich "erst Kassenbuch, dann Push" gilt. Offen blieb die
technische Reihenfolge für den Comdirect-Fall: Kassenbuch-Schreiben und YNAB-Push sind
zwei getrennte, nicht atomare Schritte (Push ist ein HTTP-Call, keine SQLite-Transaktion,
ADR 0013 Punkt 5 verlangt aber eine SQLite-Transaktion für `appendAndApply`).

Drei Spezialisten (strategic-modeler, tactical-modeler, architect) wurden dazu befragt.
Architect und strategic-modeler empfehlen unabhängig voneinander, das Kassenbuch **vor**
dem Push zu schreiben, symmetrisch zu Quick Add; der tactical-modeler war zunächst von der
umgekehrten Reihenfolge (Push, dann Kassenbuch) ausgegangen. Grund für die Revision: bei
einem HTTP-Timeout weiß BB nicht, ob YNAB die Buchung angelegt hat. Schreibt man erst nach
erfolgreichem Push, fehlt die Buchung bei einem Timeout ganz; ein erneuter Push würde von
YNAB als Duplikat abgelehnt, und der nächste Spiegel-Lauf legt die Buchung dann mit
Herkunft "YNAB-Spiegel" an, nicht mit ihrer echten Herkunft. Die ImportId
(`BB:{txId}`, Comdirect) steht schon vor dem Push fest und ändert sich nie; der Spiegel
matcht ohnehin YNAB-Id vor ImportId (ADR 0015). Diese Eigenschaft macht "Kassenbuch
zuerst" robust gegen Timeout und Absturz: der nächste Spiegel-Lauf verknüpft nach, ohne
dass die Buchung ihre BB-Herkunft verliert.

## Decision
1. **Reihenfolge:** Für jede BB-geführte Buchung, die nach YNAB gepusht wird
   (Comdirect-Import wie Quick Add), gilt dieselbe Reihenfolge: zuerst wird die Buchung im
   Kassenbuch geschrieben (`appendAndApply`), danach erst der HTTP-Push nach YNAB versucht.
   "Entsteht beim Push" heißt: als Teil desselben Vorgangs, nicht erst nach dessen
   bestätigtem Erfolg.
2. **Kein Rollback bei Push-Fehler.** Scheitert der Push (Netzwerkfehler, Timeout,
   YNAB-Fehler) oder lehnt YNAB die Buchung als Duplikat ab, bleibt die Kassenbuch-Buchung
   unverändert stehen (Append-Only, ADR 0013). Sie zeigt dann "noch nicht in YNAB" (keine
   `ynab`-Verknüpfung), bis ein erneuter Push oder der nächste Spiegel-Lauf sie über die
   ImportId verknüpft.
3. **Vorab-Prüfung gegen Doppelbuchung.** Bevor eine Buchung geschrieben wird, prüft der
   Aufrufer, ob ihre ImportId bereits verknüpft ist (z. B. weil ein vorheriger Versuch
   schon geschrieben hat oder der Spiegel schneller war). Ist sie es, entsteht keine
   zweite Buchung.
4. **Die YNAB-Verknüpfung ist ein eigenes Ereignis**, `EntryLinkedToYnab` (Payload:
   `YnabTransactionId`), nicht nur eine Zeile in der Fremd-Id-Tabelle ohne Ereignis. Ein
   reiner Tabellen-Eintrag ohne Ereignis wäre der erste Zustand, den `apply`/Replay nicht
   aus dem Log rekonstruieren könnte (ADR 0013 Punkt 6 verlangt vollständigen Replay).
   `EntryLinkedToYnab` entsteht sowohl direkt aus der Push-Antwort (Actor `ComdirectImport`
   bzw. `QuickAdd`) als auch, wenn der Spiegel eine noch unverknüpfte Buchung über die
   ImportId findet (Actor `YnabMirror`).
5. Diese Reihenfolge gilt für alle künftigen BB-geführten Schreibwege (auch v2s
   `BuchungÄndern, Push folgt`), nicht nur für die beiden hier modellierten Fälle.

## Consequences
### Positive
- Ein Timeout oder Prozess-Absturz zwischen Kassenbuch-Schreiben und Push-Antwort verliert
  nie die BB-Herkunft: die Buchung existiert bereits mit "Comdirect-Import"/"Regel"/
  "Quick Add", nur die YNAB-Verknüpfung fehlt vorübergehend.
- Symmetrisch zu Quick Add (von Roman bereits so entschieden): ein einheitliches Muster
  "erst Kassenbuch, dann Push, dann Verknüpfung" für alle BB-geführten Wege.
- `EntryLinkedToYnab` macht "seit wann in YNAB" auditierbar und hält Replay vollständig.

### Negative
- Ein gescheiterter Push hinterlässt eine Kassenbuch-Buchung ohne YNAB-Gegenstück; bis zum
  nächsten erfolgreichen Push oder Spiegel-Lauf zeigt das Kassenbuch etwas, das in YNAB
  noch nicht existiert. Sichtbar zu machen ("noch nicht in YNAB") ist Arbeit der
  Lese-Sichten, hier nur als Konsequenz benannt.
- Quick Add hat weiterhin ein kurzes Fenster zwischen erfolgreicher Push-Antwort und dem
  Schreiben von `EntryLinkedToYnab`: stürzt der Prozess genau dort ab, kann der nächste
  Spiegel-Lauf keine ImportId nutzen (Quick Add hat laut ADR 0004 keine) und legt eine
  zweite Buchung als "YNAB-Spiegel" an. Für v1 hingenommen (keine Deduplizierung über
  Betrag/Datum/Payee als Regel, ADR 0015 lehnt Fuzzy-Match im Spiegel ab).

### Neutral
- Der Push selbst bleibt weiterhin außerhalb der SQLite-Transaktion des Kassenbuchs; ADR
  0013 Punkt 5 gilt unverändert für das Kassenbuch-Schreiben allein.

## Alternatives considered
- **Push zuerst, Kassenbuch danach** (ursprüngliche naheliegende Lesart von "entsteht beim
  Push"): abgelehnt. Bei Timeout oder Absturz nach erfolgreichem Push fehlt die Buchung im
  Kassenbuch; ein Wiederholversuch wird von YNAB als Duplikat abgelehnt, und der nächste
  Spiegel-Lauf legt sie mit falscher Herkunft ("YNAB-Spiegel" statt "Comdirect-Import"/
  "Regel") an. Das unterläuft genau das v1-Kriterium der Vision.
- **Kassenbuch-Schreiben und Push in einer gemeinsamen Transaktion**: technisch
  unmöglich, Push ist ein externer HTTP-Call.
- **Bare Fremd-Id-Verknüpfung ohne eigenes Ereignis für die YNAB-Id**: abgelehnt, weil
  Replay (ADR 0013 Punkt 6) diesen Zustand dann nicht mehr aus dem Log herleiten könnte.

## Nachtrag (2026-10-19, ledger-rynk5)

Scheitert der Push, hält das Ereignis `EntryPushToYnabFailed` Grund und Zeit fest; `EntryLinkedToYnab` löst es auf (Punkt 2 bleibt: kein Rollback).
