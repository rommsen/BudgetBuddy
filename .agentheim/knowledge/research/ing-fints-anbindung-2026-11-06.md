---
topic: Lesezugriff auf ING-Konten (Girokonto und Extra-Konto) per FinTS 3.0 PIN/TAN
date: 2026-11-06
requested_by: user
related_tasks: []
---

# Research: ING per FinTS anbinden (Saldo und Umsätze, auch Extra-Konto)

## Question

Enable Banking (PSD2/XS2A) liefert das ING Extra-Konto (Tagesgeld) nicht. ADR 0071 und `src/Server/BankPort.fs` sehen die ING per FinTS hinter dem Bank-Port vor. Gesucht: Liefert ING per FinTS das Extra-Konto mit Saldo und Umsätzen, welches TAN-Verfahren gilt, welche Bibliothek (.NET oder python-fints als Sidecar) passt, wie sieht die Buchungsreferenz für Dedup aus, wie oft ist SCA nötig, wie läuft die Produktregistrierung, und warum fehlen Tagesgeldkonten bei Aggregatoren.

## Summary

- **Extra-Konto ist per FinTS vorgesehen.** ING nennt auf ihrer FinTS-Hilfeseite Girokonto, Extra-Konto und Direkt-Depot als FinTS-fähig, Adresse `https://fints.ing.de/fints/`, BLZ 50010517, Benutzerkennung die 10-stellige Zugangsnummer [1], [2]. Aufträge (Zahlungen) gehen per FinTS nicht mehr, beim Extra-Konto seit 20.11.2021 [1]. Lesen bleibt.
- **Kein TAN-Verfahren, kein decoupled.** ING bietet über FinTS kein PSD2-konformes Zwei-Schritt-Verfahren an [4]. Die BPD aus einem Dump von 2024 zeigen nur HITANS Version 1 mit „iTAN“ (Code 900); HKKAZ und HKSAL brauchen keine TAN [5]. Die SCA erledigt man außerhalb von FinTS: ein FinTS-Log-in geht nur, wenn man sich in den letzten 90 Tagen im Online-Banking oder per App angemeldet hat [1]. Decoupled-Unterstützung der Bibliothek ist für die ING daher nicht nötig.
- **Nur MT940, kein CAMT.** Im BPD-Dump stehen HIKAZS, HISALS und HIWPDS, aber kein HICAZS [5]. Umsätze kommen also per HKKAZ als MT940. Für den Abruf nennt ING 90 Tage rückwirkend [1]. Vorgemerkte Umsätze liefert ING laut einem Forenbericht per FinTS nicht (Single Source) [8]. Eine ING-spezifische Aussage zur Stabilität der Referenz in `:61:` oder `:86:` habe ich nicht gefunden.
- **Bibliotheken.** python-fints ist gepflegt (5.0.1 vom 09.10.2026) und hat seit Januar 2025 eine ING-Sonderbehandlung (Einschritt-Verfahren fest eingestellt) [4], [6]. libfintx (.NET, LGPL-3.0) hat 1.4.0 mit .NET 10 und einem decoupled-Fix, aber keine dokumentierte ING-Erfahrung; ein offenes Issue meldet, dass das NuGet-Paket 1.4.0 noch ASCII statt Latin1 kodiert [9], [10], [11].
- **Produktregistrierung ist Pflicht und machbar.** Seit 2019 nur für registrierte Produkte; Formular im Original an die DK, Zuteilung in der Regel in 10 bis 15 Werktagen [12]. Kosten laut Sekundärquellen keine [13], [14]. Ein privates ING-Werkzeug (ing-ynab) verlangt von jedem Nutzer eine eigene Registrierung, was auf Registrierbarkeit für Einzelpersonen hindeutet (Schluss, keine DK-Aussage) [15].
- **Tagesgeld fehlt bei XS2A aus rechtlichem Grund.** Der EuGH hat in C-191/17 (ING-DiBa Austria, 04.10.2018) entschieden, dass ein Sparkonto, von dem nur über ein Referenzkonto ein- und ausgezahlt wird, kein Zahlungskonto ist [16], [17]. Für solche Konten gilt die XS2A-Zugangspflicht nicht [18]. Das ING Extra-Konto zahlt nur auf das Referenzkonto aus [19].

## Findings

### 1. Extra-Konto per FinTS, Server, Geschäftsvorfälle

**Quellenlage:** Die ING-Hilfeseite [1] lief bei jedem direkten Abruf in einen Timeout. Die Angaben stammen aus den Suchauszügen dieser Seite (Domainfilter ing.de). Datum des Seitenstands unbekannt.

Laut ING [1]:
- Adresse `https://fints.ing.de/fints/`, seit 13.07.2024 nur noch diese (vorher ing-diba.de) [1], [2].
- Benutzerkennung: 10-stellige Zugangsnummer (letzte 10 Stellen der IBAN) oder der selbstgewählte Benutzername; PIN oder Passwort. Kunden-ID bleibt leer.
- HBCI/FinTS in Version 2.2 und 3.0.
- FinTS-fähig: Girokonto, Extra-Konto, Direkt-Depot. Eine Banking4-Anleitung (Subsembly, Medium) sagt dasselbe („nur das Girokonto, das Extrakonto (Tagesgeld) sowie das Depot“) [3]; der Volltext war nicht abrufbar (HTTP 403), nur der Suchauszug.
- Zahlungen per FinTS nicht mehr möglich: Girokonto seit 14.09.2019, Extra-Konto seit 20.11.2021.
- Kontoinformationen werden „rückwirkend für 90 Tage bereit gestellt“. Ob das auch für Extra-Konto und Depot gilt, bleibt im Auszug offen. Lexware nennt für die Erstanbindung 390 Tage [2]; das widerspricht ING und bezieht sich womöglich auf einen anderen Zugangsweg.

BLZ 50010517 (BIC INGDDEFFXXX) laut Bankleitzahlen-Verzeichnis [2].

Geschäftsvorfälle: Ein BPD-Dump im python-fints-Issue #159 (13.03.2024) listet HIKAZS (Umsätze, MT940), HISALS (Saldo), HIWPDS (Depot) und Zahlungs-Segmente (HICCSS u. a.), aber **kein HICAZS** [5]. CAMT.052 per HKCAZ ist demnach nicht angeboten. Das ist ein einzelner Dump von 2024; den aktuellen Stand zeigen nur die BPD beim eigenen Dialog.

Erfahrungsberichte: Ein Forennutzer meldet, dass Banking4 ein neues Extra-Konto per FinTS erkannt hat; ein anderer, dass FinTS wenige Tage nach Kontoeröffnung noch nicht ging [8]. Forum, ungeprüft. ING-DiBa steht in der python-fints-Liste getesteter Banken nur unter der Legacy-Version 1.x [7]; das Projekt ing-ynab nutzte python-fints produktiv gegen die ING (archiviert 29.11.2025, umgezogen nach Codeberg) [15].

### 2. TAN-Verfahren und Bibliotheken

**ING-Seite:** ING kann die SCA nicht über FinTS abbilden. Schon 2019 hieß es, man werde HBCI „in absehbarer Zeit“ nicht vollständig PSD2-konform machen [20]. Der python-fints-PR #182 (gemergt 07.01.2025) begründet: „ING does not offer PSD2 conform two step authentication methods“ und stellt die ING fest auf Einschritt-Authentifizierung (Sicherheitsfunktion 999) [4]. Der BPD-Dump [5] bestätigt das Bild: HITANS nur in Version 1, einziges Zwei-Schritt-Verfahren 900 „iTAN“, `onestep_method_allowed = True`; HIPINS markiert HKKAZ, HKSAL, HKWPD und HKTAN mit „N“ (keine TAN), Zahlungen mit „J“. Es gibt keine HKTAN-Prozessvariante 2 mit HKTAN#7, kein decoupled, keine App-Freigabe per FinTS. Banking4 beschreibt dasselbe: App- und photoTAN nur auf den ING-Webseiten [3].

Zwei bekannte Stolperstellen in python-fints mit der ING: Issue #159 (März 2024, `KeyError: '999'`, löste sich nach ein bis zwei Tagen von selbst) [5] und Issue #197 (Oktober 2025, nach Umstellung auf Benutzername und Passwort derselbe Fehler, behoben in 4.2.4) [21].

**python-fints:** LGPL-3.0-or-later, 5.0.1 vom 09.10.2026, 5.0.0 vom 06.01.2026, regelmäßige Releases seit 2017 [6]. Decoupled wird unterstützt (`NeedTANResponse.decoupled`, Polling über `send_tan`) [22]; Issue #179 (decoupled-Flag fehlt bei mehreren Rückmeldungen) ist laut Suchauszug offen. Für die ING spielt decoupled keine Rolle.

**libfintx:** .NET-Bibliothek für HBCI 2.2, FinTS 3.0, EBICS; LGPL-3.0 [9]. Changelog: 1.4.0 vom 09.06.2026 mit .NET 10 und dem Fix „HKTAN#7 not completing when the bank sends both 0030 and 3955“ (also decoupled) [10]; 1.3.0 vom 03.01.2025. NuGet zeigt 1.4.0 mit Datum 31.07.2026, 35 200 Downloads gesamt [11] (Datumsdifferenz zum Changelog, vermutlich Veröffentlichung nach Changelog-Eintrag). MT940 und CAMT.052/053 werden geparst [23]. Elf offene Issues, darunter #85 (26.10.2026) „NuGet 1.4.0 still encodes FinTS messages as ASCII“ (Umlaute in Verwendungszwecken), #40 Fehler 9075 „Banking-Programm nicht PSD2-fähig“, #52 und #32 zu SCA [24]. Eine ING-Erfahrung mit libfintx habe ich nicht gefunden. Ob libfintx sauber mit Einschritt-Verfahren 999 bei angebotenem iTAN 900 umgeht (genau der Fall, den python-fints per Sonderregel löst), ist offen.

Andere .NET-Optionen: Subsembly FinTS API (kommerziell, Hersteller von Banking4) taucht in den Suchen nur indirekt auf; nicht vertieft. Als weitere gepflegte Bibliothek mit decoupled-Unterstützung gibt es lib-fints (TypeScript) [13], für einen .NET-Server nur als Sidecar sinnvoll.

### 3. Buchungsreferenz und vorgemerkte Umsätze

Da ING laut BPD kein HKCAZ anbietet [5], gibt es keine CAMT-Felder (AcctSvcrRef, EndToEndId) über FinTS. Es bleibt MT940.

Allgemein (nicht ING-spezifisch): In `:61:` folgt nach der Kundenreferenz (oft `NONREF`) optional nach `//` eine Bankreferenz [25]. In `:86:` stehen bei deutschen Banken Schlüsselwörter wie `EREF+` (End-to-End-Referenz), `KREF+`, `MREF+`, `SVWZ+` [26]. Das ist das BNP-Paribas-Dokument; die DK-Spezifikation selbst („Anlage 3 Datenformate“) habe ich nicht abrufen können. Die ING-Dokumente zu MT940/CAMT, die sich finden, gelten für ING Wholesale Banking und nicht für das deutsche Privatkundengeschäft.

Ob ING-Retail in `:61:` eine stabile Bankreferenz pro Buchung liefert, ist mit den gefundenen Quellen nicht zu klären. `EREF+` gibt es nur bei SEPA-Vorgängen mit Referenz; Zinsgutschriften auf dem Extra-Konto haben typischerweise keine. Allgemein gilt, dass zwei inhaltlich gleiche MT940-Zeilen beide echt sein können, ein Dedup über den Inhalt also echte Buchungen verschlucken kann [27].

Vorgemerkte Umsätze: Laut einem Forenbeitrag zeigt die ING per FinTS keine Vormerkungen, nur indirekt über den verfügbaren Saldo (Single Source, Suchauszug) [8]. Ein MT942-Anteil in der HKKAZ-Antwort ist damit nicht belegt.

### 4. SCA-Häufigkeit

Die 180-Tage-Regel steht in der **Delegierten Verordnung (EU) 2022/2360** (nicht 2022/2361), die die RTS 2018/389 ändert; anzuwenden ab 25.07.2023 [28], [29]. Für den Zugriff über einen Kontoinformationsdienst (neuer Art. 10a) ist die Ausnahme verpflichtend. Für den direkten Zugriff des Kunden (Art. 10) bleibt es laut Entwurfsbegründung der Bank überlassen, ob sie SCA verlangt [28] (Entwurfstext, Endfassung nicht geprüft; EUR-Lex lieferte keinen lesbaren Inhalt).

ING bei FinTS: Ein FinTS-Log-in geht nur, wenn innerhalb der letzten 90 Tage ein erfolgreicher Log-in im Online-Banking (Zugangsdaten oder QR-Log-in) bzw. per App stattfand [1]. Im FinTS-Dialog selbst gibt es dann keine TAN, auch nicht für ältere Umsätze, weil HKKAZ keine TAN verlangt [5] und ohnehin nur 90 Tage geliefert werden [1]. Ob ING diese Frist inzwischen auf 180 Tage verlängert hat, geht aus den Suchauszügen nicht hervor; sie nennen 90 Tage. Ein Forennutzer berichtet, Outbank und MoneyMoney hätten keinen 90-Tage-Log-in verlangt, ein anderer, Banking4 schon [8]. Das widerspricht sich und ist Forum. Folge für den Betrieb: Der FinTS-Abruf läuft ohne Zutun, solange Roman sich mindestens alle 90 Tage einmal bei der ING anmeldet; danach lehnt die ING den Log-in ab.

### 5. Produktregistrierung

- Pflicht seit 2019. fints.org nennt den 1. August 2019 für den Zugang und die Bankenliste [12], python-fints und eine Volksbank den 14.09.2019 [22], [14].
- Ablauf: Formular vollständig ausgefüllt „im Original“ an die Registrierungsadresse der DK (registrierung@hbci-zka.de) [12]. Angaben laut einer Projektdoku: Produktname, Version, Hersteller (Person oder Organisation) [30]. Die Registrierungsnummer gehört bei jeder Dialoginitialisierung in das Element „Produktbezeichnung“, die Produktversion bleibt Pflicht [12].
- Dauer: „in der Regel innerhalb von 10–15 Werktagen“ [12]; python-fints „up to two weeks“ [22], ing-ynab „a week or two“ [15], ein anderes Projekt „mehrere Wochen“ [13].
- Kosten: fints.org nennt keine. Volksbank-Kundeninfo und lib-fints sagen kostenlos [14], [13] (Sekundärquellen).
- Einzelentwickler: Weder fints.org noch das Registrierungs-PDF (nicht gelesen) wurden dazu ausgewertet. Indiz: ing-ynab, ein privates Werkzeug, verlangt von jedem Nutzer eine eigene Product-ID per DK-Formular [15].
- Ablehnung unregistrierter IDs: Allgemein melden Banken „Software nicht als FinTS-Produkt registriert“ (KMyMoney-Liste, 2020) bzw. 9075 „Banking-Programm nicht PSD2-fähig“ (libfintx #40) [31], [24]. Eine ING-spezifische Aussage habe ich nicht gefunden. python-fints bringt eine eigene Standard-ID mit, die ing-ynab als Default nutzt [15]; ob die Nutzung fremder IDs zulässig ist, regeln die DK-Bedingungen (nicht gelesen).

### 6. Warum Tagesgeld bei XS2A fehlt

PSD2 verpflichtet Banken nur für Zahlungskonten zum Zugang durch Kontoinformationsdienste (Art. 66/67). Der EuGH hat am 04.10.2018 in C-191/17 (Bundesarbeiterkammer gegen ING-DiBa Direktbank Austria) entschieden: Ein Sparkonto mit täglicher Fälligkeit, auf das bzw. von dem Ein- und Auszahlungen nur über ein Girokonto laufen, ist kein Zahlungskonto [16], [17]. Konstitutiv sei die Möglichkeit, Zahlungen an Dritte zu leisten und von Dritten zu empfangen [17]. Die niederländische Aufsicht DNB folgert daraus ausdrücklich, dass für solche Sparkonten keine Zugangspflicht nach Art. 66/67 besteht, Banken den Zugang aber freiwillig gewähren dürfen [18]. Die EU-Kommission sieht das Urteil auch unter PSD2 als maßgeblich (laut DNB) [18].

Das ING Extra-Konto passt genau in dieses Muster: Auszahlungen nur auf das Referenzkonto, keine Überweisungen an Dritte, Lastschriften oder Daueraufträge [19]. Dass die ING es über XS2A nicht ausliefert, ist damit erklärbar. Eine ausdrückliche ING-Aussage zum Umfang ihrer XS2A-Schnittstelle habe ich nicht gefunden; ein Suchauszug von ing.de nennt Girokonto, Extra-Konto und Depot im PSD2-Zusammenhang, der Kontext ist aber unklar (eher FinTS-Seite) und widerspricht dem Befund bei Enable Banking. finAPI schreibt allgemein, XS2A liefere überwiegend Girokonten, weitere Kontoarten hole man in Deutschland je nach Bank per FinTS [32]. Ein Anbieter (re-cap) schreibt pauschal, die meisten EU-Banken gäben Sparkonten nicht per Open Banking frei [33].

## Sources

1. [ING: Hilfe zu FinTS / HBCI](https://www.ing.de/hilfe/log-in/fints/): Primärquelle für Adresse, Kontoarten, 90-Tage-Log-in, 90 Tage Rückschau, Ende der Aufträge. Nur über Suchauszüge gelesen (Abruf mit Timeout). Datum unbekannt.
2. [bank.codes: BLZ 50010517](https://bank.codes/bankleitzahl/50010517/) und [Lexware: ING-DiBa anbinden](https://help.lexware.de/de-form/articles/547852-lexware-office-ing-diba-anbinden): BLZ, 390-Tage-Angabe; Wechsel auf fints.ing.de seit 13.07.2024 aus Suchauszug.
3. [Subsembly/Banking4 auf Medium](https://medium.com/@subsembly/9488fdbba8a7) und [zweiter Beitrag](https://medium.com/@subsembly/b1150f31e494): Kontoarten, nur Abruf, kein TAN-Verfahren per FinTS, 90 Tage. HTTP 403, nur Suchauszug. Undatiert.
4. [python-fints PR #182 „Pin ING to one step authentication“](https://github.com/raphaelm/python-fints/pull/182): gemergt 07.01.2025.
5. [python-fints Issue #159](https://github.com/raphaelm/python-fints/issues/159): BPD-Dump der ING mit HIPINS, HITANS, Segmentliste. 13.03.2024.
6. [PyPI: fints](https://pypi.org/project/fints/): Versionen und Daten, Lizenz.
7. [python-fints: Tested banks](https://python-fints.readthedocs.io/en/latest/tested.html): ING-DiBa nur unter 1.x.
8. [Vielfliegertreff: Wechsel zur Ing Diba](https://www.vielfliegertreff.de/forum/threads/wechsel-zur-ing-diba.121395/post-3827389): Forum, Erfahrungsberichte zu Extra-Konto, 90 Tagen, Vormerkungen. Etwa 2023, über Suchauszüge.
9. [GitHub: libfintx/libfintx](https://github.com/libfintx/libfintx): Funktionsumfang, LGPL-3.0, .NET 6+.
10. [libfintx CHANGELOG.md](https://raw.githubusercontent.com/libfintx/libfintx/master/CHANGELOG.md): 1.4.0 (09.06.2026) mit HKTAN#7-Fix.
11. [NuGet: libfintx.FinTS](https://www.nuget.org/packages/libfintx.FinTS): 1.4.0, Downloads, Frameworks.
12. [fints.org: Produktregistrierung](https://www.fints.org/de/hersteller/produktregistrierung): Primärquelle DK. Ablauf, 10 bis 15 Werktage, Pflichten.
13. [lib-fints (robocode13)](https://awesome.ecosyste.ms/projects/github.com%2Frobocode13%2Flib-fints) und [mathiasarens/fints](https://github.com/mathiasarens/fints): kostenlos, mehrere Wochen; decoupled in TypeScript.
14. [Volksbank Allgäu-Oberschwaben: Hinweise für Nutzer von FinTS](https://www.volksbank-allgaeu-oberschwaben.de/content/dam/f0100-0/dateien_vbao/zahlungsverkehr/Download/Hinweise%20f%C3%BCr%20Nutzer%20von%20FINTS.pdf): kostenlos, Pflicht seit 14.09.2019.
15. [GitHub: bahlo/ing-ynab](https://github.com/bahlo/ing-ynab) bzw. [Codeberg](https://codeberg.org/enra/ing-ynab): privates ING-zu-YNAB-Werkzeug auf python-fints, eigene Product-ID nötig. Archiviert 29.11.2025.
16. [OGH: Vorlagefrage zu Online-Sparkonten](https://www.ogh.gv.at/entscheidungen/vorabentscheidungsersuchen-eugh/vorlagefrage-an-den-europaeischen-gerichtshof-eugh-ob-das-oesterreichische-zahlungsdienstegesetz-zadig-auf-online-sparkonten-anwendbar-ist/) und [Folgeentscheidung](https://gesetzefinden.at/judikatur/ogh/jjt_20191118_ogh0002_0080ob00144_18m0000_000): Kernaussage C-191/17.
17. [IT Finanzmagazin: EuGH zu Sparkonten und XS2A](https://www.it-finanzmagazin.de/psd2-xs2a-sparkonten-78754/): 08.10.2018, Einordnung durch Görg Rechtsanwälte.
18. [DNB: Savings accounts under PSD2](https://www.dnb.nl/en/sector-information/supervision-laws-and-regulations/laws-and-eu-regulations/psd2/savings-accounts/): Aufsicht NL, keine Zugangspflicht für solche Sparkonten.
19. [ING: Hilfe zum Extra-Konto](https://www.ing.de/hilfe/produkte/extrakonto/) und [bankdaten.de: ING Extra-Konto](https://www.bankdaten.de/ing/extra-konto.html): Auszahlung nur aufs Referenzkonto. Suchauszüge.
20. [ifun.de: PSD2-Einführung, ING schließt Banking-Apps aus](https://www.ifun.de/psd2-einfuehrung-ing-schliesst-populaere-banking-apps-aus-141868/): 20.08.2019, historische ING-Aussage.
21. [python-fints Issue #197](https://github.com/raphaelm/python-fints/issues/197): Oktober 2025, Benutzername/Passwort, behoben in 4.2.4.
22. [python-fints: Working with TANs](https://python-fints.readthedocs.io/en/latest/tans.html) und [Getting started](https://python-fints.readthedocs.io/en/latest/quickstart.html): decoupled, Registrierungspflicht.
23. [libfintx.github.io](https://libfintx.github.io): MT940 und CAMT.052/053.
24. [libfintx Issues](https://github.com/libfintx/libfintx/issues): #85, #40, #52, #32. Stand 06.11.2026.
25. [ING Sample Files MT940](https://assets.ing.com/m/33b5fb41d9d72026/original/Sample-Files-MT940.pdf): Beispiel `:61:` mit Bankreferenz nach `//`. ING Wholesale, nicht Retail DE.
26. [BNP Paribas: MT940 Details](https://www.bnpparibas.de/app/uploads/sites/21/2022/04/mt940-details.pdf): `:86:`-Schlüsselwörter EREF+, KREF+, SVWZ+ (deutsche Belegung). 2022.
27. [Stack Overflow (Archiv): Doppelte Buchungen in MT940](https://kiwix.lema.org/stackoverflow.com_en_all_2023-11/questions/17783228/how-to-find-double-transactions-in-mt940): identische Zeilen können echt sein.
28. [VKB-Bank: 90 auf 180 Tage](https://developer.vkb-bank.at/announcement-increase-account-access-account-information-without-sca-90-180-days) und [EDPS-Stellungnahme zum Entwurf](https://edps.europa.eu/system/files/2022-07/2022-06-07_formal_comments_costumer_autentification_de.pdf): 25.07.2023, Wahlfreiheit bei direktem Zugriff (Entwurf).
29. [EUR-Lex: Delegierte Verordnung (EU) 2022/2360](https://eur-lex.europa.eu/eli/reg_del/2022/2360/oj/eng): Primärquelle, Abruf ohne lesbaren Inhalt; Nummer über Suchtreffer bestätigt.
30. [SWEN: FinTS-Konzepte](https://maltewinckler.github.io/swen/concepts/fints/): Formularfelder der Registrierung.
31. [KMyMoney-Mailingliste: Software nicht als FinTS-Produkt registriert](https://mail.kde.org/pipermail/kmymoney/2020-December/003575.html): Dezember 2020.
32. [finAPI Access Dokumentation](https://documentation.finapi.io/access/): XS2A überwiegend Girokonten, FinTS für weitere Kontoarten. Anbieterdoku.
33. [re-cap: Connecting savings account](https://re-cap.com/help-center/connecting-savings-account): Anbieteraussage, undatiert.

## Open questions

- **Aktuelle BPD der ING:** Der einzige BPD-Dump ist von März 2024 [5]. Ob HKCAZ inzwischen angeboten wird und welche HKKAZ-Version gilt, zeigt erst ein eigener Dialog-Init.
- **Referenzen im ING-MT940:** Ob `:61:` eine stabile Bankreferenz trägt und ob sie zwischen zwei Abrufen gleich bleibt, ließ sich nicht klären. Braucht zwei echte Abrufe mit Vergleich (ohne echte Daten ins Repo).
- **Extra-Konto im Detail:** Ob die 90 Tage Rückschau und HKKAZ auch für das Extra-Konto gelten, ist durch keine Primärquelle belegt; nur die Kontoart ist als FinTS-fähig genannt.
- **90 oder 180 Tage bei der ING:** Die gefundenen ING-Auszüge nennen 90 Tage. Ob ING nach 2022/2360 verlängert hat, ist offen; für direkten Zugriff ist die Bank nicht verpflichtet.
- **libfintx mit der ING:** Kein Erfahrungsbericht. Offen, ob libfintx das Einschritt-Verfahren bei gleichzeitig angebotenem iTAN 900 korrekt wählt und ob das ASCII-Problem (#85) im NuGet-Paket inzwischen behoben ist.
- **DK-Bedingungen für Einzelpersonen:** Das PDF „FinTS-Produktregistrierung_V1.0.4“ und die FAQ auf fints.org habe ich nicht gelesen; dort stehen Zulassung, Kosten und Regeln zur Weitergabe von IDs.
- **ING-Primärseiten:** ing.de war per WebFetch nicht erreichbar (Timeouts). Alle ING-Angaben stammen aus Suchauszügen und sollten einmal im Browser gegengelesen werden.
