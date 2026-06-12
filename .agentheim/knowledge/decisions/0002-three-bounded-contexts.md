---
id: 0002
title: Drei Bounded Contexts entlang der Sync-Pipeline
scope: global
status: accepted
date: 2026-06-01
supersedes: []
superseded_by: []
related_tasks: []
related_research: []
---

# ADR 0002: Drei Bounded Contexts entlang der Sync-Pipeline

## Context
Mit der Companion-Vision (ADR 0001) festgelegt, musste die innere Aufteilung benannt
werden. Der bestehende Code (`src/Shared/Domain.fs`, `src/Server/*`) zeigt eine klare
lineare Pipeline: Transaktionen holen → kategorisieren → nach YNAB pushen. Die
Ubiquitous Language trennt sich sauber an diesen Stufen, inklusive zweier getrennter
Duplikat-Begriffe (Vor-Import-`DuplicateStatus` vs. YNABs `YnabImportStatus`-Dedup).

## Decision
Drei fachliche Bounded Contexts plus ein Infrastructure-Context:
- **banking-import** (core) — Bankquellen anzapfen, Vor-Import-Duplikat-Urteil.
- **categorization** (core) — Regel-Engine, Auto-Zuordnung, Order-ID-Propagierung.
- **ynab-sync** (core) — Push nach YNAB, Import-ID-Dedup, Splits/Payees/Transfers.
- **infrastructure** (generic/supporting) — Stack, Persistence, Transport, SyncSession-
  Lifecycle, Deployment.

Beziehung: Customer-supplier-Pipeline banking-import → categorization → ynab-sync;
ynab-sync ist Conformist zur YNAB-API; banking-import ist ACL zu Comdirect. Die
**ImportId** ist der Shared-Kernel-Begriff, der die zwei Duplikat-Welten verbindet.

## Consequences
### Positive
- Context-Grenzen folgen der echten Sprache des Codes, nicht einer aufgesetzten Struktur.
- Die zwei Duplikat-Begriffe bekommen je einen klaren Besitzer-Context.
- Neue Quellen (ING) und YNAB-Features (Transfer-Payee, Split-UI) haben einen offensichtlichen Ort.

### Negative
- Die heutige Codebasis ist nicht physisch nach diesen Contexts getrennt (eine `Domain.fs`,
  ein `Server`-Projekt). Die Contexts sind zunächst *konzeptionell*, nicht als Ordner-Struktur.
- SyncTransaction trägt Felder aus allen drei Contexts — ein bewusster, pipeline-getriebener
  Kompromiss, kein Modellierungsfehler.

### Neutral
- Eine physische Code-Reorganisation entlang der Contexts ist möglich, aber nicht erforderlich.

## Alternatives considered
- **Ein einziger Context** — abgelehnt: verschleiert die zwei Duplikat-Welten und die
  klar verschiedenen Sprachen (Banking-TANs vs. Regel-Pattern vs. YNAB-ImportIds).
- **Duplikat-Erkennung als eigener Context** — abgelehnt: Dedup ist zu eng mit dem
  jeweiligen Import-/Push-Schritt verwoben; eigener Context würde künstlich trennen.

## References
- `.agentheim/context-map.md`
- `src/Shared/Domain.fs`
