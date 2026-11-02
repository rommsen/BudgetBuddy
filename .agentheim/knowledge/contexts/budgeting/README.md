# budgeting

## Purpose
Das **Budget**: Geld auf Kategorien verteilen, zwischen Kategorien verschieben und Targets
anwenden, Monat für Monat, mit Vortrag in den Folgemonat. BB führt, YNAB wird nachgezogen
(ADR 0063). Später auch das, was YNAB nicht kann: erwartete Einnahmen, Leihen zwischen
Kategorien, Entwürfe, Targets mit Deckel.

Was hier **nicht** passiert: Buchungen halten oder nachbearbeiten (`ledger`), Kategorien
Buchungen zuordnen (`categorization`), YNAB-API-Verkehr (`ynab-sync`), Kategorien anlegen
oder umbenennen (bleibt in YNAB).

## Classification
**core**: das Budget ist die Fähigkeit, die am stärksten an YNAB bindet. Ohne sie bleibt
YNAB unverzichtbar.

## Actors
- **Roman**: verteilt am Monatsersten per Targets, verschiebt im Monat (oft am Handy),
  sucht bei größeren Anschaffungen nach Puffer.
- **Frau**: budgetiert praktisch nicht.
- **YNAB**: über `ynab-sync`: Quelle der Targets und des heutigen Budget-Zustands, Ziel
  der Zuweisungen.
- **Kassenbuch** (`ledger`): liefert die Aktivität pro Kategorie und Monat (auf der Budget-Seite vorerst von YNAB, bis die Naht steht).

## Ubiquitous language
- **Budget**: das Ganze, alle Kategorien mit ihren Beträgen über die Monate. YNAB nennt es
  inzwischen "Plan"; BB bleibt bei "Budget".
- **Kategorie**: der einzelne Topf. Dieselbe Kategorie, an der im Kassenbuch Buchungen
  hängen. Ein einzelner Topf heißt nie "Budget".
- **Kategoriegruppe**: Gruppierung von Kategorien zur Anzeige.
- **Monat**: die Einheit, in der zugewiesen wird. Jede Kategorie hat pro Monat Zugewiesen,
  Aktivität und Verfügbar. Client und Server tauschen ihn als "YYYY-MM-01" aus. Offen sind die Monate im Monatsfenster, ein 404 von YNAB heißt "Monat nicht offen", beim Lesen wie beim Schreiben (ADR 0074).
- **Zu verteilen**: Geld, das da ist, aber noch keiner Kategorie gehört (YNABs "Ready to
  Assign"). Darf bewusst negativ werden.
- **Zuweisen**: Geld aus "Zu verteilen" einer Kategorie für einen Monat geben, oder es von dort zurücknehmen. Eine Budget-Bewegung mit Herkunft "Zuweisen" (ADR 0069). Das Feld Zugewiesen setzt das Zugewiesen absolut (budgeting-c5w2n, ADR 0078), der Fehlbetrag-Knopf und Zu verteilen als Gegenseite bleiben relativ über Verschieben.
- **Verschieben**: Geld von einer Kategorie auf eine andere legen, im selben Monat (budgeting-afd8h, ADR 0069). Zwei Budget-Bewegungen mit Herkunft "Verschieben" und gemeinsamer Gruppen-Id: die Quelle sinkt, das Ziel steigt. Relativ zum frisch gelesenen Monat, ohne Vorschau. Die Quelle darf negativ werden, auch ihr Zugewiesen (Kategorie lebt vom Vortrag). Erst die Quelle, dann das Ziel: scheitert die Quelle, geht das Ziel nicht hinaus, sonst käme das Geld still aus Zu verteilen. Das gilt auch beim erneuten Senden, maßgeblich ist der Stand der Vorgänger-Zeile, auch aus einem früheren Lauf; eine Zeile ohne Stand zählt wie fehlgeschlagen. Gewinnt YNAB bei der Quelle, wird das Ziel ungesendet ebenfalls als in YNAB geändert geschlossen. Beide Stände werden vor dem ersten Senden geprüft: ist das Ziel schon verloren, geht die Quelle nicht hinaus und schließt mit (ADR 0070, Nachtrag). Der Monat kommt vom Client (der gezeigte Monat), nicht von der Uhr des Servers. Damit kann eine Zeile "in YNAB geändert" heißen, die YNAB nie berührt hat, und das Geld bleibt in Zu verteilen, bis es von Hand verschoben wird.
- **Aktivität**: Summe der Buchungen einer Kategorie in einem Monat (aus dem Kassenbuch). Auf der Budget-Seite kommt sie vorerst von YNAB (`activity`), bis die Naht ledger/budgeting steht (ADR 0065).
- **Verfügbar**: Vortrag + Zugewiesen + Aktivität.
- **Vortrag**: Verfügbares, das am Monatsende in der Kategorie bleibt und im Folgemonat
  weiterzählt. Nicht zu verwechseln mit dem **Übertrag** im Kassenbuch (zwei Buchungen,
  eine Bewegung zwischen Konten).
- **Target**: Romans Vorgabe pro Kategorie. Arten: *pro Woche ein Betrag*, *jeden Monat
  ein fester Betrag*, *bis zu einem Datum ansparen*, *auffüllen bis* (ungenutzt, weil es
  am Monatswechsel falsch rechnet). Gewünscht: *mit Deckel*. Die Budget-Seite und die Vorschau zeigen Betrag und Rhythmus so, wie YNAB sie liefert (`goal_target`, Kadenz; budgeting-kjsht), BB rechnet nicht damit (ADR 0065). Ohne Kadenz steht nur der Betrag, ältere Monatsziele (`MF`) gelten als monatlich. Bei wöchentlichen Targets kann "Target 50,00 €/Woche · fehlt 250,00 €" stimmen, weil der Fehlbetrag die Wochen des Monats zählt. Die Art des Targets wird nicht angezeigt. Eine unbekannte YNAB-Kadenz zählt als Target ohne Rhythmus.
- **Targets anwenden**: alle Kategorien mit einem Knopf auf das bringen, was ihre Targets
  im Monat verlangen. YNABs "Underfunded" ohne Auswählen. Kein Zeitplan.
- **Budget-Bewegung** (budgeting-h2zfn, budgeting-afd8h): Zuweisung, Verschiebung oder Target-Anwendung, mit Zeitpunkt und Herkunft. Die **Budget-Historie** ist ihre Folge, allgemein und pro Kategorie. Sie trägt Zeitpunkt, Herkunft ("Targets anwenden", "Verschieben" oder "Zuweisen"), Monat, Kategorie, altes und neues Zugewiesen. Die **Gruppen-Id** (Spalte application_id) fasst die Bewegungen eines Durchlaufs zusammen, also einer Anwendung oder einer Verschiebung. Eine Bewegung ist nach dem Festhalten unveränderlich (Tabelle budget_movements); ihr Stand ist das jüngste angehängte Ergebnis (budget_movement_results). Eine Historie-Ansicht gibt es noch nicht.
- **Erwartete Einnahme**: Ankündigung einer sicheren Einnahme (Betrag, Datum, Erkennung).
  Macht ein negatives Zu verteilen gelb statt rot und wird beim Import der echten Buchung
  eingelöst. Keine Buchung.
- **Leihe**: Geld, das aus einer Kategorie genommen wurde und zurück soll. Bleibt offen,
  bis es zurückgezahlt ist.
- **Entwurf**: eine Reihe von Verschiebungen, die noch nicht gilt. Zeigt die Summe, wird
  übernommen oder verworfen.
- **Budget-Seite** (budgeting-k7wmd, neu aufgebaut in budgeting-g8s3w, dichter in budgeting-h3k9d): die Seite #/budget mit zwei Ebenen. Die **Liste** zeigt die Kategorien nach Kategoriegruppe in Romans Reihenfolge (ADR 0074 Punkt 6) mit Target, Zugewiesen, Aktivität und Verfügbar. Eine Zeile wählt die Kategorie, alle Aktionen liegen im Inspektor, ein Tipp auf Verfügbar oder den Namen öffnet nichts Eigenes mehr. Die Seite läuft am PC über die volle Breite, der Zuwachs geht an die Spalte Kategorie, eine Zeile misst höchstens 40 px. Darüber steht am PC der Kopf als sticky Zeile: `‹ Monat ›`, Suche, Filter, Zu verteilen, Targets anwenden, ab 1024 px in einer Zeile, darunter bis 768 px in zweien. Einen sichtbaren Titel gibt es nicht, die Navigation nennt die Seite. Am Handy haften Monat mit Zu verteilen und die Chips, die Suche scrollt mit, Targets anwenden steht in ihrer Zeile. Der Monatsname führt zum laufenden Monat und ist teal, sobald ein anderer gezeigt wird. Am Fensterrand fehlt der Pfeil, sein Platz bleibt frei. Die Monatsart steuert, was geht: in einem vergangenen Monat ist Targets anwenden unsichtbar und sein Platz bleibt frei, die API-Grenze lehnt ihn ohne YNAB-Anfrage ab, Zuweisen, Holen, Abgeben und Buchungen gehen in jedem Monat. Kategorien mit Target zeigen es mit Rhythmus, dazu pausiert, Fehlt erscheint in Gelb: am Handy in der dritten Zeile, die dann auch ohne Target erscheint, am PC klein hinter dem Namen. "erfüllt" steht nur im Tab Target. Rot und Gelb dürfen in einer Zeile zusammenstehen (ADR 0075). Filter Alle, Fehlt und Überzogen stehen neben der Suche, gelten zusammen mit ihr und bleiben beim Monatswechsel stehen, ihre Zahlen zählen den ganzen Monat, unabhängig von der Suche. Überzogen heißt Verfügbar im Minus und ist eine Teilmenge von Fehlt, die Farbe heißt weiter Rot. Liest die Beträge live aus YNAB und speichert sie nicht. Geschrieben wird nur über Targets anwenden (budgeting-h2zfn), Verschieben (budgeting-afd8h) und Zuweisen (budgeting-c5w2n): die Zuweisungen nach YNAB und die Budget-Bewegungen in BB. Negatives Zu verteilen erscheint negativ, ohne Hinweis und ohne Sperre. Ausgeblendete, gelöschte und interne Kategorien (Inflow) fehlen. Das Suchfeld über der Liste (budgeting-rxxb4), am Handy und am PC immer sichtbar, filtert die Kategorien: Teilstring im Kategorie-Namen oder im Gruppennamen, Groß- und Kleinschreibung und Leerzeichen außen egal, wie im Kategorie-Picker. Ein Gruppen-Treffer zeigt die ganze Gruppe, Gruppen ohne Treffer fallen weg, ohne Treffer steht „Keine Kategorie passt zu …“, ohne Suche „Keine Kategorie in dieser Ansicht.“. Suche und Filter sind beim Betreten der Seite zurückgesetzt, außer bei der Rückkehr aus dem Kassenbuch. Der laufende Monat ist der Monat des Berliner Kalendertags, unabhängig von der Zeitzone des Servers.
- **Fehlbetrag** (budgeting-h2zfn): was einer Kategorie in einem Monat noch zugewiesen werden muss, damit ihr Target erfüllt ist (YNAB `goal_under_funded`). BB übernimmt ihn von YNAB und rechnet Targets in Stufe 1 nicht selbst. Ohne Target gibt es keinen Fehlbetrag, 0 heißt erfüllt. Er gehört zum Target und ist die Grundlage von Targets anwenden: Zugewiesen wird altes Zugewiesen plus Fehlbetrag; die Probe gegen das Budget Test bestätigt, dass der Fehlbetrag danach 0 ist. BBs Antwort je Kategorie ist **Fehlt**.
- **Pausiert** (budgeting-k7wmd): ein Target, das YNAB für den Monat ausgesetzt hat (`goal_snoozed_at` gesetzt). Sein Fehlbetrag zählt dann nicht.
- **Vorschau** (budgeting-h2zfn): der erste Schritt von Targets anwenden. Zeigt je Kategorie mit Fehlbetrag größer 0 und ohne pausiertes Target den neuen Betrag, darunter Target, altes Zugewiesen und Fehlbetrag (budgeting-kjsht), die Summe, Zu verteilen danach (darf negativ sein, ohne Warnung und Sperre) und die übersprungenen pausierten Kategorien. Schreibt weder nach YNAB noch in die Datenbank. **Übernehmen** schreibt sie nach YNAB; vorher liest der Server den Monat neu und lehnt ab, wenn er von der Vorschau abweicht ("Stand hat sich geändert", nichts geschrieben, neue Vorschau). Ein geändertes Target allein zählt dabei nicht als Abweichung.
- **Anwendung** (budgeting-h2zfn): ein Durchlauf von Targets anwenden für einen Monat, mit je einer Zeile pro Kategorie (altes und neues Zugewiesen). Eine Zeile ist geschrieben, fehlgeschlagen (mit Grund, bei 429 "Rate-Limit", erneut sendbar) oder in YNAB geändert (YNAB stand weder auf dem alten noch auf dem Zielwert, YNAB gewinnt, nichts überschrieben, ADR 0055). Zwei Anwendungen für denselben Monat laufen nicht ineinander. Erneut senden geht auch hinaus, wenn der Monat inzwischen ein vergangener ist: die Anwendung war schon freigegeben, die Sperre für vergangene Monate gilt nur für neue Anwendungen.
- **Gegenseite** (budgeting-afd8h, budgeting-c5w2n): die zweite Seite von Holen und Abgeben, eine Kategorie oder Zu verteilen. Gehört zum Modus, die Richtung ist die Wahl von Holen oder Abgeben. Gewählt im Kategorie-Picker, der "Zu verteilen" mit Betrag als ersten Eintrag zeigt, darunter Zuletzt verwendet, und die gewählte Kategorie weglässt. Sie ist nie vorbelegt.
- **Herkunft** (budgeting-nfrds): woher eine Budget-Bewegung stammt: Targets anwenden, Verschieben oder Zuweisen (`MovementOrigin`). In der Datenbank stehen diese Wörter. Nur bei Verschieben hängen die Zeilen aneinander (Quelle vor Ziel), bei den anderen steht jede Zeile für sich.
- **Offene Verschiebung** (budgeting-nfrds): eine Verschiebung mit fehlgeschlagener Zeile. Die Budget-Seite zeigt sie als Hinweis mit "Erneut senden", bis nichts mehr offen ist, bei offenem Inspektor-Sheet im festen Bereich zwischen Kopf und Inhalt, sonst auf der Seite; der Toast läuft ab, der Hinweis nicht. Er lebt im Client und ist nach einem Neuladen der App weg. Der Hinweis nennt den Monat der Verschiebung, weil er auch bei einem anderen gewählten Monat stehen bleibt. Eine fehlgeschlagene Zuweisung und ein fehlgeschlagener Fehlbetrag-Knopf stehen im selben Hinweis, mit "Nicht zugewiesen" statt "Nicht vollständig verschoben" (budgeting-c5w2n). Der Hinweis bleibt bei Tab-, Kategorie-, Monats- und Moduswechsel stehen.
- **Kategorie-Detail** (budgeting-m1bce): geht im Tab Buchungen des Inspektors auf (budgeting-g8s3w). Dort stehen die **Buchungen des Monats** aus dem Kassenbuch: alle Buchungen der Kategorie über alle Konten, neueste zuerst, ohne in YNAB gelöschte; bei einem Split nur der Anteil dieser Kategorie (Zeile mit "Split"). Ein Tipp auf eine Buchung öffnet sie im Kassenbuch (`#/ledger/entry/<id>`), Zurück oder Schließen führen wieder in denselben Tab. Holen und Abgeben stehen im Tab Geld.
- **Abweichung Kassenbuch/YNAB** (budgeting-m1bce): die Summe der Buchungen im Kategorie-Detail gegen YNABs Aktivität. Weichen sie ab, steht unter der Summe "Kassenbuch: x, YNAB: y", ohne Bewertung. BB rechnet Verfügbar nicht selbst (ADR 0065); der Grund einer Abweichung wird in budgeting-bk6tq geklärt.
- **Fehlt** (budgeting-g8s3w): BBs Antwort je Kategorie, was ihr fehlt, auch ohne Target. Das Größere aus negativem Verfügbar und Fehlbetrag, ohne Betrag über 0 fehlt nichts. Bei einem pausierten Target zählt der Fehlbetrag nicht, negatives Verfügbar bleibt. Erscheint in Gelb: offen, kein Fehler (ADR 0075).
- **Monatsfenster** (budgeting-g8s3w, ADR 0074): die Monate, die YNAB offen hat, aus `GET /months`. BB blättert genau über diese Monate und rechnet die Grenzen nicht selbst. Eine echte Zuweisung ungleich 0 im letzten Monat öffnet den nächsten, BB schiebt das Fenster nie von sich aus. Liegt der laufende Monat außerhalb, zeigt die Seite "Monat nicht offen", die Pfeile bleiben bedienbar.
- **Monatsart** (budgeting-g8s3w, ADR 0074): vergangen, aktuell oder künftig, aus dem Vergleich mit dem laufenden Monat. In vergangenen Monaten gehen Targets anwenden, Target ändern und Pausieren nicht, alles andere geht in jedem Monat.
- **Inspektor** (budgeting-g8s3w): alles zu einer gewählten Kategorie, ab 1024 px als Panel rechts neben der Liste, darunter als BottomSheet, beide mit fester Höhe. Tabs Geld, Buchungen, Historie und Target. Der Tab bleibt bei Kategorie- und Monatswechsel stehen. `˄ ˅` im Kopf des Sheets neben Schließen wechseln die Kategorie ohne Schließen, am Rand fehlt der Pfeil und sein Platz bleibt frei. Ab 1024 px gelten die Pfeiltasten seitenweit, außer in offenen Dialogen: Hoch und Runter wechseln zwischen den sichtbaren Zeilen, auch im Suchfeld, ohne Auswahl wählt Runter die erste und Hoch die letzte Zeile. Links und Rechts wechseln die Tabs, am Rand bleibt es stehen, im Suchfeld gehören sie dem Cursor, ohne Auswahl tun sie nichts. Ein Eingabefeld im Inspektor sperrt alle vier Richtungen (`pageKeyAction`). Eine Kategorie, die aus Filter oder Suche fällt, bleibt gewählt. Fehlt sie im neuen Monat, endet die Auswahl.
- **Monatswechsel** (budgeting-p4n6r): Blättern lässt Liste, Suche, Filter, Zu verteilen und Inspektor des alten Monats gedimmt stehen, bis die Antwort kommt, ohne Spinner und ohne Sprung. Nur der Monatsname zeigt sofort das Ziel. Der Zustand ist abgeleitet (`monthChanging`: `Month` und `shownMonth` weichen voneinander ab), solange gehen weder Targets noch Holen, Abgeben oder der Fehlbetrag-Knopf in den alten Monat, und das Feld Zugewiesen nimmt keine Eingabe an. Betreten der Seite beginnt beim laufenden Monat, nur die Rückkehr aus dem Kassenbuch behält den gewählten.
- **Modus** (budgeting-c5w2n): die drei Ansichten des Tabs Geld, Zuweisen, Holen und Abgeben. Standard Zuweisen, eine rote Kategorie startet auf Holen mit dem Minus als Betrag. Der Modus beginnt je Kategorie und Monat neu.
- **Holen** und **Abgeben** (budgeting-c5w2n): Verschieben aus Sicht der gewählten Kategorie. Holen: Geld kommt von der Gegenseite, Abgeben: es geht an sie.
- **Zuweisen-Eingabe** (budgeting-c5w2n): der noch nicht gespeicherte Wert im Feld Zugewiesen, mit Kategorie und Monat. Nicht zu verwechseln mit **Entwurf**.
- **Verlassen** (budgeting-c5w2n): Enter, Fokus weg, Tab-, Kategorie-, Monats- oder Moduswechsel, Schließen, Seitenwechsel. Speichert die Zuweisen-Eingabe, Esc verwirft sie. Der Wechsel in ein anderes Programm zählt nicht.
- **Betragsrechnung** (budgeting-c5w2n): Regeln des Felds, `+20` relativ zum Stand, `900+40` ersetzt ihn, Punkt vor Strich, auf den Cent.
- **Frei** (budgeting-c5w2n): Verfügbar minus offener Fehlbetrag, pausierte Targets sperren nichts. Grundlage der Warnung beim Überziehen.
- **Zuletzt verwendet** (budgeting-c5w2n): Kategorien aus den Bewegungen mit Herkunft Verschieben, neueste zuerst.

## Aggregates
- **Anwendung** (budgeting-h2zfn): Monat und Zeilen (Bewegung je Kategorie). Invarianten: nur Kategorien mit Fehlbetrag größer 0 und nicht pausiert, Beträge auf 10 Milliunits (YNAB rundet sonst still), neu gleich alt plus Fehlbetrag. Die Bewegungen werden vor dem ersten Push festgehalten und danach nie geändert; Ergebnisse werden nur angehängt.
- **Verschiebung** (budgeting-afd8h): Monat und eine oder zwei Zeilen (Bewegungen), gemeinsame Gruppen-Id, Sendereihenfolge Quelle vor Ziel. Invarianten: Betrag größer 0 und auf den Cent genau, Quelle ungleich Ziel, beide Kategorien sichtbar im Monat, neu gleich alt ∓ Betrag, Beträge auf 10 Milliunits. Negatives neues Zugewiesen ist gültig.
- **Zuweisung** (budgeting-c5w2n, ADR 0078): Monat und eine Zeile. Invarianten: Kategorie sichtbar im Monat, Betrag auf 10 Milliunits, negatives Zugewiesen gültig, keine Zeile bei gleichem Wert.

## Key events
- **TargetsAngewendet**, **ZuweisungNachYnabGeschrieben**, **ZuweisungRückschreibenFehlgeschlagen** (budgeting-h2zfn): in Stufe 1 als Zeilen in budget_movements (eine je Kategorie) und budget_movement_results (geschrieben, fehlgeschlagen mit Grund, in YNAB geändert), getrennt vom Kassenbuch-Ereignis-Log (ADR 0065). Ebenso **GeldVerschoben**, **GeldZugewiesen** (budgeting-afd8h): wie bei Targets anwenden als Zeilen in budget_movements und budget_movement_results, Herkunft "Verschieben" bzw. "Zuweisen". **GeldZugewiesen** entsteht nun auch beim Befehl Zuweisen (budgeting-c5w2n).

## Key commands
- **TargetsÜbernehmen** und **FehlgeschlageneErneutSenden** (budgeting-h2zfn): Befehle auf der Anwendung. Die Vorschau ist eine Abfrage ohne Schreiben. Die Befehle laufen unter einer Sperre je Monat, senden nacheinander einen PATCH je Kategorie und brechen bei 429 ab. Ebenso **Verschieben** (budgeting-afd8h, ADR 0069): Befehl mit Monat, Quelle, Ziel und Betrag, ohne Vorschau. Liest den Monat frisch unter der Sperre je Monat, hält die Bewegungen fest und sendet nacheinander, erst Quelle, dann Ziel. Fehlgeschlagene gehen über **FehlgeschlageneErneutSenden** hinaus. Ebenso **Zuweisen** (budgeting-c5w2n, ADR 0078): Befehl mit Monat, Kategorie und neuem Zugewiesen. Liest den Monat frisch unter der Sperre je Monat, der gelesene Wert ist der alte, der eingegebene gilt. Steht YNAB schon auf dem Wert, entsteht nichts.

## Relationships with other contexts
- **Customer of:** `ledger` (Aktivität pro Kategorie und Monat; Sprung vom
  Kategorie-Detail zur Buchung).
- **Partnership with:** `ynab-sync` (Targets und Budget-Zustand aus YNAB holen,
  Zuweisungen nach YNAB schreiben).
- **Customer of:** `banking-import` (künftig: Einlösen erwarteter Einnahmen beim Import).
- **Customer of:** `design-system`. **Styleguide-Gate:** jede UI-Task in diesem Context
  hängt an `design-system-001` (`depends_on`), gemessen an `standards/frontend/styleguide.md`.
- **Lese-Naht zu `ledger`** (budgeting-m1bce, konkret zum Customer-Eintrag oben): `LedgerApi.getCategoryMonthEntries` liefert für Kategorie und Monat die Buchungen bzw. Split-Anteile aus dem Kassenbuch, ohne YNAB-Anfrage. Der Sprung zur Buchung läuft über die Route `#/ledger/entry/<id>`. Die Aktivität auf der Budget-Seite bleibt YNABs Wert (ADR 0065).

## Szenarien und Tests

- **Bug-Tasks:** mindestens ein „kaputt“- und ein „intakt“-Szenario als Akzeptanzkriterium (GIVEN/WHEN/THEN). Der Regressionstest zum kaputt-Szenario ist vor dem Fix rot.
- **Fachliches Verhalten** (Zustandswechsel, Grenzen, Berechtigungen, was an den Client geht) steht in den ACs als Szenario; jedes Szenario hat einen Test. Einfache Fakten-ACs (Datei existiert, Zahl im Outcome) brauchen keins.
- **Maßstab für Tests:** `.claude/rules/test-doctrine.md` (Heimat-Ebene, Pflichtliste, kein Test für Tautologien).

## Test command

`./verify.sh` im Repo-Wurzelverzeichnis. Läuft dotnet build, dotnet test, npm run build, guard:classes, guard:tokens und test:scripts; grün nur bei der Schlusszeile `VERIFY: PASS` und Exit 0. Bei `VERIFY: FAIL` stehen die roten Schritte und die Namen der roten Tests am Ende der Ausgabe.

## Open questions
- ~~Liefert die YNAB-API die Targets lesbar und erlaubt sie das Setzen des zugewiesenen
  Betrags pro Kategorie und Monat?~~ Gelöst (`budgeting-r4tkq`): ja, Fehlbetrag inklusive;
  siehe `research/ynab-api-targets-zuweisung-2026-10-25.md`.
- Rechnet BB Verfügbar und Zu verteilen selbst oder übernimmt es sie von YNAB und gleicht
  ab? (open since 2026-10-25)
- Wer führt die Targets, und wie läuft der Wechsel von YNAB zu BB? Für die Spiegel-Phase gelöst (budgeting-v4ht9, ADR 0072): YNAB führt, BB bearbeitet und zieht nach. Offen bleibt der Wechsel zu BB als führendem System. (open since 2026-10-25)
- ~~Enthält `to_be_budgeted` des nächsten Monats den Rest des laufenden Monats?~~ Gelöst (`budgeting-k7wmd`, lesende Probe am 2026-10-26 gegen Romans Budget): ja. Oktober hatte Zu verteilen 35,45 €, der November (Einnahmen 0, Zugewiesen 0, Aktivität 0) ebenfalls 35,45 €. Der Rest wandert also in den Folgemonat: Zu verteilen ist ein laufender Stand, der von Monat zu Monat weiterzählt. Zwei Monate voraus (Dezember) liefert YNAB 404; der nächste Monat ist die Grenze der Seite.
- ~~Bringt budgeted plus goal_under_funded eine Kategorie auf Kurs?~~ Gelöst (`budgeting-h2zfn`, Probe am 2026-10-26 gegen das Budget Test): ja. Groceries (NEED, wöchentlich, Target 100,00 €): Fehlbetrag 500,00 €; nach teilweisem Zuweisen von 30,00 € noch 470,00 €; nach budgeted gleich 500.000 Milliunits (alt plus Fehlbetrag) Fehlbetrag 0 und Zu verteilen um genau diese Differenz gesunken. Ein zweites Senden desselben Werts ändert nichts. Nicht prüfbar über die API: pausierte Targets (goal_snoozed_at ist nicht setzbar) und Targets mit Deckel.
- ~~Liefert YNAB die Anzeigereihenfolge der Kategorien?~~ Gelöst (`budgeting-q7m2c`, Probe am 2026-11-02, von Roman bestätigt): `GET /months/{month}` ist ungeordnet und je Monat anders, `GET /categories` ist stabil und wirkt von Hand sortiert. Roman hat bestätigt, dass sie seiner YNAB-Anzeige entspricht. Siehe `research/ynab-reihenfolge-monate-target-schreiben-2026-11-02.md`.
- ~~Wie weit reichen die Monate?~~ Gelöst (`budgeting-q7m2c`, Probe am 2026-11-02): Nach hinten ist `first_month` die Grenze, nach vorn endet das Fenster einen Monat hinter der letzten Zuweisung, `GET /months` zeigt es genau. Das ersetzt die Aussage aus `budgeting-k7wmd`, der nächste Monat sei die Grenze. Eine Zuweisung im letzten Monat öffnet den nächsten, auch nach Rücksetzen auf 0; jenseits davon geben GET und PATCH 404.
- ~~Bleiben Tag und Rhythmus erhalten, wenn BB ein Target ändert?~~ Weitgehend gelöst (`budgeting-q7m2c`, Probe am 2026-11-02 gegen das Budget Test): Nur `goal_target` senden erhält Tag, Rhythmus, Datum und `goal_needs_whole_amount`. `goal_frequency` ersetzt das Target: Das Datum geht immer verloren, der Tag beim Wechsel der Art. Jährlich mit Datum geht in zwei Aufrufen. Nicht belegt, weil über die API nicht herstellbar: monatlich mit `goal_day`, alle 3 Monate mit Datum, MF. (open since 2026-11-02)
