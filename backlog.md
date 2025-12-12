# Backlog

Ideen und Features für spätere Implementierung.

---

## Bugs

### Hohe Priorität

(keine)

### Mittlere Priorität

(keine)

### Niedrige Priorität

---

## Features

### Hohe Priorität

- [x] **Uncleared Transaktionen**: YNAB Transaktionen bzw. Transaktionen die zu YNAB importiert werden, dürfen noch nicht gecleared sein ✅ (2025-12-07)
- [x] **Import ohne Kategorie**: Transaktionen müssen auch ohne Kategorie importiert werden können. Diese dürfen dann auf keinen Fall gecleared sein (Uncategorized-Workflow in YNAB) ✅ (2025-12-07)
- [x] **Regel aus Zuweisungs-Dialog erstellen**: Direkt aus dem Dialog, in dem Transaktionen Kategorien zugeordnet werden, soll eine Regel erstellt werden können, sodass ähnliche Transaktionen beim nächsten Mal automatisch kategorisiert werden. ✅ (2025-12-09)

### Mittel-Hohe Priorität

- [ ] **Optionale Comdirect-PIN (On-Demand Abfrage)**: Die Comdirect-PIN soll nicht zwingend in den Settings gespeichert werden müssen. Wenn keine PIN gespeichert ist, wird sie bei jedem Sync-Abruf über ein Modal abgefragt. Wenn eine PIN gespeichert ist, wird diese automatisch verwendet. Optional: "PIN merken" Checkbox im Modal zum nachträglichen Speichern. Vorteil: Mehr Sicherheit für Benutzer, die sensible Daten nicht persistent speichern wollen.

### Mittlere Priorität

- [ ] **Split Transaction UI**: Backend und State-Management für Split-Transaktionen existieren bereits, aber die UI fehlt komplett (kein Split-Button, kein Modal). Alternativ: Feature entfernen, da Splits auch direkt in YNAB erledigt werden können.
- [ ] **Cleared-Setting**: In den Settings einstellen können, ob YNAB Transaktionen beim Import als "cleared" oder "uncleared" markiert werden
- [x] **Automatisch geskippte Transaktionen rot statt ausgegraut**: Transaktionen die automatisch als Duplikate erkannt und geskippt werden, werden rot dargestellt. Diese sollten stattdessen ausgegraut sein (wie andere geskippte Transaktionen). ✅ (2025-12-09)
- [ ] **Transfer-Unterstützung bei Payees**: Neben der Kategorie soll man auch "Transfer to/from" als Payee hinterlegen können. Nützlich z.B. für Barabhebungen, die nur eine Verschiebung zwischen Konten sind (Transfer to Bar) und keine Budget-Auswirkung haben.

### Niedrige Priorität

- [ ] **ING Bank**: ING Bank als Datenquelle unterstützen

### Ideen / Someday


---

## Abgeschlossen

### Bugs

- [x] **JSON-Fehlermeldung bei frühem Import-Klick**: Comdirect-JSON-Fehlermeldungen werden jetzt geparst und benutzerfreundliche Nachrichten angezeigt. Bei TAN_UNGUELTIG wird z.B. "TAN-Freigabe über die App wurde noch nicht erteilt." angezeigt statt rohem JSON. ✅ (2025-12-11)
- [x] **Langsame Kategorie-Auswahl**: Optimistisches UI implementiert - UI aktualisiert sofort lokal statt auf Backend-Antwort zu warten ✅ (2025-12-08)

### Features

- [x] **Regel aus Zuweisungs-Dialog erstellen**: Inline-Regel-Erstellung direkt beim Kategorisieren. Button + expandierendes Formular, Auto-Apply auf andere pending Transaktionen ✅ (2025-12-09)
- [x] **Einzeilige Rules-Darstellung**: Kompakte Single-Line Darstellung für Rules implementiert ✅ (2025-12-09)
- [x] **Suchbare Kategorie-Selectboxen**: SearchableSelect Komponente mit Textfeld-Filter (contains-Logik) für SyncFlow und Rules ✅ (2025-12-08)

