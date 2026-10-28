---
id: 0068
title: Bob-Grenzen über den Tag, Datumsfenster und Drosselung der Bot-Schnittstelle
scope: ledger
status: accepted
date: 2026-10-28
supersedes: []
superseded_by: []
related_tasks: [ledger-ybm5x, ledger-ssmp3, ledger-bbkgs, ledger-dtrn2]
related_adrs: [0066, 0067]
related_research: []
---

# ADR 0068: Bob-Grenzen über den Tag, Datumsfenster und Drosselung der Bot-Schnittstelle

## Context

ADR 0066 und ADR 0067 begrenzen Bob je Buchung: nur das Quick-Add-Konto, höchstens
`BOT_MAX_AMOUNT_EUR` (Vorgabe 500 €). Ein Security-Review der Homelab-Seite (2026-10-28) hat
gezeigt, dass das allein wenig begrenzt. Ein per Prompt-Injection gesteuerter Bob kann beliebig
viele Buchungen bis zur Obergrenze anlegen, als Ausgabe oder Einnahme, und bis zu fünf Jahre
rückdatiert. Außerdem fragt jede Vorschau die Kategorien live bei YNAB ab; eine Schleife bei Bob
verbraucht das YNAB-Ratenlimit, das Sync und Quick Add mitbenutzen. Und
`BOT_MAX_AMOUNT_EUR` liest "500,00" als 50000, weil der Parser Tausendertrennzeichen zulässt.

Bob-Buchungen kommen in YNAB ohne `approved` an und stehen dort deshalb unbestätigt in der
Prüfliste. Das bleibt das Prüfsignal für Roman.

## Decision

1. **Obergrenze je Buchung mit hartem Deckel.** Im Code gilt höchstens 500 € je Buchung.
   `BOT_MAX_AMOUNT_EUR` darf diesen Wert nur absenken. Der Wert wird nur mit Dezimalpunkt gelesen
   (kein Tausendertrennzeichen). Ungültige oder zu hohe Werte fallen auf 500 € zurück. Der
   wirksame Wert steht beim Start im Log.
2. **Tagesgrenze.** Je Kalendertag (Europe/Berlin) höchstens 500 € Summe der Beträge (Ausgaben
   und Einnahmen zusammen, als Betrag ohne Vorzeichen) und höchstens 10 gebuchte Bob-Commits.
   Gezählt werden die im Kassenbuch gespeicherten Buchungen mit Actor `Bob`, damit ein Neustart
   die Zählung nicht zurücksetzt. Die Werte sind im Code fest, nicht konfigurierbar.
3. **Datumsfenster.** Das Buchungsdatum liegt zwischen heute minus 31 Tage und heute.
4. **Drosselung und Cache.** Höchstens 10 Anfragen pro Minute auf `/bot/`, auch nicht
   angemeldete; darüber 429 mit `Retry-After`. Die YNAB-Kategorien für die Bot-Schnittstelle
   werden 5 Minuten zwischengespeichert.
5. **Keine eigene Meldung und kein Flag.** BB schickt bei Bob-Buchungen keine Benachrichtigung.
   Bob-Buchungen bekommen kein `flag_color`, weil Flags für "Lena fragen" belegt sind (ADR 0056).

## Consequences

- Ein kompromittierter Bob kann am Tag höchstens 500 € in Summe und 10 Buchungen anlegen, nur aufs
  Barkonto, höchstens 31 Tage zurück, alles sichtbar an der Herkunft `Bob` und in YNAB
  unbestätigt.
- Für größere oder ältere Barbuchungen nimmt Roman Quick Add.
- Die Grenzen der Bot-Schnittstelle sind nur im Code einstellbar; eine Änderung braucht einen
  Deploy. Das ist gewollt.
- Umgesetzt in ledger-ybm5x (Punkt 1), ledger-ssmp3 (Punkte 2 und 3), ledger-bbkgs (Punkt 4) und
  ledger-dtrn2 (Pfadabgleich unter `/bot`).
