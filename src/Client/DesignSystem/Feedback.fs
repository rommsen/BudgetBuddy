module Client.DesignSystem.Feedback

// Rückmeldung am Handy ohne Toast (Regel R3, ADR 0098, Styleguide §7.13, design-system-ytgnc).
//
// Jede Rückmeldung hat einen festen Platz: (1) am Ort (`fieldNote`, `flash`, `removedRow`), (2) bei offenem Sheet
// eine Zeile über dem Hauptknopf (`sheetLine`), (3) sonst die Meldungsleiste über der Navigation (`bar`). Immer nur
// eine Meldung, ohne ✕ und ohne Zeitlimit. Das Modul kennt keine App-Typen: welche Meldung gerade steht, wo die
// Leiste am Rand sitzt und wann das Leuchten endet, entscheidet die App (design-system-gkded).

open Feliz
open Client.DesignSystem.Tokens

/// Ton der Meldung: ok (grünes ✓), Warnung (gelbes !), Fehler (rotes !), läuft (ohne Zeichen, Text gedämpft),
/// Info (ohne Zeichen, Text normal; BB kennt sie heute als `ToastInfo`).
[<RequireQualifiedAccess>]
type Tone =
    | Ok
    | Warning
    | Error
    | Running
    | Info

/// Der Knopf einer Meldung: „Rückgängig“, „Erneut“, „Erneut senden“, „Neu laden“.
type Act = { Label: string; OnClick: unit -> unit }

/// Eine Meldung: `Action` ist die obere Zeile (was passiert ist, nie gekürzt), `Detail` die untere (Name oder Grund).
type Message = { Tone: Tone; Action: string; Detail: string option; Act: Act option }

/// Ab welcher Breite „+ Buchung“ aus der Leiste verschwindet, weil der Seitenkopf den Knopf trägt: das Kassenbuch
/// ab 1024 px (`FromLg`), die Übersicht ab 768 px (`FromMd`), wie heute der schwebende Knopf.
[<RequireQualifiedAccess>]
type AddBreakpoint =
    | FromMd
    | FromLg

/// „+ Buchung“ in der Leiste. `Label` steht hinter dem Plus („Buchung“), `AriaLabel` bleibt auch in der runden Form.
/// `Quiet` wie `Button.fab`: ruhig, wenn die Ansicht schon eine Hauptaktion hat (§7.14).
type Add =
    { TestId: string
      Label: string
      AriaLabel: string
      Quiet: bool
      OnClick: unit -> unit
      HideFrom: AddBreakpoint }

/// Form von „+ Buchung“: breit mit Text, solange keine Meldung steht, sonst rund nur mit „+“.
[<RequireQualifiedAccess>]
type AddShape =
    | Wide
    | Round

/// Wo die Leiste steht. `Docked`: fest am unteren Rand unter 1024 px, über der Navigation unten. Die App setzt die
/// Lage über `--feedback-bar-bottom` und `--feedback-bar-left` (styles.css, `.feedback-bar-docked`), ohne Angabe
/// über der Navigation unten. `Preview`: im Fluss der Seite (Styleguide), auf jeder Breite.
[<RequireQualifiedAccess>]
type BarPlacement =
    | Docked
    | Preview

// ============================================
// Reine Entscheidungen
// ============================================

/// Die Leiste steht nur mit Meldung oder mit „+ Buchung“.
let barVisible (message: Message option) (hasAdd: bool) : bool = message.IsSome || hasAdd

/// Mit Meldung wird „+ Buchung“ rund, ohne Meldung ist es breit.
let addShape (message: Message option) : AddShape =
    match message with
    | Some _ -> AddShape.Round
    | None -> AddShape.Wide

/// Was eine Meldung zeigt, ohne sie zu rendern.
type Parts =
    {
        /// Zeichen vor dem Text und seine Farbe; Läuft und Info ohne Zeichen.
        Mark: (string * string) option
        /// Farbe der oberen Zeile.
        ActionColor: string
        /// Bricht auch der Grund um (Fehler und Warnung), statt auf einer Zeile gekürzt zu werden.
        DetailWraps: bool
        /// Der Knopf der Meldung steht nur, wenn sie einen hat.
        ShowsAct: bool
        /// Fehler werden sofort vorgelesen (`role="alert"`).
        IsAlert: bool
    }

/// Farbe der oberen Zeile je Ton.
let private actionColor (tone: Tone) =
    match tone with
    | Tone.Ok -> Colors.textPrimary
    | Tone.Warning -> Colors.attention
    | Tone.Error -> Colors.error
    | Tone.Running -> Colors.textSecondary
    | Tone.Info -> Colors.textPrimary

/// Farbe des Zeichens je Ton (Läuft und Info haben keins).
let private markColor (tone: Tone) =
    match tone with
    | Tone.Ok -> Colors.success
    | Tone.Warning -> Colors.attention
    | Tone.Error -> Colors.error
    | Tone.Running
    | Tone.Info -> ""

/// Zeichen je Ton.
let private markSymbol (tone: Tone) =
    match tone with
    | Tone.Ok -> Some "✓"
    | Tone.Warning
    | Tone.Error -> Some "!"
    | Tone.Running
    | Tone.Info -> None

let parts (message: Message) : Parts =
    { Mark = markSymbol message.Tone |> Option.map (fun symbol -> symbol, markColor message.Tone)
      ActionColor = actionColor message.Tone
      DetailWraps =
        (match message.Tone with
         | Tone.Error
         | Tone.Warning -> true
         | _ -> false)
      ShowsAct = message.Act.IsSome
      IsAlert = message.Tone = Tone.Error }

// ============================================
// Bausteine
// ============================================

/// Klasse: ein Feld, eine Zeile oder ein Wert leuchtet kurz (Rand und Ring im Akzent, rund 0,7 s, dann zurück).
/// Ohne Bewegung steht der Ring, solange die Klasse sitzt; die App nimmt sie nach der Zeit wieder ab. Auf der Hülle
/// eines Felds (`Input.pickerField`) leuchtet das Feld selbst.
let flash = "feedback-flash"

/// Farbe des Knopfs: im Akzent, in der getönten Zeile im Sheet in `text-primary`.
[<RequireQualifiedAccess>]
type private ActColor =
    | Accent
    | Primary

let private actColorClass (color: ActColor) =
    match color with
    | ActColor.Accent -> Colors.accent
    | ActColor.Primary -> Colors.textPrimary

/// Knopf einer Meldung, 48 px Trefferfläche.
let private actButton (color: ActColor) (act: Act) =
    Html.button [
        prop.type' "button"
        prop.custom ("data-feedback-action", "true")
        prop.className (
            $"flex-none inline-flex items-center justify-center {TouchTargets.minSize} px-2 {FontSizes.compact} "
            + $"{FontWeights.semibold} {actColorClass color} whitespace-nowrap touch-manipulation"
        )
        prop.onClick (fun _ -> act.OnClick ())
        prop.text act.Label
    ]

let private markSpan (tone: Tone) =
    match markSymbol tone with
    | Some symbol ->
        Html.span [
            prop.className $"flex-none {FontSizes.body} {markColor tone}"
            prop.ariaHidden true
            prop.text symbol
        ]
    | None -> Html.none

/// Schlüssel einer Meldung: eine andere Meldung baut das Element neu, damit sie wieder einblendet.
let private messageKey (message: Message) =
    let detail = defaultArg message.Detail ""
    $"{message.Tone}|{message.Action}|{detail}"

/// Die Meldung in der Leiste: Zeichen, oben die Aktion (bricht um), darunter Name oder Grund, rechts der Knopf.
let private barMessage (message: Message) =
    let p = parts message
    Html.div [
        prop.key (messageKey message)
        prop.custom ("data-testid", "feedback-message")
        prop.custom ("data-tone", (string message.Tone).ToLowerInvariant())
        if p.IsAlert then prop.role "alert"
        prop.className $"feedback-in flex min-w-0 flex-1 items-center gap-2 {FontSizes.compact}"
        prop.children [
            markSpan message.Tone
            Html.div [
                prop.className "flex min-w-0 flex-1 flex-col py-2 leading-snug"
                prop.children [
                    Html.span [
                        prop.custom ("data-testid", "feedback-action")
                        prop.className $"{FontWeights.semibold} {actionColor message.Tone} break-words"
                        prop.text message.Action
                    ]
                    match message.Detail with
                    | Some detail when p.DetailWraps ->
                        Html.span [
                            prop.custom ("data-testid", "feedback-detail")
                            prop.className $"{FontSizes.xs} {Colors.textSecondary} line-clamp-3 break-words"
                            prop.title detail
                            prop.text detail
                        ]
                    | Some detail ->
                        Html.span (
                            prop.custom ("data-testid", "feedback-detail")
                            :: Truncate.textProps $"block {FontSizes.xs} {Colors.textSecondary}" detail
                        )
                    | None -> Html.none
                ]
            ]
            match message.Act with
            | Some act when p.ShowsAct -> actButton ActColor.Accent act
            | _ -> Html.none
        ]
    ]

let private addHideClass (placement: BarPlacement) (add: Add) =
    match placement, add.HideFrom with
    | BarPlacement.Preview, _ -> ""
    | BarPlacement.Docked, AddBreakpoint.FromMd -> "md:hidden"
    | BarPlacement.Docked, AddBreakpoint.FromLg -> "lg:hidden"

let private addFill (quiet: bool) =
    if quiet then $"{Backgrounds.surface} border {Borders.accent} {Colors.accent}"
    else $"{Backgrounds.accent} {Colors.onAccent} {Colors.onAccentSvg} {Glows.accent}"

let private addShapeClass (shape: AddShape) =
    match shape with
    | AddShape.Wide -> "gap-1.5 px-[18px]"
    | AddShape.Round -> "w-[48px] justify-center"

/// „+ Buchung“: breit mit Text ohne Meldung, rund mit nur „+“, sobald eine Meldung steht.
let private addButton (placement: BarPlacement) (shape: AddShape) (add: Add) =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", add.TestId)
        prop.custom ("data-quiet", (if add.Quiet then "true" else "false"))
        prop.custom ("data-shape", (match shape with AddShape.Wide -> "wide" | AddShape.Round -> "round"))
        prop.ariaLabel add.AriaLabel
        prop.className (
            $"flex-none inline-flex items-center h-[48px] {Radius.full} {FontSizes.body} {FontWeights.semibold} "
            + $"touch-manipulation transition-transform active:scale-[0.98] {addShapeClass shape} {addFill add.Quiet} "
            + addHideClass placement add
        )
        prop.onClick (fun _ -> add.OnClick ())
        prop.children [
            Icons.plus (match shape with AddShape.Wide -> Icons.SM | AddShape.Round -> Icons.MD) (if add.Quiet then Icons.Accent else Icons.Default)
            match shape with
            | AddShape.Wide -> Html.span [ prop.text add.Label ]
            | AddShape.Round -> Html.none
        ]
    ]

let private placementClass (placement: BarPlacement) =
    match placement with
    | BarPlacement.Docked -> $"feedback-bar-docked fixed {ZIndex.fixed'} lg:hidden"
    | BarPlacement.Preview -> "relative w-full"

/// Die Meldungsleiste über der Navigation (unter 1024 px): mindestens 64 px hoch, wächst mit einer umbrechenden
/// Aktion. Links die Meldung, rechts „+ Buchung“, falls die Seite den Knopf hat. Ohne Meldung und ohne Knopf steht
/// sie nicht. Ebene `ZIndex.fixed'` (30): ein offenes Sheet deckt die Leiste.
let bar (placement: BarPlacement) (message: Message option) (add: Add option) : ReactElement =
    if not (barVisible message add.IsSome) then
        Html.none
    else
        Html.div [
            prop.custom ("data-testid", "feedback-bar")
            prop.className (
                $"{placementClass placement} flex items-center gap-2 min-h-[64px] pl-4 pr-3 "
                + $"{Backgrounds.void'} border-t {Borders.subtle}"
            )
            prop.children [
                // Der Platz der Meldung bleibt stehen, damit `aria-live` die neue Meldung vorliest.
                Html.div [
                    prop.className "flex min-w-0 flex-1 items-center"
                    prop.custom ("aria-live", "polite")
                    prop.children [
                        match message with
                        | Some m -> barMessage m
                        | None -> Html.none
                    ]
                ]
                match add with
                | Some a -> addButton placement (addShape message) a
                | None -> Html.none
            ]
        ]

/// Die Zeile im Sheet über dem Hauptknopf (Platz 2): eine Zeile, mindestens 36 px hoch, die Aktion halbfett und
/// gekürzt (sie ist hier zugleich der Name, „50,00 € nach Reise“). Fehler und Warnung stehen auf getönter Fläche
/// und brechen um, der Knopf dort in `text-primary`.
let sheetLine (message: Message) : ReactElement =
    let p = parts message
    if p.DetailWraps then
        let surface = if message.Tone = Tone.Error then "bg-error/10" else Backgrounds.attentionSubtle
        Html.div [
            prop.custom ("data-testid", "feedback-sheet-line")
            prop.custom ("data-tone", (string message.Tone).ToLowerInvariant())
            if p.IsAlert then prop.role "alert" else prop.custom ("aria-live", "polite")
            prop.className $"feedback-in flex flex-wrap items-center gap-x-2 gap-y-0.5 {Radius.md} {surface} py-1.5 pl-2.5 pr-1.5 {FontSizes.compact} {actionColor message.Tone}"
            prop.children [
                Html.span [
                    prop.className "min-w-0 flex-[1_1_170px] break-words"
                    prop.children [
                        Html.span [ prop.className FontWeights.semibold; prop.text message.Action ]
                        match message.Detail with
                        | Some detail -> Html.span [ prop.text $" {detail}" ]
                        | None -> Html.none
                    ]
                ]
                match message.Act with
                | Some act -> actButton ActColor.Primary act
                | None -> Html.none
            ]
        ]
    else
        Html.div [
            prop.custom ("data-testid", "feedback-sheet-line")
            prop.custom ("data-tone", (string message.Tone).ToLowerInvariant())
            prop.custom ("aria-live", "polite")
            prop.className $"feedback-in flex items-center gap-1.5 min-h-[36px] {FontSizes.compact}"
            prop.children [
                markSpan message.Tone
                Html.span (Truncate.textProps $"flex-1 {FontWeights.semibold} {actionColor message.Tone}" message.Action)
                match message.Detail with
                | Some detail -> Html.span (Truncate.textProps $"flex-[0_1_auto] {Colors.textSecondary}" detail)
                | None -> Html.none
                match message.Act with
                | Some act -> actButton ActColor.Accent act
                | None -> Html.none
            ]
        ]

/// Ein Zustand der Seite als Hinweis am Ort (ADR 0098: „ein Zustand der Seite ist keine Meldung“, budgeting-gtrgk,
/// Skizze ledger-vf5h6 `.mk-hint`): gelb getönte Fläche, „! <Text>“ in Gelb, darunter kleiner die Gründe, rechts eine
/// Textaktion im Akzent („Neu laden“). Er bleibt, bis die App ihn wegnimmt; kein ✕, kein Zeitlimit. `running`: die
/// Aktion läuft, sie steht gedämpft mit `aria-busy` da und ein Tipp tut nichts.
let stateHint (testId: string) (text: string) (reasons: string list) (act: Act option) (running: bool) : ReactElement =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.role "status"
        prop.className (
            $"flex flex-wrap items-center gap-x-2 gap-y-0.5 {Radius.md} {Backgrounds.attentionSubtle} "
            + $"py-1.5 pl-2.5 pr-1.5 {FontSizes.compact} {Colors.attention}"
        )
        prop.children [
            Html.div [
                prop.className "flex min-w-0 flex-[1_1_180px] flex-col"
                prop.children [
                    Html.span [ prop.className "break-words"; prop.text $"! {text}" ]
                    for reason in reasons do
                        Html.span [
                            prop.custom ("data-testid", $"{testId}-reason")
                            prop.className $"{FontSizes.xs} {Colors.textSecondary} line-clamp-2 break-words"
                            prop.title reason
                            prop.text reason
                        ]
                ]
            ]
            match act with
            | Some a ->
                Html.button [
                    prop.type' "button"
                    prop.custom ("data-feedback-action", "true")
                    prop.custom ("aria-busy", running)
                    prop.custom ("aria-disabled", running)
                    prop.className (
                        $"flex-none inline-flex items-center justify-center {TouchTargets.minSize} px-2 {FontSizes.compact} "
                        + $"{FontWeights.semibold} {Colors.accent} whitespace-nowrap touch-manipulation"
                        + (if running then " opacity-60 cursor-wait" else "")
                    )
                    prop.onClick (fun _ -> if not running then a.OnClick ())
                    prop.text a.Label
                ]
            | None -> Html.none
        ]
    ]

/// Farbe des Texts unter einem Feld: gedämpft für ok und Info, gelb für die Warnung, rot für den Fehler.
let private fieldNoteColor (tone: Tone) =
    match tone with
    | Tone.Warning -> Colors.attention
    | Tone.Error -> Colors.error
    | Tone.Ok
    | Tone.Running
    | Tone.Info -> Colors.textMuted

/// Die Rückmeldung unter einem Feld (Platz 1) mit Ton (ledger-p5y9s): ok mit grünem ✓ („✓ Geändert · Rückgängig“),
/// Warnung gelb mit !, Fehler rot mit ! (Fehler am Feld, §5). Text und Knopf stehen in einer umbrechenden Zeile, der
/// Knopf hat unter 1024 px 48 px Trefferfläche. Info und Läuft zeigen kein Zeichen und den Text gedämpft wie `fieldNote`.
let fieldNoteWith (tone: Tone) (text: string) (act: Act option) : ReactElement =
    Html.div [
        prop.custom ("data-testid", "feedback-field-note")
        prop.custom ("data-tone", (string tone).ToLowerInvariant())
        if tone = Tone.Error then prop.role "alert" else prop.custom ("aria-live", "polite")
        prop.className $"flex flex-wrap items-center gap-x-2 gap-y-0.5 {FontSizes.xs} {fieldNoteColor tone}"
        prop.children [
            match markSymbol tone with
            | Some symbol -> Html.span [ prop.className $"flex-none {markColor tone}"; prop.ariaHidden true; prop.text symbol ]
            | None -> Html.none
            Html.span [ prop.className "min-w-0 break-words"; prop.text text ]
            match act with
            | Some a ->
                Html.button [
                    prop.type' "button"
                    prop.custom ("data-feedback-action", "true")
                    prop.className (
                        $"inline-flex items-center {TouchTargets.sheetMinHeight} px-1 {FontSizes.xs} {FontWeights.semibold} "
                        + $"{Colors.accent} whitespace-nowrap touch-manipulation"
                    )
                    prop.onClick (fun _ -> a.OnClick ())
                    prop.text a.Label
                ]
            | None -> Html.none
        ]
    ]

/// Die Bestätigung unter einem Feld (Platz 1): klein, gedämpft, Text und Knopf in einer umbrechenden Zeile
/// („Bahn · 84,20 € verfügbar · Rückgängig“), ohne Zeichen. Der Knopf hat unter 1024 px 48 px Trefferfläche.
let fieldNote (text: string) (act: Act option) : ReactElement = fieldNoteWith Tone.Info text act

/// Eine Bestätigung in der Erfolgsrolle (Platz 1, „✓ Gespeichert“): klein und halbfett, im Kopf einer Karte an der
/// Stelle der Statusanzeige oder unter einem Feld, das beim Ändern speichert. Ohne Knopf, bis zur nächsten Aktion.
let saved (text: string) : ReactElement =
    Html.span [
        prop.custom ("data-testid", "feedback-saved")
        prop.className $"{FontSizes.xs} {FontWeights.semibold} {Colors.success} whitespace-nowrap"
        prop.text text
    ]

/// Wie weit das Gelöschte ist. `Removing`: das Löschen läuft („wird gelöscht …“, ohne Knopf, damit die Liste nicht
/// springt, wenn die Antwort kommt). `Removed`: gelöscht, mit „Rückgängig“. `Restoring`: das Zurückholen läuft.
/// `RestoreFailed`: rot, darunter der Grund, rechts „Erneut“ (Fehler am selben Platz, R3).
[<RequireQualifiedAccess>]
type RemovedPhase =
    | Removing
    | Removed of undo: Act
    | Restoring
    | RestoreFailed of reason: string * retry: Act

/// Was hinter dem Namen steht; es wird nie gekürzt, nur der Name.
let removedRowText (phase: RemovedPhase) : string =
    match phase with
    | RemovedPhase.Removing -> "wird gelöscht …"
    | RemovedPhase.Removed _ -> "gelöscht"
    | RemovedPhase.Restoring -> "wird wiederhergestellt …"
    | RemovedPhase.RestoreFailed _ -> "nicht wiederhergestellt"

/// Was in einer Liste gelöscht wurde, hinterlässt an seinem Platz diese Zeile bis zur nächsten Aktion, in jeder Phase
/// gleich hoch: „„Regel drei“ gelöscht · Rückgängig“. Der Name wird mit „…“ gekürzt, der Rest und der Knopf nie.
/// Scheitert das Zurückholen, wird die Zeile rot (Rand und Text in der Fehlerrolle), der Grund bricht darunter um.
let private removedRowWith (title: ReactElement) (phase: RemovedPhase) : ReactElement =
    let failed, act =
        match phase with
        | RemovedPhase.Removed undo -> false, Some undo
        | RemovedPhase.RestoreFailed (_, retry) -> true, Some retry
        | RemovedPhase.Removing
        | RemovedPhase.Restoring -> false, None
    let tone = if failed then $"{Borders.error} {Colors.error}" else $"{Borders.default'} {Colors.textSecondary}"
    Html.div [
        prop.custom ("data-testid", "feedback-removed-row")
        prop.custom ("data-phase", (match phase with
                                    | RemovedPhase.Removing -> "removing"
                                    | RemovedPhase.Removed _ -> "removed"
                                    | RemovedPhase.Restoring -> "restoring"
                                    | RemovedPhase.RestoreFailed _ -> "failed"))
        if failed then prop.role "alert" else prop.custom ("aria-live", "polite")
        prop.className (
            $"flex items-center justify-between gap-2 min-h-[48px] lg:min-h-[44px] border border-dashed {tone} "
            + $"{Radius.lg} pl-3 pr-1.5 {FontSizes.compact}"
        )
        prop.children [
            Html.span [
                prop.className "flex min-w-0 flex-col py-1.5"
                prop.children [
                    Html.span [
                        prop.className "flex min-w-0 items-baseline gap-1"
                        prop.children [
                            title
                            Html.span [ prop.className "flex-none"; prop.text (removedRowText phase) ]
                        ]
                    ]
                    match phase with
                    | RemovedPhase.RestoreFailed (reason, _) ->
                        Html.span [
                            prop.custom ("data-testid", "feedback-removed-reason")
                            prop.className $"{FontSizes.xs} {Colors.textSecondary} break-words"
                            prop.text reason
                        ]
                    | _ -> Html.none
                ]
            ]
            match act with
            | Some a -> actButton ActColor.Accent a
            | None -> Html.none
        ]
    ]

let removedRowIn (name: string) (phase: RemovedPhase) : ReactElement =
    removedRowWith (Truncate.text "" $"„{name}“") phase

/// Dieselbe Zeile ohne Namen, etwa „Buchung gelöscht“ für eine Buchung ohne Payee (ledger-k68sd): das Wort davor wird
/// nicht gekürzt und steht ohne Anführungszeichen.
let removedRowUnnamed (noun: string) (phase: RemovedPhase) : ReactElement =
    removedRowWith (Html.span [ prop.className "flex-none"; prop.text noun ]) phase

/// Die Zeile „gelöscht“ mit „Rückgängig“ (`removedRowIn` in der Phase `Removed`).
let removedRow (name: string) (undo: Act) : ReactElement = removedRowIn name (RemovedPhase.Removed undo)
