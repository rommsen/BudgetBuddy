# Backlog

Ideen und Features für spätere Implementierung.

---

## Bugs

### Hohe Priorität

- [ ] **Langsame Kategorie-Auswahl**: Die Auswahl einer Kategorie in der Selectbox dauert fast eine Sekunde bis sie als ausgewählt angezeigt wird. Möglicherweise wird zu viel gerendert oder es passiert ein unnötiger Request im Hintergrund.

### Mittlere Priorität

- [ ] **JSON-Fehlermeldung bei frühem Import-Klick**: Wenn man auf den Import-Button klickt bevor die TAN-Bestätigung erfolgt ist, wird nur rohes JSON als Fehlermeldung angezeigt. Stattdessen sollte eine verständliche Fehlermeldung erscheinen.

### Niedrige Priorität

- [ ] **Automatisch geskippte Transaktionen rot statt ausgegraut**: Transaktionen die automatisch als Duplikate erkannt und geskippt werden, werden rot dargestellt. Diese sollten stattdessen ausgegraut sein (wie andere geskippte Transaktionen).

---

## Features

### Hohe Priorität

- [ ] **Uncleared Transaktionen**: YNAB Transaktionen bzw. Transaktionen die zu YNAB importiert werden, dürfen noch nicht gecleared sein
- [ ] **Import ohne Kategorie**: Transaktionen müssen auch ohne Kategorie importiert werden können. Diese dürfen dann auf keinen Fall gecleared sein (Uncategorized-Workflow in YNAB)

### Mittlere Priorität

- [ ] **Cleared-Setting**: In den Settings einstellen können, ob YNAB Transaktionen beim Import als "cleared" oder "uncleared" markiert werden
- [ ] **Transfer-Unterstützung bei Payees**: Neben der Kategorie soll man auch "Transfer to/from" als Payee hinterlegen können. Nützlich z.B. für Barabhebungen, die nur eine Verschiebung zwischen Konten sind (Transfer to Bar) und keine Budget-Auswirkung haben.

### Niedrige Priorität

- [ ] **ING Bank**: ING Bank als Datenquelle unterstützen

### Ideen / Someday


---

## Abgeschlossen

