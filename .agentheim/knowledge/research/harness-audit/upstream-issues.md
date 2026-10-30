# Upstream-Entwürfe für agentheim (BudgetBuddy)

Stand 2026-10-30, aus der Memory-Triage infrastructure-gmx27 (`triage-ergebnis.md`). Ergänzung zur Souls-Liste `/Users/romansachse/src/souls/.agentheim/knowledge/research/harness-audit/upstream-issues.md` (Entwürfe G1 bis G4, R1 bis R3, V1 bis V3, O1 bis O6, P1 bis P3): hier steht nur, was dort fehlt. Beobachtet mit agentheim 0.9.2 bis 0.9.6. Einreichen macht der Owner unter github.com/heimeshoff/agentheim, zusammen mit der Souls-Liste.

- **B1** Dashboard gruppiert nach dem `status`-Wert im Frontmatter, der Parser in `tree.mjs` entfernt keine Inline-Kommentare. Die Scaffold-Zeile `status: done  # backlog | ...` landet unter BACKLOG. Vorschlag: Kommentare strippen oder nach Ordner gruppieren.
- **B2** `deriveContext` schneidet den letzten `-xxxxx`-Block ab. BC-Namen mit Bindestrich (`ynab-sync`) ergeben `ynab`; `capture` lehnt mit `context-mismatch` ab, auch mit `{"context":"ynab-sync"}`. Vorschlag: BC aus dem Ordner auflösen, Override respektieren.
- **B3** Der env-freie Bootstrap nimmt die höchste Version im Plugin-Cache, nicht die geladene. Nach einem Downgrade läuft die neuere lib. Vorschlag: Version aus `installed_plugins.json`.
- **B4** Id-Grammatik verlangt einen Buchstaben vorn und ein Alphabet ohne i, l, o, u. Worker raten Ids. Vorschlag: Rückgabeformat verlangt keine Id, der Conductor mintet.
- **B5** Diff-Basis `git diff main HEAD` driftet nach sequentiellen Integrationen und auf Arbeitsbranches. Vorschlag: Diff gegen den Batch-Start bzw. `merge-base`.
- **B6** Lange Orchestrator-Ergebnisse kommen abgeschnitten an (ca. 4 KB). Vorschlag: Ergebnis als Datei, Rückgabe nur der Pfad.
- **B7** Konfliktleiter Stufe 1 (`git reset --hard HEAD`) löscht uncommittete Verifier-Notizen in `doing/` und FAIL-Einträge in `protocol.md`. Vorschlag: vorher sichern oder die Notizen sofort committen.
- **B8** `complete` übernimmt die Dauer vom Conductor, der sie schätzt. Vorschlag: Dauer aus Claim- und Complete-Zeitpunkt berechnen.
- **B9** `materializeTaskFile(rootDir, body)` erwartet die Repo-Wurzel; mit `.agentheim` entsteht `.agentheim/.agentheim/board/...`. Vorschlag: beides annehmen oder ablehnen.
- **B10** `complete` auf eine Datei, die schon in `done/` liegt, zieht den Doing-Zähler ab und lehnt bei 0 mit `bookkeeping-marker-mismatch` ab. Vorschlag: idempotenter Weg ohne Zählerabzug.
