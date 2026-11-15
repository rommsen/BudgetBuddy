# Fahrplan: 95 % BB, aus einer Hand

Stand 2026-11-07. Grundlage: ADR 0086, `vision.md` Abschnitt „What success looks like“.

## Stand
Zuletzt aktualisiert: 2026-11-15 abends, nach Review von Schnitt 9 und work-Lauf ledger-pkrwd (Payee entfernen).

- **Etappe 1 · Bedeutungen:** erledigt, 5 von 5 (Skizze und Prototyp, Layout-Sweep, Muster im
  Styleguide, Designwert-Tests aufgeräumt, Palette 2 im Token-Layer). Romans Blick auf dak16 neben
  dem Prototyp: freigegeben (2026-11-08). Entschieden (Roman, 2026-11-08): Fokusring, Textlinks und
  Marke im Akzent, gescheiterter Push bleibt Gelb (Nachtrag ADR 0087).
  Panel ab 1024 px (Roman, 2026-11-07).
- **Etappe 2 · Lücken schließen:** erledigt, 8 von 8. Neu (2026-11-09): ledger-z6sya (Buchung
  löschen im Detail, mit Rückgängig, 67becce0) und ledger-m7mye (Betrag bearbeiten im Detail, ee0cc661),
  beide nach ADR 0092; Review 01b989fc..8561beb8 mit Fixes fe515af6..2c808771 (unter anderem doppelte
  Neuanlage nach halbem Split-Ersatz behoben). Romans Blick auf beide am Handy und PC (2026-11-10): passt;
  Abweichungen roter Grund, Höhe und Position des Lösch-Knopfs und Modal-Hintergrund angenommen. Leerer
  Betrag als Meldung am Feld statt Toast: ledger-q42pg. Bank-Referenz nach Betragsänderung per Test belegt,
  im Alltag beim nächsten Import auf Duplikat achten. Folge-Spike ledger-m7a9q (Betrag eines Übertrags per Update) im
  Backlog, danach kann die Sperre für Überträge fallen. ledger-nb4tv (ETF-Konto und Abgleich) nach
  Etappe 3 verschoben (Roman, 2026-11-10).
  Deployt (2026-11-10, f85d3b3d): ledger-m7k2d, ledger-z6sya, ledger-m7mye.
- **Etappe 3 · Ein Produkt:** in elf Schnitte geteilt (Roman, 2026-11-11). 8 erledigt (8 Kassenbuch ordnen, siehe Work-Lauf 2026-11-14/15 unten, deployt 2026-11-15, 1f40983e; 6 categorization-n3sxw mit ledger-hg5m5, 7 ledger-ea70s, siehe Work-Lauf 2026-11-13/14 unten; deployt 2026-11-14, 1b3a8108), davon 1 design-system-k6kfw, 2 ledger-s1weh
  (beide deployt), 3 budgeting-w9yt4 (Angepinnte Kategorien, 828f6a24), 4 banking-import-d0fey (Import direkt, ADR 0093, 613eee26)
  mit ledger-r7h2w (wartendes Duplikat übernehmen oder verwerfen, ef7e039b), ledger-fxt09 (Payee ändern, 0a7d9cf0) und
  banking-import-t4raw (automatisch abrufen, Vorgabe an, 1b1c868f), 5 ledger-v6s7c (Durchgehen, 64a3402f). Work-Lauf
  2026-11-12 nachts, Roman offline: alle Verifier PASS (w9yt4, d0fey, v6s7c in Iteration 2), zwei Review-Läufe mit Behebung
  (cfc21b06..613eee26: 45 Funde, darunter Import-Push ohne Schreib-Sperre; faa240c5..64a3402f: 28 Funde, darunter ein
  schräger Scroll, der als Wisch „Passt“ auslöste). Gate auf e59c5ef0: VERIFY: PASS (dotnet 4751, node 316). Nicht gepusht,
  nicht deployt. Entschieden (Roman, 2026-11-11): wartende Duplikate im Kontostand mitgezählt, Abruf im Intervall mit
  Vorgabe an, alle Funktionen der heutigen Prüfung bleiben (Regel aus der Buchung mit Schnitt 6), Durchgehen älteste zuerst
  und nach §7.14, 4 und 5 werden gemeinsam deployt.
  Romans Blick am Handy und PC (2026-11-13): siehe Eintrag unten. Bestätigt (Roman, 2026-11-13): „Ist doppelt, verwerfen“
  im Akzent, Texte „Alle“ und „Eine Push-TAN fragt es nie an.“ in den Einstellungen.
  ledger-s1weh (Übersicht mit Zu tun): Abweichungen (a) bis (c) vom Prototyp in der Task. Romans Blick am Handy und PC
  (2026-11-11): passt, (a) bis (c) und der Dialog am PC bestätigt.
  Review df9611c4..HEAD (2026-11-11): 22 Funde behoben in 3142ef3e..ec4f4124, darunter Budget aus Zu tun öffnete bei wartender
  Rückkehr im gewählten statt im laufenden Monat und überholte Antworten setzten Zähler zurück. Entschieden (Roman):
  „Buchung“ auf der Übersicht am PC als Dialog (Modal ab 1024 px).
  Deployt 2026-11-11 (dc0e3b9d): ledger-s1weh samt Review-Fixes. Reihenfolge und Zuordnung im Abschnitt Etappe 3.
- **Laufend erledigt (2026-11-10):** ledger-mj3a9 (Bob gesperrt bei ungültigem `BOT_MAX_AMOUNT_EUR`, Nachtrag
  ADR 0068), ledger-q42pg (Feldfehler am Betrag statt Toast), ledger-m7a9q (Probelauf: YNAB zieht die Gegenbuchung
  eines Übertrags mit, Nachtrag ADR 0092); Review 68279598..HEAD mit Fixes 009b3186 und 5444edd7. Neu im Backlog:
  ledger-t4rk8 (Betrag eines Übertrags in BB ändern, braucht Romans Entscheidungen beim Verfeinern). Danach
  design-system-ak97z (Umschalter Ausgabe/Einnahme hält die Tastatur offen, 45273030) mit Review b74d67df. Deployt 2026-11-11 (5f7fc08a).
- **Blick Roman (2026-11-11):** ledger-q42pg und design-system-ak97z am Handy geprüft, passt.
- **Review design-system-k6kfw (2026-11-11):** 31 Funde behoben in 63e9f6a7..ec87e30b, darunter „Abrufen“ startet den
  Sync erst nach dem Lesen der Sitzung (kein zweiter Start, keine zweite Push-TAN) und sichtbare Ladefehler der Konten.
  Entschieden (Roman): Schriftgrößen der Übersicht nach §7.14 (Kriterium in ledger-s1weh), Abbrechen-Pfeil im
  Sync-Ablauf in die Kann-Sammlung banking-import.
- **Blick Roman (2026-11-11):** design-system-k6kfw (Navigation mit drei Orten) am Handy und PC geprüft, passt.
- **Bestandsreview ledger (2026-11-12):** 107 Befunde, Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`. Gruppen G1,
  G2, G4, G5 verfeinert und in 10 Tasks gebaut, alle Verifier PASS (ct3cx in Iteration 2, f8bjm in Iteration 3): ledger-xqqnv
  (vorhandene import_id heißt angekommen, auch bei Barbuchungen), ledger-vfr4h (Sperre und Auslöser belegt), ledger-smv1s
  (Zeilen-Sperre `whenRowIdle`), ledger-sjbvc (Sammellauf in einem Feld), ledger-ct3cx (Liste behält Auswahl, Seiten und
  Scrollstand nach Spiegel-Lauf), ledger-nc708 (Historie an Ladevorgänge gebunden), ledger-ng110 (Split-Editor: offener Rest
  gelb, zu viel rot), ledger-vf6z5 (Neue Buchung mit Feldfehlern), ledger-dn4vy (Filter und Liste nach Styleguide,
  Kontrast-Invariante 10), ledger-f8bjm (Ersatz-POST mit verlorener Antwort: neue Ereignisse und Spalte `pending_replacement`,
  Nachträge ADR 0058 und 0092). Entschieden (Roman, 2026-11-12): Rest gelb/rot, 409 bei Barbuchung angekommen, ausgegraute
  Zeile in `text-muted`. Gate auf 6359f73c: VERIFY: PASS (dotnet 4886, node 322). Nicht gepusht, nicht deployt. G3
  (ledger-h7de3, Fehlertexte in Ereignissen) noch unverfeinert, wartet nicht mehr.
- **Review-Lauf a3f7eb1d..982bdf79 (2026-11-12):** 4 Agents, 29 Funde behoben in 7115d633..738c8407, darunter: Nachladen
  überschrieb neuere Zeilen, gescheitertes „Lena fragen“ verwarf eine offene Eingabe, die Kontoliste bei „Neue Buchung“
  zeigte Laden/Fehler/leer nie (FS0020, jetzt in allen Projekten ein Build-Fehler), die Aufzählung der Zeilen-Sperre stellte
  keine Antworten zu, ein offener Ersatz ohne Fehlschlag blieb gesperrt. Entschieden (Roman): gelbe Hinweise bleiben in der
  ausgegrauten Zeile, „Ersatz“-Texte passen, Toast am Handy als Ticket design-system-vf94m. Gate: VERIFY: PASS (dotnet 4899,
  node 322).
- **Deployt 2026-11-12 (57461f98):** Schnitte 3 bis 5 (w9yt4, d0fey mit r7h2w, fxt09, t4raw, v6s7c) und die zehn Tasks aus dem
  Bestandsreview ledger samt Review-Fixes. Automatischer Abruf ab jetzt an (alle 6 Stunden, bucht direkt), Migration
  `pending_replacement` (ledger-f8bjm). Health-Check grün.
- **Blick Roman (2026-11-13) auf den Deploy 57461f98:** Angepinnt, Abrufen und Import, automatisch abrufen, Durchgehen,
  Auswahl und Scrollstand passen; Payee ändern, Split, Filter und ausgegraute Zeile noch ohne Rückmeldung. Ein Wisch nach links im Durchgehen zeigte
  einmal „Erst eine Kategorie wählen“, nicht reproduzierbar (Roman: wohl falsch gewischt). Neue Buchung am Handy: Memo steht
  neben dem Datum, Ticket ledger-yhemh. Keine wartenden Duplikate gesehen, dafür ein Kontostand-Fehler: ein von YNAB
  abgeglichener Übertrag (ING an Girokonto) steht im Kassenbuch doppelt, weil der Spiegel
  `matched_transaction_id` nicht liest und der Import einen Übertrag nicht als mögliches Duplikat erkennt. Ticket ledger-nkwqb
  (Geld betroffen). In YNAB ist die Buchung einmal da; Roman entfernt dort die Kategorie am Übertrag und gibt ihn frei,
  in BB wird nichts gelöscht (DELETE scheitert am 404, die Comdirect-Referenz ginge verloren). Roh dazu:
  ynab-sync-c7aag (YNABs Payee-Zuordnung überschreibt Importe), banking-import-r28hx (alle Konten per FinTS).
- **Work-Lauf 2026-11-13 (laufend):** ledger-nkwqb (Spiegel legt von YNAB abgeglichene Buchungen zusammen, Vollabruf-Generation 1,
  f41a4d24) und banking-import-pknvp (YNAB-Übertrag als mögliches Duplikat beim Import, 127b9512), beide Verifier PASS in
  Iteration 1. Nachträge ADR 0014, 0015, 0093. Gate im Worktree über den kombinierten Stand: VERIFY: PASS (dotnet 4936, node 322).
  Probelauf gegen eine Kopie der Produktion (nur GET bei YNAB): 3 Zusammenlegungen (zwei ING-Überträge auf dem Girokonto, eine
  Barbuchung von Bob, die YNAB mit einer älteren Buchung gleichen Betrags abgeglichen hat), danach stimmen alle Kontostände mit
  YNAB. Nicht gepusht, nicht deployt.
- **Probelauf deckte Fehler auf (2026-11-13):** YNAB hatte eine Barbuchung von Bob mit einer eine Woche älteren Zahlung
  gleichen Betrags abgeglichen (zwei echte Zahlungen); ledger-nkwqb wäre YNAB blind gefolgt. Roman hat den Abgleich in YNAB
  aufgehoben. Entschieden (Roman): Zusammenlegen nur bei Überträgen. ledger-nnd2f (nur Comdirect-Import in einen Übertrag,
  510257b7), Verifier PASS in Iteration 1, Gate VERIFY: PASS (dotnet 4940, node 322), Ergänzung im Nachtrag ADR 0015. Zweiter
  Probelauf gegen eine frische Kopie: genau zwei Zusammenlegungen (die ING-Überträge), danach stimmen alle Konten mit YNAB.
  Neu im Backlog: ledger-ay81j (YNAB verschluckt Barbuchungen aus BB, Geld, unverfeinert). Nicht gepusht, nicht deployt.
- **Review-Lauf f972a950..HEAD (2026-11-13):** backend-code-quality und qa-milestone-reviewer, 9 Funde behoben in f8623948
  (eine Regel für Vor- und Schleifenprüfung der Zusammenlegung, Log nur mit Grund, zwei ungedeckte Prüfungen wieder
  getestet, Testnamen), 1 in die Kann-Sammlung. Gate auf main: VERIFY: PASS (dotnet 4951, node 322). Dritter Probelauf: zwei
  Zusammenlegungen, keine Log-Zeilen, alle Kontostände gleich YNAB.
- **Deployt 2026-11-13 (248b089a):** ledger-nkwqb, banking-import-pknvp, ledger-nnd2f samt Review-Fix. Health-Check grün.
  Erster Spiegel-Lauf in der Produktion geprüft: Vollabruf-Marker gesetzt, zwei Zusammenlegungen, alle Kontostände gleich
  YNAB. Roman: sieht gut aus.
- **Work-Lauf ledger-ay81j (2026-11-13):** in BB erfasste Buchungen (Quick Add, Bob, Neue Buchung) gehen beim ersten Push
  und beim Erneut senden ohne `import_id` an YNAB, der Spiegel verknüpft eine offene Erfassung nach Konto, Tag, Betrag und
  Payee, Erneut senden lässt vorher den Spiegel laufen (5e0925b1). Verifier PASS in Iteration 3, beide FAILs nur README.
  Review-Lauf mit backend-code-quality und qa-milestone-reviewer, Fixes 26a549cd und 7090252f. Entschieden (Roman): Ersatz und
  Wiederherstellen einer Erfassung wieder mit Ersatz-`import_id` vom Stamm `BB:<EntryId>` (sonst legt ein Wiederholen nach
  verlorener Antwort in YNAB eine zweite Buchung an), Korrektur in ADR 0058 und 0092. Behoben außerdem: doppelte Verknüpfung
  im Lauf, wenn die Beobachtung ohne `import_id` vor der mit `import_id` kam. Gate auf main: VERIFY: PASS (dotnet 4986, node
  322). Probelauf gegen eine frische Kopie der Produktion: keine Änderung, kein Fehler. Learn: infrastructure-ewasy
  (Delta-Gate meldet alten Wortlaut) nachgeschärft, wird verfeinert. Nicht gepusht, nicht deployt.
- **Deployt 2026-11-13 (3cf305d5):** ledger-ay81j samt Review-Fixes. Health-Check grün. Keine Migration, der Spiegel
  startet beim Öffnen des Kassenbuchs.
- **Work-Lauf 2026-11-13 abends:** infrastructure-ewasy (Delta-Gate meldet alten Wortlaut: Prüfung 7 mit `--keep`, Liste
  „ganz nachlesen“, 09e2bc5f) und Schnitt 6 categorization-n3sxw (Regeln als Tab im Kassenbuch, Panel und Sheet, Löschen mit
  Rückgängig über `restoreRule`, ADR 0094, 53e9170a), beide Verifier PASS in Iteration 2 (ewasy: Testlücke bei der
  Schwelle; n3sxw: Abweichungen von der Skizze). Entschieden (Roman): zwei Tasks für Schnitt 6, Löschen mit Rückgängig,
  Payee-Override, Export/Import und Neu laden entfallen, Regel aus der Buchung als fester Eintrag und als Angebot nach
  Kategorie-Änderung (ledger-hg5m5, Angebot als Zeile im Detail statt im Toast), DS-Tabs und Umschalter wie überall, Panel
  wie bei den Buchungen, Karte statt Tabelle von 1024 bis 1279 px. categorization-krscs ist in n3sxw aufgegangen und
  verworfen (Roman, d9cadc63). Gate auf main: VERIFY: PASS (dotnet 4991, node 333). Nicht gepusht,
  nicht deployt.
- **Review-Lauf aa2c1e46..HEAD (2026-11-13):** 4 Agents, rund 40 Funde behoben in 5107a1be..1da80ba9, darunter: eine
  Speicherantwort schrieb in ein inzwischen anderes Panel (Eingabe verloren), Umordnungen liefen parallel, Kategorien-Fehler
  ließ den Picker ewig laden, „Jetzt spiegeln“ fehlte am PC auf dem Tab Regeln, drei Bug-Regressionen der alten Regelseite
  fehlten. Entschieden (Roman): „aktiv“ und „Passt nicht“ ruhig statt Grün und Rot. Der Sweep fand danach den überdeckten
  Kopf bei 1024 px (behoben). Kann-Sammlungen categorization und design-system ergänzt. Gate: VERIFY: PASS (dotnet 5012,
  node 338). Deployt 2026-11-14 (1b3a8108).
- **Work-Lauf 2026-11-13/14 nachts, Roman offline:** Schnitt 7 ledger-ea70s (Ein Erfassen: Quick Add als Ort entfällt,
  `#/quickadd` öffnet „Neue Buchung“ auf der Übersicht, ohne Kontofilter ist das Quick-Add-Konto vorbelegt, Fokus im Betrag
  am PC, Schutz vor dem Neuladen, Konto- und Kategorie-Auswahl ersetzen an allen vier Stellen den Sheet-Inhalt, Memo unter
  dem Datum am Handy; nimmt design-system-taytp und ledger-yhemh auf; 9645bb72) und ledger-hg5m5 (Regel aus der Buchung:
  „Regel anlegen“ im Detail und auf der Karte des Durchgehens, Angebot nach einer Kategorie-Änderung mit Urteil vom Server;
  3322d427). Beide parallel, beide Verifier PASS in Iteration 1, Merge ohne Konflikt, Gate auf dem zusammengeführten Stand
  grün. Review-Lauf 9067dcd6..3cb4257a mit 4 Agents, 30 Funde behoben in 9c495e0e..4a9423d3, darunter: am PC wechselten
  ↑/↓ die Buchung, während das Regel-Formular im Panel stand, und verwarfen so den Entwurf; Esc im Regel-Dialog des
  Durchgehens tat nichts (`Modal.CloseOnEscape` wird nirgends ausgewertet); eine Memo-Regel warf bei Memo `null`;
  `addCashBooking` ohne Aufrufer entfernt; Angebotszeile als `Detail.offerRow` im DS. Kann-Sammlungen ledger,
  design-system und neu ynab-sync ergänzt. Gate: VERIFY: PASS (dotnet 5009, node 340). Deployt 2026-11-14 (1b3a8108), Health-Check grün.
  Für Romans Blick am Handy und PC: (1) Karte im Durchgehen: „Regel anlegen“ als Textlink neben „Details“ statt eigener
  Zeile (sonst rutscht „Passt, weiter“ aus dem Bild); (2) am Handy führt im Regel-Sheet das × zur Buchung zurück, ein
  beschriftetes „Zurück“ gibt es nur im Panel am PC; (3) „Regel anlegen“ als Feldzeile im Detail: als Aktion erkennbar?;
  (4) Höhensprung, wenn die Auswahl den Inhalt des Sheets ersetzt („+ Buchung“, Konto antippen); (5) gebogene Akzentkante
  der aktiven Zeile in „Konto wählen“ (älter, jetzt prominent); (6) Satz unter „Quick-Add-Konto“ in den Einstellungen
  („Konto für manuell erfasste Transaktionen“) besser „Vorbelegt bei „+ Buchung“ ohne Kontofilter, auch für Bob“?
- **Romans Blick 2026-11-14 auf ledger-ea70s (teilweise):** Bar ohne Kontofilter passt, mit Filter auf dem
  Girokonto kommt Bar (Import-Konto gesperrt), auch richtig. Nicht so schnell wie Quick Add: die Zuletzt-Chips zeigen die
  üblichen Bar-Buchungen nicht, neu ledger-sre0f (Chips aus dem gewählten Konto, fünf mit Memo, Entscheidungen in der Task).
  Learn: Abgleichsregel in CLAUDE.md geschärft (9f19b85f). Eine neue Regel stand oben, sie soll ans Ende
  (categorization-m47g8, für „Regel anlegen“ und „Neue Regel“). Sichtpunkt 6 entschieden: der Satz lautet wörtlich
  „Vorbelegt bei „+ Buchung“ ohne Kontofilter“, der Name „Quick-Add-Konto“ bleibt (ledger-wj0b8). Am Handy standen mit
  „Zu prüfen“ leere Tagesköpfe und ein Tag zweimal (ledger-pnjz3, Roman: nie leere Blöcke, jeder Tag einmal).
- **Work-Lauf 2026-11-14 mittags:** categorization-m47g8 (neue Regeln bekommen die niedrigste Priorität und stehen am Ende,
  2efcf3c0), ledger-pnjz3 (Tagesgruppen je Kalendertag; Ursache: die Spalte `date` mischt Kinds, im Browser trennte das einen
  Tag in zwei Gruppen mit demselben React-Key, React ließ alte Köpfe im DOM stehen; d9a1a5b5) und ledger-wj0b8 (Satz in den
  Einstellungen, a4dd439d). Alle drei Verifier PASS in Iteration 1, Gate auf dem zusammengeführten Stand VERIFY: PASS
  (dotnet 5013, node 340). Review-Lauf d40409da~1..ddd1ba7e mit 4 Agents, 6 Funde behoben in 7ffac40c..99782788: die
  Prioritätsprüfung lehnte die 0 und negativen Werte ab, die der Server jetzt selbst vergibt; eine neue Regel konnte nach
  einem Neuladen doppelt in der Liste stehen; beide Ansichten holen ihre Tagesgruppen über `dayGroups` (Richtung getestet);
  ein Handler-Test war gegen parallele Tests nicht stabil. Kann-Sammlungen ledger und categorization ergänzt. Gate:
  VERIFY: PASS (dotnet 5016, node 340). Deployt 2026-11-14 (5a0d5394), Health-Check grün. Für Romans Blick: am Handy mit
  „Zu prüfen“ und ungefiltert keine leeren Tagesköpfe, jeder Tag einmal; eine neue Regel steht am Ende des Tabs Regeln.
  Vorschlag: `/architecture-review ledger` (17 erledigte Tasks seit dem letzten Bestandsreview).
- **Work-Lauf 2026-11-14 abends:** ledger-sre0f (Zuletzt-Chips in „Neue Buchung“ aus dem gewählten Konto: je offenem
  Kassenbuch-Konto ohne Import-Konto die letzten fünf Vorlagen aus dem Kassenbuch, gleich wer gebucht hat, eine Abfrage
  mit `ROW_NUMBER`, der Client filtert beim Kontowechsel ohne neue Anfrage, ein Tipp setzt das Memo genau, Überträge
  bleiben Vorlagen; 50b7f07b). Verifier PASS in Iteration 1. Review-Lauf 814e1002~1..d021f49a mit backend-code-quality,
  frontend-architecture und qa-milestone-reviewer, 5 Funde behoben in 511db775..9465fd5c: zwei Chips mit gleichem Text
  teilten sich einen React-Key, jetzt aus Konto, Payee, Ziel, Betrag und Richtung; Abfrage-Test für Gelöschte und die
  Grenze; Endpoint-Zeile ohne Abhängigkeit von der geteilten DB. Kann-Sammlung ledger 123 bis 125. Gate: VERIFY: PASS
  (dotnet 5031, node 340). Nicht gepusht, nicht deployt. Entschieden (Roman): Einnahmen stehen im Chip mit „+“ vor dem Betrag.
  Für Romans Blick nach dem Deploy: „+ Buchung“, Konto Bar wählen, die üblichen Bar-Buchungen stehen als Chips, ein Tipp
  füllt Payee, Kategorie, Betrag und Memo.
- **Work-Lauf 2026-11-14/15 nachts, Roman offline:** Schnitt 8 Kassenbuch ordnen nach Prototyp Version 17, drei Tasks
  nacheinander. ledger-d9z3n (Handy unter 1024 px: Auswahlfeld „Konto“, freie Suche statt Payee- und Memo-Filter,
  „Filter (n)“ mit Filter-Sheet, gesetzte Filter als Chips mit „×“, „Filter löschen“ außer Konto, Betragsfilter als Höhe
  ohne Vorzeichen mit Umschalter „Alle | Ausgaben | Einnahmen“ nach Romans Regel, ADR 0095; a2b25490, Verifier PASS in
  Iteration 2). ledger-cnaz3 (PC ab 1024 px: Kopf mit Konto, Saldo, „Abrufen“, „n durchgehen“, Filterleiste mit Suche
  rechts, Tabelle ohne Status und Prüf-Knopf, Panel immer da und ab zwei gewählten als Zusammenfassung, Strg-, Umschalt-
  Klick, Umschalt+↑↓, L, „/“, ADR 0096; 35f75369, Verifier PASS in Iteration 3: zweimal traf eine Sammelaktion eine
  Buchung ohne Häkchen, jetzt wählt kein Weg eine nicht geladene oder ausgegraute Buchung). ledger-zfj78 (Detail: Payee
  als Kopf, Prüf-Knopf statt Schalter, Knopfzeile Aufteilen und Lena, aufklappbare Historie mit Betrag als DS-Baustein
  `HistoryList`, „Buchung löschen“ nur mit rotem Rand; fe9159d1, Verifier PASS in Iteration 2). Review-Lauf
  61bf6bd2..6acc9f6e mit 4 Agents, 37 Funde behoben in 1eaf9006..148a6a11, darunter: die Suche nahm `%` und `_` als
  Platzhalter (bei „alle zeigen“ am Payee zu viele Treffer), ein alter Client ohne Richtung bekam 500, das Ausgrauen
  kannte Suche und Betrag nicht, die gelbe Prüfkante verschwand in der Ankerzeile, negativer Saldo ohne Rot. Kann-Sammlung
  ledger 126 bis 144, budgeting-h9c3w (Historie im Budget auf `HistoryList`). Gate auf main: VERIFY: PASS (dotnet 5101,
  node 339). Deployt 2026-11-15 (1f40983e) mit ledger-sre0f, Health-Check grün, keine Fehler im Log.
- **Für Romans Blick (nachts selbst entschieden oder offen), Route `#/ledger`, am Handy 412 px und am PC 1440 px:**
  - Rückfragen aus dem Review, unverändert gelassen:
    (1) Eine gewählte Buchung, die beim Nachladen aus dem geladenen Fenster rutscht, bleibt gewählt (ledger-ct3cx); die
    Leiste zählt sie dann nicht mehr, „Geprüft“ und „Kategorie setzen“ treffen sie aber. Soll sie aus der Auswahl fallen,
    oder sollen Leiste und Panel sie mitzählen?
    (2) „nicht in YNAB“ steht in der Zeile jetzt Rot (Prototyp), im Detail die Leiste „Noch nicht in YNAB“ weiter Gelb
    (ADR 0087 Punkt 3). Eine Farbe für beides?
    (3) Am Handy scrollen Suche, Filter-Knopf und Chips mit weg; §7.3 verlangt haftende Filter.
    (4) Kategorie, Zeitraum und Sortieren öffnen aus dem Filter-Sheet ein zweites Sheet; §7.15 will den Inhalt ersetzen.
    (5) Im Filter-Sheet heißt die Aktion „Zurücksetzen“, sonst „Filter löschen“ (beides Prototyp).
    (6) Die Historie zeigt bei Erfassungen in BB „ROMAN“, auch wenn Lena gebucht hat; „BB“ wäre genauer.
  - Nachts selbst entschieden (steht als Abweichung oder Entscheidung in der Task):
    ledger-d9z3n: „Filter löschen“ setzt auch Sortierung und „Gelöschte zeigen“ zurück; die Zahl am Filter-Knopf zählt die
    Chips ohne Suche; „Abrufen“ mit dem Rahmen von `Button.Secondary`; voller Kontoname in der Zeile; ein Betrag von 0 ist
    bei gesetzter Richtung weder Ausgabe noch Einnahme; am PC trägt der Chip „Betrag“ Betrag und Richtung.
    ledger-cnaz3: (f) die Kompaktliste ab 1024 px behält die Handy-Zeile; (g) Kopfknöpfe, Tabs und Chips in den DS-Größen;
    (h) das Panel beginnt unter dem Kopf; „n durchgehen“ zählt in einem Konto über die Übersicht; „Lena fragen“ am PC ohne
    Zahl; „Kategorie: <Name> +k“ bei mehreren; Strg- oder Umschalt-Klick nimmt die im Panel offene Buchung mit.
    ledger-zfj78: (c) Tag im Kopf „Di., 03.11.“, mit Jahr nur bei älteren Jahren; (d) runder Schließen-Knopf im Sheet.
  - Hinweise der Verifier: im Konto-Sheet hat die gewählte Zeile nur Rahmen und Haken (Prototyp hinterlegt sie leicht);
    im Filter-Sheet fehlen die Trennlinien des Prototyps; „Abrufen“ und „Jetzt spiegeln“ sehen bei 412 px fast gleich aus.
- **Prüfliste (seit 2026-11-15):** Romans offene Sichtprüfungen, nachts getroffene Entscheidungen und Rückfragen stehen als
  Punkte in der Seite „BB Prüfliste“ (https://claude.ai/artifact/L6YCruuxKW9SjWJf7hsJjW). Roman setzt je Punkt „Passt“,
  „Passt nicht“ oder „Später“ mit Notiz; Claude liest die Urteile per `ArtifactData` aus der Sammlung `checks` (Punkte in
  `items`, Schlüssel = Punkt-Id) und trägt neue Punkte nach jedem Lauf dort ein, statt sie nur hier aufzuzählen.
- **Work-Lauf 2026-11-15 nachmittags, Roman online:** Schnitt 9 design-system-gcpws (Liste mit Panel überall), 86ceeb32.
  Panel ab 1024 px überall dieselbe Hülle mit `clamp(380px, 30vw, 520px)` aus `panelWidthFor`, eine Tastenregel
  `ListKeys.actionFor` für Kassenbuch, Regeln, Budget, Kategorien und Targets prüfen (.NET-Aufzählung gegen Orakel,
  Browser-Tasten-Sweep, `[fokus]`-Test), Gruppe in „Kategorie bearbeiten“ als kurze Auswahl, Wächter gegen feste
  Panelbreiten. Verifier: Iteration 1 FAIL (task-under-specified, drei Abweichungen vom Prototyp), Roman entschied G11 bis
  G13 in der Prüfliste, Iteration 2 PASS. Gate im Worktree: VERIFY: PASS (dotnet 5123, node 348). Nicht gepusht, nicht
  deployt. Abweichungen: Budget-Tabelle ab rund 1340 px (G11), Panel unter dem Seitenkopf (G12).
  Vorher am Tag: Review-Rückfragen q-roman (Historie „BB“), q-farbe („nicht in YNAB“ Gelb), q-wort („Filter löschen“)
  umgesetzt (ea612c26). Logo design-system-x1abn zurückgestellt (Roman), später eigener Durchgang.
  Neu im Backlog: design-system-m4e7q (Modal: Esc ohne Fokus, Fokus zurück), ledger-fp4wn (Auswahl außerhalb des
  geladenen Fensters mitzählen, Roman q-fenster), ledger-zqst5 (Suche und Filter am Handy haften, Roman q-sticky).
- **Review-Lauf 98eaf680..5e45b028 (2026-11-15):** gcpws mit 4 Agents, Fixes in 84859355..5e45b028, darunter: Esc in
  einem Feld von „Neue Buchung“ verlässt erst das Feld, das nächste Esc schließt das Formular (Roman, 5e45b028). Kann-Zeilen
  c2a1e54c, Protokoll e4b15d01. Gate auf main: VERIFY: PASS (dotnet 5128, node 348).
- **Work-Lauf ledger-pkrwd (Payee einer Buchung entfernen), 69c4b537:** „Payee entfernen“ in der Payee-Auswahl, derselbe
  Befehl `setEntryPayee` mit Ziel `None`, `EntryPayeeChangedInBb` jetzt Schema-Version 2 (Version 1 bleibt lesbar), PUT mit
  `payee_id` und `payee_name` null (Probelauf gegen Budget „Test“ grün). Verifier: Iteration 1 FAIL (Flow-Test fand den
  abgehenden Toast nach dem Merge von main, Timing, kein App-Fehler), Iteration 2 PASS. Platzhalter bleibt „ohne Payee“ klein
  wie in der Übersicht. Sichtpunkte pk1 bis pk3 in der Prüfliste. Nicht gepusht, nicht deployt.
- **Nächster Schritt:** `/review-agents` für pkrwd vorschlagen (review-backend, review-frontend,
  review-tests). Dann Schnitt 10 budgeting-wnkgj verfeinern (Bestandsaufnahme, Prototyp-Abgleich Version 17, Fragen
  in die Prüfliste; Vorab-Entscheidungen in den Notes: Gelb für „noch nicht in YNAB“, Budget-Tabelle ab rund 1340 px).
  Offen in der Prüfliste: q-sheet, die Sichtpunkte „Ansehen“ (g20 bis g22, pk1 bis pk3). Verfeinern bei Gelegenheit:
  ledger-fp4wn (berührt Sammelaktionen), ledger-zqst5, design-system-m4e7q, ledger-h7de3, design-system-vf94m, ledger-t4rk8
  (Geld, Roman entscheidet). ledger-ayjzq (Sicht „Für mich“) nach Gespräch mit Roman. Vorschlag: `/architecture-review
  ledger` (mehr als 20 erledigte Tasks seit dem letzten Bestandsreview).
- Verfeinert wird nur die nächste Etappe. Spätere Etappen haben Zeilen, keine Tickets.
- Neue Ideen für spätere Etappen kommen hier als Zeile dazu.
- Bestehende Tasks tragen einen Tag: `etappe-1`, `etappe-2`, `etappe-3`, `laufend`, `danach`.
- Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler gehen in die Kann-Sammlung des BC.

## Etappe 1 · Bedeutungen festlegen (erledigt 2026-11-08)
Ziel: Jede Farbe und jedes Muster heißt überall dasselbe, und die drei Orte sind als Skizze
freigegeben. Klein halten.

Entschieden in ADR 0087 (Palette 2, Lena als Flag, drei Orte, Panel statt Dialog, Tastenregeln).
Vorlage für alles Weitere: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17,
Handy und PC), Skizze https://claude.ai/artifact/HPpe24Y1owgpSZLWvtdGWd (Version 2).

Reihenfolge: `infrastructure-nx14s` → `infrastructure-jc1y1` → `design-system-dak16`;
`design-system-bcdqy` läuft unabhängig. `jc1y1` und `dak16` sind verfeinert und warten in
`backlog/`, bis ihr Vorgänger erledigt ist; dann promoten.

- [x] Skizze und Prototyp der drei Orte, von Roman freigegeben (2026-11-07)
- [x] `infrastructure-nx14s` Layout-Invarianten als ein Sweep (2026-11-07)
- [x] `infrastructure-jc1y1` Designwert-Tests aufräumen (2026-11-08, Verifier PASS in Iteration 4)
- [x] `design-system-dak16` Palette 2 im Token-Layer, Kontrast-Wächter (2026-11-08, Verifier PASS in Iteration 2, Blick Roman freigegeben)
- [x] `design-system-bcdqy` Muster im Styleguide (2026-11-07, von Roman freigegeben)

## Etappe 2 · Lücken schließen (erledigt 2026-11-10)
Ziel: Nichts zwingt mehr zu YNAB außer Reports.

- ✓ `ledger-at63w` Buchung im Kassenbuch erfassen für jedes Konto ohne Import, mit Vorbelegung
  (Roman 2026-11-08: Import-Konto erst mit Abgleich, der kommt später).
- ✓ `budgeting-pscgb` Kategorien verwalten: anlegen, umbenennen, Gruppe wechseln, Gruppen (ADR 0088)
- ✓ `budgeting-sn2sn` Kategorie deaktivieren und eigene Reihenfolge, nur in BB (ADR 0088)
- ✓ `budgeting-t7dka` Bug: Targets anwenden übergeht in BB deaktivierte Kategorien (Nachtrag ADR 0088)
- ✓ `budgeting-e2z7v` Targets beim Speichern direkt nach YNAB (Nachtrag ADR 0084, Roman 2026-11-08)
- ✓ `ledger-qvjvs` Buchung zum Übertrag machen (Konten im Picker des Details, Schreibweg nach YNAB)
- ✓ `ledger-p3vq8` Push gelungen, Verknüpfung nicht geschrieben
- ✓ `ledger-z6sya` Buchung löschen im Detail (auch in YNAB), mit Rückgängig (Blick Roman 2026-11-10: passt).
- ✓ `ledger-m7mye` Betrag einer Buchung bearbeiten im Detail (auch in YNAB), im Prototyp ab Version 15
  (Roman, 2026-11-08); Blick Roman 2026-11-10: passt, Feldfehler statt Toast in `ledger-q42pg`.
  Überträge gesperrt bis Spike `ledger-m7a9q`.

## Etappe 3 · Ein Produkt
Ziel: Drei Orte, eine Sprache, Seite für Seite an der Skizze.

Prototyp Version 15 bis 17 (Roman, 2026-11-08, am Handy durchgegangen) korrigieren Version 14:
Budget-Tabs mit fester Höhe, Monatsbalken in der Historie mit Beträgen (Styleguide „Monatsbalken“),
Split-Beträge ohne Vorzeichen und Tastatur bleibt beim Feldwechsel offen, Konten als Ziel in jeder
Kategorieauswahl einer Buchung (Detail, Durchgehen, Erfassen, Split; Übertrag mit Gegenbuchung),
Betrag im Buchungsdetail bearbeiten, Amazon nur als Link mit Bestellnummer (keine API).

Schnitte (Roman, 2026-11-11). Zuerst 1 und 2, die Navigation geht mit einer schlichten Übersicht live.
Bis 4 und 5 stehen, zeigt „Zu tun“ auf den heutigen Sync-Ablauf; der Sync ist dann kein Ort mehr.
Baut ein Schnitt eine Seite um, gilt die Zeile „Browser-Tests beim Umbau“ als Kriterium.

1. ✓ `design-system-k6kfw` Navigation mit drei Orten, Zahnrad, ab 768 px links (2026-11-11)
2. ✓ `ledger-s1weh` Übersicht als Einstieg (Zu tun, Monat, Konten am Handy, „+ Buchung“) (2026-11-11)
3. ✓ `budgeting-w9yt4` Angepinnte Kategorien (2026-11-12)
4. ✓ `banking-import-d0fey` Import direkt ins Kassenbuch (2026-11-12) (nimmt `banking-import-t4raw`, `categorization-hz6vj` auf)
5. ✓ `ledger-v6s7c` Durchgehen Karte für Karte (2026-11-12)
6. ✓ `categorization-n3sxw` Regeln ins Kassenbuch (2026-11-13) (nimmt `categorization-krscs` auf), danach ✓ `ledger-hg5m5` Regel aus der Buchung (2026-11-14)
7. ✓ `ledger-ea70s` Ein Erfassen, Quick Add entfällt (2026-11-14) (nimmt `design-system-taytp` und `ledger-yhemh` auf)
8. ✓ Kassenbuch ordnen (2026-11-15): `ledger-d9z3n` (Handy, unter 1024 px), `ledger-cnaz3` (PC) und
   `ledger-zfj78` (Detail); `ledger-ayjzq` (Sicht „Für mich“) bleibt eigene Task
9. ✓ `design-system-gcpws` Liste mit Panel überall, generischer Tastentest (86ceeb32, 2026-11-15)
10. `budgeting-wnkgj` Budget angleichen
11. `design-system-x1abn` Logo und Icon

Eigenständig bleiben `banking-import-q7vd2` (confirmTan-Fehlerzweig) und `ledger-nb4tv` (ETF-Konto).
Die Zeilen darunter sind die Quelle der Schnitte.

- Übersicht als Einstieg statt Sync, Variante A: Zu tun (eine Hauptaktion), Monat mit Balken und
  Marke „heute“, Angepinnt, am Handy Konten.
- Angepinnte Kategorien (wählbar auf der Übersicht und im Kategorie-Detail, Zustand, kein Ereignis).
- Import ins Kassenbuch: direkt in Kassenbuch und YNAB als „zu prüfen“, nur unsichere Duplikate
  warten; automatisch und jederzeit von Hand anstoßbar.
- Durchgehen neuer Buchungen, Karte für Karte bestätigen, mit Amazon-Link und Duplikat-Karte.
- „+ Buchung“ auch in der Übersicht.
- PC: Seitenleiste mit Konten, Liste plus Panel überall (auch Regeln, Erfassen, Targets prüfen,
  Kategorien), Mehrfachauswahl mit Zusammenfassung im Panel.
- Tastenregel aus dem Budget für alle Listen; Auswahl-Dialoge mit Fokus in der Suche.
- Regeln ins Kassenbuch.
- Quick Add hört auf, ein eigener Ort zu sein.
- Budget an die Bedeutungen angleichen (Targets prüfen: zu viel Orange).
- Kassenbuch: Seitenpanel ordnen, Historie wie im Budget.
- Am PC große Leerflächen, am Handy Kontowahl mal Pillenleiste, mal Selectbox: vereinheitlichen.
- Navigation nur mit den drei Orten, Einstellungen hinter einem Symbol (heute sechs Punkte;
  Styleguide §7.4).
- Kassenbuch am Handy: die zwei wischenden Leisten entfallen, Filter-Knopf mit Sheet und gesetzte
  Filter als Chips (Styleguide §7.5).
- Wochentag eines Targets als Auswahlfeld statt sieben Knöpfe im `Segmented` (Styleguide §7.5).
- Panel rund 30 % der Breite und ab 1024 px (heute fest 340, 360 und 420 px;
  Styleguide §7.6).
- Kategorie-Picker nicht mehr über einem Sheet stapeln (`CategoryPicker.sheetLayered`); die Auswahl
  ersetzt den Inhalt des Sheets und führt zurück (Styleguide §7.15).
- Rückmeldung am PC über der Auswahl-Leiste des Kassenbuchs statt auf ihr (Styleguide §7.13).
- Budget-Historie: Filter nach Art als Auswahlfeld „Zeigen“ statt `ChipBar` (Styleguide §7.11).
- Browser-Tests beim Umbau: Wird eine Seite umgebaut, fliegen ihre `[flow]`-, `[tastatur]`-,
  `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu
  geschrieben, je Flow einer. Nicht nachziehen. `[fable]`, der Layout-Sweep und die Backend-Tests
  bleiben (Roman, 2026-11-07).
- Tastenregeln aus Styleguide §7.8 und §7.9 als ein generischer Test über alle Listen mit Panel,
  wie der Layout-Sweep, statt eines Tests je Seite.
- Navigation ab 768 px (`md`) links als Seitenleiste oder Rail (heute Leiste oben; Styleguide §7.4
  und §7.15).
- Logo und Icon: Vorlage Buddy in `standards/frontend/brand/` (Entwurf 2026-11-07, Palette 2).
- `banking-import-t4raw` zeitgesteuertes Vorbereiten
- `banking-import-q7vd2` confirmTan-Fehlerzweig
- `categorization-hz6vj` Kategorisieren im Import
- `design-system-taytp` Kategorie-Picker vereinheitlichen: Import und Erfassen nutzen nur `CategoryPicker`,
  `BottomSheet.categoryPicker` entfällt (aus Etappe 2 verschoben, Roman 2026-11-08)
- ~~`categorization-krscs` Regel-Dialog~~ (in categorization-n3sxw aufgegangen, verworfen 2026-11-13)
- `ledger-hg5m5` Regel-Angebot nach Kategorie-Änderung
- `ledger-ayjzq` Sicht „Für mich“ (Lena-Filter)
- `ledger-nb4tv` ETF-Konto und Abgleich (aus Etappe 2 verschoben, Roman 2026-11-10)

## Laufend
Unabhängig von den Etappen, weil es im Alltag stört, eine Sicherheitsgrenze betrifft oder
jede Arbeit bremst.

- `infrastructure-g4wy8` Verbindungsabbrüche
- ✓ `ledger-mj3a9` Bob-Betragsdeckel (2026-11-10)
- `ledger-t4rk8` Betrag eines Übertrags in BB ändern
- ✓ `design-system-ak97z` Umschalter im Sheet Betrag hält die Tastatur offen (2026-11-10)
- `infrastructure-mshaf` Gate schneller und stabil
- `infrastructure-rm6zb` Wächter gegen Formatangaben in interpolierten Strings (Fable)

## Danach, bei Bedarf
- Reports (bis dahin in YNAB).
- Budget-Stufe 3: erwartete Einnahme, Leihen, Entwurf, Targets mit Deckel.
- ETF-Bewertung, ING-Import.
- Benachrichtigungen: `infrastructure-jsys4`, `banking-import-r9tfx`, `ledger-g47hp`
- `budgeting-bk6tq` Spike Vortrag ab dem ersten Tag
- `infrastructure-phadg` Altlasten Test-Doktrin
