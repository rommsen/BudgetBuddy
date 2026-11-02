# budgeting: Index (knowledge)

Catalog of ADRs, research, and concept synthesis pages scoped to this bounded context.

> Updated by: `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).
> Hand-edits are fine but the skills will append at the section markers below.

---

## ADRs scoped to this BC

<!-- adr-local:start -->
- **ADR 0076** — Nachtrag zu 0074: Monatsrahmen je Seitenaufruf im Client, eigenes Endpoint getBudgetFrame, Zusammenführen im Client, kein Server-Cache. — `decisions/0076-nachtrag-monatsrahmen-je-seitenaufruf.md`
- **ADR 0074** — Freie Monate: Monatswert, Fenster aus GET /months, Fähigkeiten je Monatsart — `decisions/0074-freie-monate-aus-dem-ynab-fenster.md`
- **ADR 0073** — Pause nur in BB, je Kategorie und Monat, nicht nach YNAB — `decisions/0073-pause-nur-in-bb.md`
- **ADR 0072** — Targets über BB: YNAB führt, BB bearbeitet und zieht nach, nur geänderte Felder — `decisions/0072-targets-ueber-bb-ynab-fuehrt.md`
- **0070**: Gewinnt YNAB beim erneuten Senden an der Quelle, schließt das ungesendete Ziel als "in YNAB geändert"; die Verschiebung ist damit erledigt, Roman muss das Geld gegebenenfalls von Hand verschieben.
- **0069**: Verschieben und Zuweisen als Budget-Bewegungen (Herkünfte "Verschieben" und "Zuweisen", Gruppen-Id), relativ zum frisch gelesenen Zugewiesen angewendet, erst festhalten dann senden, gleiche Sperre je Monat · 2026-10-29 · `../../decisions/0069-verschieben-und-zuweisen-als-budget-bewegungen.md`
- **0065**: Budget-Beträge in Stufe 1 live aus YNAB, nicht in BB gespeichert; Targets anwenden hält je Kategorie eine unveränderliche Budget-Bewegung fest, Ergebnisse werden angehängt, dann ein PATCH je Kategorie mit Abbruch bei 429 · 2026-10-26 · `../../decisions/0065-budget-zustand-live-aus-ynab-budget-bewegungen-in-bb.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
- **ynab-reihenfolge-monate-target-schreiben-2026-11-02** — Reihenfolge kommt aus GET /categories, Monate aus GET /months, nur geänderte Target-Felder senden — `research/ynab-reihenfolge-monate-target-schreiben-2026-11-02.md`
- **ynab-api-targets-zuweisung** — YNAB-API: Targets lesen, Fehlbetrag goal_under_funded, Zuweisen per PATCH, Verfügbar pro Monat — 2026-10-25 — `../../research/ynab-api-targets-zuweisung-2026-10-25.md`
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- concepts:end -->

## Pointers

- BC README (ubiquitous language, invariants): `README.md`
- Global ADR that created this BC: `../../decisions/0063-budget-ist-die-naechste-faehigkeit-vor-der-etf-bewertung.md`
- Task board (tasks by status) for this BC: `../../../board/budgeting/INDEX.md`
