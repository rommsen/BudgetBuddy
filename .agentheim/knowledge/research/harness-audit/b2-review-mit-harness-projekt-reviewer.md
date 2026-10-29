# B2: Review mit den Projekt-Reviewern (backend-code-quality, frontend-architecture, design-quality)

Datum: 2026-10-29. Stand der Zeilennummern: `main` bei `ed3a6ab`. Nur lesend, keine Änderung an Code oder Konfiguration.

Gegenstand: a20fef6, ae7cfb5, 92ac090, cfb05ff, 0f39ac0, a0a8cab, b4b5cc3 (Server, Bot-API, Security) und 897b13c (Design System). Die drei Agent-Dateien wurden gelesen und ihre Anweisungen von Hand ausgeführt, samt `.claude/skills/fsharp-backend/SKILL.md`, `.claude/skills/fsharp-frontend/SKILL.md` und den dort genannten `standards/`-Dateien. Die Agents schreiben laut Anweisung nach `reviews/*.md`; dieser Auftrag schreibt stattdessen hierher.

Schweregrade: hoch / mittel / niedrig / Hinweis. Die Agents selbst kennen Critical / Warning / Suggestion (`backend-code-quality.md:456-460`); hoch entspricht Critical, mittel Warning, niedrig und Hinweis Suggestion.

## 1. Zustand der Agent-Dateien

### 1.1 Warum Claude Code sie nicht lädt

Alle drei Dateien haben ein `description`-Feld als mehrzeiligen Plain-Scalar mit Leerzeilen, `<example>`-Blöcken und Zeilen wie `Context: ...`. Ein strenger YAML-Parser (Ruby `YAML.safe_load`) bricht bei allen dreien in Zeile 7, Spalte 1 ab (`could not find expected ':' while scanning a simple key`), also am ersten `<example>`. `review-code.md` parst sauber. `qa-milestone-reviewer.md` scheitert am strengen Parser ebenfalls (Doppelpunkt in einer einzeiligen Beschreibung), wird aber geladen; der Loader ist also bei einzeiligen Werten nachsichtig, bei der mehrzeiligen Form mit Leerzeilen nicht. Abhilfe: `description: |` als Block-Scalar oder alles in eine Zeile mit `\n`, wie bei `qa-milestone-reviewer.md`.

### 1.2 Inhaltliche Stimmigkeit

backend-code-quality:
- `backend-code-quality.md:76-80` lässt `src/Server/Domain.fs`, `Validation.fs`, `Persistence.fs`, `Api.fs` prüfen. `src/Server/Domain.fs` existiert nicht. Die Fachlogik liegt in Bounded-Context-Ordnern (`src/Server/Ledger/{Domain,Events,CommandHandlers,QueryHandlers,Persistence}.fs`, `src/Server/Budgeting/...`) und in Einzelmodulen wie `BotApi.fs`. Dasselbe veraltete Vier-Dateien-Bild steht in `.claude/skills/fsharp-backend/SKILL.md:42-52` und `standards/backend/overview.md:36-43`. Commands, Events und Query-Handler (`.claude/rules/feature-workflow.md`, `.claude/rules/semantic-messaging.md`) kommen im Agent nicht vor.
- "No classes" absolut (`backend-code-quality.md:238-247`, `:449`). Für nebenläufige Speicher (Vorschau, Drossel, Cache) ist eine Klasse mit gekapseltem Zustand vertretbar; der Agent kennt keine Ausnahme.
- Zwei Abschnitte heißen "Step 4" (`:199` und `:262`).
- `.claude/skills/fsharp-backend/SKILL.md:64-69`: das Validierungsbeispiel kompiliert nicht (`if ... then "Name required"` ergibt `string`, danach `List.choose id`).
- `standards/backend/overview.md:22-23` beschreibt die HttpHandler-Rückgabe irreführend ("`Some context` to continue pipeline", "`None` to short-circuit"). In Giraffe heißt `None` "nicht zuständig, nächste Alternative in `choose` probieren", und der Aufruf von `next ctx` in einer `choose`-Alternative beendet die Auswahl. Genau dieses Missverständnis steckt im Befund B1 unten.
- `standards/backend/domain-logic.md:97-99` zeigt als "GOOD - Pure function" ein Beispiel mit `DateTime.UtcNow`; `.claude/rules/domain-purity.md` verbietet genau das.

frontend-architecture:
- `frontend-architecture.md:363-376` empfiehlt Debounce über `Cmd.OfAsync.perform`; `.claude/rules/elmish-state.md` verbietet `Cmd.OfAsync.perform`. Das Projekt hat dafür `src/Client/Debounce.fs`.
- `frontend-architecture.md:380-395` zeigt Formularzustand mit `IsSubmitting: bool`, obwohl der Agent selbst (`:226-239`) und `elmish-state.md` Bool-Flags für Async-Zustand ablehnen.
- `frontend-architecture.md:336` (`text-error`) und `:340` (`alert`) sind DaisyUI-Klassen.
- `:75-80` nennt nur `State.fs`, `View.fs`, `Types.fs`; der Großteil liegt unter `src/Client/Components/*` und `src/Client/DesignSystem/*`. Kein Verweis auf `standards/frontend/styleguide.md`, das laut CLAUDE.md das Gate ist.
- Zwei Abschnitte heißen "Step 4" (`:207`, `:268`).

design-quality:
- DaisyUI-Klassen in Beispielen: `bg-base-100` (`design-quality.md:123`, `:212`, `:215`), `input input-bordered` (`:139`). CLAUDE.md (Tech Stack) und `standards/frontend/styleguide.md:170` sagen: kein DaisyUI (ADR 0031). Auch `standards/frontend/view-patterns.md:46` und `:74` nutzen noch `bg-base-100`, also genau die Datei, die der Agent als erste Lektüre vorgibt.
- Widerspruch in sich: `:74-87` sagt, Hex-Werte nie hier zwischenspeichern, und nennt `#ff2e97` ausdrücklich als veraltet; `:157-162` führt trotzdem eine Hex-Liste mit `neon-pink (#ff2e97)`. Die Bedeutung "Warnings: neon-pink" weicht von `styleguide.md:63` ab (Pink = Akzent / "attention needed").
- Schriften falsch: `:89-92` und `:173` nennen Outfit, Exo 2, Orbitron, Rajdhani. `src/Client/styles.css:41-42`, `:84-86` definieren Inter, Space Grotesk, JetBrains Mono.
- `:248` nutzt `focus:ring-opacity-50`, eine Tailwind-3-Klasse, die es in Tailwind 4 nicht mehr gibt.
- Positiv: der Abschnitt zur Farbpalette (`:74-87`) verweist bereits auf `styles.css` und `styleguide.md`.

Gemeinsam: Ausgabe nach `reviews/backend-quality.md`, `reviews/frontend-architecture.md`, `reviews/design-quality.md`. Das Verzeichnis `reviews/` existiert (die ersten beiden Dateien liegen dort), ist aber nicht Teil des agentheim-Ablaufs. Die `.claude/rules/` sind zum Teil veraltet (Supabase in `build-verification.md` und `testing.md`), die Agents verweisen aber nicht direkt darauf.

## 2. Befunde backend-code-quality (Server-Anteile)

### B1 (hoch) Bot-Handler beendet für alle Nicht-Bot-Pfade die Giraffe-Pipeline, Fable.Remoting wird nie erreicht

- Commit: b4b5cc3 (eingeführt), ae7cfb5 und 92ac090 ändern die Stelle, ohne es zu beheben.
- Ort: `src/Server/BotApi.fs:401-402` (`if not (isBotPath path) then next ctx`), eingehängt als erste Alternative in `choose` in `src/Server/Api.fs:1987-1988`.
- Beschreibung: In Giraffe bekommt jede `choose`-Alternative dasselbe `next`. Ruft die Alternative `next ctx` auf, liefert `UseGiraffe` (`next` = `earlyReturn`) `Some ctx`, `choose` gilt als erledigt, die folgenden Remoting-Handler laufen nicht. Für "nicht zuständig" müsste der Handler `skipPipeline` (also `Task.FromResult None`) liefern.
- Nachweis, lokal: `main` (HEAD) aus `git archive` gebaut, mit leerem Temp-`DATA_DIR` gestartet. `POST /api/SettingsApi/getSettings` antwortet `200` mit 0 Byte Rumpf. Derselbe Aufruf gegen den Stand `b4b5cc3^` antwortet `200` mit `{"Comdirect":null,"Ynab":null,"Sync":{"DaysToFetch":30}}`. Ein Minimal-Skript mit Giraffe 6.4.0 (`choose [ (fun next ctx -> next ctx); text "second" ]`) bestätigt das: 200, leerer Rumpf, zweiter Handler nicht erreicht. Der SPA-Fallback (`MapFallbackToFile`, `Program.fs:84-90`) ist vermutlich ebenso betroffen, weil Giraffe vor dem Endpoint-Middleware antwortet; das wurde nicht gemessen (kein `dist/public` im Testlauf).
- Folge, wenn der Stand so deployed ist: jeder API-Aufruf der App liefert einen leeren Rumpf. Das sollte sofort am Live-System geprüft werden (etwa `curl -X POST .../api/SettingsApi/getSettings -d '[]'`).
- Warum die Tests es nicht fanden: `src/Tests/BotApiTests.fs:86-90` wertet "`next` wurde aufgerufen" als "weitergereicht" (`-1`), und die Tests `:215-216`, `:232-233` sichern damit genau das fehlerhafte Verhalten ab. Es gibt keinen Test über die echte Pipeline (`TestServer`), siehe auch die Begründung in `src/Tests/CorsTests.fs:5-9`.
- Regel: `backend-code-quality.md:4` und `:47` (Code muss tun, was er ausdrückt), CLAUDE.md "Anti-Patterns to Avoid" und "Bug Fix Protocol" (Tests müssen das richtige Verhalten prüfen); fachlich falsch angeleitet durch `standards/backend/overview.md:22-23`.

### B2 (mittel) Drosselung vor der Anmeldung: Unangemeldete verbrauchen Bobs Kontingent

- Commit: 92ac090
- Ort: `src/Server/BotApi.fs:404-414`, `BotRateLimiter` `:112-127`
- Beschreibung: Das gleitende Fenster gilt global für alle `/bot/`-Anfragen und wird vor der Token-Prüfung belegt. Zehn beliebige Anfragen pro Minute ohne Token sperren Bob aus. Das ist laut Task gewollt ("vor der Anmeldung"), bleibt aber ein einfacher Hebel gegen Bob. Getrennte Fenster für angemeldete und unangemeldete Aufrufer würden das lösen.
- Regel: ohne Regelbezug (Sicherheitsabwägung).

### B3 (mittel) Push-Fehler liefert `entry_id: null`, obwohl die Buchung im Kassenbuch steht

- Commit: b4b5cc3
- Ort: `src/Server/BotApi.fs:334-339`, Ursache `src/Server/Ledger/CommandHandlers.fs:704-714` (`addCashExpense` gibt bei Push-Fehler nur `Error err` zurück und verliert die `EntryId`).
- Beschreibung: Bob bekommt bei gescheitertem YNAB-Push `status: "booked"`, `entry_id: null`. Die Buchung existiert (inkl. `EntryPushToYnabFailed`), ist für Bob aber nicht adressierbar. Der Kommentar `BotApi.fs:45-46` dokumentiert das, löst es nicht. Ein Rückgabetyp wie `EntryId * PushOutcome` würde den Zustand ehrlich abbilden.
- Regel: `backend-code-quality.md:154-170` (Geschäftszustände als DU statt Feldkombinationen), `standards/backend/error-handling.md:7`.

### B4 (niedrig) Fachregeln und Zeitzonenlogik in Validation.fs

- Commit: b4b5cc3, cfb05ff, a20fef6
- Ort: `src/Server/Validation.fs:192-229` (Berliner Zeitzone, Tagesgrenzen, `checkBotDailyLimit`), `:335-372` (`resolveBotCategory`), Typen `:160-178`
- Beschreibung: Validation.fs ist laut Standard die Eingabeprüfung. Tagesgrenze, Kategorie-Auflösung und Kalendertag-Berechnung sind Fachregeln und gehören nach `Ledger/Domain.fs` (oder ein eigenes Bot-Domain-Modul). `tryResolveBerlinZone` greift zudem auf die Zeitzonendatenbank des Systems zu, also auf Umgebung.
- Regel: `standards/backend/domain-logic.md:7` und `:140` ("All business rules in Domain.fs"), `backend-code-quality.md:319-323`, CLAUDE.md "Development Order".

### B5 (niedrig) Ausnahme statt Result für fehlende Zeitzone

- Commit: a20fef6
- Ort: `src/Server/Validation.fs:199-203` (`failwith` im `Lazy`)
- Beschreibung: Durch den Startabbruch in `src/Server/Program.fs:36-40` praktisch nicht erreichbar. Der Zugriff `berlinOffsetAt` bleibt aber eine Funktion, die werfen kann, ohne dass ihre Signatur das zeigt. Eine einmal beim Start aufgelöste `TimeZoneInfo` als Parameter (in `BotDeps`) wäre sauberer und testbarer.
- Regel: `standards/backend/error-handling.md:7`, CLAUDE.md "Use `Result<'T, string>` for fallible operations".

### B6 (niedrig) Klassen mit veränderlichem Zustand und ein Modul-Semaphor außerhalb der Abhängigkeiten

- Commit: b4b5cc3, 92ac090, cfb05ff
- Ort: `src/Server/BotApi.fs:61-85` (`BotPreviewStore`), `:112-127` (`BotRateLimiter`), `:131-149` (`BotCategoryCache`, `mutable` in `:133`), `:90` (`bookingGate` auf Modulebene)
- Beschreibung: Die Klassen sind für Nebenläufigkeit vertretbar. `bookingGate` liegt aber prozessweit außerhalb von `BotDeps`, obwohl `:151` ("Alles, was von außen kommt") die Abhängigkeiten dort bündelt. Tests teilen sich damit stillschweigend dieselbe Sperre.
- Regel: `backend-code-quality.md:238-247`, `:448-449`.

### B7 (niedrig) Pyramide aus verschachtelten `match` im Vorschau-Handler

- Commit: b4b5cc3, cfb05ff
- Ort: `src/Server/BotApi.fs:248-307`
- Beschreibung: Sechs Ebenen `match` mit je eigener Fehlerantwort. Eine kleine Fehler-DU (`BotRejection`) mit einer Abbildung auf Status und Code plus `taskResult`-artige Verkettung würde den Ablauf lesbar machen und die Zuordnung Fehler zu HTTP-Status an einer Stelle bündeln.
- Regel: `backend-code-quality.md:50` (Lesbarkeit), `:264-274` (Pipeline-Komposition).

### B8 (niedrig) Stringvergleich gegen Actor- und Ereignisnamen im SQL

- Commit: cfb05ff
- Ort: `src/Server/Ledger/Persistence.fs:861` (`actor_kind = 'Bob' AND event_type = 'CashExpenseAdded'`)
- Beschreibung: Die Literale doppeln die Abbildung in `src/Server/Ledger/Events.fs:1157` (`actorToColumns`). Ändert sich der Spaltenwert, zählt die Tagesgrenze still null Buchungen und lässt alles durch (fail-open). Den Wert aus `actorToColumns Bob` ableiten oder per Test koppeln.
- Regel: `backend-code-quality.md:454` ("No stringly-typed code").

### B9 (Hinweis) Token-Vergleich verrät die Länge

- Commit: b4b5cc3
- Ort: `src/Server/BotApi.fs:228-236`
- Beschreibung: `FixedTimeEquals` gibt bei unterschiedlicher Länge sofort `false` zurück. Praktisch kaum ausnutzbar; mit einem Hash beider Seiten (z.B. SHA-256) vor dem Vergleich wäre auch die Länge verdeckt.
- Regel: ohne Regelbezug.

### B10 (Hinweis) Geldbetrag als `float` im JSON, Primitive für Fachwerte

- Commit: b4b5cc3
- Ort: `src/Server/BotApi.fs:205` (`Encode.float (float p.AmountEur)`), `src/Server/Validation.fs:167-175` (`AmountEur: decimal`, `Payee: string`, `Category: string`)
- Beschreibung: Für zwei Nachkommastellen unkritisch, aber der Weg `decimal` zu `float` ist unnötig (`Encode.decimal` existiert). Der Agent verlangt Fachtypen statt Primitiven.
- Regel: `backend-code-quality.md:140-152`, `:438`.

### B11 (Hinweis) Drei unabhängige 500-EUR-Literale

- Commit: b4b5cc3, a0a8cab, cfb05ff
- Ort: `src/Server/ServerConfig.fs:29` (`defaultBotMaxAmountEur`), `:38` (`hardBotMaxAmountEur`), `src/Server/Validation.fs:189` (`botDailyMaxSumEur`)
- Beschreibung: Fachlich drei verschiedene Grenzen mit zufällig gleichem Wert. Vorgabe und harter Deckel sind aber dieselbe Aussage ("Umgebung darf nur senken"); `default = hard` würde das ausdrücken.
- Regel: ohne Regelbezug.

### B12 (Hinweis) Zwei JSON-Bibliotheken nebeneinander

- Commit: b4b5cc3
- Ort: `src/Server/Validation.fs:238` (`System.Text.Json`), `src/Server/BotApi.fs:12` (`Thoth.Json.Net`)
- Beschreibung: Lesen mit System.Text.Json, Schreiben mit Thoth. Funktioniert, ist aber eine zweite Konvention.
- Regel: ohne Regelbezug.

### B13 (Hinweis) Logging über `printfn`

- Commit: b4b5cc3, a0a8cab
- Ort: `src/Server/BotApi.fs:373`, `src/Server/Api.fs:1986`
- Beschreibung: Entspricht dem Standard (`standards/backend/api-implementation.md:72` nutzt selbst `printfn`), daher kein Verstoß. Der Fehlerfall loggt nur `ex.Message`, keinen Stack.
- Regel: kein Verstoß; Hinweis ohne Regelbezug.

### B14 (Hinweis) Quelltext-Tests statt Verhaltenstest für CORS

- Commit: 0f39ac0
- Ort: `src/Tests/CorsTests.fs:20-26`, `:31-44`
- Beschreibung: Der Code-Teil (Entfernen von `AddCors`/`UseCors`) ist korrekt. Die Tests prüfen Quelltext (`Contains "Cors"`, Kommentarabschnitt per `IndexOf "//"`, das auch `"http://"` in Strings abschneidet). Die Begründung (kein WebApplicationFactory-fähiger Einstieg) ist dieselbe Lücke, durch die B1 unentdeckt blieb. Ein `TestServer` über `Api.webApp()` würde beide Themen abdecken.
- Regel: `.claude/rules/testing.md` "Test names must describe behavior, not implementation" (gilt sinngemäß für die Prüfungen).

Unauffällig nach den Agent-Kriterien: ae7cfb5 `isBotPath` (`BotApi.fs:390-393`, parametrisiert, getestet), a0a8cab Parser und Deckel (`ServerConfig.fs:43-56`, rein, getestet), a20fef6 Startprüfung und tzdata im Dockerfile, cfb05ff parametrisierte Abfrage und Prüfung unter Sperre (`BotApi.fs:343-350`), b4b5cc3 neue `Actor`-Variante additiv in `Events.fs`, `QueryHandlers.fs` und `Shared/Domain.fs` mit erschöpfendem Matching.

## 3. Befunde frontend-architecture (897b13c und Client-Anteile)

### F1 (Hinweis) Öffentlich gemachte Klassenfunktionen für Tests

- Commit: 897b13c
- Ort: `src/Client/DesignSystem/Button.fs:64` (`sizeToClass` war `private`), `:70` (`iconOnlyClass`)
- Beschreibung: Für die Expecto-Tests öffentlich. Vertretbar, erweitert aber die DS-API um zwei Funktionen, die niemand außer Tests nutzen soll. Die Tests (`src/Tests/ButtonSizeTests.fs`) prüfen Klassenstrings und sind nahe an der Implementierung; der Playwright-Wächter `scripts/touch-target-small-button.test.mjs` misst dagegen echtes Verhalten und trägt die Aussage.
- Regel: ohne Regelbezug.

### F2 (Hinweis) Icon-only-Erkennung über leeren Text

- Commit: 897b13c
- Ort: `src/Client/DesignSystem/Button.fs:70-71`
- Beschreibung: `IsNullOrEmpty` statt `IsNullOrWhiteSpace`; ein Text `" "` gilt als Text. Nur Small bekommt die Mindestbreite; Medium/Large sind durch Padding breit genug.
- Regel: ohne Regelbezug.

Kein MVU-Befund: 897b13c berührt kein State/Update, b4b5cc3 ändert im Client nur `historyDotFor` (`src/Client/Components/Ledger/View.fs:1105-1106`) mit erschöpfendem Match.

## 4. Befunde design-quality (897b13c und Client-Anteile)

### D1 (niedrig) Touch-Höhe als Rohklasse statt Token

- Commit: 897b13c
- Ort: `src/Client/DesignSystem/Button.fs:65` (`min-h-[48px] md:min-h-0`), Token vorhanden in `src/Client/DesignSystem/Tokens.fs:311` (`TouchTargets.minHeight`, identischer String)
- Beschreibung: Der Wert stimmt, die Quelle ist aber doppelt. Ändert jemand das Token, wandert Button nicht mit.
- Regel: `standards/frontend/styleguide.md:196-199` (Tokens vor Rohklassen, Rohklassen gelten als Drift), `:206`.

### D2 (niedrig) Kein `touch-action: manipulation` auf Button

- Commit: 897b13c (Bestand, im Zuge der Touch-Arbeit nicht ergänzt)
- Ort: `src/Client/DesignSystem/Button.fs:104-105`
- Beschreibung: Der Styleguide verlangt für Tappables zusätzlich `touch-action: manipulation`; `Button.view` setzt es nicht (in `styles.css` nur für einzelne Spezialklassen).
- Regel: `standards/frontend/styleguide.md:206-207`.

### D3 (niedrig) Icon-only-Knöpfe ohne zugänglichen Namen

- Commit: 897b13c (Bestand, der Commit macht genau diese Knöpfe touch-tauglich)
- Ort: `src/Client/DesignSystem/Button.fs:181-196` (`iconButton`, `editButton`, `deleteButton`), `:108` (nur `title`, kein `aria-label`)
- Beschreibung: `iconButton` setzt weder `Title` noch `aria-label`. Laut Outcome gibt es derzeit keine Aufrufer außerhalb von `Button.fs`; bei der ersten Nutzung fehlt dann der Name für Screenreader.
- Regel: `design-quality.md:395` ("ARIA labels on icon-only buttons").

### D4 (Hinweis) Bob und Quick Add mit gleicher Punktfarbe in der Historie

- Commit: b4b5cc3
- Ort: `src/Client/Components/Ledger/View.fs:1105-1106`
- Beschreibung: Bob ist laut `src/Server/Ledger/Events.fs` eine eigene Herkunft ("keine Ausprägung von QuickAdd"), wird in der Zeitleiste aber orange wie Quick Add gezeigt. Das Label "Bob" unterscheidet; die Farbe nicht. Ob das gewollt ist, steht nicht im Task.
- Regel: `standards/frontend/styleguide.md:52-53` (Farbe kommuniziert Rolle) als schwacher Bezug.

Positiv: 897b13c erfüllt `design-quality.md:192`, `:196-197` und `styleguide.md:206` (48 px mobil, ab `md` ohne Mindesthöhe), misst am echten Split-Editor bei 390 px und 1440 px, und das Outcome listet alle `Button.Small`-Verwendungen auf.

## 5. Kurzbewertung

Ein hoher Befund (B1), der die gesamte Fable.Remoting-API seit b4b5cc3 lahmlegt und lokal reproduziert ist; die Unit-Tests sichern das Fehlverhalten ab, ein Pipeline-Test fehlt. Zwei mittlere Befunde zur Bot-Schnittstelle (B2, B3). Der Rest sind Platzierungs- und Stilfragen. Die drei Agents liefern brauchbare Prüffragen, sind aber in Teilen veraltet (Dateistruktur, DaisyUI, Schriften, Tailwind 3) und widersprechen an mehreren Stellen den eigenen Rules; vor einer Reparatur des Frontmatters lohnt eine inhaltliche Überarbeitung.
