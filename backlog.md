# Backlog

Ideen und Features für spätere Implementierung.

---

## Bugs

### Hohe Priorität

- [ ] **Langsame Kategorie-Auswahl**: Während der Zuordnung von Kategorien zu Transaktionen: Die Auswahl einer Kategorie in der Selectbox dauert fast eine Sekunde bis sie als ausgewählt angezeigt wird. Möglicherweise wird zu viel gerendert oder es passiert ein unnötiger Request im Hintergrund.

### Mittlere Priorität

- [ ] **JSON-Fehlermeldung bei frühem Import-Klick**: Wenn man auf den Import-Button klickt bevor die TAN-Bestätigung erfolgt ist, wird nur rohes JSON als Fehlermeldung angezeigt. Stattdessen sollte eine verständliche Fehlermeldung erscheinen.

### Niedrige Priorität

---

## Features

### Hohe Priorität

- [x] **Uncleared Transaktionen**: YNAB Transaktionen bzw. Transaktionen die zu YNAB importiert werden, dürfen noch nicht gecleared sein ✅ (2025-12-07)
- [x] **Import ohne Kategorie**: Transaktionen müssen auch ohne Kategorie importiert werden können. Diese dürfen dann auf keinen Fall gecleared sein (Uncategorized-Workflow in YNAB) ✅ (2025-12-07)
- [ ] **Regel aus Zuweisungs-Dialog erstellen**: Direkt aus dem Dialog, in dem Transaktionen Kategorien zugeordnet werden, soll eine Regel erstellt werden können, sodass ähnliche Transaktionen beim nächsten Mal automatisch kategorisiert werden.

### Mittlere Priorität

- [ ] **Cleared-Setting**: In den Settings einstellen können, ob YNAB Transaktionen beim Import als "cleared" oder "uncleared" markiert werden
- [ ] **Automatisch geskippte Transaktionen rot statt ausgegraut**: Transaktionen die automatisch als Duplikate erkannt und geskippt werden, werden rot dargestellt. Diese sollten stattdessen ausgegraut sein (wie andere geskippte Transaktionen).
- [ ] **Transfer-Unterstützung bei Payees**: Neben der Kategorie soll man auch "Transfer to/from" als Payee hinterlegen können. Nützlich z.B. für Barabhebungen, die nur eine Verschiebung zwischen Konten sind (Transfer to Bar) und keine Budget-Auswirkung haben.
- [ ] **Einzeilige Rules-Darstellung**: Rules werden aktuell zweizeilig dargestellt und nehmen viel Platz ein. Der gesamte Inhalt soll kompakt in einer Zeile angezeigt werden.
- [ ] **Suchbare Kategorie-Selectboxen**: Beide Category Select Boxen (im Zuweisungs-Dialog und Rules) sollen durchsuchbar sein. Beim Öffnen der Selectbox soll der Fokus direkt auf einem Textfeld liegen, das die Kategorien mit "contains"-Logik filtert (Suche auch in der Mitte des Namens möglich).

### Niedrige Priorität

- [ ] **ING Bank**: ING Bank als Datenquelle unterstützen

### Ideen / Someday


---

## Abgeschlossen

