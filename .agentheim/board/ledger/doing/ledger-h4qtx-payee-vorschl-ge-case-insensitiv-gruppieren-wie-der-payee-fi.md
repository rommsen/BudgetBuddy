---
id: ledger-h4qtx
title: "Payee-Vorschläge über dieselbe Faltung gruppieren wie der Payee-Filter (bb_lower, ADR 0037)"
status: doing
type: chore
context: ledger
created: 2026-09-19
completed:
depends_on: [ledger-vgfw3, ledger-k3wqn]
blocks: []
tags: [backend, kassenbuch, filter, suche]
related_adrs: [0036, 0037]
related_research: []
prior_art: [ledger-vgfw3, ledger-k3wqn]
---

## Why

`ledger-vgfw3` (ADR 0036) hat den Payee-Filter ohne Beachtung der Groß-/Kleinschreibung
gebaut, damit "rewe" auch "Rewe" und "REWE" trifft. Die Payee-Vorschlagsabfrage
(`Ledger.Persistence.loadPayeeSuggestions`), aus der die Klick-Auswahl für genau diesen
Filter stammt, gruppiert aber weiterhin schreibweisen-genau: der SQL-Pfad über
`GROUP BY payee` (Z. 827), der F#-Pfad bei aktivem Betragsfilter über `List.countBy id`
(Z. 820).

Ergebnis: "Rewe" und "REWE" erscheinen als zwei getrennte Vorschläge mit je eigener Anzahl,
obwohl ein Klick auf einen von beiden dieselbe, größere Treffermenge liefert. Die Zahl am
Vorschlag widerspricht dem, was nach dem Klick passiert. Das ist keine Kosmetik: die Zahl ist
die einzige Information, an der Roman vor dem Klick abschätzt, ob sich der Filter lohnt.

**Warum dieses Item seine eigene Lösung nicht mehr vorschlagen darf.** Die bisherige Fassung
schlug `GROUP BY payee COLLATE NOCASE` vor. Das wäre falsch gewesen: SQLites `NOCASE` faltet
nur ASCII A-Z, und `ledger-k3wqn` beseitigt gerade genau diesen Defekt im Filter selbst
(ADR 0037). Hätte dieses Item zuerst gegriffen, hätte es den soeben behobenen Fehler an einer
zweiten Stelle neu eingebaut, und zwar grün durch alle Tests, weil kein bestehender Test
Umlaute sät. Gefunden bei der Verfeinerung von `ledger-k3wqn` am 2026-09-20.

## What

Beide Pfade von `loadPayeeSuggestions` auf dieselbe Faltung umstellen, die `ledger-k3wqn`
für den Filter einführt. Damit benutzen Filter und Vorschläge denselben Begriff von
"derselbe Payee", und die Anzahl am Vorschlag stimmt mit der Treffermenge nach dem Klick
überein. Zwei verschiedene Begriffe von Gleichheit an zwei Stellen derselben Interaktion
wären genau der Zustand, den dieses Item beseitigen soll.

Konkret (Stellen aus der Abhängigkeits-Notiz von `ledger-k3wqn`):

- **SQL-Pfad**, heute `GROUP BY payee` (`src/Server/Ledger/Persistence.fs`, Z. 827): auf
  `GROUP BY bb_lower(payee)` umstellen. Ausdrücklich **nicht** auf
  `GROUP BY payee COLLATE NOCASE`, siehe oben.
- **F#-Pfad**, heute `List.countBy id` (Z. 820): über `Core.searchFold` gruppieren.
  `searchFold : string -> string` liegt in `src/Server/Persistence.fs`, Modul `Persistence`,
  und ist aus `Ledger.Persistence` als `Core.searchFold` erreichbar, weil `Core` dort bereits
  als Alias definiert ist (Z. 21), genau wie bei `Core.getConnection`.

**Die Anzeige-Schreibweise** ist die eine offene Entscheidung, und sie wird hier getroffen:
**die häufigste vorkommende Schreibweise, bei Gleichstand die lexikografisch kleinste nach
Ordinalvergleich.** Begründung: die gefaltete Form anzuzeigen ("müller gmbh") wäre für einen
Vorschlag, den Roman wiedererkennen soll, die schlechteste Wahl; "zuletzt beobachtet" wäre
über die Zeit instabil und verlangt ein Datum, das die Abfrage heute nicht führt. Die
häufigste Form ist das, was tatsächlich in seinen Daten steht, und der Ordinal-Tiebreak macht
sie unabhängig vom Ausführungsplan eindeutig.

Es ist zu erwarten, dass dabei oft die Großschreibung gewinnt, weil Comdirect-Verwendungs-
zwecke überwiegend in Großbuchstaben stehen. Das ist hingenommen und kein Fehler: es
entspricht dem Bestand. Sollte es im Alltag stören, ist das ein eigenes Item, keine
Nachbesserung hier.

## Acceptance criteria

- [ ] Ein Bestand mit "Rewe" (n mal) und "REWE" (m mal) liefert genau einen Vorschlag mit
      Anzahl n+m, nicht zwei getrennte.
- [ ] Derselbe Test mit Umlauten: ein Bestand mit `MÜLLER GMBH` (n mal) und `Müller GmbH`
      (m mal) liefert genau einen Vorschlag mit Anzahl n+m. Dieses Kriterium ist der
      eigentliche Zweck der Abhängigkeit auf `ledger-k3wqn`; mit `COLLATE NOCASE` wäre es
      rot.
- [ ] Die Anzeige-Schreibweise ist die häufigste, bei Gleichstand die lexikografisch
      kleinste. Ein Test belegt beide Fälle: einmal mit eindeutiger Mehrheit, einmal mit
      Gleichstand, damit der Tiebreak nicht zufällig durch den Ausführungsplan bestimmt wird.
- [ ] Gilt in beiden Pfaden von `loadPayeeSuggestions` gleich: je ein Test ohne und mit
      aktivem Betragsfilter, damit der SQL-Pfad und der F#-Pfad dieselbe Gruppierung und
      dieselbe Anzeige-Schreibweise liefern. Ein Test, der nur einen Pfad trifft, lässt die
      zweite Implementierung ungeprüft (siehe `ledger-c4mzp`).
- [ ] Der F#-Pfad gruppiert über `Core.searchFold`, nicht über eine eigene, zweite
      Normalisierung. Prüfbar daran, dass `loadPayeeSuggestions` keine eigene
      `ToLower`-Aufrufe oder Zeichenersetzungen enthält.
- [ ] Die Anzahl am Vorschlag stimmt mit der Treffermenge überein: ein Test wählt einen
      Vorschlag mit Anzahl k und prüft, dass ein Filter auf diesen Payee genau k Buchungen
      liefert. Das ist die Zusicherung, um die es dem Item eigentlich geht.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes

Gefunden als Randbeobachtung im Verifier-Befund zu `ledger-vgfw3`, Iteration 1 (2026-09-19),
dort als nicht blockierend eingestuft. Verfeinert am 2026-09-20.

**Zur Abhängigkeit auf `ledger-k3wqn`.** Sie ist fachlich, nicht bloß sequenzierend. `k3wqn`
führt `searchFold` und `bb_lower` überhaupt erst ein; ohne sie hat dieses Item keine Faltung,
auf die es sich stützen könnte, und würde zwangsläufig eine zweite erfinden. Die
Dateiüberschneidung in `src/Server/Ledger/Persistence.fs` ist dagegen gering: `k3wqn` arbeitet
in `buildFilterClause` (Z. 650-660), dieses Item in `loadPayeeSuggestions` (Z. 812-831).

**Zur Indexnutzung.** `GROUP BY bb_lower(payee)` kann keinen Index nutzen und wird es auch nie
können: SQLite lehnt anwendungsdefinierte Funktionen in Index-Ausdrücken ab, auch bei
`isDeterministic = true` (bei `ledger-k3wqn` nachgemessen). Auf `payee` gibt es heute ohnehin
keinen Index, es geht also nichts verloren. Bei rund 14.000 Buchungen unproblematisch, siehe
die Messungen in ADR 0037.
