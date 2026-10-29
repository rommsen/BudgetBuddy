# budgeting: Index (knowledge)

Catalog of ADRs, research, and concept synthesis pages scoped to this bounded context.

> Updated by: `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).
> Hand-edits are fine but the skills will append at the section markers below.

---

## ADRs scoped to this BC

<!-- adr-local:start -->
- **0069**: Verschieben und Zuweisen als Budget-Bewegungen (Herkünfte "Verschieben" und "Zuweisen", Gruppen-Id), relativ zum frisch gelesenen Zugewiesen angewendet, erst festhalten dann senden, gleiche Sperre je Monat · 2026-10-29 · `../../decisions/0069-verschieben-und-zuweisen-als-budget-bewegungen.md`
- **0065**: Budget-Beträge in Stufe 1 live aus YNAB, nicht in BB gespeichert; Targets anwenden hält je Kategorie eine unveränderliche Budget-Bewegung fest, Ergebnisse werden angehängt, dann ein PATCH je Kategorie mit Abbruch bei 429 · 2026-10-26 · `../../decisions/0065-budget-zustand-live-aus-ynab-budget-bewegungen-in-bb.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
- **ynab-api-targets-zuweisung** — YNAB-API: Targets lesen, Fehlbetrag goal_under_funded, Zuweisen per PATCH, Verfügbar pro Monat — 2026-10-25 — `../../research/ynab-api-targets-zuweisung-2026-10-25.md`
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- concepts:end -->

## Pointers

- BC README (ubiquitous language, invariants): `README.md`
- Global ADR that created this BC: `../../decisions/0063-budget-ist-die-naechste-faehigkeit-vor-der-etf-bewertung.md`
- Task board (tasks by status) for this BC: `../../../board/budgeting/INDEX.md`
