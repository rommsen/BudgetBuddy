---
id: ledger-cnaz3
title: "Kassenbuch am PC ordnen: Kopf mit Konto und Saldo, Abrufen, n durchgehen und + Buchung; Filterleiste mit Suche; Tabelle ohne Status-Spalte und Prüf-Knopf; Panel immer da, ab zwei gewählten Buchungen als Zusammenfassung"
status: todo
type: feature
context: ledger
created: 2026-11-14
completed:
depends_on: [design-system-001-styleguide, ledger-d9z3n]
blocks: [ledger-zfj78]
tags: [etappe-3, frontend, kassenbuch]
related_adrs: [0086, 0087]
related_research: []
prior_art: [ledger-ct3cx, ledger-pnjz3, ledger-dn4vy, ledger-v6s7c]
---

## Why
Schnitt 8 aus Etappe 3 (Fahrplan), zweiter von drei Teilen aus ledger-f8d7x (Kassenbuch ordnen). Am PC fehlen im Kopf
Konto, Saldo, „Abrufen“ und „Durchgehen“, die Tabelle trägt eine Status-Spalte und einen Prüf-Knopf je Zeile, und bei
einer Mehrfachauswahl zeigt das Panel nichts zur Auswahl (§7.7).

## What
Tab „Buchungen“ ab 1024 px nach Prototyp Version 17 (Funktionen `ledgerDesk`, `selSummary`, `panelDesk`). Die Suche und
das neue „Filter löschen“ bringt ledger-d9z3n mit; hier geht es um Anordnung, Tabelle, Tastatur und Auswahl.

## Acceptance criteria

Kopf
- [ ] Titel „Kassenbuch“, darunter „<Konto oder Alle Konten> · <Saldo>“ (Saldo Mono) und dahinter der heutige Text
      „Gespiegelt …“ (BB, Abweichung (a)).
- [ ] Rechts, in dieser Reihenfolge: „Jetzt spiegeln“ (BB, wie heute), „Abrufen“ mit Symbol (öffnet denselben Abruf wie
      auf der Übersicht), „n durchgehen“ (nur bei n > 0, n = Anzahl „Zu prüfen“ im ganzen Kassenbuch, öffnet das
      Durchgehen) und „+ Buchung“ als einziger primärer Knopf (Roman, 2026-11-14).

Filterleiste
- [ ] Unter den Tabs eine umbrechende Leiste mit Chips: „Zu prüfen n“, „Lena n“, „Ohne Kategorie n“ als Umschalter (Zahl
      klein und gedämpft hinter dem Text, aktiv im Akzent), „Kategorie ▾“ bzw. „Kategorie: <Name>“ (Mehrfachauswahl im
      Popover wie heute), „Zeitraum“ bzw. Kurzname (Popover wie heute, ohne Vorgabe; Roman, 2026-11-14), „Betrag“ (BB),
      und „Filter löschen“, sobald ein Filter oder die Suche gesetzt ist.
- [ ] Das Suchfeld „Suchen…“ steht am Ende der Leiste, rechtsbündig (Prototyp).
- [ ] Sortiert wird über die Spaltenköpfe wie heute (BB). „Gelöschte zeigen“ und die Trefferzeile bleiben wie heute (BB).

Tabelle
- [ ] Spalten: Kästchen, Datum, Konto (nur in „Alle Konten“, BB), Payee, Kategorie, Memo, Betrag. Die Spalte Status
      entfällt (Roman, 2026-11-14).
- [ ] In der Payee-Zelle stehen hinter dem Namen die Lena-Marke, „doppelt?“ in Gelb, „nicht in YNAB“ in Rot und das
      Gelöscht-Zeichen (bisher Status-Spalte).
- [ ] „ohne Kategorie“ in der Kategorie-Spalte in Gelb. Eine ungeprüfte Zeile trägt die gelbe Kante, ohne Wort.
- [ ] Die Spalte Memo steht erst ab 1500 px Fensterbreite und nur, wenn sie neben dem Panel Platz hat (Roman,
      2026-11-14). Darunter entfällt sie ohne Ersatz.
- [ ] Kein Knopf „Geprüft“ oder „Ungeprüft“ an der Zeile, auch nicht bei Hover oder Fokus (Roman, 2026-11-14).
- [ ] Tagesköpfe mit Datum links und Tagessumme rechts wie heute.

Panel
- [ ] Ab 1024 px steht rechts immer das Panel (§7.6). Ohne gewählte Buchung zeigt es „Buchung wählen, um Details zu
      sehen.“ ohne Aktion (§7.12).
- [ ] Reicht die Breite neben dem Panel nicht für die Tabelle, steht die Kompaktliste wie heute (BB, gemessene Breite).

Tastatur und Auswahl
- [ ] Neu: L fragt Lena für die aktuelle Zeile bzw. die Auswahl (wie „Lena fragen“), „/“ setzt den Fokus in die Suche;
      die Pfeile wirken auch dort (§7.8). Bleibt: ↑↓ und J/K, G, Umschalt+G, Enter, Leertaste, Esc (BB).
- [ ] Strg- oder Cmd-Klick nimmt eine Zeile in die Auswahl oder heraus, Umschalt-Klick wählt den Bereich ab der zuletzt
      gewählten Zeile, Umschalt+↑↓ erweitert die Auswahl um die nächste Zeile (§7.7). Ein Klick ohne Taste öffnet das
      Detail wie heute.
- [ ] Die Tastenleiste unten zeigt die Tasten als Tastenkästchen (`ReviewCard.kbd` oder ein gleich aussehender
      DS-Baustein): „↑ ↓ bewegen“, „G geprüft“, „L Lena fragen“, „Leertaste oder ⇧+↓ auswählen“, „/ suchen, Pfeile
      wirken auch dort“, „Esc schließen“ (Prototyp) und dazu „J K“, „Enter Detail“, „⇧+G ungeprüft“ (BB).

Mehrfachauswahl
- [ ] Bei genau einer gewählten Buchung zeigt die Leiste unten „1 ausgewählt“, die Summe und die heutigen Aktionen
      („Geprüft“ primär, „Kategorie setzen“, „Lena fragen“, „Rückfrage zurücknehmen“ (BB), „Auswahl aufheben“); das
      Panel zeigt das Detail dieser Buchung.
- [ ] Ab zwei gewählten Buchungen wird das Panel zur Zusammenfassung (§7.7, Prototyp `selSummary`):
      - Kopf „n Buchungen ausgewählt“, darunter „Umschalt-Klick wählt einen Bereich, Strg- oder Cmd-Klick einzelne“,
        rechts ein Knopf „Auswahl aufheben“ (×).
      - „Summe“ und die Summe groß (Mono).
      - Eine Karte mit „Ausgaben“, „Einnahmen“ und „Davon zu prüfen“ (Anzahl).
      - Kopf „Nach Kategorie“ und je Kategorie ihre Summe, Splits nach ihren Zeilen, „ohne Kategorie“ in Gelb,
        sortiert von der größten Ausgabe zur größten Einnahme.
      - „Alle geprüft“ als primärer Knopf über die ganze Breite, darunter „Kategorie setzen“ und „Lena fragen“ neutral,
        dazu „Rückfrage zurücknehmen“, wenn eine gewählte Buchung gefragt ist (BB).
- [ ] Ab zwei gewählten Buchungen zeigt die Leiste unten nur „n ausgewählt“, die Summe und „Auswahl aufheben“; die
      Aktionen stehen im Panel.
- [ ] Eine Rückmeldung steht über der Leiste (§7.13).

Tests und Abgleich
- [ ] Reine Funktionen unter .NET: Zusammenfassung der Auswahl (Summe, Ausgaben, Einnahmen, zu prüfen, Aufteilung nach
      Kategorie mit Split-Zeilen und Reihenfolge), Kopfzeile mit Konto, Saldo und „n durchgehen“, Tastenbefehle L und
      „/“ in `keyCommandFor`, Spaltenstufen mit der Grenze 1500 px.
- [ ] Browser-Tests: `[flow:mehrfachauswahl]` deckt am PC Strg-Klick, Umschalt-Klick und die Zusammenfassung im Panel
      ab (der Handy-Teil bleibt im bestehenden Test); `[tastatur]` für „/“ in die Suche und Pfeile aus der Suche.
      `[flow:buchung-pruefen]` prüft am PC mit G statt mit dem Knopf an der Zeile. Tests, die die Status-Spalte oder den
      Prüf-Knopf an der Zeile anfassen, werden angepasst oder entfallen.
- [ ] Layout-Sweep: `kassenbuch` bei 1024, 1280, 1440 und 1920 px mit leerem Panel; neu `kassenbuch-auswahl-pc`
      (drei gewählte Buchungen, Zusammenfassung) bei 1440 px.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten: Screenshots bei 1440 px von Liste, Detail und
      Auswahl neben `f8d7x-proto/pc-liste.png`, `pc-detail.png`, `pc-auswahl.png`, im Outcome.

## Abweichungen vom Prototyp
- (a) „Gespiegelt …“ im Untertitel und „Jetzt spiegeln“ im Kopf bleiben (BB, Roman 2026-11-14).
- (b) Zeitraum als ein Chip mit Popover (Schnellwahl und freier Bereich) ohne Vorgabe statt vier Chips „Monat |
  Vormonat | Jahr | Alle“ mit Vorgabe „Monat“ (Roman, 2026-11-14).
- (c) „Betrag“ als Chip, sortierbare Spaltenköpfe, „Gelöschte zeigen“ und Trefferzeile bleiben (BB).
- (d) Die Spalte Konto steht nur in „Alle Konten“ (BB); im Prototyp steht sie immer, in einem Konto wiederholt sie
  denselben Namen.
- (e) In der Leiste bei einer Buchung bleibt „Rückfrage zurücknehmen“ (BB).

## Notes
Teil 2 von 3 aus ledger-f8d7x (Kassenbuch ordnen). Baut auf ledger-d9z3n (Suche, `ClearAllFilters`, Breite 1024 px) auf;
danach ledger-zfj78 (Detail ordnen).

Vorlage für Worker und Verifier:
- Prototyp Version 17: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/scratchpad/f8d7x-proto/prototyp-v17.html`
  (Funktionen `ledgerDesk`, `selSummary`, `selectWithMods`, `panelDesk`, `renderDesk`).
- Screenshots im selben Ordner: `pc-liste.png`, `pc-detail.png`, `pc-auswahl.png`.
- BB-Screenshots über `node <scratchpad>/shotjs/route.mjs '#/ledger' --out <dir>` (siehe Notes in ledger-d9z3n).

Fundstellen heute (Inventar 2026-11-14): Kopf `headerActionsFor` `View.fs` 4615 bis 4698, Spaltenstufen 2805 bis 2848
und `visibleColumns` 2859 bis 2925, Status-Spalte 270 bis 288 und 1010 bis 1040, Prüf-Knopf bei Hover 553 bis 557,
Tastenleiste 2564 bis 2582, `keyCommandFor` `Types.fs` 2601, Auswahl-Leiste `DesignSystem/MultiSelect.fs` 273 bis 315,
Abruf-Sheet `src/Client/State.fs` 80. Eine Zusammenfassung im Panel gibt es heute nicht.
