---
schema_version: 1
project: BudgetBuddy
---

# Nächste Schritte: PWA-Idee erfasst — Refine das Icon (Gate) oder die Mechanik

PWA-Umsetzung gecaptured (`39a70a6`), sauber gespalten in zwei Backlog-Tasks: **design-system-008**
(App-Icon — neon-on-dark Master-Mark + Icon-Set + Theme-Farbe, durchs Styleguide-Gate) und
**infra-002** (Mechanik — `vite-plugin-pwa`, Manifest, iOS-Meta, Shell-Precache-SW). Scope steht:
**nur installierbar, kein Daten-Caching** (BB ist Live-Daten-Companion), Icon entwerfe ich selbst,
HTTPS via Tailscale ✓. `infra-002` hängt an `design-system-008` (Manifest braucht die Icons) → das
**Icon ist der kritische Pfad**. Beide sind noch `backlog` (under-refined); der eigentliche
Design-Schritt — *wie sieht der Mark aus* — fehlt noch. Davon getrennt: aus der letzten Session
liegen weiter ungepushte Commits lokal (ds-007 + dieser Capture).

<options>
  <option title="Refine: App-Icon-Mark" cmd='/agentheim:modeling refine design-system-008'>Der eigentliche Design-Schritt + Gate: was zeigt der Mark (Monogramm? Sync-Symbol? Bank→YNAB-Motiv?), Master-SVG + Theme-Farbe festlegen, von dir abnehmen. Entsperrt infra-002 — der kritische Pfad.</option>
  <option title="Refine: PWA-Mechanik" cmd='/agentheim:modeling refine infra-002'>Die Infra-Fragen klären (SW-Update-Strategie autoUpdate vs prompt, SW-scope hinter dem Tailscale-Proxy, iOS-Splash ja/nein) — kann parallel zur Icon-Frage laufen, aber bauen lässt sich's erst mit Icon.</option>
  <option title="Push + Deploy der offenen Commits">Die lokalen ungepushten Commits (ds-007 Icon-Lift + dieser Capture) nach origin; ds-007 ist ein Code-Change → Re-Deploy sinnvoll. Die PWA-Tasks selbst sind reines Markdown, brauchen keinen Deploy.</option>
  <option title="Nichts — Backlog steht">Idee ist sicher erfasst; PWA später angehen, jetzt erstmal BB im Alltag nutzen.</option>
</options>
