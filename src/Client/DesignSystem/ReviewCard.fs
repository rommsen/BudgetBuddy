/// Bausteine des Durchgehens (ledger-v6s7c, Prototyp Version 17 `review()`, `.rcard`, `.progress`, `.keys`):
/// Fortschrittsbalken, die Karte (normal und mit gelbem Rand für „braucht dich“), ihre Zeilen und die
/// Tastenleiste am PC. Drei Schriftgrößen je Ansicht (§7.14): 20 px für Payee und Betrag, 16 px am Handy
/// bzw. 14 px am PC für Werte und Knöpfe, 12 px für alles Kleine.
module Client.DesignSystem.ReviewCard

open Feliz
open Fable.Core.JsInterop
open Client.DesignSystem.Tokens

/// Der Fortschritt (Prototyp `.progress`): 4 px hoch, Spur in der Rahmenfarbe, Füllung im Akzent.
/// `percent` von 0 bis 100, `valueText` liest der Screenreader statt der Prozentzahl („4 von 10“).
let progress (percent: float) (valueText: string option) : ReactElement =
    Html.div [
        prop.custom ("data-testid", "review-progress")
        prop.role "progressbar"
        prop.ariaLabel "Fortschritt"
        prop.custom ("aria-valuemin", "0")
        prop.custom ("aria-valuemax", "100")
        prop.custom ("aria-valuenow", string (int percent))
        match valueText with
        | Some text -> prop.custom ("aria-valuetext", text)
        | None -> ()
        prop.className $"h-1 w-full overflow-hidden {Radius.full} bg-border-default"
        prop.children [
            Html.div [
                prop.className $"h-full {Backgrounds.accent} transition-[width] duration-200"
                prop.style [ style.width (length.percent percent) ]
            ]
        ]
    ]

/// Die Karte (Prototyp `.card.rcard`): Radius 14 px wie `Card.allDone`, Innenabstand 18 × 16 px, 12 px
/// zwischen den Zeilen. `attention`
/// zeichnet den Rand in Gelb (Duplikat-Karte, „braucht dich“). `extra` sind weitere Eigenschaften
/// (Wischgesten, Test-Id).
let card (attention: bool) (extra: IReactProperty list) (children: ReactElement list) : ReactElement =
    let border = if attention then "border-attention" else Borders.default'
    Html.div [
        yield! extra
        prop.className $"flex flex-col gap-3 px-4 py-[18px] rounded-[14px] border {border} {Backgrounds.dark} touch-pan-y"
        prop.children children
    ]

/// Die Karte am Handy (ledger-s2cg3, Skizze ledger-vf5h6 `.card`): wie `card`, sie füllt den Platz bis zu den
/// Knöpfen (`flex-1`, `min-h-0`), auch bei kurzem Inhalt. Ist der Inhalt länger, scrollt er in der Karte; die
/// Knöpfe darunter bleiben fest.
let cardFill (attention: bool) (extra: IReactProperty list) (children: ReactElement list) : ReactElement =
    let border = if attention then "border-attention" else Borders.default'
    Html.div [
        yield! extra
        prop.className (
            $"relative flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto overscroll-contain px-4 py-[18px] rounded-[14px] border {border} "
            + $"{Backgrounds.dark} touch-pan-y"
        )
        prop.children children
    ]

/// Kleine gedämpfte Zeile: „Heute · Girokonto · Comdirect-Import“.
let meta (text: string) : ReactElement =
    Html.p [ prop.custom ("data-testid", "review-meta"); prop.className $"{FontSizes.xs} {Colors.textMuted}"; prop.text text ]

/// Kleine Zeile in `text-secondary` (Prototyp `.small.sec`): Tag, Konto und Herkunft auf der Duplikat-Karte.
let metaSecondary (text: string) : ReactElement =
    Html.p [ prop.custom ("data-testid", "review-meta"); prop.className $"{FontSizes.xs} {Colors.textSecondary}"; prop.text text ]

/// Kleine Zeile in Gelb: „Möglicherweise doppelt · noch nicht in YNAB“.
let metaAttention (text: string) : ReactElement =
    Html.p [ prop.className $"{FontSizes.xs} {Colors.attention}"; prop.text text ]

/// Der Payee groß und halbfett. Mit `onClick` ein Knopf (Payee ändern), sonst Text. Der Name des Knopfs
/// ist `ariaLabel` mit dem sichtbaren Payee dahinter („Payee ändern: Amazon“), damit Sprachsteuerung
/// ihn über das sichtbare Wort findet. `missing`: statt eines Payees steht der Platzhalter („ohne Payee“),
/// gedämpft wie in Liste und Detail (ledger-pkrwd).
let payee (text: string) (missing: bool) (ariaLabel: string) (onClick: (unit -> unit) option) : ReactElement =
    let tone = if missing then Colors.textMuted else Colors.textPrimary
    let cls = $"{FontSizes.placeTitle} {FontWeights.semibold} {tone} min-w-0 break-words"
    match onClick with
    | Some click ->
        Html.button [
            prop.type' "button"
            prop.custom ("data-testid", "review-payee")
            prop.ariaLabel $"{ariaLabel}: {text}"
            prop.className $"{cls} -mx-1 px-1 text-left {Radius.sm} {TouchTargets.mobileMinHeight} md:min-h-0 hover:bg-surface-hover touch-manipulation"
            prop.onClick (fun _ -> click ())
            prop.text text
        ]
    | None -> Html.p [ prop.custom ("data-testid", "review-payee"); prop.className cls; prop.text text ]

/// Der Betrag groß in Mono, halbfett.
let amount (text: string) : ReactElement =
    Html.p [
        prop.custom ("data-testid", "review-amount")
        prop.className $"{FontSizes.placeTitle} {FontWeights.semibold} {Fonts.mono} tabular-nums {Colors.textPrimary}"
        prop.text text
    ]

/// Das Feld „Kategorie“ der Karte (Bauart `Input.pickerField`): Beschriftung klein, darunter der Wert.
/// Fehlt der Wert, steht `placeholder` in Gelb („braucht dich“). Ohne `onClick` (eine aufgeteilte Buchung)
/// ist es kein Knopf und hat keinen Pfeil.
let rec field (testId: string) (label: string) (value: string option) (placeholder: string) (isOpen: bool) (onClick: (unit -> unit) option) : ReactElement =
    fieldNeeding false testId label value placeholder isOpen onClick

/// Wie `field`; mit `need` tragen Rand und Ring die Farbe `attention` (Skizze `.field.need`): ein Wisch nach rechts
/// ohne Kategorie zeigt, wo sie fehlt.
and fieldNeeding (need: bool) (testId: string) (label: string) (value: string option) (placeholder: string) (isOpen: bool) (onClick: (unit -> unit) option) : ReactElement =
    let content =
        [ Html.span [
              prop.className "flex min-w-0 flex-1 flex-col"
              prop.children [
                  Html.span [ prop.className $"{FontSizes.xs} {Colors.textMuted}"; prop.text label ]
                  match value with
                  | Some v -> Truncate.text $"text-base md:text-sm {Colors.textPrimary}" v
                  | None -> Html.span [ prop.className $"text-base md:text-sm {Colors.attention}"; prop.text placeholder ]
              ]
          ]
          if onClick.IsSome then Icons.chevronRight Icons.SM Icons.Default ]
    let cls =
        if need then "input-field input-need w-full flex items-center gap-3 px-4 py-2 min-h-[52px] text-left"
        else "input-field w-full flex items-center gap-3 px-4 py-2 min-h-[52px] text-left"
    match onClick with
    | Some click ->
        Html.button [
            prop.type' "button"
            prop.custom ("data-testid", testId)
            prop.custom ("aria-haspopup", "dialog")
            prop.custom ("aria-expanded", (if isOpen then "true" else "false"))
            prop.className $"{cls} touch-manipulation"
            prop.onClick (fun _ -> click ())
            prop.children content
        ]
    | None -> Html.div [ prop.custom ("data-testid", testId); prop.className cls; prop.children content ]

/// Ein kleiner Text unter einem Feld, gedämpft („aus Bestellnummer“, „Vorgeschlagen“).
let caption (testId: string) (text: string) : ReactElement =
    Html.p [ prop.custom ("data-testid", testId); prop.className $"{FontSizes.xs} {Colors.textMuted}"; prop.text text ]

/// Eine Zeile Schlüssel und Wert mit Pfeil (Prototyp `.kv`): 52 px hoch, Linie unten. Ohne Wert steht
/// gedämpft `placeholder`.
let kvRow (testId: string) (key: string) (value: string option) (placeholder: string) (onClick: unit -> unit) : ReactElement =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.className (
            $"flex w-full min-h-[52px] items-center justify-between gap-3 border-b {Borders.subtle} text-left "
            + "touch-manipulation hover:bg-surface-hover"
        )
        prop.onClick (fun _ -> onClick ())
        prop.children [
            Html.span [ prop.className $"shrink-0 {FontSizes.xs} {Colors.textMuted}"; prop.text key ]
            Html.span [
                prop.className "flex min-w-0 items-center gap-1"
                prop.children [
                    match value with
                    | Some v -> Truncate.text $"text-base md:text-sm {Colors.textPrimary}" v
                    | None -> Html.span [ prop.className $"text-base md:text-sm {Colors.textMuted}"; prop.text placeholder ]
                    Icons.chevronRight Icons.SM Icons.Default
                ]
            ]
        ]
    ]

/// Eine Link-Zeile (Prototyp `orderLink`): Titel klein, darunter optional die Nummer in Mono, rechts
/// „öffnen“ im Akzent mit Pfeil nach außen. Öffnet in einem neuen Tab.
let linkRow (title: string) (detail: string option) (url: string) : ReactElement =
    Html.a [
        prop.custom ("data-testid", "review-link")
        prop.href url
        prop.target "_blank"
        prop.rel "noopener noreferrer"
        prop.className (
            $"flex w-full min-h-[52px] items-center justify-between gap-3 border-b {Borders.subtle} "
            + "touch-manipulation hover:bg-surface-hover"
        )
        prop.children [
            Html.span [
                prop.className "flex min-w-0 flex-col"
                prop.children [
                    Html.span [ prop.className $"{FontSizes.xs} {Colors.textMuted}"; prop.text title ]
                    match detail with
                    | Some d -> Truncate.text $"{FontSizes.xs} {Fonts.mono} {Colors.textMuted}" d
                    | None -> ()
                ]
            ]
            Html.span [
                prop.className $"inline-flex shrink-0 items-center gap-1 text-base md:text-sm {Colors.accent}"
                prop.children [ Html.span [ prop.text "öffnen" ]; Icons.externalLink Icons.XS Icons.Accent ]
            ]
        ]
    ]

/// Ein Hinweis in der Karte (Prototyp `.note`): Rand, Radius 12 px, Text klein in `text-secondary`. Ein
/// Name darin steht mit `noteName` hervorgehoben (Prototyp `.note b`).
let note (testId: string) (children: ReactElement list) : ReactElement =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className $"flex items-start gap-2 rounded-[12px] border {Borders.default'} px-3 py-2.5 {FontSizes.xs} {Colors.textSecondary}"
        prop.children children
    ]

/// Ein Name im Hinweis (Prototyp `.note b`): `text-primary`, halbfett.
let noteName (text: string) : ReactElement =
    Html.b [ prop.className $"{FontWeights.semibold} {Colors.textPrimary}"; prop.text text ]

/// Abschnittskopf in der Karte (Prototyp `.sec-h`): Mono, gesperrt, Großbuchstaben, gedämpft.
let sectionHead (text: string) : ReactElement =
    Html.p [
        prop.className $"pt-2 {FontSizes.xs} {Fonts.mono} uppercase tracking-[0.14em] {Colors.textMuted}"
        prop.text text
    ]

/// Die Zeile der schon vorhandenen Buchung auf erhöhtem Grund, Radius 12 px: Payee, darunter Tag, Konto und
/// Kategorie, rechts der Betrag.
let matchRow (payeeText: string) (metaText: string) (amountText: string) : ReactElement =
    Html.div [
        prop.custom ("data-testid", "review-match")
        prop.className $"flex items-center justify-between gap-3 rounded-[12px] {Backgrounds.surface} px-4 py-3"
        prop.children [
            Html.div [
                prop.className "flex min-w-0 flex-col"
                prop.children [
                    Truncate.text $"text-base md:text-sm {FontWeights.medium} {Colors.textPrimary}" payeeText
                    Html.span [ prop.custom ("data-testid", "review-match-meta"); prop.className $"{FontSizes.xs} {Colors.textSecondary}"; prop.text metaText ]
                ]
            ]
            Html.span [
                prop.custom ("data-testid", "review-match-amount")
                prop.className $"shrink-0 text-base md:text-sm {Fonts.mono} tabular-nums {Colors.textPrimary}"
                prop.text amountText
            ]
        ]
    ]

/// Ein Textlink unten in der Karte: klein im Akzent, am Handy 48 × 48 px Trefferfläche.
let private cardLink (testId: string) (label: string) (onClick: unit -> unit) : ReactElement =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.className (
            $"{FontSizes.xs} {Colors.accent} hover:underline {TouchTargets.mobileMinHeight} min-w-12 md:min-h-0 md:min-w-0 "
            + "px-1 touch-manipulation"
        )
        prop.onClick (fun _ -> onClick ())
        prop.text label
    ]

/// „Details“ rechts unten in der Karte, am unteren Rand (`mt-auto`), wenn die Karte den Platz füllt.
let detailsLink (onClick: unit -> unit) : ReactElement =
    Html.div [
        prop.className "mt-auto flex justify-end"
        prop.children [ cardLink "review-details" "Details" onClick ]
    ]

/// Die letzte Zeile der Karte mit einem zweiten Textlink links und „Details“ rechts (ledger-hg5m5: „Regel anlegen“),
/// am unteren Rand der Karte (`mt-auto`).
/// Eine eigene Zeile schöbe am Handy „Passt, weiter“ aus dem Bild.
let linksWithDetails (testId: string) (label: string) (onClick: unit -> unit) (onDetails: unit -> unit) : ReactElement =
    Html.div [
        prop.className "mt-auto flex items-center justify-between gap-3"
        prop.children [ cardLink testId label onClick; cardLink "review-details" "Details" onDetails ]
    ]

/// Was in der Zeile unter dem Hauptknopf steht.
type FootLine =
    /// Leer (Duplikat-Karte): die Zeile hält trotzdem ihren Platz.
    | FootEmpty
    /// Der Wisch-Hinweis in `text-secondary`.
    | FootHint of string
    /// „braucht dich“ in `attention`, halbfett („Erst Kategorie wählen“ während eines Wischs ohne Ziel).
    | FootNeed of string

/// Die Zeile unter dem Hauptknopf (Skizze ledger-vf5h6 `.ph-foot`): feste Höhe 54 px samt Abstand, einzeilig, mittig,
/// 12 px. Die Höhe ist dieselbe, was auch darin steht: die Knöpfe darüber springen nie.
let foot (line: FootLine) : ReactElement =
    let cls =
        match line with
        | FootNeed _ -> $"flex h-[54px] shrink-0 items-center justify-center overflow-hidden whitespace-nowrap px-4 pt-1.5 pb-3.5 text-center {FontSizes.xs} {FontWeights.semibold} {Colors.attention}"
        | FootEmpty
        | FootHint _ -> $"flex h-[54px] shrink-0 items-center justify-center overflow-hidden whitespace-nowrap px-4 pt-1.5 pb-3.5 text-center {FontSizes.xs} {Colors.textSecondary}"
    Html.p [
        prop.custom ("data-testid", "review-foot")
        prop.className cls
        match line with
        | FootHint text
        | FootNeed text -> prop.text text
        | FootEmpty -> ()
    ]

// ============================================
// Wischen mit Fläche (ledger-s2cg3, Skizze ledger-vf5h6, Option A1)
// ============================================

/// Was die Fläche unter der Karte zeigt.
type SwipeSurface =
    /// In Ruhe unsichtbar (auch beim Flug von „Lena fragen“).
    | SurfaceRest
    /// Nach rechts: Haken über „Passt“, grün getönt; eingerastet kräftiger.
    | SurfaceConfirm of armed: bool
    /// Nach links: Uhr über „Später“, neutral; eingerastet eine Stufe heller.
    | SurfaceSkip of armed: bool
    /// Nach rechts ohne Kategorie: nur ein gelbes „!“, rastet nie ein.
    | SurfaceNoTarget

/// Die Fläche unter der Karte: über die ganze Karte, Radius wie die Karte, je Richtung ein Symbol im Kreis über dem
/// Wort, Spalte 56 px mit 16 px Rand zur Kante. Nur das Symbol der gewischten Richtung ist sichtbar.
let swipeUnder (surface: SwipeSurface) : ReactElement =
    let state =
        match surface with
        | SurfaceRest -> "review-swipe-under"
        | SurfaceConfirm false -> "review-swipe-under is-confirm"
        | SurfaceConfirm true -> "review-swipe-under is-confirm is-armed"
        | SurfaceSkip false -> "review-swipe-under is-skip"
        | SurfaceSkip true -> "review-swipe-under is-skip is-armed"
        | SurfaceNoTarget -> "review-swipe-under is-no-target"
    let side (cls: string) (symbol: ReactElement) (word: string option) =
        Html.span [
            prop.className cls
            prop.children [
                Html.span [ prop.className "review-swipe-sym"; prop.children [ symbol ] ]
                match word with
                | Some w -> Html.span [ prop.className FontSizes.xs; prop.text w ]
                | None -> ()
            ]
        ]
    Html.div [
        prop.custom ("data-testid", "review-swipe-under")
        prop.ariaHidden true
        prop.className state
        prop.children [
            match surface with
            | SurfaceNoTarget ->
                side "review-swipe-side is-left" (Html.span [ prop.className FontWeights.bold; prop.text "!" ]) None
            | _ -> side "review-swipe-side is-left" (Icons.check Icons.SM Icons.Inherit) (Some "Passt")
            side "review-swipe-side is-right" (Icons.clock Icons.SM Icons.Inherit) (Some "Später")
        ]
    ]

/// Wie eine Karte die Seite verlässt, wenn ein Knopf oder eine Taste sie bewegt.
type SwipeLeave =
    /// Nach links mit eingerasteter neutraler Fläche („Später“, S).
    | LeaveSkip
    /// Nach links ohne Fläche („Lena fragen“, L).
    | LeaveQuiet

/// Die Eigenschaften der Bühne. Die Entscheidung über den Weg kommt als reine Funktion von außen.
type SwipeStageProps =
    { /// Wischen erlaubt (Handy, normale Karte).
      Enabled: bool
      /// Wechselt mit der Karte: die neue Karte blendet sich ein.
      CardKey: string
      /// „Passt“ läuft: die Karte steht rechts draußen, die eingerastete Fläche bleibt.
      Holding: bool
      /// Wo die Karte bei diesem Weg des Fingers steht (`dx` nach rechts positiv).
      OffsetOf: float -> float
      /// Was die Fläche bei diesem Weg zeigt (`dx`, `dy`).
      SurfaceOf: float -> float -> SwipeSurface
      /// Der Finger ist weg, nach einem waagrechten Wisch (`dx`, `dy`). Rechts eingerastet sofort (der Aufruf beginnt
      /// beim Flug), links eingerastet nach dem Flug, sonst nach dem Zurückfedern ohne Wirkung.
      OnRelease: float -> float -> unit
      /// Die Fläche wechselt (für „Erst Kategorie wählen“ unter dem Knopf).
      OnSurface: SwipeSurface -> unit
      /// Hier legt die Bühne ab, wie Knöpfe und Tasten die Karte hinausfliegen lassen.
      Leave: IRefValue<(SwipeLeave -> (unit -> unit) -> unit) option>
      Card: ReactElement }

[<Literal>]
let private ClaimSlopPx = 12.0

/// Dauer von Flug und Zurückfedern in ms (`--sf-duration-base`).
[<Literal>]
let private MoveMs = 200

let private prefersReducedMotion () : bool =
    let mq: obj = Browser.Dom.window?matchMedia ("(prefers-reduced-motion: reduce)")
    unbox<bool> (mq?matches)

/// Die Bühne des Durchgehens am Handy (Skizze `.ph-stage`, `bindSwipe`, `leave`): die Fläche unter der Karte, die Karte
/// folgt dem Finger. Die Geste gehört der Karte erst nach 12 px überwiegend waagrechtem Weg; davor und bei senkrechtem
/// Weg scrollt der Browser (Pointer-Ereignisse mit `touch-action: pan-y`, nur Touch). Ein Wisch löst keinen Tipp
/// darunter aus. Unter der Grenze federt die Karte zurück, darüber fliegt sie hinaus. Ohne Animation
/// (`prefers-reduced-motion`) springt sie, Fläche und Einrasten erscheinen trotzdem.
[<ReactComponent>]
let SwipeStage (props: SwipeStageProps) =
    let surface, setSurface = React.useState SurfaceRest
    let surfaceRef = React.useRef SurfaceRest
    let wrapRef = React.useRef<Browser.Types.HTMLElement option> None
    let propsRef = React.useRef props
    propsRef.current <- props
    let startX = React.useRef 0.0
    let startY = React.useRef 0.0
    let active = React.useRef false
    let claimed = React.useRef false
    let suppressClick = React.useRef false
    /// Die Karte ist draußen (Flug oder „Passt“ läuft) und wartet auf die nächste Karte oder das Zurückfedern.
    let outside = React.useRef false
    let timer = React.useRef<float option> None

    let paint (next: SwipeSurface) =
        if surfaceRef.current <> next then
            surfaceRef.current <- next
            setSurface next
            propsRef.current.OnSurface next
    let clearTimer () =
        match timer.current with
        | Some t -> Browser.Dom.window.clearTimeout t; timer.current <- None
        | None -> ()
    let wait (ms: int) (action: unit -> unit) =
        clearTimer ()
        timer.current <- Some (Browser.Dom.window.setTimeout ((fun () -> timer.current <- None; action ()), ms))
    /// Nach einer Bewegung; ohne Animation sofort.
    let after (ms: int) (action: unit -> unit) =
        if prefersReducedMotion () then
            clearTimer ()
            action ()
        else wait ms action
    let setTransform (transition: string) (transform: string) =
        match wrapRef.current with
        | Some el ->
            let style: obj = el?style
            style?transition <- transition
            style?transform <- transform
        | None -> ()
    let springBack () =
        outside.current <- false
        setTransform "transform var(--sf-duration-base) var(--sf-spring-out, cubic-bezier(0.2, 0, 0, 1))" "none"
        after MoveMs (fun () -> paint SurfaceRest)
    let fly (toRight: bool) =
        outside.current <- true
        let width = wrapRef.current |> Option.map (fun el -> unbox<float> el?offsetWidth) |> Option.defaultValue 400.0
        let x = if toRight then width + 60.0 else -(width + 60.0)
        setTransform "transform var(--sf-duration-base) ease-in" $"translateX({x}px)"
    /// Nach dem Flug: Steht dieselbe Karte noch da und läuft kein „Passt“, war der Schritt gesperrt; sie federt zurück.
    let settleAfter (key: string) =
        // Auch ohne Animation mit Pause: das Modell braucht einen Durchlauf, bis „Passt“ läuft oder die Karte wechselt.
        wait 60 (fun () ->
            let p = propsRef.current
            if outside.current && p.CardKey = key && not p.Holding then springBack ())
    let leave (kind: SwipeLeave) (action: unit -> unit) =
        let key = propsRef.current.CardKey
        paint (match kind with LeaveSkip -> SurfaceSkip true | LeaveQuiet -> SurfaceRest)
        fly false
        after MoveMs (fun () ->
            action ()
            settleAfter key)

    React.useEffect (
        (fun () ->
            props.Leave.current <- Some leave
            { new System.IDisposable with
                member _.Dispose() =
                    clearTimer ()
                    props.Leave.current <- None }),
        [||]
    )
    // Eine neue Karte: sie steht an ihrem Platz und blendet sich ein (Skizze `.card.enter`).
    React.useEffect (
        (fun () ->
            match wrapRef.current with
            | Some el when outside.current || surfaceRef.current <> SurfaceRest ->
                clearTimer ()
                outside.current <- false
                setTransform "none" "none"
                paint SurfaceRest
                el.classList.remove "review-swipe-enter"
                el?offsetWidth |> ignore
                el.classList.add "review-swipe-enter"
            | _ -> ()),
        [| box props.CardKey |]
    )
    // „Passt“ über Knopf oder Enter: die Karte fliegt nach rechts mit eingerasteter Fläche; scheitert der Prüfvermerk,
    // federt sie zurück.
    React.useEffect (
        (fun () ->
            if props.Holding && not outside.current then
                paint (SurfaceConfirm true)
                fly true
            elif not props.Holding && outside.current && timer.current.IsNone then
                springBack ()),
        [| box props.Holding |]
    )

    let gesture : IReactProperty list =
        if not props.Enabled then []
        else
            [ prop.custom (
                  "onClickCapture",
                  fun (e: Browser.Types.MouseEvent) ->
                      if suppressClick.current then
                          suppressClick.current <- false
                          e.stopPropagation ()
                          e.preventDefault ()
              )
              prop.onPointerDown (fun e ->
                  if e.pointerType = "touch" && not outside.current then
                      clearTimer ()
                      startX.current <- e.clientX
                      startY.current <- e.clientY
                      active.current <- true
                      claimed.current <- false
                      suppressClick.current <- false
                      match wrapRef.current with
                      | Some el -> el.classList.remove "review-swipe-enter"
                      | None -> ())
              prop.onPointerMove (fun e ->
                  if active.current then
                      let dx = e.clientX - startX.current
                      let dy = e.clientY - startY.current
                      if not claimed.current then
                          if abs dx > ClaimSlopPx && abs dx > abs dy then
                              claimed.current <- true
                              suppressClick.current <- true
                              e.currentTarget?setPointerCapture (e.pointerId) |> ignore
                          elif abs dy > ClaimSlopPx then
                              active.current <- false
                      if claimed.current then
                          e.preventDefault ()
                          let next = propsRef.current.SurfaceOf dx dy
                          let wasArmed = match surfaceRef.current with SurfaceConfirm a | SurfaceSkip a -> a | _ -> false
                          let isArmed = match next with SurfaceConfirm a | SurfaceSkip a -> a | _ -> false
                          if isArmed && not wasArmed then Viewport.vibrate 10
                          setTransform "none" $"translateX({propsRef.current.OffsetOf dx}px)"
                          paint next)
              prop.onPointerUp (fun e ->
                  if active.current && claimed.current then
                      active.current <- false
                      claimed.current <- false
                      Browser.Dom.window.setTimeout ((fun () -> suppressClick.current <- false), 50) |> ignore
                      let dx = e.clientX - startX.current
                      let dy = e.clientY - startY.current
                      let key = propsRef.current.CardKey
                      match propsRef.current.SurfaceOf dx dy with
                      | SurfaceConfirm true ->
                          fly true
                          propsRef.current.OnRelease dx dy
                          after MoveMs (fun () -> settleAfter key)
                      | SurfaceSkip true ->
                          fly false
                          after MoveMs (fun () ->
                              propsRef.current.OnRelease dx dy
                              settleAfter key)
                      | _ ->
                          springBack ()
                          propsRef.current.OnRelease dx dy
                  else
                      active.current <- false)
              prop.onPointerCancel (fun _ ->
                  if claimed.current then springBack ()
                  active.current <- false
                  claimed.current <- false) ]

    Html.div [
        prop.custom ("data-testid", "review-swipe-stage")
        prop.className "relative flex min-h-0 flex-1 flex-col"
        prop.children [
            swipeUnder surface
            Html.div [
                yield! gesture
                prop.ref (fun (el: Browser.Types.Element) -> wrapRef.current <- (if isNull el then None else Some (el :?> Browser.Types.HTMLElement)))
                prop.className "review-swipe-card"
                prop.children [ props.Card ]
            ]
        ]
    ]

/// Die Bühne als ruhiges Beispiel für `/styleguide`: Fläche im gegebenen Zustand, die Karte um `offset` verschoben.
let swipeExample (surface: SwipeSurface) (offset: float) (cardElement: ReactElement) : ReactElement =
    Html.div [
        prop.className "relative flex h-[220px] flex-col overflow-hidden"
        prop.children [
            swipeUnder surface
            Html.div [
                prop.className "review-swipe-card"
                prop.style [ style.transform.translateX (length.px offset) ]
                prop.children [ cardElement ]
            ]
        ]
    ]

/// Ein Tastenkästchen (Prototyp `.kbd`): Mono, Rand, Radius 5 px.
let kbd (key: string) : ReactElement =
    Html.kbd [
        prop.className $"rounded-[5px] border {Borders.default'} px-1.5 {Fonts.mono} {FontSizes.xs} {Colors.textSecondary}"
        prop.text key
    ]

/// Die Tastenleiste am PC (Prototyp `.keys`): Paare aus Taste und Wort, mittig, Linie oben.
let keyBar (keys: (string * string) list) : ReactElement =
    Html.div [
        prop.custom ("data-testid", "review-keys")
        prop.className $"flex flex-wrap justify-center gap-4 border-t {Borders.subtle} px-6 py-2 {FontSizes.xs} {Colors.textMuted}"
        prop.children [
            for (key, label) in keys ->
                Html.span [
                    prop.key key
                    prop.className "inline-flex items-center gap-1.5"
                    prop.children [ kbd key; Html.span [ prop.text label ] ]
                ]
        ]
    ]

let private skeletonBar = $"block {Radius.sm} {Backgrounds.surface} animate-pulse"

/// Platzhalter der Seite, solange Schlange oder Karte laden (§6, Skeleton statt Spinner): Fortschritt ohne
/// Füllung, die Karte mit grauen Balken in der Höhe ihrer Zeilen (Meta, Payee, Betrag, Feld „Kategorie“,
/// Memo) und darunter die drei Knopfflächen (zwei nebeneinander, 52 px, und der Hauptknopf, 54 px).
let skeleton () : ReactElement =
    Html.div [
        prop.custom ("data-testid", "review-skeleton")
        prop.ariaLabel "Lade Buchungen"
        prop.ariaBusy true
        prop.className "flex flex-col gap-3"
        prop.children [
            Html.div [ prop.className $"h-1 w-full {Radius.full} bg-border-default" ]
            Html.div [
                prop.className $"flex flex-col gap-3 px-4 py-[18px] rounded-[14px] border {Borders.default'} {Backgrounds.dark}"
                prop.children [
                    Html.span [ prop.className $"{skeletonBar} h-3 w-40" ]
                    Html.span [ prop.className $"{skeletonBar} h-5 w-32" ]
                    Html.span [ prop.className $"{skeletonBar} h-5 w-24" ]
                    Html.span [ prop.className $"{skeletonBar} h-[52px] w-full" ]
                    Html.span [ prop.className $"{skeletonBar} h-[52px] w-full" ]
                ]
            ]
            Html.div [
                prop.className "grid grid-cols-2 gap-2"
                prop.children [
                    Html.span [ prop.className $"{skeletonBar} h-[52px]" ]
                    Html.span [ prop.className $"{skeletonBar} h-[52px]" ]
                ]
            ]
            Html.span [ prop.className $"{skeletonBar} h-[54px] w-full" ]
        ]
    ]
