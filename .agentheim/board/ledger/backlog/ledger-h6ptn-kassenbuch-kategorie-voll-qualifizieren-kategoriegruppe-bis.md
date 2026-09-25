---
id: ledger-h6ptn
title: "Kassenbuch-Kategorie voll qualifizieren: Gruppe im Lesepfad und in der PC-Tabelle"
status: backlog
type: feature
context: ledger
created: 2026-09-19
completed:
depends_on: [ledger-p3wnq, ledger-nnn47]
blocks: []
tags: [frontend, backend, kassenbuch, styleguide]
related_adrs: [0013, 0014, 0032, 0039]
related_research: []
prior_art: [ledger-w9skm, ledger-t7v2q]
---

## Why

`ledger-w9skm` hat die Kategorie in der Kassenbuch-Liste in die Farbrolle Violett
(`#a855f7`) gebracht, aber bewusst nur den Blattnamen gezeigt ("Lebensmittel"). Der
Entwurf verlangt in der PC-Tabelle die volle Qualifizierung:
`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`, 1.9 Zeile 260,
"Kategorie Inter 500 13px `#a855f7` (voll qualifiziert, z. B. „Alltag: Lebensmittel")".

**Korrektur der früheren Fassung dieses Items.** Der alte `## Why` behauptete, der Entwurf
sehe die volle Qualifizierung "an beiden Stellen" vor und nannte dafür 1.1 Zeile 2. Das
stimmt nicht: die Beispielliste in **1.1** (Zeile 104-112) zeigt durchweg Blattnamen
("Lebensmittel", "Bahn", "Streaming", "Nebenkosten", "Apotheke"), und die Zeile 99 legt
nur Typografie und Farbe fest, nicht die Form der Beschriftung. Voll qualifiziert steht es
in **1.9** (PC-Tabelle, Zeile 260) und in **1.5** (Detail-Panel, Zeile 171: "Mobilität:
Bahn"). Die Historie in 1.5 Zeile 189 zeigt wieder den Blattnamen ("Kategorie ‚Bahn' von
YNAB übernommen"). Daraus folgt die Aufteilung unten: PC qualifiziert, Handy Blattname,
Historie Blattname.

Grund für die Lücke: `CategoryRef` (`src/Server/Ledger/Events.fs:66`) trägt nur
`CategoryId` und `CategoryName`, und `ReadModel.toEntryView`
(`src/Server/Ledger/QueryHandlers.fs:103`) reicht genau diesen einen String weiter.

**Erwogen und verworfen: die Gruppe ins Ereignis legen.** Die frühere Fassung dieses Items
war genau so formuliert ("`CategoryRef` trägt die Kategoriegruppe", plus Migrationsfrage
für bereits gespeicherte Ereignisse). Das ist am 2026-09-20 modelliert worden
(tactical-modeler und architect, unabhängig voneinander zum selben Ergebnis) und
**verworfen**, festgehalten in ADR 0039. Vier benennbare Gründe, damit das in sechs
Monaten nicht erneut aufgemacht wird:

1. **Es hätte das Anzeigeproblem nicht gelöst.** Ein neues Payload-Feld erreicht nur
   künftig geschriebene Ereignisse. Der Delta-Abruf liefert eine Buchung erst wieder, wenn
   sie sich in YNAB ändert (ADR 0014); ADR 0032 Punkt 3 hat für genau diese Lage schon
   entschieden, dass es kein Nachholen für den Bestand gibt. Von rund 14.000
   Bestandsbuchungen hätte praktisch keine eine Gruppe bekommen, und die Liste zeigte auf
   Jahre gemischt "Lebensmittel" und "Alltag: Lebensmittel".
2. **Die Daten liegen an der Schreibstelle gar nicht vor.** YNABs Transaktions-JSON trägt
   keine Gruppe (`src/Server/YnabObservations.fs:170-194` liest `category_id` und
   `category_name`, mehr gibt es dort nicht). Der Spiegel hätte zwei API-Antworten joinen
   und das Ergebnis als Beobachtung ablegen müssen; ADR 0013 Punkt 4 verlangt das
   Gegenteil.
3. **Die Gruppe ist Stammdatum der Kategorie, nicht Zustand der Buchung.** Verschiebt
   Roman eine Kategorie in YNAB, passiert an der Buchung nichts. Und er will dann die
   heutige Gruppe sehen, nicht die von damals (seine Entscheidung vom 2026-09-20, ADR
   0039, Consequences).
4. **Der Preis wäre hoch gewesen.** `CategoryRef` ist ein geteilter Baustein in sechs
   Payload-Feldern über fünf Ereignistypen. Nach dem Muster von ADR 0032 Punkt 4 hätte das
   fünf Versionssprünge, fünf Alt-Decoder und über 18 Zweige in `decodePayload` gekostet,
   und die Rechnung wiederholte sich bei jedem weiteren Baustein-Feld.

Stattdessen spiegelt `ledger-p3wnq` die Kategorien in die Tabelle `ledger_categories`, und
dieses Item schlägt sie im Lesepfad nach. `Events.fs` wird nicht angefasst, und die
Migrationsfrage, die die frühere Fassung für den Kern hielt, entfällt ersatzlos.

## What

### 1. Lesepfad (ADR 0039, Punkt 5)

- `LedgerEntryView` (`src/Shared/Domain.fs:830`) bekommt `CategoryGroupName: string option`
  neben dem bestehenden `CategoryName`. Die beiden Teile bleiben im Vertrag getrennt,
  zusammengesetzt wird erst in der Anzeige.
- `ReadModel.toEntryView` (`QueryHandlers.fs:103-119`) bekommt die Zuordnung als
  zusätzlichen Parameter (die `Map<Guid, string>` aus `ledger-p3wnq` genügt) und bleibt
  rein. Die Map wird einmal je Abfrage geladen, nicht je Buchung, an den beiden
  Aufrufstellen `QueryHandlers.fs:317` und `:377`.
- Die Historien-Sätze (`describe*`, `QueryHandlers.fs:132-232`) bleiben **unverändert**
  beim Blattnamen (ADR 0039 Punkt 5; Handoff 1.5 Zeile 189: "Kategorie ‚Bahn' von YNAB
  übernommen"). Kein Nachschlagen in `describe`, das würde die Funktion unrein machen.

### 2. Anzeige

`categoryLabel` (`src/Client/Components/Ledger/View.fs:209-213`) ist heute zwischen
Handy-Zeile (`entryRow`) und PC-Tabelle (`entryTableRow`) geteilt, und der Doc-Kommentar
darüber (Zeile 204-208) erklärt ausdrücklich, warum nur der Blattname steht. Der Entwurf
verlangt jetzt unterschiedliche Formen: die Beispielliste in 1.1 (Zeile 104-112) zeigt
Blattnamen, die PC-Tabelle in 1.9 (Zeile 260) ausdrücklich die volle Qualifizierung.

Deshalb **zwei benannte Funktionen über derselben Quelle**, nicht zwei Zweige in den Views:

- `categoryLabel` bleibt, was es ist (Blattname), und wird weiter von der Handy-Zeile
  benutzt. Verhalten unverändert.
- `categoryLabelQualified` kommt dazu und wird von der PC-Tabelle benutzt: `"{Gruppe}:
  {Name}"`, wenn Gruppe und Name vorliegen und die Gruppe nach `Trim()` nicht leer ist;
  sonst Rückfall auf `categoryLabel`. Nie ein führendes `": "`, nie ein leeres Präfix.

Der veraltete Doc-Kommentar an `categoryLabel` (Zeile 204-208, "Zeigt bewusst nur den
Blattnamen … `CategoryRef` traegt keine Kategoriegruppe") ist zu ersetzen: er beschreibt
den alten Grund und verweist auf ein Backlog-Item, das jetzt dieses ist.

Unverändert bleiben:
- Aufgeteilte Buchung ohne eigene Kategorie: weiter `"{n} Split-Zeilen"`. Es gibt keine
  eine Kategorie, also nichts zu qualifizieren.
- Buchung ohne Kategorie und ohne Splits: weiter "ohne Kategorie".
- Split-Zeilen selbst (`SplitTarget.ToCategory`): in diesem Item unqualifiziert. Der
  Lookup könnte es sofort, der Entwurf sagt nichts dazu (siehe Notes).

### 3. Bestehende Tests

Die drei `categoryLabel`-Tests in `src/Tests/LedgerViewTests.fs:147-165` (Kategoriename
direkt, Split-Zeilenanzahl, Platzhaltertext) müssen mitwandern: sie bleiben inhaltlich, was
sie sind, prüfen aber `categoryLabel` weiterhin auf das heutige Ergebnis und bekommen
Geschwister für `categoryLabelQualified`. Der `testList`-Name "categoryLabel
(ledger-w9skm)" ist um dieses Item zu ergänzen. Die Konstruktion der Test-`LedgerEntryView`
braucht das neue Feld.

### Nicht in diesem Item

- **Das Detail-Panel aus Handoff 1.5** ("Kategorie → Mobilität: Bahn"). Ausdrücklich
  nachgeprüft: das `BottomSheet` im Kassenbuch (`src/Client/Components/Ledger/View.fs:517`,
  `historySheet`) zeigt heute Kopf, Herkunfts-Badge und die Historie-Zeitleiste, aber
  **kein Kategorie-Feld**. Ein Akzeptanzkriterium "das Detail-Panel zeigt Gruppe: Name"
  wäre unerfüllbar, und ein Worker würde es entweder stillschweigend übergehen oder den
  Task um eine ganze Scheibe erweitern. Beides ist schlecht, deshalb steht es hier als
  ausgeschlossen. Die Qualifizierung im Panel kommt mit der Scheibe, die das Panel baut;
  sie erbt `CategoryGroupName` aus diesem Item, ohne weitere Backend-Arbeit.
- Das orangene Badge "Keine Kategorie" statt des Fließtexts "ohne Kategorie" (Handoff 1.1
  Zeile 111-115, `#ff6b2c`). Eigene Abweichung aus `ledger-w9skm`, eigenes Item.
- Die Kategorie-Mehrfachauswahl des Filters. Sie gehört zu `ledger-j8xq4` und lädt dort
  ihren eigenen Katalog im Client (mit `Available` und versteckten Kategorien, die
  `ledger_categories` nicht führt). Beschriftung dort nach dem bestehenden Frontend-Muster
  `"{GroupName}: {Name}"` (`QuickAddPage.fs:89`,
  `SyncFlow/Views/TransactionList.fs:182`, `Rules/View.fs:325`).

## Acceptance criteria

- [ ] `src/Server/Ledger/Events.fs` ist unverändert: kein neues Feld in `CategoryRef`,
      keine neue Schema-Version, kein neuer Zweig in `decodePayload`. Per Diff belegt.
- [ ] Ein Test pinnt den Baustein-Codec: `Codec.encodeCategory` erzeugt ein JSON-Objekt mit
      **genau** den Schlüsseln `categoryId` und `categoryName`, und ein gespeichertes
      `EntryObservedInYnab` in Schema-Version 1 **und** in Version 2 lässt sich weiterhin
      über `decodePayload` lesen. (Dieser Test tritt an die Stelle des ursprünglich
      geforderten Migrations-Kriteriums: es gibt keine Migration, weil das Ereignisformat
      unverändert bleibt, und dieser Test hält genau das fest, statt es nur zu behaupten.)
- [ ] `LedgerEntryView` trägt `CategoryGroupName: string option`.
- [ ] `getLedgerEntriesPage` liefert für eine Buchung, deren `CategoryId` in
      `ledger_categories` steht, den Gruppennamen in `CategoryGroupName`, und für eine
      Buchung mit unbekannter `CategoryId` `None`. Beides gegen In-Memory-SQLite belegt
      (ADR 0030).
- [ ] **Kategorie in YNAB gelöscht, Fall 1 (der häufige):** die Kategorie war schon einmal
      gespiegelt, ihre Zeile steht also weiter in `ledger_categories` (`ledger-p3wnq`
      löscht nie). Die Buchung zeigt weiter "Gruppe: Name". Belegt durch einen Test, der
      nach dem Befüllen einen Upsert **ohne** diese Kategorie fährt und danach die Zeile
      abfragt.
- [ ] **Kategorie in YNAB gelöscht, Fall 2 (nie gespiegelt):** die `CategoryId` steht
      nicht in `ledger_categories`. `CategoryGroupName` ist `None`, und die Anzeige zeigt
      den **Blattnamen aus dem Ereignis**, also das heutige Bild. Ausdrücklich nicht: eine
      leere Zelle, ein nacktes ": ", oder ein erfundener Gruppenname.
- [ ] Eine Buchung ohne Kategorie liefert `CategoryGroupName = None`, ohne dass der Lookup
      läuft oder wirft.
- [ ] `toEntryView` bleibt rein: die Zuordnung kommt als Parameter herein, die Funktion
      macht kein I/O. Die Map wird je Abfrage einmal geladen, nicht je Buchung (durch die
      Signatur erzwungen, nicht nur durch Absicht).
- [ ] `getLedgerEntriesPage` macht weiterhin keinen Netzaufruf: die bestehenden
      In-Memory-Fixtures ohne YNAB-Zugang laufen unverändert grün.
- [ ] `categoryLabelQualified` ist eine reine Funktion mit Tests: Gruppe und Name vorhanden
      → "Alltag: Lebensmittel"; Gruppe `None` → Blattname; Gruppe leer oder nur Leerzeichen
      → Blattname, ohne führendes ": "; keine Kategorie, aber Splits → "{n} Split-Zeilen";
      keine Kategorie, keine Splits → "ohne Kategorie".
- [ ] `categoryLabel` liefert für dieselben Eingaben unverändert die heutigen Ergebnisse.
      Die drei bestehenden Tests in `src/Tests/LedgerViewTests.fs:147-165` bleiben in ihrer
      Aussage erhalten (nur die Konstruktion des `LedgerEntryView` wird um das neue Feld
      ergänzt) und sind grün.
- [ ] Die PC-Tabelle benutzt `categoryLabelQualified`, die Handy-Zeile `categoryLabel`.
      Der Unterschied ist über die beiden benannten Funktionen ausgedrückt, nicht über
      zwei Zweige in den Views; ein Test belegt, dass beide auf derselben Eingabe
      unterschiedliche Ergebnisse liefern, sobald eine Gruppe vorliegt.
- [ ] Die Historien-Sätze sind unverändert: ein Test belegt, dass `describe*` für ein
      `EntryObservedInYnab` mit Kategorie weiterhin den Blattnamen liefert und keine Gruppe
      enthält.
- [ ] Der veraltete Doc-Kommentar an `categoryLabel` (`View.fs:204-208`) ist ersetzt und
      verweist nicht mehr auf ein offenes Backlog-Item. Er darf den Platzhalter
      `"ohne Kategorie"` wieder wörtlich nennen: `ledger-nnn47` grenzt die Zählung in
      `LedgerViewTests` auf Code ein und läuft vorher.
- [ ] Screenshots (Handy + PC) zeigen die Kategorie gegen den Entwurf: PC-Tabelle voll
      qualifiziert (1.9 Zeile 260), Handy-Zeile Blattname (1.1 Zeile 104-112).
      [human-eye]
- [ ] `dotnet build`, `dotnet test`, `npm run build` grün.

## Notes

Die frühere Auflage "erst starten, wenn `ledger-t7v2q` in `done/` liegt" ist erledigt und
entfällt doppelt: `t7v2q` liegt seit dem 2026-09-19 in `done/`, und mit ADR 0039 fasst
dieses Item `Events.fs` gar nicht mehr an. Was `t7v2q` dort hinterlassen hat und hier nur
gelesen wird: `EntryTransferCounterpartObservedInYnab` samt Payload, Codec-Paar
(`encodeTransferCounterpart`/`decodeTransferCounterpart`) und dem neuen
`decodeEntryId`-Helfer, dazu ein `describe`-Zweig in `QueryHandlers.fs`. `CategoryRef` und
`decodeCategory` hat `t7v2q` nicht berührt.

Aus `ledger-w9skm`s Notes übernommen: "Kommt die Gruppe nicht mit, ist das ein eigener
kleiner Zug durch `ReadModel.toEntryView` und gehört benannt, statt still weggelassen zu
werden." Die Modellierung hat den Zug nun benannt, er geht nur in die andere Richtung als
damals vermutet: über gespiegelte Stammdaten, nicht über das Ereignis-Schema.

**Warum die Gruppe sich ändern darf und das richtig so ist.** Benennt Roman eine
Kategoriegruppe in YNAB um oder verschiebt eine Kategorie, zeigen ab dem nächsten
Spiegel-Lauf auch alte Buchungen die neue Gruppe. Das ist gewollt (ADR 0039 Punkt 2):
auditierbar ist die Kategorie-**Identität**, und die liegt als `CategoryId` unveränderlich
im Log. Die Liste ist eine Arbeitsfläche und zeigt, wie die Welt heute aussieht; der Ort
für damals ist die Historie, und die bleibt beim beobachteten Blattnamen.

**Redundanz zum Client-Katalog, bewusst eingegangen und hier benannt, damit sie in sechs
Monaten nicht als Versehen gelesen wird.** `ledger-j8xq4` lädt für die
Kategorie-Mehrfachauswahl den YNAB-Katalog noch einmal im Client. Das ist keine doppelte
Quelle, sondern zwei Verbraucher mit unvereinbaren Anforderungen:

- Der **Picker** braucht den **heutigen** Katalog, dazu `Available` (`ledger-kwwxz`) und
  ein Kennzeichen für versteckte Kategorien, damit er sie einklappen kann. `YnabCategory`
  hat heute überhaupt kein `Hidden`-Feld, das müsste `j8xq4` erst ergänzen.
- `ledger_categories` ist bewusst das Gegenteil: die Sammlung **aller je gesehenen**
  Kategorien, inklusive der in YNAB gelöschten, ohne `Available`, weil eine alte Buchung
  ihre Beschriftung behalten soll (ADR 0039 Punkt 3).

Eine Tabelle, die gelöschte Kategorien absichtlich behält, darf einen Picker nicht
befüllen, sonst bietet er Roman Kategorien an, die es nicht mehr gibt. Die beiden Quellen
zusammenzulegen wäre deshalb kein Aufräumen, sondern ein Fehler. Falls die Doppelladung je
stört, ist die richtige Antwort ein Frische-gesteuerter Cache vor `getCategories`, nicht
eine gemeinsame Tabelle.

**Was passiert, wenn die Kategorie in YNAB gelöscht wurde.** Das ist der Fall, der im
Alltag zuerst auffällt, deshalb ausbuchstabiert. Die Kaskade hat drei Stufen und endet nie
bei "gar nichts":

1. **Kategorie war schon einmal gespiegelt.** `ledger-p3wnq` löscht keine Zeilen, es
   upsertet nur. Die Zeile steht also weiter in `ledger_categories`, der Lookup trifft,
   und die Buchung zeigt weiter "Alltag: Lebensmittel". Das ist der Normalfall nach dem
   ersten Lauf und das gewünschte Verhalten: eine alte Buchung verliert ihre Beschriftung
   nicht, nur weil Roman die Kategorie heute nicht mehr führt.
2. **Kategorie war nie gespiegelt** (in YNAB gelöscht, bevor der erste Lauf mit dem
   Kategorie-Abruf lief; YNABs `/categories` liefert gelöschte Kategorien ohne Delta nicht
   mehr mit). Der Lookup trifft nicht, `CategoryGroupName` ist `None`,
   `categoryLabelQualified` fällt auf `categoryLabel` zurück und zeigt den **Blattnamen
   aus dem Ereignis**, also exakt das heutige Verhalten. Genau dafür verlangt ADR 0014 den
   Namen im Snapshot: er ist die Rückfallebene, die keinen Katalog braucht.
3. **Buchung ohne Kategorie.** Unverändert "ohne Kategorie" bzw. "{n} Split-Zeilen".

Es gibt keinen Pfad, auf dem die Zelle leer bleibt oder ein nacktes ": " zeigt. Dasselbe
gilt nach einem frischen Deploy ohne gelaufenen Spiegel: `ledger_categories` ist dann leer,
jede Zeile fällt auf Stufe 2 zurück, und die Liste sieht aus wie heute. Richtig, nur
unvollständig, nie falsch.

**Nebenbefund, der sich teilweise mit erledigt.** `observationDecoder`
(`src/Server/YnabObservations.fs:188-194`) schreibt bei vorhandener `category_id` ohne
`category_name` ein `""` ins Ereignis. Über den Lookup bekommen solche Buchungen am PC
wieder eine Gruppe; der leere Blattname bleibt aber stehen und ergäbe "Alltag: ". Das
`Trim()`-Kriterium oben greift nur für die Gruppe, nicht für den Namen. Ob der leere
Blattname eine eigene Reparatur wert ist, gehört in ein eigenes Item, nicht hierher; für
dieses Item genügt, dass nichts abstürzt.

**Offener Punkt für später:** Split-Zeilen (`SplitTarget.ToCategory`) qualifizieren? Der
Lookup könnte es sofort, der Entwurf sagt nichts dazu. Bis zur Klärung unverändert lassen.

**Dateien, die angefasst werden** (aus dem Grep, nicht geraten): `src/Shared/Domain.fs`
(`LedgerEntryView`), `src/Server/Ledger/QueryHandlers.fs` (`toEntryView` plus die beiden
Aufrufstellen 317 und 377), `src/Client/Components/Ledger/View.fs` (`categoryLabel`,
`categoryLabelQualified`, `entryTableRow`, der Doc-Kommentar),
`src/Tests/LedgerViewTests.fs` (Zeile 147-165 plus neue Tests), `src/Tests/LedgerPageTests.fs`
(Konstruktion der erwarteten `LedgerEntryView`, plus neue Lookup-Tests).
`src/Tests/LedgerPageTests.fs:35` baut ein `CategoryRef` und bleibt unverändert.

**Reihenfolge (ergänzt am 2026-09-25):** `depends_on` um `ledger-nnn47` erweitert. Beide
Aufgaben fassen `src/Tests/LedgerViewTests.fs` und den Doc-Kommentar an `categoryLabel` an;
`nnn47` zuerst, damit der neue Kommentar hier den Platzhalter nennen darf, ohne den
Zähl-Test rot zu machen.
