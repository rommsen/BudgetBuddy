---
id: design-system-k6kfw
title: "Navigation mit drei Orten: Übersicht, Kassenbuch, Budget, Einstellungen hinter dem Zahnrad, ab 768 px links; dazu eine schlichte Übersicht als Einstieg"
status: todo
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
- [ ] Roman hat Navigation und Übersicht am Handy (412 px, Touch) und am PC neben dem Prototyp
      angesehen. [human-eye]

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
