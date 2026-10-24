# Vision: BudgetBuddy

> **Revision 2026-09-06.** Löst die Companion-Vision vom 2026-06-01 ab (ADR 0001 →
> ADR 0012). Der Companion-Teil (Bank→YNAB schnell und fehlerfrei) bleibt vollständig
> gültig; er ist jetzt der Anfang eines längeren Weges, nicht mehr dessen Grenze.

## Purpose
BudgetBuddy wird das **führende System für unsere Finanzen**, Fähigkeit für Fähigkeit.
YNAB bleibt so lange Projektion und Fallback, wie BudgetBuddy eine Fähigkeit noch nicht
hat. Der Kern ist ein eigenes, dauerhaftes **Kassenbuch** über alle Konten, mit einer
Historie, die wirklich auditierbar ist: jede Änderung an einer Buchung ist nachvollziehbar
nach Zeitpunkt und Herkunft (BB-Regel, Roman in BB, Roman in YNAB, Bank-Import).

Heute ist BudgetBuddy ein Companion: Comdirect-Import, regelbasierte Kategorisierung,
Duplikat-Schutz, Push nach YNAB, Quick Add für Bar-Ausgaben. Das bleibt und ist bereits
der erste Fall des Musters, das die neue Vision trägt: **BB entscheidet, YNAB wird
nachgezogen.** Was fehlt, ist das Gedächtnis: BB vergisst heute jede Buchung nach dem Push.

Gebaut von und für Roman als Daily Driver (Frau nutzt mit). Leitprinzip unverändert:
**fertiges Tool, kein Hobby-Projekt. Was nicht real gebraucht wird, fliegt raus.**

## Users
- **Roman** — primärer Nutzer. Zieht Comdirect-Transaktionen, kategorisiert, pflegt Regeln,
  bearbeitet Buchungen nach (Kategorie, Splits, Überträge), budgetiert am Monatsersten.
  PC für Planung und Nacharbeit, Handy für Lesen, Kategorisieren und Bar-Eingabe.
- **Frau** — erfasst Bar-Ausgaben (Quick Add), bestellt bei Amazon (Buchungen, die Roman
  ohne Kontext sieht), liest mit. Keine Planänderungen.

Single-User-Modell mit gemeinsamem Konto. Kein Multi-Tenant, keine Rollen, kein Konfliktmodell.

## The problem
1. **Nicht alles unter einem Dach.** YNAB kennt Comdirect, Bar und die ING-Konten, aber
   weder die Depots (ING, Consors) noch das Geld, das seit dem Umstieg von Tagesgeld auf
   einen Geldmarkt-ETF im ING-Depot liegt. Dieses Geld ist **Budget-Geld** (es gehört zu
   Kategorien). YNAB kann es als manuelles Budget-Konto führen, den Wert aber nicht selbst
   ermitteln. Bis zur Revision 2026-09-06 passierte damit gar nichts: das Budget war um
   diesen Posten falsch und driftete weiter.
2. **Keine Ownership über die eigenen Daten.** Jede Buchung, jede Korrektur, jede
   Historie lebt nur in YNAB. BudgetBuddy hält nichts fest. YNAB wird an fünf Stellen an
   BB vorbei beschrieben (ING-Import, Transfer-Erkennung, Split-Korrekturen, nachträgliche
   Kategorie-Änderungen, Amazon-Bestellungen der Frau), und nichts davon ist in BB sichtbar.
3. **Abhängigkeit.** YNAB ist das zentrale Tool für die Familienfinanzen; wohin es sich
   entwickelt und was es künftig kostet, ist offen. Das ist ein Versicherungsmotiv, kein
   akuter Schmerz, aber es nagt.

Was **nicht** weh tut: das Budgetieren selbst. "Alle Kategorien auswählen, Underfunded,
fertig" funktioniert gut. Budgetieren wird nicht nachgebaut, weil es schlecht wäre, sondern
nur, wenn Budget-Geld auf Konten liegen soll, die YNAB nicht kennt, und erst, wenn das
Vertrauen in das Kassenbuch da ist.

## What success looks like
Zwei Stufen, in dieser Reihenfolge. Die zweite ist das Gate für alles Weitere.

**v1 · Alles unter einem Dach.**
- BB zeigt alle Konten mit richtigem Stand: Comdirect, Bar, ING-Konten, und den
  Geldmarkt-ETF als Budget-Konto. In v1 führt YNAB den ETF als manuelles Konto, das Roman
  monatlich auf Stückzahl mal Kurs abgleicht; BB spiegelt es wie jedes andere Konto. Die
  Bewertung in BB (BB rechnet, YNAB wird nachgezogen) kommt später als BB-geführte
  Fähigkeit (ADR 0016).
- Buchungen pro Konto und über alle Konten, wie YNABs "All Accounts".
- Das Kassenbuch ist vollständig, weil es YNABs Zustand laufend nachführt (Spiegel), und
  merkt sich zusätzlich, was BB selbst entschieden hat.
- Das Budget-Loch durch den ETF ist geschlossen, in v1 über das manuelle YNAB-Konto.
- **Der Prüfvermerk ist die eine schreibende Ausnahme in v1** (ADR 0028): Roman markiert
  Buchungen in BB als geprüft, BB schreibt das nach YNAB zurück. Das ist sein häufigster
  Ablauf am Handy und der Grund, aus dem er die YNAB-App heute öffnet. Ein Kassenbuch, das
  ihn nicht übernimmt, wird nicht benutzt. Der Rückschreibe-Weg ist bewusst auf dieses eine
  Feld zugeschnitten; alles andere Nachbearbeiten bleibt v2. Ausnahme seit ADR 0054: BB
  baut "Kategorie ändern" bereits vor dem Abschluss von v1.

**v2 · Ownership.** ("Das war es wert.")
- Roman öffnet YNAB nicht mehr zum Nachbearbeiten: Kategorie ändern, Split ändern, Übertrag
  zusammenführen (Comdirect raus, ING rein = eine Bewegung), alles in BB, mit Historie.
- BB ist für diese Fähigkeiten führend; YNAB wird nachgezogen, und BB sieht, wo YNAB
  abweicht.
- Erst wenn v2 sich im Alltag bewährt hat, fällt die Entscheidung, ob das Budgetieren
  (Zuweisungen, Targets, Underfunded) nachzieht. Wenn ja: mindestens so schnell wie in YNAB.
- **Vorgezogen (2026-10-24):** Kategorie ändern (mit Split-Bearbeitung) wird als erste
  v2-Fähigkeit schon gebaut, bevor das v1-Kriterium Geldmarkt-ETF erfüllt ist (ADR 0054);
  "Lena fragen" folgt auf demselben Schreibweg (ADR 0056). Bei einem Konflikt gewinnt in
  diesen Scheiben YNAB, die Historie zeigt beides; die Sicht auf Abweichungen bleibt v2.
  Übertrag zusammenführen hat keinen vorgezogenen Termin.

**Woran man scheitern würde:** Wenn der Spiegel unzuverlässig ist und BB und YNAB sich
unerklärt widersprechen. Wenn BB langsamer oder umständlicher wird als YNAB-Web. Wenn das
Kassenbuch gebaut ist, aber niemand hineinschaut.

## Non-goals
- **YNAB abschalten.** Der Cutover ist eine Möglichkeit nach v2, kein Ziel dieser Vision.
  Solange YNAB Fähigkeiten hat, die BB nicht hat, läuft es weiter.
- **Budgetieren nachbauen vor v2.** Zuweisen, Targets, Underfunded bleiben in YNAB, bis
  das Kassenbuch Vertrauen aufgebaut hat. Wenn es kommt, dann als Parität plus ETF-Konto,
  nicht als Neuerfindung.
- **Depot-Vermögenssicht** (ING-Depot, Consors) ist nicht v1 oder v2. Depots sind Vermögen,
  kein Budget-Geld. Kommt als eigene Sicht obendrauf, wenn das Kassenbuch steht.
- **ING-Bankadapter** ist nicht v1. ING-Buchungen kommen vorerst über den YNAB-Spiegel.
- **Amazon-Auslesen, lokale LLMs, Finanz-Vorschläge** bleiben geparkt, bis das Kassenbuch
  steht. Theoretische Optionen, nichts für morgen.
- **Reports** weiterhin nur, wenn real vermisst. Wenn sie kommen, dann als Sichten auf die
  eigene Historie, nicht als Nachbau der YNAB-Reports.
- **Multi-User, Konfliktmodell, mobiles Budgetieren** bleiben draußen wie bisher.
- **Companion-Funktionen wegwerfen.** Import, Regeln, Dedup, Push, Quick Add bleiben, wie
  sie sind. Sie werden auf das Kassenbuch gesetzt, nicht ersetzt.

### Die Scope-Formel (ersetzt die aus ADR 0001)
**In-Scope = eine Fähigkeit, die heute in YNAB liegt, nach BB holen, so dass BB sie führt
und YNAB nachgezogen wird. Oder eine Fähigkeit, die YNAB nicht hat und die das Kassenbuch
braucht.** Non-Goal = eine Fähigkeit nachbauen, die in YNAB nicht weh tut und für die es
keinen Kassenbuch-Grund gibt.

## Ubiquitous language (seed)
Der Companion-Kern (Sync, SyncSession, BankTransaction, SyncTransaction, Rule,
DuplicateStatus, YnabImportStatus, ImportId, Order-ID-Matching, Quick Add) gilt
unverändert; verbindliche Definitionen in den BC-READMEs. Neu mit dieser Revision:

- **Kassenbuch** (Ledger) — BBs eigenes, dauerhaftes Buch aller Buchungen über alle Konten.
  Das Gedächtnis, das BB heute fehlt. Lebt im neuen Context `ledger`.
- **Konto** — ein Ort, an dem Geld liegt. Bankkonto (Comdirect, ING), Bar, oder ein
  **Wertpapierkonto mit Budget-Geld** (der Geldmarkt-ETF). Jedes Konto ist On-Budget oder
  nicht; Depots als Vermögen sind es nicht und sind vorerst kein Konto im Kassenbuch.
- **Buchung** — ein Eintrag im Kassenbuch: Datum, Betrag, Konto, Payee, Memo, Kategorie,
  ggf. Splits. Trägt ihre **Herkunft** und ihre **Historie**.
- **Herkunft** — woher eine Buchung oder Änderung kam: Comdirect-Import, Quick Add, Regel,
  Roman in BB, YNAB-Spiegel (dahinter: ING-Import, Änderung in YNAB, Transfer-Erkennung).
- **Historie** — die vollständige, unveränderliche Folge aller Änderungen an einer Buchung,
  je mit Zeitpunkt und Herkunft. "Auditierbar" heißt: jede Zahl im Kassenbuch lässt sich
  bis zu ihrem Ursprung zurückverfolgen.
- **Spiegel** — das laufende Nachführen von YNABs Zustand ins Kassenbuch (alle Konten,
  alle Buchungen, alle Änderungen). Der Spiegel macht das Kassenbuch vollständig, solange
  YNAB noch Eingabeort ist. Ein Spiegel mit Gedächtnis: er kennt zusätzlich BBs eigene
  Entscheidungen.
- **Abweichung** — ein Unterschied zwischen dem, was BB entschieden hat, und dem, was
  YNAB heute zeigt (z.B. Split in YNAB geändert). Sichtbar, nicht still überschrieben.
- **Führung** — welche Seite für eine Fähigkeit entscheidet. **BB-geführt:** BB entscheidet,
  YNAB wird nachgezogen (heute: Import, Kategorisierung, Quick Add). **YNAB-geführt:** YNAB
  entscheidet, BB spiegelt (heute: Nachbearbeitung, Überträge, ING, Budgetieren). Die Vision
  ist der Weg, Fähigkeiten von YNAB-geführt zu BB-geführt zu bewegen.
- **Übertrag** — zwei Buchungen auf zwei Konten, die eine Bewegung sind (Comdirect 1000
  raus, ING 1000 rein). Keine Budget-Wirkung.
- **Bewertung** — der Wert eines Wertpapierkontos mit Budget-Geld: Stückzahl mal Kurs.
  Eine Bewertungsänderung wird als Buchung sichtbar (Kursgewinn/-verlust), damit der
  Kontostand stimmt.
- **Gesamtsicht** — alle Konten zusammen, Buchungen und Stände (YNABs "All Accounts").

## Open questions
- **Wie wird die Historie technisch gehalten?** Gelöst (ADR 0013, 2026-09-06): append-only
  Log semantischer Delta-Ereignisse nach souls-Muster, persistiert, Zustandsmodell synchron
  in derselben Transaktion. SQLite bleibt.
- **Wie läuft der Spiegel?** Gelöst (ADR 0014): Delta-Sync über `server_knowledge`, ausgelöst
  über ein Frische-Kriterium (Seitenaufruf oder Vordergrund, älter als 5 Minuten) und nach
  jedem Push, kein Server-Timer. Abweichungs-Erkennung liegt im `ledger` (ADR 0013).
- **Kursquelle für den Geldmarkt-ETF:** vertagt (ADR 0016). In v1 läuft der ETF als manuelles
  YNAB-Konto mit monatlichem Abgleich; die Bewertung in BB kommt später als BB-geführte
  Fähigkeit, die Kursquelle als Research-Spike `ledger-c7j8e`.
- **Bar-Konto und Quick-Add-Konto:** Gelöst (ADR 0016): ein Konto "Bar". Quick Add ist ein
  Eingabeweg (Herkunft), kein Konto. Die frühere Annahme "heute in YNAB zwei" war falsch.
- **Zugriff der Frau** auf das Kassenbuch: nur über die bestehende App (gemeinsames Konto),
  keine eigene Identität. Bleibt so, bis es stört.
- **Wann ist v2 "bewährt"?** Abbruchkriterien aus `docs/idea-ynab-replacement.md`
  (unerklärte Abweichung an 3 Tagen in Folge → Stop) bleiben als Vorschlag stehen und
  werden bei Beginn von v2 festgezurrt.
- **ING-Adapter:** eigener Adapter in `banking-import` oder Comdirect-Muster
  generalisieren? (verschoben, nicht v1)
