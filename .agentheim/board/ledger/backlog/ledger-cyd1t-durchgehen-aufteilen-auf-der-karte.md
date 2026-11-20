---
id: ledger-cyd1t
title: "Durchgehen: Textlink „Aufteilen“ unter dem Feld Kategorie öffnet das Detail mit offenem Split-Editor, ohne Umweg über „Details“"
status: backlog
type: feature
context: ledger
created: 2026-11-20
completed:
depends_on: [design-system-001]
blocks: []
tags: [durchgehen, pruefen, split, frontend]
related_adrs: [0092]
related_research: []
prior_art: [ledger-v6s7c, ledger-hme9k, ledger-pafkp, ledger-s8t3s, ledger-mf2bg, ledger-pz5wv]
---

## Why
Roman (2026-11-20): „Man kann im Prüfen-Feature keine Split-Buchung anlegen.“ Heute geht Aufteilen im Durchgehen nur
über „Details“ und dort den Knopf „Aufteilen“ (ledger-v6s7c, Abweichung (a)). Roman findet den Weg hinein zu weit und
will den Einstieg direkt auf der Karte. Das nimmt Abweichung (a) zurück.

## What
Vorlage ist die Skizze zu dieser Task (https://claude.ai/artifact/Ko5u9VtVYxY9hFsqCV83LD, Version 4, von Roman
freizugeben).

Entschieden (Roman, 2026-11-20): kein eigener Ort für den Editor über der Karte, sondern der vorhandene Weg über das
Detail wird wiederverwendet. „Aufteilen“ tut dasselbe wie „Details“ und öffnet direkt danach den Split-Editor.

- **Textlink „Aufteilen“** unter dem Feld „Kategorie“ der Normalkarte, linksbündig, im Stil von „Regel anlegen“
  (Akzent, 12 px, ohne Fläche; gleicher DS-Baustein wie die Linkzeile der Karte). Das Feld behält die volle Breite.
  Die Trefferfläche ist am Handy mindestens 48 px hoch. Nicht auf der Duplikat-Karte. Der Link steht nicht in der
  Linkzeile unten („Regel anlegen“, „Details“), dort gibt es laut Styleguide keinen dritten Link.
- **Wirkung:** `ReviewShowDetails` und danach `OpenSplitEditor` für die Karte, etwa als eigene Nachricht
  `ReviewOpenSplit`, die beides in einem Schritt setzt (kein Zwischenzustand mit Detail ohne Editor). Am Handy ist das
  das Detail-Sheet mit dem Editor als Inhalt (ledger-hme9k), am PC das Detail-Panel rechts mit dem Editor.
- **Rückweg wie heute:** Speichern, ×, „Abbrechen“ und Esc schließen den Editor und zeigen das Detail; die Bestätigung
  steht am Feld im Detail wie heute (`NoteAtField`). Schließen des Details führt zur Karte.
- **Sperre:** dieselben Gründe wie im Detail (`splitLockTextFor`: wartend, in YNAB gelöscht, `SplitEditor.lockReasonFor`
  mit Tracking-Konto, Abgleich, Übertrag, Ersatz offen, fremde `import_id`). Gesperrt ist der Link gedämpft
  (`text-muted`), ohne Wirkung, der Grund steht darunter (12 px gedämpft) und als `title`.
- **Feld „Aufgeteilt“:** bei einer aufgeteilten Buchung mit Pfeil; ein Tipp tut dasselbe wie der Link. Der Link heißt
  weiter „Aufteilen“.
- **Taste A** am PC (nur Normalkarte, nicht aus einem Feld, nichts anderes offen) tut dasselbe wie der Link.
  Tastenleiste: „A aufteilen“ zwischen „K Kategorie“ und „L Lena“.
- **Ein Sheet zur Zeit:** ist Kategorieauswahl, Payee-Auswahl oder Regel-Formular offen, tun Link, Feld und A nichts.

## Acceptance criteria
- [ ] Reine Funktion für den Link (etwa `reviewSplitLink`): versteckt auf der Duplikat-Karte, frei ohne Sperre,
      gesperrt mit genau dem Text aus `splitLockTextFor`; ein Fall je Sperrgrund (wartend, gelöscht, Tracking,
      Abgleich, Übertrag, Ersatz offen, fremde `import_id` bei aufgeteilter Buchung) und je Kartenart.
- [ ] Client-`update` unter .NET: `ReviewOpenSplit` auf einer freien Normalkarte ergibt `DetailShown = true` und
      `SplitEditor = Some` für die Karte in einem Schritt; auf gesperrter Karte, Duplikat-Karte, bei offener
      Kategorieauswahl, Payee-Auswahl, Regel-Formular oder laufendem Aufruf der Gruppe Kategorie bleibt das Model
      gleich.
- [ ] `reviewKeyAction`: A/a auf der Normalkarte ergibt `ReviewOpenSplit`, auf der Duplikat-Karte und in der Endansicht
      nichts; ein Fall je Taste und Zustand.
- [ ] Esc im Durchgehen mit offenem Editor im Detail schließt nur den Editor, das nächste Esc das Detail, erst das
      dritte verlässt das Durchgehen. Fehlt der Editor heute in `KeyLeave` und verlässt Esc das Durchgehen, ist das ein
      Bug: Regressionstest vor dem Fix rot (Doktrin Punkt 8). Je Esc genau eine Stufe, auch wenn `EscapePressed` und
      `ReviewKey` beide am Durchgehen hängen.
- [ ] Zustandsfolge durch das echte `update` (Doktrin Punkt 20) als Aufzählung der Reihenfolgen von `ReviewOpenSplit`,
      `StartSaveSplits`, eingespeisten Antworten (`SplitsSaved` Ok und Error), `CloseSplitEditor`, Schließen des
      Details, Passt, Später und Wisch. Orakel: höchstens ein Sheet-Inhalt offen; der Index der Schlange ändert sich
      nicht, solange Editor oder Detail offen oder die Zeile belegt ist; nach Erfolg ist der Editor zu und das Detail
      der Karte offen.
- [ ] `src/Tests/LedgerRowLockWaysTests.fs`: „Aufteilen von der Karte“ als weiterer Weg der Gruppe Kategorie.
- [ ] Browser: `[flow:durchgehen]` bleibt einer; ein `[tastatur]`-Test am PC (A öffnet, Esc schließt erst Editor, dann
      Detail, dann Ansicht). Layout-Sweep: Karte mit Link (frei, gesperrt mit Grund, aufgeteilt) bei 320, 412 und
      1280 px, Trefferfläche des Links 48 px am Handy, „Passt, weiter“ ohne Scrollen erreichbar.
- [ ] Werte aus dem Styleguide, nichts erfunden; ist der Textlink am Feld eine neue Variante, steht sie an `ReviewCard`
      auf `/styleguide` und im Eintrag `ReviewCard` in `standards/frontend/styleguide.md`.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten; Screenshots von BB bei 412 und 1280 px (Karte
      mit Link, gesperrter Link, Detail mit Editor nach dem Tipp) neben den Skizzen-Screenshots im Outcome.
- [ ] README ledger: **Durchgehen** (Link „Aufteilen“, Feld „Aufgeteilt“ mit Pfeil, Taste A, Abweichung (a)
      zurückgenommen) und **Zeilen-Sperre** nachgezogen; Delta durch `npm run check:readme-delta` samt Prüfung 7.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.
- [ ] Roman hat Aufteilen auf der Karte am Handy (412 px, Touch) und am PC (Taste A) neben der Skizze angesehen.
      [human-eye]

## Notes
- Entschieden (Roman, 2026-11-20): Einstieg auf der Karte unter dem Feld „Kategorie“, als Textlink wie „Regel
  anlegen“ (Skizze Version 3 und 4), Mechanismus wiederverwendet statt eigenem Ort über der Karte. Verworfen: Knopf
  neben oder unter dem Feld, dritter Knopf unter der Karte, Eintrag in der Kategorieauswahl, Editor über der Karte mit
  Rückweg zur Karte und Bestätigung am Kartenfeld.
- Folge der Wiederverwendung: nach dem Speichern steht Roman im Detail, zurück zur Karte ist ein Tipp mehr. Stört das
  im Alltag, ist ein kleiner Nachzug möglich: nach Speichern auch das Detail schließen, wenn der Weg über „Aufteilen“
  kam. Nicht Teil dieser Task.
- `OpenSplitEditor` verlangt schon heute das offene Detail der Buchung (`State.fs` ~3989), `ReviewShowDetails` setzt es
  (~5389). Die neue Nachricht verbindet beides; die Sperrprüfung bleibt in `OpenSplitEditor`.
- Vor dem Bauen klären: ob `EscapePressed` im Durchgehen neben `ReviewKey` feuert (`ReviewKeyboard`, View.fs ~5348),
  und ob der Editor in `KeyLeave` (State.fs ~5421) fehlt.
- Keine neuen Pflichtfelder in `src/Shared`, keine Folgen für die Fable.Remoting-Fixtures. Kein ADR nötig.
- Code: `ReviewNormalCard` (View.fs ~5391), `splitLockTextFor` (~1666), `OpenSplitEditor` (State.fs ~3985),
  `ReviewShowDetails` (~5389), `KeyLeave` (~5421), `reviewKeyAction` (Types.fs ~1732), `ReviewCard.fieldIn`
  (DesignSystem/ReviewCard.fs ~127).
