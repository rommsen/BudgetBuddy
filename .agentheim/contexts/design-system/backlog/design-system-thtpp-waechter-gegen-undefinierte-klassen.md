---
id: design-system-thtpp
title: "Wächter, der Klassennamen findet, die weder Tailwind-Utility noch in styles.css definiert sind"
status: backlog
type: feature
context: design-system
created: 2026-09-13
completed:
depends_on: []
blocks: []
tags: [test-infra, styleguide, sicherheitsnetz]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Die DaisyUI-Klassen im Design-System (`design-system-fq8dn`) standen seit dem Bau der
Table-Komponente im Code und haben nie etwas getan. Aufgefallen ist es erst, als jemand
eine Tabelle neben einen Entwurf gelegt hat. Eine Klasse, die es nicht gibt, wirft keinen
Fehler: sie fehlt einfach in der Ausgabe, und das Ergebnis sieht nicht kaputt aus, sondern
nur etwas anders als gedacht.

Genau diese Lautlosigkeit ist das Muster, das sich in dieser Sitzung zweimal gezeigt hat.
Beim Testlauf, der still in die echte Datenbank schrieb, half am Ende ein Wächter-Test
(`infra-za7fq`). Hier braucht es denselben Gedanken: aus einem stillen Ausfall eine laute
Meldung machen.

Ohne das kehrt das Problem zurück, sobald jemand aus Gewohnheit eine Klasse schreibt, die
er aus einem anderen Projekt kennt.

## What
Eine Prüfung, die jeden Klassennamen im Client-Code gegen zwei Quellen hält:
1. die Tailwind-Utilities, die der Build tatsächlich erzeugt
2. die eigenen Regeln in `src/Client/styles.css`

Was in keiner von beiden vorkommt, wird gemeldet.

**Bei der Verfeinerung zu klären:**
- **Wo läuft die Prüfung?** Als Test in der Suite, als eigenes Skript, oder als Schritt im
  Build. Ein Test hat den Vorteil, dass er ohne Zutun mitläuft.
- **Woher kommt die Liste der gültigen Tailwind-Utilities?** Der einfachste verlässliche
  Weg dürfte das gebaute CSS sein: was dort als Selektor steht, existiert. Das setzt einen
  Build voraus, was die Prüfung langsamer, aber ehrlich macht.
- **Wie werden dynamisch zusammengesetzte Klassen behandelt?** F#-Code baut Klassennamen
  teils per Interpolation zusammen. Solche Stellen kann eine Textprüfung nicht auflösen;
  sie gehören entweder ausgenommen oder so umgeschrieben, dass sie prüfbar sind.
- **Wie werden Ausnahmen geführt?** Es wird begründete Fälle geben (Klassen von Dritten,
  Testhilfen). Eine Ausnahmeliste sollte kurz sein und je Eintrag eine Begründung tragen,
  sonst wächst sie zu, bis die Prüfung nichts mehr findet.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured am 2026-09-13 nach Romans Entscheidung „DaisyUI raus". Die Prüfung ist unabhängig
von `design-system-my32n` und kann davor oder danach gebaut werden; davor hat den Vorteil,
dass sie die Liste für das Aufräumen gleich mitliefert.

Verwandt: `infra-za7fq` (Wächter gegen Schreiben in die echte Datenbank), derselbe
Gedanke an anderer Stelle. Auch `design-system-t3vqr` nennt eine ähnliche Idee für das
Styleguide-Gate: eine Zusicherung, die jede öffentliche Funktion eines
Design-System-Moduls gegen die `/styleguide`-Route abgleicht.
