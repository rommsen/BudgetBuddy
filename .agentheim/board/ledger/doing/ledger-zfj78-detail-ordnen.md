---
id: ledger-zfj78
title: "Detail einer Buchung ordnen (Sheet und Panel): Payee als Kopf, Betrag mit Prüfstand, Knopf Geprüft, Payee mit alle zeigen, Aufteilen und Lena als Knopfzeile, Historie aufklappbar in der Form von §7.11"
status: doing
type: feature
context: ledger
created: 2026-11-14
completed:
depends_on: [design-system-001-styleguide, ledger-cnaz3]
blocks: []
tags: [etappe-3, frontend, kassenbuch]
related_adrs: [0086, 0087]
related_research: []
prior_art: [ledger-nc708, ledger-z6sya, ledger-m7mye, ledger-fxt09, ledger-hg5m5]
---

## Why
Schnitt 8 aus Etappe 3 (Fahrplan), dritter von drei Teilen aus ledger-f8d7x (Kassenbuch ordnen). Das Detail ist über die
Tasks gewachsen: dreizehn Abschnitte untereinander, der Prüfstand als Schalter weit unten, die Historie als Zeitleiste in
eigener Form. Der Styleguide verlangt für die Historie eine Form für Buchung und Kategorie (§7.11).

## What
Inhalt des Details (`detailContent`, gemeinsam für BottomSheet unter 1024 px und Panel ab 1024 px) nach Prototyp Version 17,
Zweig `tx` in `sheetHtml`. Alles, was BB im Detail mehr kann, bleibt und bekommt einen Platz in dieser Ordnung.

## Acceptance criteria

Reihenfolge, von oben
- [ ] Kopf: Payee als Titel (oder „ohne Payee“), darunter „<Tag> · <Konto>“ gedämpft, rechts Schließen. Ersetzt den
      Sheet-Titel „Buchung“.
- [ ] Betrag groß (Mono) als Knopf mit Pfeil, öffnet „Betrag“ wie heute; gesperrt mit Grund wie heute (BB). Rechts in
      derselben Zeile „zu prüfen“ in Gelb oder „geprüft“ in Grün.
- [ ] Die heutige Hinweisleiste (höchstens eine, etwa „Noch nicht in YNAB“ mit „Erneut senden“) direkt darunter (BB).
- [ ] Knopf über die ganze Breite: „Geprüft“ primär bei einer ungeprüften Buchung, „Als ungeprüft markieren“ neutral bei
      einer geprüften. Ersetzt den Schalter „Geprüft“ (Prototyp). Rückmeldung mit „Rückgängig“ wie heute.
- [ ] Zeilen (Prototyp `.kv`, Beschriftung links, Wert rechts):
      - „Kategorie“ mit Wert oder „ohne Kategorie“ in Gelb, „Aufgeteilt“ bei einem Split, Pfeil zur Auswahl; dahinter
        „alle zeigen“ in Akzent, setzt wie heute den Kategorie-Filter (BB).
      - Bei einem Split die Split-Zeilen (Kategorie oder Konto, Betrag) in einer Karte direkt darunter.
      - Das Regel-Angebot nach einer Kategorie-Änderung wie heute (ledger-hg5m5), unter der Kategorie.
      - „Memo“ mit Text oder „hinzufügen“ gedämpft, Pfeil zum Bearbeiten.
      - „Payee“ mit Namen und Pfeil zu „Payee ändern“ (BB, ledger-fxt09), dahinter „alle zeigen“ in Akzent: setzt die
        Suche auf den Payee (Roman, 2026-11-14), schließt am Handy das Sheet, am PC bleibt das Panel offen.
      - Bestell-Link wie heute („Bestellung bei …“, Nummer darunter, „öffnen“ mit Symbol).
      - „Regel anlegen“ wie heute (ledger-hg5m5).
- [ ] Knopfzeile mit zwei gleich breiten neutralen Knöpfen: „Aufteilen“ (öffnet den Split-Editor, ersetzt die Zeile „Kein
      Split“) und „Lena fragen“ mit Flagge bzw. „Rückfrage zurücknehmen“ (ersetzt den Abschnitt Flag).
- [ ] „Historie (n)“ als aufklappbare Zeile, beim Öffnen des Details zu. Aufgeklappt je Eintrag eine Zeile nach §7.11:
      links Zeitpunkt `TT.MM. HH:MM` (Mono, gedämpft), in der Mitte die Herkunft als Kürzel in Großbuchstaben (Mono,
      dünner Rand, ohne Farbe, etwa ROMAN, BOB, YNAB, REGEL, IMPORT) und der Text, rechts der Betrag, falls es einen gibt.
      Neueste oben.
- [ ] „Herkunft: <Herkunft>“ als kleine gedämpfte Zeile (ersetzt das Badge HERKUNFT).
- [ ] „Buchung löschen“ als kleiner roter Knopf am Ende, wie heute mit Rückfrage und Rückgängig (ledger-z6sya).

Baustein
- [ ] Die Historienzeile nach §7.11 ist ein DS-Baustein in `src/Client/DesignSystem/` und steht auf der Route
      `/styleguide`. Die Zeitleiste mit Punkten im Detail entfällt.

Tests und Abgleich
- [ ] Reine Funktionen unter .NET: Kürzel der Herkunft je Akteur, Zeitpunkt-Text, Reihenfolge der Einträge, Text und Art
      des Prüf-Knopfs je Stand, „alle zeigen“ am Payee setzt die Suche und schließt nur unter 1024 px.
- [ ] Bestehende Browser-Tests zum Detail (`[flow:buchung-oeffnen]`, `[flow:memo-setzen]`, `[flow:payee-aendern]`,
      `[flow:aufteilen]`, `[flow:lena-fragen]`, `[flow:bestell-link-oeffnen]`, `[flow:regel-aus-buchung]`) laufen mit
      der neuen Ordnung; Selektoren werden angepasst, nicht die Aussagen.
- [ ] Layout-Sweep: `kassenbuch-detail` mit aufgeklappter Historie bei 412 und 1440 px.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten: Screenshots des Details bei 412 und 1440 px neben
      `f8d7x-proto/handy-detail.png` und `pc-detail.png`, im Outcome.

## Abweichungen vom Prototyp
- (a) Bleiben aus BB: Hinweisleiste, gesperrter Betrag mit Grund, „alle zeigen“ an der Kategorie, Regel-Angebot,
  „Payee ändern“, „Regel anlegen“.
- (b) „Rückfrage zurücknehmen“ statt „Rückfrage zurück“: derselbe Text wie in der Auswahl-Leiste.

## Notes
Teil 3 von 3 aus ledger-f8d7x (Kassenbuch ordnen), nach ledger-cnaz3 (PC). Die Kategorie-Historie im Budget
(`InspectorView.historyLineView`) folgt §7.11 ebenfalls noch nicht; sie bleibt außerhalb dieser Task und kommt als Zeile
in die Kann-Sammlung budgeting, sobald der Baustein da ist.

Vorlage für Worker und Verifier:
- Prototyp Version 17: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/f8d7x-proto/prototyp-v17.html`
  (Zweig `tx` in `sheetHtml`, `histView` für die Zeilenform, Klassen `.hist`, `.hi`, `.who`).
- Screenshots im selben Ordner: `handy-detail.png`, `pc-detail.png`.

Fundstellen heute (Inventar 2026-11-14): `detailContent` `View.fs` 1969 bis 2074, Kopf 1576 bis 1586, Hinweisleiste 1277
bis 1421, Kategorie und Filterknöpfe 1724 bis 1768, Historie 1228 bis 1264 und 1538 bis 1547 mit
`DesignSystem/Detail.fs` 675 bis 708, `FilterByRowPayee` und `FilterByRowCategory` `State.fs` 1959 bis 1968.
Die Zeilennummern stammen von vor ledger-d9z3n (a2b25490) und ledger-cnaz3 (35f75369) und haben sich verschoben; über die
Namen suchen. `FilterByRowPayee` setzt seit ledger-d9z3n schon die Suche auf den Payee. Seit ledger-cnaz3 (ADR 0096) steht
das Panel ab 1024 px immer und zeigt bei genau einer gewählten Buchung ihr Detail; Lena fragen und Prüfen müssen dort
weiter genau diese eine Buchung treffen.
