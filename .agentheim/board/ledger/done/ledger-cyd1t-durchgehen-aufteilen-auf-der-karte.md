---
id: ledger-cyd1t
title: "Durchgehen: Textlink „Aufteilen“ unter dem Feld Kategorie öffnet das Detail mit offenem Split-Editor, ohne Umweg über „Details“"
status: done
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
am 2026-11-20 freigegeben).

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

## Outcome

Aufteilen geht im Durchgehen direkt von der Karte. Der Textlink „Aufteilen“ steht unter dem Feld „Kategorie“ der Normalkarte. Das Feld „Aufgeteilt“ trägt einen Pfeil, solange Aufteilen frei ist, und Taste A tut am PC dasselbe. Alle drei öffnen in einem Schritt das Detail der Karte mit offenem Split-Editor (`ReviewOpenSplit`, kein Zwischenzustand). Damit ist Abweichung (a) aus ledger-v6s7c zurückgenommen. Der Rückweg ist der heutige: Speichern, ×, „Abbrechen“ und Esc führen ins Detail, Schließen des Details führt zur Karte.

Code:
- `src/Client/Components/Ledger/Types.fs`: `splitLockReason` (der Sperrtext aus dem Detail, `splitLockTextFor` in View ruft ihn nur noch auf), `ReviewSplitLink` und `reviewSplitLink` (versteckt auf der Duplikat-Karte, frei, gesperrt mit Grund), `KeyOpenSplit` in `reviewKeyAction` (A/a nur auf der Normalkarte), Nachricht `ReviewOpenSplit`.
- `src/Client/Components/Ledger/State.fs`: `ReviewOpenSplit` (frei nur auf freier Normalkarte, ohne offene Kategorieauswahl oder Eingabe, ohne laufenden Aufruf der Gruppe Kategorie und ohne laufendes „Passt“; steht das Detail am PC schon, öffnet nur der Editor darin). `KeyLeave` schließt jetzt erst die Auswahl im Editor, dann den Editor, und während Speichern nichts. „Später“ und „Passt“ (also auch der Wisch) tun nichts, solange der Split-Editor offen ist; „Später“ zusätzlich nicht, solange ein Aufruf der Gruppe Kategorie für die Karte läuft.
- `src/Client/Components/Ledger/View.fs`: Link und Feld in `ReviewNormalCard`, Esc in `reviewKeyFor` auch bei offenem Editor und aus seinen Feldern, „A aufteilen“ in `reviewKeys` zwischen K und L, „Später“ und „Passt, weiter“ gedämpft, solange der Editor offen ist.
- Neue DS-Variante `ReviewCard.fieldLink` (`src/Client/DesignSystem/ReviewCard.fs`): 12 px im Akzent ohne Fläche wie „Regel anlegen“, linksbündig, am Handy 48 px Trefferfläche, die in den Abstand der Karte hineinragt (die Karte wird nicht höher). Gesperrt in `text-muted`, ohne Wirkung, Grund als `title` und darunter 12 px gedämpft. Steht auf `/styleguide` (frei, gesperrt, aufgeteilt) und im Eintrag `ReviewCard` in `standards/frontend/styleguide.md`.

Bug (Doktrin Punkt 8): Bei offenem Split-Editor im Durchgehen tat Esc nichts, weil `reviewOverlayOpen` jede Taste sperrte. Außerdem übersprang `KeyLeave` den Editor und hätte Detail und Entwurf in einem Schritt geschlossen. Die drei Tests in „Aufteilen auf der Karte: Esc“ waren mit zurückgedrehtem Fix rot (Esc bei offenem Editor ergab `None`, Auswahl und Editor schlossen falsch, Model änderte sich während Speichern). Die Aufzählung der Zustandsfolge war rot, bevor „Später“ auf die belegte Zeile achtete (Aufteilen, Speichern, Detail schließen, Später: die Karte wechselte, während die Aufteilung noch lief).

Tests:
- `src/Tests/LedgerReviewSplitClientTests.fs` (neu, 16 Tests): `reviewSplitLink` je Sperrgrund (gelöscht, Tracking, Abgleich, Übertrag, Ersatz offen, fremde `import_id` aufgeteilt, gegen den Text aus `splitLockTextFor`) und je Kartenart (wartend und andere Sperren auf der Duplikat-Karte: versteckt). Taste A je Taste und Zustand, Tastenleiste, A aus Feld, mit Strg, bei offenem Detail und über dem Regel-Formular. `ReviewOpenSplit` durch das echte `update`: frei ergibt `DetailShown = true` und `SplitEditor = Some` der Karte in einem Schritt. Gesperrt, Duplikat-Karte, Kategorieauswahl, Payee-Auswahl, laufende Kategorie, Editor schon offen, Endansicht und laufendes „Passt“ lassen das Model gleich. Esc-Kette in drei Stufen. Zustandsfolge als Aufzählung bis Tiefe 7 (Aufteilen, Speichern, Antworten Ok und Fehler, Editor schließen, Detail schließen, Passt mit Antworten, Später, Wisch rechts und links) mit eigenem Orakel: höchstens ein Sheet-Inhalt, die Stelle bleibt bei offenem Editor oder belegter Zeile, nach Erfolg ist der Editor zu und das Detail steht, wenn es offen war.
- `src/Tests/LedgerRowLockWaysTests.fs`: Gruppe „Kategorie und Split im Durchgehen“ mit „Aufteilen von der Karte“ als weiterem Weg. Neu ist `Calls = false` für Wege, die nur öffnen: gesperrt müssen sie still bleiben, ohne selbst einen Aufruf zu schicken.
- Browser: `[tastatur]` in `scripts/durchgehen.test.mjs` (A öffnet das Panel mit Editor, Esc aus einem Editor-Feld schließt nur den Editor, das nächste das Detail, das dritte die Ansicht). `[flow:durchgehen]` bleibt einer. Layout-Sweep: neue Zustände `durchgehen-aufteilen-gesperrt`, `durchgehen-aufgeteilt` und `durchgehen-aufteilen-editor`, die freie Karte steht in `durchgehen-karte`. Gegenprobe: ohne die 48-px-Klasse meldet der Sweep `review-split` mit 58 × 16 px (Invariante 2).
- `./verify.sh`: VERIFY: PASS (dotnet 5512 grün, node 358 grün). Ein früherer Lauf war nur am fremden Zustand `kassenbuch-kategorie-fehler-feld` rot (Zeitüberschreitung unter Last paralleler Worktrees, einzeln grün). Ein Lauf davor zeigte `durchgehen-memo-lang` als „gestaucht“, weil der Überstand des Links aus seiner Hülle ragte. Behoben: die Hülle trägt den Überstand.

Abgleich mit der Skizze (Version 4). Screenshots von BB:
- Karte mit freiem Link: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/cyd1t/a-karte-frei-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/cyd1t/a-karte-frei-1280.png`
- Gesperrt mit Grund: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/cyd1t/b-gesperrt-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/cyd1t/b-gesperrt-1280.png`
- Detail mit Editor nach dem Tipp: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/cyd1t/c-editor-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/cyd1t/c-editor-1280.png`
- Aufgeteilt mit Pfeil: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/cyd1t/d-aufgeteilt-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/cyd1t/d-aufgeteilt-1280.png`
- Skizze: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/skizze/skizze-1-aufteilen.png`

Wie in der Skizze sind Link, Grund, Pfeil an „Aufgeteilt“, Editor im Sheet bzw. Panel, „A aufteilen“ in der Tastenleiste und „Passt, weiter“ ohne Scrollen. Abweichungen, mit Grund:
1. Der Sweep läuft bei 412 und 1440 px statt 320, 412 und 1280. Roman hat die übrigen Breiten am 20.11. gestrichen (`layout-invariants.mjs`). Die Screenshots sind bei 412 und 1280 px.
2. Am PC sind „Später“ und „Passt, weiter“ gedämpft, solange der Editor im Panel offen ist (Skizze E zeigt „Später“ aktiv). Ein Kartenwechsel verwürfe den Entwurf.
3. Orakel „Index bleibt bei offenem Detail“: gilt bei offenem Editor und belegter Zeile. Bei offenem Detail ohne Editor wirken „Passt“ und „Später“ am PC neben dem Panel wie heute (nach dem Speichern gleich „Passt“ drücken). Am Handy deckt das Sheet die Knöpfe. Schließt jemand das Detail, während Speichern läuft, steht nach Erfolg die Karte.
4. Am PC mit schon offenem Detail öffnet der Link nur den Editor im Panel (sonst wäre der sichtbare Link ohne Wirkung).
5. Das Regel-Formular liegt nicht im Ledger-Model. Geprüft ist, dass A darüber nichts tut (`reviewKeyRoute`). Link und Feld liegen dann unter Sheet bzw. Dialog.
6. „Aufgeteilt“ bei gesperrtem Aufteilen bleibt ohne Pfeil wie heute.
7. Der Link hat dasselbe Schriftgewicht wie „Regel anlegen“ (Skizze 500), so verlangt es die Task („gleicher Baustein wie die Linkzeile“).

README-Delta: durch `npm run check:readme-delta -- --bc ledger` samt Prüfung 7 geprüft, Exit 0, „Ganz nachlesen: keine Einträge“, kein `--keep`.

Offen: [human-eye] Roman sieht sich Aufteilen auf der Karte am Handy (412 px, Touch) und am PC (Taste A) neben der Skizze an. Das kann der Worker nicht.
