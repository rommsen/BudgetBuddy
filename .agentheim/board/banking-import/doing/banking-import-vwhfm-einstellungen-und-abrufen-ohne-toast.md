---
id: banking-import-vwhfm
title: "Einstellungen ohne Toast (R3): „✓ Gespeichert“ am Abschnitt oder Feld, Fehler rot unter dem Feld, Bankstatus und Push-TAN im Abschnitt, alle Texte deutsch"
status: doing
type: feature
context: banking-import
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc, design-system-gkded]
blocks: []
tags: [design-exploration, toast, frontend]
related_adrs: [0087, 0098]
related_research: []
prior_art: [ledger-vf5h6, banking-import-t4raw]
---

## Why
Regel R3 (design-system-ytgnc, von Roman am 2026-11-19 freigegeben): am Handy kein Toast, jede Meldung hat einen
festen Platz. Die Einstellungen melden heute 26 Ergebnisse als Toast, acht davon englisch („Default budget set“,
„Failed to load Lena flag colour: …“, Kann-Sammlung ledger Punkt 152). Skizze Abschnitt 3 „Einstellungen: gespeichert“:
der Abschnitt schließt und zeigt den neuen Wert, daneben „✓ Gespeichert“; Fehler stehen rot unter dem Feld, der
Abschnitt bleibt offen.

Das Sheet „Abrufen“ ist abgeteilt in banking-import-ns4re (eigener Baustein, eigener Meldungsweg über die Leiste).

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, freigegeben am
2026-11-19), Ansicht „Einstellungen: gespeichert“ und Zeile „Einstellungen“ der Tabelle „Übrige Seiten“. Pfade unter Notes.

### 1. Gespeichert über ein Formular (Token, Zugangsdaten, Sync-Zeitraum, Lena-Farbe)
- Der Abschnitt schließt (heute schon `EditingSection = None`) und zeigt den neuen Wert.
- Im Kopf der Karte steht rechts „✓ Gespeichert“ in der Erfolgsrolle (Skizze `.mk-saved`, halbfett, kleiner als der
  Titel) an der Stelle der Statusanzeige („Verbunden“, „Eingerichtet“). Mit der nächsten Aktion auf der Seite oder beim
  Verlassen kommt die Statusanzeige zurück. Kein Zeitlimit (R3).
- Die geänderte Zeile leuchtet kurz (Motion aus design-system-ytgnc, mit `prefers-reduced-motion` ohne Bewegung).
- Abweichung von der Skizze: die Skizze zeigt je Wert einen eigenen Abschnitt mit „Ändern“. BB behält die Karten mit
  „Bearbeiten“ (YNAB, Comdirect, Sync-Einstellungen, Kassenbuch: Lena fragen); „✓ Gespeichert“ steht deshalb im Kopf
  der Karte statt neben dem Titel des einzelnen Werts.

### 2. Gespeichert direkt aus einem Auswahlfeld (Standard-Budget, Standard-Konto, Quick-Add-Konto, Automatisch abrufen)
- Diese Felder speichern beim Ändern, die Karte bleibt offen (heute). „✓ Gespeichert“ steht unter dem Feld bzw. Schalter
  in der Erfolgsrolle (Hilfezeile des Felds), bis zur nächsten Aktion.
- Fehler: rot unter diesem Feld (`Input.groupWithError` bzw. `ErrorDisplay.inline'`, §5), das Feld zeigt wieder den
  gespeicherten Wert. Für „Automatisch abrufen“ gibt es das schon (`AutoFetchError`); die anderen drei Felder ziehen nach.

### 3. Fehler beim Speichern über ein Formular
- Rot unter dem Feld, der Abschnitt bleibt offen, die Eingaben bleiben: Token unter dem Token-Feld, Sync-Zeitraum unter
  „Transaktionszeitraum“, Lena-Farbe unter dem Auswahlfeld. Zugangsdaten Comdirect: der Server liefert
  `ComdirectCredentialsInvalid ("general", …)` ohne Feldbezug, deshalb steht der Fehler über „Zugangsdaten speichern“
  (Abweichung von „unter dem Feld“, Grund: kein Feldbezug in der Antwort).
- Text: „Nicht gespeichert: <Grund>“ (Skizze: „Nicht gespeichert: YNAB antwortet nicht“).

### 4. Verbindung testen und Bankverbindung
- YNAB „Verbindung testen“: das Ergebnis steht in der Karte unter den Knöpfen, in Lesen und Bearbeiten. Erfolg
  „✓ Verbunden, 2 Budgets gefunden“ (Einzahl „1 Budget“), Fehler rot mit dem Grund. Heute steht im Lesen-Modus nichts,
  nur der Toast.
- Comdirect „Verbindung testen“: das Ergebnis steht im Block „Bankverbindung“ (`bankConnectionBlock`) über Lesen und
  Bearbeiten. „Bankverbindung aktiv“ zeigt die Plakette „Verbunden“ nach `LoadBankStatus`, die Zeile leuchtet kurz; kein
  Text dazu. Fehler rot im Block, auch im Lesen-Modus (heute nur im Formular).
- Push-TAN: der gelbe Hinweis `tanWaitingBlock` mit „TAN bestätigt“ steht schon im Abschnitt (Skizze: „Push-TAN als gelber
  Hinweis im Abschnitt“). Der Toast entfällt ersatzlos.
- TAN bestätigt: Plakette wechselt auf „Verbunden“, Zeile leuchtet kurz. Im Formular heißt die Erfolgszeile
  „Zugangsdaten geprüft“ (heute „Zugangsdaten erfolgreich verifiziert!“, §8 ohne Ausrufezeichen). Fehler rot im Block.
- Bank getrennt: Plakette wechselt, Zeile leuchtet kurz, kein Text. Fehler steht schon im Block
  (`ErrorDisplay.cardCompact`), der Toast entfällt.

### 5. Laden
- Einstellungen nicht geladen: auf der Seite steht schon die Karte „Einstellungen konnten nicht geladen werden“ mit
  „Erneut versuchen“. Der Toast entfällt. Andere Seiten nennen den Grund dort, wo sie die Einstellungen brauchen
  (Kategorien, Quick-Add-Konto, ledger-k9tq7, ledger-pz028); dort ändert sich nichts.
- Lena-Farbe nicht geladen: in der Karte „Kassenbuch: Lena fragen“ rot „Lena-Farbe nicht geladen: <Grund>“ mit
  „Erneut versuchen“ (`LoadLenaFlagSettings`). Erledigt Kann-Sammlung ledger Punkt 152.

### 6. Texte deutsch (§8)
- `settingsErrorToString` in `Settings/State.fs` deutsch, ohne technischen Vorsatz: „YNAB-Token ungültig: …“,
  „YNAB antwortet nicht (HTTP <n>): …“, „Comdirect-Zugangsdaten ungültig: …“, „Verschlüsselung gescheitert: …“,
  „Datenbankfehler beim <Vorgang>: …“. Wortlaut darf der Worker nach §8 schärfen, englisch bleibt nichts.
- Server-Texte, die die Seite zeigt: `src/Server/Api.fs:290` („Comdirect credentials not configured“) und `:353`
  („No YNAB token configured“) deutsch; die Feldnamen in `src/Server/Validation.fs:37` bis `:50` („YNAB token“,
  „Client ID“, „Client Secret“, „Username“, „Password“) als „YNAB-Token“, „Client-ID“, „Client-Secret“,
  „Benutzername“, „PIN“ wie die Beschriftungen der Seite. `YnabError.Unauthorized "No YNAB token configured"` aus dem
  YNAB-Client (Test `LedgerSplitWriteTests.fs:1528`) ist ein anderer Pfad und bleibt.
- „Verbunden! n Budget(s) gefunden“ (`Settings/View.fs:220`) wird „Verbunden, n Budgets gefunden“ (Einzahl „1 Budget“).

### Jeder heutige Toast und sein neuer Platz (`src/Client/Components/Settings/State.fs`)
| Zeile | Text heute | Neu |
|---|---|---|
| 115 | „Failed to load settings: …“ | entfällt, Karte auf der Seite (5.) |
| 146 | „YNAB token saved successfully“ | Kopf YNAB „✓ Gespeichert“, Zeile „API Token“ leuchtet (1.) |
| 149 | Fehler Token | rot unter dem Token-Feld (3.) |
| 161 | „Connected! Found n budget(s)“ | Zeile unter „Verbindung testen“ (4.) |
| 164 | Fehler Verbindungstest YNAB | rot unter „Verbindung testen“ (4.) |
| 212 | „Comdirect credentials saved successfully“ | Kopf Comdirect „✓ Gespeichert“ (1.) |
| 215 | Fehler Zugangsdaten | rot über „Zugangsdaten speichern“ (3.) |
| 235 | „Sync settings saved successfully“ | Kopf Sync-Einstellungen „✓ Gespeichert“ (1.) |
| 238 | Fehler Sync-Zeitraum | rot unter „Transaktionszeitraum“ (3.) |
| 262 | „Automatisch abrufen gespeichert“ | „✓ Gespeichert“ unter Schalter bzw. „Alle“ (2.) |
| 296 | „Failed to load Lena flag colour: …“ | rot in der Karte Lena mit „Erneut versuchen“ (5.) |
| 316 | „Flag-Farbe für Lena gespeichert“ | Kopf Lena „✓ Gespeichert“ (1.) |
| 319 | Fehler Lena-Farbe | rot unter dem Auswahlfeld (3.) |
| 340 | „Default budget set“ | „✓ Gespeichert“ unter „Standard-Budget“ (2.) |
| 343 | Fehler Budget | rot unter „Standard-Budget“ (2.) |
| 364 | „Default account set“ | „✓ Gespeichert“ unter „Standard-Konto“ (2.) |
| 367 | Fehler Konto | rot unter „Standard-Konto“ (2.) |
| 387 | „Quick-Add-Konto gespeichert“ | „✓ Gespeichert“ unter „Quick-Add-Konto“ (2.) |
| 390 | Fehler Quick-Add-Konto | rot unter „Quick-Add-Konto“ (2.) |
| 406 | „Bankverbindung aktiv“ | Plakette, Zeile leuchtet (4.) |
| 410 | „Push-TAN in der Comdirect-App bestätigen“ | entfällt, gelber Hinweis steht schon (4.) |
| 413 | Fehler Verbindungstest Comdirect | rot im Block Bankverbindung (4.) |
| 429 | „Zugangsdaten geprüft“ | Plakette, Zeile leuchtet (4.) |
| 435 | Fehler TAN | rot im Block Bankverbindung (4.) |
| 472 | „Bank getrennt“ | Plakette, Zeile leuchtet (4.) |
| 478 | Fehler Trennen | entfällt, `cardCompact` im Block steht schon (4.) |

### Funktionsabgleich Seite „Einstellungen“
- Karten YNAB, Comdirect (mit Bankverbindung, Automatisch abrufen, Bank trennen mit Rückfrage), Sync-Einstellungen,
  Kassenbuch: Lena fragen; je „Bearbeiten“ und „Abbrechen“: bleibt (die Skizze zeigt nur zwei Abschnitte).
- Statusanzeige im Kopf („Verbunden“, „Eingerichtet“, „Nicht konfiguriert“): bleibt, „✓ Gespeichert“ steht dort bis zur
  nächsten Aktion.
- Verbindung testen YNAB und Comdirect, TAN bestätigt, Vorschlag der Lena-Farbe, Link zu den YNAB-Entwicklereinstellungen,
  Link zum Styleguide, Kopf mit „Zurück“ und „Einstellungen aktualisieren“: bleibt.
- Rückmeldungen: ändern sich (Tabelle). Toast am Handy und am PC: gestrichen (Roman, 2026-11-19, R3, ADR 0098).
- Nicht Teil dieser Task: die Anrede „Sie“ in „Die YNAB-Flag-Farbe, die Sie in YNAB Lena genannt haben“ und
  „Budget“/„Konto“ ohne Namen (nur ein Strich) im Lesen-Modus, solange kein Test lief. Beides ist heute so und bleibt.

### Am Code
- `src/Client/Components/Settings/Types.fs`: ein Rückmeldungszustand im `Model` statt Toasts, etwa „gespeichert an“
  (Karte oder Feld) und „Fehler an“ (Feld, Block, Karte mit Text); `AutoFetchError` geht darin auf. Welche Nachricht
  „nächste Aktion“ ist und die Rückmeldung löscht, als reine Funktion. `ExternalMsg.ShowToast` entfällt.
- `src/Client/Components/Settings/State.fs`: alle Zweige der Tabelle, `settingsErrorToString`.
- `src/Client/Components/Settings/View.fs`: `sectionHeaderLabeled`, `ynabSettingsCard`, `bankConnectionBlock`,
  `autoFetchBlock`, `comdirectSettingsCard`, `syncSettingsCard`, `lenaFlagCard`.
- `src/Client/State.fs:705` bis `:712`: Zweig `Settings.Types.ShowToast` entfällt.
- `src/Server/Api.fs:290`, `:353`, `src/Server/Validation.fs:37` bis `:50`: deutsche Texte, Server-Tests mitziehen.
- `scripts/layout-states.mjs`: Zustand `einstellungen` bleibt; neu `einstellungen-gespeichert` (Lena-Farbe gespeichert,
  Kopf mit „✓ Gespeichert“) und `einstellungen-fehler` (Quick-Add-Konto abgelehnt, roter Text unter dem Feld), je Handy
  und 1280 px; Kopf und Text brechen bei 320 px nicht und verdecken nichts.
- `scripts/settings-secrets.test.mjs` (`[flow:zugangsdaten-speichern]`, `[flow:bank-trennen]`) muss grün bleiben; sucht
  er einen Toast oder englischen Text, wird er auf den neuen Platz gezogen.

### Tests (Heimat-Ebene nach `.claude/rules/test-doctrine.md`)
- .NET (`src/Tests/SettingsViewTests.fs` oder neue `SettingsFeedbackTests.fs`), rein über `update`: je Zeile der Tabelle
  der neue Zustand; „nächste Aktion“ löscht ihn; kein Zweig liefert noch einen Toast; `settingsErrorToString` ohne
  englisches Wort für jeden Fall von `SettingsError`.
- Server: Validierungstexte und die zwei Sätze aus `Api.fs` deutsch (bestehende Tests anpassen).
- Kein neuer Browser-Flow.

## Acceptance criteria
- [ ] `grep -rn "ShowToast" src/Client/Components/Settings/` ist leer; `src/Client/State.fs` reicht keinen Toast der
      Einstellungen mehr weiter.
- [ ] Jede Zeile der Tabelle oben zeigt ihre Rückmeldung am neuen Platz mit dem genannten Text (.NET-Test je Zeile).
- [ ] Nach Speichern über ein Formular schließt der Abschnitt, der Kopf zeigt „✓ Gespeichert“ in der Erfolgsrolle bis zur
      nächsten Aktion, die geänderte Zeile leuchtet kurz (`prefers-reduced-motion` ohne Bewegung).
- [ ] Fehler stehen rot unter dem Feld (bzw. über „Zugangsdaten speichern“), der Abschnitt bleibt offen, die Eingaben
      bleiben (.NET-Test, Sweep `einstellungen-fehler`).
- [ ] Verbindungstest YNAB zeigt sein Ergebnis auch im Lesen-Modus; Fehler von Comdirect-Test und TAN stehen im Block
      Bankverbindung auch im Lesen-Modus.
- [ ] Kein englischer Text mehr, den die Seite zeigen kann (Client und die genannten Server-Stellen); Kann-Sammlung
      ledger Punkt 152 erledigt.
- [ ] Alle Funktionen mit „bleibt“ funktionieren wie vorher; `[flow:zugangsdaten-speichern]` und `[flow:bank-trennen]` grün.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome, dazu PC 1280 mit „✓ Gespeichert“ am Ort,
      ohne Toast), Abweichungen nur die in dieser
      Task genannten (Karten mit „Bearbeiten“ statt Einzelabschnitten, „✓ Gespeichert“ im Kopf der Karte, Fehler der
      Zugangsdaten über dem Knopf).
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Entscheidungen Roman (2026-11-19)
- **Rückmeldung am Ort auch am PC?** Ja (ADR 0098): „✓ Gespeichert“, Fehler am Feld, Leuchten und Plakette gelten auf
  allen Breiten, ohne Toast am PC. Folge: die Seite hat in keiner Breite einen Toast, ein Weg für alle Breiten.

## Notes
Captured 2026-11-19 aus ledger-vf5h6. Verfeinert 2026-11-19; das Sheet „Abrufen“ ging in banking-import-ns4re.

Kontext: Die Seite gehört keinem Kontext allein. YNAB-Token, Budget, Konto und Quick-Add-Konto sind ynab-sync, die
Lena-Farbe ledger, Comdirect, Bankverbindung, Automatisch abrufen und Sync-Zeitraum banking-import. Die Task bleibt
eine und in banking-import: die Änderung liegt in einer Komponente (`Components/Settings`, ein `Model`, ein Rückmeldungs-
zustand), eine Teilung nach Kontext ließe drei Worker in denselben Dateien arbeiten. banking-import hat den größten Teil
und beschreibt die Karte schon in seiner README („Speichern von Automatisch abrufen“). README-Deltas: banking-import für
die Comdirect-Karte; ynab-sync und ledger nur, falls ihre README die Rückmeldung der Einstellungen beschreibt.

Skizze und Screenshots (`…` = `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad`):
- Skizze: `…/vf5h6/durchgehen-handy.html`, Quelltext `…/vf5h6/skizze.src.html` (Einstellungen Zeilen 494 bis 505, CSS
  `.mk-saved` und `.mk-field` Zeilen 282 bis 295, Tabelle „Übrige Seiten“ Zeile 518)
- Skizze 412 px: `…/vf5h6/skizze-shots/34-einstellungen-gespeichert.png`
- Heute 412 px: `…/shots/vf5h6/heute-einstellungen-412.png`

Abhängigkeit: Motion „leuchtet kurz“ und die Erfolgsrolle für „✓ Gespeichert“ aus design-system-ytgnc (DS-Modul vorläufig
`Feedback`). Die Meldungsleiste braucht diese Task nicht.
