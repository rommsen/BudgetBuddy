---
id: 0003
title: Quick Add aktiviert Phase 0 der Ersatz-Idee (Amendment zu ADR 0001)
scope: global
status: accepted
date: 2026-06-12
supersedes: []
superseded_by: []
related_tasks:
  - contexts/ynab-sync/done/2026-06-11-quick-add-manual-entry.md
  - contexts/ynab-sync/done/2026-06-12-quick-add-feedback-round.md
related_research: []
---

# ADR 0003: Quick Add aktiviert Phase 0 der Ersatz-Idee (Amendment zu ADR 0001)

## Context
ADR 0001 zog die Companion-Grenze und listete **manuelle Transaction-Eingabe** explizit
als Non-Goal ("gehört zur Ersatz-Reise, dort Phase 0"). Im UX-Overhaul vom 2026-06-11
(Branch `feature/ux-wow`) wurde genau dieses Feature als **Quick Add** gebaut und
deployed: Bar-Ausgaben am Handy erfassen, direkt nach YNAB gepusht — der dokumentierte
Frau-Workflow und Phase 0 aus `docs/idea-ynab-replacement.md`. Die Entscheidung fiel
implizit während der Umsetzung ("welche Features tun der App wirklich gut?") und wird
hier nachträglich explizit gemacht, statt sie unter den Teppich zu kehren.

## Decision
**Manuelle Transaction-Eingabe (Quick Add) wandert von Non-Goal zu In-Scope** — als
bewusst *enthaltenes* Phase-0-Experiment an der Companion-Grenze:

- YNAB bleibt uneingeschränkt Source of Truth. Quick Add ist ein zusätzlicher
  *Eingabeweg* nach YNAB, keine eigene Wahrheit (kein lokaler Transaktions-Store,
  der Push geht synchron und direkt an die YNAB-API).
- Alles Weitere der Ersatz-Reise (Phase 1+: Budget-View, Allocations, Goals,
  Move-Money, Cutover) **bleibt Non-Goal**, bis eine explizite Phase-1-Entscheidung
  fällt (siehe Abbruch-/Aktivierungskriterien in `docs/idea-ynab-replacement.md`).
- Die In-Scope-Formel aus ADR 0001 wird erweitert: "ein YNAB-Konzept bequemer aus BB
  heraus bedienbar machen" schließt jetzt auch *YNABs eigene manuelle Eingabe* ein —
  Quick Add macht nichts, was YNAB Mobile nicht auch könnte, nur schneller und im
  BB-Workflow.

ADR 0001 bleibt in Kraft; nur der eine Non-Goal-Punkt ist amendiert (Vermerk dort).

## Consequences
### Positive
- Der Frau-Workflow (Bar-Ausgaben am Phone) funktioniert, ohne YNAB Mobile zu öffnen.
- Phase 0 liefert reale Erfahrung für die spätere Phase-1-Entscheidung, mit minimalem
  Sunk Cost (1 Feature, kein eigener State).
- Die Scope-Grenze ist wieder ehrlich: Dokumentation und Code sagen dasselbe.

### Negative
- Die Companion-Grenze ist nicht mehr ganz so scharf wie in ADR 0001 formuliert —
  künftige "liegt doch nah"-Features brauchen ein bewusstes Urteil gegen diese
  präzisierte Linie (eigene Wahrheit = Non-Goal, bequemerer YNAB-Zugriff = verhandelbar).
- Schleichender Einstieg in die Ersatz-Reise ist ein reales Risiko; Gegenmittel ist
  die explizite Phase-1-Schwelle (eigene Categories/Allocations = neue Vision-Revision
  via `brainstorm` im Extend-Modus, wie in ADR 0001 vorgesehen).

### Neutral
- `docs/idea-ynab-replacement.md` bekommt damit erstmals einen umgesetzten Meilenstein
  (Phase 0 ✅), bleibt aber weiterhin Idee, nicht Roadmap.

## Alternatives considered
- **Quick Add wieder ausbauen** — abgelehnt: das Feature löst ein reales, dokumentiertes
  Bedürfnis und ist bereits deployed und getestet.
- **ADR 0001 komplett supersaden und Ersatz-Vision schreiben** — abgelehnt: Phase 1+
  ist weiterhin nicht entschieden; eine Vision-Revision jetzt wäre genau das
  Über-Engineering, das ADR 0001 verhindern wollte.

## References
- `.agentheim/knowledge/decisions/0001-companion-not-replacement.md` (amendiert)
- `docs/idea-ynab-replacement.md` (Phasenmodell, Abbruchkriterien)
- ADR 0004 (ynab-sync: technische Gestalt von Quick Add)
