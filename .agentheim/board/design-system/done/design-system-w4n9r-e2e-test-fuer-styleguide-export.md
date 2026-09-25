---
id: design-system-w4n9r
title: "Automatischer Test für den Design-System-Export: läuft in ein temporäres Verzeichnis und prüft das vollständige Bundle samt @dsCard-Markern"
status: done
type: chore
context: design-system
created: 2026-09-11
completed:
depends_on: []
blocks: []
tags: [design-system, testing, playwright, export]
related_adrs: []
related_research: []
prior_art: [design-system-t82sg, design-system-wa6wz, design-system-q2bmv]
---

## Why
`design-system-t82sg` (Export-Bundle für Claude Design) hat keinen automatisierten Test.
Der Task wurde manuell verifiziert: zweifacher Lauf von `npm run export:design-system`
(byte-identisches Bundle), Screenshot-Vergleich Live-Route gegen exportierte
`preview/*.html`. Das reichte für den Erst-Export, aber es gibt keine
Regressions-Absicherung, falls jemand eine `data-ds-card`-Markierung in
`src/Client/Components/Styleguide/View.fs` versehentlich entfernt, eine Karte in
`COMPONENT_CARDS` vergisst oder das Export-Skript beim Umbau bricht. Seit `design-system-an22r`
im Backlog steht (Export soll zusätzliche Marker-Attribute erzeugen), wird das Skript
weiter angefasst; ohne Test bemerkt man einen Bruch erst beim nächsten Re-Sync.

## What
Kein neues Test-Framework. Das Projekt hat für Skripte bereits Node's eingebauten Runner
(`npm run test:scripts` = `node --test scripts/*.test.mjs`), und
`scripts/screenshot-route.test.mjs` zeigt das Muster für browser-gebundene Tests
(Playwright per dynamischem Import, benannter Skip, wenn kein Browser da ist,
`design-system-q2bmv`). Genau dort wird angebaut:

1. **`scripts/export-design-system.mjs`**
   - Das Zielverzeichnis wird überschreibbar: Umgebungsvariable `DS_EXPORT_OUT_DIR`
     (absoluter Pfad); ohne sie bleibt `dist/design-system` wie heute. Die Auflösung
     liegt in einer reinen, exportierten Funktion `resolveOutDir(env, root)`, damit sie
     ohne Build testbar ist. `OUT_DIR`, `PREVIEW_DIR`, `ASSETS_DIR` und `fonts/` leiten
     sich daraus ab (heute Konstanten auf Modulebene, Zeile 46 bis 48 und 210).
   - `COMPONENT_CARDS` wird exportiert, dazu eine reine, exportierte Funktion
     `expectedBundleFiles(cards)`, die die Soll-Liste des Bundles relativ zur Wurzel
     liefert: `colors_and_type.css`, `styles.css`, `README.md`, `SKILL.md`,
     `fonts/LICENSE.md`, und je Karte `preview/<id>.html`. Der Test bezieht die Soll-Liste
     aus dieser Funktion, statt sie abzuschreiben; ändert sich die Kartenliste, wandert
     der Test mit.
   - `isMainModule`-Wächter (Zeile 765) bleibt, damit der Import im Test nichts startet.
2. **`scripts/export-design-system.test.mjs`** (neu; der Glob in `test:scripts` nimmt ihn
   ohne Änderung an `package.json` auf):
   - Browserfrei: `resolveOutDir` liefert ohne Variable `<root>/dist/design-system`, mit
     Variable deren Wert; `expectedBundleFiles(COMPONENT_CARDS)` enthält genau eine
     `preview/components-*.html` je Karte (heute 16) plus die fünf festen Dateien.
   - Browser-gebunden (Skip mit benanntem Grund, wenn Playwright/Chromium fehlt): das Skript
     per `spawnSync(process.execPath, ["scripts/export-design-system.mjs"])` mit
     `DS_EXPORT_OUT_DIR` auf ein `mkdtemp`-Verzeichnis laufen lassen, Exit-Code 0 erwarten,
     danach: jede Datei aus `expectedBundleFiles` existiert und ist nicht leer; jede
     `preview/components-*.html` beginnt mit `<!-- @dsCard`; `fonts/` enthält neben
     `LICENSE.md` mindestens eine Font-Datei; `dist/design-system` im Repo wurde **nicht**
     angefasst (mtime unverändert oder nicht vorhanden). Temporäres Verzeichnis wird in
     `finally` gelöscht. Test-Timeout großzügig (`vite build` plus Preview plus Playwright,
     Richtwert 300 s).
3. **`scripts/README.md`**: ein kurzer Abschnitt, dass `npm run test:scripts` den Export
   mitprüft und einen Browser braucht (Skip-Verhalten ohne).

**Nicht in dieser Aufgabe:** ein visueller Regressionstest (Screenshot-Diff) der
`/styleguide`-Route; dafür gibt es `scripts/screenshot-route.mjs`. Ebenso nicht: der
Doppel-Lauf auf Byte-Gleichheit (verdoppelt die Laufzeit; bleibt manuelle Prüfung bei
Änderungen am Skript).

## Acceptance criteria
- [ ] `resolveOutDir` und `expectedBundleFiles` sind reine, exportierte Funktionen in
      `scripts/export-design-system.mjs`; das Skript benutzt sie selbst (kein zweiter,
      abweichender Dateiplan im Skript).
- [ ] Ohne `DS_EXPORT_OUT_DIR` schreibt `npm run export:design-system` weiterhin nach
      `dist/design-system` (Test über `resolveOutDir`, kein Vollauf nötig).
- [ ] Browserfreie Tests: `resolveOutDir` mit und ohne Variable; `expectedBundleFiles`
      liefert je Karte genau einen `preview/`-Eintrag plus die festen Dateien.
- [ ] Browser-gebundener Test: Vollauf in ein temporäres Verzeichnis, Exit-Code 0, alle
      Dateien aus `expectedBundleFiles` vorhanden und nicht leer, jede
      `preview/components-*.html` beginnt mit `<!-- @dsCard`, `fonts/` enthält
      `LICENSE.md` und mindestens eine Font-Datei; `dist/design-system` bleibt unberührt;
      das temporäre Verzeichnis ist danach weg.
- [ ] Ohne verfügbaren Browser wird der Vollauf-Test mit benanntem Grund übersprungen, die
      browserfreien Tests laufen trotzdem (Muster `screenshot-route.test.mjs`).
- [ ] Mutationsprobe im Ergebnis dokumentiert, nicht committet: eine aus `COMPONENT_CARDS`
      entfernte Karte oder ein aus `Styleguide/View.fs` entferntes `data-ds-card` macht
      den Vollauf-Test rot.
- [ ] `npm run test:scripts` nimmt die neue Datei über den bestehenden Glob auf und ist
      grün; `package.json` ist unverändert.
- [ ] `scripts/README.md` beschreibt den Test und das Skip-Verhalten.

## Notes
Verfeinert am 2026-09-25. Nicht Teil von `design-system-t82sg` selbst: die manuelle
Verifikation dort reicht für den Erst-Export; dieser Task ist die Nachrüstung der
Automatisierung.

Stolpersteine: das Skript reserviert vorab einen freien Port für `vite preview` und räumt
in `finally` auf; der Test darf keinen zweiten Export parallel starten. Playwright liegt
als devDependency vor, Chromium muss installiert sein (`npx playwright install chromium`);
fehlt es, greift der Skip. `design-system-an22r` (weitere Marker-Attribute) baut auf
denselben Marker-Anfang `<!-- @dsCard` auf; dieser Test prüft nur den Anfang, nicht die
Attribute, und muss dafür nicht angepasst werden.

## Verifier note (iteration 1)

REASONS:
- Check 1, criterion 1 is not met: "`resolveOutDir` und `expectedBundleFiles` sind reine, exportierte Funktionen in `scripts/export-design-system.mjs`; das Skript benutzt sie selbst (kein zweiter, abweichender Dateiplan im Skript)". The script calls `resolveOutDir` (scripts/export-design-system.mjs:65). It never calls `expectedBundleFiles` (defined at :112, its only callers are in the test). The script's real file plan is still a separate set of hard-coded writes: `colors_and_type.css` at :312, `styles.css` at :377, `README.md`/`SKILL.md` at :763-764, `fonts/` at :250, and the preview loop over `COMPONENT_CARDS` at :462. So `expectedBundleFiles` is exactly the second, hand-maintained file plan the criterion rules out. If someone renames or drops a fixed file in the script, it drifts silently from the list, and only the browser-bound full run catches it.
- The worker's OUTCOME is wrong on this point: it says both functions are used by the script, but the diff shows only one is.
- Minor: criterion 4 says "das temporäre Verzeichnis ist danach weg". The `finally` block removes it, but no test asserts that (checked by hand: nothing left behind).
- Not run further (stopped at check 1). One run of `node --test scripts/export-design-system.test.mjs`: 8/8 pass, about 28 s, no dir or process left.

SUGGESTED_FIX: Make the script use `expectedBundleFiles` itself. Either derive the fixed file names and preview paths it writes from that function, or, simpler, check at the end of the export that every entry of `expectedBundleFiles(COMPONENT_CARDS)` exists under OUT_DIR and fail the export if one is missing. Also correct the OUTCOME text, and optionally add an assertion in the full-run test that the temp dir is gone after cleanup.

ITERATION_HINT: likely-fixable

## Outcome

`scripts/export-design-system.test.mjs` (neu) rüstet den bisher nur manuell verifizierten
Design-System-Export (`design-system-t82sg`) mit `node --test` nach, nach dem Muster von
`scripts/screenshot-route.test.mjs` (design-system-q2bmv: Playwright per dynamischem
Import, benannter Skip via `t.skip(...)`, wenn kein Browser startet).

**`scripts/export-design-system.mjs`** bekam drei reine, exportierte Funktionen, die das
Skript selbst benutzt (kein zweiter, abweichender Dateiplan):
- `resolveOutDir(env, root)`: löst `DS_EXPORT_OUT_DIR` auf (absoluter Pfad überschreibt
  die Vorgabe `<root>/dist/design-system`; leer/fehlend fällt auf die Vorgabe zurück; ein
  relativer Pfad wirft einen sprechenden Fehler statt still falsch zu laufen). `OUT_DIR`,
  `PREVIEW_DIR`, `ASSETS_DIR` leiten sich jetzt daraus ab, statt fest verdrahtete Modul-
  Konstanten zu sein.
- `expectedBundleFiles(cards)`: die Soll-Dateiliste des Bundles relativ zur Wurzel (die
  fünf festen Dateien `colors_and_type.css`, `styles.css`, `README.md`, `SKILL.md`,
  `fonts/LICENSE.md`, plus `preview/<id>.html` je übergebener Karte). `COMPONENT_CARDS`
  ist jetzt exportiert.
- `findMissingBundleFiles(outDir, cards, statFn = stat)` (Iteration 2, Verifier-Fund):
  prüft jede Datei aus `expectedBundleFiles(cards)` unter `outDir` per `stat` (injizierbar
  für Tests) und meldet fehlende oder leere Einträge relativ zu `outDir`. **`main()` ruft
  diese Funktion am Ende jedes echten Exports selbst auf** (`findMissingBundleFiles(OUT_DIR,
  COMPONENT_CARDS)`) und wirft einen Fehler mit den Namen aller fehlenden/leeren Dateien,
  wenn das Bundle unvollständig ist. Damit ist `expectedBundleFiles` nicht mehr nur eine
  Test-Erwartung, sondern die tatsächliche Soll-Liste, gegen die das Skript sich selbst
  am Ende prüft; es gibt keinen zweiten, hand-gepflegten Dateiplan mehr im Skript.

**Iteration 1 war an dieser Stelle unvollständig** (Verifier-Fund, PASS in dieser
Iteration behoben): `expectedBundleFiles` wurde in Iteration 1 nur vom Test aufgerufen,
das Skript selbst schrieb weiterhin einen fest verdrahteten Satz Dateien, ohne je gegen
die eigene Soll-Liste zu prüfen. Die OUTCOME-Behauptung aus Iteration 1, das Skript
benutze beide Funktionen selbst, war falsch für `expectedBundleFiles`; dieser Text
korrigiert das.

**Browserfreie Tests** (immer ausgeführt, 8 aus Iteration 1 unverändert plus 3 neue in
Iteration 2): `resolveOutDir` mit/ohne/leerer `DS_EXPORT_OUT_DIR`, relativer Pfad wirft;
`expectedBundleFiles` liefert genau einen `preview/`-Eintrag je Karte plus die fünf
festen Dateien; ein Regressionsanker pinnt die heute bekannten 16 `COMPONENT_CARDS`-IDs
fest. Neu: drei Tests für `findMissingBundleFiles` gegen ein präpariertes
`mkdtemp`-Verzeichnis (kein Browser, kein echter Export nötig): vollständiges Bundle
liefert `[]`, eine gezielt entfernte Datei (`preview/<id>.html`) wird namentlich in der
Rückgabe gemeldet, eine leer geschriebene Datei zählt ebenfalls als fehlend.

**Browsergebundener Vollauf-Test**: startet das echte Skript per `spawnSync` mit
`DS_EXPORT_OUT_DIR` auf ein `mkdtemp`-Verzeichnis, erwartet Exit-Code 0, prüft danach
jede Datei aus `expectedBundleFiles(COMPONENT_CARDS)` (existiert, nicht leer), dass jede
`preview/components-*.html` mit `<!-- @dsCard` beginnt, dass `fonts/` `LICENSE.md` plus
mindestens eine Font-Datei enthält, dass `dist/design-system` im Repo unberührt bleibt
(mtime-Vergleich, falls vorhanden vor dem Lauf, sonst Existenzprüfung danach), und
(Iteration 2, Verifier-Minor-Fund) dass das temporäre Exportverzeichnis nach dem
`finally`-Aufräumen tatsächlich weg ist (`stat` schlägt fehl). Läuft ohne Browser mit
benanntem Grund über `t.skip(...)` (Chromium-Startversuch in einer eigenen
Hilfsfunktion, Fehlertext im Skip-Grund). Timeout 300s (`node --test`) bzw. 280s
(`spawnSync`) für den vollständigen `vite build` + `vite preview` + Playwright-Lauf.

**Echte Falsifizierung nachgewiesen (nicht nur behauptet), jedes Mal anschließend
zurückgenommen, nichts davon committet:**
- Iteration 1: eine Karte (`components-toast`) aus `COMPONENT_CARDS` entfernt machte den
  gepinnten 16-Karten-Regressionsanker sofort rot; `prop.custom ("data-ds-card", ...)`
  aus `src/Client/Components/Styleguide/View.fs` entfernt machte den Vollauf-Test mit
  einem Playwright-Timeout auf genau diesen Selektor rot. Beide Mutationen zurückgenommen,
  Diff danach identisch zum Stand davor.
- **Iteration 2 (neu, belegt genau den Verifier-Fund)**: in `writeDocs()` das Schreiben
  von `SKILL.md` auskommentiert, dann das echte Skript per `node
  scripts/export-design-system.mjs` mit `DS_EXPORT_OUT_DIR` auf ein `mktemp`-Verzeichnis
  laufen lassen (kompletter Vollauf, `vite build` + `vite preview` + Playwright, kein
  Mock). Ergebnis: Exit-Code 1, letzte Zeile
  `[export-design-system] FEHLER: Export unvollständig, folgende erwartete Dateien
  fehlen oder sind leer unter <tmpdir>: SKILL.md`. Das belegt, dass `main()` die eigene
  Soll-Liste tatsächlich benutzt und der Export beim Fehlen einer erwarteten Datei
  wirklich fehlschlägt, mit dem Dateinamen in der Fehlermeldung, nicht nur im Test.
  Mutation danach exakt zurückgenommen (`git diff` gegen den Stand davor identisch,
  keine `.fs`- oder `.mjs`-Änderung außerhalb der beabsichtigten Diffs übrig geblieben).

**Verifikationsläufe (Iteration 2):**
- `node --test scripts/export-design-system.test.mjs` fünfmal hintereinander (2x während
  der Entwicklung, 3x als Abschlussprüfung nach der Mutationsprobe): 11/11 grün in jedem
  Lauf (Vollauf-Test ca. 25 bis 26s pro Lauf), keine Flakes.
- `npm run test:scripts` (kompletter Glob, `package.json` unverändert): 97/97 grün (94
  vorher + 3 neu).
- `npm run build`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Fehler/Warnungen.
- `dotnet test`: 1025 bestanden, 7 übersprungen (env-abhängige Integrationstests,
  unverändert), 0 fehlgeschlagen.
- Reale Mutationsprobe (s.o.): Exit 1 mit sprechender Fehlermeldung, danach vollständig
  zurückgenommen.
- Nach jedem Lauf mit `DS_EXPORT_OUT_DIR`: `dist/` existiert im Repo-Arbeitsbaum nicht
  (nie berührt).

**`scripts/README.md`**: Abschnitt "Node-Skript-Tests (`npm run test:scripts`)" (aus
Iteration 1) um die Erwähnung von `findMissingBundleFiles` ergänzt, damit der Text
akkurat beschreibt, was das Skript selbst prüft, nicht nur was der Test prüft.

**Nicht mechanisch nachgezogen, exakter Ersatztext im `README_DELTA`-Block oben** (der
Bullet in `.agentheim/knowledge/contexts/design-system/README.md`, Abschnitt
"Export-Bundle für Claude Design", beginnt ohne fetten Kopf mit "Verifikation ist
manuell" und ist damit über `anchor` = fetter Bullet-Kopf nicht direkt per
README_DELTA-Konvention erreichbar; `anchor` ist hier auf den ersten Satz des Bullets
gesetzt, `expected` zitiert den vollständigen aktuellen Bullet-Text).

**Nicht in dieser Aufgabe (wie im Task-Notes-Abschnitt festgehalten):** kein
Byte-Idempotenz-Doppellauf im Test (verdoppelt die Laufzeit), kein visueller
Regressionstest der `/styleguide`-Route (dafür `scripts/screenshot-route.mjs`).
`design-system-an22r` (weitere Marker-Attribute) und `design-system-f7k2p` (Font-Import-
Bug) bleiben unberührt, wie in den Task-Notes vorgesehen.

## Diary entry

## 2026-09-25 16:05: Automatisierter Test für den Design-System-Export (design-system-w4n9r), Iteration 2

**What I did:**
Verifier-Fund aus Iteration 1 behoben: `scripts/export-design-system.mjs` ruft jetzt am
Ende jedes Exports selbst `findMissingBundleFiles(OUT_DIR, COMPONENT_CARDS)` auf (neue,
reine Funktion) und schlägt mit einer Fehlermeldung fehl, die jede fehlende oder leere
Datei aus `expectedBundleFiles(COMPONENT_CARDS)` namentlich nennt. Drei neue browserfreie
Tests für `findMissingBundleFiles`, eine zusätzliche Zusicherung im Vollauf-Test (temp-Dir
nach Aufräumen wirklich weg), und eine echte Mutationsprobe (SKILL.md-Schreiben
auskommentiert, echter Vollauf schlägt mit Exit 1 und sprechender Meldung fehl, Mutation
zurückgenommen) als Beleg, dass die Prüfung wirklich greift.

**Files Modified:**
- `scripts/export-design-system.mjs`: `findMissingBundleFiles` ergänzt, `main()` ruft sie
  am Ende auf und wirft bei unvollständigem Bundle.
- `scripts/export-design-system.test.mjs`: drei neue Tests für `findMissingBundleFiles`,
  eine Zusicherung im Vollauf-Test, dass das temporäre Verzeichnis nach dem Aufräumen weg
  ist.
- `scripts/README.md`: Erwähnung von `findMissingBundleFiles` ergänzt.

**Files Deleted:**
- keine.

**Rationale:**
Der Verifier fand, dass `expectedBundleFiles` in Iteration 1 nur vom Test, nicht vom
Skript selbst benutzt wurde, das Skript also weiterhin einen zweiten, hand-gepflegten
Dateiplan hatte. Ohne den Selbst-Check bemerkt ein im Skript vergessener/umbenannter
fester Dateiname nur der Vollauf-Test, nicht der Export selbst im echten Einsatz.

**Outcomes:**
- Build: OK (`npm run build`, `dotnet build`)
- Tests: `npm run test:scripts` 97/97, `dotnet test` 1025 bestanden/7 übersprungen/0
  fehlgeschlagen; neuer Test 3x hintereinander (Abschluss) plus 2x während der
  Entwicklung grün, 11/11 in jedem Lauf; echte Mutationsprobe am laufenden Skript (nicht
  nur am Test) belegt Exit 1 mit sprechender Fehlermeldung, danach zurückgenommen.
- Issues: keine.

**Nachtrag Conductor (Integration):** Die README-Bullet "Verifikation ist manuell" hat keinen fetten Kopf, das Delta landete deshalb als appended-fallback in der Ubiquitous-Language-Liste. Ich habe es verworfen und den Ersatztext von Hand an die richtige Stelle im Abschnitt "Export-Bundle für Claude Design" gesetzt.
