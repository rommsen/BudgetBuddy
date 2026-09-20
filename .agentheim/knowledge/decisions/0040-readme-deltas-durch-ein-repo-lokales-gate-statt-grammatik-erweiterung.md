---
id: 0040
title: "README-Deltas durch ein repo-lokales, delta-getriebenes Gate absichern, statt die Delta-Grammatik zu erweitern"
scope: global
status: accepted
date: 2026-09-20
supersedes: []
superseded_by: []
related_tasks: [infrastructure-b6kzp]
related_adrs: []
related_research: []
---

# ADR 0040: README-Deltas durch ein repo-lokales, delta-getriebenes Gate absichern, statt die Delta-Grammatik zu erweitern

## Context

Seit der ghcaj-Fassung von ADR 0032 schreibt ein Worker seine BC-README nicht mehr selbst.
Er meldet ein `README_DELTA`, und der Conductor wendet es beim Integrieren über
`applyReadmeDelta` an (`lib/readme-delta.mjs` im Agentheim-Plugin). Die Grammatik kennt zwei
Operationen, `append` und `replace`.

Im Work-Lauf vom 2026-09-19 ging das bei drei von neun Aufgaben daneben
(`design-system-m4kp7`, `ledger-t7v2q`, `ledger-ezrw0`), jedes Mal anders, und jedes Mal
ohne Fehlermeldung. Die Dispositionen lauteten `applied` oder `merged`, und die entstehenden
Commits sahen sauber aus. Der schwerste Fall: ein `replace`, dessen Body die Einrückung der
verschluckten Unterbullets nicht reproduzierte, meldete `applied` und hätte den Extent
unumkehrbar in Spalte-0-Bullets ohne fetten Begriffskopf zerlegt, die danach für immer
`anchor-missing` liefern. Nachgestellt am ledger-README: eingerückte Unterbullets 6 auf 0,
Spalte-0-Bullets 72 auf 78, Disposition `applied`. Gefangen hat es nur die Verifikation, und
nur weil sie das Werkzeug tatsächlich hat laufen lassen statt den Text zu lesen.

Zwei Eigenschaften der Grammatik stehen dahinter, beide über alle sechs BC-READMEs gemessen:

- `replace` findet sein Ziel über `termHeadOf`, das einen fetten Begriffskopf verlangt. Von
  214 Spalte-0-Bullets sind **63 nicht adressierbar**, weil sie backtick-geführt sind:
  `## Key events` und `## Key commands` vollständig in allen vier Kontexten, die sie führen,
  und alle 13 Bullets in `## Ereignisse` des ledger-READMEs.
- `append` landet über `endOfBulletList` immer am Abschnittsende, nicht am Listenende. In
  **10 von 46** Abschnitten mit Bullets steht dort Fließtext.

Dazu kommt, dass `findBulletsInRange` ein Bullet bis zum nächsten Spalte-0-`- ` dehnt und
dabei Unterbullets und Folgeabsätze verschluckt; der Extent ist damit oft ein Vielfaches
dessen, was ein Leser als "dieses Bullet" ansieht (im ledger-README bis zu 37 Zeilen).

Widerlegt ist die zunächst naheliegende Vermutung, umbrochene Bullets seien das Problem:
`termHeadOf` und der `expected`-Vergleich laufen beide über `collapseWs`, und das Plugin hat
dafür eigene Tests (`lib/test/readme-delta.test.mjs:221` und `:264`).

Der Kern ist nicht, dass es scheitert, sondern dass es leise scheitert. Ein `anchor-missing`
existiert als Disposition, feuerte in zwei der drei Fälle aber nicht, weil der Anker formal
gefunden wurde, nur auf dem falschen Umfang.

## Decision

**1. Die Delta-Grammatik wird nicht erweitert.** `readme-delta.mjs` liegt im Plugin-Cache
unter `~/.claude/plugins/cache/agentheim/agentheim/<version>/lib/`, also außerhalb dieses
Repos. Jede Änderung dort wird beim nächsten Plugin-Update überschrieben, ohne dass es
jemand merkt, und wäre danach genau die Art stiller Regression, die dieser ADR abstellt. Der
Befund geht stattdessen als Meldung an das Plugin (Vorschlag dort: eine zusätzliche
Disposition `structure-changed` in `applyReadmeDelta`, die das Modul rein und git-frei
lässt).

**2. Die sechs BC-READMEs werden nicht auf die Grammatik umgeformt.** Das beträfe 63 Bullets
und 10 Abschnitte, es hieße backtick-geführte Ereignis- und Kommandonamen in fette
Begriffsköpfe zu zwingen und Fließtext aus Abschnittsenden zu entfernen. Betroffen wären
genau die Stellen mit dem dichtesten Fachvokabular (`## Key events`, `## Key commands`,
`## Ereignisse`), und die READMEs sind nach ADR-0041 kuratierte Prosa, kein
maschinenlesbares Register. Der Lesbarkeitsverlust steht in keinem Verhältnis zum Gewinn.

**3. Stattdessen ein repo-lokales Gate: `scripts/readme-delta-check.mjs`.** Es nimmt ein
Paar aus README und `README_DELTA`, wendet das Delta an, prüft das Ergebnis und urteilt. Es
schreibt nie (kein `--write`): das Schreiben bleibt beim Conductor, nach ADR-0054
compute-then-write. Exit 0 heißt sauber, Exit 1 heißt abgelehnt mit benanntem Grund, Exit 2
heißt, das Gate konnte nicht urteilen.

**4. Das Gate ist delta-getrieben, nicht ein statischer Lint über die READMEs.** Ein
statischer Lint müsste die Grammatik-Grenzen selbst zum Defekt erklären und wäre damit für
den gesunden Zustand rot: 63 Bullets und 10 Abschnitte sind heute in Ordnung, sie sind nur
nicht adressierbar. Ein solcher Lint würde Entscheidung 2 durch die Hintertür wieder
aufmachen, weil der einzige Weg, ihn grün zu bekommen, die verworfene Umformung wäre. Das
delta-getriebene Gate wird nur rot, wenn ein konkretes Delta tatsächlich danebengeht.

**5. Das Gate lädt das echte Plugin-Modul, es hält keine Kopie der Grammatik.** Aufgelöst
wird über `os.homedir()` nach `<home>/.claude/plugins/cache/agentheim/agentheim`, darin das
Semver-Maximum numerisch je Feld, also dasselbe Muster wie `lib/resolve-plugin-file.mjs` und
dieselbe Version, die der Conductor benutzt. Eine nachgebaute Grammatik im Repo würde beim
ersten Plugin-Update vom geprüften Verhalten abweichen, und zwar unbemerkt: eine Prüfung,
die etwas anderes prüft als das, was läuft, ist derselbe Fehler in neuer Form. Findet das
Gate das Modul nicht, bricht es mit Exit-Code 2 ab und nennt den durchsuchten Pfad; es gibt
keinen stillen Durchlauf und keinen Rückfall auf eine eigene Kopie.

**6. Die Regel, das Gate aufzurufen, ist ausdrücklich prose-only und unenforced.** Die
Aufrufstelle (der Integrationsschritt des Conductors) ist Plugin-Code, also dieselbe Fläche,
die Entscheidung 1 nicht anfasst. Nach ADR-0059 wird das benannt statt behauptet:
durchgesetzt ist das Gate selbst über `node --test`, nicht durchgesetzt ist, dass jemand es
aufruft. Mechanisch gekoppelt ist nur Skriptpfad und Verfahrensregel: ein Test prüft, dass
`CLAUDE.md` den Aufrufbefehl und den Skriptpfad wörtlich enthält.

## Consequences

### Positive

- Die drei realen Fehlschläge vom 2026-09-19 sind als Testfälle festgehalten und werden rot,
  wenn das Gate sie durchwinkt. Der wichtigste davon (`ledger-ezrw0`) ist der Fall, in dem
  das Werkzeug `applied` meldet und das Dokument trotzdem zerstört ist.
- Das Gate kann nicht von der geprüften Grammatik abdriften, weil es sie nicht nachbaut.
- Bessert das Plugin nach, ändern sich die Dispositionen in den Fixtures, die Tests werden
  rot, und das ist das Signal, die Umgehung zurückzunehmen. Die Testsuite ist damit
  gleichzeitig der Wecker für den Tag, an dem dieser ADR abgelöst gehört.
- Die READMEs bleiben, wie sie sind: lesbar, mit backtick-geführten Ereignisnamen und mit
  Fließtext, wo Fließtext hingehört.

### Negative

- Das Gate muss von Hand aufgerufen werden. Ruft es niemand auf, ändert dieser ADR nichts.
  Das ist der Preis von Entscheidung 1 und in Entscheidung 6 benannt.
- Es hängt an einer maschinenlokalen Abhängigkeit (dem Plugin-Cache). Auf einer Maschine
  ohne installiertes Plugin ist es nicht lauffähig, und die Tests, die es aufrufen, laufen
  dort nicht durch. Für ein privates Projekt mit einer Arbeitsmaschine ist das vertretbar;
  ein stiller Skip wäre es nicht, deshalb Exit 2 statt Skip.
- Die zwei Grammatik-Grenzen bleiben bestehen. `## Key events`, `## Key commands` und
  `## Ereignisse` sind per `replace` weiterhin unerreichbar, und in 10 Abschnitten landet
  jedes `append` an der falschen Stelle. Das Gate macht das sichtbar, es behebt es nicht:
  die betroffenen Stellen bleiben Handarbeit des Conductors.

### Neutral

- Das Gate wächst mit: kommt eine siebte BC-README dazu, ist nichts anzupassen, weil nicht
  die READMEs geprüft werden, sondern die Deltas gegen sie.
- Die gemessenen Zahlen (63 von 214 Bullets, 10 von 46 Abschnitten) sind Beleg, nicht
  Vertrag. Sie dürfen sich ändern, ohne dass etwas rot wird.

## Alternatives considered

**Die Grammatik im Plugin erweitern** (mehrzeilige Extents genauer schneiden, Unterbullets
adressierbar machen, Einrückung beim Einspleißen erzwingen). Verworfen nach Entscheidung 1:
Code außerhalb des Repos, beim nächsten Update weg. Der Inhalt dieses Vorschlags geht
stattdessen als Meldung an das Plugin, wo er hingehört.

**Die sechs READMEs auf die Grammatik umformen.** Verworfen nach Entscheidung 2: 63 Bullets,
10 Abschnitte, Lesbarkeitsverlust an den dichtesten Stellen, und ein Widerspruch zu
ADR-0041, das die READMEs ausdrücklich als kuratierte Prosa führt.

**Die Deltas bewusst gröber schneiden und die Nacharbeit beim Conductor lassen.** Das ist
faktisch der heutige Zustand, und er hat drei von neun Aufgaben beschädigt. Ohne eine
Prüfung, die auffällt, bleibt es Glückssache, ob der Conductor misstrauisch genug ist.
Diese Alternative ist nicht verworfen, sondern ergänzt: das Gate ist genau die Prüfung, die
ihr gefehlt hat.

**Ein statischer Lint über die READMEs**, der rot wird, sobald eine README eine für das
Werkzeug unadressierbare Form annimmt. Verworfen nach Entscheidung 4: er wäre für den
gesunden Zustand rot und würde die verworfene Umformung erzwingen.

**Eine Kopie der Grammatik im Repo**, damit das Gate ohne Plugin läuft. Verworfen nach
Entscheidung 5: die Kopie driftet, und eine driftende Prüfung prüft etwas anderes als das,
was läuft.
