---
id: 0055
title: Kategorie-Schreibweg: Recategorized-Ereignis, Fehlschlag mit Baseline, YNAB gewinnt, Available live
scope: ledger
status: accepted
date: 2026-10-24
supersedes: []
superseded_by: []
related_tasks: [ledger-k2rbn, ledger-kwwxz, ledger-w4vte]
related_adrs: [0014, 0021, 0028, 0044, 0050, 0051, 0053, 0054]
related_research: []
---

# ADR 0055: Kategorie-Schreibweg

## Context
ADR 0054 zieht Kategorie ändern vor. Der Schreibweg folgt dem Muster von ADR 0050
(Prüfvermerk). Die Prüfung am Code ergab:

- `EntryRecategorized` (Actor `UserInBb`) gibt es schon: Alt und Neu für Kategorie, Splits
  und Payee, `apply` prüft "Kategorie XOR Splits". Bisher erzeugt nur der Comdirect-Import
  (Review-Spur) es; ein Schreibweg nach YNAB hängt nicht daran.
- `YnabClient` kennt `sendReviewMark` (PUT einer Buchung) und `sendReviewMarkBatch` (PATCH
  vieler), beide auf `approved` und `cleared` zugeschnitten.
- `Entry` und `LedgerEntryView` tragen `ReviewMarkWriteBackFailure`; `observe` beendet den
  Fehlschlag, wenn YNAB den BB-Stand zeigt.
- Das Kassenbuch lädt seine Kategorien schon live über `getCategories`
  (`Model.Categories`, `YnabCategory.Available` ist YNABs `balance`). Der Spiegel-Lauf ruft
  `/categories` ebenfalls auf und verwirft `balance`.
- `LocallyRemoved` und die Seitenformel (ADR 0051, Punkt 7) tragen eine Zeile, die die
  Treffermenge verlässt, aber keine Zeile, die ihren Sortierplatz wechselt.

Entschieden von Roman am 2026-10-24: YNAB gewinnt bei einem Konflikt, kein Regel-Angebot
in dieser Scheibe, Split-Bearbeitung wird mitgebaut, Überträge ohne Kategorie bleiben
gesperrt (Details unten).

## Decision
1. **Ein Änderungs-Ereignis: `EntryRecategorized`.** Kategorie und Split ändern in BB
   erzeugen es mit Actor `UserInBb`, `OldPayee = NewPayee`. Kein neuer Änderungstyp, keine
   Schema-Änderung. Der Satz der Historie ist `describeRecategorized` ("In BB
   umkategorisiert: Kategorie geändert: A → B"); er zeigt die Herkunft "In BB". Ändert die
   Anfrage nichts (Ziel gleich Ist nach `categorizationIdentity`), gibt es kein Ereignis und
   keinen Push (Antwort `NoChange`).
2. **Reihenfolge wie ADR 0021 und 0050:** erst das Ereignis (`appendAndApply`), dann der
   Push. Ein erfolgreicher Push erzeugt kein weiteres Ereignis, der nächste Spiegel-Lauf
   liefert den Stand zurück, `observe` schweigt.
3. **Eigene Push-Funktionen.** `YnabClient.updateCategory` (PUT einer Buchung, Body nur
   `category_id`, `null` für "ohne Kategorie") und `updateCategoryBatch` (PATCH, je Buchung
   `id` und `category_id`). Die Antwort des Sammel-PATCH nennt gespeicherte Ids; nicht
   genannte werden wie beim Prüfvermerk als Fehlschlag der einzelnen Buchung geführt. Für
   Splits gibt es eine eigene Funktion in der Split-Aufgabe, keine gemeinsame
   Update-Funktion (ADR 0028, Punkt 1).
4. **Fehlschlag als Zustand mit Baseline.** Neue Ereignisse
   `EntryCategorizationWriteBackFailed { Reason; BaselineCategory; BaselineSplits }` und
   `EntryCategorizationWrittenBackToYnab` (Version 1, kein Payload). `Entry` bekommt
   `CategorizationWriteBackFailure: { Reason; BaselineCategory; BaselineSplits } option`,
   `LedgerEntryView` das Feld `CategorizationWriteBackFailure: string option`. Die
   Baseline ist der Stand, den YNAB vermutlich noch hat (die `Old*`-Werte der ersten
   gescheiterten Änderung; bei weiteren Änderungen mit offenem Fehlschlag bleibt die
   erste Baseline). Kein Rollback, die Kategorie bleibt in BB gesetzt. Ohne
   YNAB-Verknüpfung folgt sofort der Fehlschlag mit dem Grund "noch nicht in YNAB
   verknüpft" (wie ADR 0050, Punkt 12). "Erneut senden" sendet den aktuellen BB-Stand;
   Erfolg schreibt `EntryCategorizationWrittenBackToYnab`.
5. **YNAB gewinnt, mit einer Ausnahme für den Fehlschlag.** Ohne offenen Fehlschlag
   übernimmt `observe` jeden YNAB-Stand als `EntryCategorizationChangeObservedInYnab`
   (Historie: "In BB geändert", danach "In YNAB geändert"), auch wenn er von BB abweicht.
   Bei offenem Fehlschlag gilt für den beobachteten Stand: gleich der Baseline heißt, YNAB
   hat unseren Push nie gesehen, kein Ereignis, Fehlschlag bleibt; gleich dem BB-Stand
   heißt `EntryCategorizationWrittenBackToYnab` (Actor `YnabMirror`); jeder andere Stand
   ist eine echte Änderung in YNAB, das Beobachtungs-Ereignis gilt und beendet den
   Fehlschlag. Ohne diese Ausnahme würde der nächste Lauf eine gescheiterte Änderung still
   zurückdrehen, bevor Roman "Erneut senden" drücken kann. Der Vergleich nutzt
   `categorizationIdentity` (ADR 0044), eine Umbenennung in YNAB zählt nicht.
6. **Schreib-Sperre gegen den Spiegel-Lauf.** Befehl (Ereignis und Push) und Spiegel-Lauf
   (Abruf und `observe`) laufen nicht ineinander: ein gemeinsamer Wächter, den
   `MirrorGuard` und die Kategorie-Befehle teilen. Sonst kann ein Lauf, der zwischen
   Ereignis und PUT abruft, den alten YNAB-Stand als Änderung beobachten und die
   BB-Kategorie zurückdrehen, bis der nächste Lauf sie wieder umkehrt (zwei falsche
   Historie-Zeilen). Der Befehl wartet höchstens auf einen laufenden Lauf.
7. **Wo die Kategorie nicht änderbar ist.** Der Server lehnt ab (Fehler, kein Ereignis):
   in YNAB gelöschte Buchungen; Überträge ohne Kategorie und ohne Split-Zeilen
   (`TransferAccountId` gesetzt), das ist ein Übertrag zwischen Budget-Konten, bei dem YNAB
   keine Kategorie zulässt; Split-Buchungen im Kategorie-Picker (der Pfeil öffnet dort den
   Split-Editor, Sammelaktionen überspringen sie). Ein Übertrag in ein Tracking-Konto mit
   Kategorie bleibt änderbar. `LedgerAccount` führt kein `OnBudget`; die Regel nutzt, was
   die Buchung selbst trägt. Lehnt YNAB trotzdem ab, entsteht der Fehlschlag mit Grund.
8. **Available kommt live, nicht aus dem Spiegel.** `Model.Categories` trägt `Available`
   (YNABs `balance` des laufenden Monats) bereits. Eine zweite, persistierte Kopie in
   `ledger_categories` wäre älter als der Live-Abruf und brauchte eine eigene Abfrage. Die
   Frische sichert der Client: `getCategories` neu laden beim Seitenaufruf, beim Wechsel der
   PWA in den Vordergrund und beim Öffnen des Pickers, wenn der Stand älter als zwei
   Minuten ist (ein Aufruf je Auslöser; das Limit von rund 200 Anfragen je Stunde bleibt
   unberührt). Nach einer erfolgreichen Änderung schreibt der Client Available der beiden
   Kategorien lokal fort (alte Kategorie minus Betrag, neue plus Betrag, nur für Buchungen
   des laufenden Monats); der nächste Abruf überschreibt. Kein zusätzlicher Spiegel-Lauf
   nach dem Schreiben.
9. **Sortierung und Filter (ADR 0051, Punkt 7).** Die geänderte Zeile bleibt an ihrem Platz
   ("Kategoriewechsel sortiert nie um"). Ändert die Änderung den Schlüssel der aktiven
   Sortierung (`SortByCategory`) oder lässt sie die Zeile aus dem aktiven Filter fallen
   (Kategorie-Filter, Chip "Ohne Kategorie"), setzt der Client `OrderStale`. Dann zeigt die
   Liste eine Hinweisleiste "Reihenfolge oder Treffer haben sich geändert. Neu laden", das
   Nachladen weiterer Seiten ruht, und Treffer und Summe werden über die Zählwege neu
   geladen. "Neu laden" setzt die Liste ab Seite 1 mit der aktiven Sortierung auf
   (`LocallyRemoved = 0`). `LocallyRemoved` bleibt unverändert.
10. **Kein Regel-Angebot** in dieser Scheibe. Es ist ein eigener Backlog-Entwurf.

## Consequences
**Gut:**
- Kein neuer Änderungs-Ereignistyp; Replay, Codec und Historie bleiben, wie sie sind.
- Eine gescheiterte Änderung geht nicht still verloren, und eine in YNAB geänderte
  Kategorie wird nicht von BB zurückgedreht.
- Die Zahlen im Picker sind so frisch wie der letzte Abruf und kosten keinen Schema-Umbau.

**Schlecht oder offen:**
- Zwei neue Ereignistypen mit Namens-Abbildung, Decoder und `apply`-Fall, dazu ein
  Sonderfall in `observe` (Baseline).
- Die Schreib-Sperre macht einen Befehl so langsam wie ein laufender Spiegel-Lauf, im
  ungünstigen Fall einige Sekunden.
- Die Fortschreibung von Available gilt nur für Buchungen des laufenden Monats. Für ältere
  Buchungen zeigt der Hinweis keine Zahlen.
- Die Regel für Überträge ist eine Heuristik, solange das Kassenbuch `on_budget` nicht führt.
- Ob YNAB die Splits einer bestehenden Buchung per API ändern lässt, ist offen und wird in
  einem Spike geklärt; davon hängt die Split-Bearbeitung ab.

## Alternatives considered
**Eigenes Ereignis `EntryCategoryChangedInBb`.** Gleiche Information wie `EntryRecategorized`,
nur ohne Payee. Verworfen: zweiter Typ für dasselbe Ereignis, und der Kategorie-Fall kennt
Splits ohnehin.

**Fehlschlag ohne Baseline (wie ADR 0050).** Beim Prüfvermerk gewinnt der jüngere Stand,
ohne dass etwas verloren geht. Bei der Kategorie würde ein Lauf vor dem erneuten Senden die
Änderung zurückdrehen. Verworfen.

**Alle geladenen Seiten neu laden nach der Änderung.** Jede Seite der Kategorie-Sortierung
lädt und sortiert die ganze Treffermenge (rund 14.000 Zeilen, ADR 0051 Punkt 4); bei
mehreren Seiten sind das mehrere Läufe, und die Zeile springt unter Romans Fingern weg.
Verworfen zugunsten der Hinweisleiste mit Neuladen ab Seite 1.

**Available im Spiegel persistieren.** Roman hatte das als Richtung genannt. Verworfen, weil
`Model.Categories` die Zahl schon live trägt und eine persistierte Kopie nur älter wäre.
Bleibt als additive Option, falls das Kassenbuch ohne YNAB-Verbindung Zahlen zeigen soll.

**Zusätzlicher Spiegel-Lauf nach jedem Schreiben.** Bei vielen Änderungen hintereinander
würde das Limit von rund 200 Anfragen je Stunde schnell erreicht. Verworfen zugunsten der
lokalen Fortschreibung.
