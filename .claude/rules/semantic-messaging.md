---
paths: src/Server/**
---

# Semantic Messaging

Kein Wächter prüft Benennung; das ist Urteil. Die Begriffe stehen in der README des jeweiligen BC unter `.agentheim/knowledge/contexts/`.

- Befehle heißen nach der Absicht, nicht nach CRUD: `setEntryMemo`, `setReviewMark`, `setEntryCategoryBatch`, `restoreFlagBatch`, `retryPushToYnab`. Nie `CreateXInput`, `UpdateXInput`, `DeleteXInput`, `GetXInput`.
- Ereignisse stehen in der Vergangenheitsform und beschreiben, was passiert ist: `EntryObservedInYnab`, nicht `EntryUpdated`.
- Abfragen sind fachliche Lesesichten: `getLedgerAccounts`, `getLedgerAccountBalances`, `getBudgetMonth`.
- Ablauf im Kassenbuch: `src/Server/Api.fs` validiert und ruft den Befehl in `src/Server/Ledger/CommandHandlers.fs`, der Ereignisse schreibt; gelesen wird über `src/Server/Ledger/QueryHandlers.fs`. Neue Befehle folgen diesem Weg, statt Logik inline in `Api.fs` zu schreiben.
- Das Ereignis trägt, was seine Folgen brauchen (etwa die YNAB-Id für den Push). Wer ein Ereignis auswertet, lädt dafür nichts nach.
- Kein Befehls-DU ohne Aufrufer. Was nicht verdrahtet ist, wird gelöscht.
