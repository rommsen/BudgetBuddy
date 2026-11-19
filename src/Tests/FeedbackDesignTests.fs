module FeedbackDesignTests

// design-system-ytgnc (ADR 0098, Regel R3): die reinen Entscheidungen des Bausteins `Feedback`. Ob die Leiste
// steht, welche Form „+ Buchung“ hat und was eine Meldung zeigt, entscheiden `barVisible`, `addShape` und `parts`;
// die View ruft sie nur auf. Größen und Trefferflächen misst der Layout-Sweep (Zustand `styleguide-rueckmeldung`).

open Expecto
open Client.DesignSystem
open Client.DesignSystem.Feedback

let private undo : Act = { Label = "Rückgängig"; OnClick = ignore }

let private message tone act : Message =
    { Tone = tone; Action = "Geprüft"; Detail = Some "REWE Markt GmbH Filiale Leipzig-Gohlis"; Act = act }

let private allTones = [ Tone.Ok; Tone.Warning; Tone.Error; Tone.Running; Tone.Info ]

let private tests =
    testList "Feedback (Rückmeldung am Handy, ADR 0098)" [
        test "Ohne Meldung und ohne „+ Buchung“ steht keine Leiste" {
            Expect.isFalse (barVisible None false) "leer"
        }
        test "Mit „+ Buchung“ steht die Leiste auch ohne Meldung, mit Meldung auch ohne Knopf" {
            Expect.isTrue (barVisible None true) "nur Knopf"
            Expect.isTrue (barVisible (Some (message Tone.Ok None)) false) "nur Meldung"
            Expect.isTrue (barVisible (Some (message Tone.Ok None)) true) "beides"
        }
        test "Ohne Meldung ist „+ Buchung“ breit, mit Meldung rund" {
            Expect.equal (addShape None) AddShape.Wide "ohne Meldung"
            for tone in allTones do
                Expect.equal (addShape (Some (message tone None))) AddShape.Round $"mit Meldung {tone}"
        }
        test "Eine Meldung zeigt ihren Knopf nur, wenn sie einen hat" {
            for tone in allTones do
                Expect.isTrue (parts (message tone (Some undo))).ShowsAct $"{tone} mit Knopf"
                Expect.isFalse (parts (message tone None)).ShowsAct $"{tone} ohne Knopf"
        }
        test "Ok trägt ein grünes ✓, Warnung ein gelbes !, Fehler ein rotes !, Läuft und Info kein Zeichen" {
            let mark tone = (parts (message tone None)).Mark
            Expect.equal (mark Tone.Ok) (Some ("✓", Tokens.Colors.success)) "Ok"
            Expect.equal (mark Tone.Warning) (Some ("!", Tokens.Colors.attention)) "Warnung"
            Expect.equal (mark Tone.Error) (Some ("!", Tokens.Colors.error)) "Fehler"
            Expect.equal (mark Tone.Running) None "Läuft"
            Expect.equal (mark Tone.Info) None "Info"
        }
        test "Die Aktion steht bei Fehler rot, bei Warnung gelb und bei Läuft gedämpft" {
            let color tone = (parts (message tone None)).ActionColor
            Expect.equal (color Tone.Error) Tokens.Colors.error "Fehler"
            Expect.equal (color Tone.Warning) Tokens.Colors.attention "Warnung"
            Expect.equal (color Tone.Running) Tokens.Colors.textSecondary "Läuft"
            Expect.equal (color Tone.Ok) Tokens.Colors.textPrimary "Ok"
            Expect.equal (color Tone.Info) Tokens.Colors.textPrimary "Info"
        }
        test "Nur bei Fehler und Warnung bricht der Grund um, sonst wird er auf einer Zeile gekürzt" {
            for tone in allTones do
                let expected = (tone = Tone.Error || tone = Tone.Warning)
                Expect.equal (parts (message tone None)).DetailWraps expected $"{tone}"
        }
        test "Nur ein Fehler wird sofort vorgelesen" {
            for tone in allTones do
                Expect.equal (parts (message tone None)).IsAlert (tone = Tone.Error) $"{tone}"
        }
    ]

[<Tests>]
let feedbackDesignTests = tests
