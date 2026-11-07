---
topic: Farbschema für BudgetBuddys Neon-on-Dark-Theme mit verlässlichen Kontrasten und eindeutigen Bedeutungen
date: 2026-11-07
requested_by: user
related_tasks: []
---

# Research: Farbschema Neon-on-Dark

## Question

Wie baut man für ein dunkles Neon-Theme einer Finanz-App eine Palette, in der jede Farbe eine Bedeutung hat und
die Kontraste halten? Teilfragen: Aufbau (Flächen, Text, Hauptaktion, Auswahl, Semantik, Personen- oder
Markierungsfarbe), Harmonie auf dunklem Grund (OKLCH), Kontrast (WCAG 2.2 AA, APCA), Verwechslung (Orange und
Gelb, Farbsehschwäche), Violett als Lena-Farbe oder konfigurierbare Flags.

Gesetzt ist schon: Pink fällt weg, "zu prüfen" und "fehlt" bekommen eine gemeinsame Farbe für "braucht dich,
kein Fehler". Ist-Stand aus `standards/frontend/styleguide.md` §1 und §2 und `src/Client/styles.css`.

## Summary

- **Zwei Knöpfe fallen heute durch WCAG AA:** weißer Text auf `neon-orange` hat 2,84:1 (`PickerParts` "Übernehmen",
  helles Ende von `Button.primary`), am dunklen Ende des Verlaufs (#e55a1f) 3,61:1. Weiß auf `neon-red`
  (`Badge` Error) hat 3,48:1. Dunkler Text (#08081a) auf Orange hätte 6,97:1. Das ist der größte Kontrastfund.
- **Als Text fallen `neon-purple` (4,26:1 auf bg-elevated, 4,06:1 auf bg-hover) und `neon-pink` (4,47:1 auf bg-hover)
  durch.** Alle anderen Text- und Akzent-Tokens bestehen 4,5:1 auf card, elevated und hover. Der Rand `border`
  hat 1,35:1 gegen bg-card; reicht für Deko, nicht für ein Eingabefeld, das nur am Rand erkennbar ist (3:1).
- **Die eigentliche Verwechslungsgefahr liegt bei Rot und Orange, nicht bei Orange und Gelb allein.** Rot (h 17,5)
  und Orange (h 41) liegen in OKLCH 23° auseinander bei fast gleicher Helligkeit; unter Deuteranopie (eigene
  Simulation) werden beide zu Ocker mit Helligkeitsverhältnis 1,18. Rot und Pink sind das ähnlichste Paar
  überhaupt (ΔE_OK 0,084). Teal steht heute für Auswahl und für positive Beträge in Listen, das ist eine
  Doppelbelegung.
- **Etablierte Systeme kommen mit einer neutralen Skala, einer Akzentfarbe und vier bis acht Bedeutungsfarben
  aus** (Carbon: Fehler, Erfolg, Warnung, Info; Primer: accent, success, attention, danger, open, closed, done,
  sponsors). Für BB ergeben sich sieben Farbtöne plus Neutral, je einer pro Rolle.
- **Vorschlags-Palette (alle Werte gerechnet und gegen WebAIM geprüft):** Hauptaktion #fa7c20, Auswahl #28d6df,
  Fehler/Minus #fd4f75, Braucht-dich #eed059 (oder `calm-yellow` behalten), Erfolg/Plus #5be479, Info #7ab8ff,
  Markierung #c9a7ff, dazu `border-strong` #6a6c86. Alle Akzente zwischen 5,26:1 und 12,1:1 auf card und elevated.
  Die Varianten hängen voneinander ab (siehe Optionen), Roman entscheidet.
- **Lena-Farbe:** YNAB hat sechs umbenennbare Flag-Farben, Monarch löst "Person" über Namen und Zuweisung, nicht über
  eine reservierte UI-Farbe. Optionen: feste Markierungsfarbe Violett, Flag-Farbe aus den Settings oder neutraler
  Namens-Chip.

## Findings

### 1. Aufbau: welche Rollen, wie viele Farben

**Neutrale Stufen tragen das System, Farbe ist die Ausnahme.** Radix teilt jede Skala in 12 Stufen mit fester
Rolle: 1 und 2 App-Hintergrund, 3 bis 5 Komponentenflächen (normal, hover, gedrückt/gewählt), 6 bis 8 Ränder
(dezent, interaktiv, stark und Fokus), 9 und 10 volle Flächen (normal, hover), 11 und 12 Text (niedriger und hoher
Kontrast) [2]. Vercel Geist hat dieselbe Logik in 10 Stufen: 100 bis 300 Flächen, 400 bis 600 Ränder, 700 und 800
kräftige Flächen, 900 und 1000 Text, und empfiehlt Grau als Standard für Text, Icons, Flächen und Ränder [6]. BBs
heutige Flächen (app, card, elevated, input, hover) und Ränder (border, border-subtle) entsprechen den Radix-Stufen
1 bis 6; es fehlt ein starker Rand (Radix Stufe 8) für Eingabefelder und Fokus.

**Anzahl der Bedeutungsfarben.** IBM Carbon (Theme g100) hat vier Support-Farben: Fehler #fa4d56, Erfolg #42be65,
Warnung #f1c21b, Info #4589ff, dazu Link #78a9ff und Fokus Weiß auf Hintergrund #161616 [5]. GitHub Primer (dark)
hat acht Rollen: accent #4493f8 (Links, Gewähltes, Fokus, neutrale Info), success #3fb950, attention #d29922
(Warnung, laufende Vorgänge), severe #db6d28, danger #f85149, done #ab7df8, sponsors #db61a2, dazu open/closed
[3][4]. Geist führt acht Farbskalen (gray, blue, red, amber, green, teal, purple, pink) [6], ohne jeder eine feste
Bedeutung zu geben. Linear reduziert bewusst: Themes entstehen aus drei Eingaben (Grundfarbe, Akzentfarbe,
Kontrast), und das Team hat den Anteil der Akzentfarbe zurückgenommen, um neutraler zu wirken [7].

Ableitung für BB (eigene Synthese, keine Quelle schreibt eine Zahl vor): eine Neutral-Skala, eine Hauptaktion,
eine Auswahlfarbe, vier Semantikfarben (Fehler/Minus, Braucht-dich, Erfolg/Plus, Info), eine Markierungsfarbe.
Das sind sieben Farbtöne. Primer zeigt, dass mehr geht, wenn jede Farbe genau eine Bedeutung hat; dort sind Orange
(severe) und Gelb (attention) beide Warnstufen und keine Aktion.

**Hauptaktion und Erfolg dürfen zusammenfallen oder getrennt sein.** Primer färbt den Hauptknopf grün (#238636,
Rolle success) und nutzt Blau für Auswahl [3][4]. Carbon trennt: Blau für Interaktion, Grün nur für Erfolg [5].
Linear hat eine einzige Akzentfarbe für Aktion und Auswahl [7]. Alle drei Modelle sind in Gebrauch.

### 2. Harmonie auf dunklem Grund

**Gesättigte Farben flimmern auf Dunkel.** Material Design (Version 2, Dark Theme) schreibt, dass gesättigte
Farben auf dunklem Grund optisch vibrieren und die Augen anstrengen können, und empfiehlt entsättigte Töne; die
Grundlinie nimmt den 200er-Ton der Primärfarbe, der 4,5:1 auf allen Höhenstufen schafft, und 15,8:1 für weißen
Text auf #121212 [1]. (Die Seite war nicht direkt abrufbar, die Aussagen stammen aus dem zitierten Auszug im
Suchergebnis; Material 3 habe ich auf diese Werte nicht geprüft.) Google beschreibt für Google News eine
dunkelgraue Fläche, damit hellgrauer Text mit minimaler "Vibration" lesbar bleibt [8]. Eine wissenschaftliche
Studie zum Flimmern gesättigter Farben habe ich nicht gefunden; Halation bei Astigmatismus wird in mehreren
Fach-Blogs genannt, mit widersprüchlichen Prävalenzzahlen [9]. Diese Aussage ist also Praxiswissen, nicht
belegt.

**OKLCH für gleichmäßige Helligkeit.** In HSL bedeutet derselbe Helligkeitswert je nach Farbton etwas anderes; ein
Farbtonwechsel von Blau nach Rot kann Text unlesbar machen. In OKLCH ist L die wahrgenommene Helligkeit, gleiche
Änderung von L wirkt über alle Töne gleich [10]. Einschränkungen: nicht jede Kombination aus L, C und h liegt im
sRGB-Raum, Browser schneiden zu und verschieben dabei den Farbton; OKLCH ersetzt keine Kontrastprüfung [10].
Linear hat seinen Theme-Generator aus demselben Grund von HSL auf LCH umgestellt, auch für die Höhenstufen der
Flächen [7].

**Was das für BB heißt (eigene Messung).** Die heutigen Akzente liegen in OKLCH so:

| Token | Hex | OKLCH (L C h) |
|---|---|---|
| bg-card | #111128 | 19,2 % 0,046 281 |
| neon-red | #ff3b5c | 66,0 % 0,229 17,5 |
| neon-pink | #ff1f8f | 65,7 % 0,255 359 |
| neon-orange | #ff6b2c | 70,4 % 0,195 41 |
| calm-yellow | #cdb27a | 77,4 % 0,080 85 |
| neon-green | #00ff88 | 87,6 % 0,228 152,5 |
| neon-teal | #00d4aa | 77,5 % 0,151 172 |
| calm-blue | #7ab8ff | 76,9 % 0,121 253 |
| neon-purple | #a855f7 | 62,7 % 0,233 304 |
| calm-violet | #c9a7ff | 79,1 % 0,127 301 |

Auffällig: Die Helligkeit springt zwischen 62,7 % (purple) und 87,6 % (green), die Chroma zwischen 0,08 und 0,255.
Grün leuchtet deshalb deutlich stärker als alles andere (13,8:1 auf bg-card); Purple und Pink sind die dunkelsten
und fallen als Erste durch. Die drei Neon-Warmtöne Rot, Pink, Orange liegen in 42° Farbton (359 bis 41) bei fast
gleicher Helligkeit. Eine gleichmäßige Palette hält L für alle Akzente in einem engen Band (etwa 68 % bis 86 %)
und verteilt die Töne mit mindestens rund 40° Abstand zwischen Rollen, die zusammen auftreten.

### 3. Kontrast

**WCAG 2.2 AA.** Text braucht 4,5:1 (groß 3:1). Nach 1.4.11 brauchen Bestandteile von Bedienelementen, die man zum
Erkennen oder für den Zustand braucht (Haken, Fokus, gewählter Zustand), und Grafikteile, die man zum Verstehen
braucht (Balken, Linien), 3:1 gegen die angrenzende Farbe; die Schwelle ist hart, 2,999:1 fällt durch [11]. Ein
Knopf braucht keinen kontrastierenden Rand, wenn Text oder Position ihn als Knopf erkennbar machen; der
Fokusindikator braucht aber 3:1. Inaktive Elemente sind ausgenommen [11]. Diagrammbalken mit Werten als Text
müssen nicht 3:1 haben [11].

**APCA als Ergänzung.** APCA ist Kandidat für WCAG 3 und nicht Teil von WCAG 2.2. Stufen: Lc 90 bevorzugt für
Fließtext, Lc 75 Minimum für Textspalten, Lc 60 für sonstigen Inhaltstext, Lc 45 für große, fette Überschriften,
Lc 30 für Platzhalter und deaktivierten Text, Lc 15 für Trenner [12]. Die APCA-Doku sagt, WCAG 2 überschätze den
Kontrast dunkler Farben, ein 4,5:1 nahe Schwarz könne schlecht lesbar sein [12] (Aussage des APCA-Autors, nicht
unabhängig geprüft). Radix garantiert für seine Textstufen Lc 60 (Stufe 11) und Lc 90 (Stufe 12) auf Stufe 2 [2].
APCA-Werte habe ich nicht gerechnet (siehe Open questions).

**Text auf farbigem Knopf.** Radix gibt an, dass helle Skalen (Sky, Mint, Lime, Yellow, Amber) auf ihren
Vollflächen dunklen Text brauchen, die übrigen weißen [2]. Primer nutzt Weiß auf dunklem Grün (#238636) [4].
Neon-Orange ist hell (relative Luminanz 0,32); weißer Text erreicht darauf nur 2,84:1. Dunkler Text bleibt die
einzige Variante, die das Orange unverändert lässt und 4,5:1 schafft.

**Glow sparsam.** Der Styleguide sagt schon "alles glüht = nichts glüht" (§1). Zu Glow selbst gibt es in WCAG
keine Regel; ein Glow ändert die angrenzende Farbe eines Elements, also auch das Kontrastverhältnis nach 1.4.11.
Gemessen wird gegen die Fläche ohne Glow, sonst rechnet man sich den Rand schön (eigene Ableitung aus [11]).

#### Prüfung der heutigen Tokens (WCAG-Verhältnis, eigene Rechnung nach WCAG-2-Formel)

Die Rechnung habe ich gegen die drei im Styleguide genannten Werte (calm-yellow 9,03, calm-blue 8,93, calm-violet
9,21 auf bg-card) und gegen den WebAIM-Kontrastprüfer [13] (Stichproben, markiert mit W) gegengeprüft; alle
stimmen auf ±0,02.

| Token | auf bg-card #111128 | auf bg-elevated #1a1a36 | auf bg-hover #1e1e3c | Urteil |
|---|---|---|---|---|
| text-primary #e8e8f0 | 15,19 | 13,84 | 13,19 | besteht |
| text-secondary #a3a3bd | 7,52 | 6,85 | 6,53 | besteht |
| text-muted #8787a5 | 5,32 | 4,85 | 4,62 (W 4,62) | besteht knapp auf hover |
| neon-orange #ff6b2c | 6,51 | 5,94 | 5,66 | besteht |
| neon-teal #00d4aa | 9,69 | 8,83 | 8,42 | besteht |
| neon-green #00ff88 | 13,80 | 12,58 | 11,99 | besteht, sehr hell |
| neon-red #ff3b5c | 5,32 | 4,85 | 4,62 | besteht |
| neon-purple #a855f7 | 4,68 | **4,26** (W 4,26) | **4,06** | fällt auf elevated und hover durch |
| neon-pink #ff1f8f | 5,15 | 4,69 | **4,47** | fällt auf hover durch |
| calm-yellow #cdb27a | 9,03 | 8,23 | 7,84 | besteht |
| calm-blue #7ab8ff | 8,93 | 8,14 | 7,75 | besteht |
| calm-violet #c9a7ff | 9,21 | 8,40 | 8,00 | besteht |
| border #2a2a4a (als Rand) | **1,35** | | | unter 3:1, nur als Deko zulässig |

Knöpfe und Badges (Text auf Fläche):

| Paar | Stelle im Code | Verhältnis | Urteil |
|---|---|---|---|
| Weiß auf #ff6b2c | `PickerParts.fs` Zeile 38 ("Übernehmen", 14px), helles Ende von `Button.primary` (`Button.fs` Zeile 85) | **2,84** (W) | fällt durch, auch als großer Text |
| Weiß auf #e55a1f | dunkles Ende des Verlaufs in `Button.primary` | **3,61** (W) | nur großer Text |
| Weiß auf #ff3b5c | `Badge.fs` Zeile 73 (Error) | **3,48** (W) | nur großer Text |
| #08081a auf #ff6b2c | Variante mit dunklem Text | 6,97 | besteht |
| neon-red auf neon-red/10 über bg-card | `Button.Danger` | 4,87 | besteht |

Die Hero-Knöpfe (`Button.fs` ab Zeile 506) nutzen schon dunklen Text (`text-surface-app`), dort besteht der
Kontrast.

### 4. Verwechslung vermeiden

**Farbe nie allein.** WCAG 1.4.1 (Stufe A): Farbe darf nicht das einzige Mittel sein, um eine Information, eine
Aktion oder einen Zustand zu zeigen. Ausreichend sind Text, Icon, Form, Muster oder ein Helligkeitsunterschied von
mindestens 3:1; wenn die Bedeutung an einer bestimmten Farbe hängt (Grün gültig, Rot ungültig), braucht es
zusätzlich ein sichtbares Zeichen [14]. Für BB heißt das: Minus-Vorzeichen bei negativen Beträgen, das Wort
"fehlt" und "zu prüfen" neben der Farbe, Hauptaktion als gefüllter Knopf (Form), Auswahl mit Kante oder Haken.

**Farbsehschwäche.** Für Menschen mit Rot-Grün-Schwäche sind Paare schwierig, die sich nur im Rot- oder
Grünanteil unterscheiden, besonders bei gleicher Helligkeit; ebenso Blau und Violett, Grün und Braun; Gelb und
Gelbgrün wirken gleich. Empfehlung: Helligkeit oder Sättigung deutlich unterscheiden, warme und kalte Töne
abwechseln, Formen und Beschriftung ergänzen [15]. Mehrere Quellen nennen Blau und Orange als sichere Paarung [16]
(überwiegend Blogs und Tutorials, nicht begutachtet).

Eigene Simulation der Deuteranopie (Machado, Oliveira, Fernandes 2009, Matrix für Schwere 1,0 auf lineares RGB
[17]), relative Luminanz der simulierten Farbe:

| Paar | simuliert | Helligkeitsverhältnis | Einschätzung |
|---|---|---|---|
| neon-orange / neon-red | beide Ocker, Rot etwas grauer | 1,18 | schwer zu trennen |
| neon-orange / calm-yellow | beide Gelb, Gelb blasser | 1,23 | schwer zu trennen |
| neon-red / neon-green | beide Gelboliv | 2,01 | nur über Helligkeit trennbar |
| neon-teal / neon-green | Teal grau, Grün gelblich | 1,43 | mäßig |
| Vorschlag Auswahl #28d6df / Erfolg #5be479 | Cyan bläulich, Grün gelblich | | über Farbton trennbar |
| Vorschlag Hauptaktion #fa7c20 / Braucht-dich #eed059 | beide Gelb | 1,58 | über Helligkeit trennbar |
| Vorschlag Hauptaktion #fa7c20 / Fehler #fd4f75 | Ocker / graues Beige | 1,17 | weiter schwer, braucht Form und Vorzeichen |

Abstand im OKLab-Raum (ΔE_OK, eigene Rechnung, größer heißt unterschiedlicher), die ähnlichsten heutigen Paare:

| Paar | ΔE_OK |
|---|---|
| neon-red / neon-pink | 0,084 |
| neon-orange / neon-red | 0,102 |
| calm-blue / calm-violet | 0,104 |
| neon-teal / neon-green | 0,142 |
| neon-orange / calm-yellow | 0,164 |

**Semantische Verwechslung heute (aus dem Styleguide §2):**

- Orange heißt Hauptaktion, aber auch "Keine Kategorie" in Listen, der Titel "Gibst du mehr aus?" und die Balken
  über dem Target. Das sind Warnungen in der Farbe der Aktion.
- Teal heißt Auswahl und Navigation, in Listen aber auch positiver Betrag, auf Targets prüfen zusätzlich "Passt".
- Grün (Erfolg, positiv) und Teal liegen 20° auseinander; positiver Betrag ist je nach Ort Grün (Einzelbetrag) oder
  Teal (Liste).
- Rot und Pink sind fast dieselbe Farbe; mit dem Wegfall von Pink löst sich das.
- calm-blue und calm-violet unterscheiden sich fast nur im Farbton (ΔE_OK 0,104), Blau/Violett ist ein bekanntes
  Problempaar bei Rot-Grün-Schwäche [15].

### 5. Violett als Lena-Farbe oder Flags

**YNAB:** sechs Flag-Farben (Rot, Orange, Gelb, Grün, Blau, Violett), umbenennbar ("Edit Flags", Farbe umbenennen)
[18][19]. Die Liste der Farben stammt aus einem inoffiziellen API-Client (Sekundärquelle), das Umbenennen aus
YNABs Hilfe. BB setzt das Lena-Flag schon mit der Farbe aus den Settings (`lena_flag_color`, ADR 0056).

**Monarch Money:** Buchungen lassen sich einer bestimmten Person im Haushalt "zur Prüfung" zuweisen, die Person
bekommt eine Benachrichtigung (seit 26.10.2023); Buchungen haben einen Eigentümer, der vom Konto erbt; jedes
Haushaltsmitglied bekommt automatisch ein Tag mit seinem Namen, Tag-Farben wählt man selbst [20][21]. Die Person
ist also über Name und Zuweisung erkennbar, die Farbe ist frei gewählt. Wie Monarch Eigentümer farblich zeigt,
habe ich nicht gefunden.

**Copilot Money:** Tags mit frei wählbaren Farben, beliebig viele; eine Prüf-Warteschlange "To Review" [22]
(Hilfeseiten, nicht im Detail geprüft).

Kein gesichtetes System reserviert eine UI-Farbe für eine Person. Personen tragen Namen, Farben gehören zu
frei konfigurierbaren Tags oder Flags.

## Regeln (Vorschlag, kurz)

1. Jede Farbe hat genau eine Bedeutung über alle Seiten. Seitenausnahmen gibt es nicht; braucht eine Seite eine neue
   Bedeutung, bekommt sie einen neuen Token oder Neutral.
2. Die Hauptaktion-Farbe erscheint nur auf Bedienelementen (Knopf, Link zur Hauptaktion), nie als Status oder
   Warnung.
3. Die Auswahlfarbe zeigt nur "gewählt" und "hier bist du" (Tab, Nav, gewählte Zeile, Fokus), nie einen Betrag.
4. Betragsfarben: Minus in der Fehler-Farbe, Plus in der Erfolgs-Farbe oder neutral; immer mit Vorzeichen.
5. "Braucht dich" (fehlt, zu prüfen) hat eine eigene Farbe und immer ein Wort daneben.
6. Jeder Akzent als Text hat mindestens 4,5:1 auf bg-card, bg-elevated und bg-hover; geprüft wird auf der
   hellsten Fläche, auf der er vorkommt.
7. Text auf farbiger Fläche: dunkel (`bg-app`) auf hellen Neonflächen, Weiß nur, wenn es 4,5:1 erreicht.
8. Ränder, die ein Bedienelement erst erkennbar machen, und Fokusringe haben 3:1 gegen die Fläche. Deko-Ränder
   dürfen schwächer sein.
9. Farbe nie allein: Wort, Icon, Vorzeichen oder Form gehören dazu (WCAG 1.4.1).
10. Akzente in OKLCH mit ähnlicher Helligkeit (L etwa 68 % bis 86 %) definieren; Rollen, die zusammen auftreten,
    liegen mindestens rund 40° im Farbton auseinander oder unterscheiden sich deutlich in L.
11. Glow nur an der Hauptaktion und an Rückmeldungen; Kontrast immer gegen die Fläche ohne Glow messen.
12. Personen tragen einen Namen; Farbe allein kennzeichnet keine Person.

## Vorschlags-Palette

Flächen, Text und `border` bleiben unverändert (bestehen alle). Kontrastwerte aus dem WebAIM-Prüfer [13] für den
gerundeten Hex-Wert (WebAIM kürzt auf drei Stellen), Werte mit * eigene Rechnung.

| Rolle | Token | OKLCH | Hex | bg-card | bg-elevated | Text auf Fläche |
|---|---|---|---|---|---|---|
| Hauptaktion | `accent` | 72 % 0,18 50 | #fa7c20 | 7,02 | 6,40 | #08081a: 7,51; Weiß: 2,63* |
| Auswahl, Navigation | `select` | 80 % 0,13 200 | #28d6df | 10,3 | 9,45 | #08081a: 11,1* |
| Fehler, Minus | `danger` | 68 % 0,21 12 | #fd4f75 | 5,77 | 5,26 | #08081a: 6,20*; Weiß: 3,19* |
| Braucht dich (Variante B) | `attention` | 86 % 0,14 95 | #eed059 | 12,1 | 11,0 | #08081a: 13,0* |
| Braucht dich (Variante A) | `attention` = heute `calm-yellow` | 77,4 % 0,080 85 | #cdb27a | 9,03 | 8,23 | |
| Erfolg, Plus | `success` | 82 % 0,19 148 | #5be479 | 11,2 | 10,2 | |
| Info | `info` = heute `calm-blue` | 76,9 % 0,121 253 | #7ab8ff | 8,93 | 8,14 | |
| Markierung (Flag, Person) | `mark` = heute `calm-violet` | 79,1 % 0,127 301 | #c9a7ff | 9,21 | 8,40 | |
| Starker Rand, Fokus | `border-strong` | 54 % 0,04 281 | #6a6c86 | 3,61* | 3,29 | |

Auf bg-hover (eigene Rechnung): accent 6,10, select 9,04, danger 5,03, attention B 10,5, success 9,85,
border-strong 3,15. Alle über der Schwelle.

Entfällt: `neon-pink` (gesetzt), `neon-purple` (fällt als Text durch; Rolle geht an `mark`), `calm-violet` als
Sonderfarbe für "Gibst du nie aus?" (würde zu `mark` oder Neutral).

### Optionen mit Abwägung

**Option 1: Hauptaktion bleibt Orange.**
- 1a (Orange bleibt #ff6b2c, Braucht-dich bleibt `calm-yellow`): kleinste Änderung. Orange und calm-yellow liegen 44°
  auseinander und unterscheiden sich stark in der Chroma (0,195 gegen 0,080); unter Deuteranopie Helligkeitsverhältnis
  nur 1,23. Rot und Orange bleiben 23° auseinander.
- 1b (Orange wird #fa7c20, Rot wird #fd4f75, Braucht-dich wird #eed059): Rot rückt in den Bereich, den Pink frei
  macht (h 12), Orange und Gelb rücken je etwas weg. Abstände: Rot zu Orange 38°, Orange zu Gelb 45°; Orange zu Gelb
  ΔE_OK 0,19, Helligkeitsverhältnis unter Deuteranopie 1,58. Gelb wird damit neonartig und hell (12,1:1), das
  widerspricht der Absicht aus ADR 0075, Gelb neben Rot ruhig zu halten.
- Nicht kombinieren: #fa7c20 mit calm-yellow. Das neue Orange liegt näher an calm-yellow (ΔE_OK 0,135 statt 0,164).
- In beiden Fällen: Knopftext dunkel (`bg-app`), sonst bleibt der Hauptknopf unter 4,5:1.

**Option 2: Hauptaktion wird die Auswahlfarbe (Modell Linear).** Eine Akzentfarbe (Cyan oder Teal) für Aktion und
Auswahl. Orange wird frei und kann ganz entfallen oder "Braucht dich" werden. Vorteil: der warme Bereich (Rot,
Orange, Gelb) trägt nur noch Status, die Verwechslung Aktion gegen Warnung verschwindet. Nachteil: Abschied vom
orangen Charakter der Hero-Knöpfe; Aktion und Auswahl unterscheiden sich nur über Form (gefüllter Knopf gegen
Kante oder Tab).

**Option 3: Hauptaktion grün (Modell Primer).** Grün für Hauptknopf und Erfolg. Vorteil: weniger Farben. Nachteil:
Grün heißt in BB auch "positiver Betrag"; ein grüner Knopf neben grünen Beträgen vermischt Aktion und Zustand.

**Auswahlfarbe Teal oder Cyan.** Teal #00d4aa (h 172) bleibt nur stimmig, wenn Grün heller bleibt (heutiges
#00ff88) oder Plus neutral wird. Mit dem vorgeschlagenen Grün #5be479 läge Teal bei ΔE_OK 0,092, zu nah. Cyan
#28d6df (h 200) liegt 52° von Grün und bleibt unter Deuteranopie bläulich, rückt aber an Info-Blau heran
(ΔE_OK 0,115). Info kommt heute nur auf der Seite Targets prüfen vor.

**Lena und Markierung.**
- A: `mark` Violett fest als Lena-Farbe. Einfach, bekannte Farbe. Nachteil: kollidiert, sobald `lena_flag_color`
  nicht violett ist, dann zeigt BB eine andere Farbe als YNAB.
- B: Markierungsfarbe folgt der YNAB-Flag-Farbe aus den Settings. Stimmt mit YNAB überein. Nachteil: sechs Flag-Farben
  würden mit den Bedeutungsfarben kollidieren (rotes Flag gegen Fehler, grünes gegen Erfolg); verträglich nur, wenn
  die Flag-Farbe ausschließlich im Flag-Symbol erscheint und das Symbol plus Name die Bedeutung trägt.
- C: Neutraler Namens-Chip ("Lena") ohne eigene Farbe, wie Monarch über Name und Zuweisung. Spart eine Farbe. Nachteil:
  in einer ruhigen Liste weniger auffällig.

## Sources

1. [Material Design 2: Dark theme](https://m2.material.io/design/color/dark-theme) (Google, etwa 2019). Entsättigte
   Farben, optische Vibration, #121212, 15,8:1, 200er-Ton. Nur über Auszug im Suchergebnis, Seite nicht abrufbar.
2. [Radix Colors: Understanding the scale](https://www.radix-ui.com/colors/docs/palette-composition/understanding-the-scale).
   Rollen der 12 Stufen, APCA-Garantie Lc 60 und Lc 90, helle Skalen brauchen dunklen Text. Aktuelle Doku, ohne Datum.
3. [Primer: Color overview](https://primer.style/foundations/color/overview). Rollen accent, success, attention, danger,
   open, closed, done, sponsors; success für Hauptknöpfe. Aktuell.
4. [@primer/primitives dark.css](https://unpkg.com/@primer/primitives/dist/css/functional/themes/dark.css). Hex-Werte
   des Dark-Themes (Stand npm-Paket, abgerufen 2026-11-07).
5. [@carbon/themes (lib/index.js)](https://unpkg.com/@carbon/themes/lib/index.js). Werte des Themes g100 (abgerufen
   2026-11-07). Die Carbon-Webseite zeigt nur das White-Theme im Text.
6. [Vercel Geist: Colors](https://vercel.com/geist/colors). 10 Stufen, Rollen je Stufenbereich, Grau als Standard.
7. [Linear: How we redesigned the Linear UI](https://linear.app/now/how-we-redesigned-the-linear-ui) (2024). LCH statt
   HSL, drei Theme-Variablen, weniger Akzentfarbe.
8. [Google Design: Material Design dark theme](https://design.google/library/material-design-dark-theme). Fallbeispiele
   (News, Calendar), Entsättigung, "Vibration". Ohne Zahlen.
9. Suchergebnisse zu Halation und Astigmatismus, u. a. [BOIA](https://www.boia.org/blog/dark-mode-can-improve-text-readability-but-not-for-everyone)
   und [accessibilitychecker.org](https://www.accessibilitychecker.org/blog/dark-mode-accessibility/). Blogs, Zahlen
   widersprüchlich, nur als Praxishinweis.
10. [Evil Martians: OKLCH in CSS](https://evilmartians.com/chronicles/oklch-in-css-why-quit-rgb-hsl) (2022, aktualisiert).
    Vorhersagbare Helligkeit, Gamut-Grenzen, APCA weiter nötig.
11. [W3C: Understanding SC 1.4.11 Non-text Contrast (WCAG 2.2)](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html).
    3:1 für Bedienelemente und Grafik, Ausnahmen.
12. [APCA in a Nutshell](https://git.apcacontrast.com/documentation/APCA_in_a_Nutshell.html). Lc-Stufen, Kritik an
    WCAG 2 bei dunklen Farben (Quelle ist der APCA-Autor).
13. [WebAIM Contrast Checker (API)](https://webaim.org/resources/contrastchecker/). Gegenprobe der Kontrastwerte,
    abgerufen 2026-11-07.
14. [W3C: Understanding SC 1.4.1 Use of Color (WCAG 2.2)](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html).
    Farbe nie allein, zulässige Zusatzzeichen.
15. [Okabe und Ito: Color Universal Design (jfly)](https://jfly.uni-koeln.de/color). Verwechslungspaare bei
    Rot-Grün-Schwäche, Empfehlungen. Ältere, viel zitierte Primärquelle.
16. [EU Data Visualisation Guide: Accessible colour palettes](https://data.europa.eu/apps/data-visualisation-guide/accessible-colour-palettes).
    Rot-Grün nur mit Helligkeitsunterschied, Blau/Orange.
17. [Machado, Oliveira, Fernandes: A Physiologically-based Model for Simulation of Color Vision Deficiency](https://www.inf.ufrgs.br/~oliveira/pubs_files/CVD_Simulation/CVD_Simulation.html)
    (IEEE TVCG 2009). Matrix für die eigene Simulation.
18. [YNAB: Flags, a guide](https://support.ynab.com/en_us/flags-a-guide-Skh8Xb4kj). Flags umbenennen (laut Suchauszug;
    die abgerufene Seite zeigte nur die Kurzbeschreibung).
19. [ynab Ruby-Gem: TransactionFlagColor](https://rubydoc.info/gems/ynab/YNAB/TransactionFlagColor). Die sechs
    Flag-Farben. Inoffizieller Client, Sekundärquelle.
20. [Monarch: Assign transactions to a household member for review](https://monarchmoney.com/whats-new/assign-transactions-to-a-household-member-for-review)
    (26.10.2023). Hersteller-Ankündigung.
21. [Monarch Help: Organizing Transactions with Tags](https://help.monarch.com/hc/en-us/articles/4409690120596-Organizing-Transactions-with-Tags).
    Tag je Haushaltsmitglied, Farbe wählbar (laut Suchauszug).
22. [Copilot Money Help: Tags](https://intercom.help/copilotmoney/en/articles/9554342-tags). Farbige Tags (laut
    Suchauszug).

## Open questions

- **APCA-Werte nicht gerechnet.** Ohne Rechenwerkzeug in dieser Sitzung habe ich nur WCAG-2-Verhältnisse berechnet.
  Für `text-muted` (4,62:1 auf bg-hover) und die Akzente lohnt eine APCA-Prüfung (Ziel Lc 60 für Inhaltstext, Lc 75
  für Fließtext), etwa mit dem Rechner auf apcacontrast.com.
- **Gamut und P3.** Die Vorschläge liegen in sRGB. Ob man für P3-Displays kräftigere Varianten per
  `@media (color-gamut: p3)` anbietet [10], ist offen.
- **Material 3.** Die Ton-Zuordnungen des M3-Dark-Schemas (etwa Primär Ton 80, Text auf Primär Ton 20) habe ich nicht
  aus einer Primärquelle bestätigen können; die M3-Seiten lieferten keinen Text.
- **Glow.** Zum Einfluss von Glow auf Lesbarkeit habe ich keine belastbare Quelle gefunden.
- **Simulation.** Die Deuteranopie-Werte sind eine eigene Rechnung mit einer Matrix; ein Test mit Sim Daltonism oder
  Color Oracle auf echten Screens wäre die Gegenprobe. Protanopie und Tritanopie habe ich nicht gerechnet
  (Tritanopie betrifft vor allem Blau gegen Grün und Gelb gegen Violett).
- **Rechenweg der Kontraste.** Eigene Rechnung nach der WCAG-2-Luminanzformel, OKLCH nach Ottosson; Stichproben
  gegen WebAIM und die drei Styleguide-Werte stimmen. Ein Skript (`scripts/`) könnte die Tabelle im Gate halten,
  falls Roman das will.
