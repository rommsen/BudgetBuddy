---
id: ledger-n3pqw
title: "Kassenbuch-Zeilen: die Kategorie-Farbe an die CSS-Variable binden und die geteilten Helfer von Handy-Zeile und PC-Tabelle festnageln"
status: done
type: chore
context: ledger
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [tests, kassenbuch, oberflaeche, abdeckung]
related_adrs: [0031, 0035]
related_research: []
prior_art: [ledger-w9skm]
---

## Why

Lücken 8 und 9 aus dem QA-Review nach dem Work-Lauf vom 2026-09-19 (erfasst als
`ledger-r7dm2`).

**Lücke 8:** kein Test bindet `text-neon-purple` an `--color-neon-purple: #a855f7`. Der
Test zu `categoryColorClassName` (`src/Tests/LedgerViewTests.fs:179`) vergleicht den Wert
mit `Client.DesignSystem.Tokens.Colors.neonPurple`, also mit seiner eigenen Definition,
und wiederholt sich damit selbst. Benennt jemand die Variable in
`src/Client/styles.css` um, erzeugt Tailwind die Utility `text-neon-purple` nicht mehr,
die Klasse fällt stillschweigend aus der Ausgabe, und alle Tests bleiben grün.

**Lücke 9:** nichts pinnt, dass Handy-Zeile und PC-Tabelle dieselben Helfer benutzen.
`entryRow` und `entryTableRow` in `src/Client/Components/Ledger/View.fs` sind `private`,
also unter `dotnet test` nicht aufrufbar. Ein künftiges Wieder-Einbauen eines
abweichenden Strings in nur eine der beiden Ansichten bricht AC1/AC2 aus `ledger-w9skm`,
ohne einen Test rot zu machen. Heute ist genau das schon halb der Fall:
`"ohne Payee"` steht zweimal im File (Z. 271 und Z. 378), einmal je Ansicht, während
`"ohne Kategorie"` über `categoryLabel` geteilt wird.

Die `TDD_SKIPPED`-Begründung von `ledger-w9skm` („`dotnet test` kann Fable/React nicht
rendern") stimmt im Grundsatz, wurde aber zu früh gezogen: die Suite hat für genau diesen
Fall ein Muster, nämlich `FontFaceRegressionTests.fs` (liest `styles.css` als Text) und
`BackendPortGuardTests.fs` (liest `Program.fs`). Beide Lücken sind damit ohne Rendering
erreichbar.

## What

Zwei kleine Züge im Produktionscode plus Tests in `src/Tests/LedgerViewTests.fs`:

1. `payeeLabel : LedgerEntryView -> string` neben `categoryLabel` einführen (Rückgabe:
   der Payee, sonst `"ohne Payee"`) und in `entryRow` wie `entryTableRow` benutzen, damit
   der Platzhaltertext wie der Kategorietext nur noch an einer Stelle steht.
2. Die Tests nach dem Muster der bestehenden Quelltext-Zusicherungen schreiben:
   `styles.css` und `View.fs` als Text lesen, `repoRoot` über `__SOURCE_DIRECTORY__` wie
   in `FontFaceRegressionTests.fs`.

## Acceptance criteria

- [ ] **Der Token zeigt auf eine Variable, die es gibt.** Ein Test in
      `src/Tests/LedgerViewTests.fs` liest `src/Client/styles.css` als Text, nimmt
      `Client.DesignSystem.Tokens.Colors.neonPurple`, schneidet das Präfix `"text-"` ab
      und erwartet, dass der `@theme`-Block eine Deklaration `--color-neon-purple:`
      enthält. Fängt eine Umbenennung der CSS-Variable, nach der Tailwind die Utility
      nicht mehr erzeugt und die Kategorie-Farbe still ausfällt.
- [ ] **Die Farbe ist die aus dem Entwurf.** Derselbe Test erwartet als Wert dieser
      Deklaration `#a855f7` (Handoff 1.1 und 1.9). Fängt eine stille Änderung der
      Palette an der Kategorie-Farbrolle vorbei: wer `#a855f7` ändern will, muss diesen
      Test mit ändern.
- [ ] **Die Prüfung greift wirklich auf den Token, nicht auf ein Literal.** Derselbe
      Test leitet den Variablennamen aus `Tokens.Colors.neonPurple` ab, nicht aus einem
      im Test hingeschriebenen `"neon-purple"`. Benennt jemand den Token um, ohne
      `styles.css` mitzuziehen, wird der Test rot.
- [ ] **Keine Farbliterale in der Kassenbuch-Ansicht.** Ein Test liest
      `src/Client/Components/Ledger/View.fs` als Text und erwartet, dass die Zeichenkette
      `"text-neon-"` dort nicht vorkommt: Farbklassen laufen ausschließlich über
      `Tokens.Colors.*` (ADR 0031).
- [ ] **Der Kategorietext steht genau einmal.** Derselbe Test erwartet, dass
      `"ohne Kategorie"` in `src/Client/Components/Ledger/View.fs` genau einmal vorkommt
      (in `categoryLabel`). Fängt ein Wieder-Einbauen eines abweichenden Platzhalters in
      nur eine der beiden Ansichten.
- [ ] **Der Payee-Text steht genau einmal.** Derselbe Test erwartet, dass
      `"ohne Payee"` genau einmal vorkommt (in dem neu eingeführten `payeeLabel`). Heute
      sind es zwei Vorkommen, eines je Ansicht; der Test ist vor dem Umbau rot.
- [ ] **Beide Ansichten benutzen die geteilten Helfer.** Ein Test schneidet aus
      `src/Client/Components/Ledger/View.fs` den Rumpf von `entryRow` (von
      `let private entryRow` bis zum nächsten `let` auf Spalte 0) und den von
      `entryTableRow` heraus und erwartet in **beiden**: `categoryLabel entry`,
      `payeeLabel entry` und `originBadge entry.Origin`. Fällt einer der drei Helfer in
      einer der beiden Ansichten einem abweichenden Ausdruck zum Opfer, wird der Test
      rot. Findet der Test einen der beiden Funktionsköpfe nicht, schlägt er mit der
      Meldung fehl, dass die Funktion umbenannt oder verschoben wurde (Muster:
      `screenshot-route.test.mjs`, Quelltext-Zusicherung über `captureScreenshot`).
- [ ] **Die Kategorie-Farbrolle wird in beiden Ansichten aus demselben Wert gespeist.**
      Derselbe Test erwartet `categoryColorClassName` im Rumpf von `entryRow` und im
      Rumpf von `tdTruncatedCategory`, und dass `entryTableRow`
      `tdTruncatedCategory (categoryLabel entry)` aufruft. Fängt eine PC-Tabelle, die
      ihre Kategoriezelle wieder direkt über `tdTruncated` rendert und damit die Farbe
      verliert.
- [ ] `dotnet build`, `dotnet test` und `npm run build` grün. Der bestehende
      `categoryColorTests`-Fall bleibt stehen (er prüft die andere Richtung: kein
      Hex-Literal im View-Code).

## Notes

**Herkunft der Testdefinitionen.** Der Elterntask `ledger-r7dm2` behauptet, der QA-Review
liege als Volltext im Diary-Eintrag vom 2026-09-19 20:14 vor. Das stimmt nicht; die
Kriterien oben sind aus `src/Client/Components/Ledger/View.fs`, `src/Client/styles.css`
und `src/Client/DesignSystem/Tokens.fs` abgeleitet.

Die Tests rendern nichts. `entryRow` und `entryTableRow` bleiben `private`: ihre
Sichtbarkeit zu ändern, nur damit ein Test sie erreicht, brächte nichts, weil unter
`dotnet test` ohnehin kein Fable/React-Laufzeitkontext existiert. Der Quelltext ist hier
das ehrlichere Prüfobjekt.

`repoRoot` wie in `src/Tests/FontFaceRegressionTests.fs`: zwei Ebenen über
`__SOURCE_DIRECTORY__`.

Keine Überschneidung mit den parallel verfeinerten Backend-Tasks
(`ledger-k3wqn`, `ledger-h4qtx`, `ledger-xk9wq`) und keine mit `design-system-j9xnd`
(`Stats.fs`).

## Outcome

Lücken 8 und 9 aus dem QA-Review geschlossen.

**Produktionscode** (`src/Client/Components/Ledger/View.fs`): `payeeLabel : LedgerEntryView ->
string` neben `categoryLabel` eingeführt (der Payee, sonst der Platzhaltertext) und in `entryRow`
wie `entryTableRow` eingesetzt, sodass der Platzhalter wie der Kategorietext nur noch an einer
Stelle im Modul steht.

**Tests** (`src/Tests/LedgerViewTests.fs`), nach dem Muster von `FontFaceRegressionTests.fs` und
`BackendPortGuardTests.fs` (Quelltext als Text, `repoRoot` über `__SOURCE_DIRECTORY__`):
die CSS-Bindung (`--color-neon-purple: #a855f7`, Variablenname aus dem Token abgeleitet), kein
`"text-neon-"` im View-Code, `"ohne Kategorie"` und `"ohne Payee"` je genau einmal, beide
Ansichten rufen `categoryLabel`, `payeeLabel` und `originBadge` auf, und beide speisen die
Kategorie-Farbrolle aus `categoryColorClassName`.

`dotnet build` 0 Warnungen, `dotnet test` 953 erfolgreich / 7 übersprungen, `npm run build` grün.

**Verifikation (Iteration 1, PASS), mit vier Mutationsnachweisen.** Der Verifier hat die Tests
nicht gelesen, sondern gebrochen und zugesehen, ob sie rot werden:

- CSS-Variable in einer Arbeitskopie zu `--color-neon-violet` umbenannt → rot.
- Wert auf `#b955f7` geändert → rot ("Wert #b955f7 statt #a855f7"); der Test liest den Wert
  tatsächlich aus der Deklaration, statt nur ihre Existenz zu prüfen.
- Token auf `"text-neon-violet"` gesetzt bei unveränderter CSS → rot. **Damit ist belegt, dass
  die Tautologie beseitigt ist**, die diese Aufgabe beanstandet hat: beide Seiten stammen aus
  verschiedenen Quellen (`Tokens.fs` und `styles.css`), nirgends steht `"neon-purple"` im Test.
- PC-Tabelle auf `tdTruncated (categoryLabel entry)` zurückgedreht → rot.

Zusätzlich hat er die Schnittgrenzen des Rumpf-Tests von Hand nachgerechnet (`entryRow`
253..330, nächstes Spalte-0-`let` bei 331; `entryTableRow` 371..412; `tdTruncatedCategory`
350..361): kein Schnitt verschluckt die Nachbarfunktion, keine Zusicherung kann aus dem falschen
Rumpf erfüllt werden. Das laute Scheitern ist belegt: Funktionskopf umbenannt →
`failwithf "Funktionskopf 'let private entryRow' nicht gefunden (umbenannt oder verschoben?)"`.
Das Vorher/Nachher für Kriterium 6 hat er selbst nachgezählt: `"ohne Payee"` zweimal in `HEAD~1`
(Z. 271 und 378, je eine Ansicht), einmal in `HEAD` (Z. 224, in `payeeLabel`).

### Drei Befunde des Verifiers, die kein Kriterium reißen, aber notiert gehören

1. **Kriterium 5 wurde durch eine Kommentar-Umschreibung grün, nicht durch eine Codeänderung.**
   `"ohne Kategorie"` stand im Code schon vorher genau einmal; rot war der Test nur wegen des
   Doc-Kommentars, der den String zitierte. Die Zählung über die ganze Datei misst also mehr als
   die gemeinte Eigenschaft: sie bindet auch die Prosa, und ein künftiger harmloser Kommentar,
   der den String nennt, macht den Test rot. Der Konstruktionsfehler liegt in der Formulierung
   des Kriteriums, nicht in der Umsetzung; der Worker hat die Vorgabe umgesetzt, wie sie
   dasteht. Die eigentliche Regression (ein abweichender Platzhalter, wieder in eine der beiden
   Ansichten eingebaut) fängt der Test weiterhin, und zwar doppelt: die Mutation liefert
   gleichzeitig "ohne Payee=2" und "entryRow ohne payeeLabel entry" aus dem Helfer-Test. Eine
   engere Zusicherung (nur Nicht-Kommentarzeilen, oder nur die beiden Ansichts-Rümpfe) hätte
   dieselbe Regression gefangen, ohne den Kommentar zu verbiegen.
2. **Die CSS-Regex sucht im ganzen Dateitext, nicht auf den `@theme`-Block eingegrenzt**, wie das
   Kriterium es formuliert. Heute existiert genau eine Deklaration (`styles.css:12`, innerhalb
   `@theme` 7..43), die gemeinte Regression wird also gefangen. Ein zusätzliches
   `--color-neon-purple` außerhalb von `@theme` (etwa in `:root`) könnte den Test künftig grün
   halten, obwohl Tailwind die Utility nicht mehr erzeugt. Kein Kriteriumsverstoß, ein möglicher
   Feinschliff.
3. **`IndexOf` matcht als Präfix:** eine Umbenennung nach `entryRowMobile` würde gefunden statt
   gemeldet. Der Schnitt trifft dann denselben Rumpf, es entsteht also kein falsches Grün über
   die gepinnte Eigenschaft, nur ein nicht gemeldeter Rename.

Der Verifier weist außerdem darauf hin, dass `TESTS_ADDED: 5` untertrieben ist: der Runner führt
sechs neue Fälle. Untertrieben, nicht übertrieben, ohne Wirkung auf das Verdikt.

**Eine Änderung nach der Verifikation, deklariert.** Der Verifier hat in zwei neu hinzugefügten
Doc-Kommentaren Umschrift statt Umlauten gefunden (`Zaehlt`, `schlaegt`) und sie als nicht
verdikt-relevant eingestuft. Der Conductor hat sie beim Integrieren auf `Zählt` und `schlägt`
korrigiert. Der integrierte Diff weicht damit um genau diese zwei Wörter in Kommentaren vom
verifizierten ab; an keiner Prüfung des Verifiers hängt etwas davon. Die übrige Umschrift in
`View.fs` ist Altbestand und nicht Sache dieser Aufgabe.

**Zur Historie:** ein erster Worker-Prozess für diese Aufgabe wurde vom Watchdog abgebrochen,
nachdem er die Arbeit gemacht, aber bevor er sie berichtet hatte. Der Wiederholungslauf im selben
Worktree hat das Ergebnis geprüft, neun im Testfile verstreute Umschreibungen des abgebrochenen
Laufs korrigiert und regulär berichtet. Ein Transportverlust zählt nicht als fehlgeschlagene
Verifikations-Iteration; dies ist Iteration 1.
