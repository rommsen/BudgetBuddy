---
topic: Regeln für Seitenlayout einer Liste-plus-Detail-App am Handy und am PC (Dichte, Hierarchie, Daumenzone, horizontale Leisten, Finanz-Apps)
date: 2026-11-07
requested_by: user
related_tasks: []
---

# Research: Seitenlayout für Liste plus Detail (Handy und PC)

## Question

Welche Regeln für Seitenlayout machen eine Liste-plus-Detail-App am Handy und am PC ruhig, geführt und dicht
genug? Teilfragen: Dichte und Leerraum, visuelle Hierarchie, Handy-Ergonomie (Daumenzone, Trefferflächen,
Hauptaktion, Bottom-Navigation), Alternativen zu horizontal scrollenden Leisten, und wie Finanz-Apps (YNAB,
Monarch, Copilot, Finanzguru, Revolut, Lunch Money) Kontowahl, Filter, Startseite und Erfassen lösen.

Ist-Stand in BudgetBuddy (aus dem Repo): Die Bottom-Navigation hat sechs Punkte (Sync, Rules, Quick Add,
Kassenbuch, Budget, Settings; `src/Client/DesignSystem/Navigation.fs`, `navItems`). Der Styleguide legt
Trefferflächen auf mindestens 48 px fest (§4), Listenzeilen am PC auf 44 px, im Budget dicht auf 40 px (§2
"Ruhige Listen"), und Filter-Pills haften unter dem Kopf (§7.3). Bottom Sheets laufen über `BottomSheet` und
`Viewport` (§7.1), höchstens eine Ebene tief.

## Summary

- **Navigation:** Material empfiehlt am Handy drei bis fünf gleich wichtige Ziele in der unteren Leiste [1].
  Apple nennt für das iPhone keine Höchstzahl, sagt aber, weniger Tabs seien leichter zu bedienen, rät von
  Überlauf-Tabs ab und will in der Tab-Bar nur Navigation, keine Aktionen [2]. Die heutige Leiste mit sechs
  Punkten liegt über Materials Grenze, und "Quick Add" als Punkt ist eine Aktion in der Tab-Bar. Drei Orte
  (Übersicht, Kassenbuch, Budget) plus gegebenenfalls Einstellungen liegen in Materials Bereich.
- **Horizontale Leisten:** Die Evidenz ist durchgehend vorsichtig. Nutzer erwarten mehr Inhalt unten, nicht
  seitlich, wichtige Inhalte gehören nicht hinter einen Seitwärts-Scroll [5]. Auf dem Handy schauen die meisten
  nach drei bis vier Schritten nicht weiter [6]. Baymard rät von horizontalen Filterleisten für die meisten
  Fälle ab, weil versteckte Filter übersehen werden [12]. Zwei solcher Leisten übereinander sind in keiner
  Quelle ein empfohlenes Muster.
- **Ersatzmuster mit Beleg:** Auswahlfeld für die Kontowahl (wenige Werte, gültige Werte, Platz sparend [16]),
  ein Filter-Knopf mit eigenem Layer (Sheet) und eine sichtbare Übersicht der gesetzten Filter als entfernbare
  Chips [13]. Eine Zahl am Knopf allein ("Filter (3)") hat bei Baymard im Test schlecht abgeschnitten, sie taugt
  nur zusammen mit der sichtbaren Übersicht [13]. Dazu Segmented Control für eng verwandte Ansichten mit höchstens etwa fünf Segmenten am iPhone [3].
- **PC:** Material zeigt Liste und Detail ab 840 dp Breite nebeneinander, darunter nur eins von beiden [8][9].
  Für ein Nebenpanel nennt Material als Richtwert 70 zu 30 [9]. Fließtext bleibt bei 50 bis 75 Zeichen je Zeile
  [17]. Leerflächen am PC entstehen, wenn Inhalt auf eine Spalte beschränkt bleibt, obwohl die Breite für zwei
  reicht.
- **Finanz-Apps:** YNAB und Copilot legen "neue Buchungen durchgehen" auf die Startseite als Hinweis oder
  Abschnitt, YNAB seit September/Oktober 2026 als Karten-Modus mit Wischen [19][20][22]. YNAB trennt "alle
  Buchungen" (Tab Spending) und "ein Konto" (Tab Accounts) statt einer Kontoleiste [21]. Monarch hat Ende 2024
  die Zeilenhöhe gesenkt, um mehr Buchungen zu zeigen [24].
- **Lücke:** Für Finanzguru und Revolut gibt es keine brauchbaren Primärquellen zu Kontowahl und Filtern. Was
  dazu unten steht, ist schwach belegt.

## Findings

### 1. Dichte und Leerraum

**Raster.** Material 2 richtet die meisten Maße an 8 dp aus, kleine Elemente (Icons, Typo-Grundlinie) an 4 dp
[10]. Für Material 3 fand ich keine zugängliche Primärseite mit den Werten. Das Android-Designsystem von SAP
(auf Material 3 aufgebaut) nennt 4-dp-Schritte für Abstände innerhalb von Komponenten und Seitenränder von
16 dp (compact) und 24 dp (medium, expanded) [11]. Die Randwerte sind damit nur über eine Sekundärquelle belegt.

**Breakpoints.** Android definiert Fensterklassen nach Breite: compact unter 600 dp (99,96 % der Handys hochkant),
medium 600 bis 840 dp, expanded 840 bis 1200 dp, large 1200 bis 1600 dp, extra-large ab 1600 dp (Desktop) [8].
Apple arbeitet mit compact und regular und sagt: Layout nach verfügbarem Platz, nicht nach Gerätetyp [4].

**Liste plus Detail.** Im kanonischen List-Detail-Layout zeigt Material Liste und Detail nebeneinander erst ab
expanded (840 dp). Bei compact und medium sieht man entweder Liste oder Detail, Zurück führt zur Liste [9]. Für
ein unterstützendes Panel (Detail ergibt nur im Zusammenhang Sinn) gibt Material für expanded 70 % Hauptinhalt
und 30 % Panel an, bei medium halbe-halbe, bei compact unter dem Inhalt oder im Bottom Sheet [9]. Apple empfiehlt,
auf großer Fläche mehr Funktion sichtbar zu machen, etwa Sidebar statt Tab-Bar oder Inhalte aus dem
Überlaufmenü herauszuholen, aber die Funktion selbst nicht zu ändern [4].

**Dichte von Listen.** Monarch hat im Dezember 2024 die Höhe jeder Buchungszeile auf Desktop und Handy
verringert, "um mehr ins Bild zu bringen", und die Konten-Seite am Desktop dichter gemacht, Kontogruppen
einklappbar [24]. Das ist eine Produktentscheidung, keine Studie. Eine Primärquelle mit Zeilenhöhen für dichte
Listen habe ich nicht gefunden. Die Untergrenze setzen die Trefferflächen (Abschnitt 3): am Handy 44 pt / 48 dp
je Zeile, am PC gilt diese Grenze nicht, macOS nennt 28 pt Standard und 20 pt Minimum für Bedienelemente [7].

**Inhaltsbreite.** Für Fließtext gelten 50 bis 75 Zeichen je Zeile, WCAG 1.4.8 nennt höchstens 80 [17]. Baymard
selbst merkt an, dass die Studienlage dazu gemischt ist [17]. Für Tabellen und Listen mit Zahlen gibt es keine
solche Grenze. Dort entsteht Leerraum eher, wenn eine Zeile über 1600 px läuft und Payee und Betrag weit
auseinanderliegen. Daraus folgt (eigene Ableitung, keine Quelle): Breite für Listen begrenzen oder die freie
Breite an ein Panel geben.

### 2. Visuelle Hierarchie

NN/g nennt drei Mittel: Kontrast, Größe, Gruppierung [14]. Konkrete Empfehlungen daraus: höchstens drei
Schriftgrößen, das wichtigste Element am größten, höchstens zwei große Elemente je Ansicht; warme, helle Farben
für Warnungen und Fehler reservieren; höchstens drei Kontraststufen ("If everything is contrasted, then nothing
stands out"); wichtige Elemente bekommen mehr Luft, Überschrift und zugehöriger Inhalt stehen enger; Rahmen und
Hintergründe nur, wenn Leerraum allein nicht reicht [14]. Prüfung über den Squint-Test [14].

Apple: Inhalte nach Wichtigkeit ordnen, Wichtiges oben und an der Leseanfangsseite; Verwandtes über Leerraum,
Container oder Trennlinien gruppieren [4].

Eine Hauptaktion: Material beschreibt den FAB als Knopf für "a single, focused action", die häufigste Handlung,
unten rechts verankert [26]. Apple trennt streng: Tab-Bar nur für Navigation, Aktionen auf die aktuelle Ansicht
gehören in eine Toolbar [2].

### 3. Handy: Daumen, Trefferflächen, Hauptaktion, Navigation

**Trefferflächen.** Apple: 44 x 44 pt Standard, 28 x 28 pt Minimum auf iOS; Abstand um Elemente mit Rahmen etwa
12 pt, ohne Rahmen etwa 24 pt [7]. Android: mindestens 48 x 48 dp, mit 8 dp Abstand [27]. NN/g: mindestens
1 x 1 cm [15]. Der BB-Styleguide liegt mit 48 px auf der Android-Linie.

**Daumenzone.** Hoober (2013, 1333 Beobachtungen): 49 % einhändig, 36 % gehalten in einer Hand und mit der anderen
bedient, 15 % zweihändig; Nutzer wechseln den Griff oft innerhalb von Sekunden [18]. Er warnt davor, nur für
einhändige Nutzung zu entwerfen, und hat seine eigenen Zeichnungen der Daumenreichweite später als ungenau
zurückgezogen [18]. Die Daten sind alt und von 2013 (kleinere Geräte). NN/g hält die verbreitete Annahme, Bottom
Sheets verbesserten die Erreichbarkeit, für weitgehend falsch [25]. Folgerung: Häufige Handlungen nach unten zu
legen ist vertretbar, aber nicht stark belegt.

**Hauptaktion: FAB oder Leiste.** Material: FAB für eine Hauptaktion je Ansicht, unten rechts [26]. YNAB legt
"Add Transaction" als Knopf unten in die Tabs Home, Plan, Spending und Accounts, nicht in die Navigationsleiste
[21]. Apple: keine Aktion in der Tab-Bar [2].

**Bottom-Navigation.** Material: drei bis fünf gleich wichtige Ziele, nur für compact; ab medium eine Navigation
Rail (drei bis sieben Ziele, belegt über SAP Fiori) [1]. Zu den Labels sagt die Compose-Referenz zu
`NavigationBarItem`: bei drei Zielen Icon und Label für alle; bei vier trägt das aktive Icon und Label, für die
inaktiven werden Labels empfohlen; erst bei fünf tragen die inaktiven Labels nur, "if space permits" [38]. Apple
nennt für das iPhone keine Höchstzahl: "it's generally easier to navigate among fewer tabs" [2]. Die einzige Zahl
("five or fewer") steht im iPadOS-Abschnitt und gilt für die Vorauswahl einer vom Nutzer anpassbaren Tab-Bar [2].
Apple rät von Überlauf-Tabs ab ("The More tab makes it harder for people to reach and notice content") und will die
Tab-Bar beim Wechsel der Bereiche sichtbar halten [2]. Monarch hat fünf Punkte unten und den Rest hinter dem
Profil-Icon oben links [23]; YNAB hat fünf (Home, Plan, Spending, Accounts, Reflect), der Help-Tab gehört laut
Hilfe-Seite nicht mehr zur Leiste [21]. Wann er entfernt wurde, steht dort nicht; das Datum 2026-07-02 ist nur der
letzte Stand der Seite, und das YNAB-Änderungsprotokoll [19] nennt die Entfernung nicht.

### 4. Alternativen zu horizontal scrollenden Leisten

**Evidenz gegen horizontale Scroller.**
- NN/g (2014, Desktop-Fokus): "People expect to scroll vertically for additional content, but they don't expect
  to scroll sideways." Wesentlicher Inhalt gehört nicht hinter einen Seitwärts-Scroll; zulässig für zweitrangige
  Inhalte [5].
- NN/g (2018, Handy): Angeschnittene Elemente am Rand sind ein starkes Signal für "da kommt mehr". Die meisten
  hören nach drei bis vier Seiten auf; das letzte Element soll in drei bis vier Wischern erreichbar sein; für
  lange Mengen besser eine Liste [6]. Dazu: Abstand zum Bildschirmrand wegen der Zurück-Geste von iOS [6].
- Baymard (2015, aktualisiert März 2025, Desktop): Horizontale Filterleisten funktionieren nur mit wenigen
  Filtertypen; Knöpfe wie "Alle Filter" wurden übersehen; für die meisten Seiten rät Baymard ab [12].
- NN/g (2016): Versteckte Navigation wird auf dem Handy seltener benutzt (Bloomberg mit verstecktem Menü 44 %,
  kombinierte Lösung 89 %) [28]. Das spricht gegen "alles hinter einen Knopf" und für eine Mischform: Wichtigstes
  sichtbar, Rest dahinter.

Für das Kassenbuch heißt das (Ableitung): Die erste, links abgeschnittene Kachel ist das Gegenteil des Signals,
das NN/g empfiehlt. Angeschnitten sollte das Ende sein, nicht der Anfang [6].

**Muster und ihre Grenzen.**

| Muster | Beleg | Grenze |
|---|---|---|
| Auswahlfeld (Konto ▾) mit Bottom Sheet | Dropdowns passen für Attributauswahl aus gültigen Werten, Liste kurz halten [16]; Sheets für kurze, vorübergehende Aufgaben, sichtbarer Schließen-Knopf, nicht stapeln [25] | Optionen sind versteckt [16]; Saldo der anderen Konten ist nicht mehr auf einen Blick sichtbar |
| Filter-Knopf mit Sheet und Zähler | Baymard: Filter in eigenen Layer (Overlay oder Bottom Sheet), Knopf klar beschriftet und beim Scrollen sichtbar [13] | Eine Zahl allein am Knopf vermeiden: Baymard nennt Williams-Sonoma ("Filter (3)") als Negativbeispiel, die reine Anzahl "didn't perform well during testing". Zulässig nur zusammen mit einer sichtbaren Übersicht der gesetzten Filter [13] |
| Übersicht gesetzter Filter als entfernbare Chips | Baymard: ohne Übersicht mussten Testpersonen das Filter-Layer öffnen, um zu sehen, was gilt, und für jedes Entfernen erneut öffnen; Baymard rät, gesetzte Filter immer in einer Übersicht zu zeigen [13]. Am Handy nennt Baymard zwei Formen: eine horizontal scrollende Zeile, deren rechtes Element angeschnitten ist (am besten zur Hälfte oder mehr sichtbar, oder mit Ausblendung), oder gestapelte Zeilen, gegebenenfalls auf ein bis zwei Zeilen begrenzt mit "See All Applied Filters"; als ideal gilt Scroll-Signal, Anschnitt und genannte Anzahl zusammen [13] | Nimmt eine Zeile, wenn Filter aktiv sind. Die Scroll-Form ist hier ausdrücklich für gesetzte Filter empfohlen, nicht für die Auswahl aller Filter |
| Segmented Control | Apple: eng verwandte Optionen, die eine Ansicht beeinflussen; am iPhone höchstens etwa fünf Segmente, gleich breit [3]. Material: zwei bis fünf, darüber Chips [29] | Lange Labels mit Zählern ("Rückfrage Lena (0)") passen bei 390 px kaum in gleich breite Segmente (Ableitung) |
| Umbrechende Chips | SAP Fiori erlaubt Umbruch oder Seitwärts-Scroll [30] | Höhe wächst mit der Zahl der Chips; nur bei wenigen kurzen Chips ruhig (Ableitung) |
| Überlauf "Mehr" | Apple rät bei Tabs davon ab, weil Inhalte schwer zu finden sind [2]; Baymard: "Alle Filter" wurde übersehen [12] | Die Belege betreffen Tabs und Desktop-Filter, nicht Chips am Handy |
| Saldo im Kopf statt Kacheln | YNAB: Konten als eigener Ort (Tab Accounts), Buchungen aller Konten im Tab Spending [21] | Kein Beleg, dass ein Saldo im Kopf besser gefunden wird; Musterbeleg nur aus einer App |
| Kollabierender Kopf | Keine Primärquelle gefunden | Offen |

### 5. Finanz-Apps

**YNAB.**
- Unten fünf Tabs: Home, Plan, Spending (alle Buchungen der Cash- und Kreditkonten, mit Suche), Accounts (je Konto
  eine eigene Seite, Konten markierbar mit Stern), Reflect. "Add Transaction" sitzt als Knopf unten in Home, Plan,
  Spending und Accounts [21] (Stand Juli 2026).
- Home: oben Hinweise, wenn Geld zu verteilen ist, Buchungen zu bestätigen oder Überzug zu decken ist; darunter
  angeheftete Kategorien, ein aktuelles Ziel, Monatszusammenfassung; Abschnitte einklappbar [21][22]. Die
  Hilfe-Seite zu Home (Oktober 2026) nennt die Hinweise nicht als eigenen Abschnitt [22]; die beiden YNAB-Seiten
  weichen hier voneinander ab.
- Karten-Modus beim Bestätigen: iOS ab 16. September 2026, Android ab 21. Oktober 2026. Jede Buchung als
  verdichtete Karte, rechts wischen bestätigt, links überspringt; Knöpfe "Approve" und "Skip For Now" für Nutzer
  ohne Wischen; Rückgängig mit einem Tipp; die bisherige Ansicht heißt List Mode, die Wahl bleibt gespeichert;
  nach der Schlange hilft YNAB, Überzug zu decken, und zeigt die übersprungenen Buchungen [19][20].
- Kontowahl: kein Auswahlfeld über einer Liste, sondern Trennung in "alle" (Spending) und "ein Konto" (Accounts)
  [21].

**Copilot Money.** Dashboard als Start-Tab, Reihenfolge: Ausgabenverlauf mit "Free to Spend", dann "To Review"
(neu importierte, noch nicht geprüfte Buchungen, mit "MARK AS REVIEWED"), dann Budgets, dann anstehende
wiederkehrende Buchungen (horizontal scrollbar), dann Monatssaldo [31] (Stand April 2026). Ungeprüfte Buchungen
tragen überall eine hellblaue Markierung [32]. Am Mac ist die Karte auf 25 Buchungen auf einmal begrenzt, damit
niemand versehentlich über 100 bestätigt [33]. Wischgesten zum Kategorisieren nur aus einer schwachen
Drittquelle, nicht bestätigt.

**Monarch Money.** Fünf Hauptpunkte unten, weitere Bereiche hinter dem Profil-Icon oben links (Februar 2022) [23].
Dashboard am Handy getrennt vom Web anpassbar, Widgets verschiebbar und ausblendbar (März 2021) [34]. Dezember
2024: Buchungszeilen niedriger, Konten-Seite dichter, Kontogruppen einklappbar [24]. Filter am Handy: nur über
einen App-Store-Aggregator belegt (Filterauswahl mit Händlerlogos, Filter nach "pending"), schwach.

**Lunch Money.** Filter über einen Knopf "Filters" rechts über dem Bereich "Review Transactions" am Desktop [35].
Das Feedback-Board zeigt einen typischen Fehler dieses Musters: Der Knopf zeigt "keine Filter", die Liste ist aber
noch auf ein Konto gefiltert [36]. Das stützt Baymards Punkt, dass der Filterzustand sichtbar und eindeutig sein
muss [13].

**Finanzguru, Revolut.** Keine Primärquelle zu Kontowahl oder Filtern gefunden. Rezensionen beschreiben bei
Finanzguru eine Übersicht aller Konten auf einen Blick als Start [37] (Drittquelle). Für Revolut fand ich nur
Pressetexte zum Redesign "Revolut 10" (Oktober 2023) ohne Angaben zur Kontowahl; die offizielle Seite war nicht
abrufbar (403). Beides nicht entscheidungsfähig.

### 6. Abgeleitete Layout-Regeln

Die Regeln leiten sich aus den Quellen oben ab. Wo eine Regel über die Quellen hinausgeht, steht "Ableitung".

**Handy (compact, unter 600 px)**

1. Unten höchstens fünf Ziele (Material [1]; Apple nennt keine Zahl, nur "fewer tabs" und kein Überlauf-Tab [2]),
   nur Orte, keine Aktionen [2]. Quick Add wird eine Aktion in der Ansicht (Knopf
   unten oder FAB), kein Tab [2][21][26].
2. Trefferfläche jeder Zeile und jedes Knopfs mindestens 48 x 48 px, 8 px Abstand zwischen Zielen [27][7].
3. Je Ansicht eine Hauptaktion, visuell abgesetzt; alles andere neutral [26][14].
4. Höchstens eine horizontal scrollende Leiste je Ansicht, und nur für zweitrangige Inhalte; ihr Anfang ist nie
   abgeschnitten, das Ende darf angeschnitten sein; das letzte Element in drei bis vier Wischern erreichbar
   [5][6]. (Höchstens eine: Ableitung.)
5. Was den Zustand der Liste bestimmt (Konto, aktive Filter), ist ohne Öffnen eines Menüs sichtbar; gesetzte
   Filter als Chips, nicht nur als Zahl [13].
6. Gleiches nicht doppelt zeigen: eine Kontowahl, nicht Auswahlfeld und Kacheln zugleich (Ableitung aus [14],
   "If everything is contrasted, then nothing stands out").
7. Bottom Sheets mit sichtbarem Schließen-Knopf, Zurück-Geste schließt, keine gestapelten Sheets [25]
   (deckt sich mit Styleguide §7.1).
8. Drei Schriftgrößen je Ansicht, höchstens zwei große Elemente (etwa Saldo und Seitentitel) [14].
9. Seitenrand 16 px [11] (Sekundärquelle).
10. "Durchgehen" neuer Buchungen als Einstieg auf der Startseite mit Zähler, nicht als dauerhafter Filter in der
    Liste (Muster aus YNAB und Copilot [19][21][31]; ob das für BB passt, ist eine Produktentscheidung).

**PC (ab 840 px, Desktop ab 1200 px)**

1. Ab 840 px Liste und Detail nebeneinander; darunter nur eins [8][9].
2. Detail-Panel als feste Spalte; Richtwert 30 % der Breite für ein unterstützendes Panel [9] (BB heute 340 px
   fest, bei 1440 px rund 24 %).
3. Freie Breite geht an Inhalt (Spalten, Panel), nicht an Ränder; ab etwa 1600 px eine Höchstbreite für die Liste
   oder ein drittes Element (Ableitung aus [4][8]).
4. Fließtext im Panel höchstens 75 Zeichen je Zeile [17].
5. Auf großer Fläche das zeigen, was am Handy hinter Menüs liegt: Kontenliste als Sidebar statt Auswahlfeld,
   Filter als sichtbare Zeile statt Sheet [4]. Die Funktion bleibt gleich [4].
6. Ab 600 px Navigation als Rail oder Sidebar statt Leiste unten [1][2].
7. Zeilenhöhe darf unter 44 px fallen, weil die Zeiger-Trefferfläche kleiner sein darf (macOS 28 pt Standard
   [7]); Untergrenze ist Lesbarkeit. Konkrete Werte fand ich in keiner Primärquelle.
8. Gruppen über Leerraum und Nähe trennen, Rahmen und Karten nur, wo Leerraum nicht reicht [14][4].
9. Wichtigstes oben und links; Zahlen, die eine Entscheidung tragen, größer als Zahlen, die nur informieren [4][14].

### 7. Optionen für "zwei horizontale Leisten im Kassenbuch am Handy"

Ausgangslage: Auswahlfeld "Girokonto ▾", darunter eine Leiste mit Konten-Kacheln (Saldo, erste links
abgeschnitten), darunter eine Leiste mit Filter-Chips mit Zählern.

**Option A: Kontowahl im Kopf, eine Chip-Leiste darunter.**
Das Auswahlfeld im Kopf trägt Kontoname und Saldo; es öffnet ein Bottom Sheet mit allen Konten und ihren Salden.
Die Kachel-Leiste entfällt. Die Filter-Chips bleiben als eine Leiste, haftend wie heute (§7.3), Anfang nicht
abgeschnitten.
- Für: kleinster Umbau, nutzt `BottomSheet` und Picker-Muster (§7.1, §7.2); die Doppelung verschwindet; Salden
  bleiben einen Tipp entfernt [16][25].
- Gegen: Es bleibt ein horizontaler Scroller; Chips am Ende werden übersehen [5][6][12]. Die Salden aller Konten
  sind nicht mehr auf einen Blick sichtbar [16].

**Option B: Kontowahl im Kopf, ein Filter-Knopf mit Zähler, aktive Filter als Chips.**
Wie A für die Kontowahl. Statt der Chip-Leiste ein Knopf "Filter" mit Zahl der aktiven Filter; er öffnet ein Sheet
mit allen Filtern untereinander, mit Zählern. Gesetzte Filter erscheinen als entfernbare Chips unter dem Kopf.
Optional bleibt der häufigste Filter ("Zu prüfen (n)") als einzelner Chip neben dem Knopf sichtbar (Mischform
nach [28]).
- Für: kein horizontaler Scroller; folgt Baymards Mobil-Empfehlung (eigener Layer, Knopf haftend, Übersicht der
  gesetzten Filter) [13]; Höhe bleibt klein, solange kein Filter aktiv ist.
- Gegen: Filter sind einen Tipp tiefer; versteckte Optionen werden seltener genutzt [28][16]. Der Zähler am Knopf
  trägt nur zusammen mit den sichtbaren Chips der gesetzten Filter; eine Zahl allein hat bei Baymard im Test
  schlecht abgeschnitten [13]. Die Chip-Übersicht ist also Pflichtteil dieser Option, kein Zusatz. Ein Fehlerbild wie bei Lunch Money (Knopf und Liste zeigen verschiedenen
  Zustand) muss ausgeschlossen werden [36].

**Option C: Durchgehen auf die Übersicht, im Kassenbuch nur Kontowahl und Suche.**
"Zu prüfen" und "Rückfrage Lena" werden Kacheln "braucht dich" auf der Übersicht mit Zähler; ein Tipp startet das
Durchgehen (Liste oder Karte je Buchung). Das Kassenbuch zeigt oben Kontowahl (wie A), Suche und einen Filter-Knopf
für den seltenen Rest (Kategorie und Ähnliches).
- Für: entspricht dem Muster von YNAB (Hinweise auf Home, Karten-Modus) und Copilot (To Review auf dem Dashboard)
  [19][21][31]; das Kassenbuch wird ruhig; passt zum Zielbild mit drei Orten und Kacheln "braucht dich".
- Gegen: größter Umbau; der Zustand "zu prüfen" muss in der Liste trotzdem sichtbar bleiben (heute pinke Kante,
  §2), sonst verliert man ihn beim Blättern; wer gezielt im Kassenbuch nach offenen Buchungen filtern will, braucht
  den Filter weiterhin, nur eine Ebene tiefer. Die Belege sind Produktentscheidungen zweier Apps, keine Studien.

Die Optionen schließen sich nicht aus: C enthält A für die Kontowahl, und B kann den Rest-Filter in C stellen.

## Sources

1. [Navigation bar, Jetpack Compose (Android Developers)](https://developer.android.com/develop/ui/compose/components/navigation-bar): drei bis fünf gleich wichtige Ziele, compact. Rail für drei bis sieben Ziele laut [SAP Fiori: Navigation Rail](https://www.sap.com/design-system/fiori-design-android/v26-4/components/m3-standard-components/navigation-rail/usage) (Material-basiert, Sekundärquelle). Ohne Datum.
2. [Apple HIG: Tab bars](https://developer.apple.com/design/human-interface-guidelines/tab-bars): weniger Tabs sind leichter, keine Höchstzahl fürs iPhone ("five or fewer" nur für die Vorauswahl anpassbarer Tab-Bars unter iPadOS), kein Überlauf-Tab, keine Aktionen, Tab-Bar sichtbar halten. Laufend gepflegt, abgerufen 2026-11-07.
3. [Apple HIG: Segmented controls](https://developer.apple.com/design/human-interface-guidelines/segmented-controls): höchstens etwa fünf Segmente am iPhone, gleich breit. Abgerufen 2026-11-07.
4. [Apple HIG: Layout](https://developer.apple.com/design/human-interface-guidelines/layout): Reihenfolge nach Wichtigkeit, Gruppierung, Size Classes, mehr Funktion auf großer Fläche. Abgerufen 2026-11-07.
5. [NN/g: Beware Horizontal Scrolling and Mimicking Swipe on Desktop](https://www.nngroup.com/articles/horizontal-scrolling/): Nutzer erwarten vertikales Scrollen; 2014, Desktop-Fokus.
6. [NN/g: Carousels on Mobile Devices](https://www.nngroup.com/articles/mobile-carousels/): angeschnittene Elemente als Signal, Abbruch nach drei bis vier Seiten; 2018.
7. [Apple HIG: Accessibility](https://developer.apple.com/design/human-interface-guidelines/accessibility): 44 x 44 pt Standard, 28 pt Minimum, Abstand 12 bzw. 24 pt. Abgerufen 2026-11-07.
8. [Android: Use window size classes](https://developer.android.com/develop/ui/compose/layouts/adaptive/use-window-size-classes): Breakpoints 600/840/1200/1600 dp. Abgerufen 2026-11-07.
9. [Android: Canonical layouts](https://developer.android.com/develop/ui/compose/layouts/adaptive/canonical-layouts): List-Detail ab expanded, Supporting Pane 70/30. Abgerufen 2026-11-07.
10. [Material Design 2: Understanding layout](https://m2.material.io/design/layout): 8-dp-Raster, 4 dp für kleine Elemente. Material 2, älter.
11. [SAP Fiori for Android: Layout Basics](https://www.sap.com/design-system/fiori-design-android/v26-4/foundations/adaptive-layout/layout-basics): 4-dp-Schritte, Ränder 16/24 dp. Sekundär zu Material 3.
12. [Baymard: Horizontal Filtering Toolbars, 2 Reasons to Be Cautious](https://baymard.com/blog/horizontal-filtering-sorting-design): 2015, aktualisiert 2025-03-13; Desktop.
13. [Baymard: How to Design Applied Filters](https://baymard.com/blog/how-to-design-applied-filters) und [Baymard: Ecommerce Filter UI](https://baymard.com/blog/ecommerce-filter-ui): Übersicht gesetzter Filter immer zeigen, reine Anzahl ("Filter (3)") vermeiden, Mobilformen der Übersicht, Filter im eigenen Layer, haftender Knopf. "How to Design Applied Filters" veröffentlicht 2020-10-06, aktualisiert 2026-05-13.
14. [NN/g: Visual Hierarchy in UX, Definition](https://www.nngroup.com/articles/visual-hierarchy-ux-definition/): Kontrast, Größe, Gruppierung; 2021.
15. [NN/g: Touch Targets on Touchscreens](https://www.nngroup.com/articles/touch-target-size/): 1 x 1 cm; 2019.
16. [NN/g: Dropdowns, Design Guidelines](https://www.nngroup.com/articles/drop-down-menus/): wann Dropdowns passen, Nachteil versteckter Optionen; 2017.
17. [Baymard: Line Length Readability](https://baymard.com/blog/line-length-readability): 50 bis 75 Zeichen; Studienlage gemischt.
18. [Hoober, UXmatters: How Do Users Really Hold Mobile Devices?](https://www.uxmatters.com/mt/archives/2013/02/how-do-users-really-hold-mobile-devices.php): Griffverteilung; 2013, vom Autor teilweise revidiert.
19. [YNAB: Updates to YNAB](https://support.ynab.com/en_us/updates-to-ynab-S1f4aRLeC.md): Card Mode iOS 2026-09-16, Android 2026-10-21.
20. [YNAB: Up Next](https://www.ynab.com/up-next): Details Card Mode (Knöpfe, Rückgängig, Überzug decken). Herstellerseite.
21. [YNAB: Spaces in (and out of) the Mobile App](https://support.ynab.com/en_us/spaces-in-the-mobile-app-S1iIZQoqgg.md): fünf Tabs, Add Transaction unten; Stand 2026-07-02.
22. [YNAB: At Home in YNAB](https://support.ynab.com/en_us/spotlight-BkdHBZUokg.md): Abschnitte des Home-Tabs; Stand 2026-10-20.
23. [Monarch: New mobile navigation](https://www.monarch.com/blog/new-mobile-navigation): fünf Hauptpunkte, Rest hinter Profil-Icon; 2022-02-18.
24. [Monarch: Brand refresh](https://www.monarch.com/monarch-brand-refresh): niedrigere Zeilen, dichtere Konten-Seite; 2024-12-12. Herstellerseite.
25. [NN/g: Bottom Sheets, Definition and UX Guidelines](https://www.nngroup.com/articles/bottom-sheet/): modal/nicht modal, Schließen-Knopf, nicht stapeln, Erreichbarkeit überschätzt; 2023.
26. [Android: Floating action button](https://developer.android.com/develop/ui/compose/components/fab): eine Hauptaktion, unten rechts.
27. [Android Accessibility Help: Touch target size](https://support.google.com/accessibility/android/answer/7101858): 48 x 48 dp, 8 dp Abstand.
28. [NN/g: How to Make Navigation (Even a Hamburger) Discoverable on Mobile](https://www.nngroup.com/articles/find-navigation-mobile-even-hamburger/): versteckt 44 %, kombiniert 89 % Nutzung; 2016.
29. [Android: Segmented button](https://developer.android.com/develop/ui/compose/components/segmented-button): zwei bis fünf Optionen, darüber Chips.
30. [SAP Fiori for Android: Filter Feedback Bar](https://www.sap.com/design-system/fiori-design-android/v26-4/components/filter-feedback-bar/usage): Chips umbrechend oder seitlich scrollend. Designsystem eines Dritten.
31. [Copilot Help: Dashboard tab overview](https://intercom.help/copilotmoney/en/articles/6045480-dashboard-tab-overview): Reihenfolge der Dashboard-Abschnitte, To Review; Stand 2026-04-14.
32. Copilot Help Center zu To Review (über Suche gefunden, Wortlaut aus dem Suchauszug, nicht direkt abgerufen): hellblaue Markierung ungeprüfter Buchungen.
33. [Copilot Changelog](https://changelog.copilot.money): Begrenzung der To-Review-Karte am Mac auf 25. Über Suchauszug, nicht direkt abgerufen.
34. [Monarch: Customizable dashboard and manual transactions](https://www.monarch.com/customizable-dashboard-manual-transactions): Dashboard am Handy getrennt anpassbar; 2021-03-04.
35. [Lunch Money Knowledge Base: Transactions, other features](https://support.lunchmoney.app/finances/transactions/other-features): Filter-Knopf über Review Transactions. Über Suchauszug.
36. [Lunch Money Feedback: Filtering is broken when actioned from filtered account transactions view](https://feedback.lunchmoney.app/bugs/p/filtering-is-broken-when-actioned-from-filtered-account-transactions-view): Filterzustand am Knopf falsch. Nutzerbericht.
37. [reisetopia: Finanzguru](https://reisetopia.de/guides/finanzen/finanzguru/): Übersicht aller Konten. Drittquelle, Rezension.
38. [NavigationBarItem, Compose Material 3 Referenz (Android Developers)](https://developer.android.com/reference/kotlin/androidx/compose/material3/NavigationBarItem.composable): Label-Empfehlung je Anzahl der Ziele (drei, vier, fünf). Abgerufen 2026-11-07.

## Open questions

- **Material-3-Primärseiten** (m3.material.io) waren nicht abrufbar (404 oder nur per JavaScript). Seitenränder,
  Pane-Breiten des List-Detail-Layouts in dp und die M3-Abstandsskala sind deshalb nur über Android-Doku und SAP
  belegt.
- **Zeilenhöhen dichter Listen am PC:** keine Primärquelle mit Zahlen gefunden. Der Wert bleibt eine
  Designentscheidung, gemessen an Lesbarkeit und dem Styleguide.
- **Zähler am Filter-Knopf:** Baymard rät, eine Zahl allein zu vermeiden, und empfiehlt sie nur neben einer
  sichtbaren Übersicht der gesetzten Filter [13]. Offen ist, ob ein Zähler neben dieser Übersicht in BB noch etwas
  bringt; dazu fand ich keinen eigenen Test.
- **Kollabierender Kopf:** keine Evidenz gefunden, weder dafür noch dagegen.
- **Finanzguru, Revolut, Copilot-Wischgesten, Monarch-Filter am Handy:** keine Primärquellen. Um das zu klären,
  müsste jemand die Apps selbst ansehen (Screenshots); Websuche reicht dafür nicht.
- **Daumenzone:** die Griffdaten stammen von 2013 und kleineren Geräten; neuere Messungen habe ich nicht gesucht.
