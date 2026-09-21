---
id: infrastructure-b6kzp
title: "Gate gegen leise danebengehende README-Deltas (repo-lokal, gegen das echte Plugin-Modul)"
status: todo
type: chore
context: infrastructure
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [agentheim, werkzeug, readme, buchhaltung]
related_adrs: [0040]
related_research: []
prior_art: [ledger-ezrw0, design-system-m4kp7, ledger-t7v2q]
---

## Why

Seit der ghcaj-Fassung von ADR 0032 schreibt ein Worker die BC-README nicht mehr selbst,
sondern meldet ein `README_DELTA`, das der Conductor beim Integrieren anwendet
(`lib/readme-delta.mjs`, `applyReadmeDelta`). Die Grammatik kennt zwei Operationen,
`append` und `replace`, und `replace` findet sein Ziel über einen `anchor`.

**Im Work-Lauf vom 2026-09-19 hat das bei drei von neun Aufgaben nicht funktioniert, jedes
Mal anders, und jedes Mal ohne Fehlermeldung.** Der Kern ist nicht, dass es scheitert,
sondern dass es leise scheitert: in allen drei Fällen meldete die Disposition `applied` oder
`merged`, und der entstehende Commit sah sauber aus.

### Korrektur der ursprünglichen Diagnose

Die Erstfassung dieses Tasks nannte als Ursache 1 "mehrzeilig umbrochene Bullets: der
Matcher sieht nur die erste Zeile". **Das ist gemessen und verworfen.** `termHeadOf` und der
`expected`-Vergleich laufen beide über `collapseWs`, das jede Folge aus Leerraum und
Zeilenumbrüchen auf ein Leerzeichen zieht; ein Umbruch mitten im fetten Begriffskopf ist für
den Anker nicht beobachtbar. Das Plugin hat dafür eigene Tests
(`lib/test/readme-delta.test.mjs:221` "a multi-line bullet extent (continuation lines) is
captured and replaced as a whole" und `:264` "a bold lead-in that closes on a continuation
line anchors identically to its single-line equivalent"). Ein Nachstellen gegen das
37-Zeilen-Bullet `- **Sichten**` im ledger-README bestätigt es: mit vollständigem `expected`
lautet die Disposition `applied`, die Einrückung bleibt. Umbrochene Bullets sind
anker-technisch unproblematisch.

Der tatsächliche Mechanismus sind zwei andere Stellen:

- **`findBulletsInRange` dehnt ein Bullet bis zum nächsten Spalte-0-`- `** (bzw. bis zu
  einem `#`-Header, einem `<!--`-Marker oder dem Abschnittsende) und **verschluckt dabei alle
  Unterbullets und alle Folgeabsätze**. Der Extent ist damit oft ein Vielfaches dessen, was
  ein Leser als "dieses Bullet" ansieht: im ledger-README bis zu 37 Zeilen. Wer `expected`
  über den sichtbaren Absatz schreibt statt über den ganzen Extent, verfehlt den Vergleich
  und bekommt `merged`.
- **`endOfBulletList` schaut gar nicht auf Bullets.** Es liefert schlicht das Abschnittsende
  ohne abschließende Leerzeilen. Ein `append` landet deshalb immer am Abschnittsende, auch
  wenn dort Fließtext steht und die Bulletliste längst vorbei ist.

### Die drei Fälle, nachgestellt gegen das echte Modul (0.9.4)

- **`design-system-m4kp7`**: der Anker traf, aber `expected` deckte nur den sichtbaren
  Absatz ab, nicht den ganzen Bullet-Extent. Der Vergleich schlug fehl, Disposition `merged`,
  der neue Absatz landete neben dem veralteten. Die README hätte danach behauptet, das Skript
  starte immer einen Server UND es starte für präsentationale Routen keinen. Der Conductor
  hat es beim Integrieren gemerkt, zurückgesetzt und von Hand repariert.
- **`ledger-t7v2q`**: zwei `append`-Operationen landeten am Ende von `## Ereignisse` statt an
  den vorgesehenen Stellen in der Beobachtungs-Liste. Da `## Ereignisse` mit dem
  Code-Layout-Absatz endet, landet dort jedes `append`. Nachgestellt: Disposition `applied`,
  Einfügepunkt Zeile 323, direkt hinter dem Absatz. Inhaltlich richtig, an der falschen
  Stelle; die beiden Blöcke stehen dort bis heute. Kein Worker hätte es besser machen können.
- **`ledger-ezrw0`**: musste auf das Eltern-Bullet `**Sichten**` ausweichen und einen
  37-Zeilen-Extent komplett ersetzen, um an ein Unterbullet heranzukommen. In Iteration 1
  ging dabei die Einrückung verloren. Nachgestellt: Disposition **`applied`**, eingerückte
  Unterbullets im Dokument 6 auf 0, Spalte-0-Bullets 72 auf 78, und ein späterer `replace`
  auf `getLedgerEntriesPage` liefert danach dauerhaft `anchor-missing` (ohne fetten
  Begriffskopf gibt `termHeadOf` für alle `null`). Das Werkzeug meldet Erfolg, und das
  Dokument ist strukturell zerstört. Gefangen hat es nur die Verifikation, und nur weil sie
  `applyReadmeDelta` tatsächlich hat laufen lassen statt den Text zu lesen: der sichtbare
  Text war korrekt, nur der Leerraum nicht.

### Die zwei Grammatik-Grenzen, in Zahlen

Gemessen über alle sechs BC-READMEs am 2026-09-20:

1. **`replace` braucht einen fetten Begriffskopf** (`termHeadOf` liefert sonst `null`, und
   ein solches Bullet ist nie ein Ziel). Von 214 Spalte-0-Bullets sind **63 nicht
   adressierbar**, weil sie backtick-geführt sind: darunter `## Key events` und
   `## Key commands` vollständig in allen vier Kontexten, die sie führen, und alle 13 Bullets
   in `## Ereignisse`.
2. **`append` landet immer am Abschnittsende.** In **10 von 46** Abschnitten mit Bullets steht
   dort Fließtext, nicht das Listenende.

Ein `anchor-missing` gibt es zwar als Disposition, es feuerte bei `design-system-m4kp7` aber
nie, weil der Anker formal gefunden wurde, nur eben auf dem falschen Umfang.

## What

Entscheidung Roman, 2026-09-20: **das leise Scheitern abstellen und die Umgehung festhalten.**
Die Grammatik selbst wird nicht erweitert und die sechs READMEs werden nicht auf sie
umgeformt. Grund, damit die Frage nicht in drei Monaten neu aufgemacht wird:
`readme-delta.mjs` liegt außerhalb dieses Repos und wird beim nächsten Plugin-Update
überschrieben, und eine Umformung der READMEs (63 Bullets, 10 Abschnitte) kostet Lesbarkeit
an genau den Stellen, an denen das Fachvokabular am dichtesten ist. Was bleibt, ist ein Gate
in diesem Repo.

**Ein delta-getriebenes Gate, kein statischer README-Lint.** Ein Lint über die READMEs müsste
die Grammatik-Grenzen selbst zum Defekt erklären und wäre für 63 Bullets und 10 Abschnitte
sofort rot, ohne dass irgendetwas kaputt ist; er würde also durch die Hintertür genau die
Umformung erzwingen, die gerade verworfen wurde. Das Gate prüft stattdessen ein konkretes
Paar aus README und Delta und wird nur rot, wenn dieses Delta tatsächlich danebengeht.

**Es ruft das echte Plugin-Modul auf, nie eine Kopie der Grammatik.** Sonst driftet die
Prüfung vom geprüften Verhalten weg und wird mit dem nächsten Plugin-Update wertlos, ohne
dass es jemand merkt. Das wäre derselbe Fehler in neuer Form.

**Es schreibt nie.** Es rechnet und urteilt; das Schreiben bleibt beim Conductor (ADR-0054,
compute-then-write).

## Acceptance criteria

- [ ] `scripts/readme-delta-check.mjs` existiert und lädt `lib/readme-delta.mjs` aus dem
      Plugin-Cache nach derselben Regel wie `lib/resolve-plugin-file.mjs`: Cache-Wurzel aus
      `os.homedir()` ableiten (`<home>/.claude/plugins/cache/agentheim/agentheim`), darin das
      **Semver-Maximum numerisch je Feld** wählen, nicht lexikalisch (0.8.10 > 0.8.9 > 0.8.3),
      Nicht-Semver-Verzeichnisse überspringen. Keine hart kodierte Version im Skript.
      **Rot, wenn** jemand `0.9.4` einträgt: der Test T9 legt zwei Fake-Cache-Verzeichnisse
      `0.9.4` und `0.10.0` an und verlangt, dass die Auflösung `0.10.0` wählt.
- [ ] Findet das Skript die Datei nicht (kein installiertes Plugin, umbenannte Cache-Wurzel,
      leeres Versionsverzeichnis), **bricht es mit Exit-Code 2 ab** und nennt in der Meldung
      den durchsuchten Pfad und die gefundenen Versionsverzeichnisse. Es gibt **keinen
      stillen Durchlauf, kein Skip, keine eigene Kopie der Grammatik als Rückfall**. Exit 2
      ist bewusst von Exit 1 (Delta abgelehnt) verschieden, damit der Aufrufer "Gate sagt
      nein" von "Gate konnte nicht urteilen" unterscheiden kann.
      **Rot, wenn** jemand einen Rückfall einzieht: T7 setzt die Cache-Wurzel auf ein leeres
      Verzeichnis und verlangt Exit 2 mit genannter Ursache, nicht Exit 0.
- [ ] Aufruf `node scripts/readme-delta-check.mjs --bc <kontext> --delta <datei|->`. Die
      Eingabe ist der `README_DELTA`-Block unverändert in der Form aus
      `references/worker-return-format.md` (JSON-Array aus `{document, section, ops}`).
      `document: "README.md"` löst auf `.agentheim/knowledge/contexts/<kontext>/README.md`
      auf, `document: "context-map.md"` auf `.agentheim/knowledge/context-map.md`. Es gibt
      kein `--write` und keinen Schreibpfad im Skript.
- [ ] Das Skript führt sechs Prüfungen durch und beendet mit Exit-Code 1, sobald eine
      verletzt ist, je mit benannter Op und benanntem Grund:
      1. **Disposition.** Nur `applied` und `noop-already` bestehen. `merged`,
         `anchor-missing` und `appended-fallback` sind Fehlschläge.
         *Rot bei:* `design-system-m4kp7` (`merged`).
      2. **Abschnitt vorhanden.** Bei `appended-fallback` nennt die Meldung die tatsächlich
         vorhandenen `## `-Überschriften der Zieldatei.
         *Rot bei:* `{"section": "Sichten"}` gegen das ledger-README, wo der Abschnitt
         `## Oberfläche (ledger-xbrht, 2026-09-12)` heißt, inklusive Klammerzusatz.
      3. **Einfügepunkt bei `append`.** Der Einfügepunkt (Abschnittsende ohne Leerzeilen)
         muss unmittelbar auf einen Block folgen, dessen erste Zeile mit `- ` beginnt.
         *Rot bei:* `ledger-t7v2q`, `append` in `## Ereignisse`, dessen letzter Block der
         Code-Layout-Absatz ist. Trifft heute 10 von 46 Abschnitten mit Bullets.
      4. **Bullet-Budget bei `replace`.** Die Zahl der Spalte-0-`- `-Zeilen im berührten
         Abschnitt bleibt bei `replace` unverändert und steigt bei `append` um genau eins.
         *Rot bei:* `ledger-ezrw0` Iteration 1, wo sie um sechs stieg.
      5. **Einrückungsprofil bei `replace`.** Die Menge der Einrückungsbreiten aller
         `- `-Zeilen im berührten Abschnitt verliert keine Breite, die vorher vorhanden war.
         *Rot bei:* `ledger-ezrw0` Iteration 1, wo Breite 2 von sechs Vorkommen auf null fiel,
         obwohl die Disposition `applied` lautete.
      6. **Anker-Erreichbarkeit, vor dem Anwenden.** Trägt kein Bullet des Abschnitts den
         gesuchten `termHead`, nennt die Meldung die erreichbaren `termHead`s dieses
         Abschnitts und, falls es keine gibt, dass der Abschnitt per `replace` grundsätzlich
         nicht erreichbar ist.
         *Rot bei:* jedem `replace` auf `## Key events`, `## Key commands` oder
         `## Ereignisse` (dort ist kein einziges der 63 backtick-geführten Bullets
         adressierbar).
- [ ] Bei Erfolg wie bei Fehlschlag gibt das Skript einen Unified Diff der berührten
      Abschnitte aus (vorher gegen nachher), nicht des ganzen Dokuments, sowie in der ersten
      Zeile die benutzte Plugin-Version. `[human-eye]` nur für die Lesbarkeit dieser Ausgabe;
      dass Diff und Versionszeile überhaupt da sind, prüft T6.
- [ ] `scripts/readme-delta-check.test.mjs` läuft unter `node --test` und deckt mindestens
      diese neun Fälle gegen **synthetische Fixtures** ab (nie gegen den Live-Baum, wie
      `lib/test/readme-delta.test.mjs` es selbst hält, damit eine legitime README-Änderung
      die Tests nicht rot macht):
      - T1 `ledger-t7v2q` nachgestellt: `append` in einen Abschnitt, der auf Fließtext endet,
        muss Exit 1 mit Grund "hinter Fließtext" ergeben.
      - T2 `design-system-m4kp7` nachgestellt: `replace`, dessen `expected` nur einen Teil des
        Extents deckt, muss Exit 1 mit Grund `merged` ergeben.
      - T3 `ledger-ezrw0` nachgestellt: `replace` mit flachgeklopftem Body, Disposition
        `applied`, muss trotzdem Exit 1 ergeben (Prüfung 4 und 5). **Das ist der Kern: das
        Werkzeug sagt in Ordnung, das Gate sagt nein.**
      - T4 falscher Abschnittsname: Exit 1, Meldung nennt die vorhandenen Überschriften.
      - T5 `replace` auf ein backtick-geführtes Bullet: Exit 1, Meldung nennt die
        erreichbaren `termHead`s.
      - T6 Gegenprobe: ein sauberes `append` ans Listenende und ein sauberer `replace` über
        den vollen Extent ergeben Exit 0, einen nicht leeren Diff und die Versionszeile. Ohne
        T6 wäre ein Gate, das immer verweigert, ebenso wertlos wie eines, das nie verweigert.
      - T7 Lader, Fehlerfall: leere oder fehlende Cache-Wurzel ergibt Exit 2 mit benannter
        Ursache, kein Skip, kein Exit 0.
      - T8 Kopplung an die Verfahrensregel (siehe unten).
      - T9 Versionsauflösung: bei den Fake-Verzeichnissen `0.9.4` und `0.10.0` wird `0.10.0`
        gewählt (numerischer Semver-Vergleich, nicht lexikalisch).
- [ ] `package.json` trägt die Skripte
      `"check:readme-delta": "node scripts/readme-delta-check.mjs"` und
      `"test:scripts": "node --test scripts/*.test.mjs"`. Letzteres nimmt das bestehende
      `scripts/screenshot-route.test.mjs` mit auf, das heute nur von Hand läuft.
      `npm run test:scripts` ist grün.
- [ ] `CLAUDE.md` trägt einen Abschnitt "README-Deltas (Agentheim)" mit höchstens zehn Zeilen:
      der Aufrufbefehl, der Satz, dass ein rotes Gate heißt, den Block von Hand zu platzieren,
      die Unterscheidung Exit 1 (Delta abgelehnt) von Exit 2 (Gate konnte nicht urteilen), und
      die Umgehung in einem Satz (auf das nächstgelegene Spalte-0-Bullet mit fettem Kopf
      ankern, den ganzen Extent in `expected` UND `body` mit exakter Einrückung neu ausgeben).
      Alles Weitere steht in `--help` des Skripts, nicht in Prosa.
- [ ] T8 prüft, dass `CLAUDE.md` die Zeichenkette `npm run check:readme-delta` und den Pfad
      `scripts/readme-delta-check.mjs` enthält.
      **Rot, wenn** das Skript umbenannt oder verschoben wird, ohne die Verfahrensregel
      nachzuziehen. Das ist die einzige mechanische Klammer zwischen Prosa und Werkzeug.
- [ ] Die Verfahrensregel selbst ist in dieser Task-Datei und in `CLAUDE.md` ausdrücklich als
      **prose-only, unenforced** gekennzeichnet, mit Begründung (siehe Notes). Nach ADR-0059
      trägt dieser Task damit beides: durchgesetzt ist das Gate (T1 bis T9 machen es rot),
      nicht durchgesetzt ist die Regel, es überhaupt aufzurufen.

## Notes

Erfasst am 2026-09-20 auf Romans Entscheidung, nach dem Work-Lauf vom 2026-09-19. Verfeinert
am 2026-09-20: die Richtung "Grammatik erweitern oder READMEs umformen" ist verworfen, nur
noch Prüfung und Umgehung sind im Umfang. `type` von `bug` auf `chore` geändert: der Befund
ist ein Fremd-Werkzeug-Fehler, die Lieferung dieses Tasks ist ein Wächter.

**Dispatchbar an einen Worker.** Alle Zieldateien (`scripts/readme-delta-check.mjs`,
`scripts/readme-delta-check.test.mjs`, `package.json`, `CLAUDE.md`) liegen außerhalb von
`.agentheim/`. Der Checkpoint-Guard verwirft nach `lib/derived-artifact-guard.mjs:113` nur
`.agentheim/`-Pfade (`bookkeeping-path`) und `dashboard/dist/` (`derived-artifact`). Anders
als `infrastructure-qwthn` muss diese Aufgabe also nicht der Conductor selbst ausführen. Der
Worker liest die READMEs unter `.agentheim/knowledge/`, schreibt sie aber nicht; nur die
Fixtures in seiner Testdatei bilden ihre Form nach.

**Zur Kennzeichnung prose-only, unenforced.** Die Regel "vor dem Integrieren durch das Gate"
kann dieses Repo nicht erzwingen, weil die Aufrufstelle (`applyReadmeDelta` im
Integrationsschritt des Conductors) Plugin-Code ist, also genau die Fläche, die aus demselben
Grund nicht angefasst wird wie die Grammatik. Das ist der ehrliche Preis der getroffenen
Entscheidung und gehört ausgeschrieben, statt hinter einem Kriterium zu verschwinden, das nie
rot werden kann. Was mechanisiert ist: das Gate selbst (T1 bis T7, T9) und die Kopplung von
Skriptpfad und Regel (T8). Was nicht: dass jemand es aufruft.

**Zur Id:** ausgeschrieben `infrastructure-b6kzp`, nicht `infra-b6kzp`, aus demselben Grund,
den `infrastructure-qwthn` in seinen Notes festhält: `deriveContext` schneidet den Token ab
und liefert für `infra-*` den Wert `infra`, für den es kein Board-Verzeichnis gibt.

**Messgrundlage der Zahlen im Why** (Stand 2026-09-20, sechs BC-READMEs): 58 Abschnitte, 46
davon mit Spalte-0-Bullets, 214 Bullets, 151 per `replace` adressierbar, 63 nicht, 0 doppelte
`termHead`s in einem Abschnitt, 10 Abschnitte enden auf Fließtext. Die Zahlen sind Beleg,
nicht Kriterium: sie dürfen sich ändern, ohne dass das Gate rot wird.

**Nicht in diesem Task:** die beiden Blöcke, die `ledger-t7v2q` am Ende von `## Ereignisse`
im ledger-README hinterlassen hat, stehen dort noch. Ihre Umplatzierung ist eine
`.agentheim/`-Schreibung und damit Conductor-Arbeit, siehe `ledger-v8n4t`.
