---
id: budgeting-j8r4t
title: "Targets prüfen: bei Rhythmus über einen Monat gilt mehr nur, wenn auch die Zahlung über dem Target liegt (verschobene Jahresabbuchung)"
status: done
type: bug
context: budgeting
created: 2026-11-07
completed:
depends_on: [budgeting-k7r2w]
blocks: []
tags: [budget, targets, target-pruefung]
related_adrs: [0082]
related_research: []
prior_art: [budgeting-p2zr6, budgeting-k7r2w]
---

## Why
Roman am 2026-11-07 nach dem Deploy von k7r2w: Eine Kategorie mit Jahres-Target steht unter "Gibst du mehr aus?" und bekommt das doppelte Target vorgeschlagen, obwohl das Target stimmt. Die Abbuchung kam dieses Jahr gut einen Monat früher als im Vorjahr, deshalb liegen zwei Jahreszahlungen in den 12 gezählten Monaten.

Beispiel mit erfundenen Zahlen: Target 300 € jährlich, je 300 € im ersten und im zwölften gezählten Monat. Ø 600 ÷ 12 = 50 € gegen den Monatswert 25 €, also "mehr" mit dem Vorschlag 600 € jährlich. Bei Targets mit langem Rhythmus reicht eine leicht verschobene Abbuchung für dieses Ergebnis.

(Die echten Buchungen hat der Conductor in Prod nur gelesen; sie gehören nicht ins öffentliche Repo.)

## Entscheidung
Roman, 2026-11-07 (AskUserQuestion, Option "Zusätzlich je Zahlung"): Bei einem Rhythmus über einem Monat (Periode P > 1, alle n Monate oder jährlich) gilt "mehr" nur, wenn zusätzlich die durchschnittliche Zahlung über dem Target je Rhythmus liegt.

Vom Conductor ergänzt und begründet: Die Prüfung je Zahlung greift nur, wenn die Ausgaben als einzelne Zahlungen kommen. Sonst würden über das Quartal verteilte Ausgaben (etwa 150 € jeden Monat bei 300 € je Quartal) nie als "mehr" erkannt, und genau das hatte die Option Roman zugesagt ("monatlich verteilte Ausgaben bei einem Quartals-Target ebenso").

## What
In `targetCheckRow` (`src/Shared/Budgeting.fs`), nur bei P > 1:
- **Zahlungen einzeln**, wenn die Monate mit Ausgaben höchstens ⌈n ÷ P⌉ + 1 sind (n = gezählte Monate). Bei jährlich und 12 Monaten: höchstens 2; bei alle 3 Monate und 12 Monaten: höchstens 5.
- **Betrag je Zahlung** = Summe der gezählten Monate ÷ Monate mit Ausgaben. **Target je Rhythmus** = Monatswert × P.
- Kommen die Zahlungen einzeln, gilt "mehr" nur, wenn die bisherige Regel (Ø > 1,1 × Monatswert und mindestens 10 € darüber) zutrifft **und** der Betrag je Zahlung > 1,1 × Target je Rhythmus und mindestens 10 € darüber liegt.
- Kommen die Ausgaben verteilt (mehr Monate mit Ausgaben als die Grenze), bleibt die bisherige Regel.
- "Weniger", "nie", "Kein Target", der Zählbeginn (k7r2w), der Vorschlag und P = 1 bleiben unverändert. Der Vorschlag entsteht wie bisher nur in "mehr", "weniger" und "Kein Target".

## Acceptance criteria
- [x] Regressionstest auf Domain-Ebene (`targetCheckRow`), vor dem Fix rot: jährlich 300 €, 300 € im ersten und 300 € im zwölften gezählten Monat, sonst 0. Ergebnis: Passt ohne Zusatz, kein Vorschlag. Der Kommentar nennt den Bug (verschobene Jahresabbuchung, zwei Zahlungen im Fenster).
- [x] Intakt: jährlich 300 €, zwei Zahlungen je 400 € im Fenster: "mehr". Eine Zahlung 400 € im Fenster: "mehr" (wie bisher).
- [x] Intakt: alle 3 Monate 300 €, jeden Monat 150 € (verteilt, 12 Monate mit Ausgaben): "mehr" wie bisher.
- [x] Alle 3 Monate 300 €, fünf Zahlungen je 300 € im Fenster (verschobene Quartalsabbuchung): Passt.
- [x] Grenze der Einzelzahlungen: genau ⌈n ÷ P⌉ + 1 Monate mit Ausgaben prüft je Zahlung, einer mehr prüft nur den Ø (je ein Test).
- [x] Grenze beim Betrag je Zahlung: 1,1 × Target je Rhythmus ist nicht "mehr", knapp darüber mit mindestens 10 € Abstand ist "mehr".
- [x] P = 1 unverändert (die bestehenden Tests bleiben grün ohne Änderung).
- [x] ADR 0082 bekommt einen Nachtrag 2026-11-07 mit der Regel (ohne echte Beträge). Die README von budgeting beschreibt die Regel unter **Target-Prüfung**.
- [x] Der Satz der Gruppe "mehr" (`checkGroupRule`) bleibt richtig oder wird angepasst, falls er die neue Bedingung verschweigt.
- [x] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die betroffene Jahres-Kategorie steht in Prod unter Passt. [human-eye]

## Notes
- Kein Browser-Test, das Verhalten ist unter .NET prüfbar. Keine Änderung an `TargetCheckSource`, also keine Fixture-Falle.
- Zwei echte Zahlungen im Jahr mit demselben Betrag (etwa zwei Fahrzeuge unter einem Jahres-Target) sehen gleich aus wie eine verschobene Abbuchung und stehen dann unter Passt. Das nimmt Romans Wahl in Kauf.
- Keine echten Beträge, Daten oder Kategorienamen in Tests, Doku oder Commits (öffentliches Repo).

## Outcome

Die Regel für "Gibst du mehr aus?" in `targetCheckRow` (`src/Shared/Budgeting.fs`) hat bei P > 1 eine Zusatzbedingung. Kommen die Ausgaben als einzelne Zahlungen (höchstens ⌈n ÷ P⌉ + 1 Monate mit Ausgaben, n gezählte Monate), gilt "mehr" nur, wenn zusätzlich der Betrag je Zahlung (Summe der gezählten Monate ÷ Monate mit Ausgaben) über 1,1 × Target je Rhythmus (Monatswert × P) und mindestens 10 € darüber liegt. Bei mehr Monaten mit Ausgaben (verteilt) gilt die bisherige Regel. Weniger, nie, Kein Target, Zählbeginn, Vorschlag und P = 1 sind unverändert. Ohne Monat mit Ausgaben greift die Zusatzbedingung nicht (keine Division durch 0; die Zeile steht ohnehin bei "nie").

Tests in `src/Tests/TargetCheckTests.fs`, neue Liste "Targets prüfen: je Zahlung bei langem Rhythmus": Regressionstest (jährlich 300 €, 300 € im ersten und zwölften Monat: Passt ohne Zusatz und ohne Vorschlag, vor dem Fix rot), zwei Zahlungen je 400 € mehr, eine Zahlung mehr wie bisher, Quartal mit 150 € jeden Monat mehr, fünf Quartalszahlungen je 300 € Passt, Grenze der Einzelzahlungen (5 gegen 6 Monate bei P = 3, 2 gegen 3 bei jährlich), Grenze 1,1 × Target je Rhythmus (330,00 € Passt, 330,01 € mehr) und Grenze 10 € (alle 2 Monate 90 €, je Zahlung 99,99 € Passt, 100 € mehr). Vor dem Fix waren 5 der 8 neuen Tests rot.

Abweichungen von der Task:
- Das Kriterium "Eine Zahlung 400 € im Fenster: mehr (wie bisher)" stimmt rechnerisch nicht: 400 ÷ 12 = 33,33 € liegt nur 8,33 € über dem Monatswert 25 €, das war schon vorher "Passt". Der Test nimmt eine Zahlung von 500 € (Ø 41,67 €), die vorher und nachher "mehr" ist.
- Der bestehende Test "Bei P > 1 gilt die Zahl der Monate nicht: jährlich 240 € mit zwei Zahlungen von 240 € ist mehr" beschrieb genau den Bug (zwei Zahlungen gleich dem Target). Er prüft jetzt zwei Zahlungen von 300 € gegen 240 €, die Aussage (bei P > 1 zählt die Zahl der Monate über dem Target nicht) bleibt. Die Tests mit P = 1 sind unverändert.
- Der Regelsatz der Gruppe "mehr" (`checkGroupRule`, `src/Client/Components/Budget/TargetCheck.fs`) lautet jetzt "Ø mehr als 10 % und 10 € über dem Target, bei monatlichem oder wöchentlichem Rhythmus in mindestens der Hälfte der Monate, bei längerem mit einzelnen Zahlungen auch je Zahlung." (Test in `src/Tests/TargetCheckClientTests.fs` nachgezogen).

`./verify.sh` im Worktree: VERIFY: PASS (dotnet 4259 grün, node 316 grün). README-Delta mit `npm run check:readme-delta -- --bc budgeting` geprüft, Exit 0. Offen: [human-eye] die betroffene Jahres-Kategorie in Prod unter Passt.

Nachtrag 2026-11-07 an ADR 0082 angehängt (vom Conductor korrigiert nach Verifier-FAIL in Iteration 1: Verweis auf Punkt 8, Regel 3 statt Punkt 6, dazu die Einschränkung der Erklärung unter Punkt 8). Die Kann-Zeile zur Rundung von Monatswert × P steht in budgeting-h9c3w.
