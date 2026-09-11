---
id: ledger-xbrht
title: UX-Konzept Kassenbuch als Arbeitsfläche (Mockups Handy und PC, mindestens YNAB-Niveau)
status: backlog
type: spike
context: ledger
created: 2026-09-11
completed:
depends_on: [design-system-h58y4, design-system-001]
blocks: []
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
Ein UX-Konzept mit Mockups, entworfen **für das Ziel** (Entscheidung Roman, 2026-09-11):
die Mockups zeigen den ganzen Arbeitsablauf einschließlich Bearbeiten. Gebaut wird danach
in Scheiben, zuerst die lesenden Teile.

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

**Ergebnis des Tasks:**
- Mockups für jeden Ablauf auf seinem Hauptgerät (1 auf dem Handy, 2 bis 5 auf dem PC, 6
  auf beiden), als Canvas in Claude Design auf Basis des synchronisierten
  "BudgetBuddy Design System" (`design-system-h58y4`) oder mit dem `design`-Skill.
- Ein kurzes UX-Konzept (Einstieg, Navigation zwischen Gesamt- und Konto-Sicht, Aufbau der
  Liste auf beiden Geräten, Auswahl-Modus, wo Bearbeiten, Historie und Flags sitzen).
- Daraus abgeleitete Scheiben als Backlog-Tasks, zuerst die lesenden (Gesamtsicht, Filter,
  Auswahl-Summe), danach die schreibenden (geprüft, Kategorie, Split, Flags).

## Acceptance criteria
- [ ] Für jeden der sieben Abläufe gibt es ein Mockup auf seinem Hauptgerät; Ablauf 1 und 6
      zusätzlich in der jeweils anderen Größe.
- [ ] Roman ist die Abläufe 1 bis 5 in den Mockups durchgegangen und hat bestätigt, dass
      sie mindestens so schnell gehen wie in YNAB-Web bzw. der YNAB-App, oder benannt, was
      fehlt (Human-Gate).
- [ ] Das UX-Konzept ist im `ledger/README.md` (Abschnitt Oberfläche) oder als
      Concept-Seite festgehalten, mit Verweis auf die Mockups.
- [ ] Die Scheiben sind als Backlog-Tasks im `ledger` erfasst, jede mit Styleguide-Gate
      (`design-system-001`) und Verweis auf ihr Mockup; `ledger-p3n8k` ist darin
      aufgegangen oder ausdrücklich bestätigt.
- [ ] Ob schreibende Scheiben (geprüft, Kategorie) nach v1 vorgezogen werden, ist mit Roman
      entschieden; wenn ja, ist die Vision angepasst.
- [ ] Mockups enthalten erfundene Konten und Beträge, keine echten Daten.

## Notes
- **"Geprüft" ist fachlich Romans Prüfvermerk.** Technisch ist es vermutlich YNABs
  `approved` (Direktimport-Buchungen wie ING kommen unbestätigt an); `cleared` trägt für
  Roman keine Information, weil importierte Buchungen immer schon bei der Bank sind. Beim
  Modellieren der schreibenden Scheibe festzurren.
- **Der Spiegel kennt heute weder `approved` noch Flags.** Der Ereignis-Katalog
  (`ledger/README.md`, "Ereignisse") beobachtet Details, Kategorisierung, Cleared und
  Löschung. Schon eine lesende Anzeige "noch nicht geprüft" oder "Rückfrage-Flag" braucht
  eine Erweiterung von Snapshot und Beobachtungs-Ereignissen. Das wird eine eigene Scheibe.
- Schreibende Scheiben heißen "BB entscheidet, YNAB wird nachgezogen": BB braucht dafür
  den Rückschreibe-Weg nach YNAB (Update bestehender Buchungen) und die Abweichungs-Sicht.
  Das ist Vision-Stufe v2 und größer als eine UI-Scheibe.
- Die Vision nennt als Scheiterkriterium auch "wenn BB langsamer oder umständlicher wird
  als YNAB-Web". Die Liste enthält Tausende Buchungen (13.864 am echten Budget); wie sie
  sich beim Filtern und Scrollen anfühlt, gehört ins Konzept.
- Kein Worker-Task: läuft in der Hauptsession mit Roman, wie `design-system-h58y4`.
  Bleibt im Backlog, bis der Sync erledigt ist.
