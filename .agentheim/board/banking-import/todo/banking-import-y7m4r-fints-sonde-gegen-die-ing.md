---
id: banking-import-y7m4r
title: "Spike: FinTS-Sonde gegen die ING klärt Konten, MT940-Referenzen, Depot und Bibliothek, bevor banking-import-r28hx verfeinert wird"
status: todo
type: spike
context: banking-import
created: 2026-11-20
completed:
depends_on: []
blocks: [banking-import-r28hx]
tags: [fints, ing, owner-run, live]
related_adrs: [0071, 0093]
related_research: [ing-fints-anbindung-2026-11-06]
prior_art: [banking-import-p5mcp]
---

## Why
Die Produktregistrierungsnummer der DK ist am 2026-11-20 eingetroffen. Die Recherche
`ing-fints-anbindung-2026-11-06` lässt Fragen offen, die nur ein echter Dialog mit der ING beantwortet.
Von ihnen hängen der Dedup-Schlüssel und die Bibliothek für banking-import-r28hx ab. Roman (2026-11-20):
„Erstmal nur Girokonto und Extra-Konto. Ich würde jetzt aber im Spike gucken, ob wir auch an Depotdaten
kommen.“ Die Bibliothek wählt der Architect nach dem Befund, ein Python-Sidecar ist erlaubt.

## What
Zwei Wegwerf-Sonden außerhalb des Repos (Scratchpad oder `<DATA_DIR>/fints-probe/`), gleiche Fragen:

1. **python-fints** (5.x) als Referenz, weil sie die ING-Sonderregel (Sicherheitsfunktion 999) kennt.
2. **libfintx** (1.4.x) als .NET-Kandidat, etwa als Einzeldatei-Programm mit `dotnet run`.

Beide lesen `FINTS_PRODUCT_ID`, `ING_FINTS_USER` und `ING_FINTS_PIN` aus `.env`. Zu beantworten:

- **BPD:** Welche Geschäftsvorfälle und Versionen bietet die ING heute (HKKAZ, HKSAL, HKCAZ, HKWPD)? Gibt
  es inzwischen CAMT?
- **Konten (UPD):** Erscheinen Girokonto und Extra-Konto? Liefern HKKAZ und HKSAL für beide Umsätze und
  Saldo? Wie weit reicht der Abruf zurück (90 Tage oder mehr)?
- **Referenz:** Trägt `:61:` nach `//` eine Bankreferenz? Welche Schlüssel stehen in `:86:` (`EREF+`, `KREF+`,
  `MREF+`)? Zwei Abrufe mit mindestens einem Tag Abstand: Bleibt je Buchung dieselbe Referenz stehen? Gibt
  es inhaltlich gleiche Zeilen (Betrag, Datum, Text) ohne unterscheidende Referenz?
- **Vormerkungen:** Kommt ein MT942-Teil oder sonst ein Hinweis auf vorgemerkte Umsätze?
- **Depot:** Liefert HKWPD Bestände des Direkt-Depots (Stückzahl, Kurs, Wert), und braucht es eine TAN?
- **Bibliothek:** Kommt libfintx mit Verfahren 999 bei angebotenem iTAN 900 zurecht? Stimmen Umlaute in den
  Verwendungszwecken (libfintx Issue #85, ASCII statt Latin1)?
- **Produktnummer:** Nimmt die ING die eigene Nummer ohne Rückfrage an (keine 9075, kein „nicht registriert“)?

**Stop-Loss:** if, mid-spike, the mitigation is already known and cheap, record it and stop. Konkret: Lehnt die
ING den Dialog mit der eigenen Nummer ab, oder liefert sie das Extra-Konto nicht, wird das notiert und der Spike
endet; Roman entscheidet, ob banking-import-r28hx so weiterläuft.

## Acceptance criteria
- [ ] Die README von `banking-import` hat einen Abschnitt „Live-Befund ING per FinTS (<Datum>)“ mit den Antworten
      auf jede Frage aus „What“, auch „nicht ermittelt“ mit Grund.
- [ ] Der Befund enthält nur Struktur (Segmentnamen, Versionen, Feldnamen, Zählungen wie „37 von 40 Buchungen mit
      Bankreferenz“), keine IBAN, keine Beträge, keine Namen, keine Verwendungszwecke, keine Zugangsdaten und nicht die
      Produktnummer.
- [ ] Im Repo liegt kein Code der Sonden; `git status` zeigt nach dem Spike nur README, Task und ADR.
- [ ] Ein ADR (Architect) entscheidet die Bibliothek (libfintx in-process oder python-fints als Sidecar) und den
      Dedup-Schlüssel für ING-Buchungen samt `import_id`-Form, begründet mit dem Befund.
- [ ] Die Acceptance criteria von banking-import-r28hx sind mit dem Befund verfeinert oder als offen markiert, und die
      Depot-Frage hat eine Empfehlung (eigene Task, Kann-Sammlung oder fällt weg). [human-eye]

## Notes
Die Platzhalter in `.env.example` sind schon da (2026-11-20).

Owner-Task: Er braucht Romans ING-Zugang in `.env`. Nicht per `work` starten, der Conductor führt ihn mit Roman.
Ein Worker im Worktree hat weder `.env` noch die Freigabe, echte Bankdaten zu lesen.

- Vorbedingung laut ING: Roman war in den letzten 90 Tagen in der App oder im Online-Banking angemeldet, sonst lehnt
  die ING den FinTS-Log-in ab.
- Adresse `https://fints.ing.de/fints/`, BLZ 50010517, Benutzerkennung die 10-stellige Zugangsnummer oder der
  Benutzername, Kunden-ID leer (Recherche, Abschnitt 1).
- Rohdaten (BPD, UPD, MT940) liegen nur unter `<DATA_DIR>/fints-probe/` und werden nach dem Spike gelöscht oder bleiben
  dort, nie im Repo. Referenzvergleich über Hashes je Buchung, damit kein Klartext in Notizen landet.
- PIN und Produktnummer erscheinen in keinem Log und in keiner Ausgabe der Sonden.
- Die Standardnummer von python-fints wird nicht benutzt (laut DK-FAQ nur für interne Tests).
