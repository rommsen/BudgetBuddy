---
id: 0050
title: Prüfvermerk-Schreibweg: ein Ereignis mit Alt und Neu, Reconciled bleibt unberührt, Fehlschlag als eigener Zustand
scope: ledger
status: accepted
date: 2026-10-19
supersedes: []
superseded_by: []
related_tasks: [ledger-r777c, ledger-th36h, ledger-rjb8w, ledger-nhwrt]
related_adrs: [0013, 0015, 0021, 0028, 0032]
related_research: []
---

# ADR 0050: Prüfvermerk-Schreibweg

## Context
ADR 0028 macht den Prüfvermerk zur einzigen schreibenden Fähigkeit vor v2, ADR 0032 legt
fest, dass "geprüft" die Felder `approved` und `cleared` meint. Beim Verfeinern von
`ledger-r777c` am 2026-10-19 blieben vier Fragen offen: was "Zurücknehmen" bedeutet, wie
mit `Reconciled` umzugehen ist, wie das Ereignis aussieht und wie ein gescheiterter
Rückschreibe-Versuch im Log und in der Lese-Sicht erscheint.

Der Code liefert dafür diese Ausgangslage: `YnabClient.fs` kennt nur POST-Schreibwege
(Vorlage `createManualTransaction`). Das Ereignis-Log kennt Beobachtungs-Ereignisse
(`EntryApprovalChangeObservedInYnab`, `EntryClearedStateChangeObservedInYnab`) und
BB-Ereignisse mit Actor `UserInBb`, aber kein Ereignis für einen Prüfvermerk aus BB.
"Noch nicht in YNAB" bedeutet heute das Fehlen von `EntryLinkedToYnab` und passt nicht auf
eine Buchung, die schon verknüpft ist. `LedgerEntryView` zeigt nur `NeedsReview`, nicht die
rohen Felder.

## Decision
**Entscheidungen von Roman (2026-10-19):**

1. **Zurücknehmen stellt den exakten Vorzustand her (Variante C).** "Rückgängig" im Toast
   setzt `approved` und `cleared` auf die Werte vor der Geste. Ein späteres bewusstes
   "Ungeprüft" setzt `approved = false` und `cleared = Uncleared`.
2. **`Reconciled` fasst BB nie an.** "Geprüft" auf einer Reconciled-Buchung setzt nur
   `approved` und lässt `cleared = Reconciled`. "Ungeprüft" setzt nur `approved = false`.
   Auch Rückgängig schreibt nie von oder nach Reconciled.

**Entwurfsentscheidungen:**

3. **Ein Ereignis für jede Änderung des Prüfvermerks:** `EntryReviewMarkChangedInBb`
   mit `OldApproved`, `NewApproved`, `OldCleared`, `NewCleared`, Actor `UserInBb`.
   "Geprüft", "Ungeprüft" und "Rückgängig" erzeugen denselben Ereignistyp. Die Absicht
   der Geste steht nicht im Log, nur der Übergang. Die Historie erzählt ihn aus Alt und
   Neu ("Als geprüft markiert", "Prüfvermerk entfernt", "Prüfvermerk wiederhergestellt"
   ergibt sich aus den Werten). Version 1.
4. **Idempotenz am Befehl.** Ändert die Anfrage nichts am Zustand (Ziel gleich Ist), gibt
   es weder Ereignis noch PATCH. Die Antwort meldet `NoChange`.
5. **Reihenfolge wie ADR 0021, Punkt 5:** erst `EntryReviewMarkChangedInBb`
   (`appendAndApply`), dann der PATCH. Ohne zweite Ereignisfolge bei Erfolg: ein
   erfolgreicher Push erzeugt kein weiteres Ereignis. Der nächste Spiegel-Lauf liefert
   den Stand zurück, `observe` findet keinen Unterschied und schweigt (ADR 0015). Hat
   YNAB inzwischen etwas anderes, kommt die Beobachtung und der jüngere Stand gewinnt
   (ADR 0028, Punkt 3).
6. **Der PATCH trägt nur geänderte Felder.** `approved` immer, `cleared` nur wenn
   `NewCleared <> OldCleared` (beim erneuten Senden: wenn der Ist-Stand nicht Reconciled
   ist). `cleared` geht nie mit dem Wert `reconciled` hinaus. Der Weg hat einen eigenen
   Namen und eine eigene Signatur, die nur diese beiden Felder kennt (ADR 0028, Punkt 1),
   keine allgemeine Update-Funktion.
7. **Fehlschlag als Zustand der Buchung.** Scheitert der PATCH (Netz, Timeout, 4xx, 429)
   oder ist die Buchung noch nicht mit YNAB verknüpft, folgt das Ereignis
   `EntryReviewMarkWriteBackFailed { Reason }` (Actor `UserInBb`, Version 1). `Entry`
   bekommt `ReviewMarkWriteBackFailure: string option` (der Grund). Kein Rollback, der
   Prüfvermerk bleibt in BB gesetzt (ADR 0021, Punkt 2).
8. **Der Zustand endet durch ein Ereignis**, `EntryReviewMarkWrittenBackToYnab`
   (Version 1, kein Payload). Es entsteht in zwei Fällen: der manuelle Befehl
   "Erneut senden" hat Erfolg (Actor `UserInBb`), oder ein Spiegel-Lauf sieht für eine
   Buchung mit offenem Fehlschlag, dass YNAB `approved` und `cleared` schon auf dem
   BB-Stand hat (Actor `YnabMirror`, deckt den Fall "PATCH kam an, Antwort ging
   verloren"). Liefert YNAB einen anderen Stand, löst das jeweilige Beobachtungs-Ereignis
   (`EntryApprovalChangeObservedInYnab` oder `EntryClearedStateChangeObservedInYnab`)
   den Fehlschlag ebenfalls auf: der Stand von YNAB gilt dann (jüngerer Stand gewinnt).
   Ein erneuter Fehlschlag überschreibt den Grund. Eine neue Geste auf der Buchung
   (Ereignis nach Punkt 3) setzt den Zustand zurück und versucht den Weg neu.
9. **Wiederholen ist manuell.** `retryReviewMarkWriteBack` (Taste "Erneut senden" am
   Hinweis) sendet den aktuellen Stand der Buchung. Kein automatisches Wiederholen in
   dieser Task. Ohne offenen Fehlschlag tut der Befehl nichts.
10. **Die Antwort des Befehls trägt den Vorzustand.** Der Client braucht für Rückgängig
    die Werte vor der Geste. Sie liegen in der Antwort (`ReviewMarkOutcome` mit
    `Previous: { Approved; Cleared }`, `Result: Changed | NoChange`, `WriteBack: Sent |
    Failed of reason`), nicht als zusätzliche Felder in `LedgerEntryView`. Rückgängig ist
    der Befehl `restoreReviewMark` mit diesem Vorzustand; der Server ignoriert dabei jede
    Reconciled-Angabe und fasst Reconciled nicht an (Punkt 2).
11. **`LedgerEntryView` bekommt ein Feld** `ReviewMarkWriteBackFailure: string option`.
    Die Zeile zeigt damit den Hinweis "Prüfvermerk nicht in YNAB" mit Grund. Der Chip
    "Zu prüfen" zählt weiter nach dem BB-Stand, der Fehlschlag ändert die Zählung nicht.
12. **Ausnahmen am Befehl:** in YNAB gelöschte Buchungen werden abgelehnt (Fehler,
    kein Ereignis). Eine Buchung ohne YNAB-Verknüpfung bekommt das Ereignis nach Punkt 3
    und sofort `EntryReviewMarkWriteBackFailed` mit dem Grund "noch nicht in YNAB
    verknüpft". Nach dem Verknüpfen genügt "Erneut senden".

## Consequences
**Gut:**
- Ein Ereignistyp für Setzen, Entfernen und Rückgängig hält den Katalog klein. Replay und
  Historie brauchen keine Unterscheidung der Absicht.
- Reconciled bleibt, was der Abstimmungsvorgang in YNAB daraus gemacht hat.
- Ein verlorener Push ist sichtbar, wiederholbar und löst sich von selbst, sobald YNAB den
  BB-Stand zeigt.
- Der Schreibweg ist auf zwei Felder zugeschnitten und ohne Umbau für Kategorie, Split
  oder Lena-Zuweisung gedacht (v2).

**Schlecht oder offen:**
- Drei neue Ereignistypen (Changed, WriteBackFailed, WrittenBackToYnab) mit
  Namens-Abbildung, Schema-Version, Decoder und `apply`-Fall.
- `observe` bekommt einen Sonderfall (Fehlschlag offen, YNAB gleich BB).
- Rückgängig auf einer Buchung, die YNAB zwischenzeitlich geändert hat, überschreibt
  diese Änderung mit dem Vorzustand. Das ist bei diesem Feld gewollt (ADR 0028, Punkt 3).
- Ob YNAB `cleared` auf einer dort inzwischen reconcilierten Buchung ablehnt, ist offen.
  Der Fehlschlag mit Grund fängt das auf, eine Sonderbehandlung gibt es nicht.

## Alternatives considered
**Zwei Ereignisse (Approved und Cleared getrennt).** Näher an den Beobachtungs-Ereignissen,
aber eine Geste erzeugt zwei Historie-Zeilen und der Rückgängig-Zustand zerfällt. Verworfen.

**Eigene Ereignistypen für Geprüft, Ungeprüft und Rückgängig.** Die Historie liest sich
direkter, der Katalog wächst um zwei Typen ohne neue Information. Verworfen.

**PATCH zuerst, Ereignis danach.** Bei Timeout weiß BB nicht, ob YNAB geschrieben hat, und
der Spiegel würde die Änderung als Beobachtung ohne BB-Herkunft anlegen. Verstößt gegen
ADR 0021, Punkt 5.

**Fehlschlag nur im Lesepfad ableiten** (Vergleich BB-Stand mit letztem Spiegel-Stand).
Kein neues Ereignis, aber der Grund fehlt und der Zustand ist nicht aus dem Log faltbar
(ADR 0013).

**Variante A/B für Rückgängig** ("Ungeprüft" als Gegenstück oder `cleared` bleibt
gesetzt). Roman hat Variante C gewählt, weil Rückgängig den Vorzustand meint.

**Reconciled bei "Geprüft" auf Cleared herabsetzen oder bei "Ungeprüft" auf Uncleared.**
Würde Abstimmungs-Arbeit in YNAB entwerten. Von Roman abgelehnt.

**Automatisches Wiederholen.** Braucht Hintergrund-Zustand und Rate-Limit-Logik, die
dieses Feld nicht rechtfertigt. Für später möglich, ohne dieses Format zu ändern.
