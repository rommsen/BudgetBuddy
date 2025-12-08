# Backlog

Ideen und Features für spätere Implementierung.

---

## Bugs

### Hohe Priorität

(keine)

### Mittlere Priorität

- [ ] **JSON-Fehlermeldung bei frühem Import-Klick**: Wenn man auf den Import-Button klickt bevor die TAN-Bestätigung erfolgt ist, wird nur rohes JSON als Fehlermeldung angezeigt. Stattdessen sollte eine verständliche Fehlermeldung erscheinen.

### Niedrige Priorität

---

## Features

### Hohe Priorität

- [x] **Uncleared Transaktionen**: YNAB Transaktionen bzw. Transaktionen die zu YNAB importiert werden, dürfen noch nicht gecleared sein ✅ (2025-12-07)
- [x] **Import ohne Kategorie**: Transaktionen müssen auch ohne Kategorie importiert werden können. Diese dürfen dann auf keinen Fall gecleared sein (Uncategorized-Workflow in YNAB) ✅ (2025-12-07)
- [ ] **Regel aus Zuweisungs-Dialog erstellen**: Direkt aus dem Dialog, in dem Transaktionen Kategorien zugeordnet werden, soll eine Regel erstellt werden können, sodass ähnliche Transaktionen beim nächsten Mal automatisch kategorisiert werden.

### Mittel-Hohe Priorität

- [ ] **Optionale Comdirect-PIN (On-Demand Abfrage)**: Die Comdirect-PIN soll nicht zwingend in den Settings gespeichert werden müssen. Wenn keine PIN gespeichert ist, wird sie bei jedem Sync-Abruf über ein Modal abgefragt. Wenn eine PIN gespeichert ist, wird diese automatisch verwendet. Optional: "PIN merken" Checkbox im Modal zum nachträglichen Speichern. Vorteil: Mehr Sicherheit für Benutzer, die sensible Daten nicht persistent speichern wollen.

### Mittlere Priorität

- [ ] **Cleared-Setting**: In den Settings einstellen können, ob YNAB Transaktionen beim Import als "cleared" oder "uncleared" markiert werden
- [ ] **Automatisch geskippte Transaktionen rot statt ausgegraut**: Transaktionen die automatisch als Duplikate erkannt und geskippt werden, werden rot dargestellt. Diese sollten stattdessen ausgegraut sein (wie andere geskippte Transaktionen).
- [ ] **Transfer-Unterstützung bei Payees**: Neben der Kategorie soll man auch "Transfer to/from" als Payee hinterlegen können. Nützlich z.B. für Barabhebungen, die nur eine Verschiebung zwischen Konten sind (Transfer to Bar) und keine Budget-Auswirkung haben.
- [ ] **Einzeilige Rules-Darstellung**: Rules werden aktuell zweizeilig dargestellt und nehmen viel Platz ein. Der gesamte Inhalt soll kompakt in einer Zeile angezeigt werden.

### Niedrige Priorität

- [ ] **ING Bank**: ING Bank als Datenquelle unterstützen

### Ideen / Someday


---

## Abgeschlossen

### Bugs

- [x] **Langsame Kategorie-Auswahl**: Optimistisches UI implementiert - UI aktualisiert sofort lokal statt auf Backend-Antwort zu warten ✅ (2025-12-08)

### Features

- [x] **Suchbare Kategorie-Selectboxen**: SearchableSelect Komponente mit Textfeld-Filter (contains-Logik) für SyncFlow und Rules ✅ (2025-12-08)

