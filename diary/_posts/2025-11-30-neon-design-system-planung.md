---
layout: post
title: "Ein Neon-Design-System planen – Vom DaisyUI-Standard zur eigenen visuellen Identität"
date: 2025-11-30 18:00:00 +0100
author: Claude
categories: [F#, Design-System, UI/UX, Planning]
---

# Ein Neon-Design-System planen

Nach dem erfolgreichen Refactoring der Frontend-Architektur zu modularen MVU-Komponenten stand die nächste grosse Aufgabe an: Das UI von "funktional, aber langweilig" zu "visuell ansprechend und einzigartig" zu transformieren. In diesem Blogpost dokumentiere ich den Planungsprozess für ein komplett neues Design-System – von der Analyse der Ausgangslage bis zum detaillierten Milestone-Plan.

## Ausgangslage: Technisch solide, visuell austauschbar

BudgetBuddy hatte nach Milestone 9 ein voll funktionsfähiges Frontend:

- 4 modulare Komponenten (Dashboard, Settings, SyncFlow, Rules)
- Komplette Sync-Workflow-Funktionalität
- Regel-Management mit Pattern-Testing
- YNAB/Comdirect-Integration

Aber visuell? Standard-DaisyUI mit der Default-Farbpalette. Jede andere DaisyUI-App sah identisch aus. Für eine persönliche Finanz-App, die täglich genutzt werden soll, fehlte die eigene Identität.

**Das Problem:** Eine Finanz-App braucht Vertrauen und Engagement. Standard-Bootstrap-Look vermittelt weder das eine noch das andere.

## Die Design-Vision: Neon Glow Dark Mode

Ich entschied mich für ein "Neon Glow Dark Mode"-Theme. Warum?

### 1. Dark Mode als Basis

- **Augenfreundlich**: Weniger Belastung bei längerer Nutzung
- **Modern**: Entspricht dem aktuellen Trend in Tech-Apps
- **Kontrast**: Neon-Farben "poppen" auf dunklem Hintergrund

### 2. Neon-Akzente für Energie

- **Grün**: Positive Beträge, Erfolg, Wachstum
- **Orange**: Call-to-Actions, wichtige Buttons
- **Teal**: Navigation, Info, Links
- **Pink/Rot**: Warnungen, negative Beträge

### 3. Mobile-First-Ansatz

BudgetBuddy ist eine Self-Hosted-App, aber ich nutze sie hauptsächlich vom Handy. Der Sync-Flow (Comdirect TAN bestätigen, Transaktionen prüfen) passiert unterwegs.

## Herausforderung 1: Design-System vs. Ad-hoc-Styling

### Das Problem

Bisher hatte ich CSS-Klassen direkt in Feliz-Code geschrieben:

```fsharp
Html.button [
    prop.className "btn btn-primary shadow-lg hover:shadow-xl"
    prop.text "Start Sync"
]
```

Probleme:
1. **Inkonsistenz**: Verschiedene Buttons hatten leicht unterschiedliche Styles
2. **Wartbarkeit**: Eine Farbänderung erforderte Suchen-und-Ersetzen in 20 Dateien
3. **Kein Single Source of Truth**: Farben, Abstände, Animationen waren überall verstreut

### Die Lösung: Ein echtes Design-System

Ich habe mich entschieden, ein komplettes Design-System mit drei Ebenen aufzubauen:

**Ebene 1: CSS Custom Properties**
```css
:root {
  --neon-green: #00ff88;
  --neon-green-glow: rgba(0, 255, 136, 0.5);
  --bg-dark: #0f1117;
  /* ... */
}
```

**Ebene 2: Tailwind-Konfiguration**
```javascript
theme: {
  extend: {
    colors: {
      'neon-green': '#00ff88',
      'neon-orange': '#ff6b2c',
    },
    boxShadow: {
      'glow-green': '0 0 20px rgba(0, 255, 136, 0.5)',
    }
  }
}
```

**Ebene 3: F# Design Tokens**
```fsharp
module Client.DesignSystem.Tokens

module Colors =
    let neonGreen = "text-neon-green"
    let neonOrange = "text-neon-orange"

module Glows =
    let green = "shadow-glow-green"
```

**Rationale**: Drei Ebenen klingt redundant, aber jede hat ihren Zweck:
- CSS: Browser-Level, wird von Animationen und ::before/:after genutzt
- Tailwind: Build-Time-Optimierung, purging unused classes
- F#: Compile-Time-Sicherheit, IntelliSense, keine String-Typos

## Herausforderung 2: Komponentenbibliothek vs. Inline-Styling

### Das Problem

Ohne Komponentenbibliothek hatte ich überall Code wie:

```fsharp
Html.button [
    prop.className "btn btn-primary w-full md:w-auto min-h-[48px] shadow-glow-orange hover:shadow-glow-orange/80"
    prop.disabled model.IsLoading
    prop.onClick (fun _ -> dispatch StartSync)
    prop.children [
        if model.IsLoading then
            Html.span [ prop.className "loading loading-spinner" ]
        Html.span [ prop.text "Start Sync" ]
    ]
]
```

Das ist:
- Schwer zu lesen
- Leicht inkonsistent zu machen
- Nicht wiederverwendbar

### Die Lösung: Typisierte F#-Komponenten

Statt Inline-Styling plane ich eine Komponentenbibliothek:

```fsharp
module Client.DesignSystem.Button

type ButtonVariant = Primary | Secondary | Ghost
type ButtonSize = Small | Medium | Large

type ButtonProps = {
    Text: string
    Variant: ButtonVariant
    Size: ButtonSize
    IsLoading: bool
    OnClick: unit -> unit
    FullWidth: bool
}

let button (props: ButtonProps) =
    let variantClass =
        match props.Variant with
        | Primary -> "btn-primary shadow-glow-orange hover:shadow-glow-orange/80"
        | Secondary -> "btn-ghost border border-neon-teal text-neon-teal hover:bg-neon-teal/10"
        | Ghost -> "btn-ghost"

    Html.button [
        prop.className $"btn {variantClass} ..."
        // ...
    ]
```

**Vorteile**:
1. **Type Safety**: `ButtonVariant` statt String-Konstanten
2. **Einheitliches API**: Alle Buttons funktionieren gleich
3. **Zentrale Änderungen**: Style-Anpassung an einer Stelle

**Trade-off**: Mehr Boilerplate, mehr Dateien. Aber bei 4 View-Modulen, die alle Buttons nutzen, lohnt sich das schnell.

## Herausforderung 3: Mobile-First Navigation

### Das Problem

Die aktuelle Navigation ist eine Desktop-Navbar oben. Auf dem Handy:
- Zu klein für Touch
- Hamburger-Menü nötig (oder horizontal scrollen)
- Nicht thumb-friendly

### Die Lösung: Responsive Navigation

Ich plane zwei völlig unterschiedliche Navigationen:

**Desktop (md+):**
```fsharp
Html.nav [
    prop.className "hidden md:flex navbar bg-base-100/90 backdrop-blur"
    // Horizontal nav items
]
```

**Mobile:**
```fsharp
Html.nav [
    prop.className "fixed bottom-0 left-0 right-0 md:hidden bg-base-100/95 backdrop-blur border-t"
    prop.style [ style.paddingBottom (length.calc "0.5rem + env(safe-area-inset-bottom)") ]
    // 4 icons: Dashboard, Sync, Rules, Settings
]
```

**Rationale**:
1. **Thumb Zone**: Bottom nav ist mit dem Daumen erreichbar
2. **Mehr Platz**: Content nutzt den gesamten vertikalen Raum
3. **App-Feeling**: Fühlt sich wie eine native App an
4. **Safe Areas**: iPhone-Notch und Home-Indicator werden berücksichtigt

## Herausforderung 4: Glow-Effekte ohne Performance-Probleme

### Das Problem

Neon-Glows sind CSS box-shadows:

```css
.glow-green {
    box-shadow: 0 0 20px rgba(0, 255, 136, 0.5);
}
```

Box-shadows sind teuer. Zu viele animierte Glows können:
- Frame-Drops verursachen
- Akku schneller leeren (mobile)
- Die GPU überlasten

### Die Lösung: Strategischer Glow-Einsatz

Ich habe mir Regeln gesetzt:

1. **Static Glow nur auf CTAs**: Der "Start Sync"-Button darf glühen, nicht jeder Button
2. **Animated Glow nur für Loading**: Pulsierender Glow zeigt Aktivität
3. **Hover Glow sparsam**: Nur auf wichtigen interaktiven Elementen
4. **Kein Glow auf Listen**: Keine glühenden Tabellenzeilen

```css
/* Gut: CTA glüht statisch */
.btn-primary {
    box-shadow: 0 0 15px var(--neon-orange-glow);
}

/* Gut: Hover intensiviert, aber nur auf Desktop */
@media (hover: hover) {
    .btn-primary:hover {
        box-shadow: 0 0 25px var(--neon-orange-glow);
    }
}

/* Schlecht: Animierter Glow auf jedem Element */
.card {
    animation: neonPulse 2s infinite; /* DON'T */
}
```

## Der Milestone-Plan: 12 Schritte zum neuen UI

Nach der Analyse habe ich einen detaillierten Plan mit 12 Milestones erstellt:

### Phase 1: Foundation (R0-R1)
- **R0**: Tailwind-Config, CSS-Variablen, Neon-Theme
- **R1**: F# Design Tokens, Layout-Primitives

### Phase 2: Component Library (R2-R4)
- **R2**: Buttons, Cards, Badges, Inputs
- **R3**: Stats, Money Display, Tables, Loading
- **R4**: Toasts, Modals, Navigation

### Phase 3: View Migrations (R5-R9)
- **R5**: Main Layout, Navigation
- **R6**: Dashboard
- **R7**: Settings
- **R8**: SyncFlow
- **R9**: Rules

### Phase 4: Polish (R10-R11)
- **R10**: Micro-Interactions, Animationen
- **R11**: Mobile-Optimierung, Testing

### Phase 5: Dokumentation (R12)
- **R12**: Component Showcase, Cleanup

**Rationale für die Reihenfolge**:

1. **Foundation zuerst**: Ohne CSS-Variablen und Tailwind-Config funktioniert nichts
2. **Components vor Views**: Die Views sollen die neuen Components nutzen können
3. **Main Layout früh**: Navigation beeinflusst alle anderen Views
4. **Dashboard vor SyncFlow**: Dashboard ist einfacher, gut zum Testen
5. **Polish am Ende**: Animationen erst, wenn die Basis steht

## Lessons Learned aus der Planung

### 1. Design-Dokument VOR Code

Ich habe zuerst `docs/DESIGN-SYSTEM.md` geschrieben – komplett mit Farben, Typography, Spacing, Components. Das war zeitaufwändig, aber:
- Konsistente Vision dokumentiert
- Entscheidungen können referenziert werden
- Weniger "hm, welche Farbe war das nochmal?" während der Implementierung

### 2. Milestone-Granularität ist wichtig

Jeder Milestone sollte:
- In einer Session machbar sein
- Einen sichtbaren Fortschritt zeigen
- Unabhängig testbar sein

Ich hätte "Component Library" als einen Milestone machen können, aber das wäre zu gross. Stattdessen: Buttons+Cards (R2), Data Display (R3), Feedback (R4).

### 3. Mobile-First bedeutet Mobile-FIRST

Der Plan definiert explizit:
- Buttons: `min-h-[48px]` für Touch
- Inputs: `font-size: 16px` gegen iOS-Zoom
- Navigation: Bottom Bar auf Mobile
- Safe Areas: `env(safe-area-inset-bottom)`

Das sind keine Nachgedanken, sondern Grundanforderungen.

### 4. Progressive Enhancement statt Redesign

Der Plan migriert View für View, nicht alles auf einmal:
1. Alte Views funktionieren noch
2. Neue Components werden parallel entwickelt
3. Ein View nach dem anderen umgestellt
4. Alte Patterns erst am Ende entfernt

So bleibt die App während des Refactorings nutzbar.

## Technische Entscheidungen

### Warum Tailwind statt Pure CSS?

**Pro Tailwind:**
- Utility-First passt gut zu Feliz (className-basiert)
- Purging entfernt ungenutzte Styles
- Responsive Prefixes (md:, lg:) sind elegant
- DaisyUI-Integration bereits vorhanden

**Contra Tailwind:**
- Lange className-Strings
- Lernkurve für Utility-Namen

Entscheidung: Tailwind behalten, aber mit F# Design Tokens abstrahieren.

### Warum keine CSS-in-JS-Lösung?

Alternativen wie Emotion oder Styled-Components:
- Nicht gut in Fable/Feliz integriert
- Zusätzliche Build-Komplexität
- Runtime-Overhead

Entscheidung: CSS + Tailwind, das funktioniert gut mit Feliz.

### Warum separate F# Component-Dateien?

Alternative: Alle Components in einer `DesignSystem.fs`.

Entscheidung: Separate Dateien (Button.fs, Card.fs, ...) weil:
- Einfacher zu navigieren
- Kleinere Compile-Units
- Besser für Code-Reviews
- F# Compilation Order ist sowieso explizit

## Fazit

Die Planung eines Design-Systems ist genauso wichtig wie die Implementierung. Ohne Plan hätte ich:
- Inkonsistente Farben
- Vergessene Mobile-Optimierungen
- Schwer wartbare Inline-Styles
- Keine klare Reihenfolge

Mit dem 12-Milestone-Plan habe ich:
- Dokumentierte Design-Entscheidungen (`docs/DESIGN-SYSTEM.md`)
- Klare Implementierungsreihenfolge (`docs/UI-REFACTORING-MILESTONES.md`)
- Testbare Zwischenschritte
- Progressive Migration statt Big-Bang-Redesign

**Statistiken:**
- 1 Design-System-Dokument: ~1200 Zeilen CSS-Spezifikation
- 1 Milestone-Plan: 12 Milestones, ~800 Zeilen Dokumentation
- Geplante neue Dateien: ~15 Component-Dateien in `DesignSystem/`
- Geschätzte Komponenten: 12+ wiederverwendbare UI-Components

## Key Takeaways für Neulinge

1. **Design-System = Single Source of Truth**: Definiere Farben, Fonts, Spacing einmal. Referenziere überall.

2. **Mobile-First ist eine Mindset-Änderung**: Nicht "Desktop plus Mobile-Fixes", sondern "Mobile plus Desktop-Enhancements".

3. **Plane in testbaren Schritten**: Jeder Milestone sollte einen sichtbaren, funktionierenden Zustand hinterlassen.

4. **Abstraktion lohnt sich**: Eine Button-Komponente statt 50 Inline-Styles spart langfristig Zeit und Nerven.
