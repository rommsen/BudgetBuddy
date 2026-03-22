---
paths:
  - "src/Client/**"
  - "product/**"
  - "diary/**"
---

# Lokalisierung — Sprachregeln für das Projekt

<!--
AKTIVIERUNG: Entferne den Kommentar um die Regel zu aktivieren die zu deinem Projekt passt.
Lösche die nicht zutreffenden Regeln.
-->

<!-- OPTION A: Deutsches Projekt

## Deutsche Sonderzeichen — Echte Umlaute verwenden

**Bei ALLEM deutschen Text: IMMER echte Umlaute und ß verwenden.**

- ä, ö, ü, Ä, Ö, Ü, ß — NIEMALS Ersatzschreibweisen (ae, oe, ue, ss)
- Gilt für: UI-Texte, Dokumentation, Design, Microcopy, Kommentare auf Deutsch
- Gilt NICHT für: Code-Identifier, CSS-Klassen, Variablennamen (die bleiben Englisch)

**Beispiele:** "fuer" → "für", "ueber" → "über". **Wenn du Ersatzschreibweisen verwendest, STOPPE und korrigiere sofort.**

### Grep-Checks

```bash
# Finde Ersatzschreibweisen in Client-Code (deutsche UI-Texte)
grep -rn '".*fuer\|".*ueber\|".*Aenderung\|".*Ueberblick' src/Client/

# Finde Ersatzschreibweisen in Dokumentation
grep -rn 'fuer \|ueber \|Aenderung\|Ueberblick\|Pruefen\|Ausfuehren' product/ diary/
```

-->

<!-- OPTION B: Mehrsprachiges Projekt

## Sprachregeln

- **Code:** Englisch (Variablen, Funktionen, Kommentare)
- **UI-Texte:** [Primärsprache], [Sekundärsprache]
- **Dokumentation:** [Sprache]
- **Domain Terms:** Englisch im Code, [Primärsprache] in User-facing Texten

-->
