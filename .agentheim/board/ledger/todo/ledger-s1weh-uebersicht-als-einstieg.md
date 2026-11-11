---
id: ledger-s1weh
title: "Übersicht als Einstieg (Variante A): Zu tun mit einer Hauptaktion, Zähler an der Übersicht, „+ Buchung“, am PC zweispaltig"
status: todo
type: feature
context: ledger
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, design-system-k6kfw]
blocks: [budgeting-w9yt4, ledger-v6s7c]
tags: [etappe-3, frontend, uebersicht]
related_adrs: [0056, 0086, 0087]
related_research: []
prior_art: []
---

## Why
Die Übersicht beantwortet zwei Fragen: wo stehe ich, was steht an (ADR 0086). `design-system-k6kfw` bringt
die Übersicht mit Kopf, Monat und Konten. Es fehlt, was ansteht: heute muss Roman dafür Sync, Kassenbuch
und Budget einzeln aufsuchen.

## What
Die Übersicht bekommt „Zu tun“ als geordnete Liste mit genau einem Hauptknopf (Styleguide §7.4, §7.14),
einen Zähler an der Übersicht in Rail und Seitenleiste und „+ Buchung“. Am PC wird sie zweispaltig.

Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg Version 17, Funktionen `todos`,
`todoList`, `home` (Variante A), `homeDesk`, `sidebar` (Zähler) und die Klasse `.fab`.

Übergang (Roman, 2026-11-11): Bis `banking-import-d0fey` (Import direkt) und `ledger-v6s7c` (Durchgehen)
stehen, führen die Einträge zu neuen Umsätzen in den heutigen Sync-Ablauf und ins Kassenbuch.

## Acceptance criteria

### Zu tun
- [ ] Abschnitt „ZU TUN“ als erster Block der Übersicht (Kopfzeile in Mono, Großbuchstaben, gedämpft),
      darin eine Karte mit den Einträgen in dieser Reihenfolge, jeder nur, wenn seine Bedingung gilt:
      1. „<n> Buchungen zu prüfen“, Zeile 2 „Import <Stand>“; Ziel: Kassenbuch mit Filter „Zu prüfen“.
         Bedingung: das Kassenbuch hat ungeprüfte Buchungen.
      2. „<Kategorie> ist im Minus“, Zeile 2 „<verfügbar> · Geld aus einer anderen Kategorie holen“;
         Ziel: das Detail dieser Kategorie im Budget, Bereich Geld, Holen, Betrag vorbelegt mit dem Minus.
         Je Kategorie im Minus ein Eintrag.
      3. „Targets anwenden“, Zeile 2 „<Summe> für <n> Kategorien“ (eine: „1 Kategorie“); Ziel: der Dialog
         Targets anwenden im Budget. Bedingung: im laufenden Monat fehlt bei mindestens einer Kategorie
         Geld zum Target.
      4. „<n> Rückfragen an Lena offen“, Zeile 2 „Lena sieht sie über den Filter“; Ziel: Kassenbuch mit
         Filter Lena. Bedingung: mindestens eine Buchung trägt das Lena-Flag (ADR 0056).
      5. „Buchung nicht in YNAB“, Zeile 2 „<Payee> · <Betrag> · erneut senden“; Ziel: diese Buchung im
         Kassenbuch. Je Buchung, deren Push gescheitert ist, ein Eintrag.
      6. „<Betrag> verteilen“, Zeile 2 „Noch keiner Kategorie zugewiesen“; Ziel: Budget. Bedingung: Zu
         verteilen ist größer als null und kein Eintrag 3 steht da.
- [ ] Der erste Eintrag steht groß: Text 16 px halbfett, darunter Zeile 2, darunter ein gefüllter Knopf
      im Akzent über die volle Breite. Beschriftung je Ziel: „Prüfen“ (1), „Geld holen“ (2), „Anwenden“
      (3), „Öffnen“ (4, 5, 6). Das ist die eine Hauptaktion der Ansicht (§7.14).
- [ ] Jeder weitere Eintrag ist eine Zeile mit Pfeil rechts; ein Klick führt zum selben Ziel.
- [ ] Vor jedem Eintrag eine Marke von 9 px: „braucht dich“ (Gelb) für 1, 3 und 6, Rot für 2, das
      Lena-Flag in der Flag-Farbe aus den Einstellungen für 4. Eintrag 5 trägt Gelb, nicht Rot wie im
      Prototyp: ein gescheiterter Push bleibt Gelb (Nachtrag ADR 0087, Roman 2026-11-08).
- [ ] Ohne Einträge steht eine Karte mit grünem Punkt, „Alles erledigt“ (halbfett) und „Neue Aufgaben
      erscheinen nach dem nächsten Import.“ (§7.12).
- [ ] Welche Einträge in welcher Reihenfolge erscheinen, berechnet eine reine Funktion aus Budgetmonat,
      Kassenbuch-Zählern und Lena-Zahl. Expecto-Test je Bedingung, für die Reihenfolge, für „1 Kategorie“
      gegenüber „<n> Kategorien“ und dafür, dass 6 bei offenem 3 fehlt.

### Zähler
- [ ] In Rail und Seitenleiste trägt die Übersicht die Zahl der ungeprüften Buchungen (Mono 11 px,
      „braucht dich“), bei null nichts. Die Leiste am Handy trägt keinen Zähler, wie im Prototyp.

### + Buchung
- [ ] Am Handy unten rechts über der Leiste ein runder Knopf „+ Buchung“ (Höhe 52 px, Symbol 20 px).
      Stehen Einträge in Zu tun, ist er ruhig (Grund `surface-elevated`, Akzentrand und Akzentschrift),
      sonst gefüllt im Akzent. So bleibt es bei einer Hauptaktion je Ansicht.
- [ ] Am PC steht „Buchung“ mit Plus als kleiner Knopf im Kopf neben „Abrufen“.
- [ ] Beide öffnen das Erfassen aus dem Kassenbuch (`ledger-at63w`) mit dessen Vorbelegung. Nach dem
      Speichern bleibt man auf der Übersicht, die Rückmeldung nennt Konto und Betrag (§7.13).

### PC
- [ ] Ab 1024 px zwei Spalten (links rund 1,1, rechts 1 Anteil, Abstand 24 px, höchstens 1240 px breit):
      links „Zu tun“, rechts „Monat“ und „Neueste Buchungen“ aus `design-system-k6kfw`. Unter 1024 px
      einspaltig in der Reihenfolge Zu tun, Monat, Konten.

### Schriftgrößen (Roman, 2026-11-11)
- [ ] Die Übersicht hält Styleguide §7.14 (höchstens drei Schriftgrößen je Ansicht): „noch N Tage“ auf 11 px
      wie die Abschnittsköpfe, der Monatsname in der Größe der Zeilen (`ListRoles.primary`). Heute sind es
      sieben Größen (Review-Lauf zu design-system-k6kfw).

### Tests und Vergleich
- [ ] Ein `[flow:uebersicht]`-Test bei 412 px: mit einer ungeprüften Buchung steht „1 Buchung zu
      prüfen“ als erster Eintrag mit Knopf „Prüfen“, der Knopf öffnet das
      Kassenbuch mit Filter „Zu prüfen“, „+ Buchung“ ist ruhig. Der Flow steht in `FLOWS` in
      `scripts/browser-usage.mjs`.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten (Übergangsziele statt
      Durchgehen, Gelb statt Rot bei Eintrag 5, Angepinnt fehlt bis `budgeting-w9yt4`). Screenshots bei
      412 und 1280 px neben dem Prototyp im Outcome.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.
- [ ] Roman hat die Übersicht am Handy (412 px, Touch) und am PC neben dem Prototyp angesehen.
      [human-eye]

## Notes
Schnitt 2 aus Etappe 3 (Fahrplan, Roman 2026-11-11).

Einzahl: bei einer Buchung „1 Buchung zu prüfen“, bei einer Rückfrage „1 Rückfrage an Lena offen“.

Abweichung vom Prototyp, Eintrag 1: Der Prototyp sagt „<n> neue Buchungen durchgehen“ mit Knopf „Jetzt
durchgehen“. Das Durchgehen gibt es erst mit `ledger-v6s7c`; bis dahin „zu prüfen“ und das Kassenbuch mit
Filter. `ledger-v6s7c` stellt den Eintrag auf den Prototyp um. Den Sync-Ablauf erreicht man über „Abrufen“
im Kopf (aus `design-system-k6kfw`).

Heute vorhanden und hier nur verlinkt: Filter „Zu prüfen“ und Lena im Kassenbuch, Hinweis „Noch nicht in
YNAB“ mit „Erneut senden“ (`src/Client/Components/Ledger/State.fs`), Geld holen und Targets anwenden im
Budget (`src/Client/Components/Budget/`). Eine neue API braucht es nicht, falls die Zahl der ungeprüften
Buchungen und der gescheiterten Pushes je Konto schon im Client ankommt; sonst klärt der Worker das mit dem
`tactical-modeler` und meldet es im RESULT.

Zähler (Review-Lauf 2026-11-11): Die Zahl steht in Gelb ohne Wort daneben; §2 verlangt bei Gelb ein Wort.
Sichtbar bleibt die Zahl wie im Prototyp, dazu ein `aria-label` „<n> zu prüfen“ am Zähler.

Der Conductor legt vor dem Start die Prototyp-Datei und Screenshots der Übersicht (Handy und PC) in den
Scratchpad und gibt Worker, Verifier und `design-quality` die Pfade mit.
