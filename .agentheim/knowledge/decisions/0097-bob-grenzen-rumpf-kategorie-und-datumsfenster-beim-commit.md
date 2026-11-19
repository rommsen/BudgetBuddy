---
id: 0097
title: Bob-Grenzen am Rumpf der Vorschau, an der Kategorie und am Datumsfenster beim Commit
scope: ledger
status: accepted
date: 2026-11-19
supersedes: []
superseded_by: []
related_tasks: [ledger-b7k2q]
related_adrs: [0066, 0068]
related_research: []
---

# ADR 0097: Bob-Grenzen am Rumpf der Vorschau, an der Kategorie und am Datumsfenster beim Commit

## Context

ADR 0068 begrenzt Bob je Buchung, je Berliner Kalendertag, im Buchungsdatum und in der Zahl der
Anfragen. Das Bestandsreview ledger vom 2026-11-19 (Gruppe G1) fand drei Lücken an derselben
Schnittstelle:

- Die Vorschau las den Rumpf ohne eigene Obergrenze. Im ganzen Server gibt es keine Rumpfgrenze,
  es gilt Kestrels Vorgabe von rund 30 MB.
- `category` war nur auf leer geprüft, ohne Längengrenze. Die 422-Meldung
  (`category_not_found`, `category_ambiguous`) gibt den Wert zurück. Payee (200) und Memo (500)
  waren schon begrenzt.
- Nur die Vorschau prüfte das Datumsfenster. Eine Vorschau um 23:55 Berliner Zeit mit Datum heute
  minus 31 Tage, committet um 00:05, buchte 32 Tage zurück. Möglich ist das nur in den
  15 Minuten vor Berliner Mitternacht, weil die Vorschau 15 Minuten gilt; ein Datum in der
  Zukunft wird durch Warten nicht neu, deshalb kann nur das Zurückliegen kippen.

## Decision

1. **Rumpfgrenze der Vorschau: 8192 Bytes, beim Lesen gezählt.** `BotApi.readBody` liest
   höchstens `botMaxBodyBytes + 1` Bytes aus dem Rumpf. Sind es mehr als `botMaxBodyBytes`
   (8192, `src/Server/Validation.fs`), antwortet die Vorschau 400 `invalid_request` mit
   `fields: [{ "field": "body", "message": "Der Rumpf darf höchstens 8192 Bytes haben" }]`,
   ohne JSON zu parsen, ohne YNAB-Aufruf, ohne Quick-Add-Auflösung und ohne gespeicherte
   Vorschau. Gezählt werden die gelesenen Bytes, nicht `Content-Length` (der kann fehlen oder
   lügen). Die Grenze greift nach Token und Sperre. Commit und `GET /bot/categories` lesen keinen
   Rumpf und sind nicht betroffen.
2. **Kategorie höchstens 200 Zeichen** nach dem Trimmen, wie Payee. Sonst Feldfehler `category`
   „Höchstens 200 Zeichen“, gesammelt mit den übrigen Feldfehlern (400 `invalid_request`). Damit
   ist auch die Rückgabe in der 422-Meldung begrenzt.
3. **Datumsfenster beim Commit am Berliner Tag des Commits neu geprüft.** `bookWithinLimit`
   prüft vor der Tagesgrenze `checkBotBookingDate` mit dem Berliner Tag des Commits. Außerhalb
   antwortet der Commit 422 `date_not_allowed` mit dem Satz der Domäne („Das Datum darf
   höchstens 31 Tage zurückliegen“). Die Prüfung kommt nach dem Blick ins Kassenbuch
   (`bookedFromLedger`): eine schon gespeicherte Buchung wird bei einem erneuten Commit nach
   Mitternacht weiter mit 200 gemeldet, wie bei der Tagesgrenze. Das Ergebnis bleibt im
   Commit-Slot, ein zweiter Commit derselben Vorschau liefert wieder 422; Bob legt eine neue
   Vorschau an und bekommt dort 400 mit Feld `date`. Entschieden von Roman am 2026-11-19.
4. **Wie ADR 0068: fest im Code.** Diese Grenzen stehen wie die aus ADR 0068 fest im Code. Sie
   dürfen nur strenger werden und sind nicht konfigurierbar (keine Umgebungsvariable, kein
   Setting).

## Gründe

- **8192 statt rund 4 KB.** Die längste gültige Anfrage (Payee 200, Kategorie 200, Memo
  500 Zeichen, jedes Zeichen als `\uXXXX` maskiert, also 6 Bytes) braucht rund 5,5 KB. Mit 8192
  lehnt die Grenze nie eine sonst gültige Anfrage ab.
- **Eigenes Lesen statt Kestrel.** `KestrelServerOptions.Limits.MaxRequestBodySize` gilt für
  den ganzen Host, auch für `/api` mit Fable.Remoting. `IHttpMaxRequestBodySizeFeature` je
  Anfrage fehlt im `DefaultHttpContext` der Tests, und Kestrel wirft bei Überschreitung eine
  Ausnahme außerhalb des JSON-Fehlerformats der Bot-Schnittstelle. Das eigene Lesen antwortet wie
  alle Bot-Antworten als JSON über `respond` und `errorBody` und ist mit `MemoryStream` testbar.
- **400 mit Feld `body` statt 413.** Ein unlesbarer Rumpf meldet schon 400 mit Feld `body`
  („Der Rumpf ist kein gültiges JSON“). Bob wertet `fields` aus, ein eigener Status bräuchte
  einen eigenen Zweig.
- **422 statt 400 beim Commit.** Die Anfrage selbst ist gültig, der Zustand lehnt ab, wie
  `daily_limit_reached` beim Commit.
- **Fenster am Tag des Commits, nicht am Tag der Vorschau.** ADR 0068 Punkt 3 misst „heute“,
  und beim Buchen ist heute der Tag des Commits. Das Fenster am Tag der Vorschau zu messen ließe
  32 Tage zu.

## Consequences

- Bob sieht nur neue Ablehnungen, die eine gültige Anfrage nie trifft (Rumpf, Kategorie), oder
  die nur in den 15 Minuten vor Berliner Mitternacht auftreten können (Commit).
- Belegt durch Tests in `src/Tests/BotApiTests.fs` (Commit um Berliner Mitternacht, Rumpf mit
  8192 und 8193 Bytes, 401 ohne Token) und `src/Tests/BotValidationTests.fs` (Kategorie mit 200
  und 201 Zeichen).
- Kein Client-Code und keine UI betroffen.
