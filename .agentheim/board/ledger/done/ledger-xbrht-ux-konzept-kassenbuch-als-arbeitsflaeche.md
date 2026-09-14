---
id: ledger-xbrht
title: UX-Konzept Kassenbuch als Arbeitsfläche (Handy und PC, mindestens YNAB-Niveau)
status: done
type: spike
context: ledger
created: 2026-09-11
completed: 2026-09-12
depends_on: [design-system-h58y4, design-system-001, design-system-y6gkd, design-system-f7k2p]
blocks: [ledger-s8m3w]
tags: [frontend, ux, kassenbuch, claude-design]
related_adrs: [0013, 0015, 0021]
related_research: []
prior_art: [ledger-q5w9c, ledger-kt0wq]
---

## Why
Der Walking Skeleton hat die Architektur bewiesen, nicht die Oberfläche (so gewollt, siehe
`ledger-p3n8k`). Roman sagt dazu am 2026-09-11: "Nur reingucken, dafür würde ich es
wahrscheinlich gar nicht öffnen." Ein Kassenbuch, in dem man nichts tun kann, läuft genau
in das Scheiterkriterium der Vision ("Kassenbuch gebaut, aber niemand schaut hinein").
Die Arbeit, für die Roman heute YNAB öffnet, soll im Kassenbuch mindestens so gut gehen
wie dort, mit besserer UX.

Bevor weitere Screens entstehen (Gesamtsicht, Filter, Bearbeiten), braucht es deshalb ein
UX-Konzept für das **ganze Ziel**, auch wenn in Scheiben gebaut wird. Sonst wird jede
Scheibe für sich entworfen und die Oberfläche später umgebaut.

## What
Ein UX-Konzept **für das ganze Ziel**, festgehalten im `ledger/README.md` und in Briefs je
Ablauf. Gebaut wird danach in Scheiben; das Ableiten der Scheiben ist der Folge-Task
`ledger-s8m3w`, nicht Teil dieses Tasks (Entscheidung Roman, 2026-09-11).

**Zuschnitt geändert am 2026-09-12 (Entscheidung Roman).** Ursprünglich sollte ein
klickbarer Prototyp über alle sieben Abläufe in Claude Design entstehen und dort
durchgespielt werden. Das ist gestrichen, aus drei Gründen:
1. Claude kann Mockup-Projekte nicht lesen (`DesignSync` erreicht nur
   Design-System-Projekte). Jede Rückkopplung bräuchte Roman als Boten; bei sieben
   Abläufen mit Iterationen sind das Dutzende Screenshots von Hand.
2. Ein Mockup mit erfundenen Daten kann das Maß "mindestens so schnell wie YNAB" nicht
   belegen, und die Frage nach dem Verhalten bei rund 14.000 Buchungen beantwortet es
   grundsätzlich nicht. Die echte App mit Romans Bestand beantwortet beides.
3. Der eigentliche Wert des Tasks ist eingetreten, bevor ein einziger Screen entstand: die
   Konzept-Skizze aus Schritt 3 der Session, mit Roman abgestimmt.

**Stattdessen:** das Konzept und die Briefs werden festgehalten, der Durchgang wandert in
die **erste lesende Bau-Scheibe** (aus `ledger-s8m3w`), wo Roman ihn an echten Daten
durchspielt. Claude Design bleibt als Werkzeug für einzelne Layout-Fragen verfügbar, auf
Zuruf und nicht als Pflichtstation je Ablauf. Der Design-System-Sync (`design-system-h58y4`)
bleibt davon unberührt und hat seinen Wert unabhängig.

**Geräte:** insgesamt etwa 70 % Handy, 30 % PC. Die Nachbearbeitung passiert fast nur am
PC; am Handy ist der Hauptfall das Durchsehen und Bestätigen neuer Buchungen.

**Arbeitsabläufe, die das Konzept abdecken muss** (Romans heutige YNAB-Nutzung):

1. **Durchsehen und als geprüft markieren** (Handy zuerst). Roman markiert Buchungen als
   "habe ich mir angesehen", vor allem die ING-Buchungen, die über YNABs Direktimport
   automatisch kommen. Das Geld ist durch den Import immer schon bei der Bank angekommen;
   die Markierung heißt für Roman "geprüft".
2. **Buchungen finden** (PC). Filter nach Häufigkeit: Kategorie etwa 70 bis 80 %, Zeitraum
   etwa 15 %, Payee, Betrag und Memo je etwa 5 % (Betrag und Payee vor allem, wenn eine
   Buchung sich nicht findet).
3. **Mehrfachauswahl mit Summe** (PC). Z. B. für die Steuererklärung: Kategorie wählen, die
   letzten zwölf Buchungen markieren, Gesamtbetrag der Auswahl sehen. Wird viel genutzt.
4. **Kategorie ändern** (PC), weil sie falsch war oder am Ende anders zugeordnet wird;
   **Split festlegen oder ändern**.
5. **Flags**: vor allem ein Rückfrage-Flag für Buchungen, die Romans Frau sich ansehen muss
   (in YNAB heißt es "Lena"); selten ein temporäres Flag zum Gruppieren.
6. **Gesamtsicht und Konto-Sicht** mit Kontoständen (v1-Kriterium der Vision).
7. **Was nur BB kann**: Herkunft und Historie einer Buchung, gelöschte Buchungen
   (`ledger-p3n8k`), später Abweichung zwischen BB und YNAB (v2). Dazu gehört die Markierung
   "noch nicht in YNAB" für Buchungen ohne YNAB-Verknüpfung (Push ausstehend oder
   gescheitert, ADR 0021); gewollt von Roman am 2026-09-11.

**Arbeitsteilung (Entscheidung Roman, 2026-09-11):** Roman baut die Screens und den
Prototyp in claude.ai/design mit dem synchronisierten "BudgetBuddy Design System". Claude
schreibt dafür je Ablauf einen **Brief** und hält das Konzept im Repo fest. Claude kann
Mockup-Projekte in Claude Design nicht lesen (`DesignSync` erreicht nur
Design-System-Projekte); Roman zeigt Zwischenstände als Screenshot oder Export.

**Ablauf der Session** (Hauptsession mit Roman, in dieser Reihenfolge):
1. **Re-Sync** des Design-Systems nach dem Ablauf im design-system-README
   (`npm run export:design-system`, `DesignSync`-Plan, Roman gibt frei). Sichtprüfung in
   Claude Design: die Warnung zu fehlenden Schriften ist weg, die Kategorienauswahl und die
   übrigen Styleguide-Sektionen sind als Karten da.
2. **YNAB-Referenz:** Roman beschreibt je Ablauf kurz, wie er ihn heute in YNAB-Web bzw. der
   YNAB-App macht (Gerät, Schritte, was stört). Das ist der Maßstab für "mindestens so gut".
3. **Konzept-Skizze** mit Roman abstimmen, bevor Briefs entstehen: Einstieg, Navigation
   zwischen Gesamt- und Konto-Sicht, Aufbau der Liste auf Handy und PC, Auswahl-Modus, wo
   Bearbeiten, Historie und Flags sitzen, und wie sich die Liste bei rund 14.000 Buchungen
   verhält (Laden, Scrollen, Filtern; 13.864 am echten Budget).
4. **Briefs** je Ablauf (Claude): Ziel, Gerät und Viewport, Ausgangszustand, Schrittfolge,
   erfundene Konten, Payees und Beträge, Pflicht-Komponenten aus dem Design-System (z. B.
   Kategorienauswahl, Money, Badge, BottomSheet), der YNAB-Referenzweg aus Schritt 2 und was
   nur BB zeigt (Herkunft, Historie, "noch nicht in YNAB").
5. **Prototyp:** Roman erzeugt die Screens in einem eigenen Claude-Design-Projekt (das
   Design-System-Projekt selbst bleibt unverändert) und verbindet sie zu einem klickbaren
   Prototyp.
6. **Durchgang:** Roman spielt die Abläufe im Prototyp durch; Befunde fließen in Briefs und
   Prototyp zurück, bis er zufrieden ist oder die Lücke benannt ist.
7. **Festhalten:** Konzept im `ledger/README.md`, Briefs und Screenshots im Repo.

## Acceptance criteria
- [x] Re-Sync erledigt: Claude Design meldet keine fehlenden Schriften, die
      Kategorienauswahl ist als Karte im "BudgetBuddy Design System" sichtbar.
      (2026-09-12, Sichtprüfung Roman; Manifest: 23 Karten, drei Schriften "ok".
      Zwei Funde als Backlog: design-system-hadjy, design-system-an22r.)
- [x] Für jeden der sieben Abläufe liegt ein Brief in `docs/ux/kassenbuch/briefs.md`,
      jeweils mit dem YNAB-Referenzweg aus Romans Beschreibung.
- [x] Das UX-Konzept steht im `ledger/README.md` (Abschnitt "Oberfläche"): Einstieg,
      Navigation, Listenaufbau je Gerät, Auswahl-Modus, Orte für Bearbeiten, Historie und
      Flags, Verhalten der Liste bei rund 14.000 Buchungen.
- [x] Die Briefs enthalten nur erfundene Konten, Payees und Beträge, keine echten Daten.
- [x] Was das Konzept an Fähigkeiten voraussetzt, die es heute nicht gibt, ist im
      Konzept benannt (Prüfvermerk und Flags im Spiegel, Available-Betrag je Kategorie,
      seitenweises Laden mit serverseitiger Summe), damit `ledger-s8m3w` daraus Scheiben
      schneiden kann.
- [x] Roman hat das Konzept gelesen und bestätigt (Human-Gate). Der Durchgang an echten
      Daten ist ausdrücklich in die erste lesende Scheibe verschoben.

## Notes
- **Kein Worker-Task.** Läuft in der Hauptsession mit Roman, wie `design-system-h58y4`:
  `work` claimt den Task, dispatcht aber keinen Worker; Human-Gate statt Verifier. Kann
  sich über mehrere Sessions ziehen und bleibt dazwischen in `doing/`.
- **"Geprüft" ist fachlich Romans Prüfvermerk,** technisch vermutlich YNABs `approved`.
  Der Spiegel kennt heute weder `approved` noch Flags; der Prototyp darf beides zeigen, die
  Umsetzung klärt `ledger-s8m3w` (Details dort).
- Schreibende Abläufe (geprüft, Kategorie, Split, Flags) brauchen später den
  Rückschreibe-Weg nach YNAB und die Abweichungs-Sicht (Vision-Stufe v2). Der Prototyp
  entwirft sie trotzdem vollständig; ob sie vor v2 gebaut werden, entscheidet
  `ledger-s8m3w`.
- Die Vision nennt als Scheiterkriterium auch "wenn BB langsamer oder umständlicher wird
  als YNAB-Web". Deshalb gehört das Verhalten der Liste bei Tausenden Buchungen ins
  Konzept und nicht erst in die Umsetzung.
- **Verfeinerung 2026-09-11 (nach `design-system-y6gkd` und `-f7k2p`):** alle
  Abhängigkeiten erledigt. Roman entschied: Mockups in Claude Design mit Briefs von
  Claude, Maßstab ist ein klickbarer Prototyp statt eines Bauchgefühls beim Durchsehen
  statischer Screens, Scheiben und v1/v2-Entscheidung wandern in den Folge-Task
  `ledger-s8m3w`.

## Outcome

**Was entstanden ist**
- Re-Sync des Design-Systems nach Claude Design: 33 Dateien im Projekt "BudgetBuddy
  Design System", Manifest mit 23 Karten (Brand 1, Colors 2, Type 1, Components 14,
  Feedback 3, Forms 2), alle drei Schriften als "ok" erkannt, Kategorienauswahl als
  Karte `components-categorypicker` vorhanden. Sichtprüfung durch Roman am 2026-09-12.
- `docs/ux/kassenbuch/briefs.md`: Briefs für die Abläufe 1 bis 7, die ersten fünf mit
  Romans YNAB-Referenzweg aus seiner eigenen Beschreibung; 6 und 7 tragen die Struktur,
  ihr Referenzweg blieb offen (Ablauf 7 hat in YNAB ohnehin keinen).
- `docs/ux/kassenbuch/claude-design-kickoff.md`: Kontext-Briefing für ein Mockup-Projekt.
  Nach dem Zuschnitt-Wechsel nur noch für einzelne Layout-Fragen auf Zuruf.
- `ledger/README.md`, Abschnitt "Oberfläche": das Konzept in dauerhafter Form.

**Zwei Funde am Export-Weg** (beide als Backlog erfasst)
- `design-system-hadjy`: der Export trägt die `@font-face`-Regeln der App mit absolutem
  `url(/fonts/...)` in `styles.css`. In Claude Design liegen die Previews unter
  `.../serve/preview/`, die absolute URL zeigt damit auf die Origin-Wurzel und liefert
  404. Weil `styles.css` nach `colors_and_type.css` geladen wird, gewinnt die kaputte
  Regel. Workaround in dieser Session: Bundle von Hand gepatcht, nur `styles.css` neu
  hochgeladen.
- `design-system-an22r`: Claude Design baut das Manifest **nicht** nach einem
  DesignSync-Upload neu, sondern erst auf eine Anfrage im Projekt-Chat. Dabei hat es 26
  Bundle-Dateien umgeschrieben (`@dsCard`-Marker um `name`/`subtitle`/`viewport`
  ergänzt, `thumbnail.html` angelegt, Tailwind-Interna in `styles.css` annotiert). Der
  nächste Export überschreibt das wieder, deshalb soll der Export es selbst erzeugen.

**Zuschnitt-Wechsel am 2026-09-12 (Entscheidung Roman).** Der klickbare Prototyp über
alle sieben Abläufe ist gestrichen; Begründung im Abschnitt "What". Roman brachte es auf
den Punkt: "ständig Screenshots zu machen ist doch wild". Der Durchgang wandert an echte
Daten in die erste lesende Scheibe aus `ledger-s8m3w`. Brief 1 war zu diesem Zeitpunkt in
Claude Design gebaut und diente als Beleg, dass der Weg funktioniert, aber teuer ist.

**Human-Gate.** Die Konzept-Skizze wurde am 2026-09-11/12 Punkt für Punkt mit Roman
abgestimmt (Einstieg im zuletzt gewählten Konto, Prüfen per Wisch, Seitenpanel am PC,
Split nur im Detail), dazu die YNAB-Referenzwege 1 bis 5 aus seiner Beschreibung. Der
README-Abschnitt "Oberfläche" ist die Verschriftlichung genau dieser Abstimmung.

**Was offen bleibt**
- YNAB-Referenzweg für Ablauf 6 (Gesamtsicht, Konto-Sicht, ETF-Konto) fehlt in den
  Briefs; bei der Scheibe nachzuholen.
- Die drei Voraussetzungen aus dem README-Abschnitt (Prüfvermerk und Flags im Spiegel,
  Available je Kategorie, seitenweises Laden mit serverseitiger Summe) sind
  Scheiben-Material für `ledger-s8m3w`, nicht hier entschieden.
