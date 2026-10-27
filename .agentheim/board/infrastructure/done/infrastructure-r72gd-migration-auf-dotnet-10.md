---
id: infrastructure-r72gd
title: Migration von .NET 9 auf .NET 10, nachdem brew upgrade die Runtime 9 entfernt hat
status: done
type: chore
context: infrastructure
created: 2026-10-27
completed: 2026-10-27
depends_on: []
blocks: []
tags: [dotnet, nuget, docker, nachgetragen]
related_adrs: []
related_research: []
prior_art: [infra-001]
---

## Why
Ein `brew upgrade` am 2026-10-27 hob das Cask `dotnet-sdk` auf 10.0.403. Danach war lokal nur noch die Runtime 10.0.14 installiert. `dotnet build` lief weiter (SDK 10 kompiliert net9.0), `dotnet test` und `dotnet run` brachen mit `Framework: 'Microsoft.NETCore.App', version '9.0.0'` ab. Damit liefen weder die Tests noch der Server noch die Playwright-Wächter in `scripts/`. .NET 9 ist STS und endet im November 2026, .NET 10 ist LTS.

## What
Alle vier Projekte auf net10.0 umstellen, die Docker-Images auf 10.0 heben und die an die Runtime gebundene Paketversion mitziehen.

## Acceptance criteria
- [x] `TargetFramework` net10.0 in `src/Client/Client.fsproj`, `src/Shared/Shared.fsproj`, `src/Server/Server.fsproj`, `src/Tests/Tests.fsproj`.
- [x] `Microsoft.Data.Sqlite` in Server und Tests auf dieselbe Version 10.0.14 (Gleichstand aus infra-001 bleibt).
- [x] `Dockerfile`: `dotnet10-sdk` in der Fable-Stufe, `sdk:10.0-alpine`, `aspnet:10.0-alpine`.
- [x] `dotnet build` ohne Warnungen, `dotnet test` grün.
- [x] `npm run build` und `docker build` gelingen.

## Notes
Nachgetragen: Die Arbeit lief am 2026-10-27 außerhalb eines agentheim-`work`-Laufs, direkt in einer Sitzung. Kein Verifier-Agent, deshalb steht die Verifikation im Protokoll als übersprungen.

ADR 0015 (`Guid.CreateVersion7()`, ".NET 9 liefert das nativ") und ADR 0037 (`aspnet:9.0-alpine`) nennen .NET 9 als damaligen Stand. Sie bleiben als Historie unverändert; beide Aussagen gelten unter .NET 10 weiter.

## Outcome
Geändert: die vier fsproj, `Dockerfile` (drei Zeilen), `scripts/measure-ledger-sort.fsx` (`#r`-Pfade auf `bin/Debug/net10.0`), `README.md` (Voraussetzung .NET 10 SDK), `.agentheim/knowledge/contexts/infrastructure/README.md` (Stack .NET 10), `diary/development.md`.

Neu in `src/Shared/Shared.fsproj`: direkte Referenz `Newtonsoft.Json` 13.0.3. Der NuGet-Audit prüft unter SDK 10 auch transitive Pakete und meldete NU1903 (GHSA-5crp-9r3c-p9vr) für das 12.0.3 aus `Fable.Remoting.Giraffe` 5.16.0. Server nutzt 13.0.3 schon direkt.

Ergebnis: `dotnet build` nach Löschen von bin/obj 0 Warnungen, 0 Fehler. `dotnet test` 2412 bestanden, 6 übersprungen (YNAB-Integration ohne .env), 0 Fehler. `npm run build` (Fable/Vite) grün. `docker build` grün. `npm run test:scripts`: alle 33 Dateien grün (die 6 Dateien vor `ledger-category-change` im ersten Lauf, `ledger-category-change` einzeln 7/7, die 26 Dateien danach mit `--test-concurrency=1` 217/217). Ein erster Lauf mit Standard-Parallelität lief parallel zu einem `verify.sh` in einem anderen Repo, hing nach etwa einer Stunde in `ledger-category-change` und brachte 60-s-Timeouts in `ledger-lena`, `ledger-memo` und `ledger-not-in-ynab`. Alle drei liefen seriell grün.

Kein ADR: Versionswechsel innerhalb des gewählten Stacks.
