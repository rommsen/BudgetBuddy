/// Der Tab „Regeln“ im Kassenbuch (categorization-n3sxw, Prototyp Version 17 `rulesView()`): Hinweis, Liste nach
/// Priorität mit Pfeilen, am PC Tabelle mit Panel rechts (ab 1024 px), am Handy Karte mit Sheet. Kopf und Tabs
/// stellt das Kassenbuch (`Components.Ledger.View.view`), der Root reicht `subtitle`, `headerActions` und
/// `content` hinein.
module Components.Rules.View

open Feliz
open Fable.Core
open Fable.Core.JsInterop
open Components.Rules.Types
open Components.Rules.State
open Types
open Shared.Domain
open Client.DesignSystem
open Client.DesignSystem.Icons
open Client.DesignSystem.Tokens

// ============================================
// Texte (rein, testbar)
// ============================================

/// „Wenn“ im Formular und in der Liste.
let patternTypeLabel (patternType: PatternType) =
    match patternType with
    | Contains -> "Enthält"
    | Exact -> "Exakt"
    | PatternType.Regex -> "Regex"

/// „Suchen in“ im Formular und in der Liste.
let targetFieldLabel (targetField: TargetField) =
    match targetField with
    | Combined -> "Payee & Memo"
    | Payee -> "Payee"
    | Memo -> "Memo"

/// Die Spalte Art am PC, etwa „Enthält · Payee“.
let kindText (rule: Rule) = $"{patternTypeLabel rule.PatternType} · {targetFieldLabel rule.TargetField}"

/// Zeile 2 am Handy, etwa „Enthält „rewe“ in Payee“.
let conditionText (rule: Rule) =
    $"{patternTypeLabel rule.PatternType} „{rule.Pattern}“ in {targetFieldLabel rule.TargetField}"

/// Untertitel im Kopf des Kassenbuchs, etwa „5 Regeln, 4 aktiv“; ohne Regeln keiner.
let summaryText (rules: Rule list) =
    match List.length rules with
    | 0 -> ""
    | total ->
        let active = rules |> List.filter (fun r -> r.Enabled) |> List.length
        let noun = if total = 1 then "Regel" else "Regeln"
        $"{total} {noun}, {active} aktiv"

let priorityHint = "Regeln werden von oben nach unten geprüft. Die erste passende, aktive Regel gewinnt."
let emptyPanelText = "Regel wählen oder eine neue anlegen."
let testHintText = "Text eingeben, um das Muster zu prüfen."

/// Die Zeile unter „Muster testen“: Text und Ton.
type TestLine =
    | HintLine of string
    | MatchLine of string
    | NoMatchLine of string
    | FaultLine of string

let testLine (test: PatternTest) : TestLine =
    match test with
    | NothingToTest -> HintLine testHintText
    | Testing _ -> HintLine "Wird geprüft…"
    | Tested (_, Matches) -> MatchLine "Passt"
    | Tested (_, NoMatch) -> NoMatchLine "Passt nicht"
    | Tested (_, PatternFaulty reason) -> FaultLine reason
    | Tested (_, Unreachable) -> FaultLine ruleTestLostText
    | Tested (_, TestFailed message) -> FaultLine $"Muster konnte nicht geprüft werden: {message}"

let editorTitle (editor: Editor) =
    match editor with
    | NewRule -> "Neue Regel"
    | Editing _ -> "Regel bearbeiten"

let submitLabel (editor: Editor) =
    match editor with
    | NewRule -> "Regel anlegen"
    | Editing _ -> "Speichern"

/// Die Auswahl der Kategorie: der gewählte Name wie im Picker, fehlt er in den geladenen Kategorien, der
/// gespeicherte Name der Regel.
let categoryValue (categories: YnabCategory list) (panel: Panel) : string option =
    let known =
        panel.Draft.CategoryId
        |> Option.bind (fun id -> categories |> List.tryFind (fun c -> c.Id = id))
        |> Option.map (fun c -> c.Name)
    let saved =
        match panel.Editor with
        | Editing rule when Some rule.CategoryId = panel.Draft.CategoryId -> Some rule.CategoryName
        | _ -> None
    known |> Option.orElse saved

let private ruleKey (rule: Rule) = let (RuleId id) = rule.Id in string id

let private selectedId (model: Model) =
    match model.Panel with
    | Some { Editor = Editing rule } -> Some rule.Id
    | _ -> None

/// Die geladenen Kategorien; solange sie laden oder fehlen, keine.
let private loadedCategories (model: Model) : YnabCategory list =
    match model.Categories with
    | RemoteData.Success categories -> categories
    | _ -> []

/// Der Ladezustand der Kategorien für die Auswahl: nach einem Fehler der Grund mit „Erneut versuchen“, kein ewiges
/// „Kategorien werden geladen“.
let categoryLoadState (categories: RemoteData<YnabCategory list>) : CategoryPicker.PickerLoadState =
    match categories with
    | RemoteData.Success _ -> CategoryPicker.PickerReady
    | RemoteData.Failure reason -> CategoryPicker.PickerFailed (Some reason)
    | RemoteData.NotAsked
    | RemoteData.Loading -> CategoryPicker.PickerLoading

// ============================================
// Liste
// ============================================

let private moveArrows (testId: string) (index: int) (total: int) (rule: Rule) (dispatch: Msg -> unit) =
    [ Button.orderArrow $"{testId}-up" "Priorität erhöhen" (chevronUp SM Default) (index > 0) (fun () ->
          dispatch (MoveRequested (rule.Id, Up)))
      Button.orderArrow $"{testId}-down" "Priorität senken" (chevronDown SM Default) (index < total - 1) (fun () ->
          dispatch (MoveRequested (rule.Id, Down))) ]

/// PC: Tabelle mit #, Name, Muster, Art, Kategorie, Status und den Pfeilen (Prototyp `rhead`/`rrow`).
let private ruleTable (model: Model) (rules: Rule list) (dispatch: Msg -> unit) =
    let total = rules.Length
    let selected = selectedId model
    let headCell (text: string) = Html.span [ prop.className "truncate"; prop.title text; prop.text text ]
    let grid = "grid grid-cols-[28px_minmax(0,1fr)_minmax(0,1fr)_minmax(0,1fr)_minmax(0,1fr)_64px_88px] items-center gap-3 px-3.5"
    Html.div [
        prop.custom ("data-testid", "rules-table")
        prop.className $"overflow-hidden rounded-xl border {Borders.default'} {Backgrounds.dark}"
        prop.children [
            Html.div [
                prop.className $"{grid} h-[34px] border-b {Borders.default'} {ListRoles.groupHeader}"
                prop.children [
                    headCell "#"; headCell "Name"; headCell "Muster"; headCell "Art"; headCell "Kategorie"; headCell "Status"
                    Html.span []
                ]
            ]
            for index, rule in List.indexed rules do
                let isSelected = selected = Some rule.Id
                Html.div [
                    prop.key (ruleKey rule)
                    prop.custom ("data-testid", "rule-row")
                    if isSelected then prop.custom ("aria-current", "true")
                    prop.className (
                        $"{grid} h-11 border-b {Borders.subtle} last:border-b-0 cursor-pointer text-sm transition-colors "
                        + (if isSelected then ListRoles.selectedRow else "hover:bg-surface-hover")
                    )
                    prop.onClick (fun _ -> dispatch (RuleChosen rule.Id))
                    prop.children [
                        Html.span [ prop.className ListRoles.date; prop.text (string (index + 1)) ]
                        Html.button [
                            prop.type' "button"
                            prop.className $"truncate text-left font-medium {Colors.textPrimary}"
                            prop.title rule.Name
                            prop.ariaLabel $"Regel bearbeiten: {rule.Name}"
                            prop.text rule.Name
                        ]
                        Html.span [ prop.className $"truncate {Fonts.mono} {Colors.textPrimary}"; prop.title rule.Pattern; prop.text rule.Pattern ]
                        Html.span [ prop.className $"truncate {Colors.textSecondary}"; prop.title (kindText rule); prop.text (kindText rule) ]
                        Html.span [ prop.className $"truncate {Colors.textSecondary}"; prop.title rule.CategoryName; prop.text rule.CategoryName ]
                        Html.span [
                            // Der Status ist kein Erfolg: „aktiv“ in `text-secondary`, „inaktiv“ gedämpft (Entscheidung Roman 2026-11-13).
                            prop.className (if rule.Enabled then $"{ListRoles.secondary}" else $"{ListRoles.muted}")
                            prop.text (if rule.Enabled then "aktiv" else "inaktiv")
                        ]
                        Html.span [
                            prop.className "flex justify-end"
                            prop.onClick (fun e -> e.stopPropagation ())
                            prop.children (moveArrows "rule-move" index total rule dispatch)
                        ]
                    ]
                ]
        ]
    ]

/// Handy: Karte mit einer Zeile je Regel (Prototyp `rulesView()`): Nummer, Name, Bedingung, Kategorie, rechts die
/// Pfeile übereinander.
let private ruleCard (model: Model) (rules: Rule list) (dispatch: Msg -> unit) =
    let total = rules.Length
    let selected = selectedId model
    Html.div [
        prop.custom ("data-testid", "rules-card")
        prop.className $"overflow-hidden rounded-xl border {Borders.default'} {Backgrounds.dark}"
        prop.children [
            for index, rule in List.indexed rules ->
                Html.div [
                    prop.key (ruleKey rule)
                    prop.custom ("data-testid", "rule-row")
                    if selected = Some rule.Id then prop.custom ("aria-current", "true")
                    prop.className (
                        $"flex items-center border-b {Borders.subtle} last:border-b-0 "
                        + (if selected = Some rule.Id then ListRoles.selectedRow else "")
                    )
                    prop.children [
                        Html.button [
                            prop.type' "button"
                            prop.className "flex flex-1 min-w-0 items-center gap-2.5 py-3 pl-4 pr-2 text-left touch-manipulation"
                            prop.ariaLabel $"Regel bearbeiten: {rule.Name}"
                            prop.onClick (fun _ -> dispatch (RuleChosen rule.Id))
                            prop.children [
                                Html.span [ prop.className $"w-[18px] shrink-0 {ListRoles.date}"; prop.text (string (index + 1)) ]
                                Html.span [
                                    prop.className "flex min-w-0 flex-col gap-0.5"
                                    prop.children [
                                        Html.span [
                                            prop.className "min-w-0 break-words"
                                            prop.children [
                                                Html.span [ prop.className ListRoles.primary; prop.text rule.Name ]
                                                if not rule.Enabled then
                                                    Html.span [ prop.className ListRoles.muted; prop.text " · inaktiv" ]
                                            ]
                                        ]
                                        Html.span [ prop.className $"break-words {ListRoles.secondary}"; prop.text (conditionText rule) ]
                                        Html.span [ prop.className $"break-words {ListRoles.secondary}"; prop.text $"→ {rule.CategoryName}" ]
                                    ]
                                ]
                            ]
                        ]
                        Html.div [
                            // 8 px zwischen den Pfeilen (§7.15), wie in `Button.orderNameRow`.
                            prop.className "flex shrink-0 flex-col gap-2 pr-1"
                            prop.children (moveArrows "rule-move" index total rule dispatch)
                        ]
                    ]
                ]
        ]
    ]

let private newRuleButtonBlock (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "rule-new-block")
        prop.children [
            Button.view {
                Button.defaultProps with
                    Text = "Neue Regel"
                    Variant = Button.Outline
                    FullWidth = true
                    Icon = Some (Icons.plus SM Icons.Default)
                    OnClick = fun () -> dispatch NewRuleRequested
            }
        ]
    ]

/// Wie die Liste: Tabelle, wo die Tabelle steht, sonst die Karte mit drei Zeilen je Regel.
let private skeleton () =
    Html.div [
        prop.role "status"
        prop.ariaLabel "Regeln werden geladen…"
        prop.children [
            Html.div [ prop.className "hidden md:block lg:hidden xl:block"; prop.children [ Loading.tableSkeleton 6 5 ] ]
            Html.div [ prop.className "md:hidden lg:block xl:hidden"; prop.children [ Loading.cardListSkeleton 5 3 ] ]
        ]
    ]

// ============================================
// Formular (Panel und Sheet)
// ============================================

let private labelClass = $"mb-1 block {FontSizes.xs} {Colors.textSecondary}"

/// Ein Textfeld mit seiner Bezeichnung: das `label` umschließt das Feld und ist so mit ihm verbunden.
let private labelled (text: string) (field: ReactElement) =
    Html.label [
        prop.className "block"
        prop.children [ Html.span [ prop.className labelClass; prop.text text ]; field ]
    ]

/// Die Bezeichnung über einer Leiste: die Leiste (`role group`) trägt denselben Namen als `aria-label`, ein `label`
/// hätte kein Feld.
let private groupCaption (text: string) =
    Html.span [ prop.ariaHidden true; prop.className labelClass; prop.text text ]

let private segmentsFor (testId: string) (ariaLabel: string) (options: ('a * string) list) (current: 'a) (onChoose: 'a -> unit) (disabled: bool) =
    Segmented.view
        testId
        ariaLabel
        [ for value, text in options ->
              { Segmented.segment text (value = current) (fun () -> onChoose value) with
                  KeepsFocus = true
                  Disabled = disabled } ]

let private testLineView (test: PatternTest) =
    let className, text =
        match testLine test with
        | HintLine t -> ListRoles.muted, t
        | MatchLine t -> $"text-xs {Colors.success}", t
        // „Passt nicht“ ist ein Urteil, kein Fehler: neutral (Entscheidung Roman 2026-11-13).
        | NoMatchLine t -> ListRoles.secondary, t
        | FaultLine t -> $"text-xs {Colors.error}", t
    Html.p [
        prop.custom ("data-testid", "rule-test-result")
        prop.custom ("aria-live", "polite")
        prop.className $"mt-1.5 px-0.5 {className}"
        prop.text text
    ]

/// Die Auswahl der Kategorie, im Popover am PC und im Sheet am Handy dieselbe.
let private pickerPropsFor (model: Model) (panel: Panel) (dispatch: Msg -> unit) : CategoryPicker.CategoryPickerProps =
    { Categories = loadedCategories model
      Current = panel.Draft.CategoryId
      Suggested = []
      Recent = []
      Accounts = []
      CurrentAccount = None
      AccountsLoad = CategoryPicker.PickerReady
      OfferWithout = false
      ToBeBudgeted = None
      StandLabel = None
      LoadState = categoryLoadState model.Categories
      OnRetry = fun () -> dispatch LoadCategories
      OnSelect = CategoryPicker.categoryChoice (CategoryChosen >> dispatch)
      OnClose = fun () -> dispatch CategoryPickerClosed }

let private formFields (model: Model) (panel: Panel) (shell: Shell) (dispatch: Msg -> unit) =
    let draft = panel.Draft
    let saving = SaveState.isSaving panel.Save
    let patternError =
        match panel.Save with
        | SaveFailed (PatternRejected reason) -> Some reason
        | _ -> None
    let pickerProps = pickerPropsFor model panel dispatch
    Html.div [
        prop.custom ("data-testid", "rule-form")
        prop.className "flex flex-col gap-3"
        prop.children [
            labelled "Name" (
                Input.text {
                    Input.textInputDefaults with
                        Value = draft.Name
                        OnChange = NameEntered >> dispatch
                        Placeholder = "z. B. Rewe"
                        AriaLabel = Some "Name"
                        Disabled = saving
                }
            )
            Html.div [
                prop.children [
                    groupCaption "Wenn"
                    segmentsFor "rule-pattern-type" "Wenn"
                        [ Contains, "Enthält"; Exact, "Exakt"; PatternType.Regex, "Regex" ]
                        draft.PatternType (PatternTypeChosen >> dispatch) saving
                ]
            ]
            Html.div [
                prop.children [
                    groupCaption "Suchen in"
                    segmentsFor "rule-target-field" "Suchen in"
                        [ Combined, "Payee & Memo"; Payee, "Payee"; Memo, "Memo" ]
                        draft.TargetField (TargetFieldChosen >> dispatch) saving
                ]
            ]
            Html.div [
                prop.children [
                    labelled "Muster" (
                        Input.text {
                            Input.textInputDefaults with
                                Value = draft.Pattern
                                OnChange = PatternEntered >> dispatch
                                Placeholder = "rewe"
                                AriaLabel = Some "Muster"
                                ClassName = Some "input-mono"
                                Disabled = saving
                                State = (match patternError with Some reason -> Input.Error reason | None -> Input.Normal)
                                AriaInvalid = patternError.IsSome
                                DescribedBy = (if patternError.IsSome then Some "rule-pattern-error" else None)
                        }
                    )
                    match patternError with
                    | Some reason ->
                        Html.p [
                            prop.id "rule-pattern-error"
                            prop.custom ("data-testid", "rule-pattern-error")
                            prop.className $"mt-1.5 px-0.5 text-xs {Colors.error}"
                            prop.text reason
                        ]
                    | None -> ()
                ]
            ]
            Html.div [
                prop.className "relative"
                prop.children [
                    Html.span [
                        // Ohne das wertet das Popover den Mausdruck als Klick außerhalb, und der Klick öffnete es
                        // gleich wieder (Bug budgeting-nfrds).
                        prop.onMouseDown (fun e -> e.stopPropagation ())
                        prop.className "block"
                        prop.children [
                            Input.pickerField
                                "rule-category"
                                "Dann Kategorie"
                                (categoryValue (loadedCategories model) panel)
                                "wählen"
                                None
                                panel.PickerOpen
                                (fun () -> dispatch (if panel.PickerOpen then CategoryPickerClosed else CategoryPickerOpened))
                        ]
                    ]
                    // Am Handy ersetzt die Auswahl den Inhalt des Sheets (`sheet`), hier steht nur das Popover am PC.
                    match shell with
                    | InPanel -> CategoryPicker.popoverInPanelRevealed panel.PickerOpen pickerProps
                    | InSheet -> ()
                ]
            ]
            // Ein Schalter mit kurzer Bezeichnung ohne Erklärsatz ist `toggle` (§5 Input).
            Html.div [
                prop.custom ("data-testid", "rule-enabled")
                prop.children [ Input.toggle draft.Enabled (EnabledSwitched >> dispatch) (Some "Regel aktiv") ]
            ]
            Html.div [
                prop.children [
                    labelled "Muster testen" (
                        Input.text {
                            Input.textInputDefaults with
                                Value = draft.TestInput
                                OnChange = TestInputEntered >> dispatch
                                Placeholder = "Beispiel: REWE Markt 4711"
                                AriaLabel = Some "Muster testen"
                        }
                    )
                    testLineView panel.Test
                ]
            ]
            match panel.Save with
            | SaveFailed (SaveRejected message) ->
                Html.p [
                    prop.custom ("data-testid", "rule-form-error")
                    prop.role "alert"
                    prop.className $"text-sm {Colors.error}"
                    prop.text message
                ]
            | _ -> ()
            match panel.Editor with
            | Editing _ ->
                Button.view {
                    Button.defaultProps with
                        Text = "Regel löschen"
                        Variant = Button.Danger
                        Size = Button.Small
                        FullWidth = true
                        IsDisabled = saving
                        OnClick = fun () -> dispatch DeleteRequested
                }
            | NewRule -> ()
        ]
    ]

let private formButtons (panel: Panel) (shell: Shell) (dispatch: Msg -> unit) =
    let submit () =
        match panel.Editor with
        | NewRule -> dispatch (CreateRequested shell)
        | Editing _ -> dispatch (ChangeRequested shell)
    Html.div [
        prop.className "grid w-full grid-cols-2 items-start gap-3"
        prop.children [
            Button.view {
                Button.defaultProps with
                    Text = "Abbrechen"
                    Variant = Button.Outline
                    FullWidth = true
                    IsDisabled = SaveState.isSaving panel.Save
                    OnClick = fun () -> dispatch PanelClosed
            }
            Form.submitButtonBlock (submitLabel panel.Editor) submit (SaveState.isSaving panel.Save) (requiredFieldPairs panel.Draft)
        ]
    ]

let private panelHead (title: string) (dispatch: Msg -> unit) =
    Html.div [
        prop.className $"flex items-center justify-between gap-3 px-4 py-3 border-b {Borders.subtle}"
        prop.children [
            Html.h2 [ prop.className $"{FontSizes.lg} font-semibold {Colors.textPrimary}"; prop.text title ]
            Button.view {
                Button.defaultProps with
                    Icon = Some (Icons.x SM Icons.Default)
                    Variant = Button.Ghost
                    Size = Button.Small
                    AriaLabel = Some "Schließen"
                    Title = Some "Schließen"
                    OnClick = fun () -> dispatch PanelClosed
            }
        ]
    ]

/// PC: das Panel rechts, so breit wie das Panel der Buchungen. Ohne Auswahl der Hinweis.
let private sidePanel (model: Model) (dispatch: Msg -> unit) =
    match model.Panel with
    | Some panel ->
        Detail.panel (editorTitle panel.Editor) [
            panelHead (editorTitle panel.Editor) dispatch
            Detail.scrollBody [
                Html.div [ prop.className "px-4 py-3"; prop.children [ formFields model panel InPanel dispatch ] ]
                Html.div [
                    prop.className $"border-t {Borders.default'} px-4 py-3"
                    prop.children [ formButtons panel InPanel dispatch ]
                ]
            ]
        ]
    | None ->
        Detail.panel "Regel" [
            Html.p [
                prop.custom ("data-testid", "rule-panel-empty")
                prop.className $"m-auto px-6 text-center text-sm {Colors.textMuted}"
                prop.text emptyPanelText
            ]
        ]

/// Handy: das Sheet. Ist die Auswahl der Kategorie offen, ersetzt sie den Inhalt (§7.15) und führt nach der Wahl
/// zurück. Immer gerendert (`viewHeld`): es fährt ein und aus und hält beim Ausfahren den letzten Inhalt, statt leer
/// herauszufahren.
let private sheet (model: Model) (dispatch: Msg -> unit) =
    match model.Panel with
    | Some panel when panel.PickerOpen ->
        BottomSheet.viewHeld
            { IsOpen = true
              OnClose = fun () -> dispatch CategoryPickerClosed
              Title = CategoryPicker.sheetTitle []
              Subtitle = None
              Footer = None }
            [ CategoryPicker.sheetContent (pickerPropsFor model panel dispatch) ]
    | Some panel ->
        BottomSheet.viewHeld
            { IsOpen = true
              OnClose = fun () -> dispatch PanelClosed
              Title = editorTitle panel.Editor
              Subtitle = None
              Footer = Some [ formButtons panel InSheet dispatch ] }
            [ Html.div [ prop.className "py-2"; prop.children [ formFields model panel InSheet dispatch ] ] ]
    | None -> BottomSheet.viewHeld { IsOpen = false; OnClose = ignore; Title = ""; Subtitle = None; Footer = None } []

// ============================================
// Regel aus einer Buchung (ledger-hg5m5)
// ============================================

/// Der Hinweis unter dem Formular aus einer Buchung: Regeln wirken nur auf neue Importe (README categorization).
let entryFormHint = "Gilt für neue Importe."

/// Der Titel des Formulars aus einer Buchung.
let entryFormTitle = "Regel anlegen"

let private entryHint () =
    Html.p [
        prop.custom ("data-testid", "rule-form-hint")
        prop.className $"{FontSizes.xs} {Colors.textMuted}"
        prop.text entryFormHint
    ]

/// Das Formular aus einer Buchung, falls es offen ist. `inSheet`: am Handy im Sheet, sonst im Panel oder Dialog am PC.
let entryForm (model: Model) (dispatch: Msg -> unit) (inSheet: bool) : EntryRuleForm option =
    match model.Panel with
    | Some ({ Origin = FromEntry _ } as panel) ->
        if inSheet && panel.PickerOpen then
            Some
                { Title = CategoryPicker.sheetTitle []
                  OnBack = fun () -> dispatch CategoryPickerClosed
                  Body = [ CategoryPicker.sheetContent (pickerPropsFor model panel dispatch) ]
                  Footer = None }
        elif inSheet then
            Some
                { Title = entryFormTitle
                  OnBack = fun () -> dispatch PanelClosed
                  Body =
                    [ Html.div [
                          prop.className "flex flex-col gap-3 py-2"
                          prop.children [ formFields model panel InSheet dispatch; entryHint () ]
                      ] ]
                  Footer = Some [ formButtons panel InSheet dispatch ] }
        else
            Some
                { Title = entryFormTitle
                  OnBack = fun () -> dispatch PanelClosed
                  Body =
                    [ Html.div [ prop.className "px-4 py-3"; prop.children [ formFields model panel InPanel dispatch ] ]
                      Html.div [
                          prop.className $"flex flex-col gap-3 border-t {Borders.default'} px-4 py-3"
                          prop.children [ formButtons panel InPanel dispatch; entryHint () ]
                      ] ]
                  Footer = None }
    | _ -> None

// ============================================
// Tastatur am PC (§7.8)
// ============================================

let private focusOf (target: Browser.Types.HTMLElement) : KeyFocus =
    if isNull (box target) then Elsewhere
    else
        match target.tagName with
        | "TEXTAREA" | "SELECT" -> InTextField
        | "INPUT" ->
            let kind: string = target?``type``
            if kind = "checkbox" || kind = "radio" || kind = "button" then Elsewhere else InTextField
        | _ when target.isContentEditable -> InTextField
        | _ -> Elsewhere

[<ReactComponent>]
let private RulesKeyboard (model: Model) (dispatch: Msg -> unit) =
    let modelRef = React.useRef model
    modelRef.current <- model
    let dispatchRef = React.useRef dispatch
    dispatchRef.current <- dispatch
    React.useEffect (
        (fun () ->
            let handler (e: Browser.Types.Event) =
                let ke = e :?> Browser.Types.KeyboardEvent
                let target = ke.target :?> Browser.Types.HTMLElement
                let modifier = ke.altKey || ke.ctrlKey || ke.metaKey || ke.shiftKey
                match ruleKeyAction modelRef.current (focusOf target) modifier ke.defaultPrevented ke.key with
                | Some action ->
                    ke.preventDefault ()
                    match action with
                    | ShowRule id -> dispatchRef.current (RuleChosen id)
                    | CloseRulePanel -> dispatchRef.current PanelClosed
                    | CloseRulePicker -> dispatchRef.current CategoryPickerClosed
                | None -> ()
            Browser.Dom.document.addEventListener ("keydown", handler)
            { new System.IDisposable with
                member _.Dispose() = Browser.Dom.document.removeEventListener ("keydown", handler) }),
        [||]
    )
    Html.none

/// Die Tastenleiste unter der Liste (Prototyp `.keys`): „↑ ↓ Regel · Esc schließen“.
let private keyBar () =
    Html.div [
        prop.custom ("data-testid", "rules-keys")
        prop.className $"sticky bottom-0 mt-auto flex flex-wrap gap-4 border-t {Borders.subtle} {Backgrounds.void'} px-1 py-2.5 md:mr-6 {FontSizes.xs} {Colors.textMuted}"
        prop.children [
            Html.span [
                prop.className "inline-flex items-center gap-1.5"
                prop.children [ ReviewCard.kbd "↑"; ReviewCard.kbd "↓"; Html.span [ prop.text "Regel" ] ]
            ]
            Html.span [
                prop.className "inline-flex items-center gap-1.5"
                prop.children [ ReviewCard.kbd "Esc"; Html.span [ prop.text "schließen" ] ]
            ]
        ]
    ]

// ============================================
// Teile für das Kassenbuch
// ============================================

/// Untertitel im Kopf, etwa „5 Regeln, 4 aktiv“.
let subtitle (model: Model) : string option =
    match model.Rules with
    | RemoteData.Success rules when not rules.IsEmpty -> Some (summaryText rules)
    | _ -> None

/// Die Hauptaktion im Kopf am PC: „+ Neue Regel“. Am Handy steht sie unter der Liste.
let headerActions (dispatch: Msg -> unit) : ReactElement list =
    [ Button.view {
          Button.defaultProps with
              Text = "Neue Regel"
              Variant = Button.Primary
              Icon = Some (Icons.plus SM Icons.IconColor.Primary)
              ClassName = Some "hidden md:inline-flex"
              OnClick = fun () -> dispatch NewRuleRequested
      } ]

/// Der Inhalt des Tabs: Hinweis, Liste, Panel bzw. Sheet, Tastatur.
let content (model: Model) (dispatch: Msg -> unit) : ReactElement =
    // Ein Formular aus einer Buchung gehört zur Buchung, nicht in den Tab (ledger-hg5m5).
    let model = if (entryFormFor model).IsSome then { model with Panel = None } else model
    let isPanel = Viewport.isInspectorPanelViewport ()
    Html.div [
        prop.custom ("data-testid", "rules-tab")
        prop.className "relative"
        prop.children [
            if isPanel then sidePanel model dispatch else sheet model dispatch
            if isPanel then RulesKeyboard model dispatch
            Html.div [
                // Mit Panel: die Spalte reicht bis an den Fensterrand, die Tastenleiste steht unten (Prototyp `.keys`).
                prop.className (if isPanel then "md:pr-[420px] md:min-h-[calc(100vh-7rem)] flex flex-col" else "")
                prop.children [
                    Html.div [
                        prop.className "flex flex-col gap-3 md:pr-6"
                        prop.children [
                            Html.p [ prop.className $"{FontSizes.sm} {Colors.textSecondary}"; prop.text priorityHint ]
                            match model.Rules with
                            | RemoteData.NotAsked
                            | RemoteData.Loading -> skeleton ()
                            | RemoteData.Failure error ->
                                ErrorDisplay.cardWithTitle "Regeln konnten nicht geladen werden" error (Some (fun () -> dispatch LoadRules))
                            | RemoteData.Success rules when rules.IsEmpty ->
                                Card.emptyState
                                    (Icons.rules XL Icons.Default)
                                    "Keine Regeln"
                                    "Eine Regel kategorisiert neue Umsätze beim Abrufen."
                                    (Some (
                                        Button.view {
                                            Button.defaultProps with
                                                Text = "Neue Regel"
                                                Variant = Button.Secondary
                                                Icon = Some (Icons.plus SM Icons.Default)
                                                OnClick = fun () -> dispatch NewRuleRequested
                                        }
                                    ))
                            | RemoteData.Success rules ->
                                // Neben dem Panel bleiben von 1024 bis 1279 px rund 350 px: zu schmal für sieben Spalten, dort
                                // steht die Karte wie am Handy. Die Tabelle von 768 bis 1023 px (ohne Panel) und ab 1280 px.
                                Html.div [
                                    prop.className "hidden md:block lg:hidden xl:block"
                                    prop.children [ ruleTable model rules dispatch ]
                                ]
                                Html.div [
                                    prop.className "md:hidden lg:block xl:hidden"
                                    prop.children [ ruleCard model rules dispatch ]
                                ]
                                Primitives.Responsive.mobileOnly [ newRuleButtonBlock dispatch ]
                        ]
                    ]
                    if isPanel then keyBar ()
                ]
            ]
        ]
    ]
