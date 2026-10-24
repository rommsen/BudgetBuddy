---
id: ledger-mas5g
title: "Spike: Lässt die YNAB-API zu, die Split-Zeilen einer bestehenden Buchung zu setzen, zu ändern und aufzuheben, und mit welchem Aufruf"
status: done
type: spike
context: ledger
created: 2026-10-24
completed:
depends_on: []
blocks: [ledger-kxme4, ledger-pafkp]
tags: [backend, ynab, split, spike, v2]
related_adrs: [0006, 0028, 0054, 0055, 0058]
related_research: []
prior_art: [ledger-k2rbn]
---

## Why
Roman will Split-Buchungen in BB bearbeiten (Entscheidung 2026-10-24). Der Schreibweg nach
YNAB ist dafür offen: Nach der YNAB-OpenAPI ist nach meiner Erinnerung das Ändern der
`subtransactions` einer bestehenden Buchung nicht vorgesehen (das Feld gilt beim Anlegen).
Das ist nicht belegt. Davon hängt ab, ob `ledger-kxme4` und `-6` überhaupt wie geplant
gebaut werden können oder ob ein anderer Weg nötig ist (Buchung löschen und neu anlegen,
Splits nur in BB halten).

## What
Zu klären, jeweils mit Beleg (OpenAPI-Text, Antwort der API):
1. Nimmt `PUT /budgets/{id}/transactions/{tid}` oder `PATCH /budgets/{id}/transactions`
   `subtransactions` an, um (a) eine unaufgeteilte Buchung aufzuteilen, (b) die Zeilen einer
   Split-Buchung zu ändern, (c) einen Split aufzuheben? Welche Fehler kommen zurück?
2. Falls nein: Was ist der kleinste sichere Ersatz? Kandidaten: Buchung in YNAB löschen und
   mit gleicher `import_id` neu anlegen (Folgen für `approved`, `cleared`, Verknüpfung,
   Übertrags-Gegenbuchungen und die YNAB-Id im Kassenbuch), oder Splits nur in BB führen
   und die Abweichung zu YNAB sichtbar machen (Abweichungs-Sicht, ADR 0028).
3. Übertrags-Zeilen im Split (`transfer_account_id` je Subtransaction): schreibbar?

Der Spike läuft gegen die OpenAPI-Beschreibung und, wo nötig, gegen ein Wegwerf-Budget in
YNAB (nie gegen Romans echtes Budget). Ergebnis ist eine kurze Notiz in der Task und, wenn
sich eine Richtung ergibt, ein ADR-Entwurf; die Folge-Tasks `ledger-kxme4` und `-6`
werden danach angepasst.

## Acceptance criteria
- [ ] Zu jeder der drei Fragen aus Punkt 1 steht ein Ergebnis mit Beleg (Zitat aus der
      OpenAPI oder aufgezeichnete Antwort).
- [ ] Ist Punkt 1 verneint: ein empfohlener Ersatz mit Folgen für `approved`, `cleared`,
      Verknüpfung und Übertrags-Gegenbuchungen.
- [ ] `ledger-kxme4` und `-6` sind auf das Ergebnis angepasst oder als hinfällig
      markiert; Roman ist über die Wahl informiert, wenn sie eine Abweichung berührt.
- [ ] Es wurde keine Buchung in Romans echtem Budget verändert.

## Notes
Entworfen am 2026-10-24 (Sammel-Modeling). Ziel-Lage: todo, kann sofort starten. Rahmen: ein
Arbeitstag. Ist die Lösung mitten im Spike schon bekannt und billig, halte sie fest und hör
auf. Der Spike schreibt keinen Produktivcode. Dateien: keine im Quellbaum (höchstens ein
Wegwerf-Skript im Scratchpad).

## Outcome

Spike ledger-mas5g, 2026-10-24. Kein Schreibaufruf gegen YNAB, kein Produktivcode. Quelle ist die
offizielle OpenAPI-Beschreibung `https://api.ynab.com/papi/open_api_spec.yaml` (Version 1.87.0, am
2026-10-24 geladen) und die API-Doku `https://api.ynab.com/` (dort steht zu Splits nichts, die
Seite nennt nur Drittanwendungen). Forum und Support-Artikel waren in dieser Umgebung nicht
abrufbar (keine Web-Tools), also gibt es dazu keinen Beleg. Aufgezeichnete Antworten gibt es
keine, weil nichts geschrieben werden durfte.

### Ergebnis zu Frage 1

Beide Routen (PUT einzeln, PATCH Bulk) nehmen denselben Body (`SaveTransactionWithOptionalFields`).
Dort steht beim Feld `subtransactions`:

> "An array of subtransactions to configure a transaction as a split. Updating `subtransactions` on an existing split transaction is not supported and will return an error. Splits are not allowed on tracking accounts or on transfers between on-budget accounts; a transfer to a tracking account can be a split."

Beim Feld `category_id`:

> "To configure a split transaction, you can specify null for `category_id` and provide a `subtransactions` array as part of the transaction object. If an existing transaction is a split, the `category_id` cannot be changed."

Bei `date` und `amount`: "Split transaction dates cannot be changed and if a different date is supplied it will be ignored." und "Split transaction amounts cannot be changed and if a different amount is supplied it will be ignored."

- (a) Unaufgeteilte Buchung aufteilen: Die Spec verbietet es nicht, sagt aber auch nicht, dass es geht. Der Satz zum Fehler nennt nur "an existing split transaction". Das Feld ist als "to configure a transaction as a split" beschrieben, die Beispielsprache zielt aufs Anlegen. **Nicht belegt, nur per Wegwerf-Budget prüfbar.** Offene Teilfragen: Wird der Aufruf angenommen? Wird `subtransactions` stillschweigend ignoriert (Antwort 200, Buchung bleibt unaufgeteilt)? Das zweite wäre gefährlicher als ein Fehler, deshalb muss der Server nach dem PUT die Antwort auf `subtransactions` prüfen.
- (b) Zeilen einer Split-Buchung ändern: **Verneint, belegt.** Zitat oben, "will return an error". Der Fehlertext und der Statuscode (die Spec kennt für PUT nur 200 und 400) sind nicht belegt.
- (c) Split aufheben: **Verneint, belegt.** Aufheben hieße, `subtransactions` zu entfernen und eine `category_id` zu setzen. Beides schließt der Text aus ("category_id cannot be changed" bei einem bestehenden Split, "Updating subtransactions ... not supported").

### Ergebnis zu Frage 3 (Übertrags-Zeilen)

`SaveSubTransaction` kennt genau diese Felder: `amount`, `payee_id`, `payee_name`, `category_id`, `memo`. Ein `transfer_account_id` gibt es dort nicht; gelesen wird es auf `SubTransaction` (`transfer_account_id`, "If a transfer, the account_id which the subtransaction transfers to"). Das passt zu ADR 0006: Die Transfer-Zeile geht über die Transfer-`payee_id` (am Konto als `transfer_payee_id`). Für die Unter-Zeile selbst beschreibt die Spec nur "The payee for the subtransaction"; dass eine Transfer-Payee dort einen Übertrag erzeugt, belegt unsere gebaute Anlage-Strecke (ynab-001, im Betrieb), nicht der Spec-Text. Einschränkung aus der Spec: Splits sind auf Tracking-Konten und bei Überträgen zwischen Budget-Konten nicht erlaubt, ein Übertrag auf ein Tracking-Konto darf Split sein. Beim Anlegen sind Übertrags-Zeilen also schreibbar (Ersatzweg Löschen und Neuanlegen), beim Ändern einer bestehenden Split-Buchung gilt Frage 1(b). Was YNAB mit den Gegenbuchungen beim Löschen tut, steht nicht in der Spec. **Nicht belegt, nur per Wegwerf-Budget prüfbar.**

### Frage 2: kleinster sicherer Ersatz

Empfohlen: **Löschen und Neuanlegen** (`DELETE /plans/{id}/transactions/{tid}`, dann `POST /plans/{id}/transactions` mit derselben `import_id`). Splits nur in BB zu halten wird verworfen: Es widerspricht ADR 0054 und lässt die Kategoriezahlen in YNAB falsch (ADR 0028: "ein kaputter Split verfälscht Zahlen, auf die sich das Budget stützt").

Folgen:
- **import_id:** Die Anlage-Spec sagt: "409: A transaction on the same account with the same `import_id` already exists." Deshalb Delete zuerst. Ob eine gelöschte Buchung die `import_id` noch sperrt (Delta-Abrufe liefern gelöschte Buchungen als Tombstone), steht nicht in der Spec. **Nicht belegt, nur per Wegwerf-Budget prüfbar.** Ohne `import_id` anlegen wäre die Notlösung, kostet aber die Duplikat-Erkennung von BB (`duplicate_import_ids`) und den Abgleich beim Import.
- **approved:** Spec bei `approved`: "If not supplied, transaction will be unapproved by default." Beim Neuanlegen muss BB `approved` also ausdrücklich aus dem Spiegelstand mitgeben, sonst wird jede ersetzte Buchung wieder unbestätigt.
- **cleared:** Beim Neuanlegen mitgeben. `reconciled` ist die Ausnahme: Ob die API eine neue Buchung als `reconciled` annimmt oder auf `cleared` zurücksetzt, und was ein Löschen einer abgeglichenen Buchung auslöst (Kontostand-Abgleich bricht), ist **nicht belegt, nur per Wegwerf-Budget prüfbar**. Empfehlung: reconciled Buchungen im Split-Editor sperren, bis das geprüft ist.
- **Verknüpfung, YNAB-Id im Kassenbuch:** Die Buchung bekommt eine neue YNAB-Id, ebenso neue Subtransaction-Ids (ADR 0046). Der Schreibweg muss die neue Id aus der POST-Antwort (`data.transaction_ids`, `data.transactions`) übernehmen und in BB als Zuordnung des bestehenden Eintrags führen. Der nächste Abruf meldet die alte Id als gelöscht (Delta) und die neue als neu; der Spiegel muss beides über die `import_id` zusammenführen, sonst entsteht ein Doppel. Die Mechanik dafür gibt es noch nicht und gehört in `ledger-kxme4`.
- **Übertrags-Gegenbuchungen:** Löscht YNAB beim Löschen der Hauptbuchung die Gegenbuchungen der Split-Überträge mit (zu erwarten, **nicht belegt**), entstehen sie beim Neuanlegen mit neuen Ids. Die ADR-0046-Verknüpfung (Subtransaction-Id zur Gegenbuchung) und die Nachverknüpfungs-Arbeit aus `ledger-h6mer` müssen dann nach jedem Ersatz laufen. Eine manuell geänderte Gegenbuchung in YNAB (Memo, Kategorie) ginge beim Löschen verloren.
- **Fenster zwischen Delete und POST:** Scheitert der POST nach erfolgreichem Delete, fehlt die Buchung in YNAB. BB behält die Daten. Der Fehlschlag-Zustand aus ADR 0055 braucht dafür einen eigenen Grund ("in YNAB gelöscht, Neuanlage offen") und "Erneut senden" muss POST ohne erneutes Delete ausführen können. Dazu kommt das Rate-Limit (200 Anfragen je Stunde): ein Ersatz kostet zwei Aufrufe plus einen GET /payees, wenn Übertrags-Zeilen dabei sind.

### Probelauf, den Roman (oder der Conductor mit Romans Freigabe) gegen ein Wegwerf-Budget machen muss

Roman legt in YNAB ein Wegwerf-Budget an (zwei Budget-Konten, ein Tracking-Konto), ein Skript im Scratchpad ruft nacheinander auf:
1. PUT mit `category_id: null` und `subtransactions` auf eine unaufgeteilte Buchung (Fall a): angenommen, Fehler oder stillschweigend ignoriert? Statuscode, Fehlertext.
2. PUT mit geänderten `subtransactions` auf eine Split-Buchung (Fall b): Statuscode, Fehlertext.
3. DELETE einer Buchung mit `import_id`, danach POST mit derselben `import_id`: 201 oder 409?
4. POST mit `approved: true` und `cleared: cleared`, dann `reconciled`: kommt der Stand in der Antwort zurück?
5. DELETE einer Split-Buchung mit Übertrags-Zeile: verschwindet die Gegenbuchung? Neuanlage: neue Gegenbuchung, neue Ids?
Ergebnis in den ADR 0058 einarbeiten, bevor `ledger-kxme4` gebaut wird.

### Folge-Tasks

#### ledger-kxme4 (anzupassen, nicht hinfällig)

Folgender Abschnitt wird an `.agentheim/board/ledger/backlog/ledger-kxme4-split-schreibweg.md` angehängt:

```markdown
## Spike-Ergebnis (ledger-mas5g, 2026-10-24)

Die YNAB-OpenAPI schließt das Ändern von `subtransactions` einer bestehenden Split-Buchung
aus: "Updating `subtransactions` on an existing split transaction is not supported and will
return an error." Ebenso: "If an existing transaction is a split, the `category_id` cannot
be changed." (https://api.ynab.com/papi/open_api_spec.yaml, Version 1.87.0). Damit sind das
Ändern der Zeilen und das Aufheben eines Splits per Update nicht möglich. Das Aufteilen einer
unaufgeteilten Buchung per PUT ist in der Spec weder erlaubt noch verboten (nicht belegt).

Der Rahmen ändert sich so:
1. Punkt 3 "eigene Funktion `YnabClient.updateSplits`" wird `YnabClient.replaceSplitTransaction`:
   DELETE der Buchung, danach POST mit gleicher `import_id`, gleichem Datum, Betrag, Konto,
   Payee, Flag, ausdrücklich gesetztem `cleared` und `approved` und den neuen
   `subtransactions` (beim Aufheben mit `category_id`). Für eine unaufgeteilte Buchung zuerst
   PUT mit `category_id: null` und `subtransactions` versuchen, bei Ablehnung oder
   stillschweigendem Ignorieren (Antwort ohne `subtransactions`) derselbe Ersatzweg.
2. Der Fehlschlag-Zustand (ADR 0055, Punkt 4) braucht einen zusätzlichen Grund "in YNAB
   gelöscht, Neuanlage offen". "Erneut senden" muss POST allein ausführen können.
3. Die neue YNAB-Id (und neue Subtransaction-Ids, ADR 0046) aus der POST-Antwort in den
   Eintrag übernehmen; `observe` und der Spiegel führen alte (gelöscht) und neue Id über die
   `import_id` zusammen. Übertrags-Gegenbuchungen entstehen neu (Annahme, siehe ADR 0058);
   die Nachverknüpfung aus `ledger-h6mer` muss nach jedem Ersatz laufen.
4. Buchungen mit `cleared: reconciled` im Schreibweg ablehnen, bis der Probelauf zeigt, wie
   YNAB sie behandelt.
5. Vorbedingung: Probelauf gegen ein Wegwerf-Budget (fünf Aufrufe, Liste im Outcome von
   `ledger-mas5g`) und Annahme von ADR 0058 (proposed). Erst danach verfeinern und bauen.
6. Punkt 4 (Übertrags-Zeilen) ist beim Neuanlegen schreibbar: Transfer-Payee-Auflösung wie
   ADR 0006; Tracking-Konto-Regel der Spec ("Splits are not allowed on tracking accounts or
   on transfers between on-budget accounts") im Validierungsschritt prüfen.

Acceptance criteria bleiben "to be defined", bis ADR 0058 angenommen ist.
```

#### ledger-pafkp (anzupassen, nicht hinfällig)

Folgender Abschnitt wird an `.agentheim/board/ledger/backlog/ledger-pafkp-split-editor-im-kassenbuch.md` angehängt:

```markdown
## Spike-Ergebnis (ledger-mas5g, 2026-10-24)

Die Oberfläche bleibt wie geplant, der Schreibweg dahinter ändert sich (siehe das Spike-Ergebnis
in `ledger-kxme4` und ADR 0058, proposed): YNAB lässt die Zeilen einer bestehenden
Split-Buchung nicht per Update ändern oder aufheben. "Ändern" und "Aufheben" laufen als Löschen
und Neuanlegen. Folgen für die Oberfläche:
1. Speichern kann länger dauern (zwei Aufrufe) und einen zusätzlichen Fehlschlag-Zustand haben
   ("in YNAB gelöscht, Neuanlage offen"). Die Fehlschlag-Leiste aus `ledger-sfab0` braucht
   dafür einen Text und "Erneut senden".
2. Der Editor ist für Buchungen mit `cleared: reconciled` gesperrt, bis der Probelauf klärt,
   wie YNAB sie behandelt (Hinweistext im Detail).
3. "Rückgängig" im Toast ist ein zweiter Ersatz (Löschen und Neuanlegen) und kostet zwei
   weitere YNAB-Aufrufe; die Historie zeigt beide Schreibvorgänge. Ratenbegrenzung beachten.
4. Die YNAB-Id der Buchung ändert sich nach dem Speichern. Der Eintrag im Kassenbuch bleibt
   bestehen (Zuordnung über `import_id`); Kriterien dazu stehen in `ledger-kxme4`.
5. Abhängigkeit bleibt `ledger-kxme4`; zusätzlich Vorbedingung Probelauf und ADR 0058.
```

### Für Roman

Das Ändern und Aufheben von Splits geht in YNAB nur über Löschen und Neuanlegen der Buchung. Damit ändern sich die YNAB-Id, die Subtransaction-Ids und vermutlich die Übertrags-Gegenbuchungen, und es gibt kurz ein Fenster, in dem die Buchung in YNAB fehlt. Bitte entscheide, ob das für die Split-Bearbeitung akzeptabel ist, und ob du für den Probelauf ein Wegwerf-Budget in YNAB anlegst (ich durfte nichts schreiben und habe keinen Zugriff darauf). Alternativ bliebe die Split-Bearbeitung in v2 aus dem Umfang, dann sind `ledger-kxme4` und `ledger-pafkp` hinfällig.
