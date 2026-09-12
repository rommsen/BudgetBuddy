# Kickoff-Briefing für das Mockup-Projekt in Claude Design

Erste Nachricht im neuen Projekt (Design-System "BudgetBuddy Design System" ausgewählt,
Vorlage "UI mockups"). Danach folgen die Briefs je Ablauf aus `briefs.md`, einer pro
Nachricht. Der Text unter der Linie ist zum Einfügen gedacht.

---

## Worum es geht

**BudgetBuddy (BB)** ist die selbstgebaute Finanz-App einer Familie (Roman und Lena),
läuft privat hinter Tailscale, kein Produkt für Dritte. Bisher ist BB ein Companion zu
YNAB: Import von Comdirect-Buchungen, regelbasierte Kategorisierung, Push nach YNAB,
Quick Add für Bar-Ausgaben. Jetzt bekommt BB ein eigenes, dauerhaftes **Kassenbuch** über
alle Konten. Jede Buchung trägt ihre **Herkunft** (Comdirect-Import, Regel, Quick Add,
Roman in BB, YNAB-Spiegel) und eine vollständige **Historie** (was sich wann und woher
geändert hat). YNAB bleibt vorerst Projektion und Fallback; das Kassenbuch spiegelt YNABs
Zustand laufend und merkt sich zusätzlich, was BB selbst entschieden hat.

**Warum diese Screens:** Ein Kassenbuch, in das man nur hineinschaut, öffnet niemand.
Roman sagt: "Nur reingucken, dafür würde ich es wahrscheinlich gar nicht öffnen." Die
Arbeit, für die Roman heute YNAB öffnet, soll im Kassenbuch mindestens so schnell gehen,
mit besserer Bedienung. Scheiterkriterium: BB wird langsamer oder umständlicher als
YNAB-Web.

**Nutzer:** Roman (Hauptnutzer, etwa 70 % Handy, 30 % PC; Nachbearbeiten fast nur am PC,
am Handy vor allem Durchsehen und Prüfen). Lena liest mit und bekommt Rückfragen zu
Buchungen, zu denen Roman nichts findet. Beide Geräte müssen sehr gut bedienbar sein.

**Was das Kassenbuch zeigt:** Konten (Bankkonten Comdirect und ING, Bar, ein
Geldmarkt-ETF als Budget-Konto) mit Kontoständen, die Buchungen je Konto und über alle
Konten ("Gesamtsicht"), je Buchung Datum, Betrag, Payee, Memo, Kategorie oder Splits,
Herkunft, Historie, Kennzeichen (geprüft, Rückfrage Lena, in YNAB gelöscht, noch nicht in
YNAB). Rund 14.000 Buchungen im echten Bestand.

**Die sieben Abläufe, die der Prototyp abdeckt** (je ein Brief folgt):
1. Durchsehen und als geprüft markieren (Handy zuerst)
2. Buchungen finden per Filter, vor allem nach Kategorie (PC und Handy)
3. Mehrfachauswahl mit Summe (PC und Handy)
4. Kategorie ändern, Split festlegen oder ändern (PC)
5. Rückfrage an Lena ("Lena fragen") und temporäre Markierung (PC)
6. Gesamtsicht und Konto-Sicht mit Kontoständen (beide)
7. Was nur BB kann: Herkunft, Historie, gelöschte Buchungen, "noch nicht in YNAB"

## Regeln für die Screens

- **Nur Komponenten und Tokens aus dem Design-System** "BudgetBuddy Design System"
  (neon-on-dark, Glow sparsam, Zahlen in JetBrains Mono mit tabular-nums, Fließtext Inter,
  Überschriften Space Grotesk). Keine neuen Farben, keine neuen Schriften. Wo eine
  Komponente fehlt, aus vorhandenen zusammensetzen und das im Screen benennen.
- **Handy 390 × 844, PC 1440 × 900.** Handy mit Bottom-Navigation (Karte
  `components-navigation`, mobile Variante), PC mit Top-Navigation.
- **Sprache:** Deutsch, Du-Form, knapp. Beträge im Format "-48,12 €". Datum "11.09.2026",
  in Listen "Heute", "Gestern", sonst "08.09.".
- **Nur erfundene Daten.** Konten: Girokonto Comdirect 2.418,37 €, ING Extra 6.902,10 €,
  Bar 63,50 €, Geldmarkt-ETF 12.004,88 €. Payees: REWE Mitte, Bäckerei Krause, Stadtwerke
  Musterstadt, Deutsche Bahn, Amazon Marketplace, Apotheke am Markt, Kita-Beitrag Stadt,
  Netflix, Bauhaus, Café Lotte. Kategorien in Gruppen: Alltag (Lebensmittel, Haushalt,
  Restaurants), Wohnen (Nebenkosten, Möbel), Mobilität (Bahn, Auto), Familie (Kinder,
  Geschenke), Abos (Streaming), Gesundheit (Apotheke); versteckt: "Umzug 2023".
- **Klickbarer Prototyp:** Screens so verbinden, dass sich jeder Ablauf durchspielen
  lässt (Tipp oder Klick auf Zeile, Chip, Button, Wisch-Geste als Zustand vorher und
  nachher).
- **Ein Screen je Schritt**, benannt nach Ablauf und Schritt (z. B. "1.2 Handy: nach dem
  Wisch"). Wo ein Zustand nur eine Variante ist (Sheet offen und zu), beide zeigen.

## Gemeinsame Grundlage der Oberfläche (abgestimmt)

- Einstieg im zuletzt gewählten Konto. Oben Kontostände (Handy: waagerecht scrollbare
  Leiste; PC: linke Spalte mit Konten und Ständen, "Alle Konten" zuoberst), darunter eine
  sticky Filterleiste aus Chips, darunter die Liste. Erster Chip ist immer "Zu prüfen (n)".
- Konto ist ein Filter. Konto-Wechsel lässt andere Filter und eine laufende Auswahl stehen.
  In der Gesamtsicht zeigt jede Zeile ein Konto-Kürzel (Handy) oder eine Konto-Spalte (PC).
- Liste Handy: Datum als Gruppenkopf; Zeile mit Payee, Kategorie, Betrag; darunter klein
  Memo und nur abweichende Kennzeichen. Wisch nach rechts = geprüft, ein einziger Wisch.
  Tipp auf die Zeile öffnet das Detail als BottomSheet.
- Liste PC: Tabelle mit Sticky-Kopf (Auswahl-Kästchen, Datum, Konto, Payee, Kategorie,
  Memo, Betrag, Status). Klick auf die Zeile öffnet ein Seitenpanel rechts, die Liste
  bleibt sichtbar. Klick auf die Kategorie-Zelle öffnet den Kategorie-Picker direkt.
- Filter-Chips: Zu prüfen, Kategorie (Mehrfachauswahl, Available-Betrag je Kategorie),
  Zeitraum, Payee, Betrag, Memo, Flag, Gelöschte zeigen. Sobald ein Filter aktiv ist,
  steht über der Liste eine Summenzeile: Trefferzahl und Gesamtbetrag.
- Auswahl-Modus: langes Drücken (Handy), Kästchen oder Shift-Klick (PC). Unten eine
  Auswahl-Leiste mit Anzahl, Summe, Aktionen "Geprüft", "Kategorie ändern", "Lena fragen",
  "Auswahl aufheben".
- Detail: Kopf mit Betrag, Payee, Konto, Datum. Felder Kategorie, Split (Zeilen mit
  "Rest"-Button), Memo, "Lena fragen", "Markieren", Geprüft-Schalter. Darunter Herkunft
  und Historie als Zeitleiste. Hinweis "Noch nicht in YNAB" mit Grund.
- Gelöschte Buchungen sind standardmäßig ausgeblendet und kommen über den Chip zurück.
- Kategoriewechsel sortiert nie um und prüft nie mit.

Bestätige kurz, dass du das Design-System und diese Grundlage gelesen hast. Danach
schicke ich dir Brief 1.
