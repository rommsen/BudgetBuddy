---
id: ledger-r7dm2
title: "Testlücken aus dem QA-Review des Work-Laufs 2026-09-19 schließen"
status: backlog
type: chore
context: ledger
created: 2026-09-20
completed:
depends_on: []
blocks: []
tags: [tests, kassenbuch, abdeckung]
related_adrs: [0017, 0021, 0036]
related_research: []
prior_art: [ledger-vgfw3, ledger-fq3kd, ledger-t7v2q, ledger-w9skm, ledger-p9k3w]
---

## Why

Der QA-Review nach dem Work-Laufs vom 2026-09-19 (neun Aufgaben) hat die Tests der sechs
code-berührenden Aufgaben durchgesehen. Die Suite ist grün und die meisten neuen Tests sind
echt falsifizierend, aber es bleiben Lücken, in denen Verhalten still brechen kann. Der
eigentliche Defekt aus demselben Review (Umlaut-Faltung) liegt getrennt als `ledger-k3wqn`;
dieses Item sammelt die Testschuld.

Die Lücken, nach Risiko geordnet:

1. **Der Betragsfilter-Zweig ist eine zweite, fast ungetestete Implementierung.**
   `hasAmountFilter` gabelt `loadEntriesPage`, `countEntries`, `countDeletedEntries`,
   `loadAmountsForQuery` und `loadPayeeSuggestions` in einen SQL- und einen F#-Pfad über
   `loadAllMatching`. Nur `TotalCount` wird dort geprüft (AC6). Ungeprüft: `TotalAmount`,
   `DeletedCount`, Payee-Vorschläge (eigene Gruppierung über `List.countBy id`, ohne
   Tiebreak, also andere Reihenfolge als `GROUP BY ... ORDER BY cnt DESC`) und das Blättern
   über `safePage`. Fünf Funktionen, je zwei Implementierungen, eine davon getestet.
2. **Einseitige Betragsgrenzen.** `amountMatches` prüft beide Enden über `Option.forall`
   unabhängig; der einzige Test setzt `AmountMin = AmountMax`. Eine Umsetzung, die beide
   Enden verlangt oder die Vergleiche vertauscht, käme durch. Bei überwiegend negativen
   Beträgen ist die Min/Max-Richtung leicht zu verdrehen.
3. **Die `forceImportDuplicates`-Umschreibung in `src/Server/Api.fs` ist ungetestet.** Die
   drei Tests aus `ledger-fq3kd` sitzen auf `Ledger.CommandHandlers.pushComdirectImports`.
   Ungeprüft bleibt alles, was die API-Ebene neu tut, vor allem
   `ExistingEntryImportId = Shared.Domain.generateImportId p.TransactionId`: dieser Ausdruck
   muss byteweise dieselbe ImportId erzeugen wie der normale Push mit
   `prepareTransactions ... forceNewImportId = false`. Driften die beiden Ableitungen je
   auseinander, legt Force-Import wieder still eine zweite Buchung an, und alle Tests bleiben
   grün, weil sie den Kandidaten von Hand bauen statt ihn abzuleiten.
4. **`Stats.fs` hat keinerlei Testabdeckung**, also auch der neue `NoAccent`-Fall nicht
   (AC4 aus `ledger-w9skm` ist automatisiert nicht belegt).
5. **Der eigentliche Fehler aus `ledger-p9k3w` ist von keinem Test falsifizierbar.** Die
   beiden Chromium-Tests bilden `captureScreenshot`s Rumpf gegen eine synthetische Seite nach
   und messen PNG-Maße; keiner enthält eine `.bottom-sheet`, ein `100dvh` oder eine Aussage
   darüber, was im Bild sichtbar ist. Sie belegen, dass Playwright sich so verhält wie
   angenommen, nicht dass das Skript das tut.
6. **`NeedsReviewCount` bleibt bewusst ungefiltert, `DeletedCount` bewusst gefiltert.** Nur
   die `DeletedCount`-Hälfte ist getestet (AC8). Ein späteres "Vereinheitlichen" käme durch.
7. **Selbstverknüpfung im Erzeuger.** `apply` wirft bei einer Buchung, die auf sich selbst
   zeigt, und das ist getestet. `linkTransferCounterpart` hat keine entsprechende Sperre: löst
   `findEntryByLink` je auf die eigene Id auf, wirft `appendAndApply` und reißt den ganzen
   Spiegel-Lauf mit. Unwahrscheinlich, aber teuer, und billig abzusichern.
8. **Kein Test bindet `text-neon-purple` an `--color-neon-purple: #a855f7`.** Der Test zu
   `categoryColorClassName` wiederholt seine eigene Definition; ändert jemand die CSS-Variable,
   bleibt alles grün.
9. **Nichts pinnt, dass Handy-Zeile und PC-Tabelle dieselben Helfer benutzen.** `entryRow` und
   `entryTableRow` sind `private`; ein künftiges Wieder-Einbauen eines abweichenden Strings in
   nur eine der beiden bricht das Kriterium, ohne einen Test rot zu machen.

**Zum `TDD_SKIPPED` von `ledger-w9skm`:** die Begründung ("`dotnet test` kann Fable/React
nicht rendern") stimmt im Grundsatz, wurde aber zu früh gezogen. Die Suite hat für genau
diesen Fall bereits ein Muster: `FontFaceRegressionTests.fs` liest `styles.css` als Text,
`BackendPortGuardTests.fs` liest `Program.fs`, und `ledger-p9k3w` hat drei Commits später in
derselben Sitzung eine Quelltext-Zusicherung über `screenshot-route.mjs` geschrieben. Punkte 8
und 9 sind damit ohne Rendering erreichbar. Was wirklich Augenschein bleibt (liest sich das
Violett gut auf dunklem Grund, wirkt die Zeile als Liste statt als Karte), ist zu Recht per
Screenshot belegt.

## What

Die im Review definierten Tests schreiben. Der Review liegt als Volltext im Diary-Eintrag vom
2026-09-19 20:14 und nennt je Test Datei, Eingabe und erwartetes Ergebnis. Für Punkt 4 ist ein
kleiner Umbau nötig (`accentBarClassName : StatAccent -> string option` aus `Stats.view`
herausziehen, damit die Entscheidung eine reine Funktion wird) — derselbe Zug, den
`ledger-w9skm` für `entryRowClassName` bereits freiwillig gemacht hat.

Nebenher, ohne neue Abdeckung: acht vorbestehende `LedgerEntryPageQuery`-Literale in
`LedgerPageTests.fs` wurden beim Erweitern des Records mit sieben Feldern von Hand aufgefüllt,
obwohl derselbe Commit `baseQuery` genau dafür eingeführt hat. Auf `baseQuery` umstellen, damit
die nächste Felderweiterung kein 20-Zeilen-Diff wird. Und `import { chromium } from "playwright"`
in `screenshot-route.test.mjs` in die zwei Browser-Tests hineinziehen, damit eine Maschine ohne
Browser zwei Tests verliert statt aller 29.

**Beim Verfeinern zu entscheiden:** ob das als ein Zug läuft oder zerlegt wird. Punkt 1 allein
ist ein ordentliches Stück Arbeit; die Punkte 8 und 9 sind klein und unabhängig.

## Acceptance criteria

- [ ] To be defined during refinement.

## Notes

Aus dem QA-Review (`qa-milestone-reviewer`) nach dem Work-Lauf vom 2026-09-19. Der dort
ebenfalls gefundene echte Defekt liegt getrennt als `ledger-k3wqn`. Zwei Zahlen aus dem
Protokoll jenes Laufs sind vom Review korrigiert worden: die Suite meldet je nach Umgebung
941/6 statt 940/7, und `ledger-w9skm` hat sechs statt sieben Tests hinzugefügt.
