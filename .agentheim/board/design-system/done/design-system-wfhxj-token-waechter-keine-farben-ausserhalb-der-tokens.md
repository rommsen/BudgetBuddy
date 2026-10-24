

## Verifier note (iteration 1)

**REASONS:**
- Check 4/5: der Diff führt "Token-Wächter" als neuen BC-Begriff ein (Ausgaben und Dateikommentar von `scripts/token-guard.mjs`, Konvention "Feature-Code nutzt keine Farb- oder Schriftwerte außerhalb der Tokens" mit gezählter Ausnahmeliste), das `readmeDelta` ist aber `[]`. Die BC-README listet den Geschwister-Mechanismus **Tailwind-Klassen-Wächter** als eigenen Bullet in `## Ubiquitous language`; ohne Delta beschreibt sie nur einen der beiden Wächter.
- Checks 1 bis 3 bestanden: alle Kriterien belegt, `dotnet build` 0 Warnungen, `dotnet test` 1666/7 übersprungen/0 Fehler, `npm run build`, `guard:classes`, `guard:tokens` grün, `test:scripts` 176/176; Ausnahmeliste stimmt mit den echten Zeilen überein; Schreibregel-grep ohne Treffer.

**SUGGESTED_FIX:** README_DELTA-Op, die einen Bullet "**Token-Wächter**" in `## Ubiquitous language` direkt nach "Tailwind-Klassen-Wächter" einfügt: `scripts/token-guard.mjs`, `npm run guard:tokens`/`test:scripts`, die fünf Muster, null Toleranz außerhalb `src/Client/DesignSystem/`, gezählte `scripts/token-guard-exceptions.json` mit Regel für veraltete Einträge. Code unverändert.

**ITERATION_HINT:** likely-fixable

## Outcome

`scripts/token-guard.mjs` prüft alle `.fs` unter `src/Client/` (ohne obj/bin) auf fünf Muster: `hex-arbitrary`, `rgb-arbitrary`, `palette-farbe`, `inline-style` (style.color/backgroundColor/borderColor/fontSize) und `text-arbitrary-size`. Kommentarzeilen zählen nicht. Außerhalb von `src/Client/DesignSystem/` gilt null Toleranz. Innerhalb braucht jedes Muster je Datei einen Eintrag mit exakter Anzahl und Grund in `scripts/token-guard-exceptions.json`; ein Eintrag ohne Treffer oder mit abweichender Anzahl ist veraltet und lässt den Lauf scheitern. Exit 0 sauber, 1 Treffer oder veraltete Ausnahme, 2 Wächter kann nicht urteilen (Liste ungültig, Verzeichnis fehlt). Meldung je Treffer: Datei:Zeile, Muster, Treffer, plus der Satz "Optik kommt aus standards/frontend/styleguide.md und src/Client/DesignSystem/, nicht aus Tafel-Werten".

Echter Ausnahmestand (gezählt, weicht von der Task-Schätzung ab): Tokens.fs 8 Hex-Arbitrary (nicht 9), 6 rgb-Arbitrary, 7 `text-[Npx]` (nicht 8); Button.fs 1 Hex + 1 rgb; Loading.fs 1 Hex; Input.fs 3 `style.fontSize` und zusätzlich 2 rgb-Arbitrary (Fokusring Fehler/Erfolg); Card.fs 3 rgb-Arbitrary (in der Task nicht genannt). Alle mit Grund eingetragen.

Feature-Code: einziger Treffer war `src/Client/Components/Styleguide/View.fs:162` (Beschriftungstext "Body — text-[15px] md:text-base"). Ersetzt durch `prop.text ("Body: " + Tokens.FontSizes.body)`; der Beschriftungstext zeigt damit weiter dieselbe Klasse an, nur der Gedankenstrich ist einem Doppelpunkt gewichen. Es gibt keine Ausnahme für Feature-Code.

Dokumentation: `standards/frontend/styleguide.md` (Block "Harte Regel", steht in der Datei am Ende von §2 vor §3) nennt den Wächter als Durchsetzung; `.claude/rules/design-tokens.md` hat den neuen Abschnitt "Mechanische Prüfung" vor den Grep-Checks. `package.json`: `guard:tokens`. `test:scripts` nimmt den Test über den Glob `scripts/*.test.mjs` mit.

Tests (`scripts/token-guard.test.mjs`, 19): für alle sechs Fälle (Hex, rgba-Schatten, Paletten-Farbe, style.color, style.fontSize, text-[13px]) je ein Fixture-Test außerhalb von DesignSystem (Exit 1, Datei und Zeile, Hinweissatz) und einer unter DesignSystem (ohne Eintrag Exit 1, mit Eintrag 0); veraltete Ausnahme; zu kleine Anzahl; Token-Klassen und `min-h-[36px]`/`w-[420px]` ohne Treffer; Kommentare; ungültige Liste Exit 2; obj/bin ignoriert; Live-Test gegen den echten Baum mit Exit 0.

Gelaufen aus dem Worktree: `dotnet build` 0 Warnungen/0 Fehler; `dotnet test` 1666 bestanden, 7 übersprungen, 0 Fehler; `npm run build` ok; `npm run guard:classes` sauber; `node --test --test-concurrency=1 scripts/*.test.mjs` 176 von 176 bestanden; `npm run guard:tokens` Exit 0. Selbstcheck gegen 480a62c ohne Treffer.
