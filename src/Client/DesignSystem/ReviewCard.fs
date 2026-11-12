/// Bausteine des Durchgehens (ledger-v6s7c, Prototyp Version 17 `review()`, `.rcard`, `.progress`, `.keys`):
/// Fortschrittsbalken, die Karte (normal und mit gelbem Rand für „braucht dich“), ihre Zeilen und die
/// Tastenleiste am PC. Drei Schriftgrößen je Ansicht (§7.14): 20 px für Payee und Betrag, 16 px am Handy
/// bzw. 14 px am PC für Werte und Knöpfe, 12 px für alles Kleine.
module Client.DesignSystem.ReviewCard

open Feliz
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
        prop.className (
            $"flex flex-col gap-3 px-4 py-[18px] rounded-[14px] border {border} {Backgrounds.dark} "
            + "transition-[transform,opacity] duration-150 touch-pan-y"
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
/// ihn über das sichtbare Wort findet.
let payee (text: string) (ariaLabel: string) (onClick: (unit -> unit) option) : ReactElement =
    let cls = $"{FontSizes.placeTitle} {FontWeights.semibold} {Colors.textPrimary} min-w-0 break-words"
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
let field (testId: string) (label: string) (value: string option) (placeholder: string) (isOpen: bool) (onClick: (unit -> unit) option) : ReactElement =
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
    let cls = "input-field w-full flex items-center gap-3 px-4 py-2 min-h-[52px] text-left"
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

/// „Details“ rechts unten in der Karte: klein im Akzent, am Handy 48 × 48 px Trefferfläche.
let detailsLink (onClick: unit -> unit) : ReactElement =
    Html.div [
        prop.className "flex justify-end"
        prop.children [
            Html.button [
                prop.type' "button"
                prop.custom ("data-testid", "review-details")
                prop.className (
                    $"{FontSizes.xs} {Colors.accent} hover:underline {TouchTargets.mobileMinHeight} min-w-12 md:min-h-0 md:min-w-0 "
                    + "px-1 touch-manipulation"
                )
                prop.onClick (fun _ -> onClick ())
                prop.text "Details"
            ]
        ]
    ]

/// Die Hinweiszeile unter den Knöpfen: 12 px, gedämpft, mittig.
let hint (text: string) : ReactElement =
    Html.p [ prop.custom ("data-testid", "review-hint"); prop.className $"text-center {FontSizes.xs} {Colors.textMuted}"; prop.text text ]

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
