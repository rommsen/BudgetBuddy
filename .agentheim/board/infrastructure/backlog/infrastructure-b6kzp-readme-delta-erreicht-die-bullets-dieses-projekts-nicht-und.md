---
id: infrastructure-b6kzp
title: "README_DELTA erreicht die Bullets dieses Projekts nicht und scheitert dabei leise"
status: backlog
type: bug
context: infrastructure
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [agentheim, werkzeug, readme, buchhaltung]
related_adrs: []
related_research: []
prior_art: [ledger-ezrw0, design-system-m4kp7, ledger-t7v2q]
---

## Why

Seit der ghcaj-Fassung von ADR 0032 schreibt ein Worker die BC-README nicht mehr selbst,
sondern meldet ein `README_DELTA`, das der Conductor beim Integrieren anwendet
(`lib/readme-delta.mjs`, `applyReadmeDelta`). Die Grammatik kennt zwei Operationen,
`append` und `replace`, und `replace` findet sein Ziel über einen `anchor`.

**Im Work-Lauf vom 2026-09-19 hat das bei drei von neun Aufgaben nicht funktioniert, jedes
Mal anders, und jedes Mal ohne Fehlermeldung.** Zwei Eigenschaften der READMEs dieses
Projekts kollidieren mit der Grammatik:

1. **Mehrzeilig umbrochene Bullets.** Die READMEs brechen ihre Bullets bei ~90 Zeichen um,
   mit zwei Leerzeichen Fortsetzungs-Einrückung. Der Matcher sieht nur die erste Zeile.
2. **Inline-Code als Bullet-Anfang.** `BULLET_START_RE` adressiert nur Zeilen, die auf
   Spalte 0 mit `- ` beginnen, und `termHeadOf` braucht einen fetten Begriffskopf. Ein
   Unterbullet wie ``- `getLedgerEntriesPage` — ...`` auf Einrückung 2 ist damit
   grundsätzlich nicht adressierbar.

**Die drei Fälle im Einzelnen:**

- **`design-system-m4kp7`**: der Anker traf ein mehrzeiliges Bullet, der Matcher erkannte es
  nicht, Disposition `merged`. Der neue Absatz wurde ans Abschnittsende gehängt, der veraltete
  blieb stehen. Die README hätte danach behauptet, das Skript starte immer einen Server UND es
  starte für präsentationale Routen keinen. Der Conductor hat es beim Integrieren gemerkt,
  zurückgesetzt und von Hand repariert.
- **`ledger-t7v2q`**: zwei `append`-Operationen landeten am Ende des Abschnitts `## Ereignisse`
  statt an den vorgesehenen Stellen in der Beobachtungs-Liste, weil deren Bullets
  backtick-geführt sind und `**Verknüpfung:**` ein Absatz ist. Inhaltlich richtig, an der
  falschen Stelle. Kein Worker hätte es besser machen können.
- **`ledger-ezrw0`**: musste auf das Eltern-Bullet `**Sichten**` ausweichen und einen
  32-Zeilen-Block komplett ersetzen, um an ein Unterbullet heranzukommen. In Iteration 1 ging
  dabei die Einrückung verloren; da `replaceRange` den Body wörtlich einspleißt, hätte das den
  ganzen Block flachgeklopft und **jeden künftigen `replace` auf diese Unterbullets dauerhaft
  unmöglich gemacht** (ohne fetten Begriffskopf liefert `termHeadOf` für alle `null`). Erst
  die Verifikation hat es gefangen, und zwar nur, weil sie `applyReadmeDelta` tatsächlich hat
  laufen lassen statt den Text zu lesen: der sichtbare Text war korrekt, nur der Leerraum
  nicht.

**Der Kern ist nicht, dass es scheitert, sondern dass es leise scheitert.** In allen drei
Fällen meldete die Disposition `applied` oder `merged`, und der entstehende Commit sah sauber
aus. Ein `anchor-missing` gibt es zwar als Disposition, es feuerte hier aber nie, weil der
Anker formal gefunden wurde, nur eben auf dem falschen Umfang.

## What

Zu klären, in dieser Reihenfolge:

1. **Wo liegt das Problem, beim Werkzeug oder bei den READMEs?** Beides ist denkbar und die
   Antwort bestimmt alles Weitere:
   - Die Grammatik erweitern (mehrzeilige Bullets erkennen, Unterbullets adressierbar machen,
     Einrückung beim Einspleißen erhalten oder erzwingen) — das ist eine Änderung am
     Agentheim-Plugin, nicht an diesem Projekt, und damit hier nur meldbar, nicht behebbar.
   - Oder die READMEs dieses Projekts auf eine Form bringen, die die Grammatik trifft (keine
     umbrochenen Bullets, fette Begriffsköpfe statt Inline-Code). Das ist eine erhebliche
     Umformung von sechs Dateien und kostet Lesbarkeit.
   - Oder die Deltas bewusst gröber schneiden und die Nacharbeit beim Conductor lassen.
2. **Unabhängig von 1: das leise Scheitern abstellen.** Eine Prüfung beim Anwenden, die
   auffällt, wäre schon viel wert — etwa: nach dem Anwenden erneut parsen und melden, wenn sich
   die Zahl der Bullets im Abschnitt oder ihre Einrückungsebenen geändert haben. Der Conductor
   kann das heute von Hand (Dry-Run gegen eine Kopie, dann `diff`), tut es aber nur, wenn er
   misstrauisch ist.
3. **Bis dahin: die Umgehung festhalten**, damit nicht jede Aufgabe sie neu erfindet. Der
   gangbare Weg ist heute: auf das nächstgelegene Spalte-0-Bullet mit fettem Kopf ankern, den
   ganzen Block in `expected` UND `body` mit exakter Einrückung neu ausgeben, und das Ergebnis
   per Dry-Run gegen eine Kopie prüfen, bevor man zurückmeldet.

## Acceptance criteria

- [ ] To be defined during refinement.

## Notes

Erfasst am 2026-09-20 auf Romans Entscheidung, nach dem Work-Lauf vom 2026-09-19.

**Zur Verortung:** der Befund betrifft das Agentheim-Plugin
(`~/.claude/plugins/cache/agentheim/agentheim/0.9.4/lib/readme-delta.mjs`), nicht
BudgetBuddy-Code. Er liegt hier im `infrastructure`-Kontext, weil das dieselbe Art Aufgabe ist
wie `infrastructure-qwthn` (Agentheim-Werkzeug, das diesen Lauf behindert). Ob eine Änderung am
Plugin selbst aus diesem Repo heraus überhaupt sinnvoll ist, gehört in die Verfeinerung.

**Zur Id:** ausgeschrieben `infrastructure-b6kzp`, nicht `infra-b6kzp`, aus demselben Grund, den
`infrastructure-qwthn` in seinen Notes festhält: `deriveContext` schneidet den Token ab und
liefert für `infra-*` den Wert `infra`, für den es kein Board-Verzeichnis gibt.
