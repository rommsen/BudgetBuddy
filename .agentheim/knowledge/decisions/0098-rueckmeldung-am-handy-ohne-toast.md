---
id: 0098
title: Rückmeldung am Handy ohne Toast (Regel R3), eine Meldung an einem festen Platz
scope: global
status: accepted
date: 2026-11-19
supersedes: []
superseded_by: []
related_tasks: [ledger-vf5h6, design-system-ytgnc, design-system-gkded, design-system-vf94m, ledger-pz5wv, ledger-mf2bg, ledger-gwr9j, ledger-p5y9s, ledger-enjex, ledger-k68sd, budgeting-gtrgk, budgeting-y1kkr, budgeting-sx9v5, budgeting-p3sk0, categorization-t6j00, banking-import-vwhfm, banking-import-ns4re]
related_adrs: [0087, 0007]
related_research: []
---

# ADR 0098: Rückmeldung am Handy ohne Toast (Regel R3)

## Context

Am Handy stehen heute oft zwei Toasts zugleich: einfache Toasts oben (`Toast.container`, `top-16`), Toasts mit
„Rückgängig“ unten über der Navigation (`actionContainer`, `toast-above-nav`). Jede Rückmeldung ist ein eigener
Eintrag einer Liste (`State.Model.Toasts`) und verschwindet nach 5 s. Romans Screenshots vom 2026-11-19 aus dem
echten Kassenbuch zeigen zwei Toasts gleichzeitig, oben der Spiegel-Lauf und unten der Wisch, und nach mehreren Wischen
drei gestapelte Toasts über Liste und „+ Buchung“. Im Durchgehen liegt der Toast der vorigen Aktion auf „Passt, weiter“,
man muss warten, bis er weg ist. design-system-vf94m (Toast verdeckt „+ Buchung“) war dazu offen.

Die Design-Exploration ledger-vf5h6 hat drei Regeln gezeigt (Skizze
https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 7 mit allen Optionen, Version 9 freigegeben).

## Decision

Entschieden von Roman am 2026-11-19 (Freigabe der Skizze Version 9, „Kein Toast“): **Regel R3. Am Handy gibt es
keinen Toast. Jede Rückmeldung hat einen festen Platz.** Die Plätze in dieser Reihenfolge:

1. **Ergebnis am Ort sichtbar:** die Stelle bestätigt selbst. Ein Feld leuchtet kurz, darunter steht die Bestätigung
   mit „Rückgängig“ (etwa „Bahn · 84,20 € verfügbar · Rückgängig“). Wechselt ein Knopf sichtbar (Lena fragen), ist er
   selbst die Bestätigung, ohne Text.
2. **Sheet offen:** eine Zeile im Sheet über dem Hauptknopf, nie darüber.
3. **Sonst: die Meldungsleiste über der Navigation.** Auf Seiten mit „+ Buchung“ trägt die Leiste den Knopf, der
   schwebende Knopf entfällt, die Liste endet über der Leiste. Kommt eine Meldung, wird der Knopf zum runden „+“, links
   steht die Meldung in zwei Zeilen: oben die Aktion (nie gekürzt), darunter Name oder Grund (gekürzt mit „…“), rechts
   „Rückgängig“ oder „Erneut“. Seiten ohne „+ Buchung“ zeigen die Leiste nur, solange es eine Meldung gibt.

Dazu gilt:

- **Immer nur eine Meldung.** Gleiche Aktionen hintereinander werden eine („3 geprüft“, „Rückgängig“ nimmt alle
  zurück), eine andere Aktion ersetzt sie.
  Ausnahme Durchgehen: dort steht die letzte Aktion unter „Passt, weiter“ und wird nicht zusammengefasst (Skizze 08),
  weil „Rückgängig“ die eine Karte zurückholt (ledger-pz5wv).
- **Sie bleibt bis zur nächsten Aktion oder bis man die Seite verlässt:** kein ✕, kein Zeitlimit.
- **Fehler** stehen rot am selben Platz und bleiben bis zur nächsten Aktion. YNAB-Warnungen stehen gelb dort.
- **Ein Zustand der Seite ist keine Meldung,** sondern ein Hinweis auf der Seite (etwa „Beträge nicht neu gelesen“
  unter „Zu verteilen“, Muster wie die alten Kontostände im Kassenbuch, ledger-pfhdq).
- **Gelöschtes in einer Liste** hinterlässt an seinem Platz eine schmale Zeile („„Regel drei“ gelöscht ·
  Rückgängig“) bis zur nächsten Aktion.
- **Am PC (ab 1024 px) bleibt der Toast unten rechts** (Styleguide §7.13), höchstens einer zugleich.
  Entscheidung Roman (2026-11-19) bei der Verfeinerung: Rückmeldungen, deren Ergebnis man am Ort sieht, stehen auch am
  PC am Ort und ohne zusätzlichen Toast (gelöschte Zeile am Platz, „✓ Gespeichert“ am Feld, Aufleuchten, Pin-Knopf,
  Hinweis „nicht neu gelesen“, letzte Aktion unter „Passt, weiter“ im Durchgehen, Rückgängig am Feld). Den Toast unten
  rechts gibt es am PC nur noch für Meldungen ohne Ort, also dort, wo am Handy die Leiste steht. Er ist höchstens einer,
  geht nach 5 s und hat ein ✕, wie heute.

Der Baustein im Design-System heißt `Feedback` (`src/Client/DesignSystem/Feedback.fs`, design-system-ytgnc). Die App
hält genau einen Platz für die Meldung, Handy-Leiste und PC-Toast zeigen denselben Zustand (design-system-gkded). Die
Seiten setzen die genaueren Plätze 1 und 2 in eigenen Tasks (ledger-pz5wv, ledger-mf2bg, ledger-gwr9j, ledger-p5y9s,
ledger-enjex, ledger-k68sd, budgeting-gtrgk, budgeting-sx9v5, budgeting-p3sk0, categorization-t6j00,
banking-import-vwhfm, banking-import-ns4re).

## Verworfene Alternativen

- **R1: Toast nur dort, wo man das Ergebnis nicht sieht.** Behält den Toast als Form und damit seine Probleme: er liegt
  über Inhalt und Knöpfen, und auf Seiten mit vielen Aktionen ohne sichtbares Ergebnis stehen weiter mehrere zugleich.
  Romans Screenshots vom 2026-11-19 zeigen genau das: zwei Toasts gleichzeitig, einer oben, einer unten.
- **R2: alle Toasts klein oben.** Löst das Verdecken unten, aber nicht das Stapeln: nach mehreren Wischen stünden drei
  Toasts übereinander, wie auf Romans Screenshot mit drei gestapelten Toasts über Liste und „+ Buchung“. Oben liegen
  außerdem Kopf und Abrufen.

## Gründe

- Ein fester Platz verdeckt nichts, weil die Seite ihn freihält. Toasts liegen immer über etwas.
- Eine Meldung statt einer Liste: Romans Screenshots zeigen, dass mehrere Toasts zugleich nicht lesbar sind.
- Ohne Zeitlimit muss man nicht warten, bis eine Meldung weg ist, und verpasst kein „Rückgängig“.
- Am PC ist unten rechts Platz neben Liste und Panel; dort stört der Toast nicht, sobald es höchstens einer ist.

## Consequences

- Styleguide §7.13 wird neu geschrieben, §5 bekommt die Zeile `Feedback`, die Zeile `Toast` gilt nur noch ab 1024 px
  und dort nur für Meldungen ohne Ort.
  ADR 0087 Punkt 22 („Rückmeldungen verdecken nie Bedienelemente“) bleibt und wird hier für das Handy konkret.
- Die Toast-Liste im App-Modell wird ein einzelner Platz. Bis jede Seite umgestellt ist, landen ihre bisherigen
  Toasts am Handy in der Leiste (Übergang, design-system-gkded).
- Der Layout-Sweep prüft, dass am Handy kein Toast steht und keine Rückmeldung Inhalt oder Bedienelement verdeckt, mit
  Gegenbeweis. Das ersetzt den Regressionstest aus design-system-vf94m.
- Englische Toast-Texte fallen mit der Umstellung der Einstellungen weg (banking-import-vwhfm).
