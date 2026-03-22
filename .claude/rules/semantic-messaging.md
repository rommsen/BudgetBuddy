---
paths: src/Server/**
---

# Semantic Messaging — Commands verdrahten oder löschen

## Verboten

- Command DUs die nicht dispatcht werden (Dead Code)
- CRUD-Naming: `CreateXInput`, `UpdateXInput`, `DeleteXInput`, `GetXInput`
- Handler die direkt Input-DTOs verarbeiten wenn ein Command-DU existiert

## Richtig

- Commands als imperative Nachrichten: `InviteToSpace`, `CaptureMoment`, `ReviseThought`
- Events als Vergangenheitsform: `SpaceOpened`, `InviteRedeemed`, `MomentCaptured`
- Queries als semantische Reads: `getMomentsForSpace`, `getInvitesForSpace`
- Input-DTOs semantisch benennen: `InviteToSpaceInput`, `CaptureMomentInput`, `ReviseThoughtInput`
- Api.fs -> Validation -> Domain -> EventHandler Flow

## Grep-Checks

```bash
# Finde CRUD-Naming in Shared DTOs
grep -n "Create.*Input\|Update.*Input\|Delete.*Input\|Get.*Input" src/Shared/Domain.fs

# Prüfe ob Command-Module referenziert werden
grep -rn "open Server.Commands" src/

# Finde unbenutztes Command-Modul
grep -l "Command" src/Server/*.fs | while read f; do basename "$f"; done
```
