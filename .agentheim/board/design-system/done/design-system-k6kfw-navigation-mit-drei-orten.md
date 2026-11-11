---
id: design-system-k6kfw
title: "Navigation mit drei Orten: Übersicht, Kassenbuch, Budget, Einstellungen hinter dem Zahnrad, ab 768 px links; dazu eine schlichte Übersicht als Einstieg"
status: done
type: feature
context: design-system
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-s1weh, categorization-n3sxw, ledger-ea70s]
tags: [etappe-3, frontend, navigation, uebersicht]
related_adrs: [0086, 0087]
related_research: [seitenlayout-liste-detail-2026-11-07]
prior_art: []
---

## Why
Die Navigation zeigt heute sechs Punkte (Sync, Rules, Quick Add, Kassenbuch, Budget, Settings), oben am PC
und unten am Handy, dazu am Handy einen eigenen Kopf mit Marke. Aktionen und Inseln stehen neben Orten, und
der Einstieg ist der Sync, weil er zuerst da war. ADR 0086 und ADR 0087 legen drei Orte fest (Styleguide §7.4).

## What
Die Navigation zeigt die drei Orte Übersicht, Kassenbuch und Budget. Quick Add bleibt als vierter Eintrag,
bis `ledger-ea70s` (Ein Erfassen) fertig ist (Roman, 2026-11-11). Am Handy unten eine Leiste, von 768 bis
1023 px links eine schmale Rail, ab 1024 px links die Seitenleiste aus dem Prototyp mit Konten. Die
Einstellungen bleiben eine Seite und liegen hinter dem Zahnrad. Neue Startseite ist eine schlichte Übersicht
mit Kopf, Monat und (am Handy) Konten; „Zu tun“ und „+ Buchung“ bringt `ledger-s1weh`, „Angepinnt“
`budgeting-w9yt4`.

Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg Version 17, Funktionen `nav`,
`sidebar`, `homeHead`, `monthBlock`, `home`, `homeDesk` und die CSS-Klassen `.nav`, `.side`, `.nv`,
`.accl`, `.brand`, `.top`, `.dtop`.

## Acceptance criteria

### Routen
- [ ] `/` zeigt die Übersicht (neuer Fall `Overview` in `Page`), der Sync-Ablauf liegt unter `/sync`.
      Unbekannte Pfade landen auf der Übersicht. Test in `src/Tests` für `Routing.parse` und
      `Routing.toPath` je Fall, auch dass `/sync` und `/` sich nicht vertauschen.
- [ ] Ein Klick auf die Marke führt zur Übersicht.
- [ ] Markiert ist der Ort, unter dem eine Seite hängt: Sync-Ablauf unter Übersicht; Buchungsdetail unter
      Kassenbuch; Budget-Historie, Targets prüfen und Kategorien unter Budget. Test für die Zuordnung
      `Page` → Ort.
- [ ] Jede Unterseite (Sync-Ablauf, Budget-Historie, Targets prüfen, Kategorien, Einstellungen,
      Styleguide) hat im Kopf „Zurück“ mit Pfeil nach links (Prototyp `top`, Klasse `.back`). Zurück führt
      zu ihrem Ort, aus den Einstellungen zur zuvor offenen Seite.

### Handy (unter 768 px)
- [ ] Unten eine Leiste mit gleich breiten Spalten, je Eintrag Symbol 22 px über Beschriftung 12 px, Höhe
      60 px plus Safe-Area, Rand oben, Grund `surface-card`. Aktiv in Akzentfarbe mit
      `aria-current="page"`, sonst gedämpft. Einträge: Übersicht (Haus), Kassenbuch (Buch), Budget
      (Kreis mit Segment), vorläufig Quick Add.
- [ ] Der globale Kopf mit Marke (`mobileHeader`) entfällt. Jeder Ort hat seinen eigenen Kopf mit Titel
      (Prototyp `.top`: Titel 20 px in der Display-Schrift).
- [ ] Die Einstellungen erreicht man über ein Zahnrad im Kopf der Übersicht (Symbolknopf 44 × 44 px,
      `aria-label="Einstellungen"`).

### Rail (768 bis 1023 px)
- [ ] Links eine schmale Rail (rund 72 px) mit den Orten als Symbol mit kurzer Beschriftung darunter,
      aktiv wie in der Seitenleiste. Unten das Zahnrad zu den Einstellungen. Keine Kontenliste.
      Abweichung vom Prototyp, der diese Breite nicht zeigt (Roman, 2026-11-11).

### Seitenleiste (ab 1024 px)
- [ ] Links eine feste Spalte von 232 px, Grund `surface-card`, Rand rechts, Innenabstand 16 × 12 px.
- [ ] Oben die Marke: Zeichen 30 × 30 px mit Akzentrand und „BudgetBuddy“ in der Display-Schrift 17 px.
      Bis `design-system-x1abn` (Logo und Icon) bleibt das heutige Zeichen „B“ statt des Buddy-Bilds.
- [ ] Je Ort eine Zeile von 40 px: Symbol 19 px, Beschriftung 14 px, Ecken 10 px. Aktiv mit Grund
      `surface-elevated` und Akzentfarbe, beim Überfahren `surface-hover`. Rechts Platz für einen Zähler
      (Mono 11 px, Farbe „braucht dich“), gefüllt von `ledger-s1weh`.
- [ ] Darunter der Abschnitt „KONTEN“ (Mono, Großbuchstaben, gedämpft): zuerst „Alle Konten“ mit
      Gesamtsaldo, dann jedes Konto mit Saldo rechts in Mono, negativ in Rot. Zeilen 34 px, Schrift 13 px.
      Ein Klick öffnet das Kassenbuch mit diesem Konto. Steht man im Kassenbuch, ist das gewählte Konto
      mit einem Akzentstrich links und `surface-elevated` markiert.
- [ ] Ganz unten „Einstellungen“ mit Zahnrad, in der Form der Ort-Zeilen.
- [ ] Die Leiste oben am PC (`desktopNav`, 64 px) entfällt; der Inhalt beginnt oben und rechts der
      Seitenleiste.
- [ ] Ab 1024 px steht die Kontowahl nur einmal je Ansicht (§7.14): Das Kassenbuch zeigt neben der
      Seitenleiste keine zweite Kontowahl, falls es heute eine trägt. Ist das ohne Umbau des Kassenbuchs
      nicht zu lösen, steht der Rest als Zeile in `ledger-f8d7x` (Kassenbuch ordnen).

### Übergänge (Roman, 2026-11-11)
- [ ] Regeln stehen nicht mehr in der Navigation. Bis `categorization-n3sxw` (Regeln ins Kassenbuch) führt
      ein Link „Regeln“ im Kopf des Kassenbuchs auf die heutige Regelseite; die Regelseite zeigt im Kopf
      „Zurück“ zum Kassenbuch.
- [ ] Quick Add bleibt Eintrag in Leiste, Rail und Seitenleiste, bis `ledger-ea70s` es ablöst. In der
      Seitenleiste steht es unter den drei Orten.
- [ ] Der Styleguide ist von der Einstellungsseite aus erreichbar (ein Link im unteren Bereich) und steht
      nirgends in der Navigation.
- [ ] Sync ist kein Eintrag mehr. Der Sync-Ablauf startet über „Abrufen“ im Kopf der Übersicht.

### Schlichte Übersicht
- [ ] Kopf: Titel „Übersicht“, darunter in 12 px gedämpft der Stand des Imports (etwa „Comdirect · Import
      vor 2 Std.“), soweit BB ihn heute kennt; sonst entfällt die Zeile. Rechts „Abrufen“: am Handy als
      Symbolknopf (Pfeile im Kreis, `aria-label="Abrufen"`) neben dem Zahnrad, am PC als kleiner Knopf
      mit Symbol und Text. „Abrufen“ startet den Sync-Ablauf.
- [ ] Block „MONAT“ als Karte mit fester Höhe: oben der Monatsname und rechts „noch N Tage“; darunter
      „Ausgegeben“ mit „<ausgegeben> von <zugewiesen>“ in Mono, ein Balken von 8 px (Füllung gedämpft,
      nicht farbig) und darunter die Marke „▲ heute“ an der Stelle des heutigen Tages im Monat; darunter
      „Zu verteilen <Betrag>“ (Klick: Budget) und, falls mindestens eine Kategorie im Minus ist,
      „Im Minus <n>“ in Rot (Klick: Budget mit Ansicht „Im Minus“). Zu verteilen unter null in Rot.
- [ ] Die Werte des Monatsblocks berechnet eine reine Funktion aus dem Budgetmonat (`getBudgetMonth`)
      und dem heutigen Datum als Parameter (Europe/Berlin). Expecto-Test für Tage übrig, Lage der Marke
      am Monatsersten, am Monatsletzten und in einem Februar, Summe ausgegeben und zugewiesen, Zahl im
      Minus.
- [ ] Am Handy unter dem Monat der Abschnitt „KONTEN“ mit Gesamtsaldo im Kopf und je Konto einer Zeile
      (Name, Saldo rechts, negativ in Rot, Zeile 46 px). Tippen öffnet das Kassenbuch mit diesem Konto.
      Am PC stehen die Konten nur in der Seitenleiste.
- [ ] Am PC unter dem Monat „NEUESTE BUCHUNGEN“: die sechs jüngsten Buchungen mit Payee, darunter Tag und
      Kategorie (ohne Kategorie in „braucht dich“), Betrag rechts in Mono. Klick öffnet die Buchung im
      Kassenbuch. Die Übersicht ist am PC einspaltig, bis `ledger-s1weh` „Zu tun“ als linke Spalte bringt.
- [ ] Während Monat oder Konten laden, steht ein Skeleton; ein Ladefehler zeigt `ErrorDisplay`
      (Styleguide §7.12).

### Tests und Vergleich
- [ ] Die `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests, die die alte Navigation oder `/` als
      Sync voraussetzen, fliegen raus und werden gegen die Muster des Styleguides neu geschrieben, je Flow
      einer. Neu: ein `[flow:navigation]`-Test, der bei 412 px die Leiste, bei 900 px die Rail und bei
      1280 px die Seitenleiste prüft (Orte, aktive Markierung, Konto aus der Seitenleiste öffnet das
      Kassenbuch mit diesem Konto, Zahnrad führt zu den Einstellungen). Der Flow steht in `FLOWS` in
      `scripts/browser-usage.mjs`.
- [ ] Der Layout-Sweep (`scripts/layout-invariants.test.mjs`) läuft auch über die Übersicht und über die
      Breiten 412, 900 und 1280 px.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten (Quick Add als vierter Eintrag,
      Rail zwischen 768 und 1023 px, Zeichen „B“ statt Buddy-Bild, Einstellungen als Seite, kein Knopf
      „Handy-Ansicht“, noch ohne Zu tun und Angepinnt). Screenshots von Übersicht und Kassenbuch bei
      412 und 1280 px neben dem Prototyp im Outcome.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.
- [x] Roman hat Navigation und Übersicht am Handy (412 px, Touch) und am PC neben dem Prototyp
      angesehen. [human-eye] (2026-11-11: passt)

## Notes
Schnitt 1 aus Etappe 3 (Fahrplan, Roman 2026-11-11).

Entscheidungen Roman, 2026-11-11:
- Einstellungen bleiben eine eigene Seite; im Prototyp sind sie ein Sheet. Grund: Bankverbindung,
  Flag-Farbe und Konten sind zu viel für ein Sheet.
- Regeln als Link im Kassenbuch-Kopf, bis `categorization-n3sxw`.
- Quick Add bleibt in der Navigation, bis es nicht mehr gebraucht wird (`ledger-ea70s`).
- Styleguide in die Einstellungen.
- Zwischen 768 und 1023 px eine schmale Rail ohne Konten.

Der Knopf „Handy-Ansicht“ in der Seitenleiste ist ein Werkzeug des Prototyps und kommt nicht nach BB.

Heute: `src/Client/DesignSystem/Navigation.fs` (`navItems`, `desktopNav`, `mobileNav`, `mobileHeader`),
`src/Client/View.fs` (`toNavPage`, `fromNavPage`), `Routing` in `src/Client/Types.fs` (`[] -> SyncFlow`).
Daten: `getBudgetMonth` und `getLedgerAccounts` in `src/Shared/Api.fs`; eine neue API braucht es nicht.

Der Conductor legt vor dem Start die Prototyp-Datei und je Ansicht (Übersicht Handy und PC, Kassenbuch PC)
einen Screenshot in den Scratchpad und gibt Worker, Verifier und `design-quality` die Pfade mit.

## Verifier note (iteration 1)

**REASONS:**
- Kriterium „Block MONAT als Karte mit fester Höhe“ nicht erfüllt: `monthCard` in `src/Client/Components/Overview/View.fs` hat keine feste Höhe (`{cardClass} px-3.5 py-3 flex flex-col gap-2`), nur `monthSkeleton` hat `min-h-[118px]`, also eine Mindesthöhe.
- Die geladene Karte ist höher als das Skeleton (rund 157 px bei 412 px, rund 131 px bei 1280 px, Skeleton 118 px). Beim Laden rutschen Konten (Handy) und Neueste Buchungen (PC) nach unten.
- Kein Test deckt das ab: Der Sweep-Zustand `uebersicht` in `scripts/layout-states.mjs` hat keinen `deferred`-Eintrag, Invariante 9 (kein Sprung beim Nachladen) läuft auf der Übersicht nie. Das OUTCOME nennt es nicht als Abweichung.
- Nicht blockierend: (a) „Abrufen“ am PC ist `Button.Small` als Pille; im Prototyp `.btn.sm` mit 10 px Radius und 36 px Höhe, im OUTCOME nicht als Abweichung genannt. (b) `bb-uebersicht-1280.png` zeigt mehr als sechs Buchungen, ein Fixture-Effekt (Route `kassenbuch` registriert zuletzt), kein Code-Fehler. (c) Der Monat nutzt `DateTime.Today` (Ortszeit des Browsers) statt eines Tages in Europe/Berlin; die reine Funktion nimmt „heute“ als Parameter.

**SUGGESTED_FIX:** Der Monatskarte je Breite eine feste Höhe geben (geladene Karte und `monthSkeleton` gleich, ob „Im Minus“ steht oder nicht). Im Sweep-Zustand `uebersicht` einen `deferred`-Eintrag für `getBudgetMonth` ergänzen, damit Invariante 9 den Sprung findet, dann `./verify.sh` erneut laufen lassen.

**ITERATION_HINT:** likely-fixable

## Outcome

Navigation mit drei Orten und schlichte Übersicht gebaut. `./verify.sh` endet mit `VERIFY: PASS` (dotnet 4679 grün, node 311 grün, beide Wächter grün).

**Routen und Orte** (`src/Client/Types.fs`): neuer Fall `Overview` unter `/`, der Sync-Ablauf unter `/sync`, unbekannte Pfade landen auf der Übersicht. `placeOf` ordnet jede Seite ihrem Ort zu (Sync-Ablauf unter Übersicht, Buchungsdetail und Regeln unter Kassenbuch, Historie, Targets prüfen und Kategorien unter Budget). `settingsReturn` merkt die Seite vor den Einstellungen, der Styleguide darunter ändert das Ziel nicht. Tests: `src/Tests/RoutingTests.fs` (je Seite hin und zurück, `/` und `/sync` nicht vertauscht, jeder `Page`-Fall abgedeckt, Zuordnung Seite → Ort, Ziel von „Zurück“).

**Navigation** (`src/Client/DesignSystem/Navigation.fs`, neu geschrieben): `bottomBar` am Handy (vier gleich breite Spalten, Symbol 22 px über Text 12 px, 60 px plus Safe-Area, `surface-card`, aktiv im Akzent mit `aria-current="page"`), `rail` von 768 bis 1023 px (72 px, Beschriftung 10 px, unten das Zahnrad nur als Symbol mit `aria-label`, keine Konten), `sidebar` ab 1024 px (232 px, Marke mit Zeichen „B“ 30 × 30 px und „BudgetBuddy“ 17 px, Ort-Zeilen 40 px mit Symbol 19 px und Platz für den Zähler, Abschnitt „Konten“ mit „Alle Konten“ und je Konto Saldo in Mono, negativ rot, Zeilen 34 px, gewähltes Konto im Kassenbuch mit Akzentstrich links und `surface-elevated`, unten „Einstellungen“). `desktopNav` und `mobileHeader` sind weg; `--nav-height` ist 0, die Haft-Regeln in `styles.css` haften am Fensterrand. Neue Symbole in `Icons.fs` (`home`, `book`, `pie`, `gear`, `refresh`, `arrowLeft`, Größen `Tab` 22 px und `Side` 19 px). Token `FontSizes.brand` (17 px, Ausnahme in `scripts/token-guard-exceptions.json` auf 9 erhöht).

**Kopf und Zurück** (`src/Client/DesignSystem/PageHeader.fs`): `top`, `backButton`/`backButtonFor`, `iconButton`. „Zurück“ steht auf Sync-Ablauf (zur Übersicht), Budget-Historie, Targets prüfen, Kategorien (wie bisher zum Budget), Einstellungen (zur zuvor offenen Seite), Styleguide (zu den Einstellungen) und Regeln (zum Kassenbuch). Das Kassenbuch trägt im Kopf den Link „Regeln“ (bis `categorization-n3sxw`), der Styleguide-Link der Einstellungen war schon da.

**Übersicht** (`src/Client/Components/Overview/`): Kopf „Übersicht“, am Handy „Abrufen“ (Pfeile im Kreis) und Zahnrad als Symbolknöpfe, am PC „Abrufen“ als kleiner Knopf. „Abrufen“ öffnet `/sync` und schickt `StartSync`. Block „Monat“ als Karte fester Höhe (am Handy 160 px mit 48 px hoher unterer Zeile, ab 768 px 136 px; geladene Karte und Platzhalter gleich hoch, mit oder ohne „Im Minus“; lange Beträge werden mit Ellipse und `title` gekürzt): Monatsname, „noch N Tage“, „Ausgegeben … von …“ in Mono, Balken 8 px gedämpft, Marke „▲ heute“, „Zu verteilen“ (Klick: Budget, unter null rot) und „Im Minus n“ in Rot (Klick: Budget mit Filter Überzogen über `Budget.EnterWithFilter`). Die Werte rechnet die reine Funktion `monthSummary` aus `getBudgetMonth` und dem heutigen Tag; „heute“ ist der Kalendertag in Europe/Berlin (`Overview.State.berlinToday`, unter .NET die Ortszeit der Tests mit `TZ=Europe/Berlin`), auch für „Heute“/„Gestern“ der neuesten Buchungen. Darunter am Handy (bis 1023 px) „Konten“ mit Gesamtsaldo, ab 768 px „Neueste Buchungen“ (sechs, Payee, Tag und Kategorie, ohne Kategorie in „braucht dich“, Klick öffnet `#/ledger/entry/<id>`). Skeleton beim Laden, `ErrorDisplay` bei Fehler. Konten und Stände laden beim Start der App für die Seitenleiste und ziehen mit, wenn das Kassenbuch neu lädt (`State.syncNavAccounts`). Ein Konto aus Seitenleiste oder Übersicht setzt den Filter vor dem Betreten (`Ledger.PresetFilter`). Tests: `src/Tests/OverviewTests.fs` (Tage übrig, Marke am Monatsersten, am Monatsletzten und im Februar und Schaltjahr, Summen, Zahl im Minus, Übergänge durch das Root-`update`, Bereitschaft der Seite).

**Kontowahl einmal je Ansicht (§7.14):** Ab 1024 px blendet das Kassenbuch seine Konten-Spalte aus (`lg:hidden`), die Seitenleiste ist die einzige Kontowahl. Zwischen 768 und 1023 px bleibt die Spalte neben der Rail. Am Handy stehen weiterhin Kontowechsler und Kartenleiste übereinander; das ist heutiger Stand und gehört zu `ledger-f8d7x` (Kassenbuch ordnen).

**Folgeanpassungen an die Seitenleiste:** Tastatur-Hinweis und Auswahl-Leiste am PC beginnen rechts von Rail und Seitenleiste; der Platzhalter unter der Liste ist bis 1279 px zweizeilig hoch. Die Budget-Liste zeigt neben Seitenleiste und Panel, solange sie schmaler als 600 px ist, die Zeilen wie am Handy (Container-Abfrage in `MonthView.fs`), sonst lief die Tabelle bei 1024 px aus der Spalte.

**Iteration 2 (Verifier-Hinweis):** Die Karte „Monat“ hatte keine feste Höhe, beim Laden rutschten Konten und Neueste Buchungen um 13 bis 61 px. Der Sweep-Zustand `uebersicht` hat jetzt einen `deferred`-Eintrag für `getBudgetMonth` (Invariante 9). Rot vor dem Fix: ein Lauf nur über `uebersicht` meldete Invariante 9 bei 320, 390, 412, 768 und 900 px („overview-account … verschoben um 0 / 42 px“ bei 412, „overview-recent … 0 / 13 px“ bei 768). Grün nach dem Fix (`monthCardClass` mit `h-[160px] md:h-[136px]`, Platzhalter mit derselben Klasse), danach `./verify.sh` mit `VERIFY: PASS`.

**Browser-Tests:** neuer `[flow:navigation]` in `scripts/navigation.test.mjs` (412 px Leiste, 900 px Rail, 1280 px Seitenleiste mit Konto ins Kassenbuch und Zahnrad), Flow in `FLOWS`. `[flow:budget-oeffnen]` (Budget über die alte Tab-Leiste) ist entfernt, der neue Flow deckt es. Die Sync-Tests öffnen jetzt `#/sync`; `KNOWN_ROUTES` kennt `/sync`. Der Layout-Sweep hat den Zustand `uebersicht` und läuft zusätzlich bei 412, 900 und 1280 px (`PAGE_WIDTHS`); `mockApp` liefert Konten und Stände für die Seitenleiste.

**Vergleich mit dem Prototyp (Version 17).** BB-Screenshots: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/241b633c-5a75-4c48-bb3b-28a91c68340d/scratchpad/shots/bb-uebersicht-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/241b633c-5a75-4c48-bb3b-28a91c68340d/scratchpad/shots/bb-uebersicht-1280.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/241b633c-5a75-4c48-bb3b-28a91c68340d/scratchpad/shots/bb-kassenbuch-412.png`, `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/241b633c-5a75-4c48-bb3b-28a91c68340d/scratchpad/shots/bb-kassenbuch-1280.png`, dazu `bb-uebersicht-900.png` und `bb-budget-1024.png` im selben Ordner; Prototyp daneben in `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/241b633c-5a75-4c48-bb3b-28a91c68340d/scratchpad/proto/` (`uebersicht-412.png`, `uebersicht-1280.png`, `kassenbuch-412.png`, `kassenbuch-1280.png`). Daten sind die synthetischen Fixtures des Layout-Sweeps (Übersicht nur mit den Antworten der Übersicht, sechs Buchungen). Abweichungen, die die Task nennt: Quick Add als vierter Eintrag, Rail zwischen 768 und 1023 px, Zeichen „B“ statt Buddy-Bild, Einstellungen als Seite, kein Knopf „Handy-Ansicht“, noch ohne Zu tun und Angepinnt. Weitere Abweichungen mit Grund:
- Die Zeile mit dem Stand des Imports unter „Übersicht“ entfällt: ohne neue Abfrage kennt BB den letzten Comdirect-Import nicht (`getCurrentSession` liefert nur eine laufende Sitzung).
- Symbolknöpfe im Kopf und „Zurück“ sind am Handy 48 px statt 44 px, Kontozeilen der Übersicht 48 px statt 46 px: Styleguide §7.15 (Trefferfläche 48 px), der Sweep prüft das.
- Die Konten der Übersicht stehen bis 1023 px da, weil die Rail keine Konten hat; „Neueste Buchungen“ ab 768 px.
- Das Zahnrad der Rail hat keine sichtbare Beschriftung, „Einstellungen“ passt nicht in 72 px (Name als `aria-label` und `title`).
- Der Sync-Ablauf zeigt im Kopf nur „Zurück“, seine Titel trägt er selbst; in der Prüfung steht zusätzlich der bisherige Pfeil, der die Prüfung abbricht.
- „Abrufen“ am PC ist `Button` `Secondary`/`Small` aus dem DS (Pille, am PC ohne Mindesthöhe), der Prototyp hat `.btn.sm` mit 10 px Radius und 36 px Höhe. Das DS hat dafür keine Variante; eine neue Knopfvariante gehört in eine DS-Task und nicht inline in die Übersicht (Drift, Styleguide §5).
- Das Budget behält seine Monatsleiste als Kopf (wie im Prototyp), einen eigenen Titel „Budget“ gibt es dort nur für den Screenreader.

Offen für Roman: Navigation und Übersicht am Handy (412 px, Touch) und am PC neben dem Prototyp ansehen [human-eye].

Kann-Sammlung design-system (keine eigenen Tickets nach der Backlog-Disziplin): (1) die Styleguide-Galerie und das Export-Bundle laden ohne Backend jetzt einen Aufruf für die Konten der Seitenleiste, der still scheitert; (2) am Handy zeigt das Kassenbuch Kontowechsler und Kartenleiste übereinander (gehört zu `ledger-f8d7x`); (3) die Budget-Liste wechselt neben dem Panel bei 1024 bis rund 1150 px auf Zeilen wie am Handy, eine Tabelle mit weniger Spalten könnte das ersetzen; (4) eine DS-Knopfvariante „klein mit 10 px Radius und 36 px Höhe“ für „Abrufen“ (Prototyp `.btn.sm`).
