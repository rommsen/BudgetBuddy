# Kassenbuch als Arbeitsfläche: Briefs für den klickbaren Prototyp

Task `ledger-xbrht`. Die Screens entstehen in claude.ai/design in einem eigenen
Mockup-Projekt mit dem synchronisierten "BudgetBuddy Design System". Je Ablauf ein Brief:
Ziel, Gerät und Viewport, Ausgangszustand, Schrittfolge, Pflicht-Komponenten aus dem
Design-System, der YNAB-Referenzweg (Romans heutige Nutzung, der Maßstab für "mindestens
so gut") und was nur BB zeigt.

Alle Konten, Payees, Kategorien und Beträge sind erfunden.

## Gemeinsame Grundlage (Konzept-Skizze, mit Roman abgestimmt am 2026-09-11)

- **Geräte:** Handy (Viewport 390 × 844) und PC (Viewport 1440 × 900) sind gleichwertig.
  Beide müssen sehr gut bedienbar sein. Handy-Hauptfall ist Durchsehen und Prüfen, am PC
  liegt der Schwerpunkt auf Nachbearbeiten. Filter und Summen gehören auf beide Geräte.
- **Einstieg:** das Kassenbuch öffnet im zuletzt gewählten Konto (Entscheidung Roman).
  Gesamtsicht über den Konto-Wechsler erreichbar.
- **Aufbau (beide Geräte):** oben Kontostände (Handy: waagerecht scrollbare Leiste; PC:
  linke Spalte mit Konten und Ständen, "Alle Konten" zuoberst), darunter die sticky
  Filterleiste (Chips), darunter die Liste. Erster Chip ist immer "Zu prüfen (n)".
- **Konto ist ein Filter.** Konto-Wechsel lässt andere Filter und eine laufende Auswahl
  stehen. In der Gesamtsicht zeigt jede Zeile ein Konto-Kürzel (Handy) oder eine
  Konto-Spalte (PC).
- **Liste Handy:** Datum als Gruppenkopf; je Zeile Payee, Kategorie, Betrag (Money,
  tabular); darunter klein Memo und nur abweichende Kennzeichen ("noch nicht in YNAB",
  "Rückfrage Lena", "in YNAB gelöscht"). Wisch nach rechts = geprüft, ein einziger Wisch
  (Entscheidung Roman). Tipp auf die Zeile öffnet das Detail als BottomSheet.
- **Liste PC:** Tabelle mit Sticky-Kopf: Auswahl-Kästchen, Datum, Konto (nur
  Gesamtsicht), Payee, Kategorie, Memo, Betrag, Status. Klick auf die Zeile öffnet ein
  Seitenpanel rechts (Entscheidung Roman), die Liste bleibt sichtbar. Klick auf die
  Kategorie-Zelle öffnet den Kategorie-Picker direkt. Tastatur: Pfeile bewegen, Leertaste
  wählt aus, G markiert geprüft.
- **Filter:** Chips "Zu prüfen", "Kategorie", "Zeitraum", "Payee", "Betrag", "Memo",
  "Flag", "Gelöschte zeigen". Kategorie öffnet die Kategorienauswahl mit Mehrfachauswahl,
  versteckte Kategorien bleiben eingeklappt. Sobald ein Filter aktiv ist, steht über der
  Liste eine Summenzeile: Trefferzahl und Gesamtbetrag über den gesamten Treffer (nicht
  nur den geladenen Teil).
- **Auswahl-Modus:** langes Drücken (Handy), Kästchen oder Shift-Klick (PC). Unten eine
  Auswahl-Leiste: Anzahl, Summe der Auswahl, Aktionen "Geprüft", "Kategorie ändern",
  "Flag", "Auswahl aufheben". Solange eine Auswahl aktiv ist, zeigt die Summenzeile die
  Auswahl statt des Filters.
- **Detail (Sheet am Handy, Seitenpanel am PC):** Kopf mit Betrag, Payee, Konto, Datum.
  Felder Kategorie (Picker), Split als Zeilenliste mit Restbetrag (Split-Bearbeitung nur
  hier, Entscheidung Roman), Memo, Flag (Lena, temporär), Geprüft-Schalter. Darunter
  Herkunft und Historie als Zeitleiste. "Noch nicht in YNAB" als Hinweis mit Grund.
- **Gelöschte Buchungen** sind standardmäßig ausgeblendet (`ledger-p3n8k`) und kommen
  über den Chip "Gelöschte zeigen" zurück, ausgegraut mit Kennzeichen.
- **Große Listen (rund 14.000 Buchungen):** seitenweise vom Server, 100 je Seite, Nachladen
  beim Scrollen. Treffer, Anzahl und Summe rechnet der Server über den gesamten Treffer.
  Kontostände kommen aus dem Zustand, nie aus der geladenen Liste. Filterwechsel springt
  nach oben, Chips bleiben sichtbar. Payee-, Memo- und Betragssuche mit kurzer Verzögerung.

**Erfundene Daten für alle Screens**

| Konto | Art | Stand |
|---|---|---|
| Girokonto Comdirect | Bank | 2.418,37 € |
| ING Extra | Bank | 6.902,10 € |
| Bar | Bar | 63,50 € |
| Geldmarkt-ETF | Wertpapierkonto mit Budget-Geld | 12.004,88 € |

Payees: REWE Mitte, Bäckerei Krause, Stadtwerke Musterstadt, Deutsche Bahn, Amazon
Marketplace, Apotheke am Markt, Kita-Beitrag Stadt, Netflix, Bauhaus, Café Lotte.
Kategorien (Gruppen): Alltag (Lebensmittel, Haushalt, Restaurants), Wohnen (Nebenkosten,
Möbel), Mobilität (Bahn, Auto), Familie (Kinder, Geschenke), Abos (Streaming),
Gesundheit (Apotheke). Versteckt: "Umzug 2023".

**Pflicht-Komponenten aus dem Design-System** (Karten im Projekt "BudgetBuddy Design
System"): Navigation (Desktop und Mobile), Card, Money, Badge, Table, BottomSheet,
Kategorienauswahl (`components-categorypicker`), Swipe, Button, Stats, Input, Toast,
Icons, PageHeader.

---

## Ablauf 1: Durchsehen und als geprüft markieren

**Ziel:** Roman geht neue Buchungen durch und markiert jede als geprüft. Ein Wisch je
Buchung, keine zweite Geste, kein Umweg über ein Konto.

**Gerät:** Handy (Hauptgerät, 390 × 844); zusätzlich PC (1440 × 900) als zweite Größe.

**YNAB-Referenzweg (Roman, 2026-09-11):** Die YNAB-App zeigt "13 Transactions to Review".
Dort geht nur "approve" per Wisch nach rechts; ein zweiter Wisch macht es wieder
rückgängig. Deshalb geht Roman in das Konto, lässt sich alle Buchungen zeigen, wischt
einmal (approved) und noch einmal (cleared). Zwei Gesten je Buchung, an zwei Orten
unterschiedlich belegt. "Super nervig und inkonsistent."

**Ausgangszustand:** Kassenbuch öffnet im Konto "ING Extra" (zuletzt gewählt). Chip
"Zu prüfen (7)" ist aktiv. Die Liste zeigt sieben ungeprüfte Buchungen aus dem
YNAB-Spiegel, gruppiert nach Datum:

| Datum | Payee | Kategorie | Betrag |
|---|---|---|---|
| Heute | REWE Mitte | Lebensmittel | -48,12 € |
| Heute | Bäckerei Krause | Lebensmittel | -6,40 € |
| Gestern | Deutsche Bahn | Bahn | -29,90 € |
| Gestern | Netflix | Streaming | -13,99 € |
| 08.09. | Stadtwerke Musterstadt | Nebenkosten | -142,00 € |
| 08.09. | Amazon Marketplace | (keine Kategorie) | -37,45 € |
| 07.09. | Apotheke am Markt | Apotheke | -18,20 € |

**Schrittfolge (Handy):**
1. Roman öffnet das Kassenbuch. Sichtbar: Kontostände-Leiste, Chip "Zu prüfen (7)"
   aktiv, Summenzeile "7 Buchungen · -296,06 €", Liste.
2. Wisch nach rechts auf "REWE Mitte": Zeile färbt sich kurz (Swipe-Aktion "Geprüft",
   Häkchen-Icon), verschwindet aus der Liste, Chip zählt auf "6", Summenzeile aktualisiert.
   Toast unten: "Geprüft · Rückgängig".
3. Wisch auf "Amazon Marketplace" (ohne Kategorie): gleiche Geste, gleiche Wirkung. Die
   fehlende Kategorie hindert nicht am Prüfen (Kategorie ist Ablauf 4).
4. Roman tippt auf "Deutsche Bahn": BottomSheet mit Detail (Kopf, Kategorie Bahn, Memo,
   Geprüft-Schalter aus, Herkunft "YNAB-Spiegel", Historie mit zwei Zeilen). Schalter
   "Geprüft" an, Sheet schließen: Zeile ist weg.
5. Chip "Zu prüfen" abwählen: die Liste zeigt wieder alle Buchungen des Kontos, die drei
   geprüften ohne Kennzeichen, die vier ungeprüften mit einem kleinen Punkt am linken
   Rand.

**Schrittfolge (PC):**
1. Gleicher Ausgangszustand als Tabelle. Zeile "REWE Mitte" markieren (Pfeil runter),
   Taste G: Status-Spalte wechselt auf geprüft, Zeile verlässt die gefilterte Liste.
2. Kästchen bei drei Zeilen setzen, Auswahl-Leiste unten "3 ausgewählt · -186,10 €",
   Aktion "Geprüft": alle drei weg.

**Pflicht-Komponenten:** Navigation (mobileNav, desktopNav), Money, Badge, Swipe
(SwipeableRow), BottomSheet, Toast, Table (PC), Stats oder eine Summenzeile mit Money.

**Was nur BB zeigt:** Herkunft "YNAB-Spiegel" und die Historie im Detail. Eine Buchung
ohne YNAB-Verknüpfung trüge zusätzlich "noch nicht in YNAB" (hier keine).

**Offen für den Prototyp:** Wisch nach links bleibt frei (kein Löschen, kein Flag), damit
die Geste eindeutig bleibt; später denkbar für "Rückfrage Lena". Roman entscheidet beim
Durchgang.

---

## Ablauf 2: Buchungen finden

**Ziel:** Roman findet Buchungen über Filter, vor allem nach Kategorie (70 bis 80 %),
seltener Zeitraum (15 %), Payee, Betrag, Memo (je 5 %). Mehrere Kategorien zugleich.
Am Handy genauso einfach wie am PC.

**Gerät:** PC (1440 × 900) und Handy (390 × 844), beide Hauptgerät (Entscheidung Roman:
der Filter soll auch am Handy gehen, dort nutzt er ihn in YNAB heute nie).

**YNAB-Referenzweg (Roman, 2026-09-11):** In YNAB-Web die Kategorie in das Suchfeld
tippen. Es dauert, bis die richtige gefunden ist; versteckte Kategorien tauchen auf und
werden manchmal ungewollt gewählt. Dann "Filtern" klicken. Nur eine Kategorie je Filter.
Am Handy praktisch unbenutzbar.

**Ausgangszustand:** Gesamtsicht "Alle Konten", kein Filter aktiv, Liste zeigt die
neuesten Buchungen aller vier Konten.

**Schrittfolge (PC):**
1. Chip "Kategorie" klicken: Kategorienauswahl öffnet (am PC als Popover unter dem Chip,
   am Handy als BottomSheet). Oben Vorschläge und "Zuletzt verwendet", darunter die
   Gruppenliste mit Suchfeld, jede Kategorie mit Kästchen. Versteckte Kategorien stehen
   eingeklappt am Ende unter "Versteckt (1)".
2. "Lebensmittel" und "Restaurants" ankreuzen, "Übernehmen". Chip zeigt "Kategorie: 2".
   Summenzeile "312 Buchungen · -9.874,20 €". Liste zeigt nur diese Buchungen, über alle
   Konten, mit Konto-Spalte.
3. Chip "Zeitraum" klicken: Schnellwahl "Dieser Monat", "Letzter Monat", "Dieses Jahr",
   "Letzte 12 Monate", dazu Von/Bis. "Letzte 12 Monate" wählen. Summenzeile "148
   Buchungen · -4.610,55 €".
4. Chip "Payee": Suchfeld mit Vorschlägen aus den bisherigen Treffern, "Café Lotte"
   wählen. Summenzeile "9 Buchungen · -163,40 €".
5. Filter zurücksetzen über "×" an jedem Chip oder "Alle Filter löschen" am Ende der
   Leiste.

**Schrittfolge (Handy):**
1. Gleicher Ablauf, Kategorienauswahl als BottomSheet mit Mehrfachauswahl, "Übernehmen"
   als Button am unteren Sheet-Rand (Click-Commit-Muster, Styleguide 7.1).
2. Chips sind waagerecht scrollbar und bleiben sticky unter dem Kopf (Styleguide 7.3).

**Pflicht-Komponenten:** Kategorienauswahl (Mehrfachauswahl ist eine neue Variante der
bestehenden Karte, der Prototyp zeigt Kästchen je Zeile), BottomSheet, Input (Suche,
Datum), Badge (Chips), Money, Table (PC).

**Was nur BB zeigt:** die Konto-Spalte in der Gesamtsicht mit Herkunfts-Badge; Buchungen
"noch nicht in YNAB" erscheinen im Treffer mit Kennzeichen.

---

## Ablauf 3: Mehrfachauswahl mit Summe

**Ziel:** Roman sieht die Summe eines Treffers sofort und kann daraus eine Teilmenge
auswählen, deren Summe er ebenfalls sieht (Beispiel Steuererklärung: Kategorie wählen, die
letzten zwölf Buchungen markieren, Gesamtbetrag ablesen).

**Gerät:** PC (1440 × 900) und Handy (390 × 844), beide Hauptgerät (Entscheidung Roman).

**YNAB-Referenzweg (Roman, 2026-09-11):** In YNAB-Web nach Kategorie filtern, dann Zeilen
einzeln ankreuzen; die Summe der Auswahl erscheint unten. Roman möchte, dass der Treffer
selbst schon eine Summe zeigt, ohne einzeln anzukreuzen, und das auch am Handy.

**Ausgangszustand:** Ende von Ablauf 2, Filter "Kategorie: Kinder", Zeitraum "Dieses
Jahr". Summenzeile "34 Buchungen · -2.140,00 €".

**Schrittfolge (PC):**
1. Die Summenzeile steht ohne weiteres Zutun da (Ergebnis von Ablauf 2). Für viele Fälle
   ist Roman hier fertig.
2. Kästchen in der Kopfzeile: alle 34 auswählen. Auswahl-Leiste unten "34 ausgewählt ·
   -2.140,00 €".
3. Auswahl aufheben, dann die obersten zwölf per Klick auf das erste Kästchen und
   Shift-Klick auf das zwölfte. Auswahl-Leiste "12 ausgewählt · -780,00 €".
4. "Auswahl aufheben": Summenzeile zeigt wieder den Treffer.

**Schrittfolge (Handy):**
1. Summenzeile wie am PC über der Liste.
2. Langes Drücken auf eine Zeile: Auswahl-Modus, Kästchen erscheinen links, Auswahl-Leiste
   unten über der Navigation. Weitere Zeilen per Tipp. "Alle" als Aktion in der Leiste.
3. Summe der Auswahl in der Leiste, "Fertig" beendet den Modus.

**Pflicht-Komponenten:** Table mit Auswahlzeilen (`IsSelected`), Money, Button (Leiste),
BottomSheet-artige Auswahl-Leiste am Handy (kein Sheet, sondern eine feste Leiste über
der Bottom-Navigation), Badge.

**Was nur BB zeigt:** die Summenzeile rechnet über den gesamten Treffer, auch über nicht
geladene Seiten.

---

## Ablauf 4: Kategorie ändern, Split festlegen oder ändern

**Ziel:** Roman korrigiert die Kategorie einer Buchung oder teilt sie auf.

**Gerät:** PC (1440 × 900), Hauptgerät. Handy nur als Nachweis, dass es im Detail-Sheet
geht.

**YNAB-Referenzweg (Roman, 2026-09-12):** Buchung raussuchen, in das Kategorie-Feld
gehen, im Auswahlfenster die neue Kategorie wählen. Funktioniert im Web ordentlich. Was
stört: in der Ansicht der noch nicht geprüften Buchungen kann Roman die Buchung während
des Kategoriewechsels prüfen, dann wird sie umsortiert und verschwindet aus dem Blickfeld.
Was Roman an der YNAB-App mag: beim Wechsel sieht man bei beiden Kategorien den
verbleibenden Budgetbetrag, bei der alten geht er hoch, bei der neuen runter. Wunsch:
der aktuelle Budgetbetrag je Kategorie direkt in der Kategorienauswahl (Available).
Split: in YNAB-Web mehrere Zeilen, in der App mehrere Seiten nacheinander; beides geht,
die App-Führung könnte besser sein. Was BB heute schon besser macht: der Split-Editor
der Import-Prüfung mit "Rest"-Button je Zeile, kein Kopfrechnen (Roman: "gar nicht so
schlecht, wie es jetzt gelöst ist").

**Ausgangszustand:** Konto "Girokonto Comdirect", Chip "Zu prüfen (4)" aktiv. Zeile
"Bauhaus -81,90 €", Kategorie "Haushalt", ungeprüft.

**Schrittfolge (PC):**
1. Klick auf die Kategorie-Zelle "Haushalt": Kategorienauswahl öffnet als Popover an der
   Zelle. Vorschläge oben, darunter die Gruppenliste; jede Kategorie zeigt rechts ihren
   Available-Betrag (Money, grün/rot/neutral, wie die Karte `components-categorypicker`).
2. "Möbel" wählen. Die Zelle zeigt "Möbel", die Zeile bleibt an ihrem Platz und bleibt
   ungeprüft: Kategoriewechsel sortiert nie um und prüft nie mit. Kurzer Hinweis am
   Popover-Rand: "Haushalt 412,10 € → 494,00 €, Möbel 120,00 € → 38,10 €".
3. Zeile anklicken: Seitenpanel rechts. Im Feld Kategorie steht "Möbel", darunter
   "Aufteilen".
4. "Aufteilen" klicken: Split-Editor wie in der Import-Prüfung (SyncFlow): Zeile 1 mit
   Kategorie-Picker, Betragsfeld, Memo; darunter "Zeile hinzufügen"; über der Liste der
   Gesamtbetrag -81,90 € und der Rest.
5. Zeile 1: Möbel, 23,40 €. Zeile hinzufügen, Kategorie Haushalt, Button "Rest" neben dem
   Betragsfeld füllt 58,50 € ein. Rest zeigt 0,00 €, "Speichern" wird aktiv.
6. Speichern: die Tabellenzeile zeigt Kategorie "Aufgeteilt (2)", im Panel die zwei
   Split-Zeilen. Historie bekommt eine Zeile "Roman in BB: aufgeteilt in Möbel 23,40 €,
   Haushalt 58,50 €".
7. Rückweg: im Panel eine Split-Zeile entfernen, Rest-Button auf der verbleibenden Zeile,
   Speichern: wieder eine Kategorie.

**Schrittfolge (Handy):**
1. Tipp auf die Zeile: Detail-Sheet. Tipp auf Kategorie: Kategorienauswahl als
   BottomSheet (Layer 2), gleiche Available-Beträge, Click-Commit.
2. "Aufteilen" im Sheet öffnet den Split-Editor im selben Sheet (Zeilen untereinander,
   "Rest"-Button je Zeile, sticky "Speichern" am unteren Rand).

**Pflicht-Komponenten:** Kategorienauswahl (mit Available), Input (Betrag, Memo),
Button, BottomSheet (Layer 2), Table, Money, Badge ("Aufgeteilt (2)").

**Was nur BB zeigt:** die Historie-Zeile mit Herkunft "Roman in BB"; bis zum Rückschreiben
nach YNAB (v2) trägt die Buchung "Änderung noch nicht in YNAB". Der Available-Betrag je
Kategorie kommt aus dem Spiegel (YNAB kennt ihn heute, BB muss ihn mitspiegeln;
Klärung in `ledger-s8m3w`).

---

## Ablauf 5: Flags

**Ziel:** Roman setzt das Rückfrage-Flag "Lena" (Lena schaut sich die Buchung an), selten
ein temporäres Flag zum Gruppieren.

**Gerät:** PC (1440 × 900), Hauptgerät; Handy im Detail-Sheet.

**YNAB-Referenzweg (Roman, 2026-09-12):** Roman wählt die Buchungen aus, zu denen er
nichts findet, und setzt das Flag, das er "Lena" genannt hat. Lena nutzt YNAB über den
Familien-Account mit eigenem Login und sieht die Flags dort. Romans Gedanke beim
Beschreiben: eigentlich will er nicht "ein Flag setzen", sondern "Lena zuweisen". Das
Flag in YNAB wäre dann nur die Umsetzung im Hintergrund. Später denkbar: Lena bekommt
eine Push-Benachrichtigung oder öffnet in BB eine Sicht "Für mich" (wie gepinnte
Kategorien auf der YNAB-Startseite). Festgehalten als Backlog-Idee `ledger-xfjcp`.

**Ausgangszustand:** Gesamtsicht, Chip "Zu prüfen (5)" aktiv. Zeile "Amazon Marketplace
-37,45 €" ohne Kategorie, Memo leer.

**Schrittfolge (PC):**
1. Zeile anklicken, Seitenpanel. Unter den Feldern der Bereich "Zuweisen": Button
   "Lena fragen" (Icon: Sprechblase). Klick: die Zeile bekommt das Kennzeichen
   "Rückfrage Lena" (Badge, violett), das Panel zeigt "Zugewiesen an Lena, offen".
2. Zurück in der Tabelle: Kästchen bei zwei weiteren Zeilen, Auswahl-Leiste unten, Aktion
   "Lena fragen": beide bekommen das Kennzeichen.
3. Chip "Flag" in der Filterleiste: Untermenü "Rückfrage Lena", "Temporär". "Rückfrage
   Lena" wählen: Liste zeigt die drei Buchungen, Summenzeile "3 Buchungen · -96,25 €".
4. Im Panel einer dieser Buchungen: "Erledigt" nimmt die Zuweisung zurück, Kennzeichen
   verschwindet.
5. Temporäres Flag: im Panel "Markieren" (Icon: Fähnchen, orange) zum Gruppieren; Filter
   "Temporär" zeigt sie; "Alle Markierungen aufheben" in der Filterleiste.

**Schrittfolge (Handy):**
1. Detail-Sheet, Button "Lena fragen" unter den Feldern. Kennzeichen in der Zeile.
2. Im Auswahl-Modus (langes Drücken) Aktion "Lena fragen" in der Auswahl-Leiste.

**Pflicht-Komponenten:** Badge (Kennzeichen "Rückfrage Lena", "Markiert"), Button
(Ghost mit Icon), Icons, Table, BottomSheet.

**Was nur BB zeigt:** die Zuweisung als Historie-Zeile "Roman in BB: Lena gefragt". In
YNAB wird daraus das Flag "Lena" (Rückschreiben ab v2, bis dahin Kennzeichen "noch nicht
in YNAB" an der Änderung).

---

## Ablauf 6: Konto wechseln, Gesamtsicht als Suchhilfe

> **Zuschnitt korrigiert am 2026-09-12 nach Romans YNAB-Referenz.** Der Ablauf hieß
> vorher "Gesamtsicht und Konto-Sicht mit Kontoständen" und ging davon aus, dass die
> Gesamtsicht ein eigener, häufiger Arbeitsplatz ist. Das stimmt nicht. Der häufige Fall
> ist der **Wechsel in ein einzelnes Konto**; die Gesamtsicht hat genau einen, seltenen
> Zweck.

**Ziel:** Roman wechselt schnell in das Konto, das er sehen will. Die Gesamtsicht ist die
Rückfallebene für den einen Fall, in dem er nicht weiss, in welchem Konto eine Buchung
liegt.

**Gerät:** beide (Handy 390 × 844, PC 1440 × 900).

**YNAB-Referenzweg (Roman, 2026-09-12):** Die Gesamtsicht nutzt er **eigentlich nicht**.
Wenn er in den Konten ist, wechselt er direkt in das, was er sehen will, mal Bar, mal ein
anderes. Die Gesamtsicht braucht er nur, wenn eine Buchung im falschen Konto gelandet ist
und er sie suchen muss; das kommt selten vor. Häufiger wäre sie nur, wenn er abwechselnd
mit ING und Comdirect zahlen würde und für beide einen Abruf hätte, und das ist nicht der
Fall. An den Kontoständen, wie YNAB sie zeigt, stört ihn **nichts**.

**Was daraus folgt:**
1. **Der Konto-Wechsel ist der Ablauf**, nicht die Gesamtsicht. Er muss mit einer Geste
   gehen und darf nicht wie ein Seitenwechsel wirken. Am Handy über die
   Kontostände-Leiste oder den Chevron im Titel, am PC über die Konten-Spalte.
2. **Die Gesamtsicht ist eine Suchhilfe.** Ihr eigentlicher Zweck ist "finde die Buchung,
   von der ich nicht weiss, in welchem Konto sie liegt". Das ist fachlich näher an
   Ablauf 2 (Filter) als an einer eigenen Sicht: die Gesamtsicht ist der Zustand, in dem
   die Filter über alle Konten greifen. Deshalb trägt dort jede Zeile ihr Konto.
3. **Kein eigener Aufwand für die Kontostände.** Sie sollen stimmen und sichtbar sein,
   mehr verlangt Roman nicht. Kein Verlauf, keine Kacheln, keine Auswertung.

**Ausgangszustand:** Konto "ING Extra" (zuletzt gewählt), Chip "Zu prüfen" aktiv.

**Schrittfolge (Handy):**
1. Kontostände-Leiste oben, waagerecht scrollbar: "Alle Konten", danach die vier Konten.
   Aktives Konto hervorgehoben.
2. Tipp auf "Bar": die Liste zeigt sofort die Bar-Buchungen, der aktive Filter bleibt
   stehen. Kein Ladebildschirm, kein Seitenwechsel.
3. Tipp auf "Alle Konten": Liste über alle Konten, jede Zeile mit Konto-Kürzel. Hier
   sucht Roman die verirrte Buchung, danach wechselt er zurück.

**Schrittfolge (PC):**
1. Konten-Spalte links, "Alle Konten" zuoberst, darunter die vier Konten mit Stand.
2. Klick auf ein Konto wechselt die Liste; in der Konto-Sicht entfällt die Konto-Spalte
   der Tabelle, in der Gesamtsicht kommt sie dazu.

**Offen und nicht Teil dieses Ablaufs:** Der Geldmarkt-ETF ist in YNAB noch gar nicht
angelegt, und wie der monatliche Abgleich aussehen soll, ist ungeklärt (Romans Idee
bisher: vielleicht ein Reconcile einmal im Monat). Das ist ein v1-Kriterium der Vision und
liegt als eigener Task `ledger-nb4tv` im Backlog. Der Entwurf zeigt das Konto trotzdem mit
seinem Stand, damit die Sicht vollständig ist.

**Ausgangszustand:** Kassenbuch im Konto "ING Extra" (zuletzt gewählt).

**Schrittfolge (Handy):**
1. Kontostände-Leiste oben: Karten "Alle Konten 21.388,85 €", "ING Extra 6.902,10 €",
   "Girokonto Comdirect 2.418,37 €", "Bar 63,50 €", "Geldmarkt-ETF 12.004,88 €". Aktives
   Konto hervorgehoben (Glow).
2. Tipp auf "Alle Konten": Liste wechselt auf die Gesamtsicht, Zeilen zeigen ein
   Konto-Kürzel (ING, CD, Bar, ETF). Aktiver Filter "Zu prüfen" bleibt.
3. Tipp auf "Geldmarkt-ETF": Liste zeigt die Bewertungs-Buchungen ("Kursgewinn Juli
   +42,10 €") und die Einzahlungen.

**Schrittfolge (PC):**
1. Linke Spalte mit "Alle Konten" zuoberst und den vier Konten samt Ständen, darunter der
   Lauf-Status des Spiegels ("Zuletzt gespiegelt: heute 21:40").
2. Klick auf ein Konto wechselt die Liste, die Tabelle verliert in der Konto-Sicht die
   Konto-Spalte.

**Pflicht-Komponenten:** Stats (Kontostände), Card, Money.balance, Navigation, Table.

**Was nur BB zeigt:** Bewertungs-Buchungen des Wertpapierkontos mit Herkunft; den
Lauf-Status des Spiegels.

---

## Ablauf 7: Was nur BB kann

**Ziel:** Roman sieht zu einer Buchung, woher sie stammt, was sich wann geändert hat, ob
sie in YNAB gelöscht wurde, und ob sie noch nicht in YNAB ist.

**Gerät:** Handy und PC, Detail-Sheet beziehungsweise Seitenpanel.

**YNAB-Referenzweg:** keiner, das gibt es in YNAB nicht. Erwartung von Roman: *(offen)*

**Ausgangszustand:** Konto "Girokonto Comdirect", Liste mit gemischten Herkünften.

**Schrittfolge:**
1. Zeile "Bauhaus -84,90 €" trägt das Kennzeichen "noch nicht in YNAB". Tipp: Detail
   zeigt Herkunft "Comdirect-Import", Hinweis "Noch nicht in YNAB: Push gescheitert am
   10.09. 18:02 (Netzwerkfehler)". Aktion "Erneut senden" (Prototyp: nur der Button).
2. Zeile "REWE Mitte -48,12 €" (Herkunft Regel): Detail zeigt die Historie: "10.09. 17:55
   Comdirect-Import: Buchung erfasst", "10.09. 17:55 Regel: Kategorie Lebensmittel
   gesetzt", "11.09. 08:12 YNAB-Spiegel: Kategorie in YNAB geändert: Lebensmittel →
   Haushalt". Dazu der Abweichungshinweis (v2): "Abweichung: BB sagt Lebensmittel, YNAB
   sagt Haushalt" mit Aktionen "YNAB übernehmen", "BB durchsetzen".
3. Chip "Gelöschte zeigen": Zeile "Café Lotte -9,80 €" erscheint ausgegraut mit "in YNAB
   gelöscht", zählt nicht zum Kontostand.

**Pflicht-Komponenten:** Badge (Herkunft, Kennzeichen), BottomSheet, Card, Button,
Icons.

**Was nur BB zeigt:** alles in diesem Ablauf.
