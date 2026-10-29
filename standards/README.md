# standards/

Nur, was BudgetBuddy-spezifisch ist und nicht aus dem Code abzulesen. Generisches F#-, Giraffe- und Elmish-Wissen steht hier nicht.

| Datei | Inhalt |
|---|---|
| `standards/frontend/styleguide.md` | Das Gate für alle UI-Arbeit: Farbsemantik, Token-Layer, Komponenten, Motion, Muster, Microcopy. |
| `standards/frontend/overview.md` | Aufbau des Clients und Gelerntes. |
| `standards/backend/overview.md` | Aufbau des Servers, Giraffe-Pipeline mit `skipPipeline`, Konventionen. |

Weitere Quellen:

- Regeln je Dateipfad: `.claude/rules/` (Test-Doktrin in `.claude/rules/test-doctrine.md`).
- Fachbegriffe je Bounded Context: `.agentheim/knowledge/contexts/<bc>/README.md`.
- Entscheidungen: `.agentheim/knowledge/decisions/`.
- Gate: `./verify.sh`.
