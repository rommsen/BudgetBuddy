---
id: 0039
title: Kategoriegruppe wird im Lesepfad aufgelöst; geteilte Bausteine im Ereignis-Log wachsen nur um ehrlich optionale Felder
scope: ledger
status: accepted
date: 2026-09-20
supersedes: []
superseded_by: []
related_tasks: [ledger-h6ptn, ledger-p3wnq]
related_adrs: [0013, 0014, 0018, 0032]
related_research: []
---

# ADR 0039: Kategoriegruppe im Lesepfad, nicht im Ereignis-Log

## Context
Die Kassenbuch-Liste soll die Kategorie am PC voll qualifiziert zeigen ("Alltag:
Lebensmittel" statt "Lebensmittel", Entwurf
`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`, 1.9 Zeile 260; ebenso das
Detail-Panel in 1.5 Zeile 171). Heute trägt `CategoryRef` (`src/Server/Ledger/Events.fs`)
nur `CategoryId` und `CategoryName`.

`CategoryRef` ist kein Ereignis, sondern ein geteilter Baustein der Payloads. Er steckt in
sechs Payload-Feldern über fünf Ereignistypen (`EntryObservedInYnab`,
`EntryCategorizationChangeObservedInYnab`, `EntryCategorizedByRule`, `EntryRecategorized`,
`CashExpenseAdded`). Ein Feld in diesem Baustein nach dem Muster aus ADR 0032 Punkt 4
(Schema-Version hochzählen, eigener Alt-Decoder je Ereignistyp) kostet fünf neue Decoder,
brächte `EntryObservedInYnab` auf drei Versionen und `decodePayload` auf über 18 Zweige,
und diese Rechnung wiederholt sich bei jedem weiteren Baustein-Feld.

ADR 0013 Punkt 7 verlangt, dass ein gespeicherter Ereignis-Typ nie seine Bedeutung ändert
und alte Ereignisse unangetastet lesbar bleiben. Der Punkt spricht von Ereignis-Typen und
ihrer Payload-Form; ob ein *hinzugefügtes optionales* Feld schon eine andere Payload-Form
ist, und wie ein Baustein unterhalb der Ereignis-Ebene zu behandeln ist, steht dort nicht.
Diese Lücke ist der eine Gegenstand dieses ADRs.

Der zweite Gegenstand: YNABs Transaktions-Objekt trägt die Gruppe gar nicht
(`src/Server/YnabObservations.fs:170-194` liest `category_id` und `category_name`, mehr
gibt es dort nicht). Die Gruppe steht nur an `YnabCategory.GroupName` aus `/categories`
bzw. den `category_groups` in `/budgets/{id}`. Der Spiegel-Lauf (`MirrorFetchers`,
`src/Server/Ledger/CommandHandlers.fs:31-38`) kennt heute zwei Abrufe, Konten und
Transaktionen.

Im Kassenbuch liegen rund 14.000 gespiegelte Buchungen. Ereignisse, die ohne ein neues
Feld gespeichert wurden, bekommen es nie nachträglich; eine Bestandsbuchung erhielte eine
Gruppe erst, wenn YNAB sie wieder im Delta liefert (ADR 0032 Punkt 3).

## Decision

1. **`CategoryRef` bleibt unverändert, die Gruppe kommt nicht ins Ereignis.** Die Gruppe
   ist Stammdatum der Kategorie, nicht Zustand der Buchung: verschiebt Roman eine Kategorie
   in YNAB in eine andere Gruppe, passiert an der Buchung nichts. Sie in ein
   Beobachtungs-Ereignis zu schreiben hieße außerdem, zwei API-Antworten zu joinen und das
   Ergebnis als Beobachtung abzulegen; ADR 0013 Punkt 4 verlangt das Gegenteil (der Spiegel
   macht aus dem *gelieferten* Snapshot ein Delta-Ereignis). Die Linie ist dieselbe wie in
   ADR 0032 Punkt 5: roh im Log, gedeutet im Lesepfad.

2. **Alte Buchungen zeigen die heutige Gruppe, nicht die von damals.** Entscheidung Romans
   vom 2026-09-20, ausführlich begründet unter Consequences ("Die tragende Festlegung"):
   eine Umbenennung oder Verschiebung ist eine Korrektur der Einordnung, kein neuer
   Sachverhalt. Auditierbarkeit nach ADR 0013 bleibt gewahrt, weil sie sich auf die
   Kategorie-**Zuweisung** bezieht, nicht auf deren Beschriftung: die Zuweisung liegt als
   `CategoryId` plus beobachteter Blattname unveränderlich im Log, und die Historie zeigt
   sie unverändert (Punkt 5). Die Liste ist eine Arbeitsfläche und zeigt, wie die Welt
   heute aussieht.

3. **Kategorie-Stammdaten werden gespiegelt wie die Konten.** Der Spiegel-Lauf bekommt
   einen dritten Abruf `FetchCategories` und schreibt Id, Name und Gruppenname per Upsert
   in eine eigene Tabelle `ledger_categories`, nach dem Muster von `ledger_accounts`. Kein
   `json_extract`, keine SQLite-Spezialitäten (ADR 0013 Punkt 8), ein normales
   `CREATE TABLE IF NOT EXISTS` im bestehenden Schema-Init. **Voller Abruf je Lauf, kein
   Delta** (wie ADR 0018 es für die Konten festgelegt hat; `getCategories` nimmt ohnehin
   kein `last_knowledge_of_server`), und **nur Upsert, nie Löschen**: eine in YNAB
   gelöschte Kategorie behält ihre Zeile, damit alte Buchungen ihre Beschriftung nicht
   rückwirkend verlieren. Die Tabelle ist damit die Sammlung aller je gesehenen
   Kategorien, kein exaktes Abbild des heutigen Katalogs; für den Zweck (Beschriftung
   nachschlagen) ist genau das richtig.

4. **Der Kategorie-Abruf ist bestes Bemühen und steht am Ende des Laufs.** Er läuft nach
   der Beobachtungsschleife und nach `saveServerKnowledge`. Schlägt er fehl, bleibt der
   Lauf `Ok`, die Tabelle behält ihren letzten Stand, und die betroffenen Zeilen zeigen
   weiter nur den Blattnamen. Unterschied zu den Konten (ADR 0018): Konten braucht die
   Schreibseite vollständig, sonst bricht der Lauf ab; Kategorien beschriften nur. Die
   Stellung am Ende sorgt dafür, dass ein 429 aus diesem Abruf nicht einen Lauf kippt, der
   seine eigentliche Arbeit schon getan hat.

5. **Der Lesepfad qualifiziert, die Anzeige setzt zusammen.** `ReadModel.toEntryView`
   bekommt ein Nachschlagen `CategoryId -> Gruppenname option`; `LedgerEntryView` trägt die
   Gruppe als eigenes Feld neben `CategoryName`. Zusammengesetzt wird erst in der Anzeige
   (`Components/Ledger/View.fs`), damit der Vertrag die beiden Teile getrennt lässt.
   Unbekannte oder in YNAB gelöschte Kategorien ergeben keine Gruppe und damit den heutigen
   Text, nie einen erfundenen Gruppennamen und nie ein führendes ": ". Die Sätze der
   Historie (`describeObserved` und Geschwister, `QueryHandlers.fs:132-232`) bleiben beim
   Blattnamen: sie schlagen nichts nach und müssen rein bleiben. Derselbe Grundsatz steht
   im ledger-README bereits beim Katalog-Eintrag zu
   `EntryTransferCounterpartObservedInYnab` (Payload ohne Kontonamen der Gegenbuchung).

6. **Rate-Limit-Rechnung fortgeschrieben.** Ein Lauf besteht ab jetzt aus drei Requests
   (Konten voll, Transaktions-Delta, Kategorien voll). Worst Case 24 Läufe pro Stunde ergibt
   72 Requests, Limit rund 200 pro Stunde und Token (ADR 0014, fortgeschrieben in ADR
   0018). Wird der Abstand je eng, ist die erste Stellschraube ein Frische-Kriterium für den
   Kategorie-Abruf (höchstens einmal pro Stunde), ohne dass sich sonst etwas ändert.

7. **Regel für neue Felder im Ereignis-Log** (füllt die Lücke in ADR 0013 Punkt 7):
   - Kann der Zieltyp "steht nicht drin" darstellen und ist das Fehlen genau das, dann
     optionales Feld ohne Versionssprung. Ein `string option`, das beim Fehlen `None` wird,
     behauptet nichts Neues über das alte Ereignis; dessen Bedeutung bleibt unverändert,
     nur der Typ wurde weiter. Das ist mit ADR 0013 Punkt 7 vereinbar.
   - Erzwingt das Lesen ohne das Feld eine **Behauptung**, dann Versionssprung mit eigenem
     Alt-Decoder und im ADR festgeschriebenem Vorgabewert (Muster ADR 0032 Punkt 4:
     `Approved: bool` kennt kein "unbekannt", `true` wäre eine Aussage über eine Buchung,
     die nie genehmigt wurde).
   - Muss "vor Einführung des Feldes geschrieben" von "danach geschrieben, Feld legitim
     leer" unterscheidbar bleiben, dann Versionssprung, auch wenn der erste Punkt greift.
     Die Schema-Version ist der einzige Marker dafür.
   - Ändert sich die Bedeutung eines bestehenden Feldes (Einheit, Wertebereich, Semantik),
     immer neue Version oder neuer Ereignis-Typ.
   - **Ein geteilter Baustein darf nur um Felder der ersten Art wachsen.** Braucht er ein
     Feld der zweiten Art, gehört das Feld zum Ereignis, nicht zum Baustein, oder der
     Baustein ist zu teilen. Sonst multipliziert sich der Versionssprung über alle
     Ereignistypen, die den Baustein benutzen, und das bei jedem weiteren Feld erneut.

8. **Kein Umschreiben gespeicherter Payloads.** Ein Backfill über den Bestand wäre ein
   Umschreiben des Logs und ist durch ADR 0013 ausgeschlossen.

## Consequences

### Die tragende Festlegung (Roman, 2026-09-20)
Der Einwand, der gegen diese Entscheidung am ehesten vorgebracht wird, lautet: ein
auditierbares Kassenbuch müsse den Zustand von damals zeigen, also auch die Gruppe, in der
eine Kategorie zum Buchungszeitpunkt stand. Roman hat dazu ausdrücklich entschieden, auf
Vorlage beim Verfeinern von `ledger-h6ptn`:

> Benennt er eine Kategoriegruppe in YNAB um oder verschiebt er eine Kategorie in eine
> andere Gruppe, soll eine alte Buchung **die heutige Gruppe** zeigen, nicht die von
> damals.

Begründung: eine Umbenennung oder Verschiebung ist in seiner Arbeitsweise eine **Korrektur
der Einordnung**, kein neuer Sachverhalt. Sie sagt "so hätte es immer heißen sollen", nicht
"ab jetzt ist es anders". Die Historie bleibt davon unberührt, weil sie die
Kategorie-**Zuweisung** protokolliert (welche Kategorie wurde wann von wem gesetzt), nicht
deren **Beschriftung**.

Damit trennt sich der Auditierbarkeits-Anspruch sauber in zwei Hälften, und beide bleiben
erfüllt:
- **Für die Zuweisung** gilt "Zustand von damals" unverändert weiter. Sie steht als
  `CategoryId` plus beobachteter Blattname unveränderlich im Log, und die Historie zeigt
  sie so, wie sie beobachtet wurde.
- **Für die Beschriftung der Kategorie** gilt es ausdrücklich nicht. Sie ist Stammdatum und
  wird in der jeweils gültigen Fassung angezeigt.

Das ist die eigentliche Rechtfertigung dieser Entscheidung, und es ist eine bewusste
Festlegung Romans, kein Nebeneffekt der Implementierung. Fiele sie andersherum aus, wäre
der richtige Weg **nicht** ein Feld an sechs Buchungs-Payloads, sondern ein eigener
beobachteter Kategorie-Katalog mit eigenem Log, aus dem sich der Stand zu jedem Zeitpunkt
rekonstruieren ließe.

### Positive
- Alle rund 14.000 Bestandsbuchungen zeigen die Gruppe sofort, ohne Vollabruf, ohne
  Nachziehen, ohne dauerhaft gemischte Beschriftungen in der Liste.
- Benennt Roman eine Gruppe um oder verschiebt eine Kategorie, wirkt das beim nächsten
  Lauf überall, ohne dass ein gespeichertes Ereignis angefasst wird.
- `Events.fs` bleibt unberührt: keine neue Schema-Version, kein neuer Decoder, kein neuer
  Zweig in `decodePayload`, keine Migrationsfrage.
- Derselbe Lookup kann später die Split-Zeilen (`SplitTarget.ToCategory`) qualifizieren,
  ohne ein zweites Mal Codec-Arbeit.
- Buchungen, bei denen YNAB kein `category_name` lieferte und `observationDecoder` deshalb
  `""` ins Ereignis geschrieben hat, bekommen über die Id wieder eine Gruppe.
- Die Regel in Punkt 7 macht die nächste Feld-Frage entscheidbar statt diskutierbar.

### Negative
- Der Spiegel-Lauf kostet einen Request mehr und `MirrorFetchers` ein Feld mehr; alle
  Test-Fixtures des Laufs müssen einmalig angefasst werden.
- Eine neue Tabelle mit gespiegelten Stammdaten, die veralten kann. Nach einem frischen
  Deploy ohne gelaufenen Spiegel ist sie leer, die Liste zeigt dann nur Blattnamen.
- Weil nie gelöscht wird, wächst die Tabelle monoton und enthält irgendwann Kategorien,
  die es in YNAB nicht mehr gibt. Bei der Größenordnung (einige Dutzend bis wenige
  Hundert Zeilen) ohne Belang, aber sie taugt deshalb nicht als Quelle für einen
  Kategorie-Picker: der bräuchte den heutigen Katalog, nicht die Sammlung aller je
  gesehenen.
- Die Historie zeigt die Gruppe nicht. Wer später die Gruppe *zum Beobachtungszeitpunkt*
  sehen will, braucht einen eigenen beobachteten Kategorie-Katalog mit eigenem Log, nicht
  ein Feld an sechs Buchungs-Payloads.
- Der Client lädt für die Kategorie-Mehrfachauswahl (`ledger-j8xq4`) denselben Katalog noch
  einmal, in anderer Form (mit `Available` und versteckten Kategorien). Bewusst in Kauf
  genommen: zwei Verbraucher mit verschiedenen Anforderungen, nicht eine Quelle doppelt.

### Neutral
- Die Entscheidung ist umkehrbar. Stellt sich heraus, dass die Gruppe zum
  Beobachtungszeitpunkt gebraucht wird, fällt das neue Feld unter Punkt 7, erste Art
  (optionales Feld, kein Versionssprung), und die Tabelle bleibt als Quelle für den Bestand.

## Alternatives considered

**Schema-Version hochzählen nach ADR-0032-Muster.** `EntryObservedInYnab` und
`CashExpenseAdded` auf 3, die anderen drei auf 2, fünf neue Alt-Decoder, über 18 Zweige in
`decodePayload`. Abgelehnt: kauft gegenüber dem optionalen Feld nichts außer einer
Versionsnummer (das Anzeige-Ergebnis für den Bestand ist in beiden Fällen "keine Gruppe"),
und der Aufwand wiederholt sich bei jedem weiteren Baustein-Feld.

**Optionales Feld `GroupName: string option` in `decodeCategory`, keine Versionssprünge.**
Nach Punkt 7 zulässig und billig, löst aber das eigentliche Problem nicht: die Gruppe
erreicht nur künftig geschriebene Ereignisse, die Liste bliebe auf Jahre gemischt
beschriftet. Außerdem friert sie ein Stammdatum ein, das YNAB global ändert, und braucht
denselben dritten Abruf. An zwei der vier Stellen, die ein `CategoryRef` bauen (Spiegel,
Import-Push), liegt die Gruppe ohnehin nicht vor.

**Eigene Version im `category`-Objekt (`"v": 2`).** Zweite Versionsachse unterhalb der
Dispatch-Tabelle, unsichtbar in `decodePayload`; das Zurücklesen wäre nicht mehr an einer
Stelle nachvollziehbar. Abgelehnt.

**Backfill über die gespeicherten Payloads.** Umschreiben des Logs, ausgeschlossen durch
ADR 0013.

**Gruppe im Client auflösen, gegen den Katalog, den `ledger-j8xq4` ohnehin lädt.** Der
stärkste Gegenvorschlag: kein Backend-Zug außer `CategoryId` in `LedgerEntryView`, und die
Ladekosten fallen für die Kategorie-Mehrfachauswahl sowieso an. Abgelehnt, weil der
Kassenbuch-Lesepfad heute bewusst netzfrei ist und seitenweise lädt (Endlos-Scrollen): jede
Zeile bekäme eine zweiphasige Beschriftung, die vom Blattnamen auf die qualifizierte Form
umspringt, sobald ein separater YNAB-Abruf zurückkommt. Die gespiegelte Tabelle liefert die
Gruppe zusammen mit der Zeile, aus derselben Quelle, in derselben Abfrage.

**Kategorien als JSON-Blob in den Settings** statt als Tabelle. Spart die Migration,
verlangt aber einen handgeschriebenen Codec und ein Parsen je Abfrage für etwas, das ein
Nachschlagen ist. Die Tabelle ist die langweiligere Form und passt zu `ledger_accounts`.
