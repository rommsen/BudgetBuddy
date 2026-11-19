---
id: ledger-ef068
title: "Kassenbuch weicht von DS-Bausteinen ab: Knöpfe, Köpfe, Kontowahl, Kanten"
status: backlog
type: refactor
context: ledger
created: 2026-11-19
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G5, von Roman zur Task gewählt. Gleiche Dinge sehen im Kassenbuch anders aus als in Übersicht und DS; höchste Schwere mittel. Verweis: ledger-k4n2s Zeile 47 (FAB).

## What
Alle Befunde der Gruppe in Befundform, wörtlich aus dem Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`:

### G5 Kassenbuch weicht von DS-Bausteinen ab: Knöpfe, Köpfe, Kontowahl, Kanten

**Kern:** Über mehrere Tasks gewachsen sehen gleiche Dinge im Kassenbuch anders aus als in der Übersicht oder im DS: FAB, Symbolknöpfe, „Abbrechen“, Kontowahl, Panel-Kopf, gelbe Kante am Anker, leere Zustände, Begriffe.

**Höchste Schwere:** mittel · **Befunde:** 19 · **BCs:** ledger

**Verweis:** „+ Buchung“ als `Button.fab` steht schon in ledger-k4n2s Zeile 47.

- src/Client/Components/Ledger/View.fs:467 | styleguide §2 Ruhige Listen, Zeile „Zu prüfen“ (`ListRoles.statusEdgeWithAnchor`) | mittel | In `mouseRowContainerClassName` bei Anker und „zu prüfen“ `statusEdgeWithAnchor`, sonst `ListRoles.selectedEdge` statt rohem Schatten (wie `selectableRowClassName` 742); heute fehlt in der Kompaktliste mit Maus die gelbe Kante am Anker.
- src/Client/Components/Ledger/View.fs:4522 | styleguide §5 Button `fab`, §2 | mittel | `Button.fab "ledger-capture-fab" …` wie die Übersicht (Overview/View.fs:748) statt `Button.view` Primary mit eigener Klasse.
- src/Client/Components/Ledger/View.fs:4861 | styleguide §5 PageHeader `iconButton` | mittel | „Abrufen“ und „Jetzt spiegeln“ (4861, 4871) über `PageHeader.iconButton` wie in der Übersicht; heute sieht „Abrufen“ in Übersicht und Kassenbuch verschieden aus.
- src/Client/Components/Ledger/View.fs:4363 | styleguide §2 Praktische Konsequenzen („Abbrechen“ ohne Füllung, `Button.Outline`), §5 Form `submitButtonBlock` | mittel | „Abbrechen“ in „Neue Buchung“ (Panel, Sheet, Dialog) auf `Button.Outline` wie im Filter-Sheet (3430).
- src/Client/Components/Ledger/View.fs:2093 | styleguide §2, §5 Button `Outline` | mittel | „Abbrechen“ im Fuß von „Betrag“ auf `Button.Outline`.
- src/Client/DesignSystem/SplitEditor.fs:257 | styleguide §2, §5 Button `Outline` | mittel | `cancelButton` auf `Button.Outline`, Knopfpaar im Fuß (283) über die volle Breite wie die anderen Formular-Füße; heute rechtsbündig mit `IsLoading` statt `IsBusy`.
- src/Client/Components/Ledger/View.fs:4122 | styleguide §5 Money `accountChoiceRow` | mittel | Kontowahl in „Neue Buchung“ über `Money.accountChoiceRow` wie das Sheet „Konto“ (3373); heute zwei Markierungen des gewählten Kontos (Haken mit Akzentrahmen vs. Akzentlinie links).
- src/Client/Components/Ledger/View.fs:4374 | styleguide §5 Detail `titleHead`, Drift §5 | mittel | Kopf des Panels „Neue Buchung“ über `Detail.titleHead` (oder Variante ohne Unterzeile) statt h2 plus `Button.view` Secondary; heute je nach Inhalt anderer Schließen-Knopf.
- src/Client/Components/Ledger/View.fs:5257 | styleguide §5 Icons (`flagSolid` nur als Marke einer Rückfrage) | mittel | Am Knopf „Lena fragen“ der Karte `Icons.flag Icons.SM Icons.LenaFlag` wie im Detail (1617) statt des gefüllten Flags.
- src/Client/Components/Ledger/View.fs:2911 | styleguide §5 Card `allDone`, §7.12 | mittel | „Alles geprüft“ und „Alles kategorisiert“ (2920) über `Card.allDone` wie „Zu tun“ auf der Übersicht statt `Card.emptyState` mit Häkchen.
- src/Client/Components/Ledger/View.fs:741 | .claude/rules/design-tokens.md, styleguide §3 | niedrig | Literal `bg-accent/[0.07]` hier und in 450, 464, 666 durch `Tokens.ListRoles.selectedFill` ersetzen.
- src/Client/Components/Ledger/View.fs:5252 | styleguide §8 (konsistente Begriffe) | niedrig | „Rückfrage zurück“ wie Detail und Auswahl „Rückfrage zurücknehmen“ nennen (oder `lena.Label` aus `lenaActionFor`).
- src/Client/Components/Ledger/View.fs:3470 | styleguide §8 | niedrig | Einen Namen für den Lena-Filter: heute „Lena gefragt“ (Filter-Sheet), „Lena“ (Chip 3219, Types.fs:304), „Rückfrage Lena“ (`askLenaChipLabel` 161).
- src/Client/Components/Ledger/View.fs:3058 | styleguide §5 BottomSheet `sectionTitle`, Drift §5 | niedrig | Abschnittskopf „Schnellwahl“ über `BottomSheet.sectionTitle` wie Sortieren (3267).
- src/Client/Components/Ledger/View.fs:3366 | design-tokens.md, styleguide §3 | niedrig | `border-border-subtle bg-surface-card` hier und `border-border-subtle` in 3485 über `Tokens.Borders.subtle` und `Tokens.Backgrounds.card`.
- src/Client/Components/Ledger/View.fs:4264 | styleguide §7.2 (Payee darf weggelassen werden) | niedrig | Platzhalter des Payee-Felds in „Neue Buchung“ sagt, dass es optional ist (Memo tut es schon).
- src/Client/Components/Ledger/View.fs:2940 | styleguide §7.12 (Beispiel „Keine Buchungen“) | niedrig | Satz und Aktion des leeren Zustands an §7.12 angleichen (Import direkt, ADR 0093); heute „Für diesen Filter steht noch nichts im Kassenbuch.“ und „Jetzt spiegeln“.
- src/Client/DesignSystem/SplitEditor.fs:228 | styleguide §2 (Akzent nur für Hauptaktion, Auswahl, Link, Fokus) | niedrig | Plus an „Zeile hinzufügen“ im neutralen Knopf in `Icons.Default`.
- src/Client/DesignSystem/Loading.fs:306 | README („Buchung“) | niedrig | `aria-label` „Lade Transaktionen“ → „Buchungen werden geladen“.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-19, raw, unrefined. Bewusst groß; kleiner geschnitten wird im Refinement (`agentheim:modeling`).
