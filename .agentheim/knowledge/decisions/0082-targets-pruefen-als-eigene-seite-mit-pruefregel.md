---
id: 0082
title: Targets prüfen ist eine eigene Seite für den laufenden Monat. Eine reine Prüfregel ordnet jede Kategorie aus dem Kassenbuch einer Gruppe zu, ohne Gedächtnis und ohne Hinweis an der Kategorie
scope: budgeting
status: accepted
date: 2026-11-05
supersedes: []
superseded_by: []
related_tasks: [budgeting-p2zr6, budgeting-q4v8n, budgeting-r7k2m, budgeting-m3x7f]
related_adrs: [0065, 0072, 0075, 0080]
related_research: []
---

# ADR 0082: Targets prüfen

## Context
Das Gesamtkonzept sah "Passt das Target noch?" als Hinweis in Gelb an der Kategorie vor, mit `Target •` im Tab und einem gespeicherten "So lassen" (ADR 0075 Punkt 1, alte Fassung von budgeting-p2zr6). Roman hat das am 2026-11-05 an echten Daten verworfen (128 sichtbare Kategorien, 100 Targets, 12 Monate Kassenbuch): Er prüft gelegentlich, nicht an jedem Tag, und eine falsche Einordnung kostet nichts. YNAB rechnet keinen Vergleich zwischen Target und Ausgaben, das Kassenbuch hat die Monatssummen (budgeting-dyn8g). Targets ändert BB nur im laufenden Monat (ADR 0080), geschrieben wird nur `goal_target`, Rhythmus, Tag und Datum bleiben (ADR 0072, Probe zu Punkt 6 bestanden). `target_changes` hält nur Änderungen über BB fest.

## Decision
1. **Eigene Seite statt Hinweis.** "Targets prüfen" ist eine Seite `#/budget/target-check`, geöffnet über einen Knopf im Kopf der Budget-Seite wie Historie. Sie gilt immer für den laufenden Monat. An der Kategorie steht kein Hinweis, der Tab heißt nie `Target •`, `BudgetCategory` bekommt kein Feld, die Budget-Liste keinen Prüf-Filter. Das löst ADR 0075 Punkt 1 für "Passt das Target noch?" ab, Gelb für `fehlt` bleibt.
2. **Kein Gedächtnis.** Kein "So lassen", keine neue Tabelle. Jeder Aufruf der Seite rechnet neu.
3. **Inhalt.** Alle sichtbaren Kategorien des laufenden Monats ohne ausgeblendete, gelöschte und interne, in fünf Gruppen in dieser Reihenfolge: "Gibst du mehr aus?", "Gibst du weniger aus?", "Gibst du nie aus?", "Kein Target", "Passt". "Passt" ist eingeklappt, leere Gruppen fehlen. Eine pausierte Kategorie wird geprüft wie jede andere.
4. **Monatswert.** Das Target wird auf einen Monat umgerechnet: monatlich und MF bleiben, alle n Monate ÷ n, jährlich ÷ 12, alle n Jahre ÷ 12n, wöchentlich × 52 ÷ 12, alle n Wochen × 52 ÷ 12n. Ohne Rundung verglichen.
5. **Nicht geprüft.** TB und TBD (Sparziele) sind ein Guthaben, das wachsen soll, ihr Betrag ist keine Monatsausgabe. Sie stehen bei "Passt" mit "Sparziel, wird nicht geprüft". DEBT, eine unbekannte Art und ein Target ohne Rhythmus haben keinen belastbaren Monatswert und stehen bei "Passt" mit "wird nicht geprüft".
6. **Vergleichswert.** Der Durchschnitt (Ø) der Netto-Ausgaben je Monat aus dem Kassenbuch: Aktivität mit umgekehrtem Vorzeichen, Splits mit ihrem Anteil, Überträge nicht, in YNAB gelöschte Buchungen nicht, Erstattungen mindern. Ein Monat ohne Buchung zählt mit 0. Ein Monat "mit Ausgaben" hat Netto-Ausgaben über 0, ein Monat "über dem Target" Netto-Ausgaben über dem Monatswert.
7. **Gezählte Monate.** Volle Monate vor dem laufenden, höchstens 12, der laufende nie. Gibt es eine in BB geschriebene Target-Änderung (jüngste Zeile in `target_changes`, deren Schritt 1 als geschrieben gilt), zählen nur die vollen Monate nach dem Monat der Änderung, nach Berliner Kalendertag. Der Monat der Änderung selbst zählt nicht: in ihm lagen Ausgaben unter dem alten Target. Eine fehlgeschlagene Änderung zählt nicht. Eine Änderung direkt in YNAB kennt BB nicht, das ist hingenommen. Die Periode P eines Rhythmus in Monaten: monatlich, MF, wöchentlich und alle n Wochen 1, alle n Monate n, jährlich 12, alle n Jahre 12n. Weniger als 3 gezählte Monate, bei P > 3 weniger als P: keine Einordnung, die Zeile steht bei "Passt" mit "neu gesetzt am TT.MM.JJJJ", nach einem Entfernen mit "entfernt am TT.MM.JJJJ". Ein Rhythmus mit P > 12 (alle n Jahre ab n = 2) lässt sich in 12 Monaten nicht beurteilen und steht bei "Passt" mit "wird nicht geprüft".
8. **Einordnung in dieser Reihenfolge**, die erste zutreffende Regel gilt (M Monatswert, N gezählte Monate):
   1. nicht geprüft (Punkt 5, P > 12), dann zu wenige gezählte Monate (Punkt 7): "Passt" mit Zusatz;
   2. mit Target, bei P = 1 höchstens 1 Monat mit Ausgaben, bei P > 1 kein Monat mit Ausgaben: "Gibst du nie aus?";
   3. mit Target, Ø > 1,1 M und Ø − M ≥ 10 €, bei P = 1 zusätzlich in mindestens der Hälfte der Monate über dem Target (2 × Monate darüber ≥ N): "Gibst du mehr aus?";
   4. mit Target, Ø < 0,7 M und M − Ø ≥ 10 €: "Gibst du weniger aus?";
   5. ohne Target, mindestens 3 Monate mit Ausgaben und Ø ≥ 10 €: "Kein Target";
   6. sonst "Passt", auch eine Kategorie ohne Target, die Regel 5 nicht erfüllt.

   P unterscheidet, weil ein Target mit langem Rhythmus gebündelt gezahlt wird: ein jährliches Target zahlt man in einem Monat, "höchstens 1 Monat mit Ausgaben" träfe es immer, "die Hälfte der Monate darüber" nie. Bei P > 1 entscheidet deshalb nur der Ø über die gezählten Monate, also die Summe gegen das Target. Nachgerechnet an Romans Daten am 2026-11-05: ohne P wären 26 von 28 jährlichen Targets bei "nie" gelandet, mit P melden sich nur die drei, die wirklich abweichen (doppelte Zahlung, deutlich weniger gezahlt, keine Zahlung).

   "Nie" steht vor "weniger", weil eine Kategorie ohne Ausgaben auch "weniger" erfüllt, die Frage dort aber eine andere ist (braucht es das Target?) und es keinen Betrag zum Übernehmen gibt. "Mehr" und "weniger" schließen sich aus, ebenso "nie" und "mehr" (ab N = 3 verlangt "mehr" mindestens 2 Monate über dem Target). Die Schwellen sind Startwerte und bewusst grob.
9. **Vorschlag.** Ø auf volle 5 € aufgerundet, als Monatswert. Aufgerundet, weil ein Target unter dem Ø im Mittel zu wenig zurücklegt, Aufrunden kostet höchstens 4,99 € im Monat. Ø ≤ 0 ergibt keinen Vorschlag. Die Rundung gilt für den Monatswert, weil auf ihm verglichen wird. Zurückgerechnet in den Rhythmus des Targets: alle n Monate × n, alle n Jahre × 12n (beides bleibt auf 5 €), alle n Wochen × 12n ÷ 52 auf volle Euro kaufmännisch gerundet, mindestens 1 €. Danach liegt die Kategorie stets bei "Passt": der Monatswert weicht vom Ø um weniger als 10 € ab (bei wöchentlich höchstens 5 € plus 2,17 €).
10. **Übernehmen** schreibt den Vorschlag über den bestehenden Weg `changeCategoryTarget` (ADR 0080) im laufenden Monat: mit Target als Wunsch mit dem Vorschlag im Rhythmus, demselben Datum und unverändertem Rhythmus, also nur `goal_target` (ADR 0072 Punkt 3). Bei "Kein Target" legt es ein monatliches NEED mit dem Vorschlag an. Bei "Gibst du nie aus?" gibt es kein Übernehmen. Nach dem Schreiben rechnet der Client die Zeile mit dem Target aus der Antwort und dem heutigen Tag als letzter Änderung neu, sie steht dann bei "Passt" mit "neu gesetzt am …". Das deckt sich mit dem Server: das Schreiben legt eine Zeile in `target_changes` an, ein Neuladen ordnet gleich ein.
11. **Anpassen …** öffnet auf der Seite das bestehende Target-Formular (am PC ab 1024 px als Panel, darunter als BottomSheet), vorbelegt mit dem Vorschlag. Ein eigener Endpoint zum Schreiben entsteht nicht.
12. **Die Regel ist eine reine Funktion in `Shared.Budgeting`.** Der Server liefert die Rohdaten (Kategorien mit Target aus YNAB, 12 Monatssummen aus einer Kassenbuch-Abfrage für alle Kategorien, die letzte Änderung je Kategorie, den Berliner Tag). Der Client ordnet ein, auch nach einem Schreiben, mit derselben Funktion.

## Consequences
### Positive
- Keine neue Tabelle, nichts zu migrieren, nichts, was veralten kann.
- Die Budget-Seite und `BudgetCategory` bleiben unverändert, kein Fixture zieht mit.
- Eine Quelle für die Einordnung, vor und nach dem Schreiben.

### Negative
- Wer eine Kategorie bewusst so lässt, sieht sie beim nächsten Prüfen wieder in derselben Gruppe.
- Ein Target, das Roman direkt in YNAB geändert hat, wird gegen bis zu 12 Monate unter dem alten Target verglichen.
- Ein einmaliger großer Posten hebt den Ø. Die Hälfte-Regel hält ihn aus "mehr" heraus, in "weniger" kann er eine Kategorie fehlen lassen.
- Eine junge Kategorie hat Monate mit 0 aus der Zeit vor ihrer Anlage, ebenso jede Kategorie, wenn das Kassenbuch weniger als 12 Monate kennt.

### Neutral
- Ein Übernehmen bei MF sendet wie im Tab Target nur `goal_target`, die Form MF ist weiter nicht per Probe belegt (README, Open questions).
- Ob das Feld "Targets prüfen" der Leiste am Monatsersten eine Zahl zeigt und wann es erledigt ist, klärt budgeting-m3x7f.

## Alternatives considered
- **Feld `TargetCheck` an `BudgetCategory`, Hinweis an der Kategorie, `Target •`, gespeichertes "So lassen".** Verworfen (Roman): er prüft gelegentlich, ein dauerhafter Hinweis an 100 Targets wäre Lärm, und "So lassen" bräuchte eine Tabelle und eine eigene Regel für "spürbar anders".
- **Median statt Durchschnitt.** Verworfen: der Ø verteilt Jahresposten richtig auf den Monat, die Hälfte-Regel fängt den einmaligen Ausreißer in "mehr".
- **Den Monat der Änderung mitzählen.** Verworfen: Ausgaben vor der Änderung lagen unter dem alten Target.
- **Kaufmännisch auf 5 € runden.** Verworfen: ein Target bis 2,50 € unter dem Ø legt im Mittel zu wenig zurück.
- **Auf 5 € im Rhythmus runden.** Verworfen: bei jährlich wäre das grob ungenau gegenüber dem Monat, bei wöchentlich entstünde bis zu 21 € Abweichung im Monat und die Kategorie bliebe nach dem Übernehmen in "mehr" oder "weniger".
- **Server ordnet ein und liefert fertige Gruppen.** Verworfen: nach dem Schreiben müsste der Client dieselbe Regel kennen oder neu laden.

## References
- ADR 0065, 0072 (Punkt 3, Nachtrag 2026-11-05), 0075 (Punkt 1), 0080
- docs/ux/budget/konzept.md, Abschnitte "Die vier Abläufe" und "Targets"

## Nachtrag 2026-11-05 (ADR 0084)

- **Punkt 7:** Die letzte Target-Änderung kommt nach budgeting-hyn77 aus `events` (Altbestand-Typ
  mit geschriebenem Schritt 1), nach budgeting-eh8xe aus dem jüngsten `TargetSet` oder
  `TargetRemoved` im Stream `budgeting:target:<categoryId>`. Eine Änderung direkt in YNAB gibt
  es nach dem Einfrieren der Goals nicht mehr. `TargetTakenOverFromYnab` zählt nicht als
  Änderung: Die Übernahme hält einen bestehenden Stand fest, sonst begänne die Zählung für alle
  Kategorien am Tag der Übernahme neu.

## Nachtrag 2026-11-06 (budgeting-k7r2w)

- **Punkt 7, Zählbeginn erste Buchung:** Gezählt werden die vollen Monate ab dem späteren von
  zwei Monaten: dem Monat der ersten Buchung der Kategorie im Kassenbuch überhaupt
  (einschließlich, eigene Buchung oder Split-Zeile, ohne in YNAB gelöschte) und dem Monat nach
  der letzten Target-Änderung. YNAB liefert kein Anlegedatum einer Kategorie, die erste Buchung
  ist die einzige Quelle. Vorher zählten die Monate, bevor es die Kategorie gab, mit 0 € und
  drückten den Ø (Fall Strom: Ø 56 € statt rund 134 €). Liegt die erste Buchung vor dem
  Fenster, ändert sich nichts. Ohne jede Buchung bleibt es bei 12 Monaten mit 0 €.
- **Neue FitsNote:** Weniger gezählte Monate als nötig (3, bei P > 3 P) seit der ersten
  Buchung: "Passt" mit "neu seit Juli 2026" (Monat der ersten Buchung ausgeschrieben), in Teal
  wie "neu gesetzt am …".
- **Vorrang beim Zusatz:** Erklären die erste Buchung und eine Target-Änderung beide die zu
  wenigen Monate, nennt der Zusatz das spätere der beiden Ereignisse nach Berliner Tag. Bei
  gleichem Tag gilt die Änderung.
- **Balken:** Monate vor der ersten Buchung sind wie Monate vor einer Änderung dunkler
  (`NotCounted`). Die Zahl der Balken bleibt gleich.
