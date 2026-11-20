---
id: budgeting-nyx75
title: "Kategorie-Gruppe in BB deaktivieren: die Gruppe verschwindet samt ihren Kategorien aus Budget-Liste und Pickern, in YNAB bleibt sie"
status: todo
type: feature
context: budgeting
created: 2026-11-20
completed:
depends_on: [design-system-001]
blocks: []
tags: [kategorien, gruppe, deaktivieren, frontend]
related_adrs: [0083, 0088]
related_research: [ynab-api-kategoriegruppen-ausblenden-2026-11-20]
prior_art: [budgeting-sn2sn, budgeting-pscgb]
---

## Why
Roman (2026-11-20): „Ich kann keine Budget-Gruppen löschen.“ Auf der Seite „Kategorien“ lassen sich Gruppen heute nur
anlegen und umbenennen (budgeting-pscgb). Die YNAB-API kann Gruppen weder löschen noch ausblenden: `SaveCategoryGroup`
kennt nur `name`, ein `DELETE` gibt es nicht (Spezifikation 1.91.0, Bericht
`ynab-api-kategoriegruppen-ausblenden-2026-11-20`). Roman hat „In BB ausblenden“ gewählt: wie „Kategorie deaktivieren“
(budgeting-sn2sn), nur für die ganze Gruppe. In YNAB bleibt die Gruppe bestehen.

## What
**Zustand** (ADR 0083: Zustand, kein Ereignis; ADR 0088 Nachtrag 2026-11-20)
- Neue Tabelle `budget_category_group_layout` (`group_id` Schlüssel, `deactivated`, `updated_at`), wiederholbar in
  `initializeBudgetingSchema`. Nach YNAB geht nichts, der Kategorie-Spiegel berührt die Tabelle nie.
- `CategoryLayout` bekommt `DeactivatedGroups: Set<Guid>`, `loadCategoryLayoutOn` liest beide Tabellen.

**Wirksam deaktiviert**
- Eine Kategorie ist wirksam deaktiviert, wenn sie selbst oder ihre Gruppe deaktiviert ist. Die Regel steht rein in
  `src/Shared/Budgeting.fs` (etwa `effectiveDeactivated layout placements : Set<Guid>`).
- Jede heutige Nutzung von `layout.Deactivated` liest dieses Set: `pickerCategories`, `arrangeGroups`,
  `deactivatedForPage` (`QueryHandlers.fs`), `deactivatedForTargets` (`CommandHandlers.fs`), damit auch
  `applyDeactivation`, `targetApplicationLines` und das Sheet „Angepinnt“.
- Eine Kategorie, die YNAB in der Gruppe neu anlegt oder in sie verschiebt, ist ohne Zutun deaktiviert. Verlässt sie
  die Gruppe, gilt wieder ihr eigener Zustand.
- Für Budget-Seite und Targets fehlt heute die Zuordnung zur Gruppe (`BudgetGroup` ohne Id, `YnabMonthCategory` nur
  `GroupName`). Der Worker liest `category_group_id` im Monats-Decoder mit oder nimmt die Placements aus
  `getCategoriesPlaced`; ob `YnabMonth` irgendwo serialisiert gespeichert wird, prüft er.

**Deaktivieren** (Endpoint `CategoryApi.setCategoryGroupActive`, `CategoryGroupActiveSave = { GroupId: Guid; Active: bool }`,
Handler `setCategoryGroupActiveWith` in `Server.Budgeting.Categories`)
- Zuerst `FetchGroups`. Unbekannte Gruppe (auch intern und „Credit Card Payments“): „Gruppe nicht gefunden.“
- Dann genau ein `FetchMonth` für den laufenden Berliner Monat. Geprüft wird jede nicht gelöschte Kategorie der Gruppe,
  auch einzeln deaktivierte und in YNAB ausgeblendete: 0 verfügbar und keine BB-Zuweisung in einem übernommenen
  künftigen Monat. Die künftigen Zuweisungen liest eine Abfrage für alle Kategorien der Gruppe (kein N+1).
- Hält eine Kategorie Geld, lehnt BB mit allen Gründen ab und schreibt nichts. Die Meldung reiht die Sätze aus
  `deactivationCheck` je Kategorie in BBs Reihenfolge (über `groupDeactivationCheck`), etwa „Restaurant hat noch
  24,50 €. Erst verschieben, dann deaktivieren. Kantine hat im Dezember 2026 50,00 € zugewiesen. Erst zurücknehmen,
  dann deaktivieren.“
- Soll nicht lesbar: „Künftige Monate nicht lesbar, Gruppe nicht deaktiviert.“
- Eine leere Gruppe lässt sich deaktivieren.

**Aktivieren** prüft nichts, ruft YNAB nicht auf und bringt den vorigen Stand zurück: einzeln deaktivierte Kategorien
bleiben deaktiviert und stehen danach einzeln im Abschnitt „Deaktiviert“. BBs Reihenfolge je Gruppe bleibt.

**Folgen in BB**
- Die Gruppe fehlt samt Kategorien in jedem Kategorie-Picker, in der Gruppenauswahl des Kategorie-Formulars, in der
  Budget-Liste, in Targets anwenden und im Sheet „Angepinnt“.
- Hat eine ihrer Kategorien im Monat Aktivität oder Verfügbares, steht die Gruppe in diesem Monat mit genau diesen
  Kategorien da, je mit „deaktiviert“ wie heute. Verschieben, Zuweisen und Pausieren bleiben dort möglich.
- In eine deaktivierte Gruppe legt BB keine Kategorie an und verschiebt keine dorthin: die Auswahl zeigt sie nicht,
  `validateCategorySave` lehnt ab mit „Gruppe Wohnen ist in BB deaktiviert.“
- Regeln aus `rules.yml`, bestehende Buchungen und Historie: wie bei einer einzeln deaktivierten Kategorie, keine
  eigene Prüfung, keine Warnung.

**Seite „Kategorien“** (Form legt die Skizze fest: https://claude.ai/artifact/Ko5u9VtVYxY9hFsqCV83LD, Abschnitt 2,
Version 4, von Roman am 2026-11-20 freigegeben)
- Eine deaktivierte Gruppe fehlt oben und steht im Abschnitt „Deaktiviert (n)“ als ein Eintrag, ihre Kategorien nicht
  einzeln: Name gedämpft, darunter klein „Gruppe · 3 Kategorien“ (ohne Kategorien „Gruppe · keine Kategorien“), rechts
  „Aktivieren“ (secondary, klein, Name „Gruppe Wohnen aktivieren“). Gruppen in YNABs Folge vor den einzelnen
  Kategorien, n zählt die Einträge.
- Bedienelement zum Deaktivieren: Schalter „Aktiv“ im Gruppen-Formular, `Input.switchRow`,
  Satz „Deaktiviert verschwindet sie samt ihren Kategorien aus Auswahl und Budget. Buchungen und Historie bleiben. Nur
  in BB, YNAB bleibt unverändert.“. Der Knopf am Gruppenkopf heißt „Bearbeiten“, das Formular „Gruppe bearbeiten“.
- Ablehnung am Schalter (`ErrorDisplay.inline'`), nie als Toast. Ändern sich Schalter und Name zugleich, geht der
  Schalter zuerst an BB, der Name danach nach YNAB.
- Nach dem Deaktivieren ist „Deaktiviert“ aufgeklappt, der Eintrag leuchtet kurz, rollt ins Bild und trägt „In YNAB noch
  sichtbar, dort ausblenden.“ (`DeactivatedNotice`) bis zur nächsten Aktion. Nach dem Aktivieren steht die Gruppe
  wieder an ihrer Stelle, der Kopf leuchtet kurz. Scheitert das Aktivieren: rot „Nicht aktiviert: …“ am Eintrag.
- Toast nur wie heute für eine Antwort nach „Zurück“ (§7.13). Keine Bestätigungsfrage, das Deaktivieren lässt sich mit
  „Aktivieren“ zurücknehmen.

## Acceptance criteria
- [ ] Domain-Test (rein): `effectiveDeactivated` enthält eigene Flags und alle Kategorien deaktivierter Gruppen; eine
      Kategorie, die in eine deaktivierte Gruppe kommt, ist deaktiviert; eine, die sie verlässt, gilt nach ihrem
      eigenen Flag; eine deaktivierte Gruppe ohne Placements ändert nichts.
- [ ] Domain-Test (rein): `groupDeactivationCheck` lässt eine Gruppe ohne Geld und eine leere Gruppe zu, lehnt ab bei
      Verfügbarem (positiv und negativ) und bei künftiger Zuweisung, auch einer einzeln deaktivierten Kategorie; die
      Meldung nennt jede Kategorie mit Betrag in BBs Reihenfolge.
- [ ] Domain-Test (rein): `applyDeactivation` mit dem wirksamen Set: Gruppe fehlt ohne Aktivität und Verfügbares,
      sonst nur mit diesen Kategorien, je `Deactivated = Some true`.
- [ ] Handler-Test (In-Memory-SQLite): Deaktivieren und Aktivieren überleben einen neuen Lese-Durchgang, der
      Kategorie-Spiegel überschreibt nichts; Ablehnung schreibt nichts; unbekannte Gruppe abgelehnt ohne `FetchMonth`;
      Aktivieren lässt eine einzeln deaktivierte Kategorie deaktiviert und die Reihenfolge unverändert; genau ein
      `FetchMonth` je Prüfung (zählender Port).
- [ ] Handler-Test: Soll künftiger Monate nicht lesbar ergibt „Künftige Monate nicht lesbar, Gruppe nicht
      deaktiviert.“, nichts geschrieben.
- [ ] Abfrage-Test: `getCategories` enthält keine Kategorie einer deaktivierten Gruppe.
- [ ] Abfrage-Test: Budget-Seite lässt Kategorien einer deaktivierten Gruppe ohne Geld weg und markiert die mit
      Aktivität. Gegenprobe: liest `deactivatedForPage` nur `layout.Deactivated`, wird der Test rot.
- [ ] Handler-Test: Targets anwenden (Vorschau und Anwenden) übergeht Kategorien einer deaktivierten Gruppe;
      Gegenprobe mit `deactivatedForTargets`.
- [ ] Validierung: Anlegen in und Verschieben in eine deaktivierte Gruppe abgelehnt mit „Gruppe <Name> ist in BB
      deaktiviert.“ ohne YNAB-Aufruf; leere Guid an `setCategoryGroupActive` abgelehnt.
- [ ] Client-Funktion (rein, .NET): Gruppenauswahl ohne deaktivierte Gruppe; Abschnitt „Deaktiviert“ zeigt die Gruppe
      als einen Eintrag ohne ihre Kategorien, Gruppen vor Kategorien, n zählt Einträge; „Angepinnt“ (Sheet und Block)
      ohne Kategorien deaktivierter Gruppen.
- [ ] Client-`update` (.NET): Deaktivieren, Ablehnung am Bedienelement, Aktivieren aus „Deaktiviert“, Fehler beim
      Aktivieren am Eintrag; Sperre beim Speichern über alle Folgen bis Länge 3, auch Schalter und Name zugleich
      (Vorbild `CategoryAdminClientTests.fs`, Doktrin Punkt 20).
- [ ] Je neuem Endpoint eine Zeile in `src/Tests/RemotingEndpointNegativeTests.fs`.
- [ ] Live-Verdrahtung des Endpoints und das wirksame Set in Picker, Seite und Targets in
      `StructureGuardTests.apiWirings` mit Gegenbeweis (Doktrin Punkt 3).
- [ ] Neues Pflichtfeld in einem Record aus `src/Shared/` (etwa `ManagedCategoryGroup.Active`) in den Fixtures von
      `scripts/*.test.mjs` im selben Schritt nachgezogen.
- [ ] Layout-Sweep: neuer Zustand `kategorien-gruppe-deaktiviert` (Abschnitt aufgeklappt mit einer Gruppe und einer
      einzelnen Kategorie, langer Gruppenname) und Gruppen-Formular mit Schalter, Handy und PC, ohne Verletzung.
- [ ] README budgeting nachgezogen (Gruppe deaktivieren, wirksam deaktiviert), Delta mit `npm run check:readme-delta`
      geprüft.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.
- [ ] Seite neben der Skizze verglichen, Abweichungen nur die genannten; Screenshots je Ansicht (Handy 412 px und PC)
      im Outcome. [human-eye]

## Notes
- Entschieden (Roman, 2026-11-20, alle Empfehlungen; Nachtrag ADR 0088 freigegeben). Skizze freigegeben (2026-11-20, Version 4, Abschnitt 2):
  1. Wo deaktiviert man? (a) Schalter „Aktiv“ im Gruppen-Formular, (b) eigener Textknopf am Gruppenkopf.
     Entschieden (a).
  2. Was bringt „Aktivieren“ zurück? Entschieden: nur die vorher aktiven Kategorien.
  3. Was wird geprüft? Entschieden: alle Kategorien der Gruppe, auch einzeln deaktivierte und in YNAB ausgeblendete.
  4. Knopf am Kopf „Umbenennen“ oder „Bearbeiten“ (Formular „Gruppe bearbeiten“)? Entschieden: „Bearbeiten“ (Formular „Gruppe bearbeiten“).
  5. Zusätzlich Ablehnung an der Grenze beim Anlegen oder Verschieben in eine deaktivierte Gruppe? Entschieden: ja.
- YNAB-API: Ein nicht dokumentiertes `hidden` im `PATCH` wird nicht versucht. Nebenbefund des Berichts: `YnabClient`
  nutzt die alten Pfade `/budgets/{id}`, laut Changelog v1.79.0 weiter gültig, aber nicht mehr dokumentiert.
- In YNAB ausgeblendete Gruppen fehlen schon heute (`interpretCategoryGroupsResponse`); BBs Deaktivieren bleibt davon
  getrennt (ADR 0088 Nachtrag 2026-11-08).
- Gruppen bleiben in YNABs Folge, eine eigene Gruppen-Reihenfolge gehört nicht hierher. Eine deaktivierte Gruppe wird
  nicht umbenannt.
- Bekannte Lücken aus budgeting-sn2sn gelten auch hier (Kassenbuch-Filter `CategoryMultiSelect`, Bob über
  `BotApi.fs` `getCategories`), Zeile in der Kann-Sammlung budgeting, kein Ticket.
- Heutige Seite für die Skizze: `npm run screenshot:route` auf `/budget/categories`, Layout-Zustände `kategorien` und
  `kategorien-deaktiviert`.
