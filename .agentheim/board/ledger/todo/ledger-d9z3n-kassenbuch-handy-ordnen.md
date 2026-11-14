---
id: ledger-d9z3n
title: "Kassenbuch am Handy ordnen: eine Kontowahl als Feld mit Saldo, freie Suche statt Payee- und Memo-Filter, Filter-Knopf mit Sheet und gesetzte Filter als Chips; gilt unter 1024 px"
status: todo
type: feature
context: ledger
created: 2026-11-14
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-cnaz3]
tags: [etappe-3, frontend, kassenbuch]
related_adrs: [0086, 0087]
related_research: []
prior_art: [ledger-ct3cx, ledger-pnjz3, ledger-dn4vy]
---

## Why
Schnitt 8 aus Etappe 3 (Fahrplan), erster von drei Teilen aus ledger-f8d7x (Kassenbuch ordnen). Das Kassenbuch hat am
Handy zwei Kontowahlen (Titel-Knopf und wischende Kartenleiste) und eine wischende Filterleiste mit acht Chips. Der
Styleguide (§7.5) verlangt eine Kontowahl als Auswahlfeld mit Saldo, darunter Suche und Filter-Knopf mit Sheet, gesetzte
Filter als Chips, keine seitlich wischende Leiste.

## What
Kopf, Kontowahl, Suche, Filter und Listenzeile des Tabs „Buchungen“ unter 1024 px nach Prototyp Version 17 (Funktion
`ledger()` und `sheetHtml` mit den Sheets `acct` und `filter`). Dazu im Modell die freie Suche, die die Filter Payee und
Memo ersetzt, und „Filter löschen“ für alle Filter. Beides gilt auch am PC; die PC-Anordnung baut ledger-cnaz3.

## Acceptance criteria

Breite
- [ ] Die Handy-Form gilt für den Tab „Buchungen“ unter 1024 px, auch zwischen 768 und 1023 px (Roman, 2026-11-14). Die
      Konten-Spalte neben der Liste (768 bis 1023 px, `accountStrip`/`accountListRowView`) entfällt.

Kopf
- [ ] Titel „Kassenbuch“ mit dem Untertitel wie heute („Gespiegelt …“, BB).
- [ ] Rechts im Kopf zwei Symbolknöpfe: „Abrufen“ (Symbol Aktualisieren wie im Prototyp, öffnet denselben Abruf wie auf
      der Übersicht) und „Jetzt spiegeln“ wie heute (BB). Beide mit `aria-label`.
- [ ] Unter 1024 px steht „+ Buchung“ als schwebender Knopf (FAB) wie heute, nicht im Kopf; er fehlt im Auswahl-Modus.

Kontowahl
- [ ] Unter den Tabs ein Auswahlfeld: Beschriftung „Konto“, Wert Kontoname oder „Alle Konten“, daneben der Saldo (Mono,
      klein, gedämpft), rechts ein Pfeil nach unten (Prototyp `.field`).
- [ ] Tippen öffnet ein BottomSheet „Konto“: „Alle Konten“ mit Gesamtsaldo, danach jedes Konto mit Saldo, ein negativer
      Saldo in `text-danger`, das gewählte mit Haken und Akzentrahmen. Tippen wählt und schließt.
- [ ] Der Titel-Knopf „<Konto> ▾“ und die wischende Kartenleiste mit Salden entfallen. Es gibt unter 1024 px genau eine
      Kontowahl.
- [ ] Die Wahl bleibt wie heute gespeichert (`ledger.selectedAccountFilter`).

Suche
- [ ] Unter der Kontowahl: Suchfeld „Suchen…“ (Lupe, `type=search`, `aria-label` „Buchungen durchsuchen“)
      und rechts der Knopf „Filter“ mit Filter-Symbol.
- [ ] Die Suche findet Teiltext ohne Groß- und Kleinschreibung in Payee, Memo und Kategoriename der Buchung, auf dem
      Server, mit Verzögerung beim Tippen wie heute der Memo-Filter. Gelöschte Zeilen bleiben ausgeblendet, solange
      „Gelöschte zeigen“ aus ist.
- [ ] Die Filter Payee und Memo entfallen in Modell, Vertrag und Oberfläche (Roman, 2026-11-14). Der Knopf im Detail,
      der heute den Payee-Filter setzt (`FilterByRowPayee`), setzt stattdessen die Suche auf den Payee; Beschriftung und
      Ort ändert erst ledger-zfj78.
- [ ] Am PC steht das Suchfeld bis ledger-cnaz3 am Ende der Filterleiste, damit die Suche dort nicht fehlt.

Filter-Sheet
- [ ] „Filter“ öffnet ein BottomSheet „Filter“ mit, in dieser Reihenfolge:
      - Schalter „Zu prüfen“, „Lena gefragt“, „Ohne Kategorie“, je mit der Anzahl gedämpft hinter dem Text (Prototyp).
      - Zeile „Kategorie“ mit Wert „alle“ oder den gewählten Kategorien und Pfeil; öffnet die heutige
        Kategorie-Auswahl mit Mehrfachauswahl (BB).
      - Zeile „Zeitraum“ mit Wert „alle“ oder dem gesetzten Zeitraum; öffnet die heutige Zeitraum-Auswahl mit
        Schnellwahl und Von/Bis (BB; Abweichung (a)).
      - „Betrag von“ und „bis“ als zwei Felder nebeneinander, `inputmode="decimal"`, ohne Vorzeichen.
      - Sortieren wie heute (BB), als Zeile „Sortieren“ mit Wert, öffnet die heutige Sortier-Auswahl.
      - Schalter „Gelöschte zeigen“ mit Anzahl, nur wenn es gelöschte Buchungen gibt (BB).
- [ ] Fuß des Sheets: „Zurücksetzen“ (neutral) und „n zeigen“ (primär, n = Treffer der Liste, während des Ladens nur
      „Zeigen“). Jede Änderung im Sheet wirkt sofort auf die Liste; „n zeigen“ schließt das Sheet.
- [ ] Der Filter-Knopf zeigt bei gesetzten Filtern „Filter (n)“ mit Akzentrahmen und Akzentschrift.

Gesetzte Filter
- [ ] Unter Suche und Filter-Knopf steht je gesetztem Filter ein Chip mit „×“: „Zu prüfen“, „Lena“, „Ohne Kategorie“,
      „Kategorie: <Name>“ (bei mehreren „Kategorie: n“), Zeitraum mit Kurzname, Betrag mit Bereich, eine andere
      Sortierung als „Datum, neueste zuerst“ und „mit Gelöschten“. „×“ nimmt genau diesen Filter zurück.
- [ ] Die Chips brechen um, die Zeile wischt nicht seitlich (§7.5).
- [ ] Ohne gesetzten Filter gibt es die Zeile nicht. Die wischende Filterleiste unter 1024 px entfällt.

Filter löschen
- [ ] „Zurücksetzen“ im Sheet und „Filter löschen“ im leeren Zustand löschen alle Filter einschließlich „Zu prüfen“,
      „Ohne Kategorie“, „Lena“ und der Suche; das Konto bleibt (Roman, 2026-11-14). Gilt für `ClearAllFilters` auf
      allen Breiten.

Liste
- [ ] Trefferzeile „n Buchungen“ mit Summe über der Liste wie heute, sobald ein Filter oder eine Suche gesetzt ist (BB).
- [ ] Zeile: oben Payee und Betrag; darunter Konto als gedämpfter Text (nur in „Alle Konten“), Kategorie oder „ohne
      Kategorie“ in Gelb, Lena-Marke, Memo gedämpft, „nicht in YNAB“ in Rot, „doppelt?“ in Gelb, „in YNAB gelöscht“
      (BB), Datum bei anderer Sortierung als Datum (BB).
- [ ] Eine ungeprüfte Zeile hat die gelbe Kante und unter dem Betrag das Wort „zu prüfen“ in Gelb (Prototyp).
- [ ] Kein Prüf-Knopf an der Zeile, auch nicht bei Maus zwischen 768 und 1023 px (Roman, 2026-11-14). Geprüft wird per
      Wisch nach rechts (ledger-th36h, unverändert), im Detail oder mit G.
- [ ] Tagesköpfe mit Datum und Tagessumme wie heute (ledger-pnjz3).
- [ ] Leere Zustände nach §7.12: „Kein Treffer“ mit „Keine Buchung passt zu Suche und Filtern.“ und „Filter löschen“;
      die übrigen wie heute.

Tests und Abgleich
- [ ] Reine Funktionen unter .NET für: Chips aus dem Filterzustand (Text, Reihenfolge, „×“ nimmt den richtigen zurück),
      `ClearAllFilters` löscht alles außer dem Konto, Anzahl am Filter-Knopf, Suche setzt aus dem Detail den Payee.
- [ ] Handler-Test der Suche mit In-Memory-SQLite: Treffer in Payee, Memo und Kategoriename, Groß- und Kleinschreibung,
      kein Treffer bei gelöschter Zeile ohne „Gelöschte zeigen“.
- [ ] Browser-Tests: `[flow:konto-wechseln]` und `[flow:kassenbuch-filtern]` werden für die neue Form neu geschrieben
      (Handy: Feld, Sheet, Chip, „×“), je einer. `[flow:buchung-pruefen]` prüft am Handy per Wisch statt Knopf. Der
      `[fokus]`-Test zum Prüfknopf an der Zeile (`ledger-review-mark.test.mjs`) und der `[tastatur]`-Test zum
      Payee-Filter (`ledger-filter-bar.test.mjs`) entfallen. Fixtures in `scripts/*.test.mjs` folgen dem neuen Vertrag.
- [ ] Layout-Sweep: Zustände `kassenbuch-filter-payee` entfernt; neu `kassenbuch-filter-sheet` (offenes Sheet) und
      `kassenbuch-filter-gesetzt` (drei Chips) bei 320, 390, 412 und 768 px; `kassenbuch` bei 900 px ohne Konten-Spalte.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten: Screenshots bei 412 px von Liste, Konto-Sheet,
      Filter-Sheet und gesetzten Filtern neben `f8d7x-proto/handy-*.png`, im Outcome.

## Abweichungen vom Prototyp
- (a) Zeitraum als Zeile mit der heutigen Auswahl (Schnellwahl und freier Bereich), ohne Vorgabe, statt Umschalter
  „Monat | Vormonat | Jahr | Alle“ mit Vorgabe „Monat“. Grund: BB kann den freien Bereich, Roman hat das für den PC so
  entschieden (2026-11-14), am Handy gilt dasselbe.
- (b) Sortieren, „Gelöschte zeigen“ und die Trefferzeile kommen dazu (BB kann mehr, Roman 2026-11-14).
- (c) „Jetzt spiegeln“ und der Untertitel „Gespiegelt …“ bleiben (BB).

## Notes
Teil 1 von 3 aus ledger-f8d7x (Kassenbuch ordnen); danach ledger-cnaz3 (PC) und ledger-zfj78 (Detail), nacheinander, weil
alle drei `src/Client/Components/Ledger/View.fs` ändern. ledger-ayjzq (Sicht „Für mich“) bleibt eigene Task.

Vorlage für Worker und Verifier (claude.ai-Artifacts können sie nicht öffnen):
- Prototyp Version 17: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/f8d7x-proto/prototyp-v17.html`
  (Funktionen `txRow`, `ledger`, `sheetHtml` Zweige `acct` und `filter`).
- Screenshots: derselbe Ordner, `handy-liste.png`, `handy-konto.png`, `handy-filter.png`, `handy-filter-gesetzt.png`.
- Screenshot einer BB-Route: `npm run screenshot:route` wartet auf `networkidle` und bricht beim Kassenbuch ab
  (infrastructure-hgbey). Funktioniert: `node <scratchpad>/shotjs/route.mjs '#/ledger' --out <dir>` aus dem Repo.
  Die Dev-DB ohne YNAB-Token stapelt Fehler-Toasts (Kann-Zeile ledger-k4n2s 122); für den Vergleich die API mocken oder
  die Toasts wegklicken.

Fundstellen heute (Inventar 2026-11-14): Kontowahl `View.fs` 1101 bis 1189 und 4535 bis 4579, Filterleiste
`filterChipBar` ab 3588, Sortieren ab 3547, Trefferzeile `summaryRow` ab 3614, Prüf-Knopf an der Zeile 572 bis 600 und
821 bis 832, Filterfelder `Types.fs` 169 bis 216 und 322 bis 369, `ClearAllFilters` `State.fs` 2158, Payee- und
Memo-Vergleich auf dem Server `src/Server/Ledger/Persistence.fs` 1431 bis 1448. Der Wechsel bei 768 px kommt aus
`Primitives.Responsive` und `Viewport.isDesktopViewport`; für diese Seite gilt 1024 px.

Der Vertrag in `src/Shared` ändert sich (Suchfeld statt Payee und Memo): Laufzeitfalle Fable.Remoting, Fixtures in
`scripts/*.test.mjs` im selben Schritt mitziehen.
