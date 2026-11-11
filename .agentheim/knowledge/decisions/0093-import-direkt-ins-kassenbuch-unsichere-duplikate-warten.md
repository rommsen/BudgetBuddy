---
id: 0093
title: Import direkt ins Kassenbuch und nach YNAB, unsichere Duplikate warten im Kassenbuch, automatischer Abruf im Intervall
scope: global
status: accepted
date: 2026-11-11
supersedes: []
superseded_by: []
related_tasks: [banking-import-d0fey, banking-import-t4raw, ledger-r7h2w, ledger-v6s7c, ledger-fxt09, categorization-hz6vj]
related_adrs: [0021, 0048, 0050, 0071, 0083, 0086, 0087, 0091, 0092]
related_research: []
---

# ADR 0093: Import direkt ins Kassenbuch, unsichere Duplikate warten dort

## Context
Heute läuft ein Comdirect-Abruf in eine Prüfsitzung, die nur im Speicher lebt (`SyncSessionManager`, Status
`ReviewingTransactions`). Roman kategorisiert, teilt auf, ändert den Payee, fragt Lena und schließt mit
„Importieren“ ab (`importToYnab`). Erst dann entstehen die Buchungen im Kassenbuch (ADR 0021) und in YNAB. Die
Sitzung ist eine eigene Insel neben dem Kassenbuch. Etappe 3 (ADR 0086, Prototyp Version 17, Styleguide §7.4)
legt fest: abgerufene Umsätze gehen sofort ins Kassenbuch und nach YNAB als „zu prüfen“, nur unsichere
Duplikate warten, Abrufen läuft automatisch und jederzeit von Hand. Durchgesehen wird danach Karte für Karte von
der Übersicht aus.

Die Duplikaterkennung (`DuplicateDetection.fs`) vergleicht mit den YNAB-Buchungen des Import-Kontos: eine
gefundene Reference oder `import_id` ist ein sicheres Duplikat (`ConfirmedDuplicate`, heute übersprungen), ein
Treffer auf Betrag, Tag (±1) und Payee ist unsicher (`PossibleDuplicate`, heute nicht zurückgehalten).

ADR 0071 Punkt 7 sah ein Vorbereiten nach Zeitplan vor, das nichts bucht, Vorgabe aus.

## Decision
Entschieden von Roman am 2026-11-11. Ereignisse und Zustände abgestimmt mit dem `tactical-modeler`.

1. **Kein Prüfschritt vor dem Buchen.** Ein Abruf (von Hand oder automatisch) bucht jede neue Buchung sofort:
   `EntryImportedFromComdirect` mit `Approved = false` und `Uncleared` (also „zu prüfen“, ADR 0032), Regeln und
   Order-ID-Vorschlag wie heute als Kategorisierungs-Ereignisse, danach der Push nach YNAB und
   `EntryLinkedToYnab` oder `EntryPushToYnabFailed`. Die Reihenfolge aus ADR 0021 gilt unverändert. Die
   Prüfsitzung samt ihren Endpunkten entfällt; die Sitzung bleibt nur für Push-TAN und Abruf.
2. **Sichere Duplikate** werden wie heute übersprungen, es entsteht keine Buchung.
3. **Unsichere Duplikate warten im Kassenbuch.** Die Buchung entsteht wie jede andere, bekommt danach statt des
   Pushs das Ereignis `EntryHeldAsPossibleDuplicate` (Actor `ComdirectImport`, Payload: Grund als Satz, YNAB-Id
   des Treffers, die über die `ynab`-Verknüpfung aufgelöste EntryId des Treffers, falls bekannt, und ein
   Schnappschuss von Datum, Betrag, Payee und Kategorie des Treffers). Sie trägt im Kassenbuch das Kennzeichen
   „doppelt?“, geht nicht nach YNAB und übersteht einen Neustart, weil sie ein Ereignis ist (ADR 0083 Abschnitt 7).
4. **Ausweg aus dem Warten**, je ein Ereignis der Entscheidungs-Familie mit Actor `UserInBb`:
   - „Ist neu“: `EntryConfirmedAsNewInBb`, danach der Push wie bei „Erneut senden“ (ADR 0021, kein Rollback).
   - „Ist doppelt, verwerfen“: `EntryDiscardedAsDuplicateInBb`. Die Buchung gilt danach als gelöscht wie nach
     `EntryDiscardedInBb` (ADR 0091) und `EntryDeletedInBb` (ADR 0092): sie zählt nirgends. Ein YNAB-Aufruf ist
     nicht nötig, sie war nie in YNAB.
   - Rückgängig des Verwerfens: `EntryDiscardRevokedInBb`, die Buchung wartet wieder. Rückgängig gibt es im Toast
     direkt nach dem Verwerfen, der Server setzt keine Frist.
5. **Eine wartende Buchung zählt im Kontostand, in der Summe der Kassenbuch-Liste und in der Budget-Aktivität mit**,
   wie im Prototyp (Roman, 2026-11-11). Sie ist eine Buchung im Kassenbuch, bis Roman sie verwirft. Solange sie
   wartet, weicht der Kontostand des Kontos in BB bewusst von YNAB ab; das ist die einzige zugelassene Ausnahme von
   „Kontostand stimmt mit YNAB überein“, und die Kennzeichnung „doppelt?“ macht sie sichtbar. Sie zählt in „zu
   prüfen“ (Zu-tun-Eintrag 1, Zähler an der Übersicht) und nicht in „Buchung nicht in YNAB“ (Eintrag 5), weil kein
   Push gescheitert ist. Nach dem Verwerfen zählt sie nirgends mehr.
6. **Gesperrt, solange sie wartet:** jeder schreibende Kassenbuch-Befehl außer „Ist neu“ und „Verwerfen“ lehnt
   sie ab (Prüfvermerk, Kategorie, Split, Übertrag, Lena, Memo, Payee, Betrag, Löschen, Erneut senden und alle
   Erneut-senden-Befehle eines Zurückschreibens), mit dem Satz „Die Buchung ist vielleicht doppelt. Erst
   entscheiden: Ist neu oder verwerfen.“ Sammelbefehle überspringen sie und zählen sie mit. Eine Verknüpfung
   mit YNAB (`EntryLinkedToYnab`) auf einer wartenden Buchung ist eine Invariantenverletzung in `apply`.
7. **Wiederholter Abruf.** Die Bank liefert dieselbe Reference bei jedem Abruf im Zeitraum wieder. Vor der
   YNAB-Erkennung prüft der Abruf das Kassenbuch über die `import_id` (`BB:` plus Reference, unveränderlich):
   eine wartende oder verworfene Buchung wird übersprungen (kein Ereignis, kein Push), eine verknüpfte ist
   `AlreadyImported`, eine unverknüpfte mit offenem Push-Fehlschlag oder unbekanntem Ausgang wird erneut gesendet
   (wie heute, die feste `import_id` macht das sicher). Die Entscheidung ist eine reine Funktion über den
   gefundenen Eintrag.
8. **Ohne YNAB-Abgleich wird nichts gebucht.** Liefert das Lesen der YNAB-Buchungen nichts, endet der Abruf ohne
   Buchung mit einer Meldung, von Hand wie automatisch. Ohne Abgleich fehlt die Duplikatprüfung, und der Push
   ginge ohnehin nicht durch.
9. **Automatischer Abruf** (ersetzt ADR 0071 Punkt 7): Setting an/aus und Intervall 60 bis 1440 Minuten, Vorgabe
   an und 360 Minuten. Ein eigener Hintergrunddienst, registriert nur in `Program.fs` (ADR 0071 Punkt 8), ruft
   denselben Abruf wie „Abrufen“ und bucht damit direkt. Fällig ist er, wenn seit dem jüngsten Abruf in der
   Sync-Historie das Intervall vergangen ist. Er startet nie eine Challenge und nie einen Passwort-Login (ADR 0071
   Punkt 2, Invariante Challenge): ohne `Connected` überspringt er, stirbt die Verbindung im Abruf, endet er ohne
   Challenge.
10. **Sync-Historie.** Jeder Abruf, der die Bank erreicht, schreibt wie heute einen Eintrag in `sync_sessions`
    (Zustand, ADR 0083 Abschnitt 8), mit Zahlen (neu, per Regel, aus Bestellnummer, ohne Kategorie, sichere
    Duplikate, wartend, nicht in YNAB) und ob er automatisch lief. Die Zeile „Comdirect · Import <Stand>“ der
    Übersicht liest daraus den jüngsten gelungenen Abruf, egal wer ihn auslöste.

## Verhältnis zu ADR 0021 und 0071
- ADR 0021 bleibt: Kassenbuch zuerst, dann Push, Verknüpfung als Ereignis, kein Rollback. Neu ist ein Zustand
  „zurückgehalten“, in dem bewusst kein Push folgt, bis Roman entscheidet. Punkt 3 (Vorab-Prüfung über die
  `import_id`) wird um Punkt 7 dieses ADR geschärft: eine vorhandene, unverknüpfte Buchung wird nur erneut
  gesendet, wenn sie weder wartet noch verworfen ist.
- ADR 0071 Punkt 7 (Vorbereiten nach Zeitplan, bucht nichts, Vorgabe aus) ist ersetzt durch Punkt 9. Die Punkte
  2, 3, 4, 6 und 8 gelten weiter; Punkt 6 (der Server entscheidet über die TAN) gilt für „Abrufen“ von Hand, die
  Antwort heißt danach nicht mehr „Prüfung offen“, sondern trägt das Ergebnis des Abrufs.

## Consequences
- Was der Sync-Ablauf vor dem Buchen konnte, geht im Kassenbuch: Kategorie, Aufteilen samt Barabhebung, Lena,
  Memo (vorhanden), Payee (neu, `ledger-fxt09`), Regel aus der Buchung (mit `categorization-n3sxw`). Bis dahin
  fehlen Payee ändern und Regel aus der Buchung; das ist bewusst.
- Eine falsch kategorisierte Buchung steht kurz in YNAB, bis Roman sie durchgeht. Das ist gewollt: „zu prüfen“
  (Approved = false) zeigt das auch in YNAB.
- Die Weitergabe einer Kategorie an Buchungen derselben Bestellnummer innerhalb eines Imports entfällt; der
  Vorschlag aus der YNAB-Historie bleibt (Zeile in der Kann-Sammlung categorization).
- Der Weg „trotzdem importieren“ für von YNAB abgelehnte Buchungen (`forceImportDuplicates`) entfällt mit der
  Sitzung; eine solche Buchung steht als „Buchung nicht in YNAB“ mit Grund da.
- Die Erkennung bleibt gegen YNAB. Gegen das Kassenbuch wäre sie fachlich sauberer (BB führt), braucht aber einen
  Spiegel-Lauf vor der Erkennung; das ist eine spätere Entscheidung.

## Alternatives considered
- **Unsichere Duplikate wie heute mitschicken:** abgelehnt, YNAB hätte dann eine Doppelbuchung, die erst beim
  Prüfen auffällt.
- **Unsichere Duplikate in einer Warteliste außerhalb des Kassenbuchs (Zustand):** abgelehnt, es geht um Geld
  (ADR 0083), und Roman will sie im Kassenbuch sehen.
- **Feld in `EntryImportedFromComdirect` statt eigenem Ereignis:** abgelehnt, das Zurückhalten ist eine eigene
  Entscheidung mit eigener Zeile in der Historie, und das Payload bekäme eine neue Version.
- **Wartende Buchung im Kontostand nicht mitzählen:** abgelehnt (Roman). Der Kontostand stimmte dann mit YNAB
  überein, aber BB zeigte eine Buchung im Kassenbuch, die in keiner Summe steht. Roman nimmt die sichtbare Abweichung
  bis zur Entscheidung in Kauf.
- **Vorbereiten nach Zeitplan ohne Buchen (ADR 0071 Punkt 7):** abgelehnt, ohne Prüfsitzung gibt es nichts
  vorzubereiten.
