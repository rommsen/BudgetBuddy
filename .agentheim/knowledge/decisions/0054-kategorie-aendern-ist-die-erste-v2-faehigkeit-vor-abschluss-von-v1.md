---
id: 0054
title: Kategorie ändern wird als erste v2-Fähigkeit gebaut, bevor v1 abgeschlossen ist
scope: ledger
status: accepted
date: 2026-10-24
supersedes: []
superseded_by: []
related_tasks: [ledger-k2rbn, ledger-kwwxz, ledger-w4vte]
related_adrs: [0012, 0028, 0050, 0053, 0055]
related_research: []
---

# ADR 0054: Kategorie ändern wird als erste v2-Fähigkeit gebaut, bevor v1 abgeschlossen ist

## Context
Die Vision teilt in v1 (alles unter einem Dach, lesend, mit dem Prüfvermerk als einziger
schreibender Ausnahme) und v2 (Ownership: Kategorie, Split und Übertrag in BB führen, YNAB
nachziehen, Abweichungen sehen). ADR 0028 hält fest, dass der Prüfvermerk die einzige
schreibende Fähigkeit vor v2 ist und dass Kategorie ändern, Split und Lena-Zuweisung mit
dem allgemeinen Rückschreibe-Weg und der Abweichungs-Sicht kommen.

Das v1-Kriterium "Geldmarkt-ETF als Budget-Konto" ist noch offen (`ledger-nb4tv`,
`ledger-c7j8e`). v2 hat damit formal nicht begonnen. Das Kassenbuch ist im Alltag aber
benutzbar, der Prüfvermerk läuft, und die häufigste Nachbearbeitung, aus der Roman heute
YNAB öffnet, ist das Ändern einer Kategorie. Roman hat am 2026-10-24 entschieden, v2
jetzt zu verfeinern und mit dieser Fähigkeit zu beginnen. Die Stufen-Reihenfolge der Vision
("zwei Stufen, in dieser Reihenfolge") bleibt als Gate für die Frage, wann v2 als bewährt
gilt, bestehen. Dieser ADR ändert nur, welche Fähigkeit wann gebaut wird.

## Decision
1. **Kategorie ändern wird jetzt gebaut**, als erste Fähigkeit aus v2, parallel zu den
   offenen v1-Aufgaben (ETF-Konto). Dazu gehören: ein Picker mit zwei Einstiegen
   (Detail für eine Buchung, Auswahl-Leiste für viele, ADR 0053), die Bearbeitung von
   Split-Buchungen im Kassenbuch und die Available-Anzeige im Picker. Der Schreibweg steht
   in ADR 0055.
2. **ADR 0028 bleibt gültig und wird nicht ersetzt.** Sein Punkt 2 ("Kategorie ändern ...
   bleibt v2") wird mit diesem ADR aufgehoben, soweit er die Kategorie und den Split
   betrifft. Die Lena-Zuweisung (`ledger-xfjcp`) und der Übertrag-Weg bleiben v2 ohne
   vorgezogenen Termin. Sein Punkt 1 gilt sinngemäß weiter: es gibt keinen allgemeinen
   Update-Weg nach YNAB, jedes geschriebene Feld bekommt eine eigene benannte Funktion mit
   eigener Signatur (`updateReviewMark`, künftig `updateCategory` und der Split-Weg).
   Sein Punkt 3 (jüngerer Stand gewinnt) gilt nur für den Prüfvermerk. Für die Kategorie
   gilt "YNAB gewinnt", siehe ADR 0055.
3. **Die Abweichungs-Sicht ist nicht Voraussetzung.** ADR 0028 koppelte Kategorie und
   Split an die Abweichungs-Sicht, weil ein Konflikt dort Information vernichten kann.
   Roman nimmt das in Kauf: Bei einem Konflikt übernimmt der Spiegel den YNAB-Stand wie
   jede andere Beobachtung, die Historie zeigt beide Schritte ("In BB geändert", danach
   "In YNAB geändert"). Die Pink-Leiste "Abweichung" bleibt eine spätere Fähigkeit.
4. **Die Vision wird nicht geändert, solange Roman nicht zustimmt.** Der Vorschlag für den
   Hinweis steht in der Ergebnis-Notiz der Verfeinerung (Vision, Abschnitt "What success
   looks like"): v2 beginnt mit Kategorie ändern, bevor das v1-Kriterium ETF erfüllt ist.

## Consequences
**Gut:**
- Roman muss YNAB für die häufigste Nachbearbeitung nicht mehr öffnen, ohne auf den ETF
  zu warten.
- Der Prüfvermerk-Schreibweg (ADR 0050) hat die Erfahrung mit dem Schreiben nach YNAB
  geliefert, die ADR 0028 als Voraussetzung nannte.

**Schlecht oder offen:**
- Die Einteilung der Vision in zwei Stufen ist nicht mehr zeitlich zu lesen. Wer die Vision
  liest, sieht v1 mit zwei schreibenden Fähigkeiten (Prüfvermerk, Kategorie), ohne dass v2
  begonnen hätte, bis der Hinweis eingepflegt ist.
- Kategorie und Split verfälschen Zahlen, auf die das Budget sich stützt (Argument aus
  ADR 0028). Das Risiko wird durch die Historie, den sichtbaren Fehlschlag-Zustand und
  "YNAB gewinnt" begrenzt, nicht beseitigt.
- Das Abbruchkriterium "wann ist v2 bewährt" (Vision, offene Fragen) wird erst bei v2-Beginn
  festgezurrt. Es gilt für diese Fähigkeit nicht automatisch.

## Alternatives considered
**Warten, bis das ETF-Kriterium erfüllt ist.** Hält die Vision sauber, aber Roman nutzt
YNAB für die Nachbearbeitung weiter, solange der ETF offen ist (`ledger-nb4tv` hat keinen
Termin). Von Roman abgelehnt.

**Erst die Abweichungs-Sicht, dann Kategorie ändern.** Entspricht ADR 0028 wörtlich, bringt
aber eine zweite große Fähigkeit vor die erste. Verworfen, weil "YNAB gewinnt" mit sichtbarer
Historie für den Anfang genügt.

**ADR 0028 ersetzen.** Der Kern (kein allgemeiner Update-Weg, Prüfvermerk als kleinste
Fähigkeit) bleibt wahr. Ein Nachtrag in ADR 0028 verweist auf diesen ADR.
