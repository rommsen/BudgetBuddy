---
id: ledger-cyd1t
title: "Durchgehen: Aufteilen direkt auf der Karte, ohne Umweg über „Details“"
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
über „Details“ und dort den Knopf „Aufteilen“ (ledger-v6s7c, Abweichung (a)). Roman findet den Weg zu weit und hat
„Knopf direkt auf der Karte“ gewählt. Das nimmt Abweichung (a) zurück. Eine Sammelbuchung (Supermarkt mit Drogerie,
Abhebung an der Kasse) lässt sich im Durchgehen sonst nur mit dem Umweg über das Detail sortieren.

## What
Vorlage ist die Skizze zu dieser Task (von Roman freizugeben; Stand der Verfeinerung: Option A, siehe Notes).

- **Knopf „Aufteilen“** rechts neben dem Feld „Kategorie“ der Normalkarte, in einer Zeile, 8 px Abstand, neutral
  (`Button.Secondary`), so hoch wie das Feld (52 px), ohne Symbol. Das Feld nimmt den Rest der Breite, der Wert wird
  gekürzt. Neue DS-Variante „Feld mit Knopf daneben“ an `ReviewCard` auf `/styleguide`. Nicht auf der Duplikat-Karte.
- **Sperre:** dieselben Gründe wie im Detail (`splitLockTextFor`: wartend, in YNAB gelöscht,
  `SplitEditor.lockReasonFor` mit Tracking-Konto, Abgleich, Übertrag, Ersatz offen, fremde `import_id`). Gesperrt ist
  der Knopf deaktiviert, der Grund steht darunter (12 px gedämpft) und als `title`.
- **Feld „Aufgeteilt“:** bei einer aufgeteilten Buchung mit Pfeil, ein Tipp öffnet denselben Editor mit denselben
  Sperren. Der Knopf heißt weiter „Aufteilen“.
- **Taste A** am PC (nur Normalkarte, nicht aus einem Feld, nichts anderes offen) öffnet den Editor. Tastenleiste:
  „A aufteilen“ zwischen „K Kategorie“ und „L Lena“.
- **Editor über der Karte:** Handy als Sheet „Aufteilen“ (heutiger Inhalt aus dem Detail-Sheet samt Barabhebung,
  ledger-s8t3s), PC als Panel rechts (420 px) wie bei „Details“, kein Modal. Ein Sheet zur Zeit (ledger-hme9k): ist
  Kategorieauswahl, Payee-Auswahl, Regel-Formular oder „Details“ offen, öffnen Knopf, Feld und A nichts. Die
  Split-Ziel-Auswahl ersetzt den Inhalt des Sheets.
- **Zustand:** kein neues Feld. „Editor über der Karte“ heißt `SplitEditor = Some e` mit `e.EntryId` = Karte und
  `DetailShown = false`, abgeleitet wie `reviewEditorPlace`. Solange der Editor offen ist, ändert sich `DetailShown`
  nicht.
- **Schließen:** ×, „Abbrechen“ und Esc schließen den Editor und zeigen die Karte, der Entwurf wird verworfen.
  Esc-Kette in `KeyLeave`: Split-Ziel-Auswahl, Editor, Kategorieauswahl, Payee, Memo, Details, Ansicht verlassen.
  Während gespeichert wird (`CategorySending`) schließt der Editor nicht (`CloseSplitEditor` hinter `whenRowIdle`).
- **Tasten bei offenem Editor über der Karte:** nur Esc; Enter, S, K, L, Z und A wirken nicht.
- **Nach dem Speichern:** die Karte steht wieder da, das Feld zeigt „Aufgeteilt“ (bzw. die Kategorie beim Aufheben)
  und leuchtet kurz. Bestätigung am Feld (wie ledger-mf2bg), ohne Toast:
  - aufgeteilt: „Aufgeteilt in <n> Zeilen“ mit „Rückgängig“ (`UndoSplit`, nur bei `undoAvailable`)
  - aufgehoben: Text aus `splitSavedNote` mit `unsplit`
  - mit geändertem Betrag (ADR 0092): Text aus `amountChangedNote`, ohne Rückgängig
  - YNAB nimmt es nicht an: gelb mit Text aus `notInYnabDetailNote`
  - `CategoryUnchanged`: keine Bestätigung

  `ReviewFieldNote` bekommt dafür `Undo: ToastActionKind option` und einen Ton. „Passt, weiter“ ist danach frei
  (`reviewHasTarget`). Die Zeile der letzten Aktion bleibt unverändert.
- **Speicherfehler:** der Editor bleibt mit `SaveError` über „Speichern“ offen (wie heute).
- **Taste Z** nimmt die jüngste Aktion mit Rückgängig; hat die jüngste Bestätigung keins, tut Z nichts.
- Das Detail behält seinen Knopf „Aufteilen“/„Ändern“; Schließen dort führt weiter ins Detail.

## Acceptance criteria
- [ ] Reine Funktion für den Knopf (etwa `reviewSplitButton`): versteckt auf der Duplikat-Karte, frei ohne Sperre,
      gesperrt mit genau dem Text aus `splitLockTextFor`; ein Fall je Sperrgrund (wartend, gelöscht, Tracking,
      Abgleich, Übertrag, Ersatz offen, fremde `import_id` bei aufgeteilter Buchung) und je Kartenart.
- [ ] Reine Funktion, ob die Karte den Editor öffnen darf (etwa `reviewSplitOpenable`): nein bei offener
      Kategorieauswahl, Payee-Auswahl, Regel-Formular, „Details“, laufendem Aufruf der Gruppe Kategorie und
      Duplikat-Karte, sonst ja; Tabelle aller Fälle.
- [ ] `reviewKeyAction`: A/a auf der Normalkarte öffnet den Editor, auf der Duplikat-Karte und in der Endansicht
      nichts; bei offenem Editor über der Karte liefert `reviewKeyFor` nur Esc; ein Fall je Taste und Zustand.
- [ ] Esc-Kette als reine Funktion (etwa `reviewLeaveStep`), ein Fall je Stufe, darunter „Editor offen, Esc verlässt
      das Durchgehen nicht“ als Regressionstest, rot vor dem Fix (heute fehlt der Editor in `KeyLeave`).
- [ ] Ein Esc schließt genau eine Stufe, auch wenn `EscapePressed` und `ReviewKey` beide am Durchgehen hängen (Test
      über den echten Tastenweg oder Nachweis, dass nur einer feuert).
- [ ] Reine Funktion für die Bestätigung am Feld nach einem Split (Text, Ton, Rückgängig): aufgeteilt mit und ohne
      `undoAvailable`, aufgehoben, mit Betrag ohne Rückgängig, `CategorySendFailed` gelb, `CategoryUnchanged` ohne
      Bestätigung.
- [ ] Zustandsfolgen durch das echte `update` nach Doktrin Punkt 20 als Aufzählung aller Reihenfolgen. Nachrichten:
      Öffnen von der Karte (Knopf, Feld, A), `StartSaveSplits`, Antworten direkt eingespeist (`SplitsSaved` Ok mit und
      ohne Rückgängig, mit `CategorySendFailed`, Error, `SplitsWithAmountSaved`), `KeyLeave`, `CloseSplitEditor`,
      „Details“, Kategorieauswahl öffnen, Passt, Später, Wisch, `ReviewUndoLast`, Z. Orakel unabhängig vom Code:
      höchstens eine Ebene offen; `DetailShown` und Index bleiben gleich, solange der Editor offen oder die Zeile
      belegt ist; nach Erfolg ist der Editor zu, dieselbe Karte steht, `FieldNote` gehört zu ihr, `Last` bleibt gleich;
      kein Prüfvermerk während des Speicherns; Schließen während des Speicherns bleibt still.
- [ ] `src/Tests/LedgerRowLockWaysTests.fs`: „Aufteilen von der Karte“ als weiterer Weg der Gruppe Kategorie.
- [ ] Nach dem Speichern auf der Karte ist „Passt, weiter“ frei und Z nimmt den Split zurück; nach Speichern mit
      Betrag tut Z nichts (Folge durch `update`).
- [ ] Browser: `[flow:durchgehen]` bleibt einer; ein `[tastatur]`-Test am PC (A öffnet, Esc schließt erst
      Split-Ziel-Auswahl, dann Editor, dann Ansicht). Layout-Sweep: Karte mit Knopf (frei, gesperrt mit Grund,
      aufgeteilt) und Editor über der Karte bei 320, 412 und 1280 px, „Passt, weiter“ ohne Scrollen erreichbar.
- [ ] Touch bei 412 px im Editor über der Karte: Betragsfelder ohne Vorzeichen (`inputmode="decimal"`, kein Minus),
      der Wechsel zwischen Betragsfeldern und zur Memo-Zeile schließt die Tastatur nicht (`[fokus]`-Test, falls nicht
      schon vom Editor im Detail gedeckt).
- [ ] Werte aus dem Styleguide, nichts erfunden; „Feld mit Knopf daneben“ als Variante an `ReviewCard` auf
      `/styleguide`, Eintrag in `standards/frontend/styleguide.md` nachgezogen.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten; Screenshots von BB bei 412 und 1280 px (Karte
      mit Knopf, gesperrter Knopf, aufgeteilte Karte mit Bestätigung, Editor über der Karte) neben den
      Skizzen-Screenshots im Outcome.
- [ ] README ledger: **Durchgehen**, **Split-Editor (Kassenbuch)**, **Barabhebung (Split-Editor)** und
      **Zeilen-Sperre** nachgezogen (Abweichung (a) zurückgenommen), Delta durch `npm run check:readme-delta` samt
      Prüfung 7.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.
- [ ] Roman hat Aufteilen auf der Karte am Handy (412 px, Touch) und am PC (Taste A) neben der Skizze angesehen.
      [human-eye]

## Notes
- Fragen an Roman vor dem Promote, danach Skizze zur Freigabe:
  1. Ort des Knopfs: (A) neben dem Feld „Kategorie“, (B) dritter Knopf unter der Karte zwischen „Lena fragen“ und
     „Später“, (C) Eintrag „Aufteilen …“ in der Kategorieauswahl. Empfehlung A: ein Tipp, Karte wird nicht höher,
     die Knöpfe unter der Karte bleiben die, die zur nächsten Karte gehen. B bricht „Rückfrage zurücknehmen“ bei
     412 px auf drei Zeilen um; C braucht zwei Tipps und ändert einen gemeinsamen Baustein.
  2. Feld „Aufgeteilt“ öffnet den Editor (mit Pfeil wie im Detail)? Empfehlung: ja.
  3. Knopf bei aufgeteilter Buchung „Aufteilen“ oder „Ändern“? Empfehlung: „Aufteilen“ bleiben.
  4. Bestätigung am Feld „Kategorie“ oder in der Zeile unter „Passt, weiter“? Empfehlung: am Feld (ledger-mf2bg).
  5. Taste A am PC? Empfehlung: ja.
  6. Z ohne Rückgängig bei der jüngsten Bestätigung? Empfehlung: tut nichts.
- Vor dem Bauen klären: ob `EscapePressed` im Durchgehen neben `ReviewKey` feuert (`ReviewKeyboard`, View.fs ~5348).
- `StartSaveSplits` lässt den Editor bis zur Antwort offen; mit gesperrtem Schließen landet die Bestätigung immer auf
  der richtigen Karte. `reviewCardEditing` hält die Karte schon heute fest.
- Der Zweig `NoteOnReviewCard` in `reportSplitNote` wird für Splits zur Bestätigung am Feld; ein Split aus „Details“
  ergibt weiter `NoteAtField`.
- Keine neuen Pflichtfelder in `src/Shared`, keine Folgen für die Fable.Remoting-Fixtures. Kein ADR nötig.
- Die Karte heute (für die Skizze): Kopf „Zurück“, „Durchgehen“, „<i> von <n>“, Fortschrittsbalken; auf der Karte
  „<Tag> · <Konto> · <Herkunft>“, Payee, Betrag, Feld „Kategorie“ (52 px; „Aufgeteilt“ heute ohne Pfeil und ohne
  Wirkung), Bestätigung am Feld, „aus Bestellnummer“, „Vorgeschlagen“-Chips, Linkzeilen, Memo, Lena-Hinweis, unten
  „Regel anlegen“ und „Details“. Darunter „Lena fragen“ und „Später“, „Passt, weiter“, Fußzeile (Wisch-Hinweis oder
  letzte Aktion). PC: Spalte bis 620 px, Tastenleiste „Enter passt · S später · K Kategorie · L Lena · Z rückgängig ·
  Esc zurück“.
- Code: `ReviewNormalCard` (View.fs ~5391), `splitLockTextFor`/`splitActionMsg` (~1666), `detailPanelView` (~2267),
  `DetailBottomSheet` (~2300), `OpenSplitEditor` (State.fs ~3985), `SplitsSaved` (~4390), `KeyLeave` (~5421),
  `reportSplitNote` (~1109), `reviewKeyAction` (Types.fs ~1732), `ReviewFieldNote` (~1499), `reviewEditorPlace`
  (~2284), `ReviewCard.fieldIn` (DesignSystem/ReviewCard.fs ~127).
