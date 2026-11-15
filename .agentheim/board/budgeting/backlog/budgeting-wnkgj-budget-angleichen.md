---
id: budgeting-wnkgj
title: "Budget an die Bedeutungen angleichen: Targets prüfen mit weniger Orange, Wochentag als Auswahlfeld, Historie mit „Zeigen“, Leerflächen am PC"
status: backlog
type: feature
context: budgeting
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, budget]
related_adrs: [0087]
related_research: []
prior_art: []
---

## Why
Das Budget weicht an einigen Stellen von der Bedeutungsschicht ab (ADR 0087).

## What
Targets prüfen nutzt Orange nur nach §2. Wochentag eines Targets als Auswahlfeld statt sieben Knöpfe (§7.5). Budget-Historie mit Auswahlfeld „Zeigen“ statt `ChipBar` (§7.11). Große Leerflächen am PC nach §7.15.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Vorab entschieden (Roman, 2026-11-15, für die Verfeinerung):
- „noch nicht in YNAB“ an einer Kategorie mit ausstehendem Push in Gelb wie im Kassenbuch (heute `Badge.neutralSmall`,
  `MonthView.fs` mit Text aus `Month.fs`), Styleguide §2.
- Die Budget-Tabelle steht neben dem Panel erst ab rund 1340 px (G11 aus design-system-gcpws); hier nochmal ansehen.
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Prototyp Version 15 bis 17: Budget-Tabs mit fester Höhe, Monatsbalken mit Beträgen.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.

## Bestandsaufnahme (Conductor, 2026-11-15)
Abgleich jeder Budget-Ansicht des Prototyps Version 17 mit BB, bei 412 px (Touch) und 1440 px, dazu 1100, 1280, 1340
und 1920 px. BB lief mit den Mocks aus `scripts/layout-states.mjs`, nachgebaut auf die Daten des Prototyps. Screenshots
und Skripte lagen im Scratchpad der Session (flüchtig). Neu aufnehmen mit `scripts/layout-states.mjs` und dem Prototyp
per `Artifact` read.

Die vier Punkte aus „What“, belegt:
1. Gelb in Targets prüfen: große Zähler „Hinweise“ (gelb) und „Passt“ (grün) am PC (`TargetCheckView.fs:385`), Satz am
   Handy (`:444`), Gruppentitel (`:164`), Legende (`:289`), Balken (`Stats.fs:458`). „Hinweise“ zählt auch Gruppen mit, die
   nicht gelb sind. Orange gibt es seit Palette 2 nicht mehr, gemeint ist Gelb.
2. Wochentag: `TargetFormView.fs:159-177` (`weekdayChoice`, `Segmented` mit sieben Knöpfen), ohne Wahl roter Fehler.
   Der Prototyp hat keinen Wochentag. Vorlage für das Feld: `Input.selectField`.
3. Historie-Filter: `HistoryView.fs:171-187` (`ChipBar`), am Handy angeschnitten. Prototyp: Feld „Zeigen: Alle“ mit
   Sheet „Historie zeigen“. Die Zeilenform weicht von §7.11 ab.
4. Leerflächen am PC: leeres Budget-Panel nur mit „Kategorie wählen“, Lücke im Tab „Geld“ (Schritte fehlen am PC),
   Historie und Targets prüfen über die volle Breite (bei 1920 px Text und Betrag rund 1600 px auseinander), breite
   Namensspalte der Budget-Tabelle bei 1920 px.

Weitere Abweichungen vom Prototyp (gelten als Kriterium, soweit keine Frage offen ist):
- Budget: Gruppenkopf mit Summe Verfügbar (Handy), Gruppenzeile mit Summen Zugewiesen/Aktivität/Verfügbar (PC);
  „kein Target“ als Text; Lupe in der Suche; Monat im Kopf groß; Verfügbar in der Tabelle halbfett; Filter-Chips mit
  Zahl und Suche rechts (PC).
- Panel am PC: Kopf mit ‹ › und ×, langer Leertext „Kategorie wählen, um Geld, Buchungen, Historie und Target zu sehen.“
- Kategorie-Detail: im Tab „Geld“ Aktivität und Verfügbar als zwei Kacheln über dem Modus; Feld „Zugewiesen“
  rechtsbündig; „Verfügbar danach“ und „Zu verteilen danach“; im Tab „Historie“ 12 statt 6 Monatsbalken.
- Targets anwenden: Unterzeile „Target · zugewiesen“ je Zeile, Unterzeile mit dem Monat und × im Kopf.
- Kategorien: „Umbenennen“ im Akzent.
- Fehler unabhängig vom Prototyp: im ⋯-Menü am PC ist „Targets nach YNAB schreiben“ abgeschnitten (`View.fs:125`).

Nur in BB (bleibt, bis Roman streicht): Leiste „Monatserster“ mit vier Kacheln, Uhr-Icon Historie im Kopf, „Targets nach
YNAB schreiben“, „noch nicht in YNAB“, „deaktiviert“, Fälligkeit und Wochentag im Target, „Target pausieren“ im Tab
„Geld“, offene Verschiebungen mit „Erneut senden“, zuletzt verwendete Gegenseite, beide Seiten beim Verschieben, in
Targets prüfen aufklappbare Gruppen, „n von 12 Mon. darüber“ und das Panel „Anpassen …“, „monatlich anlegen“, in der
Historie aufklappbare Anwendung, Status, Monat je Zeile und zwei YNAB-Arten, „Überzogene decken“.

Touch bei 412 px: Betragsfelder `type=text inputmode=decimal` ohne Vorzeichen. Vom Betragsfeld auf „von“/„nach“
schließt die Tastatur, weil die Auswahl den Sheet-Inhalt ersetzt. Das ist im Prototyp genauso.

Offene Fragen an Roman stehen in der Prüfliste, Gruppe „Fragen: Budget angleichen (Schnitt 10)“, Punkte w01 bis w11:
Teilung in vier Tasks (A Targets prüfen, B Target-Formular, C Budget-Historie, D Budget-Seite), Gelb in Targets
prüfen, Wochentag als Feld und ohne Wahl, Arten und Zeilenform der Historie, Leerflächen am PC, „Targets anwenden“ als
Hauptaktion, „Zu verteilen“ grün oder neutral, Filter-Wort, „Fehlbetrag einsetzen“. Kriterien erst nach den Antworten.
