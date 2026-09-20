---
id: ledger-v8n4t
title: "Die von ledger-t7v2q am Ende von ## Ereignisse gestrandeten Übertrags-Blöcke an ihre vorgesehenen Stellen bringen"
status: backlog
type: chore
context: ledger
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [doku, kassenbuch, readme, buchhaltung]
related_adrs: [0015]
related_research: []
prior_art: [ledger-t7v2q, infrastructure-b6kzp]
---

## Why

`ledger-t7v2q` hat zwei `append`-Operationen auf den Abschnitt `## Ereignisse` des
ledger-READMEs gemeldet. Beide sind inhaltlich richtig und beide sind an der falschen Stelle
gelandet, weil `endOfBulletList` in `lib/readme-delta.mjs` nicht das Listenende liefert,
sondern das Abschnittsende. `## Ereignisse` endet mit dem Code-Layout-Absatz, also landete
dort beides. Die Disposition lautete `applied`, der Commit sah sauber aus. Der Befund und
seine Behandlung stehen in `infrastructure-b6kzp`; dieser Task räumt nur den Rest auf.

**Die zwei Blöcke, Stand 2026-09-20:**

- **Zeile 321**, 687 Zeichen in einer einzigen Zeile:
  `` - `EntryTransferCounterpartObservedInYnab`: die Gegenbuchung eines Übertrags ist
  gefunden. ... `` Ein Beobachtungs-Ereignis, das in die Beobachtungs-Liste gehört (heute
  Zeilen 253 bis 265, eingeleitet von `**Beobachtungen** (der Spiegel hat in YNAB etwas
  gesehen):` in Zeile 252). Es steht stattdessen hinter dem Code-Layout-Absatz, getrennt von
  seinen sechs Geschwistern.
- **Zeile 322**, 1075 Zeichen in einer einzigen Zeile:
  `**Übertrags-Verknüpfung:** Die Buchung-zu-Buchung-Verknüpfung aus ADR 0015 entsteht nicht
  beim Entstehungs-Ereignis. ...` Ein Absatz in der Form der übrigen `**...:**`-Absätze des
  Abschnitts, der neben den `**Verknüpfung:**`-Absatz gehört (Zeilen 300 bis 306), weil
  beide dieselbe Sache aus zwei Richtungen beschreiben: die Fremd-Id-Verknüpfung und die
  Buchung-zu-Buchung-Verknüpfung nach ADR 0015.

**Beide Zeilen brechen außerdem den Umbruchstil der Datei.** Die README bricht bei ungefähr
90 Zeichen um, die mittlere Zeilenlänge liegt bei 86. `replaceRange` und `spliceIn` spleißen
den gemeldeten Body wörtlich ein, und der Body eines Workers ist eine einzige Zeile. Für
einen Diff ist das unbrauchbar: jede spätere Änderung an einem dieser Blöcke erzeugt eine
Ein-Zeilen-Änderung von 700 oder 1000 Zeichen.

## What

Die beiden Blöcke an ihre vorgesehenen Stellen setzen, im Umbruchstil der Datei, ohne
inhaltlich etwas zu ändern. Kein Umformulieren, kein Kürzen, kein Ergänzen, mit der einen
Ausnahme aus Akzeptanzkriterium 5.

**Eine Stolperstelle bei der Einsortierung:** das letzte Bullet der Beobachtungs-Liste
(`EntryFlagChangeObservedInYnab`) endet mit dem Satz "Letzte Gruppe in der Reihenfolge."
Dieser Satz meint die Gruppen-Reihenfolge der Änderungserkennung (ADR 0032), nicht die
Reihenfolge der Bullets. `EntryTransferCounterpartObservedInYnab` entsteht in einem späteren
Schritt des Spiegel-Laufs, nachdem die Beobachtung geschrieben ist, und ist überhaupt keine
dieser Gruppen. Wird es hinter das Flag-Bullet gesetzt, ohne das zu sagen, liest sich der
Abschnitt widersprüchlich.

## Acceptance criteria

- [ ] Das Bullet `` - `EntryTransferCounterpartObservedInYnab`: ... `` steht in der
      Beobachtungs-Liste. **Prüfbar:** seine Zeilennummer ist größer als die des Bullets
      `` - `EntryFlagChangeObservedInYnab` `` und kleiner als die der Zeile, die mit
      `Mehrere Gruppen in einer Beobachtung` beginnt. Heute ist sie größer als beide.
- [ ] Der Absatz `**Übertrags-Verknüpfung:**` steht unmittelbar hinter dem Absatz
      `**Verknüpfung:**` und vor dem Absatz `**Konten**`. **Prüfbar:** Zeilennummernvergleich
      der drei Anfangszeilen. Heute steht `**Übertrags-Verknüpfung:**` hinter `**Konten**`
      und hinter `**Code-Layout**`.
- [ ] Keine Zeile im Abschnitt `## Ereignisse` ist länger als 100 Zeichen. **Prüfbar:**
      `awk 'length($0)>100'` über den Zeilenbereich des Abschnitts liefert nichts. Heute
      liefert es die Zeilen 321 (687 Zeichen) und 322 (1075 Zeichen).
- [ ] Kein Textverlust und keine stille Umformulierung: der whitespace-kollabierte Text der
      beiden Blöcke ist vor und nach der Änderung zeichengleich, bis auf die Ergänzung aus
      Kriterium 5. **Prüfbar:** beide Fassungen durch `tr -s '[:space:]' ' '` und
      vergleichen.
- [ ] Der Satz "Letzte Gruppe in der Reihenfolge." im `EntryFlagChangeObservedInYnab`-Bullet
      steht unverändert da und wird durch die Einsortierung nicht falsch: das neu
      einsortierte Bullet trägt einen Halbsatz, der es von der Gruppen-Reihenfolge ausnimmt
      (es entsteht in einem späteren Schritt des Spiegel-Laufs, nicht aus der
      Änderungserkennung). **Prüfbar:** beide Sätze sind im Abschnitt vorhanden.
      `[human-eye]` nur für die Formulierung des Halbsatzes.
- [ ] Der Abschnitt `## Ereignisse` endet nach der Änderung weiterhin mit dem
      `**Code-Layout**`-Absatz. **Prüfbar:** die letzte nicht leere Zeile des Abschnitts
      gehört zu diesem Absatz. Das ist kein Versehen: der Abschnitt bleibt damit einer der
      10, in die ein `append` hinter Fließtext fällt, und genau das soll das Gate aus
      `infrastructure-b6kzp` künftig melden, statt dass es jemand nachträglich aufräumt.
- [ ] Falls `scripts/readme-delta-check.mjs` (aus `infrastructure-b6kzp`) zum Zeitpunkt der
      Ausführung existiert: ein `append`-Delta auf `## Ereignisse` gegen die geänderte Datei
      wird vom Gate mit Exit-Code 1 und dem Grund "hinter Fließtext" abgelehnt. Das belegt,
      dass das Gate diesen Abschnitt weiterhin korrekt zurückweist und die Umstellung daran
      nichts geändert hat. Existiert das Skript noch nicht, entfällt dieses Kriterium und
      das wird im Outcome vermerkt.
- [ ] `dotnet build` und `dotnet test` sind nicht betroffen und werden nicht ausgeführt: die
      Aufgabe ändert genau eine Markdown-Datei. Im Outcome vermerken.

## Notes

**Nicht an einen Worker dispatchbar, vom Conductor selbst auszuführen.** Das Ergebnis dieser
Aufgabe ist eine Änderung an `.agentheim/knowledge/contexts/ledger/README.md`, und ein
Worker darf seit ADR-0032 (Fassung ghcaj) keinen `.agentheim/`-Pfad schreiben; der
Checkpoint-Guard verwirft solche Pfade mit `bookkeeping-path`
(`lib/derived-artifact-guard.mjs:113`). Ein Dispatch ergäbe per Konstruktion ein leeres
Worktree. Der Präzedenzfall ist `infrastructure-qwthn`, das aus demselben Grund der
Conductor übernommen hat. Auch der `verifier` greift hier nicht: das Gate ist für
Worker-Ergebnisse definiert. Die Kriterien sind deshalb so geschnitten, dass sie ohne ihn
maschinell belegbar sind.

**Die Umstellung nicht als Delta einspielen.** Sie von Hand machen. Ein `replace` käme an das
Bullet ohnehin nicht heran: alle 13 Bullets in `## Ereignisse` sind backtick-geführt und
damit für `termHeadOf` unsichtbar, und ein `append` landet genau dort, wo die beiden Blöcke
heute schon stehen.

**Nebenbefund, nicht Teil dieser Aufgabe.** Dasselbe Muster (ein wörtlich eingespleißter
Worker-Body als überlange Einzelzeile) steht an drei weiteren Stellen derselben Datei:
Zeile 160 (993 Zeichen, `- **Such-Filter (Kassenbuch)**` in `## Ubiquitous language`), Zeile
225 (1197 Zeichen, `- *BuchungÜbernehmen (Force-Import)*` in `## Key commands`) und die
Zeilen 416 und 418 (168 und 136 Zeichen, `## Open questions`). Sie sind an der richtigen
Stelle, nur im falschen Umbruch. Ein Reflow dieser drei ist kosmetisch und gehört in eine
eigene Aufgabe, zusammen mit `ledger-t4m9c` und `ledger-f8q2x`, die dieselbe Datei anfassen.
Zeile 160 trägt zusätzlich den Umlaut-Defekt aus `ledger-k3wqn` (ue/ae/oe statt ä/ö/ü), der
dort behandelt wird.
