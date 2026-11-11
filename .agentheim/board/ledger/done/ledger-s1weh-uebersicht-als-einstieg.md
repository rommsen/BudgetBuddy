---
id: ledger-s1weh
title: "Übersicht als Einstieg (Variante A): Zu tun mit einer Hauptaktion, Zähler an der Übersicht, „+ Buchung“, am PC zweispaltig"
status: done
type: feature
context: ledger
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, design-system-k6kfw]
blocks: [budgeting-w9yt4, ledger-v6s7c]
tags: [etappe-3, frontend, uebersicht]
related_adrs: [0056, 0086, 0087]
related_research: []
prior_art: []
---

## Why
Die Übersicht beantwortet zwei Fragen: wo stehe ich, was steht an (ADR 0086). `design-system-k6kfw` bringt
die Übersicht mit Kopf, Monat und Konten. Es fehlt, was ansteht: heute muss Roman dafür Sync, Kassenbuch
und Budget einzeln aufsuchen.

## What
Die Übersicht bekommt „Zu tun“ als geordnete Liste mit genau einem Hauptknopf (Styleguide §7.4, §7.14),
einen Zähler an der Übersicht in Rail und Seitenleiste und „+ Buchung“. Am PC wird sie zweispaltig.

Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg Version 17, Funktionen `todos`,
`todoList`, `home` (Variante A), `homeDesk`, `sidebar` (Zähler) und die Klasse `.fab`.

Übergang (Roman, 2026-11-11): Bis `banking-import-d0fey` (Import direkt) und `ledger-v6s7c` (Durchgehen)
stehen, führen die Einträge zu neuen Umsätzen in den heutigen Sync-Ablauf und ins Kassenbuch.

## Acceptance criteria

### Zu tun
- [ ] Abschnitt „ZU TUN“ als erster Block der Übersicht (Kopfzeile in Mono, Großbuchstaben, gedämpft),
      darin eine Karte mit den Einträgen in dieser Reihenfolge, jeder nur, wenn seine Bedingung gilt:
      1. „<n> Buchungen zu prüfen“, Zeile 2 „Import <Stand>“; Ziel: Kassenbuch mit Filter „Zu prüfen“.
         Bedingung: das Kassenbuch hat ungeprüfte Buchungen.
      2. „<Kategorie> ist im Minus“, Zeile 2 „<verfügbar> · Geld aus einer anderen Kategorie holen“;
         Ziel: das Detail dieser Kategorie im Budget, Bereich Geld, Holen, Betrag vorbelegt mit dem Minus.
         Je Kategorie im Minus ein Eintrag.
      3. „Targets anwenden“, Zeile 2 „<Summe> für <n> Kategorien“ (eine: „1 Kategorie“); Ziel: der Dialog
         Targets anwenden im Budget. Bedingung: im laufenden Monat fehlt bei mindestens einer Kategorie
         Geld zum Target.
      4. „<n> Rückfragen an Lena offen“, Zeile 2 „Lena sieht sie über den Filter“; Ziel: Kassenbuch mit
         Filter Lena. Bedingung: mindestens eine Buchung trägt das Lena-Flag (ADR 0056).
      5. „Buchung nicht in YNAB“, Zeile 2 „<Payee> · <Betrag> · erneut senden“; Ziel: diese Buchung im
         Kassenbuch. Je Buchung, deren Push gescheitert ist, ein Eintrag.
      6. „<Betrag> verteilen“, Zeile 2 „Noch keiner Kategorie zugewiesen“; Ziel: Budget. Bedingung: Zu
         verteilen ist größer als null und kein Eintrag 3 steht da.
- [ ] Der erste Eintrag steht groß: Text 16 px halbfett, darunter Zeile 2, darunter ein gefüllter Knopf
      im Akzent über die volle Breite. Beschriftung je Ziel: „Prüfen“ (1), „Geld holen“ (2), „Anwenden“
      (3), „Öffnen“ (4, 5, 6). Das ist die eine Hauptaktion der Ansicht (§7.14).
- [ ] Jeder weitere Eintrag ist eine Zeile mit Pfeil rechts; ein Klick führt zum selben Ziel.
- [ ] Vor jedem Eintrag eine Marke von 9 px: „braucht dich“ (Gelb) für 1, 3 und 6, Rot für 2, das
      Lena-Flag in der Flag-Farbe aus den Einstellungen für 4. Eintrag 5 trägt Gelb, nicht Rot wie im
      Prototyp: ein gescheiterter Push bleibt Gelb (Nachtrag ADR 0087, Roman 2026-11-08).
- [ ] Ohne Einträge steht eine Karte mit grünem Punkt, „Alles erledigt“ (halbfett) und „Neue Aufgaben
      erscheinen nach dem nächsten Import.“ (§7.12).
- [ ] Welche Einträge in welcher Reihenfolge erscheinen, berechnet eine reine Funktion aus Budgetmonat,
      Kassenbuch-Zählern und Lena-Zahl. Expecto-Test je Bedingung, für die Reihenfolge, für „1 Kategorie“
      gegenüber „<n> Kategorien“ und dafür, dass 6 bei offenem 3 fehlt.

### Zähler
- [ ] In Rail und Seitenleiste trägt die Übersicht die Zahl der ungeprüften Buchungen (Mono 11 px,
      „braucht dich“), bei null nichts. Die Leiste am Handy trägt keinen Zähler, wie im Prototyp.

### + Buchung
- [ ] Am Handy unten rechts über der Leiste ein runder Knopf „+ Buchung“ (Höhe 52 px, Symbol 20 px).
      Stehen Einträge in Zu tun, ist er ruhig (Grund `surface-elevated`, Akzentrand und Akzentschrift),
      sonst gefüllt im Akzent. So bleibt es bei einer Hauptaktion je Ansicht.
- [ ] Am PC steht „Buchung“ mit Plus als kleiner Knopf im Kopf neben „Abrufen“.
- [ ] Beide öffnen das Erfassen aus dem Kassenbuch (`ledger-at63w`) mit dessen Vorbelegung. Nach dem
      Speichern bleibt man auf der Übersicht, die Rückmeldung nennt Konto und Betrag (§7.13).

### PC
- [ ] Ab 1024 px zwei Spalten (links rund 1,1, rechts 1 Anteil, Abstand 24 px, höchstens 1240 px breit):
      links „Zu tun“, rechts „Monat“ und „Neueste Buchungen“ aus `design-system-k6kfw`. Unter 1024 px
      einspaltig in der Reihenfolge Zu tun, Monat, Konten.

### Schriftgrößen (Roman, 2026-11-11)
- [ ] Die Übersicht hält Styleguide §7.14 (höchstens drei Schriftgrößen je Ansicht): „noch N Tage“ auf 11 px
      wie die Abschnittsköpfe, der Monatsname in der Größe der Zeilen (`ListRoles.primary`). Heute sind es
      sieben Größen (Review-Lauf zu design-system-k6kfw).

### Tests und Vergleich
- [ ] Ein `[flow:uebersicht]`-Test bei 412 px: mit einer ungeprüften Buchung steht „1 Buchung zu
      prüfen“ als erster Eintrag mit Knopf „Prüfen“, der Knopf öffnet das
      Kassenbuch mit Filter „Zu prüfen“, „+ Buchung“ ist ruhig. Der Flow steht in `FLOWS` in
      `scripts/browser-usage.mjs`.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten (Übergangsziele statt
      Durchgehen, Gelb statt Rot bei Eintrag 5, Angepinnt fehlt bis `budgeting-w9yt4`). Screenshots bei
      412 und 1280 px neben dem Prototyp im Outcome.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.
- [x] Roman hat die Übersicht am Handy (412 px, Touch) und am PC neben dem Prototyp angesehen.
      [human-eye]

## Notes
Schnitt 2 aus Etappe 3 (Fahrplan, Roman 2026-11-11).

Einzahl: bei einer Buchung „1 Buchung zu prüfen“, bei einer Rückfrage „1 Rückfrage an Lena offen“.

Abweichung vom Prototyp, Eintrag 1: Der Prototyp sagt „<n> neue Buchungen durchgehen“ mit Knopf „Jetzt
durchgehen“. Das Durchgehen gibt es erst mit `ledger-v6s7c`; bis dahin „zu prüfen“ und das Kassenbuch mit
Filter. `ledger-v6s7c` stellt den Eintrag auf den Prototyp um. Den Sync-Ablauf erreicht man über „Abrufen“
im Kopf (aus `design-system-k6kfw`).

Heute vorhanden und hier nur verlinkt: Filter „Zu prüfen“ und Lena im Kassenbuch, Hinweis „Noch nicht in
YNAB“ mit „Erneut senden“ (`src/Client/Components/Ledger/State.fs`), Geld holen und Targets anwenden im
Budget (`src/Client/Components/Budget/`). Eine neue API braucht es nicht, falls die Zahl der ungeprüften
Buchungen und der gescheiterten Pushes je Konto schon im Client ankommt; sonst klärt der Worker das mit dem
`tactical-modeler` und meldet es im RESULT.

Zähler (Review-Lauf 2026-11-11): Die Zahl steht in Gelb ohne Wort daneben; §2 verlangt bei Gelb ein Wort.
Sichtbar bleibt die Zahl wie im Prototyp, dazu ein `aria-label` „<n> zu prüfen“ am Zähler.

Der Conductor legt vor dem Start die Prototyp-Datei und Screenshots der Übersicht (Handy und PC) in den
Scratchpad und gibt Worker, Verifier und `design-quality` die Pfade mit.

## Verifier note (iteration 1)
REASONS:
- Check 1, Kriterium „Seite neben der Skizze verglichen, Abweichungen nur die genannten“: sichtbare Abweichungen, die die Task nicht nennt. Nach der CLAUDE.md-Regel („Ein Baustein aus dem DS ersetzt ein Element der Skizze nur, wenn er gleich aussieht. Sonst wird er eine neue DS-Variante.“) machen die Gründe im OUTCOME sie nicht zulässig.
- Nicht zulässig, Lena-Marke (Eintrag 4): `todoMarkView` in src/Client/Components/Overview/View.fs nutzt `Icons.flag Icons.XS Icons.LenaFlag` (Umriss, 16 px). Der Prototyp (`todoList`) zeichnet ein gefülltes Flag mit 15 px in `var(--flag)` (`I.flag`, fill="currentColor", Pfad "M5 4h12l-2.5 4.5L17 13H5z"). Das verlangt eine neue DS-Variante, etwa ein gefülltes `Icons.flagSolid` auf `/styleguide`. Die Kann-Sammlung reicht nicht.
- Nicht zulässig, Zeile unter dem Titel: Der Prototyp hat „Comdirect · Import vor 2 Std.“ unter „Übersicht“, am Handy und am PC. Schon design-system-k6kfw verlangte die Zeile, sobald BB den Importstand kennt. Diese Task lädt den Stand jetzt (`SyncHistoryLoaded`, `ImportStand`), die Zeile fehlt trotzdem. Der Grund des Workers trägt nicht: Eintrag 1 erscheint nur, wenn es ungeprüfte Buchungen gibt, ohne sie steht der Importstand nirgends.
- Nicht genannt, klein: Der Hauptknopf „Prüfen“ behält den Glow von DS Primary (`--shadow-glow-accent`). `.btn.primary.block` im Prototyp hat keinen Glow.
- Zulässig, wenn die Task sie nennt: (a) Eintrag 5 zählt auch Pushes mit unbekanntem Ausgang, (b) „Im Kassenbuch“ ohne bekannten Import, (c) „Abrufen“/„Buchung“ als Pillen Secondary/Small.
- Checks 2 bis 8 nicht erreicht.
SUGGESTED_FIX: Ein gefülltes Lena-Flag als neue DS-Icon-Variante (auf /styleguide gezeigt) für die Marke von Eintrag 4. Die 12-px-Importzeile unter „Übersicht“ aus dem jetzt geladenen `ImportStand` wiederherstellen (Handy und PC). Den Glow vom Zu-tun-Knopf über die volle Breite entfernen. Die Abweichungen (a) bis (c) trägt der Conductor in die Task ein.
ITERATION_HINT: likely-fixable

## Abweichungen, nach Verifier iteration 1 eingetragen (Conductor, 2026-11-11)
Diese Abweichungen von Prototyp Version 17 gelten zusätzlich zu den oben genannten. Roman hat sie beim Blick bestätigt (2026-11-11), ebenso den Dialog für „Buchung“ am PC.
- (a) Eintrag 5 „Buchung nicht in YNAB“ zählt jede nicht verknüpfte, nicht in YNAB gelöschte Buchung, auch einen Push ohne bekannten Ausgang. Grund: Es ist dieselbe Menge, die das Kassenbuch mit „Nicht in YNAB“ kennzeichnet (Ziel des Eintrags). Abgestimmt mit dem tactical-modeler.
- (b) Kennt BB keinen Import, sagt Zeile 2 von Eintrag 1 „Im Kassenbuch“ statt „Import <Stand>“. Grund: Der Prototyp deckt den Fall nicht ab.
- (c) „Abrufen“ und „Buchung“ am PC sind `Button` Secondary/Small (Pille) statt `.btn.sm`. Grund: übernommen aus design-system-k6kfw, dort schon als offene DS-Lücke in der Kann-Sammlung. „Buchung“ folgt „Abrufen“.

## Outcome

Die Übersicht ist der Einstieg mit „Zu tun“ (Variante A). `./verify.sh` endet mit `VERIFY: PASS` (dotnet 4725 grün, node 313 grün, beide Wächter grün), zuletzt nach den Korrekturen aus Verifier-Iteration 1. Das Delta zur README ist mit `npm run check:readme-delta` geprüft (Exit 0).

**Zu tun** (`src/Client/Components/Overview/Types.fs`, `todos`, `todoMark`, `todoActionLabel`, `todoState`): Einträge 1 bis 6 in der Reihenfolge der Task. Einzahl gilt für „1 Buchung zu prüfen“, „1 Rückfrage an Lena offen“ und „1 Kategorie“. Eintrag 6 fehlt, solange Eintrag 3 dasteht. Der Block erscheint erst, wenn Monat, neueste Buchungen (samt `NeedsReviewCount` und `AskLenaCount` über alle Konten) und `getEntriesNotInYnab` geantwortet haben. Scheitert eine Zählquelle, steht ein Fehler mit „Erneut laden“ statt eines falschen „Alles erledigt“. View in `src/Client/Components/Overview/View.fs`: der erste Eintrag ist groß (16 px halbfett, Zeile 2, gefüllter Knopf im Akzent über die volle Breite), die übrigen sind Zeilen mit Pfeil. Die Marke ist 9 px groß, Gelb oder Rot; Eintrag 4 trägt das gefüllte Lena-Flag des Prototyps (`I.flag`) als neue DS-Variante `Icons.flagSolid` in der neuen Größe `Icons.Mark` (15 px) und der Lena-Farbe (`Badge.lenaFlagScope`, Farbe aus `getLenaFlagSettings`). Der Knopf des ersten Eintrags leuchtet nicht (Prototyp `.btn.primary.block`): neues Feld `Button.ButtonProps.Glow` (Vorgabe `true`, `false` nimmt bei Primary nur `shadow-glow-accent` weg). Beides steht auf `/styleguide` (Abschnitt 1 Lena, 3 Button „Primär ohne Glow“, 11 Icons). Ohne Eintrag steht die neue DS-Karte `Card.allDone` mit grünem Punkt.

**Kopf**: unter „Übersicht“ steht am Handy und am PC „Comdirect · Import <Stand>“ in 12 px gedämpft (`Overview.Types.importLine`, Stand aus `getSyncHistory`, jüngste abgeschlossene Sitzung). Kennt BB keinen Import (keine abgeschlossene Sitzung, Ladefehler der Historie oder noch nicht geladen), fehlt die Zeile, passend zu Abweichung (b). Ein Ladefehler der Historie lässt den Kopf sonst unverändert, „Abrufen“ und Einstellungen bleiben, wie sie waren.

**Ziele** (`src/Client/State.fs`, `OpenOverviewTodo`): 1 und 4 öffnen das Kassenbuch über alle Konten mit genau diesem Filter, die übrigen Filter sind leer (`Ledger.PresetQuickFilter`). 2 und 3 laufen über `Budget.EnterWithAction`; `Budget.State.takeEntryAction` verbraucht den Eintrag, sobald der Monat frisch gelesen ist. Für 2 ist das Kategorie-Detail mit Tab Geld und Holen offen, der Betrag ist mit dem Minus vorbelegt. Für 3 öffnet der Dialog Targets anwenden. 5 öffnet `#/ledger/entry/<id>`, 6 das Budget.

**Neue Leseabfrage** `LedgerApi.getEntriesNotInYnab` (`EntryNotInYnab` in `src/Shared/Domain.fs`, reine Auswahl `Ledger.QueryHandlers.entriesNotInYnab`, SQL `Ledger.Persistence.loadEntriesNotLinkedToYnab`). Abgestimmt mit dem tactical-modeler: die Abfrage zählt jede nicht verknüpfte, nicht gelöschte Buchung, auch einen Push ohne bekannten Ausgang. Das ist dieselbe Menge, die das Kassenbuch mit „Nicht in YNAB“ kennzeichnet. Die Task sagt nur „Push gescheitert“; würde die Übersicht nur Fehlschläge zeigen, widerspräche sie der Liste, auf die sie verweist. Eine Zeile steht in der Endpoint-Negativtabelle.

**Zähler**: `Navigation.Props.OverviewCount` zeigt die Zahl in Rail (rechts oben am Symbol) und Seitenleiste in Mono 11 px Gelb, mit `aria-label`/`title` „<n> zu prüfen“. Die Leiste unten am Handy trägt keinen Zähler. Beim Start auf einer anderen Seite und nach einem Import lädt die Zahl über `Overview.CountsNeeded`.

**+ Buchung**: am Handy schwebt die neue DS-Variante `Button.fab` (52 px, Symbol 20 px, `.overview-capture-fab`). Sie ist ruhig, solange Zu tun nicht leer ist (auch beim Laden), sonst gefüllt. Am PC steht „Buchung“ mit Plus als `Secondary`/`Small` neben „Abrufen“. Beide öffnen das Formular aus `ledger-at63w` als Sheet über der Übersicht (`Ledger.OpenCaptureKeepingPage`, `Ledger.View.captureSheet`) und laden fehlende Konten und Stände nach. Nach dem Speichern bleibt man auf der Übersicht, am PC öffnet kein Detail, und Zu tun lädt neu. Die Rückmeldung nennt jetzt überall Konto und Betrag: „Buchung angelegt · Bar · -8,60 €“ (`Capture.createdText`, auch im Kassenbuch).

**PC und Schriftgrößen**: ab 1024 px ist die Seite ein Raster `minmax(0,1.1fr) minmax(0,1fr)` mit 24 px Abstand und höchstens 1240 px Breite: links Zu tun, rechts Monat und Neueste Buchungen. Darunter ist die Seite einspaltig: Zu tun, Monat, Konten (ab 768 px Neueste Buchungen). Im Monat steht der Monatsname in `ListRoles.primarySize`, „noch N Tage“ auf 11 px.

**Tests**: `src/Tests/OverviewTodoTests.fs` (27 Fälle, darunter die Zeile unter dem Titel mit und ohne bekannten Import; je Bedingung, Reihenfolge, 1 Kategorie gegenüber n Kategorien, 6 fehlt bei 3, Marken und Knöpfe, Block-Zustand, Zähler, Stand des Imports, alle Ziele durch das Root-`update`, Budget nach dem Laden, „+ Buchung“ bleibt auf der Übersicht samt Toast-Text) und `entriesNotInYnabTests` in `src/Tests/LedgerCashBookingTests.fs` (3 Fälle), dazu in `src/Tests/FarbrollenTests.fs` Primary mit und ohne Glow. Der `[flow:uebersicht]`-Test bei 412 px steht in `scripts/uebersicht.test.mjs`, der Flow in `FLOWS`; er prüft zusätzlich die Zeile „Comdirect · Import vor 2 Std.“, „Prüfen“ ohne Schatten und das gefüllte 15-px-Flag der Rückfrage an Lena. Der neue Sweep-Zustand `uebersicht-zu-tun` (320, 412, 900, 1280 px) prüft langen Payee, Zähler und ruhigen Knopf. Angepasst: `OverviewTests.fs` (Bereitschaft wartet auf „nicht in YNAB“), `LedgerCaptureClientTests.fs` (Toast-Text mit Betrag), `scripts/ledger-read-retry.test.mjs` (die Zähl-Abfrage beim Start zählt nicht als Listenaufruf).

**Sweep, Invariante 9**: unter 1024 px steht Zu tun über dem Monat und wächst nach Vorgabe (§7.4 Variante A) mit seinem Inhalt, sobald der Monat da ist. Der verzögerte Monat verschob dort Konten und Neueste Buchungen um 52 bis 160 px. Deshalb gilt der `deferred`-Check der vier `uebersicht`-Zustände jetzt erst ab 1024 px (`deferred.fromWidth` in `scripts/layout-invariants.mjs`). Ab 1024 px ist er grün und hält die neuesten Buchungen unter der Karte „Monat“ an Ort.

**Vergleich mit dem Prototyp (Version 17)**: BB `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/s1weh/bb-neu-uebersicht-handy-412.png` und `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/s1weh/bb-neu-uebersicht-pc-1280.png`, daneben `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/s1weh/prototyp-uebersicht-handy-412.png` und `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/s1weh/prototyp-uebersicht-pc-1280.png`. Die Daten sind synthetisch; im PC-Bild ist „Neueste Buchungen“ leer, weil die Attrappe dort keine Buchungen lieferte. Neu aufgenommen nach Verifier-Iteration 1: die Zeile „Comdirect · Import <Stand>“ unter dem Titel (Handy und PC), das gefüllte Lena-Flag mit 15 px und der Knopf „Prüfen“ ohne Glow. Abweichungen, alle in der Task genannt: Eintrag 1 sagt „zu prüfen“ und „Prüfen“ statt „durchgehen“, Eintrag 5 ist Gelb, „Angepinnt“ fehlt bis `budgeting-w9yt4`, dazu (a) Eintrag 5 zählt auch Buchungen ohne bekannten Push-Ausgang, (b) ohne bekannten Import sagt Zeile 2 von Eintrag 1 „Im Kassenbuch“ (und die Zeile unter dem Titel fehlt), (c) „Abrufen“ und „Buchung“ am PC sind `Button` Secondary/Small (Pille) statt `.btn.sm`.

Offen für Roman: Übersicht am Handy (412 px, Touch) und am PC neben dem Prototyp ansehen [human-eye].

Kann-Sammlung ledger (keine eigenen Tickets nach der Backlog-Disziplin):
1. Die Zahl an der Übersicht zieht nach Prüfen im Kassenbuch erst beim nächsten Betreten der Übersicht oder nach einem Import nach.
2. Die Übersicht hat außer den beiden geforderten Änderungen weiter mehr als drei Schriftgrößen (Titel 20, erster Eintrag 16, Zeilen 15/14, 13, 11, „▲ heute“ 10).
3. Der Sweep prüft Invariante 9 der Übersicht erst ab 1024 px (`deferred.fromWidth`).
4. Die Zeile „Import vor …“ rechnet den Abstand beim Laden der Historie und zieht auf einer offenen Übersicht nicht nach.