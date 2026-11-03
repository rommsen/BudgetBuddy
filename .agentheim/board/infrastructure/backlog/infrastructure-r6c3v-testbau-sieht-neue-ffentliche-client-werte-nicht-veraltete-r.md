---
id: infrastructure-r6c3v
title: "Testbau sieht neue öffentliche Client-Werte nicht: veraltete Referenz-Assembly"
status: backlog
type: bug
context: infrastructure
created: 2026-11-03
completed:
depends_on: []
blocks: []
tags: [build, test]
related_adrs: []
related_research: []
prior_art: [infrastructure-t5m2b]
---

## Why
Bei infrastructure-t5m2b zweimal aufgetreten: Wird im Client ein `[<Literal>]` neu angelegt oder ein `private let` öffentlich, meldet der Bau von `src/Tests` "not defined". `src/Client/obj/Debug/net10.0/ref/Client.dll` und `refint/Client.dll` bleiben veraltet, obwohl `Client.dll` neu gebaut ist. Löschen beider Dateien behebt es. `./verify.sh` ist davon betroffen, solange `obj/` besteht.

## What
Ursache klären, etwa durch `ProduceReferenceAssembly=false` in `Client.fsproj` oder eine bessere Zuordnung der Eingaben, und den Weg ohne Handarbeit stabil machen.

## Acceptance criteria
- [ ] Regressionstest oder reproduzierbarer Ablauf: neues `[<Literal>]` im Client, Testbau sieht es ohne Löschen von `obj/`.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.