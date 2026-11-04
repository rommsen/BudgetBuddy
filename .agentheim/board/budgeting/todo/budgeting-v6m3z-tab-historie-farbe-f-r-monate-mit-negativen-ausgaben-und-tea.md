---
id: budgeting-v6m3z
title: "Tab Historie ruhig und ablesbar: Monat mit Erstattungsüberschuss neutral mit Plus, Bewegungsbeträge neutral mit Vorzeichen, Wertzeile über den Ausgaben-Balken bei Maus, Tipp und Tastatur"
status: todo
type: feature
context: budgeting
created: 2026-11-03
completed:
depends_on: [budgeting-x4k7p, design-system-w7c2h, budgeting-t6p8z]
blocks: [budgeting-h7q4v]
tags: [budget, historie, design, entscheidung]
related_adrs: [0049, 0075]
related_research: []
prior_art: [budgeting-dyn8g]
---

## Why
design-quality hat am 2026-11-03 zwei Punkte im Tab Historie gefunden, die eine Auslegung von Roman brauchten. Roman hat sie am 2026-11-04 entschieden. Dazu kommt ein Wunsch von Roman aus derselben Sitzung: Die Ausgaben-Balken zeigen nur gerundete Beträge, den genauen Wert kann man weder mit der Maus noch am Handy ablesen.

## What
**Entscheidungen (Roman, 2026-11-04)**

1. **Monat mit Erstattungsüberschuss in den Ausgaben-Balken:** neutral statt rot. Heute ist der Ausgabenwert (`spendingBars` in `src/Shared/Budgeting.fs`, Ausgaben = −Aktivität) in so einem Monat negativ und der Betrag steht in `text-neon-red` (`monthBarAmountClass` in `src/Client/DesignSystem/Stats.fs`). Künftig steht er in `text-secondary` wie die anderen Balkenbeträge, mit Plus als Vorzeichen: Ausgaben −85 erscheinen als "+85". Grund: Auf der Budget-Seite heißt Rot "Problem" (Verfügbar im Minus, ADR 0075). Ein Monat, in dem mehr zurückkam als ausgegeben wurde, ist kein Problem.
2. **Die Bewegungsliste im Tab Historie ist eine ruhige Liste (ADR 0049).** Beträge neutral in `text-primary` mit Vorzeichen ("+20,00 €", "−20,00 €"), kein Teal. Das Stand-Badge ist der einzige Farbakzent der Zeile. Heute nutzt `historyLineView` (`src/Client/Components/Budget/InspectorView.fs`) `Money.listAmount`, das positive Beträge in Teal setzt. Die Regel gilt auch für die allgemeine Historie (budgeting-h7q4v), die dieselben Zeilenbausteine nutzt.

**Wertzeile über den Ausgaben-Balken**

3. Über den Balken von `Stats.monthBars` steht eine feste Wertzeile. Steht die Maus über einem Balken, hat ein Balken den Tastaturfokus oder wurde er am Handy angetippt, zeigt die Zeile Monat und genauen Betrag dieses Balkens, beim laufenden Monat mit dem Zusatz, dass er noch läuft (Inhalt wie das heutige `aria-label` aus `spendingMonthBar`, etwa "Oktober 2026 · 1.234,56 €"). Der gewählte Balken ist leicht hervorgehoben. Ohne Auswahl ist die Zeile leer, ihre Höhe bleibt reserviert, damit nichts springt. Die Zeile läuft nie über den Rand des Sheets.
4. Jeder Balken ist fokussierbar. Der Tipp am Handy setzt den Fokus, ein Tipp daneben nimmt ihn wieder weg. Vorschlag zur Umsetzung: allein über CSS (`:hover`, `:focus-within`, etwa eine absolut positionierte Wertzeile je Balken oder `has-[…]` am Container), ohne Zustand im Elmish-Model. Braucht es doch Zustand, gehört er in den DS-Baustein und nicht in den Inspektor. Der Fokus am Balken folgt der Fokus-Regel aus design-system-w7c2h (Fokus ohne Kasten), keine eigene Fokus-Optik.
5. Der Abschnitt "Monatsbalken" in `/styleguide` zeigt die Wertzeile.

## Acceptance criteria
- [ ] (reine Funktion, .NET) Balkenbetrag: Ausgaben −85 ergeben "+85", Ausgaben 1.234,5 ergeben "1.235", 0 ergibt "0". Der bestehende Test "negativ" in `src/Tests/BudgetHistoryClientTests.fs` wird angepasst.
- [ ] (reine Klassenfunktion, .NET, Vorbild `src/Tests/RuhigeListenTests.fs`) Der Balkenbetrag eines Monats mit Erstattungsüberschuss trägt `text-secondary` und kein Rot.
- [ ] (reine Funktion, .NET) Betrag einer Bewegungszeile: positiv mit "+", negativ mit "−", Klasse neutral in `text-primary`, kein Teal. Gilt für alle drei Herkünfte (Verschieben, Zuweisen, Targets anwenden).
- [ ] (reine Funktion, .NET) Text der Wertzeile je Balken: vergangener Monat mit genauem Betrag, laufender Monat mit Zusatz "läuft noch", Monat mit Erstattungsüberschuss mit "+".
- [ ] (Browser, `[fokus]`) Fokus auf einem Balken (per Tipp oder Tab-Taste) zeigt dessen Wert in der Wertzeile, Fokus auf einem anderen Balken wechselt den Wert, Fokus weg leert die Zeile.
- [ ] (Browser, `[layout]`) Die Wertzeile hält ihre Höhe mit und ohne Auswahl, und am Handy (390 px) bleibt sie bei Auswahl des ersten und des letzten Balkens innerhalb des Sheets.
- [ ] Die bestehenden Browser-Tests zur Bewegungszeile in `scripts/budget-page.test.mjs` bleiben grün.
- [ ] README budgeting: die beiden Farbentscheidungen und die Wertzeile sind beschrieben.
- [ ] Styleguide `standards/frontend/styleguide.md`, Abschnitt Ruhige Listen: die Bewegungslisten der Budget-Historie sind als ruhige Listen genannt.
- [ ] Der Abschnitt "Monatsbalken" in `/styleguide` zeigt die Wertzeile mit gewähltem Balken. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Entscheidungen: Roman, 2026-11-04, beide Male der Vorschlag aus dem design-quality-Review. Die Wertzeile hat Roman gegen einen schwebenden Tooltip gewählt (kein neuer Positionierungs-Baustein, kann am Sheet-Rand nicht überlaufen, gleich für Maus und Touch).

budgeting-h7q4v wartet auf diese Task, damit die allgemeine Historie gleich mit neutralen Beträgen gebaut wird.
