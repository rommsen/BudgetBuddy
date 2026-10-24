---
id: ledger-h6mer
title: "Split-Zeilen-Überträge: Bestandsfälle auf der Live-Datenbank nachverknüpfen"
status: done
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

Einmalig, auf der Live-Datenbank auf `docker-host`, **vom Conductor in der Hauptsitzung
ausgeführt** (Romans Entscheidung, 2026-10-24). `ledger-h4nxz` ist deployt: der Container
`budgetbuddy-app` läuft seit dem Deploy vom 2026-10-24 mit einem Image, das Commit 3250658
enthält.

Echte Lage (Stand 2026-10-24, abweichend vom alten `deploy-rules.sh`-Muster):
Datenbank `/opt/stacks/budgetbuddy/data/budgetbuddy.db` (Besitzer root, ~36 MB), Compose-Stack
`/opt/stacks/budgetbuddy`, Container `budgetbuddy-app`. Auf dem Host gibt es kein `sqlite3`
und kein `sudo`.

1. **Vorher zählen:** Datenbank per `docker cp budgetbuddy-app:/app/data/budgetbuddy.db` (Pfad im
   Container vorher mit `docker inspect` prüfen) oder `scp` nach lokal kopieren und dort mit
   `sqlite3` zählen:
   `SELECT event_type, COUNT(*) FROM ledger_entry_events WHERE event_type IN
   ('EntrySplitTransferCounterpartObservedInYnab','EntryTransferCounterpartObservedInYnab')
   GROUP BY event_type;`
2. **Container stoppen** (`docker compose stop` im Stack-Verzeichnis).
3. **Backup** nach `~/backups/budgetbuddy-<datum>-vor-h6mer.db` auf `docker-host`, mit `cmp`
   gegen das Original prüfen.
4. **Settings-Schlüssel löschen:** `DELETE FROM settings WHERE key LIKE
   'ledger_mirror_server_knowledge_%';` (Schlüsselmuster aus
   `Ledger.CommandHandlers.serverKnowledgeKey`). Weg ohne `sqlite3` auf dem Host: lokal an der
   Kopie ausführen und zurückspielen (`docker cp` in den gestoppten Container oder über einen
   kurzlebigen Container mit gemountetem Volume); vorher prüfen, dass die Live-Datei seit dem
   Kopieren unverändert ist (`cmp` mit dem Backup).
5. **Container starten**, Health abwarten (`docker inspect ... {{.State.Health.Status}}`).
6. **Spiegel-Lauf auslösen** über den Knopf im Kassenbuch. Ohne gespeichertes `server_knowledge`
   holt er voll (`since_date=1900-01-01`, ADR 0046 Punkt 7).
7. **Nachher zählen** wie in Schritt 1 und das Lauf-Ergebnis (geänderte Buchungen) notieren.

Kein dauerhaftes Skript, kein Button, kein API-Endpunkt: ein wiederkehrender Vollabgleich ist
nach ADR 0014 abgelehnt. Die Befehle werden einzeln im Outcome protokolliert.

## Acceptance criteria
- [ ] Backup existiert auf `docker-host` unter `~/backups/`, `cmp` gegen die Live-Datei vor dem
      Eingriff ist gleich; Pfad steht im Outcome.
- [ ] Die Zahl der `EntrySplitTransferCounterpartObservedInYnab`-Ereignisse ist nach dem Lauf
      um ungefähr 206 gestiegen (abzüglich Split-Zeilen, deren Gegenbuchung nicht gespiegelt
      ist, z. B. weil das Zielkonto geschlossen wurde). Vorher- und Nachher-Zahl stehen im
      Outcome, Abweichungen vom Soll sind erklärt.
- [ ] Die Zahl der `EntryTransferCounterpartObservedInYnab`-Ereignisse ist in derselben
      Größenordnung gestiegen; Zahlen im Outcome.
- [ ] Das Lauf-Ergebnis meldet 0 geänderte Buchungen, oder jede Abweichung ist im Outcome
      einzeln erklärt (ein Vollabruf nach Jahren kann echte, bisher verpasste Änderungen
      aufdecken; das ist kein Fehler dieses Tasks).
- [ ] Stichprobe: das Beispielpaar aus ADR 0046 (Girokonto-Buchung
      `f668dd4a-4f88-4bbb-9413-f9c1a462a540`, Bar-Buchung
      `8466b531-94a6-4a52-a14d-655113cef732`) trägt nach dem Lauf auf beiden Seiten die
      Verknüpfung (`ledger_entry_split_transfers` bzw. `TransferEntryId`), per SQL an einer
      Kopie der Live-Datei belegt.
- [ ] Ein neuer Settings-Schlüssel `ledger_mirror_server_knowledge_<budgetId>` steht nach dem
      Lauf wieder in `settings` (der nächste normale Lauf ist wieder ein Delta-Abruf).
- [ ] Die App ist nach dem Eingriff healthy, das Kassenbuch lädt.

## Notes

Dieser Task fasst die Live-Datenbank an und kann erst nach dem Deploy von `ledger-h4nxz`
laufen. Deshalb als eigener, abhängiger Task geschnitten statt als Teil von `ledger-h4nxz`
(Begründung und vollständige Modellierung in ADR 0046, Punkt 7).

**Nicht an einen Worker dispatchbar, vom Conductor selbst auszuführen** (Romans Entscheidung,
2026-10-24). Es ist eine Operation auf der Produktionsumgebung (Container, Volume,
Settings-Tabelle) ohne Code-Änderung; ein Worktree-Worker hätte nichts zu committen, und der
`verifier` greift nicht, weil sein Gate für Worker-Ergebnisse definiert ist. Präzedenzfälle:
`ledger-v8n4t`, `infrastructure-qwthn`. Die Kriterien sind deshalb so geschnitten, dass sie
über Zählungen und SQL-Stichproben ohne Verifier belegbar sind. Vor Schritt 2 holt sich der
Conductor Romans ausdrückliches Go, weil der Container kurz stoppt.

Verfeinert am 2026-10-24 (Sammel-Modeling): Ausführender festgelegt, Pfade auf den echten
Deploy-Stand (`/opt/stacks/budgetbuddy`, kein `sqlite3`/`sudo` auf dem Host) gezogen,
Vorher-/Nachher-Zählung und Rücksetz-Prüfung des Settings-Schlüssels ergänzt.

## Outcome

Kein Eingriff nötig: die Nachverknüpfung war auf der Live-Datenbank schon vollständig, als der Conductor am 2026-10-24 um 15:00 nachzählte. Roman hat daraufhin entschieden, den Task ohne Stopp, Backup und Vollabruf zu schließen (2026-10-24).

**Befund (Kopie der Live-Datei per `scp docker-host:/opt/stacks/budgetbuddy/data/budgetbuddy.db`, nur lesend, Container lief weiter):**
- Mount laut `docker inspect budgetbuddy-app`: `/opt/stacks/budgetbuddy/data -> /app/data`, Container healthy, keine WAL-Dateien neben der Datenbank.
- `EntrySplitTransferCounterpartObservedInYnab`: 206 Ereignisse, `EntryTransferCounterpartObservedInYnab`: 768 Ereignisse. Alle 974 tragen denselben Zeitstempel `2026-10-20T05:59:10.0077895Z`; die Beobachtungen entstanden also in einem einzigen Lauf am 20.10. (vermutlich der erste Spiegel-Lauf nach dem Deploy von ledger-h4nxz). Das Soll aus den Kriterien ("um ungefähr 206 gestiegen") ist damit schon erreicht, ein weiterer Vollabruf hätte nichts verknüpft.
- `ledger_entry_split_transfers`: 197 Zeilen; 197 Buchungen tragen `split_transfer_counterparts`, das entspricht den 197 historischen Split-Zeilen-Überträgen aus dem Why.
- Stichprobe Beispielpaar ADR 0046 (über `ledger_entry_links.value`): Girokonto-Buchung `f668dd4a-...` ist Kassenbuch-Eintrag `01a157e4-9baf-7b03-985e-1b65277cd2c4` mit `split_transfer_counterparts = [{"ynabSubtransactionId":"f585c0f4-c211-4217-adb6-6be0f6b619f9","counterpartEntryId":"01a157e4-9bf4-7857-8b5c-4d64fbdc52ef"}]` und einer Zeile in `ledger_entry_split_transfers`; Bar-Buchung `8466b531-...` ist Eintrag `01a157e4-9bf4-7857-8b5c-4d64fbdc52ef` mit `transfer_entry_id = 01a157e4-9baf-7b03-985e-1b65277cd2c4`. Beide Seiten verknüpft.
- `settings` enthält `ledger_mirror_server_knowledge_e0b4c3e7-d283-423e-8e30-3f37b6328ebf` (Wert 47950), der normale Lauf ist ein Delta-Abruf.
- App nach der Prüfung: `docker inspect` healthy (gestartet 2026-10-24 10:46 UTC), HTTPS-Aufruf 200.

**Nicht gemacht (bewusst):** Container-Stopp, Backup nach `~/backups/`, Löschen der Settings-Schlüssel, Vollabruf. Die Kriterien dazu sind durch den Befund gegenstandslos; kein Schreibzugriff auf die Live-Umgebung.
