---
id: 0020
title: Im Kassenbuch einen Elmish-Auslöser mit Entscheidungslogik in eine reine und eine unreine Nachricht teilen, damit der Reduzierer unter .NET testbar bleibt
scope: ledger
status: accepted
date: 2026-09-06
supersedes: []
superseded_by: []
related_tasks: [ledger-q5w9c]
related_adrs: []
related_research: []
---

# ADR 0020: Im Kassenbuch einen Elmish-Auslöser mit Entscheidungslogik in eine reine und eine unreine Nachricht teilen

> **Geltungsbereich: nur der Kassenbuch-Kontext (`src/Client/Components/Ledger`).**
> Diese Entscheidung beschreibt eine Technik, die das Kassenbuch anwendet. Sie stellt
> **keine** Anforderung an `Rules`, `Settings`, `SyncFlow` oder künftige Komponenten
> anderer Kontexte, und sie behauptet nicht, dass das Repo ihr folgt. Es folgt ihr
> nachweislich nicht (siehe "Der Bestand im übrigen Repo"). Ob sie repo-weit gelten
> soll, ist eine größere Entscheidung mit eigenem Preis, und die wird hier nicht
> getroffen. Eine Durchsetzung braucht diese Fassung nicht: im Kassenbuch gibt es
> genau einen so geteilten Auslöser, und seine Tests sind die Kontrolle.

## Context
Die Reduzierer der Client-Komponenten werden unter .NET mit Expecto getestet, nicht im
Browser. Ein `update`-Zweig, der den Fable.Remoting-Proxy anfasst (`Api.ledger`,
`Api.api`, `Api.rules`, …), ist dort nicht ausführbar: der Typ-Initialisierer des Proxys
existiert nur im Fable-Ausgabecode und wirft unter .NET, und zwar schon beim Bauen des
Kommandos, weil `Cmd.OfAsync.either Api.ledger.…` den Proxy-Wert auswertet. Steht dieser
Aufruf im selben `Msg`-Zweig wie der interessante Zustandsübergang, ist der Übergang
nicht testbar, obwohl er die eigentliche Zusage an Roman ist.

Bei "Jetzt spiegeln" auf der Kassenbuch-Seite ist genau das der Fall. Der Zweig
entscheidet etwas: Läuft schon ein Spiegel-Lauf, tut ein zweiter Druck nichts
(Doppeldruck-Sperre); sonst wird der Lauf-Hinweis gesetzt und die Liste bleibt bewusst
stehen, statt auf `Loading` zurückzufallen. Das ist Logik, die falsch sein kann. Der
`Cmd.OfAsync` daneben ist Mechanik.

Verworfene Alternativen:
- **Den Proxy hinter eine Schnittstelle legen.** Eine Indirektion durch jede Komponente,
  nur um einem Typ-Initialisierer auszuweichen. Für einen Auslöser steht der Preis in
  keinem Verhältnis.
- **Den Übergang gar nicht testen.** Dann ist die Doppeldruck-Sperre eine Behauptung.
  Genau in diesem Zustand ist `ConfirmTan` im Sync-Kontext (siehe unten).

## Decision
1. **Im Kassenbuch wird ein Auslöser geteilt, der sowohl einen Seiteneffekt startet als
   auch sichtbaren Zustand mit Entscheidungslogik setzt**: eine **reine** Nachricht setzt
   nur den Zustand und feuert die zweite über `Cmd.ofMsg` (`StartMirror`,
   `src/Client/Components/Ledger/State.fs:132`), eine **unreine** setzt nur den Aufruf ab
   und lässt das Modell unverändert (`RunMirror`, ebenda `:140`).
2. **Getestet werden die reine Nachricht und die Ergebnis-Nachricht** (`MirrorFinished`).
   Die unreine bleibt ungetestet und ist deshalb logikfrei zu halten: kein `if`, kein
   `match` über das Modell, keine Modelländerung. Enthält sie Logik, ist sie falsch
   geschnitten.
3. **Nicht geteilt wird der schlichte RemoteData-`Loading`-Marker neben seinem eigenen
   Abruf.** Ein Zweig der Form `{ model with X = Loading }, Cmd.OfAsync …` trifft keine
   Entscheidung: der Marker ist die mechanische Kehrseite des Aufrufs, es gibt keinen
   Zweig, der anders ausgehen könnte, und ein Test darauf läse nur die Schreibweise des
   Zweigs zurück. Im Kassenbuch fallen `LoadAccounts`, `LoadEntries`, `LoadHistory` und
   `LoadMirrorStatus` darunter. Das Abgrenzungsmerkmal ist **Entscheidungslogik im
   Zweig**, nicht "sichtbarer Zustandswechsel": die `Load*`-Auslöser setzen sehr wohl
   sichtbaren Zustand.
4. **Der Geltungsbereich ist der Kassenbuch-Kontext.** Der technische Grund (.NET-
   Reduzierertests plus Fable-only-Proxy) gilt zwar für jede Elmish-Komponente, aber ein
   Grund ist noch keine Regel: repo-weite Geltung hieße, den Bestand entweder umzubauen
   oder eine Regel zu führen, die an zehn Stellen gebrochen ist. Beides ist mehr, als
   diese Aufgabe entscheiden darf.

## Ausnahme in Punkt 3, ehrlich abgegrenzt
`LoadEntries` (`src/Client/Components/Ledger/State.fs:68`) matcht auf
`SelectedAccountId`. Der `Some`-Fall ist der reine `Loading`-Marker und fällt unter
Punkt 3. Der `None`-Fall dagegen gibt `model, Cmd.none, NoOp` zurück, fasst den Proxy
also gar nicht an und **ist** unter .NET testbar. In einer früheren Fassung dieser ADR
stand er als Begründung für die Ausnahme; das war falsch herum. Er ist kein Argument für
die Ausnahme, sondern eine kleine offene Test-Lücke: "ohne gewähltes Konto lädt
`LoadEntries` nichts" ist ein Satz, den ein Reduzierertest festhalten könnte, und heute
tut es keiner (`src/Tests/LedgerPageTests.fs` deckt `LoadEntries` nicht ab).

## Der Bestand im übrigen Repo
Nachgezählt am 2026-09-06 über `src/Client/Components/{Rules,Settings,SyncFlow}/State.fs`.
Zeilennummern sind die des jeweiligen `Cmd.OfAsync`-Aufrufs, nicht die des
Zweig-Kopfes, damit sie per `grep -n "Cmd.OfAsync"` reproduzierbar sind.

42 `Cmd.OfAsync`-Stellen insgesamt:
- **23 geben das Modell unverändert zurück** (`model, cmd, …`) und berühren die Frage
  überhaupt nicht.
- **8 sind der reine `Loading`-Marker** aus Punkt 3: Rules `:36`; Settings `:48`, `:104`,
  `:266`, `:282`; SyncFlow `:117`, `:132`, `:196`.
- **11 setzen geänderten Zustand jenseits von `Loading` im selben Zweig wie den
  Proxy-Aufruf**: Rules `:83` (`ConfirmDeleteRule`), `:96` (`DeleteRule`), `:167`
  (`MoveRule`), `:269`/`:290` (`SaveRule`); SyncFlow `:176` (`ConfirmTan`), `:301`
  (`CommitCategoryChange`), `:413` (`SkipAllVisible`), `:454` (`UnskipAllVisible`),
  `:895` (`CommitPayeeChange`), `:1008` (`SaveInlineRule`).

Von diesen 11 tragen **9 Stellen (8 Nachrichten) echte Entscheidungslogik** und wären
nach Punkt 1 zu teilen: `MoveRule`, `SaveRule` (zwei Stellen), `ConfirmTan`,
`CommitCategoryChange`, `SkipAllVisible`, `UnskipAllVisible`, `CommitPayeeChange`,
`SaveInlineRule`. Die restlichen zwei liegen anders: `DeleteRule` (`:96`) ändert
sichtbaren Zustand (`ConfirmingDeleteRuleId = None`) ohne jede Entscheidung, ist damit
weder von Punkt 1 erfasst noch von Punkt 3 gedeckt, und dieser Zustandswechsel ist
heute nicht testbar; `ConfirmDeleteRule` (`:83`) fasst den Proxy überhaupt nicht an
(`Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep 3000 })`), ist unter .NET
ausführbar und hat mit dieser ADR nichts zu tun.

Am deutlichsten ist `SyncFlow/State.fs:164` (`ConfirmTan`): `if model.IsTanConfirming
then model, Cmd.none` als Doppeldruck-Sperre, sonst `IsTanConfirming = true` **und**
optimistisch `Status = FetchingTransactions` **und** `Cmd.OfAsync Api.sync.confirmTan`.
Das ist Zeile für Zeile dieselbe Gestalt wie `StartMirror`/`RunMirror`, mit derselben
Begründung, ungeteilt und untestbar. Es liegt im Kontext `ynab-sync`, nicht im
Kassenbuch, deshalb wird hier weder umgebaut noch eine Aufgabe dafür geschnitten; es ist
der Grund, warum diese ADR keinen repo-weiten Anspruch erhebt.

Eine frühere Fassung behauptete, eine mechanische Prüfung würde an "40 von 42"
regelkonformen Stellen anschlagen, und schloss daraus, sie sei unbaubar. Die Zahl war
falsch: die Ausnahmeliste wäre 8 Einträge lang, also gewöhnlich, und eine Prüfung würde
11 Stellen melden, davon 9 zu Recht. Der Grund gegen eine Prüfung war damit vorgetäuscht.
Der wirkliche Grund, sie nicht zu bauen, ist der jetzt gewählte Geltungsbereich: eine
Prüfung über einen einzigen Auslöser in einem Kontext prüft nichts, was seine Tests nicht
schon prüfen.

## Consequences
### Positive
- Der Übergang von "Jetzt spiegeln" ist ohne Browser, Netz und Test-Doubles prüfbar
  (12 Reduzierer-Tests in `src/Tests/LedgerPageTests.fs`).
- Die ADR behauptet nur noch, was nachweisbar gilt. Der Bestand widerspricht ihr nicht
  mehr, weil sie ihn nicht mehr regiert.
- Künftige Kassenbuch-Auslöser (etwa der automatische Spiegel-Anstoß aus `ledger-h4p6c`)
  haben eine Vorlage samt Grund, statt `StartMirror`/`RunMirror` für einen Zufall zu
  halten und zusammenzuziehen.

### Negative
- **Kein repo-weiter Nutzen.** Die zehn ungeteilten Nachrichten in `Rules` und `SyncFlow`
  bleiben untestbar; `ConfirmTan`s Doppeldruck-Sperre bleibt eine unbelegte Behauptung.
  Diese ADR ändert daran nichts und gibt vor, es zu tun.
- **Zwei Techniken im Repo.** Wer vom Sync-Kontext ins Kassenbuch schaut, sieht zwei
  Bauweisen für denselben Fall, und die Begründung steht nur an einer Stelle.
- Ein zusätzlicher `Msg` je geteiltem Auslöser, plus die Disziplin, die unreine
  Nachricht zustandsfrei zu halten.
- Die Grenze "Entscheidungslogik" bleibt ein Urteil. `DeleteRule` im Sync-nahen Bestand
  zeigt den Fall, den weder Punkt 1 noch Punkt 3 sauber fasst: sichtbarer Zustand ohne
  Entscheidung, trotzdem untestbar.
