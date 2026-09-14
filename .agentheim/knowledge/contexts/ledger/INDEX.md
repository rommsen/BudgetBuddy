## ADRs scoped to this BC

<!-- adr-local:start -->
- **0032** — Geprüft heißt approved und cleared; Flags werden roh beobachtet, "Lena" am Flag-Namen erkannt — 2026-09-14 — `../../decisions/0032-pruefvermerk-ist-approved-und-cleared-flags-roh-beobachtet.md`
- **0029** — Seitenweise Kassenbuch-Liste, Kontostände-Leiste und Konto-Filter (AllAccounts vs. OneAccount) — 2026-09-12 — `../../decisions/0029-seitenweise-kassenbuch-liste-und-kontostaende-leiste.md`
- **0028** — Der Prüfvermerk ist die einzige schreibende Fähigkeit, die vor v2 nach BB wandert — 2026-09-12 — `../../decisions/0028-pruefvermerk-als-einzige-schreibende-faehigkeit-vor-v2.md`
- **0025** — Order-ID-Abgleich als eigener Actor im Kassenbuch, kein eigenes Ereignis — 2026-09-11 — `../../decisions/0025-order-id-abgleich-als-eigener-actor-kein-eigenes-ereignis.md`
- **0021** — BB-geführte Buchungen entstehen zuerst im Kassenbuch, der YNAB-Push folgt; die Verknüpfung ist ein eigenes Ereignis — 2026-09-11 — `../../decisions/0021-kassenbuch-zuerst-dann-push-verknuepfung-als-ereignis.md`
- **0020** — Im Kassenbuch einen Elmish-Auslöser mit Entscheidungslogik in eine reine und eine unreine Nachricht teilen (Reduzierer unter .NET testbar) — 2026-09-06 — `../../decisions/0020-elmish-ausloeser-in-reine-und-unreine-nachricht-teilen.md`
- **0019** — Unkategorisierte Split-Zeile ist ein benanntes drittes Split-Ziel (ergänzt ADR 0006) — 2026-09-06 — `../../decisions/0019-unkategorisierte-split-zeile-als-drittes-split-ziel.md`
- **0017** — Beträge im Kassenbuch als exakter Dezimal-Text, Kontostand in F# summiert — 2026-09-06 — `../../decisions/0017-betraege-als-exakter-dezimaltext-im-kassenbuch.md`
- **0016** — Kontenmodell (Art x Verknüpfung), Bewertung als Buchung, Bar/Quick-Add und ETF-Konto in v1 — 2026-09-06 — `../../decisions/0016-kontenmodell-und-bewertung-als-buchung.md`
- **0015** — Buchungs-Identität: eigene Id im Kassenbuch, Fremd-Ids als Verknüpfungen, Spiegel matcht YNAB-Id vor ImportId — 2026-09-06 — `../../decisions/0015-buchungs-identitaet-eigene-id-fremd-id-verknuepfung.md`
- **0013** — Kassenbuch-Historie als append-only Ereignis-Log mit synchron geführtem Zustandsmodell — 2026-09-06 — `../../decisions/0013-append-only-event-log-with-synchronous-state.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- concepts:end -->


## Pointers

- BC README (ubiquitous language, invariants): `README.md`
- Task board (tasks by status) for this BC: `../../../board/ledger/INDEX.md`
