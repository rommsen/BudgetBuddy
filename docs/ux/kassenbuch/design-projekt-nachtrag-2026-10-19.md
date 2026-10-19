# Nachtrag für das Design-Projekt (2026-10-19)

Zum Einfügen in die `CLAUDE.md` des Claude-Design-Projekts „Gelesen UI-Mockups"
(`5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`).

**Warum dieser Umweg:** `CLAUDE.md` und `.claude/` sind im Design-Projekt reservierte
Pfade und lassen sich von außen nicht schreiben (siehe Nachtrag vom 2026-09-18). Roman gibt
den Text unten in einen Chat des Design-Projekts, der Agent pflegt seine Anweisungsdatei
selbst.

Grundlage im Repo: ADR 0049 „Ruhige Listen" (`.agentheim/knowledge/decisions/0049-ruhige-listen-farbe-als-signal.md`),
Styleguide §2 Abschnitt „Ruhige Listen" (`standards/frontend/styleguide.md`), umgesetzt in
`design-system-q7rkm` und `ledger-n4vte` (Repo-Commits `212be44`, `4be8bc1`, `7b4c900`).

---

## Neue Regel: Ruhige Listen (gilt ab Ablauf 4 und für jede Überarbeitung)

Roman hat am 2026-10-18 drei Fassungen derselben Kassenbuch-Liste verglichen (Mockup 1.9,
ruhige Tabelle, ruhige Karten) und die **ruhige Tabelle** gewählt. Vorbild ist die ältere
Ansicht „Import prüfen". Das Kassenbuch ist im Repo bereits so gebaut.

**Grundsatz:** In Listen und Tabellen mit wiederkehrenden Zeilen ist Farbe ein Signal,
keine Dekoration. Pro Zeile höchstens ein farbiger Akzent, und der meldet einen Zustand
(zu prüfen, ohne Kategorie). Die Hierarchie entsteht über Größe und Gewicht.

| Element | PC | Handy |
|---|---|---|
| Payee | Inter 500 14px `#e8e8f0` | Inter 500 15px `#e8e8f0` |
| Betrag | Mono 500 14px `tabular-nums` `#e8e8f0`, rechtsbündig; **nur Eingänge** `#00d4aa` | Mono 500 15px, sonst gleich |
| Datum | Mono 400 12px `#4a4a68` | nur im Gruppenkopf |
| Kategorie | Inter 12px `#7a7a98`; „Keine Kategorie" `#ff6b2c` | gleich |
| Memo | Inter 12px `#4a4a68`, eine Zeile, gekürzt | gleich |
| Zu prüfen | 3px-Kante `#ff1f8f` am linken Zeilenrand, dazu das Wort „zu prüfen" Inter 11px `#ff1f8f` in der Statusspalte | nur die 3px-Kante |
| Datums-Gruppenkopf | Mono 600 10px, `letter-spacing:.16em`, `#4a4a68`; **Tagessumme rechts, bündig unter der Betragsspalte** | Tagessumme rechts |
| Spaltenkopf | Mono 600 10px, `letter-spacing:.12em`, `#4a4a68`, Höhe 42px | entfällt |
| Zeilenhöhe | 44px | zweizeilig, `padding:11px 16px`, gleicher Abstand zwischen Zeile 1 und 2 in jeder Zeile |
| Kontostand | `#e8e8f0` (aktiv) oder `#7a7a98`; **nur negativ** `#ff3b5c` | gleich |

**Was damit wegfällt:**
- Violette Kategorien (`#a855f7`) in Listen. Violett bleibt für Kategorie nur außerhalb von
  Listen, z. B. im Detail-Sheet.
- Rote Ausgaben (`#ff3b5c`) in Listen. Rot bleibt für negative Kontostände, für einzeln
  stehende Beträge mit Gewicht (Detail-Kopf, Hero-Saldo) und für Fehler.
- Neongrüne Kontostände (`#00ff88`) und jeder Glow in Listen.
- Das Badge „Zu prüfen" und der 6 × 6px-Punkt aus Ablauf 1 in der Zeile. Die Bedeutung
  bleibt, die Form ist jetzt die Kante.
- Herkunfts-Badges wie „YNAB-Spiegel" in der Zeile. Herkunft steht in Detail und Historie.
- Der große Seitentitel mit Verlauf über Listen. Listen-Seiten haben einen schlanken Kopf
  (Space Grotesk 600 20px, ohne Verlauf, Untertitel klein und gedämpft, Aktionen rechts)
  und füllen die verfügbare Breite.

**Was bleibt:** Struktur und Verhalten aller Abläufe (Kontenspalte, Chips, Summenzeile,
Tastatur, Mehrfachauswahl, Wisch-Geste, Detail-Sheet, Historie). Rückmeldungen auf eine
Aktion dürfen leuchten (grünes „Geprüft" beim Wischen, Toasts), weil sie kurz und an eine
Handlung gebunden sind.

**Überträge:** Ein Übertrag ist kein offener Fall (ADR 0048). Er zeigt in der
Kategorie-Spalte „Übertrag" in `#7a7a98`, nie „Keine Kategorie".

**Zielbilder im Repo** (aus dem Sichttest, Fassung B):
`docs/ux/kassenbuch/mockups/ruhige-listen-b-pc-1440.png` und
`docs/ux/kassenbuch/mockups/ruhige-listen-b-handy-390x844.png`. Im PC-Bild steht der
Übertrag „Transfer : Tagesgeld ING" fälschlich als „Keine Kategorie"; maßgeblich ist der
Absatz oben. Die gebaute Fassung zeigen `docs/ux/kassenbuch/screenshots/*-ruhige-listen.png`.

## Auswirkung auf die bestehenden Tafeln

- **Abläufe 1 bis 3 und 7:** Die Farb- und Schriftwerte der Listenzeilen in diesen Tafeln
  sind überholt. Die Tafeln müssen nicht neu gezeichnet werden, solange sie nur als
  Verhaltens-Referenz dienen; wer sie überarbeitet, zieht die Zeilen auf die Tabelle oben.
- **Abläufe 4 bis 6** (abgenommen, Übergabe noch offen): vor der Übergabe die Listen-Screens
  auf die neue Regel ziehen, damit die Übergabe-Dokumente keine überholten Werte tragen.
- **Offener Punkt 1 aus dem Nachtrag vom 2026-09-18** („Abweichung" und „Zu prüfen" im
  selben Pink übereinander) entschärft sich: „Zu prüfen" ist in der Zeile jetzt die Kante,
  kein Badge mehr. Ob „Abweichung" als Badge bleibt, ist weiter offen.
- **Offener Punkt 3 aus demselben Nachtrag** (Werte in Ablauf 7 weichen ab): die
  Memo-Farbe `#4a4a68` und der Tabellenkopf 42px sind mit der neuen Regel entschieden; die
  PC-Summe ist neutral `#e8e8f0`, nicht mehr rot.

## Für die Übergabe-Dokumente

Übergaben ab sofort gegen die Regel oben schreiben und im Abschnitt „Fidelity" vermerken:
„Listenwerte nach ADR 0049 (Ruhige Listen); Bausteine im Repo: `Tokens.ListRoles`,
`Money.listAmount`, `Money.accountBalanceClass`, `Table.roleCell`/`amountCell`/
`calmHeaderCell`/`calmGroupRow`/`calmRow`, `PageHeader.calm`, `Primitives.Container.full`."
Der Styleguide-Abschnitt „8a. Ruhige Listen" in der App (`/styleguide`) zeigt die
Bausteine gerendert.
