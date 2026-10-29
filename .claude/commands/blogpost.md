---
description: Erstellt einen lokalen Blogpost aus protocol.md und git log für ein Zeitfenster
---

# BlogPost Command

Erstelle einen ausführlichen Blogpost über die Arbeit in einem Zeitfenster. Quellen sind die Einträge in `.agentheim/board/protocol.md` und `git log` für dieses Zeitfenster, ergänzt um den aktuellen Context Window. Der Post wird nur lokal unter `diary/_posts/` abgelegt. Es gibt keinen Veröffentlichungsschritt, nichts wird nach außen deployt.

## Anweisungen

### Phase 1: Änderungen analysieren und zur Auswahl stellen

1. **Sammle alle Änderungen** für das Zeitfenster:
   - Kläre das Zeitfenster: vom User genannt, sonst seit dem letzten Post in `diary/_posts/` (Datum im Dateinamen)
   - Lies die Einträge in `.agentheim/board/protocol.md`, die in das Zeitfenster fallen (nur den passenden Ausschnitt, nicht die ganze Datei)
   - Führe `git log --since=<start> --until=<ende> --stat` aus; für einzelne Commits bei Bedarf `git show <sha>`
   - Schau dir den aktuellen Context Window an, falls er zur Arbeit im Zeitfenster gehört
   - Gruppiere die Änderungen in logische Themenblöcke (z.B. "Feature X", "Bugfix Y", "Refactoring Z")

2. **Präsentiere die Änderungen dem User**:
   - Verwende das `AskUserQuestion` Tool mit `multiSelect: true`
   - Liste alle gefundenen Themenblöcke als Optionen auf
   - Jede Option sollte einen kurzen Titel und eine Beschreibung haben
   - Beispiel-Frage: "Welche Themen sollen in den Blogpost aufgenommen werden?"

3. **Warte auf die Auswahl** des Users bevor du weitermachst

### Phase 2: Blogpost erstellen (nur mit ausgewählten Themen)

4. **Analysiere die ausgewählten Themen im Detail**:
   - Welche Milestones/Features wurden umgesetzt?
   - Welche Dateien wurden erstellt/geändert?
   - Welche Probleme wurden gelöst?
   - Welche Tests wurden geschrieben?

5. **Erstelle einen Blogpost** in `diary/_posts/YYYY-MM-DD-titel.md` mit:

   ### Struktur:
   - **Titel**: Beschreibend und spezifisch (z.B. "Milestone X: Feature-Name – Hauptthema")
   - **Metadaten**: Datum, Autor (Claude), Thema
   - **Einleitung**: Was sollte erreicht werden? (2-3 Absätze)
   - **Ausgangslage**: Was war bereits vorhanden? (1-2 Absätze)
   - **Haupt-Herausforderungen**: 5-8 Challenges mit je:
     - Problem-Beschreibung
     - Verschiedene Lösungsansätze (mit Vor-/Nachteilen)
     - Gewählte Lösung (mit Code-Beispielen)
     - Architekturentscheidungen und deren Rationale
     - Warum diese Entscheidung getroffen wurde
   - **Lessons Learned**: Was würde ich anders machen?
   - **Fazit**: Was wurde erreicht? (mit Dateien/Statistiken)
   - **Key Takeaways für Neulinge**: 3 wichtigste Lernpunkte

   ### Stil:
   - **Für Neulinge verständlich**: Konzepte erklären, nicht voraussetzen
   - **Rationale überall**: Jede Entscheidung begründen
   - **Code-Beispiele**: Konkrete Implementierungen zeigen
   - **Trade-offs diskutieren**: Warum X statt Y?
   - **Lehrreich**: Architektur-Patterns erklären
   - **Persönlich**: Aus Ich-Perspektive ("Ich habe mich entschieden...")
   - **Deutsch**: Der Blogpost soll auf Deutsch sein

   ### Inhaltliche Schwerpunkte:
   - **Warum > Was**: Nicht nur beschreiben WAS gemacht wurde, sondern vor allem WARUM
   - **Entscheidungsprozess**: Welche Alternativen gab es? Warum wurde diese gewählt?
   - **Lernmomente**: Was war überraschend? Was war schwieriger als erwartet?
   - **Architektur**: Wie fügt sich das ins Gesamtsystem ein?
   - **Type-Safety**: Warum F#-Features (DUs, Options, Result) verwendet wurden
   - **Testing**: Wie wurde getestet? Warum diese Test-Strategie?

6. **Dateiname**:
   - Format: `YYYY-MM-DD-kurzbeschreibung.md`
   - Beispiel: `2025-11-29-milestone-4-comdirect-integration.md`

7. **Nach dem Schreiben**:
   - Nichts committen, pushen oder veröffentlichen: der Post bleibt eine lokale Datei
   - Gib eine kurze Zusammenfassung aus
   - Zeige den Dateipfad an
   - Liste die Haupt-Sections auf

## Beispiel-Herausforderung (zur Orientierung):

```markdown
## Herausforderung 3: Session-Management für Single-User-App

### Das Problem

Die Comdirect-Authentifizierung ist **stateful**: [Erklärung]

Wie speichern wir den Session-State?

### Optionen, die ich betrachtet habe

1. **In-Memory mit Mutable Refs** (gewählt)
   - Pro: [...]
   - Contra: [...]

2. **In der Datenbank**
   - Pro: [...]
   - Contra: [...]

### Die Lösung: ComdirectAuthSession.fs

[Code-Beispiel]

**Architekturentscheidung: Warum ein separates Modul?**

1. **Separation of Concerns**: [Erklärung]
2. **Testbarkeit**: [Erklärung]
3. **Klarheit**: [Erklärung]

**Rationale für Mutable Refs**:
- BudgetBuddy ist eine **Self-Hosted Single-User-App** [Erklärung warum das wichtig ist]
```

## Wichtig:

- **Keine generischen Phrasen**: Jede Aussage sollte spezifisch zur aktuellen Arbeit sein
- **Code-Beispiele aus echtem Code**: Keine erfundenen Beispiele
- **Konkrete Zahlen**: Test-Anzahl, Zeilen-Code, etc.
- **Ehrlichkeit**: Auch Fehler/Umwege erwähnen ("Lessons Learned")
- **Technische Tiefe**: Für Neulinge verständlich, aber technisch fundiert

Beginne jetzt mit Phase 1: Sammle die Änderungen und präsentiere sie dem User zur Auswahl!
