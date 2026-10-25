# Vision: BudgetBuddy

> **Revision 2026-10-25.** Erweitert die Vision vom 2026-09-06 um das **Budget** (ADR 0063).
> Das Budget ist die nächste große Fähigkeit, vor der ETF-Bewertung. YNAB abschalten wird
> vom Non-Goal zur Richtung. Kassenbuch, Spiegel und Companion-Teil bleiben unverändert
> gültig.
>
> **Revision 2026-09-06.** Löste die Companion-Vision vom 2026-06-01 ab (ADR 0001 →
> ADR 0012).

## Purpose
BudgetBuddy wird das **führende System für unsere Finanzen**, Fähigkeit für Fähigkeit.
YNAB bleibt so lange Projektion und Fallback, wie BudgetBuddy eine Fähigkeit noch nicht
hat. Die Richtung ist, YNAB perspektivisch nicht mehr zu brauchen. Der Kern ist ein
eigenes, dauerhaftes **Kassenbuch** über alle Konten, mit einer Historie, die wirklich
auditierbar ist: jede Änderung an einer Buchung ist nachvollziehbar nach Zeitpunkt und
Herkunft (BB-Regel, Roman in BB, Roman in YNAB, Bank-Import).

Das Kassenbuch steht und wird benutzt: Spiegel, Prüfvermerk, Kategorie ändern, Splits,
Lena fragen, Memo. Die größte Fähigkeit, die noch an YNAB bindet, ist das **Budget**:
Geld auf Kategorien verteilen, verschieben, Targets anwenden. Es kommt als Nächstes, nach
demselben Muster wie alles andere (**BB entscheidet, YNAB wird nachgezogen**), und an
einigen Stellen besser als in YNAB.

Gebaut von und für Roman als Daily Driver (Frau nutzt mit). Leitprinzip unverändert:
**fertiges Tool, kein Hobby-Projekt. Was nicht real gebraucht wird, fliegt raus.**

## Users
- **Roman**: primärer Nutzer. Zieht Comdirect-Transaktionen, kategorisiert, pflegt Regeln,
  bearbeitet Buchungen nach, budgetiert. Verteilt am Monatsersten nach seinen Targets,
  verschiebt im Laufe des Monats häufig Geld zwischen Kategorien, auch am Handy. Sucht bei
  größeren Anschaffungen nach Gefühl, aus welchen Kategorien er Geld nehmen kann.
- **Frau**: erfasst Bar-Ausgaben (Quick Add), bestellt bei Amazon, liest mit. Budgetiert
  praktisch nicht.

Single-User-Modell mit gemeinsamem Konto. Kein Multi-Tenant, keine Rollen, kein Konfliktmodell.

## The problem
1. **Nicht alles unter einem Dach.** YNAB kennt Comdirect, Bar und die ING-Konten, aber
   weder die Depots (ING, Consors) noch das Geld im Geldmarkt-ETF, das Budget-Geld ist.
   Gemildert: der ETF läuft als manuelles YNAB-Konto, BB spiegelt es.
2. **Keine Ownership über die eigenen Daten.** Bis zum Kassenbuch lebte jede Buchung nur
   in YNAB. Für Buchungen ist das gelöst. Für das Budget gilt es weiter: jede Zuweisung,
   jede Verschiebung, jedes Target lebt nur in YNAB.
3. **Abhängigkeit.** YNAB ist das zentrale Tool für die Familienfinanzen; wohin es sich
   entwickelt und was es kostet, ist offen (inzwischen heißt das Budget dort "Plan").
   Seit das Kassenbuch trägt, ist aus dem Versicherungsmotiv eine Richtung geworden.
4. **YNABs Budget kann einiges nicht, was Roman braucht.** Es tut nicht weh, aber Roman
   löst es heute im Kopf, von Hand oder in Excel:
   - **Underfunded** verlangt, jedes Mal alle Kategorien auszuwählen.
   - **"Auffüllen bis"** rechnet mit dem scheinbaren Rest. Zahlungen vom Monatsende, die
     erst am 1. bis 3. abgebucht werden, fehlen dann im neuen Monat. Roman nutzt dieses
     Target deshalb nie.
   - **Rot ist Rot.** Am Ersten verteilt Roman bewusst mehr, als da ist, weil das
     Kindergeld sicher am 15. kommt. YNAB zeigt das genauso wie einen echten Fehlbetrag.
     Geplante Transaktionen helfen nicht, weil die echte Buchung über Comdirect kommt und
     dann doppelt da ist.
   - **Durchspielen geht nicht.** "Hier 200 runter, da 100, wie viel habe ich dann?"
     rechnet Roman von Hand oder macht danach vieles rückgängig.
   - **Leihen kennt YNAB nicht.** Beim Autokauf hat Roman in Excel geführt, aus welchen
     Kategorien er Geld geliehen hat, und es nach und nach zurückgezahlt.
   - **Am Handy** kann man nicht nach einer Kategorie suchen.

## What success looks like
Die bisherigen Stufen bleiben, die Reihenfolge ändert sich: **das Budget kommt vor der
ETF-Bewertung** (ADR 0063). v2 (Ownership der Nachbearbeitung) läuft schon und wird
weitergeführt.

**v1 · Alles unter einem Dach.** Erreicht bis auf die ETF-Bewertung in BB, die nach
hinten rückt. Der ETF bleibt bis dahin ein manuelles YNAB-Konto mit monatlichem Abgleich,
das BB spiegelt (ADR 0016).

**v2 · Ownership der Nachbearbeitung.** Kategorie ändern, Split, Lena fragen und Memo
laufen in BB (ADR 0054). Offen sind Übertrag zusammenführen und die Sicht auf
Abweichungen. Sie haben keinen Termin vor dem Budget.

**Budget · Der Monat entsteht in BB.** Kein langer Lese-Vorlauf: das Kassenbuch hat
gezeigt, dass eine reine Lese-Sicht nicht benutzt wird. Sehen und Handeln kommen zusammen.

- **Grundlage:** Verfügbar pro Kategorie im laufenden Monat, mit Gruppen, am Handy und am
  PC; **Zu verteilen** als eine Zahl; der **Vortrag** in den Folgemonat stimmt ab dem
  ersten Tag; Kategorie-Suche am Handy.
- **Stufe 1, die Handlung, die holt:**
  1. **Targets anwenden** mit einem Knopf am Monatsersten, ohne alle auszuwählen. Die
     Targets kommen zunächst aus YNAB.
  2. **Geld verschieben** zwischen Kategorien, am Handy schnell.
  3. **Kategorie-Detail:** Ausgaben des Monats, Sprung zur Buchung im Kassenbuch.
- **Stufe 2:** **Budget-Historie** (wer hat wann wie viel zugewiesen, verschoben,
  per Targets verteilt), allgemein und pro Kategorie, nach Art filterbar.
- **Stufe 3, besser als YNAB:**
  - **Erwartete Einnahme**: Gelb statt Rot, wird beim Comdirect-Import eingelöst.
  - **Leihen und zurückzahlen** zwischen Kategorien.
  - **Entwurf**: Verschiebungen durchspielen, Summe sehen, übernehmen oder verwerfen.
  - **Targets in BB pflegen**, mit **Deckel** ("50 € pro Monat bis 1000 €").

Woran man merkt, dass es gelungen ist: Roman öffnet YNAB am Monatsersten nicht mehr und
verschiebt Geld am Handy in BB. YNAB ist für das Budget nur noch Nachzieh-Ziel.

**Woran man scheitern würde:** Wenn BBs Verfügbar und YNABs Verfügbar sich unerklärt
widersprechen. Wenn Verschieben in BB langsamer ist als in YNAB. Wenn das Budget gebaut
ist, Roman aber weiter in YNAB budgetiert.

## Non-goals
- **YNAB abschalten an einem Datum.** Es ist die Richtung, kein Termin. Abgeschaltet wird
  erst, wenn BB alles führt, was Roman braucht.
- **Eine neue Budget-Methode.** "Nur verteilen, was da ist", Zu verteilen und der Vortrag
  bleiben. BB verbessert YNABs Modell und ersetzt es nicht durch ein anderes.
- **Automatik am Monatsersten.** Ein Knopf, kein Zeitplan. Roman bestätigt.
- **Berechnete Empfehlung, woher Geld kommen kann.** Die Entscheidung "da brauche ich es
  nicht" bleibt Romans Gefühl. BB macht sie leichter (Entwurf, Leihen), trifft sie nicht.
- **Kategorie-Verwaltung** (anlegen, umbenennen, Gruppen umbauen) im Budget-Schritt.
  Bleibt in YNAB.
- **Prognosen, Cashflow-Vorschau.** Die erwartete Einnahme ist eine einzelne Ankündigung,
  kein Forecast-Modul.
- **Lena-Sicht** im Budget als eigenes Feature.
- **Depot-Vermögenssicht** (ING-Depot, Consors): Vermögen, kein Budget-Geld. Kommt als
  eigene Sicht, wenn sie gebraucht wird.
- **ING-Bankadapter:** ING-Buchungen kommen weiter über den YNAB-Spiegel.
- **Amazon-Auslesen, lokale LLMs, Finanz-Vorschläge** bleiben geparkt.
- **Reports** nur, wenn real vermisst, dann als Sichten auf die eigene Historie.
- **Multi-User, Konfliktmodell.** Mobiles Budgetieren ist kein Non-Goal mehr: Verschieben
  und Ansehen am Handy gehören zum Budget.
- **Companion-Funktionen wegwerfen.** Import, Regeln, Dedup, Push, Quick Add bleiben.

### Die Scope-Formel (erweitert 2026-10-25)
**In-Scope = eine Fähigkeit, die heute in YNAB liegt, nach BB holen, so dass BB sie führt
und YNAB nachgezogen wird. Oder eine Fähigkeit, die YNAB nicht hat und die das Kassenbuch
oder das Budget braucht, weil Roman sie heute im Kopf, von Hand oder in Excel löst.**
Non-Goal = eine Fähigkeit nachbauen, für die es weder einen YNAB-Ablöse-Grund noch einen
gelebten Behelf gibt.

## Ubiquitous language (seed)
Der Companion-Kern (Sync, SyncSession, BankTransaction, SyncTransaction, Rule,
DuplicateStatus, YnabImportStatus, ImportId, Order-ID-Matching, Quick Add) und die
Kassenbuch-Begriffe gelten unverändert; verbindliche Definitionen in den BC-READMEs.

**Kassenbuch** (`ledger`):
- **Kassenbuch**: BBs dauerhaftes Buch aller Buchungen über alle Konten.
- **Konto**: ein Ort, an dem Geld liegt (Bankkonto, Bar, Wertpapierkonto mit Budget-Geld).
- **Buchung**: ein Eintrag mit Datum, Betrag, Konto, Payee, Memo, Kategorie, ggf. Splits;
  trägt Herkunft und Historie.
- **Herkunft**, **Historie**, **Spiegel**, **Abweichung**, **Führung**, **Bewertung**,
  **Gesamtsicht**: wie im `ledger`-README.
- **Übertrag**: zwei Buchungen auf zwei Konten, die eine Bewegung sind. Keine
  Budget-Wirkung. Nicht zu verwechseln mit dem **Vortrag**.

**Budget** (`budgeting`, neu 2026-10-25):
- **Budget**: das Ganze, alle Kategorien mit ihren Beträgen über die Monate. YNAB nennt es
  inzwischen "Plan"; BB bleibt bei "Budget".
- **Kategorie**: der einzelne Topf (Essen, Strom, Haushaltshilfe). Dieselbe Kategorie, an
  der Buchungen hängen. Kein eigenes Wort "Budget" für einen Topf.
- **Kategoriegruppe**: Gruppierung von Kategorien zur Anzeige.
- **Zu verteilen**: Geld, das da ist, aber noch keiner Kategorie gehört (YNABs "Ready to
  Assign"). Darf bewusst negativ werden.
- **Zuweisen**: Geld aus "Zu verteilen" einer Kategorie für einen Monat geben.
- **Verschieben**: Geld von einer Kategorie auf eine andere legen.
- **Verfügbar**: was in einer Kategorie im Monat übrig ist (Vortrag + Zugewiesen +
  Aktivität).
- **Vortrag**: Verfügbares, das am Monatsende in der Kategorie bleibt und im Folgemonat
  weiterzählt.
- **Target**: Romans Vorgabe pro Kategorie. Arten heute: *pro Woche ein Betrag*, *jeden
  Monat ein fester Betrag*, *bis zu einem Datum ansparen*, *auffüllen bis* (ungenutzt).
  Gewünscht: *mit Deckel*.
- **Targets anwenden**: alle Kategorien mit einem Knopf auf das bringen, was ihre Targets
  verlangen (YNABs "Underfunded" ohne Auswählen).
- **Budget-Bewegung**: eine Zuweisung, Verschiebung oder Target-Anwendung mit Zeitpunkt
  und Herkunft. Die **Budget-Historie** ist ihre Folge.
- **Erwartete Einnahme**: eine Ankündigung ("Kindergeld, ca. 250 €, am 15."), die ein
  negatives Zu verteilen gelb statt rot macht und beim Import der echten Buchung eingelöst
  wird. Keine Buchung.
- **Leihe**: Geld, das aus einer Kategorie genommen wurde und zurück soll. Bleibt offen,
  bis es zurückgezahlt ist.
- **Entwurf**: eine Reihe von Verschiebungen, die noch nicht gilt; zeigt die Summe und
  wird übernommen oder verworfen.

## Open questions
- **Kursquelle für den Geldmarkt-ETF:** vertagt (ADR 0016, Research-Spike `ledger-c7j8e`),
  durch ADR 0063 weiter nach hinten gerückt. (open since 2026-09-06)
- **Wann ist v2 "bewährt"?** Abbruchkriterium aus `docs/idea-ynab-replacement.md`
  (unerklärte Abweichung an 3 Tagen in Folge → Stop) als Vorschlag. (open since 2026-09-06)
- **ING-Adapter:** eigener Adapter in `banking-import` oder Comdirect-Muster
  generalisieren? (verschoben) (open since 2026-09-06)
- **Zugriff der Frau** auf das Kassenbuch: über die bestehende App, keine eigene
  Identität. Bleibt so, bis es stört. (open since 2026-09-06)
- **Liefert die YNAB-API die Targets lesbar** (Art, Betrag, Rhythmus) und erlaubt sie,
  den zugewiesenen Betrag pro Kategorie und Monat zu setzen? Voraussetzung für Stufe 1,
  nicht verifiziert. (open since 2026-10-25)
- **Wer führt die Targets?** Bis Stufe 3 YNAB (BB spiegelt und wendet an), danach BB.
  Wie der Wechsel aussieht und was mit in YNAB geänderten Targets passiert, ist offen.
  (open since 2026-10-25)
- **Woher kennt BB Verfügbar und Zu verteilen?** Selbst rechnen aus Kassenbuch und
  Budget-Bewegungen, oder von YNAB übernehmen und abgleichen? Eng verbunden mit
  "unerklärter Widerspruch" als Scheiter-Kriterium. (open since 2026-10-25)
- **Löst BB das "Auffüllen bis"-Problem** über den Monatswechsel (vorgemerkte Umsätze,
  Buchungsdatum)? Nur relevant, wenn es mit den Targets in BB ohne Aufwand mitkommt.
  (open since 2026-10-25)
- **Gibt es bei YNAB schon ein Target mit Deckel?** Ändert nichts am Wunsch, aber an der
  Frage, ob BB es beim Spiegeln der Targets schon vorfindet. (open since 2026-10-25)
