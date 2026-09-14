---
title: "Mobile-Polish: Sticky-Filter, Spring-Easing, Transaktions-Skeleton"
status: done
bc: infrastructure
created: 2026-06-11
completed: 2026-06-11
captured: retroaktiv 2026-06-12 — Arbeit lief außerhalb von agentheim (Session feature/ux-wow)
related_adrs: [0005]
commits: [49a9469]
branch: feature/ux-wow
---

# Mobile-Polish: Sticky-Filter, Spring-Easing, Transaktions-Skeleton

## Problem
Drei Reibungspunkte im Review-Flow auf Mobile: Filter-Pills scrollten bei langen Listen
weg (Filterwechsel = hochscrollen); Sheet-Einfahrt wirkte mechanisch; der Lade-Zustand
zeigte einen generischen Spinner mit Layout-Sprung beim Eintreffen der echten Zeilen.

## Acceptance criteria
- [x] Filter-Pills sticky unter dem Header (Backdrop-Blur), Date-Header darunter neu verankert
- [x] Sheet-Einfahrt mit Spring-Easing (`linear()`) hinter `@supports`, Fallback bestehende Kurve
- [x] Lade-Zustand zeigt tx-row-förmige Skeletons (kein Layout-Sprung)
- [x] `prefers-reduced-motion` weiterhin global respektiert

## Outcome
`styles.css`: Sticky-Positionierung + `--sf-spring-out`-Token; `Loading.fs`:
`txListSkeleton`; `TransactionList.fs` nutzt ihn im Loading-Zweig. Design-System-Ebene,
kein fachlicher Context betroffen — daher hier in infrastructure abgelegt (offene Frage
"eigener design-system-Context?" bleibt offen, siehe context-map.md).

## Verification
- dotnet build + Fable-Build grün; rein präsentational, keine Logik-Tests nötig
