---
id: ledger-xbrht
title: UX-Konzept und klickbarer Prototyp Kassenbuch als Arbeitsfläche (Handy und PC, mindestens YNAB-Niveau)
status: doing
type: spike
context: ledger
created: 2026-09-11
completed:
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
Ein UX-Konzept mit einem **klickbaren Prototyp**, entworfen **für das Ziel** (Entscheidung
Roman, 2026-09-11): der Prototyp zeigt den ganzen Arbeitsablauf einschließlich Bearbeiten.
Gebaut wird danach in Scheiben; das Ableiten der Scheiben ist der Folge-Task
`ledger-s8m3w`, nicht Teil dieses Tasks (Entscheidung Roman, 2026-09-11).

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
- [ ] Re-Sync erledigt: Claude Design meldet keine fehlenden Schriften, die
      Kategorienauswahl ist als Karte im "BudgetBuddy Design System" sichtbar.
- [ ] Für jeden der sieben Abläufe liegt ein Brief in `docs/ux/kassenbuch/briefs.md`,
      jeweils mit dem YNAB-Referenzweg aus Romans Beschreibung.
- [ ] Ein klickbarer Prototyp in Claude Design deckt alle sieben Abläufe auf ihrem
      Hauptgerät ab (1 auf dem Handy, 2 bis 5 auf dem PC, 6 auf beiden); Ablauf 1 und 6
      zusätzlich in der jeweils anderen Größe.
- [ ] Roman hat die Abläufe 1 bis 5 im Prototyp durchgespielt und bestätigt, dass sie
      mindestens so schnell gehen wie in YNAB-Web bzw. der YNAB-App, oder benannt, was
      fehlt; Benanntes ist eingearbeitet oder als offener Punkt im Konzept vermerkt
      (Human-Gate).
- [ ] Das UX-Konzept steht im `ledger/README.md` (Abschnitt "Oberfläche"): Einstieg,
      Navigation, Listenaufbau je Gerät, Auswahl-Modus, Orte für Bearbeiten, Historie und
      Flags, Verhalten der Liste bei rund 14.000 Buchungen, Link auf das
      Claude-Design-Projekt des Prototyps.
- [ ] Screenshots der Prototyp-Screens liegen unter `docs/ux/kassenbuch/`, damit die
      Scheiben aus `ledger-s8m3w` auf sie verweisen können.
- [ ] Briefs, Prototyp und Screenshots enthalten nur erfundene Konten, Payees und
      Beträge, keine echten Daten.

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
