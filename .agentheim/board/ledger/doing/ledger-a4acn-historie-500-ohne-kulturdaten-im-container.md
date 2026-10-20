---
id: ledger-a4acn
title: "Historie liefert in Produktion bei jeder Buchung HTTP 500: ReadModel lädt CultureInfo de-DE, das es im Alpine-Container ohne ICU nicht gibt"
status: doing
type: bug
context: ledger
created: 2026-10-20
completed:
depends_on: []
blocks: []
tags: [backend, kassenbuch, historie, produktion, globalisierung]
related_adrs: [0013]
related_research: []
prior_art: [ledger-h9tq2, ledger-q5w9c]
---

## Why
Nach dem Deploy vom 2026-10-19 zeigt das Historie-Sheet bei jeder Buchung "Historie
konnte nicht geladen werden: Internal server error (500) while making request to
/api/LedgerApi/getEntryHistory" (Screenshot von Roman, 2026-10-20). Server-Log: "The type
initializer for '<StartupCode$Server>.$Ledger.QueryHandlers' threw an exception" bei
`ReadModel.get_de()` aus `formatMoney`. `ReadModel.de = CultureInfo.GetCultureInfo
"de-DE"`; die Runtime `mcr.microsoft.com/dotnet/aspnet:9.0-alpine` läuft im
Invariant-Globalization-Modus ohne ICU, dort gibt es "de-DE" nicht. Lokal (macOS mit ICU)
waren alle Tests grün, deshalb fiel es erst in Produktion auf.

## What
- Szenario kaputt: GIVEN der Server läuft ohne Kulturdaten (wie im Container), WHEN
  `getEntryHistory` eine Buchung beschreibt, THEN entsteht der Satz ohne Ausnahme, mit
  Betrag "-42,50 EUR".
- Szenario intakt: Tausenderpunkt und Dezimalkomma ("-1.234,56 EUR") und das Datum
  "04.03.2026" bleiben genau wie bisher.
- `src/Server/Ledger/QueryHandlers.fs`: `ReadModel` formatiert Zahlen mit einem eigenen
  `NumberFormatInfo` (aus `InvariantInfo` geklont, Dezimalkomma, Tausenderpunkt), Daten mit
  `InvariantCulture` und festem Format-String. Kein Zugriff auf benannte Kulturen.
- `src/Tests/Tests.fsproj`: `InvariantGlobalization=true`, damit die ganze Suite wie der
  Container ohne Kulturdaten läuft und dieselbe Fehlerklasse künftig lokal rot wird.
- `src/Client/Components/Ledger/View.fs`: `germanCulture ()` liefert `InvariantCulture`
  (feste numerische Formate, im Browser gleiche Ausgabe), damit die Client-Tests unter der
  invarianten Suite laufen.

## Acceptance criteria
- [ ] Ein Test hält fest, dass die Suite ohne Kulturdaten läuft (`GetCultureInfo "de-DE"`
      wirft), mit Kommentar, welchen Fehler er verhindert.
- [ ] Ohne den Fix sind die Historien-Tests unter der invarianten Suite rot (13 rote Tests
      vor dem Fix), mit dem Fix grün.
- [ ] Ein Test prüft "-1.234,56 EUR" (Tausenderpunkt, Dezimalkomma) im Historien-Satz.
- [ ] Die bestehenden Historien- und Datumstests (Erstbeobachtung "-42,50 EUR",
      `formatEntryDate`, `mirrorRunSubtitle`) bleiben unverändert grün.
- [ ] Kein Code in `src/Server` oder `src/Shared` greift auf eine benannte Kultur zu.
- [ ] `dotnet build` (0 Warnungen), `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün.
- [ ] Nach dem Deploy lädt die Historie in Produktion wieder (kein 500 im Server-Log bei
      `getEntryHistory`).

## Notes
Hotfix auf `main`, am 2026-10-20 vom Conductor direkt umgesetzt (Produktion betroffen),
danach auf Romans Wunsch über agentheim nachgetragen: Task erfasst, Fix vom Verifier mit
frischem Blick geprüft, per complete integriert. Kein Worker-Lauf; die Änderung lag schon
im Hauptbaum (Patch im Scratchpad gesichert).
