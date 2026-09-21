---
id: ledger-v8n4t
title: "Die von ledger-t7v2q am Ende von ## Ereignisse gestrandeten Übertrags-Blöcke an ihre vorgesehenen Stellen bringen"
status: done
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

## Outcome

Die beiden von `ledger-t7v2q` am Ende von `## Ereignisse` gestrandeten Blöcke stehen jetzt an
ihren vorgesehenen Stellen, im Umbruchstil der Datei, inhaltlich unverändert bis auf die von
Kriterium 5 verlangte Ergänzung.

- Das Bullet `` - `EntryTransferCounterpartObservedInYnab` `` steht in der Beobachtungs-Liste
  (Zeile 265), zwischen `` - `EntryFlagChangeObservedInYnab` `` (262) und dem Absatz
  `Mehrere Gruppen in einer Beobachtung` (278). Aus einer Zeile mit 687 Zeichen sind zwölf
  umbrochene Zeilen geworden.
- Der Absatz `**Übertrags-Verknüpfung:**` steht unmittelbar hinter `**Verknüpfung:**` und vor
  `**Konten**` (312 < 319 < 338). Aus einer Zeile mit 1544 Zeichen sind achtzehn geworden.

**Ausgeführt vom Conductor, nicht von einem Worker** (wie in den Notes vorgesehen). Das Ergebnis
ist ausschließlich eine Änderung an `.agentheim/knowledge/contexts/ledger/README.md`; ein Worker
darf seit ADR 0032 (Fassung ghcaj) keinen `.agentheim/`-Pfad schreiben, der Checkpoint-Guard
verwirft solche Pfade als `bookkeeping-path`, ein Dispatch hätte per Konstruktion ein leeres
Worktree ergeben. Präzedenzfall: `infrastructure-qwthn`. Der `verifier` greift aus demselben
Grund nicht, sein Gate ist für Worker-Ergebnisse definiert. **Es gibt für diese Aufgabe deshalb
keine unabhängige Zweitprüfung**; die Kriterien waren so geschnitten, dass sie ohne eine solche
maschinell belegbar sind, und genau so sind sie belegt worden:

| Kriterium | Prüfung | Ergebnis |
|---|---|---|
| 1 Bullet in der Beobachtungs-Liste | Zeilennummernvergleich | 262 < 265 < 278 |
| 2 Absatz zwischen `**Verknüpfung:**` und `**Konten**` | Zeilennummernvergleich | 312 < 319 < 338 |
| 3 keine Zeile > 100 Zeichen im Abschnitt | `awk 'length($0)>100'` über 238..351 | 0 Treffer (vorher 687 und 1544) |
| 4 kein Textverlust, keine stille Umformulierung | whitespace-kollabierter Vergleich vorher/nachher | Absatz zeichengleich; Bullet zeichengleich nach Abzug der Ergänzung aus 5 |
| 5 beide Sätze vorhanden | `grep -c` | "Letzte Gruppe in der Reihenfolge." 1x, der neue Halbsatz 1x |
| 6 Abschnitt endet mit `**Code-Layout**` | letzte nicht leere Zeile des Abschnitts | `` `Ledger/CommandHandlers.fs` und `Ledger/QueryHandlers.fs`. `` |
| 7 Gate lehnt `append` weiterhin ab | `node scripts/readme-delta-check.mjs --bc ledger` mit Sondierungs-Delta | Exit 1, `Prüfung 3 (Einfügepunkt bei append) verletzt: der Einfügepunkt in "Ereignisse" liegt hinter Fließtext, nicht hinter einem Bullet.` |
| 8 Build und Tests nicht betroffen | nicht ausgeführt, genau eine Markdown-Datei geändert | vermerkt |

**Zur Ergänzung aus Kriterium 5.** Das letzte Bullet der Beobachtungs-Liste endet mit "Letzte
Gruppe in der Reihenfolge.", was die Gruppen-Reihenfolge der Änderungserkennung meint (ADR 0032),
nicht die Reihenfolge der Bullets. Das neu einsortierte Bullet steht dahinter und wäre ohne
Hinweis widersprüchlich, weil es überhaupt keine dieser Gruppen ist. Es trägt deshalb jetzt den
Halbsatz: "Anders als die Bullets darüber entsteht es nicht aus der Änderungserkennung, sondern
in einem späteren Schritt des Spiegel-Laufs, nachdem die Beobachtung geschrieben ist; die
Gruppen-Reihenfolge (ADR 0032) betrifft es deshalb nicht."

**Kriterium 7 ist der interessante Teil.** Der Abschnitt endet bewusst weiterhin mit Fließtext
und bleibt damit einer der zehn Abschnitte, in die ein `append` an der falschen Stelle fällt. Das
ist kein Versehen: nicht der Abschnitt soll repariert werden, sondern das Melden. Das Gate aus
`infrastructure-b6kzp`, das in derselben Sitzung integriert wurde, weist genau das jetzt zurück,
statt dass es jemand nachträglich aufräumt. Die Sondierung oben belegt, dass die Umstellung daran
nichts geändert hat.

**Nebenbefund aus den Notes, nicht Teil dieser Aufgabe und weiterhin offen.** Dasselbe Muster
(ein wörtlich eingespleißter Worker-Body als überlange Einzelzeile) steht an drei weiteren
Stellen derselben Datei: der Eintrag **Such-Filter (Kassenbuch)** in `## Ubiquitous language`
(in dieser Sitzung von `ledger-k3wqn` neu geschrieben, dabei ist der dort genannte
Umlaut-Defekt behoben worden, die Einzelzeile blieb), `- *BuchungÜbernehmen (Force-Import)*` in
`## Key commands`, und zwei Zeilen in `## Open questions`. Sie stehen an der richtigen Stelle,
nur im falschen Umbruch. Ein Reflow gehört in eine eigene Aufgabe, zusammen mit `ledger-t4m9c`
und `ledger-f8q2x`, die dieselbe Datei anfassen.
