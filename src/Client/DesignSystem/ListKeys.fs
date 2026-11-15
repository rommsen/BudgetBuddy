/// Die Tastenregel für jede Liste mit Panel am PC (Styleguide §7.8, ADR 0087 Punkte 18 und 19, ADR 0096 Punkt 2;
/// design-system-gcpws). Rein: die Seite liest Fokus, Zusatztasten und ihren Zustand aus dem Ereignis und dem Modell,
/// bildet ihre eigenen Tasten davor ab (Kassenbuch G, L, J/K, Leertaste, Enter) und führt aus, was hier herauskommt.
/// Vorbild war `pageKeyAction` in `Components/Budget/Inspector.fs`.
module Client.DesignSystem.ListKeys

/// Wo der Fokus steht, wenn die Taste kommt.
type Focus =
    /// Im Suchfeld der Liste: die Pfeile gehören trotzdem der Liste (§7.8 Punkt 1).
    | InSearch
    /// In einem anderen Feld (Betrag, Zugewiesen, Text, Auswahl): die Pfeile gehören dem Cursor (Punkt 2).
    | InTextField
    /// Sonst (Seite, Zeile, Knopf).
    | Elsewhere

/// Richtung eines Schritts: nächste oder vorige Zeile, rechter oder linker Tab.
type Step =
    | Forward
    | Backward

/// Was die Liste kann. Fest je Seite.
type Traits = {
    /// Mehrfachauswahl (§7.7): Umschalt+↑↓ erweitert die Auswahl.
    MultiSelect: bool
    /// Das Panel hat Tabs: ←→ wechseln sie.
    Tabs: bool
    /// Die Liste hat ein Suchfeld.
    Search: bool
    /// Ein druckbares Zeichen ohne Feldfokus geht in die Suche (§7.8 Punkt 5). Das Kassenbuch nicht: dort sind
    /// G, L, J und K belegt, gesucht wird mit „/“ (F9).
    TypeToSearch: bool
}

/// Die Taste und der Zustand der Seite in dem Moment.
type Situation = {
    Key: string
    Focus: Focus
    /// Alt, Strg oder Cmd gedrückt.
    Command: bool
    Shift: bool
    /// `defaultPrevented`: ein Baustein (etwa `Tabs` oder ein Feld) hat die Taste schon genommen.
    Handled: bool
    /// Ein Dialog, Picker, Popover oder Sheet liegt über der Liste; er bekommt die Tasten zuerst (Punkt 6).
    LayerOpen: bool
    /// Eine Zeile ist gewählt (das Panel zeigt sie): ↑↓ gehen von ihr aus.
    RowSelected: bool
    /// Das Panel zeigt ein Ding (Detail, Formular, neue Buchung), das Esc schließt.
    PanelOpen: bool
    /// Eine Mehrfachauswahl steht, die Esc aufhebt.
    SelectionActive: bool
}

/// Was die Taste auslöst.
type Action =
    | FirstRow
    | LastRow
    | StepRow of Step
    | StepTab of Step
    | ExtendSelection of Step
    | ClosePanel
    | ClearSelection
    | FocusSearch
    | TypeIntoSearch
    /// Die Taste gehört dem Feld, dem Knopf, dem Browser oder der Ebene darüber.
    | Leave

/// Was Esc ohne offene Ebene tut (ADR 0096 Punkt 2, §7.8 Punkt 6): zuerst schließt es das Panel, die Auswahl bleibt;
/// danach hebt es die Auswahl auf. Eigene Funktion, weil das Kassenbuch die Stufen vor dem Panel (Popover, Picker,
/// Bearbeitung im Detail) in seiner Kette hält und für den Rest dieselbe Reihenfolge braucht.
let escapeStep (panelOpen: bool) (selectionActive: bool) : Action =
    if panelOpen then ClosePanel
    elif selectionActive then ClearSelection
    else Leave

let private rowStep (situation: Situation) (step: Step) : Action =
    match situation.RowSelected, step with
    | true, _ -> StepRow step
    | false, Forward -> FirstRow
    | false, Backward -> LastRow

let private arrow (traits: Traits) (situation: Situation) (vertical: Step option) (horizontal: Step option) : Action =
    match situation.Focus, vertical, horizontal with
    | InTextField, _, _ -> Leave
    | _, Some step, _ when situation.Shift -> if traits.MultiSelect then ExtendSelection step else Leave
    | _, Some step, _ -> rowStep situation step
    | _, _, Some step when not situation.Shift && traits.Tabs && situation.RowSelected -> StepTab step
    | _ -> Leave

let private printable (traits: Traits) (situation: Situation) : Action =
    match situation.Focus with
    | Elsewhere when traits.Search && traits.TypeToSearch -> TypeIntoSearch
    | Elsewhere when traits.Search && situation.Key = "/" -> FocusSearch
    | _ -> Leave

/// Die Regel. Reihenfolge: genommene Taste, offene Ebene, Zusatztaste Alt/Strg/Cmd, dann die Taste.
let actionFor (traits: Traits) (situation: Situation) : Action =
    if situation.Handled || situation.LayerOpen || situation.Command then
        Leave
    else
        match situation.Key with
        | "Escape" -> escapeStep situation.PanelOpen situation.SelectionActive
        | "ArrowDown" -> arrow traits situation (Some Forward) None
        | "ArrowUp" -> arrow traits situation (Some Backward) None
        | "ArrowRight" -> arrow traits situation None (Some Forward)
        | "ArrowLeft" -> arrow traits situation None (Some Backward)
        | key when key.Length = 1 && key <> " " -> printable traits situation
        | _ -> Leave
