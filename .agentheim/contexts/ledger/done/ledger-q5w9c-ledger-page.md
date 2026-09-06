---
id: ledger-q5w9c
title: Kassenbuch-Seite: Buchungen eines Kontos mit Herkunfts-Badge und Historie, "Jetzt spiegeln"
status: done
type: feature
context: ledger
created: 2026-09-06
depends_on: [ledger-m8r2v, design-system-001]
blocks: [ledger-kt0wq]
tags: [foundation, frontend]
related_adrs: [0013, 0014, 0016, 0005, 0020]
related_research: []
prior_art: []
---

## Why
Slice 3 von 3 des Walking Skeletons (`ledger-kt0wq`). Ohne Sicht bleibt das Kassenbuch
unsichtbar ("wenn niemand hineinschaut" ist ein Scheiterkriterium der Vision). Die Seite
beweist, dass Log, Spiegel und Anzeige zusammen funktionieren.

## What
1. **Queries** (semantische Read-Funktionen, Read-Models in `src/Shared/Domain.fs`):
   `getLedgerAccounts`, `getLedgerEntriesForAccount accountId` (Datum, Payee, Betrag,
   Kategorie-Name, Herkunft, gelöscht-Flag, Splits), `getEntryHistory entryId` (Historie
   als Liste: Zeitpunkt, Actor, sprechende Beschreibung des Ereignisses),
   `getMirrorStatus` (letzter Lauf). Endpunkte in `src/Shared/Api.fs` und `src/Server/Api.fs`.
2. **Neue Seite "Kassenbuch"** unter Route `#/ledger` (Hash-Routing wie die anderen
   Seiten), Eintrag in der Navigation, eigene Komponente `src/Client/Components/Ledger/`
   (Types, State, View) nach MVU mit RemoteData.
3. **Anzeige**: Konto-Auswahl (Standard: das Konto, das `ynab_default_account_id`
   entspricht, sonst das erste), Kontostand als Summe der nicht gelöschten Buchungen,
   Buchungsliste (Datum, Payee, Betrag über `Money`, Kategorie, Herkunfts-Badge
   "YNAB-Spiegel"), gelöschte Buchungen bleiben sichtbar mit Badge "in YNAB gelöscht".
   Tippen auf eine Buchung öffnet die Historie (BottomSheet nach ADR 0005) mit allen
   Einträgen und Zeitpunkt.
4. **"Jetzt spiegeln"**: Button im PageHeader ruft `mirrorYnabBudget`, währenddessen
   Hinweis "Spiegel läuft" (Loading), danach Liste neu laden und Zusammenfassung als Toast.
   Letzter Lauf mit Zeitpunkt sichtbar. Kein automatischer Auslöser in diesem Slice.
5. **Nur DS-Komponenten und Tokens** (Styleguide-Gate `design-system-001`); mobil lesbar.
6. **Tests**: Reducer-Tests für den Ledger-State (Laden, Konto wechseln, Spiegel-Lauf
   Start/Ende/Fehler; keine Pfade, die den Fable.Remoting-Proxy berühren), Read-Model-
   Mapping rein (Ereignis → Historie-Beschreibung) mit Tests.

## Acceptance criteria
- [x] Route `#/ledger` mit Navigationseintrag; Seite zeigt Konten, Buchungen und
      Kontostand des gewählten Kontos.
- [x] Jede Buchung zeigt Herkunfts-Badge; gelöschte Buchungen sind markiert, nicht
      verschwunden.
- [x] Historie pro Buchung im BottomSheet, Einträge mit Zeitpunkt und sprechender
      Beschreibung.
- [x] "Jetzt spiegeln" mit Lauf-Hinweis, Neuladen und Ergebnis-Toast; letzter Lauf sichtbar.
- [x] Ausschließlich DS-Komponenten und Tokens; keine Inline-Farben.
- [x] `dotnet build`, `dotnet test`, `npm run build` grün.
- [x] `ledger/README.md`: Abschnitt "Sichten" mit den Queries.

## Notes
Historie-Beschreibungen sind Sprache des Kassenbuchs ("Kategorie in YNAB geändert:
Lebensmittel → Restaurant"), keine Typnamen. Screenshot-Rezept für die Abnahme: `npm run
dev`, Vite auf 5181, Route `/#/ledger`.

## Verifier note (iteration 1)

Checks 1 bis 6b bestanden: `dotnet test` Exit 0 (709 bestanden / 7 übersprungen / 0 Fehler),
`npm run build` Exit 0, ADR 0017 gehalten (kein `SUM(` / `CAST(` in `src/Server/Ledger/`
außer im verbietenden Kommentar), keine Inline-Farben in der Ledger-View,
`hover:bg-surface-hover` als Vorbild in Navigation, Button, Modal und Toast bestätigt,
kein `.agentheim/`-Pfad im Diff. **Check 6c schlägt fehl.**

**REASONS:**
- ADR 0020 (`scope: global`) stellt eine repo-weite strukturelle Konvention auf — ein
  Auslöser, der einen Seiteneffekt startet und sichtbaren Zustand setzt, muss in eine reine
  Nachricht (`StartMirror`, setzt nur Zustand) und eine unreine (`RunMirror`, setzt nur den
  Aufruf ab) geteilt werden — und sagt selbst, die Regel gelte für jede Elmish-Komponente im
  Repo. Der Diff liefert keine Durchsetzung: nichts in `src/Tests/` würde eine künftige
  Elmish-Komponente fangen, die nicht teilt. `src/Tests/LedgerPageTests.fs` prüft nur die
  Teilung dieser einen Komponente, also die Instanz, nicht die Konvention.
- Es fehlt auch der Gegenpol: kein "prose-only, unenforced"-Vermerk. Die `## Notes` des
  Tasks halten nur die Historien-Sprache und das Screenshot-Rezept fest; die
  `## Consequences` des ADR halten fest, dass die unreine Nachricht ungetestet bleibt (eine
  Folge des Befolgens, keine Entscheidung, die Regel undurchgesetzt zu lassen);
  `BACKLOG_ITEMS` ist leer. Keine der beiden Hälften von mechanize-or-drop ist da.
- Die Konvention hat schon im Diff, der sie einführt, unerwähnte Ausnahmen, und daran hängt
  der Befund: `src/Client/Components/Ledger/State.fs` setzt in `LoadAccounts`,
  `LoadEntries`, `LoadHistory` und `LoadMirrorStatus` je einen sichtbaren `Loading`-Zustand
  **und** ein `Cmd.OfAsync` im selben Zweig — genau die Form, die ADR 0020 verbietet. Der
  Kopfkommentar von `src/Tests/LedgerPageTests.fs` benennt alle vier als bewusst ungetestet.
  Entweder braucht der ADR eine ausdrückliche Ausnahme für den RemoteData-`Loading`-Marker,
  oder die vier Auslöser brauchen dieselbe Teilung wie `StartMirror`/`RunMirror`.

**SUGGESTED_FIX:** Entweder Durchsetzung für ADR 0020 liefern (eine laufende Prüfung über
`src/Client/Components/*/State.fs`, die fehlschlägt, wenn ein `update`-Zweig sowohl ein
geändertes Modell zurückgibt als auch ein `Cmd.OfAsync` baut), oder die bewusste Lücke
festhalten — einen "prose-only, unenforced"-Vermerk plus die Ausnahme, dass ADR 0020 reine
RemoteData-`Loading`-Auslöser nicht erfasst, sodass `LoadAccounts` / `LoadEntries` /
`LoadHistory` / `LoadMirrorStatus` eine benannte statt einer stillen Ausnahme sind.

**ITERATION_HINT:** likely-fixable

## Verifier note (iteration 2)

Checks 1 bis 5 bestanden, eigener Lauf: `dotnet test` im Worktree Exit 0 (709 bestanden,
7 übersprungen, 0 Fehler), Diff byte-identisch mit Iteration 1 (HEAD `fd37023`, sauberer
Worktree, 18 Dateien / 1519 Einfügungen), kein `.agentheim/`-Pfad, keine Task-Datei.
**Check 6 schlägt fehl.**

**REASONS:**
- **Die tragende Begründung von ADR 0020 für Route (B) ist über das Repo, das sie regelt,
  sachlich falsch.** Der neue Abschnitt "Durchsetzung" behauptet, eine Prüfung auf
  "geändertes Modell **und** `Cmd.OfAsync`" würde an 40 von 42 regelkonformen
  Bestandszweigen anschlagen, und schließt daraus, die Prüfung sei nicht baubar. Ich habe
  alle 42 Stellen in `src/Client/Components/{Rules,Settings,SyncFlow}/State.fs` danach
  klassifiziert, welches Modell tatsächlich neben dem `Cmd.OfAsync` zurückgegeben wird:
  **23 geben das Modell unverändert zurück** (`model, cmd, NoOp` — Settings L77/L141/L173/
  L194/L218/L242, SyncFlow L150/L272/L345/L368/L657/L686/L709/L734/L753/L785/L814/L837)
  und würden die genannte Regel nie auslösen; **19 geben ein geändertes Modell zurück**.
  Die Regel feuert also auf 19, nicht auf 40. Die "40" ist um rund das 2,5-fache daneben.
- **Von diesen 19 haben nur 8 die Form der Ausnahme.** Schlichtes
  `{ model with X = Loading }, Cmd.OfAsync`: Rules L36, Settings L48/L104/L266/L282,
  SyncFlow L117/L132/L196. Die übrigen **11 tragen echte Entscheidungslogik plus einen
  Zustandswechsel, der kein `Loading` ist — sie verletzen also die eigene, bereits
  verengte "mit Entscheidungslogik"-Regel des ADR**, und der ADR nennt keine davon:
  Rules L153 `MoveRule` (No-op-Guard, dann optimistisch `Rules = Success reordered` +
  `Cmd.OfAsync`), Rules L243 `SaveRule` (vierstufige Validierungskaskade, `nextPriority`-
  Fold, `IsSaving = true` + Effekt, zweimal), Rules L80 `ConfirmDeleteRule` und L96
  `DeleteRule`, SyncFlow L388 `SkipAllVisible` / L425 `UnskipAllVisible` (Filter +
  optimistisches Status-Rewrite + gebündelter Effekt), SyncFlow L301/L895/L1008. Die
  Ausnahmeliste umfasst damit **8 Einträge, nicht 40** — eine gewöhnliche
  Bestandsschutz-Liste — und die Prüfung würde sofort 11 echte Verstöße sichtbar machen.
  Die Schlussfolgerung des ADR ("eine Prüfung mit einer Ausnahmeliste dieser Länge
  schreibt den Bestand fest, statt zu prüfen") ist das Gegenteil dessen, was der Code
  zeigt; Route (B) wurde auf einer Prämisse gewählt, die nicht trägt.
- **`SyncFlow/State.fs:164` `ConfirmTan` ist das eigene Musterbeispiel des ADR, steht
  bereits ungeteilt da und wird nicht erwähnt.** Es ist
  `if model.IsTanConfirming then model, Cmd.none` (Doppeldruck-Sperre) `else` setze
  `IsTanConfirming = true` **und** optimistisch `Status = FetchingTransactions` (L174/L182)
  **und** `Cmd.OfAsync` — strukturgleich zur Teilung `StartMirror`/`RunMirror`, die ADR
  0020 vorschreibt, und mit derselben Doppeldruck-Sperre begründet. Ein `scope: global`-ADR
  kann nicht gleichzeitig behaupten, das Repo sei weitgehend konform, und sein eigenes
  Musterbeispiel anderswo unverändert verletzen lassen.
- **`ledger-w2m4t` erbt die falsche Zahl.** Die Forderung "keine Fehlalarme gegen die 42
  bestehenden Stellen" macht den Wiederaufnahme-Pfad wie spezifiziert unerfüllbar: 11 der
  42 sind echte Verstöße, die eine korrekte Prüfung fangen *soll*. Zusammen mit den Notes,
  die das Schließen als "dauerhaft Prosa" erlauben, ist das Backlog-Item ein Parkschein
  statt eines Wiederaufnahme-Pfads.
- Nebenbefund: die Ausnahme deckt `LoadAccounts`, `LoadHistory`, `LoadMirrorStatus`
  (`Ledger/State.fs:33,99,117`) sauber ab. `LoadEntries` (`Ledger/State.fs:68`) ist ein
  Guard derselben Form wie `StartMirror`, und die im ADR genannte Begründung für die
  Ausnahme ist verkehrt herum: der `None`-Zweig ist gerade das, was ein .NET-Reduzierertest
  festhalten *kann* (Modell unverändert, leeres `Cmd`, kein Proxy berührt).
- Bestätigt gesund, nicht anfassen: die Teilung `StartMirror`/`RunMirror` ist in sich
  stimmig — `RunMirror` (`Ledger/State.fs:140`) gibt `model, cmd, NoOp` zurück, ist also
  wirklich zustandsfrei. Die Marker-Hälfte von 6c ist formal erfüllt und gut platziert.

**SUGGESTED_FIX:** Den Abschnitt "Durchsetzung" auf die echten Zahlen korrigieren (19 von
42 Zweigen geben ein geändertes Modell zurück; 8 passen in die `Loading`-Ausnahme; 11
verletzen die eigene "Entscheidungslogik"-Regel, allen voran `ConfirmTan`) und die Route
auf den wahren Zahlen neu entscheiden — eine Bestandsschutz-Liste mit 8 Einträgen macht
Route (A), eine laufende Prüfung über `src/Client/Components/*/State.fs`, zur plausiblen
statt zur unmöglichen Wahl. Bleibt es bei Route (B), muss der ADR die 11 bestehenden
Verstöße als bekannte Schuld benennen statt repo-weite Konformität zu behaupten, und
`ledger-w2m4t` muss die Forderung "keine Fehlalarme gegen die 42" fallen lassen.

**ITERATION_HINT:** likely-fixable

**Anmerkung des Conductors:** der Verifier hat den ADR-Text aus meiner Zusammenfassung
beurteilt, nicht aus dem Wortlaut — ich hatte den `ADRS`-Block im Spawn-Prompt referenziert
statt eingefügt. Sein Befund stützt sich aber auf den Quelltext, den er selbst gelesen hat,
nicht auf den ADR-Wortlaut. Beim nächsten Durchgang wird der Wortlaut mitgegeben.

## Outcome

Die Kassenbuch-Seite liegt unter `#/ledger` und ist als fünfter Eintrag in der Navigation
erreichbar. Sie zeigt eine Kontoauswahl (vorausgewählt ist das gespiegelte
YNAB-Standardkonto, sonst das erste Konto), den Kontostand, die Buchungsliste mit
Herkunfts-Badge und Kennzeichen für in YNAB gelöschte Buchungen, und je Buchung die
Historie in einem BottomSheet. "Jetzt spiegeln" stößt den manuellen Spiegel-Lauf an, lädt
Konten, Buchungen und Lauf-Status danach neu und meldet das Ergebnis als Toast. Damit ist
Slice 3 von 3 des Walking Skeletons geschlossen und `ledger-kt0wq` kann seine Abnahme
fahren.

**Dieser Lauf hat einen unterbrochenen Lauf wieder aufgenommen.** Der erste Worker war
mit dem Bauen fertig, wurde aber vor dem Melden abgeschnitten; die Gates waren bereits
grün. Die einzige inhaltliche Änderung danach war der Wortlaut der Historien-Sätze:
ASCII-Umschreibung raus, echte Umlaute und der modellierte Pfeil rein. Betroffen waren
`describe` und `clearedLabel` in `QueryHandlers.fs`, die Erwartungen in
`LedgerPageTests.fs` und zwei Doku-Kommentare.

**Wo was liegt.** Der reine `ReadModel`-Teil und drei I/O-Abfragen stehen in
`src/Server/Ledger/QueryHandlers.fs`; die Lesemodelle und `ledgerBalance` in
`src/Shared/Domain.fs`; der Vertrag in `src/Shared/Api.fs`, verdrahtet in
`src/Server/Api.fs`; die Seite in `src/Client/Components/Ledger/{Types,State,View}.fs`,
eingehängt in `Client.fsproj`, `Client/State.fs`, `Client/Types.fs`, `Client/View.fs` und
`DesignSystem/Navigation.fs`. 33 Tests, aufgeteilt 11 (Lesemodelle/Kontostand),
10 (Historien-Sätze), 12 (Reduzierer der Seite).

**Zwei geprüfte Zusagen.** ADR 0017 hält: ein Test summiert zehnmal 0,10 und erwartet
exakt 1,00, kein SQL-`SUM`, keine Fließkomma-Zwischenstufe. Die Historie spricht
Kassenbuch-Sprache, nicht Ereignis-Typnamen (ADR 0013); ein Test hält fest, dass der
Typname im Satz nicht vorkommt.

Das BottomSheet kommt aus dem Design System (ADR 0005). Ein einziges Utility außerhalb
der Tokens ist stehen geblieben, `hover:bg-surface-hover` auf der Buchungszeile, weil das
DS für Zeilen-Hover nichts anbietet.

**Ehrliche Einschränkung zum README-Delta:** einen Abschnitt `## Sichten` gibt es im
BC-README nicht, und `applyReadmeDelta` kann keinen anlegen. Die Sichten-Liste landet
deshalb in `## Ubiquitous language` unter einer fetten Überschriftszeile. Inhaltlich ist
die Zusage erfüllt, eine eigene Überschrift hat sie nicht bekommen.

**Beobachtungen ohne Aufgabe:** `loadEntryOrigins` liest die Herkunft aller Buchungen
eines Kontos in einem Rutsch, das trägt für ein Privat-Kassenbuch, für ein Konto mit
sehr vielen Buchungen nicht ewig. Die mobile Navigation steht jetzt bei fünf Tabs und ist
damit an ihrer Grenze. Der automatische Spiegel-Anstoß war ausdrücklich nicht Teil dieser
Aufgabe und liegt als `ledger-h4p6c` im Backlog.

### Iteration 3: ADR 0020 auf den Kassenbuch-Kontext eingeengt (Route 3)

Ich habe die Zahlen des Verifiers selbst nachgezählt, Zweig für Zweig, über alle 42
`Cmd.OfAsync`-Stellen in `src/Client/Components/{Rules,Settings,SyncFlow}/State.fs`. **Er
hat recht, meine Zahl aus Iteration 2 war falsch.** 23 Zweige geben das Modell unverändert
zurück, 8 sind der reine `Loading`-Marker (Rules `:36`; Settings `:48`, `:104`, `:266`,
`:282`; SyncFlow `:117`, `:132`, `:196`), 11 setzen geänderten Zustand jenseits von
`Loading` neben dem Proxy-Aufruf. Die Ausnahmeliste wäre also 8 Einträge lang, nicht 40.
Mein Schluss, eine Prüfung "schreibt nur den Bestand fest", war das Gegenteil dessen, was
der Code zeigt.

Zwei Korrekturen am Bild des Verifiers, beide nach unten und beide belegbar: von den 11
tragen **9 Stellen (8 Nachrichten) echte Entscheidungslogik** und wären nach der ADR zu
teilen. `Rules:96` (`DeleteRule`) ändert sichtbaren Zustand ohne jede Entscheidung, fällt
damit durch beide Punkte der ADR hindurch, und `Rules:83` (`ConfirmDeleteRule`) fasst den
Proxy gar nicht an (`Cmd.OfAsync.perform` über ein lokales `Async.Sleep`), ist unter .NET
also ausführbar und testbar. In der Sache ändert das nichts: `SyncFlow:164` (`ConfirmTan`)
ist Zeile für Zeile dieselbe Gestalt wie `StartMirror`/`RunMirror`, mit derselben
Doppeldruck-Begründung, ungeteilt und untestbar, im Kontext `ynab-sync`.

**Gewählt: Route 3.** ADR 0020 geht von `scope: global` auf `scope: ledger`. Sie
beschreibt jetzt eine Technik, die das Kassenbuch anwendet, und erhebt keinen Anspruch auf
das Repo mehr. Damit entfällt die Compliance-Erzählung, entfällt der vorgetäuschte Grund
gegen eine Mechanisierung (der echte Grund ist der enge Geltungsbereich: eine Prüfung über
einen einzigen Auslöser prüft nichts, was seine 12 Tests nicht schon prüfen), und entfällt
`ledger-w2m4t` ersatzlos: ohne repo-weiten Anspruch gibt es keinen Wiederaufnahme-Faden
für einen FCS-Linter. Die nachgezählten Zahlen und die zehn abweichenden Nachrichten
stehen trotzdem in der ADR, im Abschnitt "Der Bestand im übrigen Repo", weil sie der Grund
für die Einengung sind. `ConfirmTan` ist dort namentlich genannt.

Was die ADR jetzt **nicht** mehr behauptet: dass `Rules`, `Settings`, `SyncFlow` oder
künftige Komponenten anderer Kontexte ihr folgen; dass das Repo repo-weit konform ist;
dass eine mechanische Prüfung unbaubar wäre. Was sie zusätzlich einräumt: die zehn
ungeteilten Nachlassstellen bleiben untestbar und diese ADR ändert daran nichts, und der
`None`-Zweig von `LoadEntries` (`Ledger/State.fs:68`) ist entgegen meiner Iteration-2-
Begründung sehr wohl testbar, ist also kein Argument für die Ausnahme, sondern eine kleine
offene Test-Lücke, die ich benenne statt sie als Rechtfertigung zu benutzen.

**Keine `.fs`-Änderung in dieser Iteration.** Route 3 ist reine Berichtskorrektur; Seite,
Tests, README-Delta und alles bis Prüfpunkt 6b bleiben byte-identisch. Die Gate-Verdikte
stehen deshalb unverändert bei denen aus Iteration 1, vom Verifier in Iteration 2 und 3
unabhängig nachgefahren: `dotnet build` grün, `dotnet test` grün bei 709 Tests,
`npm run build` grün.

`ConfirmTan`s untestbare Doppeldruck-Sperre habe ich bewusst **nicht** als Backlog-Item
geschnitten: sie gehört in den Kontext `ynab-sync`, und meine Items landen im Backlog des
Kassenbuchs. Sie steht hier und in der ADR als Beobachtung, damit sie nicht verloren geht.
