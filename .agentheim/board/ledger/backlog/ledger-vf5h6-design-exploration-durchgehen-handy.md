---
id: ledger-vf5h6
title: "Design-Exploration Durchgehen am Handy: Wischen mit Bewegung und sichtbarer Aktion darunter, Bestätigen ohne störenden Toast, Memo auf der Karte ganz lesbar. Ergebnis ist eine Skizze mit Optionen zur Freigabe durch Roman, danach Tasks je Teil."
status: backlog
type: spike
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: [design-system-vf94m]
tags: [design-exploration, durchgehen, toast, wischen, frontend]
related_adrs: [0086, 0087]
related_research: []
prior_art: [ledger-v6s7c, ledger-th36h, ledger-nc708]
---

## Why
Romans Eindruck vom Durchgehen am Handy (2026-11-19, nach dem Lauf zum Bestandsreview ledger):

1. **Wischen fühlt sich nicht nach Wischen an.** Man hat nicht das Gefühl, dass man die Karte wirklich nach links oder
   rechts wischen kann. Es gibt keine Bewegung der Karte und keine Animation, und darunter fehlt die Darstellung, was
   passiert, wenn man weiter wischt. Im Kassenbuch gibt es das schon (Wisch geprüft, ledger-th36h: die Zeile zeigt beim
   Wischen die Aktion darunter).
2. **Toasts sind hässlich und stehen im Weg**, besonders im Durchgehen: der Toast der vorigen Aktion liegt über
   „Passt, weiter“, man muss warten, bis er weg ist. Frage von Roman: braucht man überhaupt für alles einen Toast? Geht die
   Bestätigung schicker?
3. **Memo auf der Karte:** Roman tippt beim Memo auf `›` und erwartet den ganzen Text. Er bleibt aber genauso
   abgeschnitten wie auf der Karte; erst „Details“ zeigt ihn.

Roman: für alle drei braucht es eine Design-Exploration.

## What
Eine Skizze (claude.ai-Artifact, CLAUDE.md „Skizzen“) mit Optionen je Teil, zur Freigabe durch Roman. Der Conductor baut
sie in der Sitzung (Worker können keine Artifacts erstellen) und gleicht sie vor der Freigabe Funktion für Funktion mit
der heutigen Seite ab (Screenshot von BB daneben, Touch bei 412 px).

- **Wischen:** die Karte folgt dem Finger, dahinter erscheint die Aktion der Richtung (rechts „Passt“, links „Später“)
  in ihrer Farbe, ab der Schwelle (heute 90 px, `reviewSwipeOf`) sichtbar „rastet“ sie ein; Loslassen unter der Schwelle
  federt zurück, darüber fliegt die Karte raus und die nächste kommt. Vorbild und Abgleich: Wisch geprüft im Kassenbuch.
  Ohne Ziel (keine Kategorie) zeigt „Passt“ schon beim Wischen, dass es nicht geht. `prefers-reduced-motion` beachten.
- **Bestätigen:** welche Aktionen überhaupt eine Rückmeldung brauchen und welche nicht (die Karte ist weg, das ist schon
  die Bestätigung); wo „Rückgängig“ bleibt, wenn kein Toast mehr kommt (etwa kleine Zeile oder Knopf im Kopf „Letzte
  rückgängig“); wenn Toast, wo er steht, ohne „Passt, weiter“ oder den FAB zu verdecken (§7.13, design-system-vf94m).
  Ergebnis ist auch eine Regel für den Styleguide (wann Toast, wann nicht), nicht nur fürs Durchgehen.
- **Memo:** was `›` am Memo auf der Karte zeigt (ganzer Text in der Karte aufgeklappt, oder Memo-Editor mit ganzem Text),
  oder ob `›` dort fehlt, wenn es nichts anderes tut als „Details“.

Spike-Regel (ADR 0065): Zeigt sich mitten in der Exploration, dass eine Lösung schon klar und billig ist (etwa Memo `›`
öffnet den Editor mit dem ganzen Text), wird sie festgehalten und die Exploration für diesen Teil beendet.

## Acceptance criteria
- [ ] Skizze als claude.ai-Artifact mit je mindestens zwei Optionen für Wischen, Bestätigen und Memo, bedienbar mit Touch
      bei 412 px, daneben Screenshots der heutigen Seite in BB.
- [ ] Abgleich mit der heutigen Seite in der Skizze: jede Funktion des Durchgehens (Passt, Später, Lena fragen,
      Rückgängig, Details, Kategorie, Payee, Memo, Tastenleiste am PC) ist genannt mit „bleibt“, „ändert sich“ oder
      „fehlt“, keine Funktion geht stillschweigend verloren.
- [ ] Liste aller heutigen Toasts im Kassenbuch und Durchgehen (Text, Auslöser, mit oder ohne „Rückgängig“) als Teil der
      Skizze, je Toast ein Vorschlag „bleibt“, „entfällt“ oder „anders bestätigt“.
- [ ] Roman hat die Skizze freigegeben (Notiz in dieser Task mit Datum und gewählten Optionen). [human-eye]
- [ ] Nach der Freigabe: Bau-Tasks je Teil angelegt (Wischen und Memo in ledger, Toast-Regel in design-system, dort
      design-system-vf94m einbezogen oder abgelöst), jede mit dem Kriterium „Seite neben der Skizze verglichen,
      Abweichungen nur die genannten“; Entscheidung zur Toast-Regel als ADR.

## Notes
Captured 2026-11-19 im Gespräch mit Roman. Zusammenhang: design-system-vf94m (Toast am Handy verdeckt den FAB) hängt an der
Toast-Regel aus dieser Exploration (`blocks`). Prototyp Version 17 `review()` ist die bisherige Vorlage des Durchgehens
(ADR 0086). Die Bilder aus dem Lauf 2026-11-19 in der BB Prüfliste zeigen die heutige Karte.
