---
id: budgeting-nyx75
title: "Kategorie-Gruppe in BB deaktivieren: die Gruppe verschwindet samt ihren Kategorien aus Budget-Liste und Pickern, in YNAB bleibt sie"
status: done
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

## Outcome

Kategoriegruppe in BB deaktivieren ist gebaut (Nachtrag ADR 0088 vom 2026-11-20). `./verify.sh` endet mit `VERIFY: PASS` (dotnet 5570 grün, node 358 grün). Die ersten beiden Läufe waren nur durch bekannte oder lastbedingte Flakes rot (Layout-Zustand `targets-pruefen-nicht-neu-gelesen @ 412 px`, Zeitgrenze des Kassenbuch-Tests AC14 mit 1000 Buchungen, einmal `[flow:design-system-export]` mit Timeout unter Last, allein grün), der dritte Lauf war grün. Das README-Delta ist mit `npm run check:readme-delta -- --bc budgeting` geprüft (Exit 0, „Ganz nachlesen: keine Einträge“).

**Zustand und Regeln (rein, `src/Shared/Budgeting.fs`)**
- `CategoryLayout` hat `DeactivatedGroups: Set<Guid>`, neuer Typ `CategoryGroupActiveSave`, `ManagedCategoryGroup` hat das Pflichtfeld `Active`.
- `effectiveDeactivated layout placements` (wirksam deaktiviert: eigenes Flag oder Gruppe deaktiviert, nach YNABs heutiger Zuordnung), `monthPlacements` (Gruppe und Kategorie aus einem gelesenen Monat), `groupDeactivationCheck` (reiht die Sätze aus `deactivationCheck` je Kategorie in der übergebenen Reihenfolge). `pickerCategories` nimmt jetzt das wirksame Set statt des Layouts.
- `YnabMonthCategory` hat `GroupId: Guid option`. Entscheidung zum Lesepfad (Task-Text älter als budgeting-m5pqn): der Monats-Decoder liest `category_group_id`, und der YNAB-Stand speichert sie in der neuen Spalte `ynab_month_snapshot_categories.group_id` (`ALTER TABLE` mit Abfangen von „duplicate column name“, in `createYnabMonthSnapshotSchemaOn`, nicht in `initializeBudgetingSchema`). So liest die Budget-Seite die Zuordnung aus dem Stand ohne YNAB-Aufruf (ADR 0101 Punkt 1), Targets anwenden aus dem ohnehin gelesenen Monat. Ein Stand von vor der Spalte liest sich ohne Gruppe, bis das nächste Beobachten ihn ersetzt (höchstens bis zum nächsten Befehl auf dem Monat oder zur Übernahme). `YnabMonth` wird sonst nirgends serialisiert gespeichert.
- `arrangeGroups` setzt `Active` der Gruppe aus `DeactivatedGroups`, die Kategorien behalten ihren eigenen Zustand (nicht das wirksame Set): nur so kann der Client nach „Aktivieren“ die vorher einzeln deaktivierten Kategorien wieder einzeln zeigen. Auf der Seite wirkt das wie das wirksame Set, weil eine deaktivierte Gruppe nur als ein Eintrag erscheint.

**Server**
- `src/Server/Budgeting/Persistence.fs`: Tabelle `budget_category_group_layout` (`group_id`, `deactivated`, `updated_at`), angelegt in `createCategoryLayoutSchemaOn` (läuft in `initializeBudgetingSchema`, `initializeBudgetingSchema` selbst ist unverändert, Rücksicht auf budgeting-yx51n), `setCategoryGroupActiveOn`, `loadCategoryLayoutOn` liest beide Tabellen.
- `src/Server/Budgeting/Categories.fs`: `setCategoryGroupActiveWith` (Aktivieren ohne Prüfung und ohne YNAB; Deaktivieren: `FetchGroups`, unbekannte Gruppe „Gruppe nicht gefunden.“ ohne Monat, genau ein `FetchMonth`, BBs Soll künftiger Monate für alle Kategorien der Gruppe in einer Abfrage über das neue `futureAssignedForOn`, geprüft wird jede nicht gelöschte Kategorie der Gruppe laut Monat, auch ausgeblendete und einzeln deaktivierte, in BBs Reihenfolge; „Künftige Monate nicht lesbar, Gruppe nicht deaktiviert.“). `saveCategory` bekommt `loadLayout` und prüft gegen die Liste mit BBs Gruppenzustand; ist der Zustand nicht lesbar, wird nichts geschrieben. `pickerOptionsWith` nutzt das wirksame Set.
- `QueryHandlers.deactivatedForPage` und `CommandHandlers.deactivatedForTargets` lesen das wirksame Set aus dem Monat (`preparedMonth` gibt dafür den rohen Monat mit zurück).
- `src/Server/Validation.fs`: `validateCategorySave` lehnt Anlegen in und Verschieben in eine deaktivierte Gruppe ab („Gruppe Wohnen ist in BB deaktiviert.“, eine Kategorie, die schon darin steht, bleibt unberührt); `validateCategoryGroupActiveSave` („Keine Gruppe angegeben.“).
- `src/Shared/Api.fs` und `src/Server/Api.fs`: Endpoint `CategoryApi.setCategoryGroupActive`.

**Client** (`Components/Budget/Categories.fs`, `CategoriesView.fs`)
- Am Gruppenkopf „Bearbeiten“ (Name „Gruppe Wohnen bearbeiten“, `EditGroup`), Formular „Gruppe bearbeiten“ mit Schalter „Aktiv“ (`Input.switchRow`, `GroupActiveSwitchText`). Schalter und Name zugleich: erst `setCategoryGroupActive`, dann der Name nach YNAB. Ablehnung am Schalter (`ErrorDisplay.inline'`, `FailedCall = SavingGroupActive`), nie als Toast.
- Abschnitt „Deaktiviert (n)“ über `inactiveEntries`: Gruppen (YNABs Folge) als ein Eintrag mit „Gruppe · n Kategorien“ bzw. „Gruppe · keine Kategorien“ (`inactiveGroupDetail`) und „Aktivieren“ („Gruppe Wohnen aktivieren“), dann die einzeln deaktivierten Kategorien aktiver Gruppen; n zählt die Einträge.
- Nach dem Deaktivieren: Abschnitt aufgeklappt, Eintrag leuchtet, rollt ins Bild, trägt `DeactivatedNotice`. Nach dem Aktivieren: Gruppe wieder an ihrer Stelle, Kopf leuchtet und rollt ins Bild. Scheitert es: „Nicht aktiviert: …“ rot am Eintrag.
- Gruppenauswahl und Vorbelegung von „+ Kategorie“ nur mit aktiven Gruppen (`activeGroups`), ↑↓ am PC übergehen deaktivierte Gruppen.

**Tests**
- Neu `src/Tests/CategoryGroupLayoutTests.fs` (25 Tests): wirksam deaktiviert (eigene Flags und Gruppe, Hinein- und Herausschieben, Gruppe ohne Placements), `groupDeactivationCheck` (ohne Geld, leer, positiv, negativ, künftig, Reihenfolge), `applyDeactivation` mit wirksamem Set; Handler mit In-Memory-SQLite und zählendem Port (Deaktivieren und Aktivieren überleben einen neuen Lese-Durchgang, genau ein `FetchMonth`, Aktivieren ruft YNAB nicht, Aktivieren lässt eine einzeln deaktivierte Kategorie deaktiviert und die Reihenfolge stehen, ausgeblendete und einzeln deaktivierte Kategorie mit Geld lehnt ab, künftige Zuweisung über echte Übernahme, leere Gruppe, unbekannte Gruppe ohne `FetchMonth`, Soll nicht lesbar, Kategorie-Spiegel überschreibt nichts); `getCategories`-Optionen; Budget-Seite einmal beobachtet und einmal aus dem gespeicherten Stand (prüft auch die neue Spalte); Monats-Decoder; Targets Vorschau und Anwenden; Validierung (Anlegen und Verschieben ohne YNAB-Schreiben, Zustand nicht lesbar, leere Guid); Endpoint über `categoryApiWith` (Berliner Monat am Monatswechsel, übergebene Datenbank, `saveCategory` liest den Zustand von dort).
- Gegenproben von Hand: `deactivatedForPage`, `deactivatedForTargets` und `pickerOptionsWith` auf `layout.Deactivated` zurückgestellt: Seiten-, Picker- und Targets-Test werden rot (Mutantenliste genau diese drei), danach wiederhergestellt.
- `src/Tests/CategoryAdminClientTests.fs`: Sperre beim Speichern über alle Folgen bis Länge 3 jetzt auch mit Gruppen-Schalter allein und mit Schalter und Name zugleich (Orakel erweitert um `SavingGroupActive`); 10 neue Fälle (Titel und Schalter nur für bestehende Gruppe, Deaktivieren, Ablehnung am Schalter, Schalter und Name, Aktivieren samt Fehler am Eintrag, einzeln deaktivierte bleibt nach Aktivieren der Gruppe einzeln, Abschnitt Deaktiviert mit Gruppen vor Kategorien und Zählung, Gruppenauswahl und Vorbelegung, Tasten, Angepinnt Block und Sheet ohne Kategorien deaktivierter Gruppen).
- `RemotingEndpointNegativeTests.fs`: Zeile `CategoryApi.setCategoryGroupActive` (leere Gruppe an der Grenze, ohne Einrichtung). `StructureGuardTests.apiWirings`: Verdrahtung von `setCategoryGroupActiveWith` (Datenbank, Berliner Monat, Uhr) und `saveCategory` mit BBs Zustand, je mit Gegenbeweis. Das wirksame Set in Picker, Seite und Targets ist auf Verhaltensebene belegt (Gegenproben oben); die Live-Verdrahtung des Pickers (`pickerOptionsWith … loadCategoryLayout`) stand schon im Wächter.
- Fixtures: `Active` je Gruppe in `scripts/layout-states.mjs` (keine weitere Fixture in `scripts/*.test.mjs` liefert `ManagedCategoryGroup`). Neue Layout-Zustände `kategorien-gruppe-deaktiviert` (Abschnitt aufgeklappt, eine Gruppe mit langem Namen und eine einzelne Kategorie) und `kategorien-gruppe-bearbeiten` (Formular mit Schalter), Handy und PC, ohne Verletzung.
- In 13 Testdateien `GroupId = None` an `YnabMonthCategory` nachgezogen (reine Feldergänzung).

**Vergleich mit der Skizze (Version 4, Abschnitt 2)** [human-eye, bleibt offen für Roman am Gerät], BB mit Mock-Daten:
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/nyx75/bb-nyx75-handy-F-liste.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/nyx75/bb-nyx75-handy-G-abgelehnt.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/nyx75/bb-nyx75-handy-H-nach-deaktivieren.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/nyx75/bb-nyx75-pc-F-liste.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/nyx75/bb-nyx75-pc-G-abgelehnt.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/nyx75/bb-nyx75-pc-H-nach-deaktivieren.png`
(Skript dazu: `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/2f547bf2-b03c-403f-becf-43160bc54c80/scratchpad/nyx75/shots.mjs`)

Abweichungen:
- Der Gruppenname am Kopf steht in Versalien klein (`ListRoles.groupHeader`, BB heute), die Skizze zeigt ihn normal geschrieben und fett. BB gilt, weil die Skizze den Kopf nur um „Bearbeiten“ ändert.
- Die Ablehnung am Schalter trägt das Fehler-Icon von `ErrorDisplay.inline'` (Task schreibt den Baustein vor), die Skizze zeigt nur roten Text.
- Am Handy ist „Aktivieren“ 48 px hoch (Trefferfläche nach Styleguide), in der Skizze flacher; am PC wie in der Skizze.

Kann-Sammlung budgeting (Zeilen):
- Kassenbuch-Filter (`CategoryMultiSelect`) und Bob (`BotApi.getCategories`) bieten Kategorien einer deaktivierten Gruppe weiter an, wie bei einzeln deaktivierten (bekannte Lücke aus budgeting-sn2sn).
- Ein YNAB-Stand von vor der Spalte `group_id` kennt die Gruppe nicht: bis zum nächsten Beobachten des Monats zeigt die Budget-Seite die Kategorien einer frisch deaktivierten Gruppe noch (kein Geld wird unsichtbar, nur eine Zeile zu viel).
- Der Styleguide zeigt `Button.quietTextFor` weiter mit „Umbenennen“ als Beispieltext.
- Der Satz unter „Was hier nicht passiert“ im README (Kategorien anlegen oder umbenennen bleibt in YNAB) ist seit budgeting-pscgb überholt.
