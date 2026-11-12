---
id: ledger-v6s7c
title: "Durchgehen Karte für Karte unter der Übersicht: ungeprüfte Buchungen bestätigen mit Kategorie, Vorschlägen, Bestell-Link, Memo, Lena und Wischen, Duplikat-Karte für wartende Buchungen, Zu tun und „Abrufen“ führen dorthin"
status: done
type: feature
context: ledger
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, ledger-s1weh, banking-import-d0fey, ledger-r7h2w, ledger-fxt09]
blocks: []
tags: [etappe-3, frontend, kassenbuch, uebersicht]
related_adrs: [0050, 0055, 0056, 0062, 0086, 0087, 0089, 0093]
related_research: []
prior_art: []
---

## Why
Nach `banking-import-d0fey` stehen neue Buchungen sofort als „zu prüfen“ im Kassenbuch. Durchgesehen werden sie bis
jetzt über das Kassenbuch mit Filter. Der Prototyp sieht dafür eine eigene Ansicht vor: Karte für Karte, eine
Entscheidung je Karte, mit Wischen am Handy und Tasten am PC (ADR 0086, Styleguide §7.4 „Durchgehen“). Wartende
Duplikate (ADR 0093) brauchen hier ihre Karte, sonst gehen sie nie nach YNAB.

## What
Vorlage: Prototyp Version 17 (https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg), Funktion `review()` (Karte,
Duplikat-Karte, Endansicht), Aktionen `startReview`, `rvOk`, `rvSkip`, `rvLena`, `rvCat`, `dupNew`, `dupDrop`,
`orderLink`, `pickCat` mit `rv`, `catPickerBody` (Vorgeschlagen, Konten), `renderDesk` (Spalte `.drev`,
Tastenleiste), keydown-Handler (Enter, S, K, L, Esc), touchend (`#rcard`), `todos()`/`todoList()`. Screenshots:
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/p-durchgehen-karte-handy-412.png`,
`…/p-durchgehen-karte-pc-1280.png`, `…/p-durchgehen-amazon-2-handy-412.png`, `…/p-durchgehen-amazon-2-pc-1280.png`,
`…/p-durchgehen-amazon-3-handy-412.png`, `…/p-durchgehen-amazon-3-pc-1280.png`, `…/p-durchgehen-amazon-9-handy-412.png`,
`…/p-durchgehen-amazon-9-pc-1280.png`, `…/p-durchgehen-duplikat-handy-412.png`, `…/p-durchgehen-duplikat-pc-1280.png`,
`…/p-durchgehen-ende-handy-412.png`, `…/p-durchgehen-ende-pc-1280.png`, `…/p-abrufen-fertig-handy-412.png`,
`…/p-abrufen-fertig-pc-1280.png` (`…` = `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3`).

**Warteschlange**
- Alle ungeprüften, nicht gelöschten Buchungen über alle Konten (dieselbe Menge wie `NeedsReviewCount`, Zu-tun-Eintrag
  1 und der Zähler an der Übersicht), jede Herkunft: Comdirect-Import, Regel, YNAB-Spiegel, Quick Add, Bob. Grund:
  Zähler und Inhalt stimmen überein; der Prototyp nimmt ebenso alle ungeprüften (`toReview`, Karte mit Herkunft
  „YNAB-Spiegel“).
- Reihenfolge: zuerst wartende Duplikate (sie halten YNAB auf), danach nach Buchungsdatum, älteste zuerst, bei
  Gleichstand die kleinere EntryId zuerst (Roman, 2026-11-11: älteste zuerst). Eine reine Funktion
  ordnet, eine neue Leseabfrage `LedgerApi.getReviewQueue` liefert die Buchungen (`LedgerEntryView`) in dieser Folge.
- Die Schlange steht beim Öffnen fest (Liste von Ids, „i von n“ springt nicht). Eine Buchung, die inzwischen geprüft
  oder gelöscht ist, wird beim Erreichen übersprungen. „Später“ lässt sie ungeprüft und geht weiter; sie kommt beim
  nächsten Öffnen wieder.

**Ansicht** (eigene Seite unter dem Ort Übersicht, die Navigation markiert Übersicht, Styleguide §7.4)
- Kopf (`PageHeader.top`): „Zurück“ (zur Übersicht), Titel „Durchgehen“, rechts „<i> von <n>“ in Mono, klein,
  gedämpft. Darunter ein Fortschrittsbalken 4 px, Spur `border`, Füllung im Akzent, Breite (i−1)/n. Gibt es keinen
  DS-Baustein, der gleich aussieht, eine neue DS-Variante auf `/styleguide`.
- Karte (`Card`, Innenabstand und Abstände wie `.rcard`), von oben:
  1. „<Tag> · <Konto> · <Herkunft>“ klein, gedämpft (Tag wie in der Kassenbuch-Liste: Heute, Gestern, Datum;
     Herkunft aus `Shared.Domain.originLabel`).
  2. Payee groß (halbfett, Schriftgröße nach §7.14). Ein Tipp öffnet die Payee-Auswahl aus `ledger-fxt09`.
  3. Betrag groß in Mono (halbfett, `Money`, Schriftgröße nach §7.14).
  4. Feld „Kategorie“ (`Input.pickerField`, Pfeil rechts), Wert oder gedämpft-gelb „Kategorie wählen“; bei einer
     aufgeteilten Buchung „Aufgeteilt“ ohne Pfeil, bei einem Übertrag „→ <Konto>“. Öffnet die Kategorieauswahl wie im
     Detail (`CategoryPicker`, Props aus `View.detailPickerProps`): Suche, „Vorgeschlagen“, Gruppen, Abschnitt
     „Übertrag auf Konto“ mit den Konten (`transferTargetsFor`). Am Handy `CategoryPicker.sheet` (kein gestapeltes
     Sheet, die Karte ist kein Sheet), am PC das Popover mit Fokus in der Suche (§7.9). Eine Kategorie geht über
     `setEntryCategory`, ein Konto über den Übertrag (`ledger-qvjvs`). Toast „Kategorie: <Name> · <verfügbar>
     verfügbar“ bzw. „Übertrag nach <Konto> · Gegenbuchung angelegt“, je mit „Rückgängig“.
  5. Unter dem Feld, wenn die aktuelle Kategorie per Order-ID-Abgleich gesetzt wurde (jüngste Kategorisierung ist
     `EntryRecategorized` mit Actor `OrderIdMatch`): „aus Bestellnummer“ klein, gedämpft. Braucht ein Feld in
     `LedgerEntryView` (Fixtures nachziehen).
  6. „Vorgeschlagen“ (klein, gedämpft) mit Chips (`BottomSheet.chipButtonTouch` am Handy) aus
     `getPayeeCategorySuggestions` (bis drei, häufigste zuerst, ohne die aktuelle). Ein Tipp setzt die Kategorie wie
     in 4. Ohne Vorschlag fehlt der Block.
  7. Bestell-Link: bei Amazon mit Bestellnummer die Zeile „Bestellung bei Amazon“, darunter die Nummer in Mono, rechts
     „öffnen“ im Akzent mit `Icons.externalLink` (Prototyp `orderLink`, Anker in neuem Tab). Die übrigen Links aus
     `LedgerEntryView.ExternalLinks` (Amazon ohne Nummer „Amazon Orders“, „PayPal Activity“) in derselben Zeilenform
     mit ihrer Beschriftung.
  8. Zeile „Memo“ (Schlüssel links, Wert oder gedämpft „hinzufügen“ rechts mit Pfeil). Öffnet am Handy ein Sheet, am
     PC einen Dialog „Memo“ mit Feld und „Speichern“ (`setEntryMemo`), Toast „Memo gespeichert“ bzw. „Memo entfernt“
     mit „Rückgängig“.
  9. Bei Rückfrage an Lena ein Hinweis mit dem gefüllten Lena-Flag (`Icons.flagSolid`): „<Flag> ist gefragt. Die
     Buchung bleibt offen, bis Lena oder du sie prüft.“
  10. Link „Details“ (klein, im Akzent, rechts unten in der Karte): öffnet das Buchungs-Detail des Kassenbuchs, am
      Handy als Sheet, am PC als Panel rechts (§7.6). Dort Aufteilen samt Barabhebung, Betrag, Löschen, Historie.
      Nach dem Schließen lädt die Karte die Buchung neu.
- Unter der Karte zwei gleich breite neutrale Knöpfe: „Lena fragen“ mit Flag (bzw. „Rückfrage zurück“) und „Später“;
  darunter der Hauptknopf über die volle Breite, mindestens 54 px hoch: „Passt, weiter“, ohne Ziel deaktiviert mit
  „Erst Kategorie wählen“. Ein Ziel ist eine Kategorie, ein Übertrag oder eine Aufteilung (Sinn von „Ohne Kategorie“,
  ADR 0048).
- Darunter die Hinweiszeile 12 px gedämpft, zentriert: „Nach rechts wischen heißt „Passt“, nach links „Später“.“ (nur
  unter 1024 px).
- „Passt, weiter“ setzt den Prüfvermerk über `setReviewMark Reviewed` (ADR 0050, Push nach YNAB) und zeigt die
  nächste Karte, ohne Toast (Prototyp). Scheitert der Aufruf, bleibt die Karte und ein Toast nennt den Grund.
- „Lena fragen“ setzt die Rückfrage (`setLenaFlag AssignLena`) und geht weiter, Toast „Lena gefragt“ mit „Rückgängig“
  (nimmt sie zurück und zeigt die Karte wieder). „Rückfrage zurück“ nimmt sie zurück und bleibt auf der Karte.
- Wischen am Handy: mehr als 90 px nach rechts ist „Passt“ (ohne Ziel: Toast „Erst eine Kategorie wählen“, die Karte
  bleibt), mehr als 90 px nach links ist „Später“. Während des Wischens folgt die Karte dem Finger und wird blasser
  (Prototyp `touchmove`). Die Schwellen-Entscheidung ist eine reine Funktion.
- PC (ab 1024 px): die Ansicht steht als zentrierte Spalte (höchstens 620 px), unten eine Tastenleiste über die Breite
  der Spalte mit `kbd`-Kästchen: „Enter passt“, „S später“, „K Kategorie“, „L Lena“, „Esc zurück“. Tasten nur ohne
  Fokus in einem Feld und ohne offenen Dialog (§7.8 Punkt 6): Enter = Passt (nur mit Ziel, nicht auf der
  Duplikat-Karte), S = Später, K = Kategorieauswahl öffnen (nicht auf der Duplikat-Karte), L = Lena fragen bzw.
  zurück (nicht auf der Duplikat-Karte), Esc = zurück zur Übersicht (ist ein Picker oder Dialog offen, schließt Esc
  nur ihn). Die Zuordnung Taste zu Aktion ist eine reine Funktion (Vorbild `pageKeyAction`).

**Duplikat-Karte** (wartende Buchung nach ADR 0093)
- Karte mit gelbem Rand („braucht dich“): oben klein in Gelb „Möglicherweise doppelt · noch nicht in YNAB“, Payee
  groß, Betrag groß in Mono, „<Tag> · <Konto> · <Herkunft>“, Abschnittskopf „Schon im Kassenbuch“, darin die Zeile der
  vorhandenen Buchung auf erhöhtem Grund (Payee, darunter „<Tag> · <Konto> · <Kategorie>“, rechts der Betrag): die
  aufgelöste Kassenbuch-Buchung, ohne sie der Schnappschuss aus `EntryHeldAsPossibleDuplicate`. Darunter klein,
  gedämpft die Begründung aus dem Ereignis (Prototyp: „Gleicher Betrag, gleicher Tag, gleicher Payee, aber eine andere
  Bank-Referenz.“).
- Zwei gleich breite Knöpfe: „Ist neu“ (neutral) und primär „Ist doppelt, verwerfen“. Kein Wischen, keine Hinweiszeile.
- „Ist neu“ ruft `confirmEntryIsNew` (`ledger-r7h2w`), Toast „Als neu übernommen, geht nach YNAB“; dieselbe Stelle zeigt
  danach die normale Karte der Buchung zum Kategorisieren und Prüfen (Prototyp `dupNew`). Scheitert der Push, nennt der
  Toast den Grund, die normale Karte erscheint trotzdem.
- „Ist doppelt, verwerfen“ ruft `discardDuplicateEntry`, geht weiter, Toast „Verworfen, nicht nach YNAB gegangen“ mit
  „Rückgängig“ (`restoreDiscardedDuplicate`, die Duplikat-Karte steht wieder da).

**Endansicht**: Fortschritt voll, leerer Zustand „Alles geprüft“ in Grün, „<n> Buchungen durchgegangen. Neue
erscheinen nach dem nächsten Import.“ (Einzahl „1 Buchung“), primär „Zur Übersicht“. Ist die Schlange schon beim
Öffnen leer, steht dieselbe Ansicht mit „Neue erscheinen nach dem nächsten Import.“ allein.

**Zugänge**
- Zu-tun-Eintrag 1 auf der Übersicht nach dem Prototyp: „<n> neue Buchungen durchgehen“ (Einzahl „1 neue Buchung
  durchgehen“), Zeile 2 „Import <Stand>“ (bzw. „Im Kassenbuch“, Abweichung (b) aus `ledger-s1weh`), Knopf „Jetzt
  durchgehen“, Ziel Durchgehen (bisher „Buchungen zu prüfen“, „Prüfen“, Kassenbuch mit Filter; Übergang aus
  `ledger-s1weh`).
- „Jetzt durchgehen“ im Sheet „Abrufen“ führt ins Durchgehen (bisher Kassenbuch mit Filter, Übergang aus
  `banking-import-d0fey`).
- Nach dem Durchgehen laden Zu tun und Zähler neu.

## Acceptance criteria

### Logik (Client-`update` unter .NET, reine Funktionen, Handler mit In-Memory-SQLite)
- [ ] Reihenfolge der Schlange: wartende zuerst, dann Datum aufsteigend, Gleichstand EntryId (reine Funktion, Fälle mit
      gleichem Datum, mit wartenden, ohne Buchung); `getReviewQueue` liefert genau die Menge von `NeedsReviewCount` ohne
      gelöschte (Handler-Test mit Buchungen jeder Herkunft).
- [ ] Zustandsfolge der Ansicht durch das echte `update` nach Doktrin Punkt 20 (Passt, Später, Lena mit Rückgängig,
      Verwerfen mit Rückgängig, Ist neu, Kategorie, Ende, Antwort kommt nach dem Weitergehen, Fehler beim Prüfvermerk),
      mit Orakel unabhängig vom Code.
- [ ] „Passt“ ist ohne Ziel gesperrt und mit Kategorie, Übertrag oder Aufteilung frei (reine Funktion).
- [ ] Wischschwelle und Tastenzuordnung als reine Funktionen, je Taste und Kartenart ein Fall.
- [ ] „aus Bestellnummer“ erscheint nur bei Kategorie aus `OrderIdMatch`, verschwindet nach einer Änderung durch Roman
      (Lesepfad-Test).
- [ ] Zu-tun-Eintrag 1 trägt Text, Einzahl und Knopf des Prototyps und führt ins Durchgehen (`OverviewTodoTests.fs`
      angepasst), „Jetzt durchgehen“ im Sheet „Abrufen“ ebenso.
- [ ] Neue Pflichtfelder in `LedgerEntryView` sind in allen Fixtures unter `scripts/` nachgezogen.
- [ ] `src/Tests/RemotingEndpointNegativeTests.fs` hat die Zeile für `getReviewQueue`.

### Browser und Vergleich
- [ ] Browser-Tests beim Umbau (Fahrplan Etappe 3): Die Übersicht ändert Eintrag 1, ihr `[flow:uebersicht]` in
      `scripts/uebersicht.test.mjs` wird gegen das neue Ziel neu geschrieben (Satz in `FLOWS` angepasst). Neu: Flow
      `durchgehen` in `FLOWS` („Am Handy über Jetzt durchgehen eine Kategorie wählen, per Wisch bestätigen, ein
      Duplikat verwerfen und bei Alles geprüft ankommen.“) mit genau einem `[flow:durchgehen]`-Test bei 412 px, der
      den Wisch per Touch auslöst; ein `[tastatur]`-Test am PC für Enter, S, K, L und Esc (Esc schließt erst den
      Picker, dann die Ansicht); ein `[fable]`-Test für Datum und Betrag des Treffers auf der Duplikat-Karte (die
      Aussage aus dem in d0fey entfernten `scripts/import-pruefen-duplikat-details.test.mjs`). Der Sweep bekommt die
      Zustände Karte, Karte mit Bestell-Link, Vorschlägen und Lena-Hinweis, Duplikat-Karte und Endansicht bei 320, 412
      und 1280 px; geprüft wird dabei auch, dass „Passt, weiter“ ohne Scrollen erreichbar ist.
- [ ] Eingaben mit Touch bei 412 px: in der Kategorieauswahl und im Memo-Sheet schließt der Wechsel zwischen Suche und
      Liste bzw. das Speichern die Tastatur nicht vor dem Ende; die Karte hat keine Betragsfelder.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten (Notes). Screenshots von BB bei 412 und 1280 px
      (Karte, Karte mit Amazon, Duplikat-Karte, Endansicht, Zu tun) neben den Prototyp-Screenshots oben im Outcome.
- [ ] Werte aus dem Styleguide, nichts erfunden; DS-Bausteine nur, wo sie gleich aussehen, sonst neue Varianten auf
      `/styleguide` (Fortschrittsbalken, Tastenleiste mit `kbd`, Karte des Durchgehens, gelber Kartenrand, falls
      nicht vorhanden).
- [ ] README ledger: Begriffe **Durchgehen**, **Warteschlange (Durchgehen)**, **Duplikat-Karte**; **Zu tun
      (Übersicht)** auf Eintrag 1 neu und ohne den Übergang. Delta durch `npm run check:readme-delta` und die
      grep-Prüfung aus CLAUDE.md.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.
- [ ] Roman hat das Durchgehen am Handy (412 px, Touch, Wischen) und am PC (Tasten) neben dem Prototyp angesehen.
      [human-eye]

## Notes
Schnitt 5 aus Etappe 3 (Fahrplan, Roman 2026-11-11). Hängt an `banking-import-d0fey` (wartende Buchungen),
`ledger-r7h2w` (Ist neu, Verwerfen) und `ledger-fxt09` (Payee ändern).

**Abweichungen vom Prototyp Version 17**
- (a) Link „Details“ auf der Karte. Grund: Aufteilen samt Barabhebung bleibt nach Romans Entscheidung (2026-11-11);
  das Detail kann es schon, eine zweite Aufteilung auf der Karte wäre doppelt.
- (b) Payee auf der Karte antippbar (Payee-Auswahl aus `ledger-fxt09`). Grund: Payee ändern bleibt (Entscheidung 3).
  Er sieht aus wie im Prototyp.
- (c) „aus Bestellnummer“ unter der Kategorie. Grund: Der Order-ID-Vorschlag ist gesetzt, nicht vorgeschlagen, und soll
  erkennbar bleiben (Entscheidung 3).
- (d) Weitere Links (Amazon ohne Nummer, PayPal) in der Form des Bestell-Links. Grund: BB zeigt sie heute im Detail.
- (e) Herkunft mit BBs Bezeichnungen (`originLabel`: „Comdirect-Import“, „Regel“, „YNAB-Spiegel“) statt „Import“.
  Grund: dieselben Wörter wie im Detail.
- (f) Reihenfolge der Schlange: Duplikate zuerst, dann nach Datum. Der Prototyp nimmt die Folge des Imports.
- (g) L wirkt auf der Duplikat-Karte nicht (Prototyp: ja). Grund: eine wartende Buchung ist für Lena gesperrt
  (ADR 0093 Punkt 6).
- (h) Die Karte hält §7.14 (höchstens drei Schriftgrößen je Ansicht) statt der fünf Größen des Prototyps (11/12,
  15/16, 20, 30 px). Grund: Roman, 2026-11-11, wie auf der Übersicht. Welche Elemente welche der drei Größen tragen,
  legt der Worker nach §7.14 fest und nennt es im Outcome; Payee und Betrag bleiben die größten.

**Bewusste Lücke:** Eine Regel aus der Buchung anlegen fehlt, bis `categorization-n3sxw` (Schnitt 6) sie ins
Kassenbuch bringt. Heute geht sie im Sync-Ablauf, der mit `banking-import-d0fey` entfällt.

Am PC zeigt der Prototyp im Kassenbuch-Kopf zusätzlich „<n> durchgehen“; das gehört zum Umbau des Kassenbuchs
(`ledger-f8d7x`, Schnitt 8).

Der Conductor legt vor dem Start Prototyp und Screenshots in den Scratchpad (Pfade oben) und gibt sie Worker,
Verifier und `design-quality` mit.

## Verifier note (iteration 1)
**REASONS:** Kriterium „DS-Bausteine nur, wo sie gleich aussehen, sonst neue Varianten auf /styleguide“ nicht erfüllt: Die neue Größe `Button.Choice` (src/Client/DesignSystem/Button.fs:26 und :90), genutzt in Ledger/View.fs:4650, :4662, :4672, steht weder auf /styleguide (Abschnitt 29 zeigt nur `ReviewCard.*`) noch in styleguide.md §5; der Outcome behauptet beides. Dazu: Abschnitt 29 steht zwischen dem Doc-Kommentar zu `back` und `let view` (Styleguide/View.fs um Zeile 2529). Alles andere bestanden (verify PASS, Fixtures, Negativtabelle, FLOWS, Lesepfad, Delta-Gate).
**SUGGESTED_FIX:** Einen Knopf mit `Button.Choice` auf /styleguide zeigen (etwa das Paar „Ist neu“ / „Ist doppelt, verwerfen“ in Abschnitt 29) und `Choice` in der Zeile **Button** von §5 beschreiben (zwei gleich breite Entscheidungen, 16/14 px, Höhe setzt der Aufrufer). Doc-Kommentar zu `back` wieder direkt über `let view`.
**ITERATION_HINT:** likely-fixable

## Outcome

Durchgehen Karte für Karte steht unter der Übersicht (Route `#/review`, Navigation markiert Übersicht, am Handy ohne Leiste unten). Zu-tun-Eintrag 1 („<n> neue Buchungen durchgehen“, Einzahl „1 neue Buchung durchgehen“, Knopf „Jetzt durchgehen“) und „Jetzt durchgehen“ im Sheet „Abrufen“ führen dorthin. Der Übergang über das Kassenbuch mit Filter ist entfernt.

**Server und Lesepfad**
- `LedgerApi.getReviewQueue` (`src/Shared/Api.fs`, `src/Server/Api.fs`, `Ledger.QueryHandlers.getReviewQueue`, `Ledger.Persistence.loadNeedsReviewEntries`): alle ungeprüften, nicht gelöschten Buchungen über alle Konten, geordnet von der reinen Funktion `Shared.Domain.reviewQueueOrder` (wartende zuerst, dann Datum aufsteigend, Gleichstand EntryId als Text).
- Neues Pflichtfeld `LedgerEntryView.CategoryFromOrderId` („aus Bestellnummer“): `Ledger.Persistence.loadOrderIdCategorizedEntries` sucht `EntryRecategorized` mit Actor `OrderIdMatch`, nach dem kein weiteres Kategorisierungs-Ereignis kam; gesetzt in Seite, Einzelabfrage, Konto-Abfrage und Schlange (`ReadModel.withOrderIdCategory`). Fixtures in `scripts/ledger-filter-bar.mjs` und `scripts/ledger-status-column.mjs` nachgezogen.

**Client**
- Zustand in `Components.Ledger.Types.ReviewSession` (`Model.Review`); die Karte ist die Buchung in `DetailFor`, so laufen Kategorie, Übertrag, Payee, Memo, Lena und das Detail („Details“) über die vorhandenen Befehle. Neue Nachrichten und Logik in `src/Client/Components/Ledger/State.fs` (`showReviewCardAt`, `advanceReview`, `returnToReviewCard`, Abschnitt „Durchgehen“), reine Funktionen in `Types.fs` (`reviewHasTarget`, `reviewSwipeOf`, `reviewKeyAction`, `reviewSkipsEntry`, Texte) und `View.fs` (`reviewKeyFor`, `reviewCategoryValue`, `reviewLinks`, `reviewMatchRow`, `ReviewPage`).
- Toasts der Karte: „Kategorie: <Name> · <verfügbar> verfügbar“, „Übertrag nach <Konto> · Gegenbuchung angelegt“, „Memo gespeichert“/„Memo entfernt“, „Lena gefragt“ mit Rückgängig (holt die Karte zurück), „Als neu übernommen, geht nach YNAB“, „Verworfen, nicht nach YNAB gegangen“ mit Rückgängig (`ToastActionKind.UndoDiscardDuplicate` → `restoreDiscardedDuplicate`, die Duplikat-Karte steht wieder da). „Passt, weiter“ ohne Toast, beim Fehler bleibt die Karte.
- Neuer DS-Baustein `src/Client/DesignSystem/ReviewCard.fs` (Fortschritt, Karte normal und mit gelbem Rand, Feld „Kategorie“ mit gelbem Platzhalter, Link-, Memo-, Hinweis- und Trefferzeile, „Details“, Tastenleiste mit `kbd`), neue Button-Größe `Button.Choice` (zwei gleich breite Entscheidungen, 16/14 px, `px-3`, kein Umbruch, Höhe setzt der Aufrufer). `ReviewCard` steht auf `/styleguide` (Abschnitt 29) und in `standards/frontend/styleguide.md` §5 (Zeile **ReviewCard**); `Button.Choice` erst seit Iteration 2 (siehe unten).
- Schriftgrößen nach §7.14 (Abweichung (h)): 20 px für Seitentitel, Payee und Betrag; 16 px am Handy bzw. 14 px am PC für Feldwerte, Memo, „öffnen“ und alle Knöpfe; 12 px für Meta-Zeile, „aus Bestellnummer“, „Vorgeschlagen“, Hinweise, „Details“, Abschnittskopf, „i von n“ und Tastenleiste.

**Tests**
- `src/Tests/LedgerReviewQueueTests.fs`: Reihenfolge (rein) und `getReviewQueue` gegen In-Memory-SQLite mit jeder Herkunft, gleiche Zahl wie `countNeedsReviewEntries`; „aus Bestellnummer“ im Lesepfad, weg nach Romans Änderung.
- `src/Tests/LedgerReviewClientTests.fs`: reine Funktionen (Ziel, Wischschwelle, Tasten je Kartenart, Texte, Feld, Links, Trefferzeile), Aufzählung aller Reihenfolgen bis Tiefe 6 durch das echte `update` mit eigenem Orakel (Passt mit später Antwort und Fehler, Später, Lena mit Rückgängig, Kategorie, Verwerfen mit Rückgängig, Ist neu, Ende; Gegenprobe: das Weitergehen ohne Prüfung der Karte bei der Passt-Antwort macht den Test rot), Einzelfälle (Überspringen beim Neulesen, veraltete Antwort, Fehler beim Prüfvermerk, Wisch ohne Ziel, Toasts, Details, Esc-Kette, Verlassen, Endansicht).
- Angepasst: `OverviewTodoTests.fs` (Text, Einzahl, Knopf, Ziel Durchgehen, „Jetzt durchgehen“ aus dem Sheet), `FetchSheetTests.fs`, `RemotingEndpointNegativeTests.fs` (Zeile `getReviewQueue`), `RetryTests.fs`, `RoutingTests.fs`.
- Browser: `scripts/durchgehen.test.mjs` mit `[flow:durchgehen]` (412 px, Wisch über CDP-Touch), `[tastatur]` (Enter, K, Esc schließt erst den Picker, L, S, Esc zur Übersicht) und `[fable]` (Datum und Betrag des Treffers). `[flow:uebersicht]` in `scripts/uebersicht.test.mjs` gegen das neue Ziel, `FLOWS` in `scripts/browser-usage.mjs` angepasst und um `durchgehen` ergänzt. Sweep-Zustände `durchgehen-karte`, `durchgehen-amazon` (Bestell-Link, Vorschläge, Lena-Hinweis, „aus Bestellnummer“), `durchgehen-duplikat`, `durchgehen-ende` bei 320, 412 und 1280 px mit Hauptknopf ohne Scrollen, dazu `durchgehen-memo` (412 px, „Speichern“ bei offener Tastatur im Bild) in `scripts/layout-states.mjs`; `/review` in `KNOWN_ROUTES`.
- `./verify.sh` (Iteration 1): VERIFY: PASS (dotnet 4707 grün, node 316 grün).

**Vergleich mit der Skizze** (Prototyp Version 17): BB-Screenshots unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/ledger-v6s7c/` (`bb-durchgehen-karte-*`, `bb-durchgehen-amazon-*`, `bb-durchgehen-duplikat-*`, `bb-durchgehen-ende-*`, `bb-durchgehen-zu-tun-*`, je `handy-412` und `pc-1280`), neben `p-durchgehen-karte-*`, `p-durchgehen-amazon-3-*`, `p-durchgehen-duplikat-*`, `p-durchgehen-ende-*`, `p-uebersicht-angepinnt-*` im Ordner darüber. Abweichungen nur die genannten (a) bis (h). Zu (h): Betrag 20 px statt 30 px. Die Eingabe mit Touch (Wechsel zwischen Suche und Liste im Kategorie-Sheet, Speichern im Memo-Sheet ohne vorzeitig geschlossene Tastatur) läuft über die vorhandenen Bausteine `CategoryPicker.sheet` und `Detail.memoEditor`; der Sweep-Zustand `durchgehen-memo` prüft „Speichern“ bei offener Tastatur. Ein Blick mit echtem Touch steht mit dem Kriterium [human-eye] noch aus.

**Hinweis für den Conductor:** `.agentheim/knowledge/contexts/banking-import/README.md` (Bullet **Abrufen**) sagt noch „„Jetzt durchgehen“ führt bis ledger-v6s7c ins Kassenbuch über alle Konten mit Filter „Zu prüfen“.“ Das gilt nicht mehr (fremder BC, deshalb nicht im Delta). Im ledger-README nennt *WartendeVerwerfen* (Key commands, Bullet ohne fetten Kopf) „Eine Oberfläche dafür baut ledger-v6s7c (Duplikat-Karte)“; das ist jetzt erledigt und kann von Hand nachgezogen werden.

**Iteration 2** (Verifier-Note Iteration 1): `Button.Choice` steht jetzt auf `/styleguide` (Abschnitt 29, Paar „Ist neu“ / „Ist doppelt, verwerfen“ mit Hinweis zu Größe und Höhe, `src/Client/Components/Styleguide/View.fs`) und in `standards/frontend/styleguide.md` §5 in der Zeile **Button** (`ButtonSize.Choice`: zwei gleich breite Entscheidungen, 16 px am Handy, 14 px am PC, `px-3`, kein Umbruch, Höhe setzt der Aufrufer, 52 px, der Hauptknopf 54 px). Die Behauptung aus Iteration 1, `Button.Choice` stehe schon dort, war falsch. Abschnitt 29 steht wieder vor dem Kopf „Seite“, der Doc-Kommentar zu `back` direkt über `let view`. Stand mit `main` (banking-import-t4raw) gemergt; `./verify.sh`: VERIFY: PASS (dotnet 4740 grün, node 316 grün).

**Für die Kann-Sammlung**
- `ReviewCard.field` wiederholt die Bauart von `Input.pickerField` nur wegen des gelben Platzhalters und des fehlenden Pfeils; eine Variante von `pickerField` wäre sauberer.
- Am PC landet der Fokus nach Esc im Kategorie-Popover auf dem Feld; ein Enter danach öffnet den Picker erneut, statt „Passt“ auszulösen.
- Eine sehr lange Karte (langer Payee, mehrere Links, Lena-Hinweis) kann bei 320 px „Passt, weiter“ unter den Rand schieben; der Sweep prüft die üblichen Fälle.
