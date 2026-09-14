---
id: 0032
title: Geprüft heißt approved und cleared; Flags werden roh beobachtet, "Lena" am Flag-Namen erkannt
scope: ledger
status: accepted
date: 2026-09-14
supersedes: []
superseded_by: []
related_tasks: [ledger-kdnnz, ledger-r777c]
related_adrs: [0013, 0014, 0028]
related_research: []
---

# ADR 0032: Geprüft heißt approved und cleared; Flags werden roh beobachtet

## Context
ADR 0028 hat den Prüfvermerk zur einzigen schreibenden Fähigkeit vor v2 erklärt. Offen
blieb, welches YNAB-Feld "geprüft" ist. Die ADR ging von **einem** Feld aus (vermutlich
`approved`) und nahm an, `cleared` trage für Roman keine Information.

Romans YNAB-Referenzweg (Ablauf 1 in `docs/ux/kassenbuch/briefs.md`) zeigt etwas anderes:
er wischt je Buchung zweimal, einmal für `approved`, einmal für `cleared`. Beide Gesten
gehören zu "habe ich mir angesehen". Beim Verfeinern von `ledger-kdnnz` am 2026-09-14 hat
Roman festgelegt, dass eine Buchung erst geprüft ist, wenn beides gesetzt ist.

Der Spiegel beobachtet heute weder `approved` noch Flags. YNAB liefert laut OpenAPI
`approved` (bool, nie null), `flag_color` (red, orange, yellow, green, blue, purple oder
null) und `flag_name` (string oder null). Roman hat ein Flag "Lena" genannt: Lena soll sich
die Buchung ansehen. Andere Flags nutzt er selten zum Gruppieren.

Im Kassenbuch liegen rund 14.000 gespiegelte Buchungen. Der Delta-Abruf über
`server_knowledge` (ADR 0014) liefert sie erst wieder, wenn sie sich in YNAB ändern.

## Decision
1. **Geprüft = `approved` und (`cleared` ist Cleared oder Reconciled).** Der Chip
   "Zu prüfen (n)" zählt jede nicht gelöschte Buchung mit `approved = false` oder
   `cleared = Uncleared`. Das korrigiert die Annahme aus ADR 0028. Die Ausnahme bleibt auf
   den Prüfvermerk beschränkt, er umfasst jetzt zwei Felder. `ledger-r777c` schreibt beide
   zurück. Die Konfliktregel aus ADR 0028 (der jüngere Stand gewinnt) gilt für beide.
2. **Zwei neue Beobachtungs-Ereignisse**, analog zu `EntryClearedStateChangeObservedInYnab`:
   `EntryApprovalChangeObservedInYnab` (alt/neu) und `EntryFlagChangeObservedInYnab`
   (Farbe und Name, alt/neu). Beide erscheinen als sprechende Zeilen in der Historie.
3. **Kein Nachholen für den Bestand.** Ereignisse, die ohne die neuen Felder gespeichert
   sind, gelten beim Lesen als `approved = true` und ohne Flag. Eine Bestandsbuchung
   bekommt ihren echten Stand, sobald YNAB sie wieder im Delta liefert.
4. **Schema-Version 2 für die drei Entstehungs-Ereignisse**, die einen Prüfstand brauchen
   (ADR 0013, Punkt 7): `EntryObservedInYnab` trägt `approved`, `flagColor`, `flagName`;
   `EntryImportedFromComdirect` und `CashExpenseAdded` tragen `approved` (beim Schreiben
   `false`, weil der Push kein `approved` sendet und YNAB die Buchung unbestätigt anlegt).
   Version 1 bleibt unverändert lesbar und liefert die Vorgaben aus Punkt 3.
5. **Flags roh im Log, gedeutet im Lesepfad.** Ereignis und Zustand halten Farbe und Namen,
   wie YNAB sie liefert. Eine reine Funktion macht daraus *Rückfrage Lena* (Name nach
   Abschneiden der Leerzeichen gleich "Lena", ohne Beachtung der Groß- und
   Kleinschreibung), *Markiert* (jedes andere gesetzte Flag) oder *kein Flag*.

## Consequences
**Gut:**
- Der Chip bildet Romans heutigen Prüfweg ab: nach einem Durchgang ist eine Buchung in
  beiden Feldern erledigt.
- Frisch gepushte Buchungen erzeugen beim ersten Spiegel-Lauf kein Prüf-Ereignis, weil ihr
  Anfangsstand dem entspricht, was YNAB zeigt.
- Die Deutung von Flags lässt sich ändern (zweite Person, andere Namen), ohne ein
  gespeichertes Ereignis anzufassen.

**Schlecht oder offen:**
- Der Katalog wächst um zwei Ereignisse und drei Schema-Versionen 2.
- Bestandsbuchungen, die in YNAB ungeprüft sind und sich nicht mehr ändern, erscheinen
  nicht unter "Zu prüfen". Roman hat das in Kauf genommen; ein Vollabruf bleibt jederzeit
  als eigene Task möglich.
- Der Rückschreibe-Weg aus ADR 0028 wird für zwei Felder gebaut statt für eines.

## Alternatives considered
**Nur `approved` als Prüfvermerk** (ADR 0028 wörtlich). Einfacher, aber nach dem Wisch in
BB blieben Buchungen in YNAB uncleared, und Roman müsste für den zweiten Wisch weiter die
YNAB-App öffnen.

**Einmaliger Vollabruf für den Bestand.** Auditierbar, erzeugt aber tausende
Historie-Zeilen ohne Aussage und einen großen API-Abruf. Von Roman abgelehnt.

**Stand für den Bestand still in die Zustandstabelle setzen.** Ruhig, verletzt aber die
Regel, dass der Zustand aus dem Log faltbar ist (ADR 0013).

**`approved = false` für BB-eigene Buchungen fest in `apply`.** Ohne Payload-Feld, aber
alle schon geschriebenen Comdirect- und Quick-Add-Buchungen fielen beim Replay auf
ungeprüft und stünden dauerhaft im Chip.

**Flag nach Farbe deuten.** Bricht, sobald Roman die Farbe des Lena-Flags ändert.
