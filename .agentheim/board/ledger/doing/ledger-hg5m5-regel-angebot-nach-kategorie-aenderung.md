---
id: ledger-hg5m5
title: "Regel aus der Buchung: „Regel anlegen“ im Detail und beim Durchgehen, dazu ein Angebot nach einer Kategorie-Änderung, vorbelegt mit Payee und Kategorie"
status: doing
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [design-system-001-styleguide, ledger-sfab0, categorization-n3sxw]
blocks: []
tags: [etappe-3, frontend, kassenbuch, kategorie, regeln]
related_adrs: [0055, 0087]
related_research: []
prior_art: [ledger-k2rbn]
---

## Why
Bis zum Import direkt (banking-import-d0fey, 613eee26) legte Roman Regeln an der Importzeile im Sync an,
vorbelegt aus der Buchung. Mit dem SyncFlow ist das weggefallen. Entschieden (Roman, 2026-11-11): alle
Funktionen der heutigen Prüfung bleiben, „Regel aus der Buchung“ kommt mit Schnitt 6. Wer eine Kategorie von
Hand ändert, wiederholt das oft für denselben Payee.

## What
Zwei Einstiege in dasselbe Regel-Formular aus categorization-n3sxw, vorbelegt aus der Buchung (Roman,
2026-11-13: „Beides“):
1. Ein fester Eintrag „Regel anlegen“ im Buchungs-Detail und auf der Karte beim Durchgehen.
2. Ein Angebot im Buchungs-Detail, nachdem Roman die Kategorie einer Buchung mit Payee geändert hat.

Vorbelegung: Name = Payee, Wenn = Enthält, Suchen in = Payee, Muster = Payee, Dann Kategorie = Kategorie der
Buchung. Roman sieht und ändert alles vor dem Anlegen.

## Acceptance criteria

### Fester Eintrag
- [ ] Das Buchungs-Detail (PC Panel, Handy Sheet) hat unter den Feldern den Eintrag „Regel anlegen“
      (`Detail.forwardArrow`-Zeile oder gleich aussehend). Er öffnet das Regel-Formular aus
      categorization-n3sxw, das den Inhalt von Panel bzw. Sheet ersetzt und mit „Zurück“ zur Buchung führt
      (§7.15).
- [ ] Die Karte beim Durchgehen hat denselben Eintrag. Er öffnet das Formular am PC als `Modal` (Seite ohne
      Liste, wie „Neue Buchung“ auf der Übersicht), am Handy als Sheet. Nach Anlegen oder Abbrechen steht
      dieselbe Karte da, Fortschritt unverändert.
- [ ] Die Vorbelegung ist wie oben. Hat die Buchung keine Kategorie, eine Aufteilung oder ist sie ein
      Übertrag, bleibt „Dann Kategorie“ leer. Ohne Payee gibt es den Eintrag nicht.
- [ ] Nach „Regel anlegen“ meldet ein Toast „Regel angelegt“; die Regel steht vorn in der Liste des Tabs
      Regeln. Die Buchung selbst ändert sich nicht.
- [ ] Unter dem Formular steht „Gilt für neue Importe.“ (12 px, gedämpft): Regeln wirken nicht auf
      vorhandene Buchungen (README categorization).

### Angebot nach einer Kategorie-Änderung
- [ ] GIVEN eine Buchung mit Payee „Rewe“ WHEN Roman im Detail die Kategorie auf „Lebensmittel“ ändert und
      das Speichern gelingt THEN steht unter dem Feld Kategorie die Zeile „Regel für „Rewe“ anlegen?“ mit
      „Anlegen“ und einem Schließen-×. „Anlegen“ öffnet das vorbelegte Formular wie der feste Eintrag.
- [ ] GIVEN eine aktive Regel, die als erste auf den Payee passt, ordnet schon die neue Kategorie zu WHEN
      Roman die Kategorie ändert THEN erscheint kein Angebot. Das Urteil kommt vom Server (dieselbe
      Engine wie beim Import, `RulesEngine`), nicht aus einem Nachbau im Client.
- [ ] Kein Angebot bei Sammelaktionen (Kategorie für eine Auswahl), bei Aufteilung, Übertrag oder ohne Payee.
- [ ] × oder ein Wechsel zu einer anderen Buchung lässt das Angebot verschwinden; es kommt erst nach der
      nächsten Kategorie-Änderung wieder. „Rückgängig“ im Toast nimmt die Kategorie zurück und das Angebot weg.
- [ ] Der Toast nach der Änderung bleibt wie heute („Rückgängig“); das Angebot verdeckt ihn nicht und er
      verdeckt das Angebot nicht (§7.13).

### Tests und Gate
- [ ] Szenarien oben als Tests auf ihrer Heimat-Ebene (`update` für Angebot zeigen und verwerfen, Server
      für das Urteil „passt schon“).
- [ ] Ein Browser-Test `[flow:regel-aus-buchung]` (Handy 412 px mit Touch: Kassenbuch, Buchung öffnen,
      „Regel anlegen“, Vorbelegung sichtbar, anlegen, zurück im Detail); `FLOWS` in
      `scripts/browser-usage.mjs` ergänzt.
- [ ] Neue Pflichtfelder in Records aus `src/Shared/` sind in den Fixtures der `scripts/*.test.mjs` nachgezogen.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Romans Blick am Handy und am PC auf Eintrag, Angebot und Formular. [human-eye]

## Abweichungen
- Der Prototyp (Version 17) zeigt keinen Weg von der Buchung zur Regel. Diese Funktion gab es in BB im
  SyncFlow, sie gilt (CLAUDE.md „gilt BB“) und folgt im Aussehen dem Formular aus categorization-n3sxw und
  den Mustern `Detail` und §7.15.
- Roman wählte „Angebot im Toast“. Der Toast hat genau eine Aktion, nach einer Kategorie-Änderung ist das
  „Rückgängig“, und ein Toast ist Rückmeldung, kein Angebot (§7.13). Das Angebot steht deshalb als Zeile im
  Detail unter der Kategorie; es läuft nicht nach ein paar Sekunden ab.

## Notes
Verfeinert 2026-11-13 aus Schnitt 6 (categorization-n3sxw), mit Roman: zwei Tasks, diese hängt am Tab
Regeln und nutzt dessen Formular. Keine Anzeige, wie viele vorhandene Buchungen betroffen wären (Regeln
wirken nur auf neue Importe); doppelte Regeln verhindert das Urteil „passt schon“.
Bausteine: `Detail` (Kassenbuch-Detail, `src/Client/Components/Ledger/`), Durchgehen (`Page.Review`,
`ReviewCard`), `RulesApi.createRule`. Für das Urteil braucht es einen lesenden Aufruf (etwa „erste passende
aktive Regel für Payee und Memo“), über `Retry.reading`, Inventar in `src/Tests/RetryTests.fs`.
Stil: Optik aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`.
