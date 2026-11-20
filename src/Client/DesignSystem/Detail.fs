/// Bausteine des Buchungs-Details im Kassenbuch (ledger-q1hw2, ADR 0053, geordnet nach Prototyp Version 17 in
/// ledger-zfj78): das Seitenpanel am PC, der Kopf mit Payee, die Betragszeile mit Prüfstand, der Prüf-Knopf, die
/// Hinweisleiste mit Grund und Aktion, die Feldzeile mit „alle zeigen“, die Split-Karte, die Knopfzeile, die
/// Herkunft und die aufklappbare Historie (Zeilen aus `HistoryList`, §7.11). Der Inhalt ist für Panel und
/// BottomSheet derselbe; nur die Hülle unterscheidet sich (`panel` hier, `BottomSheet.view` dort).
/// Darstellung ruhig nach ADR 0049: Farbe nur für das, was Aufmerksamkeit braucht (Hinweisleiste, Prüfstand).
module Client.DesignSystem.Detail

open Feliz
open Client.DesignSystem
open Client.DesignSystem.Tokens

// ============================================
// Hülle: Seitenpanel
// ============================================

/// Das Seitenpanel rechts neben einer Liste (§7.6), dieselbe Hülle auf jeder Seite (design-system-gcpws, Prototyp
/// Version 17 `.dpanel`): beginnt unter dem Seitenkopf wie im Kassenbuch seit ledger-cnaz3 (Abweichung (c) vom
/// Prototyp, G12), Rand links, ohne Karte, Breite `Tokens.PanelWidths.panel` (30 % des Fensters, 380 bis 520 px). Es haftet unter der Navbar (`.list-panel` in styles.css, die Seite kann den Abstand oben über
/// `--list-panel-top` setzen), scrollt in sich und liegt rechts in der Spalte, die der Aufrufer `relative` hält und
/// mit einem rechten Innenabstand freihält (`PanelWidths.columnInset`, `columnInsetWide`). Nur ab `md`; am Handy
/// öffnet das Detail als BottomSheet. `hidden md:block` ist nur die Grundsichtbarkeit, den Breakpoint 1024 px (§7.6)
/// setzt der Aufrufer. `testId` benennt den Inhalt für Tests, `data-list-panel` die Hülle für den
/// Tasten-Sweep.
let panelWith (testId: string) (ariaLabel: string) (children: ReactElement list) : ReactElement =
    Html.div [
        prop.className $"hidden md:block absolute inset-y-0 right-0 {PanelWidths.panel}"
        prop.children [
            Html.aside [
                prop.custom ("data-testid", testId)
                prop.custom ("data-list-panel", "")
                prop.ariaLabel ariaLabel
                prop.className (
                    $"list-panel flex {PanelWidths.panel} flex-col overflow-hidden " +
                    $"border-l {Borders.default'} {Backgrounds.dark}"
                )
                prop.children children
            ]
        ]
    ]

/// Das Seitenpanel des Kassenbuchs (Buchung, Auswahl, neue Buchung, Regeln).
let panel (ariaLabel: string) (children: ReactElement list) : ReactElement =
    panelWith "ledger-detail-panel" ariaLabel children

/// Das Panel ohne gewählte Buchung (ledger-cnaz3, §7.6, §7.12, Prototyp Version 17 `.dempty`): ab 1024 px steht
/// es immer da, mittig ein gedämpfter Satz wie „Buchung wählen, um Details zu sehen.“, ohne Aktion.
let emptyPanel (text: string) : ReactElement =
    panel "Detail" [
        Html.div [
            prop.custom ("data-testid", "ledger-panel-empty")
            prop.className $"grid h-full place-items-center p-6 text-center {FontSizes.sm} {Colors.textMuted}"
            prop.text text
        ]
    ]

/// Der scrollende Bereich unter dem Kopf (Hinweisleiste, Felder, Historie).
let scrollBody (children: ReactElement list) : ReactElement =
    Html.div [
        prop.className "min-h-0 flex-1 overflow-y-auto"
        prop.children children
    ]

// ============================================
// Kopf
// ============================================

/// Der Betrag im Kopf: Mono, ruhig neutral, auch ein Eingang (ADR 0087 Punkt 2); gedämpft für eine
/// in YNAB gelöschte Buchung (Lesezustand).
let amount (value: decimal) (currency: string) (muted: bool) : ReactElement =
    let color =
        if muted then Colors.textMuted
        else Colors.textPrimary
    Html.span [
        prop.custom ("data-testid", "detail-amount")
        prop.className $"font-mono font-semibold tabular-nums whitespace-nowrap text-xl md:text-2xl {color}"
        prop.text (Money.formatAmountText (value > 0m) true value currency)
    ]

/// Der Betrag im Kopf als Knopf (ledger-m7mye, ADR 0092, Prototyp Version 17): derselbe Betrag wie
/// `amount`, dahinter der Pfeil `›`; ein Tipp öffnet das Sheet "Betrag". Trefferfläche mindestens
/// 44 px hoch (am Handy 48 px über `TouchTargets.mobileMinHeight`). Der Name nennt Aktion und
/// Betrag ("Betrag bearbeiten, -42,80 €"), damit der Screenreader den Betrag nicht verliert.
/// `onClick = None` sperrt den Knopf vorübergehend (etwa solange ein Aufruf läuft): `aria-disabled`,
/// ohne Wirkung und ohne Hover, der Pfeil gedämpft wie beim gesperrten `pickerArrow` (`opacity-40`).
/// Der Fokus bleibt, deshalb kein `disabled`.
let amountButton (value: decimal) (currency: string) (label: string) (onClick: (unit -> unit) option) : ReactElement =
    let name = $"{label}, {Money.formatAmountText (value > 0m) true value currency}"
    let disabled = onClick.IsNone
    Html.button [
        prop.type'.button
        prop.custom ("data-testid", "detail-amount-edit")
        prop.ariaLabel name
        prop.title name
        prop.custom ("aria-disabled", (if disabled then "true" else "false"))
        prop.className (
            $"-ml-1 inline-flex {TouchTargets.mobileMinHeight} md:min-h-11 items-center gap-2 rounded-lg px-1 " +
            "transition-colors touch-manipulation " +
            (if disabled then "cursor-not-allowed" else "hover:bg-surface-hover")
        )
        prop.onClick (fun e ->
            e.stopPropagation ()
            onClick |> Option.iter (fun click -> click ()))
        prop.children [
            amount value currency false
            Html.span [
                prop.className (Colors.textMuted + (if disabled then " opacity-40" else ""))
                prop.ariaHidden true
                prop.children [ Icons.chevronRight Icons.SM Icons.Default ]
            ]
        ]
    ]

/// Ein gesperrter Betrag im Kopf: der Betrag ohne Knopf, darunter der Grund wie bei der Kategorie
/// (`fieldLockHint`), links bündig unter dem Betrag.
let amountLocked (value: decimal) (currency: string) (muted: bool) (reason: string) : ReactElement =
    Html.div [
        prop.children [
            amount value currency muted
            Html.p [
                prop.custom ("data-testid", "detail-amount-lock-hint")
                prop.className $"mt-1 {ListRoles.muted}"
                prop.text reason
            ]
        ]
    ]

/// Schließen rechts im Kopf; `label` ist der Name für den Screenreader („Detail schließen“, „Aufteilen schließen“,
/// „Formular schließen“), `testId` die Test-Id. `locked` sperrt den Knopf per `aria-disabled` (kein `disabled`, der
/// Fokus bleibt erreichbar): gedämpft, ohne Hover, ein Klick tut nichts (§5 Detail).
let private closeButton (label: string) (testId: string) (locked: bool) (onClose: unit -> unit) : ReactElement =
    Html.button [
        prop.type'.button
        prop.ariaLabel label
        prop.title "Schließen"
        prop.custom ("data-testid", testId)
        if locked then prop.ariaDisabled true
        prop.className (
            $"-mr-2 inline-flex {TouchTargets.mobileSquare} md:h-11 md:w-11 shrink-0 items-center justify-center rounded-xl " +
            $"{Colors.textSecondary} transition-colors touch-manipulation " +
            (if locked then "opacity-40 cursor-not-allowed" else "hover:bg-surface-hover hover:text-text-primary")
        )
        prop.onClick (fun _ -> if not locked then onClose ())
        prop.children [ Icons.x Icons.SM Icons.Default ]
    ]

/// Der Kopf des Details im Panel (ledger-zfj78, Prototyp Version 17 `sh-head`): der Payee als Titel (17 px
/// halbfett, gedämpft ohne Payee), darunter „<Tag> · <Konto>“ gedämpft, rechts Schließen. Im Sheet stellt
/// `BottomSheet.view` denselben Kopf (Titel und Unterzeile). Der Titel trägt `data-testid="detail-title"`.
let titleHead (title: string) (muted: bool) (subtitle: string) (onClose: (unit -> unit) option) : ReactElement =
    Html.div [
        prop.className "flex items-start justify-between gap-3 px-4 pt-3.5 pb-2"
        prop.children [
            Html.div [
                prop.className "min-w-0"
                prop.children [
                    Html.h2 [
                        prop.custom ("data-testid", "detail-title")
                        prop.className (
                            $"break-words {FontSizes.lg} font-semibold "
                            + (if muted then Colors.textMuted else Colors.textPrimary)
                        )
                        prop.text title
                    ]
                    Html.p [
                        prop.custom ("data-testid", "detail-subtitle")
                        prop.className $"mt-0.5 {FontSizes.xs} {Colors.textMuted}"
                        prop.text subtitle
                    ]
                ]
            ]
            match onClose with
            | Some close -> closeButton "Detail schließen" "detail-close" false close
            | None -> ()
        ]
    ]

/// Der Kopf eines Formulars im Panel („Neue Buchung“, ledger-zf6zg): derselbe Kopf wie `titleHead`, ohne
/// Unterzeile und ohne Linie darunter. Titel 17 px halbfett, rechts derselbe Schließen-Knopf mit `closeLabel` als
/// Namen und `data-testid="capture-close"`. `closing = true` (während das Formular speichert) setzt `aria-disabled`.
let formHead (title: string) (closeLabel: string) (closing: bool) (onClose: unit -> unit) : ReactElement =
    Html.div [
        // Ohne Unterzeile steht der Titel mittig neben dem Knopf.
        prop.className "flex items-center justify-between gap-3 px-4 pt-3.5 pb-2"
        prop.children [
            Html.h2 [
                prop.className $"min-w-0 break-words {FontSizes.lg} font-semibold {Colors.textPrimary}"
                prop.text title
            ]
            closeButton closeLabel "capture-close" closing onClose
        ]
    ]

/// Der Kopf über dem Split-Editor: Betrag, Unterzeile und der Schließen-Knopf. Schließen führt zurück ins Detail
/// (ledger-hme9k) und heißt für den Screenreader „Aufteilen schließen“.
let head
    (amountElement: ReactElement)
    (subtitle: string)
    (onClose: (unit -> unit) option)
    : ReactElement =
    Html.div [
        prop.className $"flex items-start justify-between gap-3 px-4 py-4 border-b {Borders.subtle}"
        prop.children [
            Html.div [
                prop.className "min-w-0"
                prop.children [
                    amountElement
                    Html.p [
                        prop.className $"mt-1.5 {FontSizes.xs} {Colors.textMuted}"
                        prop.text subtitle
                    ]
                ]
            ]
            match onClose with
            | Some close -> closeButton "Aufteilen schließen" "detail-close" false close
            | None -> ()
        ]
    ]

// ============================================
// Hinweisleiste mit Grund und Aktion
// ============================================

/// Ton der Hinweisleiste nach Farbrolle (ADR 0087): Gelb braucht dich, Rot Fehler.
type NoticeTone =
    /// Gelb: braucht dich, kein Fehler, auf jeder Seite.
    | NoticeYellow
    | NoticeRed

/// Die Aktion einer Hinweisleiste; `Busy` zeigt den eigenen Lauf und sperrt sie, `Disabled` sperrt sie
/// ohne Spinner, etwa solange ein anderer Aufruf läuft.
type NoticeAction = {
    Label: string
    Busy: bool
    Disabled: bool
    OnClick: unit -> unit
}

type NoticeProps = {
    Tone: NoticeTone
    /// Das Wort im Badge, z. B. "Noch nicht in YNAB".
    Badge: string
    Reason: string
    Action: NoticeAction option
}

/// Die Hinweisleiste direkt unter dem Kopf: ein Badge, der Grund darunter und, wenn eine
/// Aktion wirkt, genau diese eine. Die Fläche ist nur getönt (ADR 0049), die Aufmerksamkeit
/// trägt das Badge.
let notice (props: NoticeProps) : ReactElement =
    let surface, variant =
        match props.Tone with
        | NoticeYellow -> "bg-attention/[0.07]", Badge.Warning
        | NoticeRed -> "bg-error/[0.07]", Badge.Error

    Html.div [
        prop.role "status"
        prop.custom ("data-testid", "detail-notice")
        prop.className $"px-4 py-3 border-b {Borders.subtle} {surface}"
        prop.children [
            Badge.view { Badge.defaultProps with Text = props.Badge; Variant = variant; Size = Badge.Small }
            Html.p [
                prop.className $"mt-2 {FontSizes.xs} {Colors.textSecondary} leading-normal"
                prop.text props.Reason
            ]
            match props.Action with
            | Some action ->
                Html.div [
                    prop.className "mt-3"
                    prop.children [
                        Button.view {
                            Button.defaultProps with
                                Text = action.Label
                                Variant = Button.Secondary
                                Size = Button.Small
                                IsLoading = action.Busy
                                IsDisabled = action.Disabled
                                OnClick = action.OnClick
                        }
                    ]
                ]
            | None -> ()
        ]
    ]

// ============================================
// Felder
// ============================================

/// Eine Feldzeile: Bezeichnung links, Wert rechts, optional ein Knopf dahinter
/// (Filter-Knopf, ab v2 der Pfeil des Kategorie-Pickers). `testId` benennt die Zeile.
let fieldRowWith
    (onClick: (unit -> unit) option)
    (testId: string)
    (label: string)
    (value: ReactElement)
    (trailing: ReactElement option)
    : ReactElement =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className (
            $"flex min-h-14 items-center gap-3 px-4 detail-inset detail-line py-2 border-b {Borders.subtle}"
            + (if onClick.IsSome then " cursor-pointer" else "")
        )
        match onClick with
        | Some click ->
            prop.onClick (fun _ -> click ())
            // Ein offenes Popover schließt bei einem Mausdruck außerhalb. Gehört der Druck zu
            // dieser Zeile, soll der Klick es schalten und nicht erst schließen und neu öffnen.
            prop.onMouseDown (fun e -> e.stopPropagation ())
        | None -> ()
        prop.children [
            Html.span [ prop.className $"shrink-0 {FontSizes.sm} {Colors.textMuted}"; prop.text label ]
            Html.div [
                prop.className "flex min-w-0 flex-1 items-center justify-end gap-1 text-right"
                prop.children [
                    value
                    match trailing with
                    | Some button -> button
                    | None -> ()
                ]
            ]
        ]
    ]

let fieldRow (testId: string) (label: string) (value: ReactElement) (trailing: ReactElement option) : ReactElement =
    fieldRowWith None testId label value trailing

/// Der Pfeil neben einem Feld, der eine Auswahl öffnet (Kategorie-Picker, ledger-sfab0).
/// `lockedHint`: gesperrt, der Knopf ist gedämpft, wirkt nicht und nennt den Grund als
/// Tooltip. Die Trefferfläche ist mindestens 44 px hoch.
let pickerArrow (label: string) (isOpen: bool) (lockedHint: string option) (onClick: unit -> unit) : ReactElement =
    let locked = lockedHint.IsSome
    Html.button [
        prop.type'.button
        prop.title (lockedHint |> Option.defaultValue label)
        prop.ariaLabel (lockedHint |> Option.defaultValue label)
        prop.custom ("aria-disabled", (if locked then "true" else "false"))
        prop.custom ("aria-expanded", (if isOpen then "true" else "false"))
        prop.custom ("data-testid", "detail-category-arrow")
        prop.className (
            $"inline-flex {TouchTargets.mobileSquare} md:h-11 md:w-8 shrink-0 items-center justify-center rounded-lg {Colors.textMuted} transition-colors touch-manipulation "
            + (if locked then "opacity-40 cursor-not-allowed" else "hover:text-text-primary hover:bg-surface-hover")
        )
        prop.onMouseDown (fun e -> e.stopPropagation ())
        prop.onClick (fun e ->
            e.stopPropagation ()
            if not locked then onClick ())
        prop.children [ Icons.chevronRight Icons.SM Icons.Default ]
    ]

/// Der Pfeil `›` neben einem Feld, das eine eigene Auswahl öffnet, die den Inhalt ersetzt (Payee,
/// ledger-fxt09). Gleiche Trefferfläche und Sperre wie `pickerArrow`; `testId` benennt den Knopf.
let forwardArrow (testId: string) (label: string) (lockedHint: string option) (onClick: unit -> unit) : ReactElement =
    let locked = lockedHint.IsSome
    Html.button [
        prop.type'.button
        prop.title (lockedHint |> Option.defaultValue label)
        prop.ariaLabel (lockedHint |> Option.defaultValue label)
        prop.custom ("aria-disabled", (if locked then "true" else "false"))
        prop.custom ("data-testid", testId)
        prop.className (
            $"inline-flex {TouchTargets.mobileSquare} md:h-11 md:w-8 shrink-0 items-center justify-center rounded-lg {Colors.textMuted} transition-colors touch-manipulation "
            + (if locked then "opacity-40 cursor-not-allowed" else "hover:text-text-primary hover:bg-surface-hover")
        )
        prop.onMouseDown (fun e -> e.stopPropagation ())
        prop.onClick (fun e ->
            e.stopPropagation ()
            if not locked then onClick ())
        prop.children [ Icons.chevronRight Icons.SM Icons.Default ]
    ]

/// Ein unsichtbarer Platzhalter in der Breite des Pfeils: Feldzeilen ohne Pfeil (Payee) halten
/// damit ihren Filter-Knopf bündig mit dem der Kategorie-Zeile.
let pickerArrowSpacer () : ReactElement =
    Html.span [ prop.className $"{TouchTargets.mobileSquare} md:h-11 md:w-8 shrink-0"; prop.ariaHidden true ]

/// Der Wert einer Feldzeile als Text; `className` trägt die Farbe (Kategorie) oder
/// `Colors.textMuted` für einen Platzhalter.
let valueText (className: string) (text: string) : ReactElement =
    Html.span [
        prop.className ($"min-w-0 break-words {FontSizes.sm} " + className)
        prop.text text
    ]

/// Ein externer Link im Buchungs-Detail (ledger-rjhwp, geordnet nach Prototyp Version 17 `orderLink` in
/// ledger-zfj78): links der Titel („Bestellung bei Amazon“) klein und gedämpft, darunter die Nummer in Mono, rechts
/// „öffnen“ im Akzent mit Pfeil nach außen. Die ganze Zeile ist der Link und öffnet in einem neuen Tab; am Handy
/// mindestens 52 px hoch wie die Feldzeilen. Dieselbe Form wie `ReviewCard.linkRow` mit dem Rand des Details.
let externalLinkRow (title: string) (detail: string option) (url: string) : ReactElement =
    Html.a [
        prop.key url
        prop.custom ("data-testid", "detail-external-link")
        prop.href url
        prop.target "_blank"
        prop.rel "noopener noreferrer"
        prop.className (
            $"flex min-h-14 w-full items-center justify-between gap-3 border-b {Borders.subtle} px-4 detail-inset detail-line py-2 "
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

/// Ein Angebot unter einer Feldzeile, nach einer Änderung (ledger-hg5m5, „Regel für „Rewe“ anlegen?“): die Frage
/// gedämpft, rechts ein kleiner Zweitknopf, der das Angebot annimmt, und ein ×, das es ausblendet. Steht in Panel
/// und Sheet gleich. `dismissLabel` benennt das ×, nicht „Schließen“: das Sheet hat schon einen Knopf so.
let offerRow
    (testId: string)
    (text: string)
    (acceptLabel: string)
    (onAccept: unit -> unit)
    (dismissLabel: string)
    (onDismiss: unit -> unit)
    : ReactElement =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className $"flex items-center gap-2 px-4 detail-inset detail-line py-2 border-b {Borders.subtle}"
        prop.children [
            Html.p [
                prop.className $"min-w-0 flex-1 {FontSizes.sm} {Colors.textSecondary}"
                prop.text text
            ]
            Button.view {
                Button.defaultProps with
                    Text = acceptLabel
                    Variant = Button.Secondary
                    Size = Button.Small
                    OnClick = onAccept
            }
            Button.view {
                Button.defaultProps with
                    Icon = Some (Icons.x Icons.SM Icons.Default)
                    Variant = Button.Ghost
                    Size = Button.Small
                    AriaLabel = Some dismissLabel
                    Title = Some dismissLabel
                    OnClick = onDismiss
            }
        ]
    ]

/// Ein mehrzeiliges Feld als Block (Skizze ledger-vf5h6 Bild 11 `.memo-stack`, design-system-x045q): oben links die
/// Bezeichnung, rechts die Aktion (`fieldBlockAction`), darunter der ganze Text über die volle Breite, linksbündig und
/// umbrechend, Linie unten wie die Feldzeilen. `onClick`: die ganze Fläche öffnet dasselbe wie die Aktion. Für
/// einzeilige Werte bleibt `fieldRowWith` (Bezeichnung links, Wert rechts).
let fieldBlock
    (onClick: (unit -> unit) option)
    (testId: string)
    (label: string)
    (action: ReactElement)
    (content: ReactElement list)
    : ReactElement =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className (
            $"flex flex-col gap-1 px-4 detail-inset detail-line pb-3 md:pt-3 border-b {Borders.subtle}"
            + (if onClick.IsSome then " cursor-pointer" else "")
        )
        match onClick with
        | Some click ->
            prop.onClick (fun _ -> click ())
            prop.onMouseDown (fun e -> e.stopPropagation ())
        | None -> ()
        prop.children [
            Html.div [
                prop.className $"flex {TouchTargets.mobileMinHeight} md:min-h-0 items-center justify-between gap-3"
                prop.children [
                    Html.span [ prop.className $"shrink-0 {FontSizes.sm} {Colors.textMuted}"; prop.text label ]
                    action
                ]
            ]
            yield! content
        ]
    ]

/// Die Aktion rechts im Kopf eines `fieldBlock` als Text mit Pfeil („Bearbeiten ›“, „hinzufügen ›“), in
/// `text-muted`. `label` ist der Name für Screenreader und enthält den sichtbaren Text. `lockedHint`: gesperrt wie
/// `forwardArrow`, gedämpft, ohne Wirkung, der Grund als Tooltip. Am Handy 48 px Trefferfläche.
let fieldBlockAction
    (testId: string)
    (text: string)
    (label: string)
    (lockedHint: string option)
    (onClick: unit -> unit)
    : ReactElement =
    let locked = lockedHint.IsSome
    Html.button [
        prop.type'.button
        prop.title (lockedHint |> Option.defaultValue label)
        prop.ariaLabel label
        prop.custom ("aria-disabled", (if locked then "true" else "false"))
        prop.custom ("data-testid", testId)
        prop.className (
            $"inline-flex {TouchTargets.mobileMinHeight} min-w-[48px] md:min-h-0 md:min-w-0 shrink-0 items-center justify-end gap-0.5 rounded-lg {FontSizes.sm} {Colors.textMuted} transition-colors touch-manipulation "
            + (if locked then "opacity-40 cursor-not-allowed" else "hover:text-text-primary")
        )
        prop.onMouseDown (fun e -> e.stopPropagation ())
        prop.onClick (fun e ->
            e.stopPropagation ()
            if not locked then onClick ())
        prop.children [
            Html.span [ prop.text text ]
            Icons.chevronRight Icons.SM Icons.Default
        ]
    ]

/// Der Text eines `fieldBlock`: über die volle Breite, linksbündig, umbrechend, ohne Kürzung. `className` trägt die
/// Farbe (gedämpft bei einer gelöschten Buchung).
let blockText (className: string) (text: string) : ReactElement =
    Html.p [
        prop.className ($"min-w-0 break-words text-left {FontSizes.sm} " + className)
        prop.text text
    ]

/// Der Grund einer Sperre im `fieldBlock`, linksbündig unter dem Text.
let blockLockHint (testId: string) (text: string) : ReactElement =
    Html.p [
        prop.custom ("data-testid", testId)
        prop.className $"mt-1 text-left {ListRoles.muted}"
        prop.text text
    ]

/// Der Grund, aus dem der Knopf einer Feldzeile gesperrt ist, als gedämpfter Text unter dem Wert.
let fieldLockHint (testId: string) (text: string) : ReactElement =
    Html.p [
        prop.custom ("data-testid", testId)
        prop.className $"mt-1 text-right {ListRoles.muted}"
        prop.text text
    ]

/// Das Eingabefeld des Memos im Detail (ledger-fa1fp, ADR 0062, Tafeln 5.2 und 7.8): eine Zeile
/// mit der Bezeichnung, darunter das mitwachsende mehrzeilige Feld (`Input.AutoGrowTextarea`, design-system-x045q), der Zähler gegen die Grenze von YNAB, ein Grund
/// bei zu langem Text und die Knöpfe "Speichern" und "Abbrechen". Enter speichert, Esc bricht ab
/// (das Ereignis erreicht das Dokument nicht, das Detail bleibt offen). `Busy`: der Aufruf läuft.
type MemoEditorProps = {
    Value: string
    OnChange: string -> unit
    OnSave: unit -> unit
    OnCancel: unit -> unit
    Busy: bool
    /// Warum Speichern gesperrt ist (zu lang) oder der Befehl scheiterte; `None`: nichts zu melden.
    Problem: string option
    /// Die Grenze von YNAB in Zeichen, für den Zähler.
    MaxLength: int
    /// Speichern ist gesperrt (Problem, oder der Aufruf läuft).
    SaveDisabled: bool
}

let memoEditor (props: MemoEditorProps) : ReactElement =
    let save () = if not props.SaveDisabled then props.OnSave ()
    Html.div [
        prop.custom ("data-testid", "detail-memo-editor")
        prop.className $"flex flex-col gap-2 px-4 detail-inset detail-line py-3 border-b {Borders.subtle}"
        prop.onKeyDown (fun e ->
            if e.key = "Enter" then
                e.preventDefault ()
                e.stopPropagation ()
                save ()
            elif e.key = "Escape" then
                e.stopPropagation ()
                if not props.Busy then props.OnCancel ())
        prop.children [
            Html.label [
                prop.className $"{FontSizes.sm} {Colors.textMuted}"
                prop.text "Memo"
            ]
            // Mehrzeilig, wächst mit (design-system-x045q); Enter der Bildschirmtastatur speichert über `OnEnter`.
            Input.AutoGrowTextarea
                { Value = props.Value
                  OnChange = props.OnChange
                  Placeholder = "Memo"
                  State = (if props.Problem.IsSome then Input.Error "" else Input.Normal)
                  Disabled = props.Busy
                  OnEnter = save
                  AriaLabel = Some "Memo" }
            Html.div [
                prop.className "flex items-center justify-between gap-3"
                prop.children [
                    Html.span [
                        prop.custom ("data-testid", "detail-memo-counter")
                        prop.className $"{Fonts.mono} {ListRoles.muted}"
                        prop.text $"{props.Value.Length} / {props.MaxLength}"
                    ]
                    Html.div [
                        prop.className "flex items-center gap-2"
                        prop.children [
                            // Beide Knöpfe halten den Fokus: der Tipp trifft den Knopf, bevor die Tastatur zugeht
                            // und das Sheet springt (Vorbild „Speichern“ im Sheet „Betrag“, §5 Button).
                            Button.view
                                { Button.defaultProps with
                                    Text = "Abbrechen"
                                    Variant = Button.Ghost
                                    IsDisabled = props.Busy
                                    KeepsFocus = true
                                    OnClick = props.OnCancel
                                    Title = Some "Abbrechen (Esc)" }
                            Button.view
                                { Button.defaultProps with
                                    Text = "Speichern"
                                    Variant = Button.Secondary
                                    IsLoading = props.Busy
                                    IsDisabled = props.SaveDisabled
                                    KeepsFocus = true
                                    OnClick = props.OnSave
                                    Title = Some "Speichern (Enter)" }
                        ]
                    ]
                ]
            ]
            match props.Problem with
            | Some problem ->
                Html.p [
                    prop.custom ("data-testid", "detail-memo-problem")
                    prop.role "alert"
                    prop.className $"{FontSizes.sm} text-error"
                    prop.text problem
                ]
            | None -> ()
        ]
    ]

// ============================================
// Abschnitte, Knopfzeile, Herkunft und Historie (ledger-zfj78)
// ============================================

/// Die Überschrift eines Abschnitts im Detail ("AUFTEILEN" über dem Split-Editor).
let sectionLabel (text: string) : ReactElement =
    Html.div [
        prop.className $"{ListRoles.groupHeader} px-4 pt-4 pb-2"
        prop.text text
    ]


/// Ein Block mit dem Rand der Feldzeilen (`px-4`): Betragszeile, Prüf-Knopf, Knopfzeile, Herkunft, Löschen.
let block (testId: string) (extra: IReactProperty list) (children: ReactElement list) : ReactElement =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className "px-4 detail-inset py-2"
        yield! extra
        prop.children children
    ]

/// Der Prüfstand rechts neben dem Betrag (Prototyp Version 17): „zu prüfen“ in Gelb oder „geprüft“ in Grün, 12 px.
let reviewStatus (needsReview: bool) : ReactElement =
    Html.span [
        prop.custom ("data-testid", "detail-review-status")
        prop.className (
            $"shrink-0 whitespace-nowrap {FontSizes.xs} "
            + (if needsReview then Colors.attention else Colors.success)
        )
        prop.text (if needsReview then "zu prüfen" else "geprüft")
    ]

/// Die Zeile unter dem Kopf: links der Betrag (`amountButton` oder `amountLocked`), rechts der Prüfstand, auf der
/// Grundlinie ausgerichtet wie im Prototyp.
let amountRow (amountElement: ReactElement) (status: ReactElement) : ReactElement =
    Html.div [
        prop.custom ("data-testid", "detail-amount-row")
        prop.className "flex items-baseline justify-between gap-3 px-4 detail-inset pt-3 pb-1"
        prop.children [ amountElement; status ]
    ]

/// Der Knopf „Geprüft“ bzw. „Als ungeprüft markieren“ über die ganze Breite (Prototyp `.btn.block`): `primary` als
/// Hauptaktion bei einer ungeprüften Buchung, sonst neutral mit Rand (`Outline`). `busy`: der Aufruf läuft.
let reviewButton (label: string) (primary: bool) (busy: bool) (onClick: unit -> unit) : ReactElement =
    block "detail-reviewed" [] [
        Button.view {
            Button.defaultProps with
                Text = label
                Variant = (if primary then Button.Primary else Button.Outline)
                IsBusy = busy
                ClassName = Some "w-full"
                OnClick = onClick
        }
    ]

/// „alle zeigen“ hinter einem Wert (Prototyp Version 17, Payee und Kategorie): 12 px im Akzent, setzt einen Filter
/// auf die Liste. Name und Tooltip nennen das volle Ziel („Nur Buchungen von REWE zeigen“). Am Handy 48 px hoch.
let showAllLink (label: string) (onClick: unit -> unit) : ReactElement =
    Html.button [
        prop.type'.button
        prop.title label
        prop.ariaLabel label
        prop.custom ("data-testid", "detail-filter")
        prop.className (
            $"inline-flex {TouchTargets.mobileMinHeight} md:min-h-11 shrink-0 items-center rounded-lg px-1 {FontSizes.xs} {Colors.accent} "
            + "whitespace-nowrap hover:underline touch-manipulation"
        )
        prop.onMouseDown (fun e -> e.stopPropagation ())
        prop.onClick (fun e ->
            e.stopPropagation ()
            onClick ())
        prop.text "alle zeigen"
    ]

/// Die Zeilen eines Splits in einer Karte direkt unter der Kategorie (Prototyp `.card` mit `.kv`, 40 px je Zeile):
/// links Kategorie oder Konto, rechts der Betrag in Mono.
let splitCard (lines: (string * string) list) : ReactElement =
    Html.div [
        prop.className "px-4 detail-inset py-2"
        prop.children [
            Html.div [
                prop.custom ("data-testid", "detail-split-lines")
                prop.className $"rounded-xl border {Borders.subtle} {Backgrounds.dark} px-3 py-1"
                prop.children [
                    for (target, amountText) in lines ->
                        Html.div [
                            prop.key (target + amountText)
                            prop.className $"flex min-h-10 items-center justify-between gap-3 border-b {Borders.subtle} last:border-b-0"
                            prop.children [
                                Truncate.text $"{FontSizes.sm} {Colors.textMuted}" target
                                Html.span [
                                    prop.className $"shrink-0 font-mono tabular-nums {FontSizes.sm} {Colors.textPrimary}"
                                    prop.text amountText
                                ]
                            ]
                        ]
                ]
            ]
        ]
    ]

/// Ein Knopf der Knopfzeile (Prototyp `.btnrow .btn.sm`): neutral mit Rand `border-strong`, ohne Füllung, füllt
/// seine Spalte. `icon` steht vor dem Text (Flagge bei „Lena fragen“). `lockedHint`: gesperrt, gedämpft, ohne Wirkung,
/// der Grund als Tooltip (und als Text unter der Zeile, `fieldLockHint`). `busy`: der Aufruf läuft. Der Fokus bleibt,
/// deshalb `aria-disabled` statt `disabled`.
let rowAction
    (testId: string)
    (label: string)
    (icon: ReactElement option)
    (lockedHint: string option)
    (busy: bool)
    (onClick: unit -> unit)
    : ReactElement =
    let inactive = lockedHint.IsSome || busy
    Html.button [
        prop.type'.button
        prop.title (lockedHint |> Option.defaultValue label)
        prop.custom ("aria-disabled", (if inactive then "true" else "false"))
        prop.custom ("data-testid", testId)
        prop.className (
            $"inline-flex {TouchTargets.mobileMinHeight} md:min-h-9 w-full min-w-0 items-center justify-center gap-1.5 rounded-lg border {Borders.strong} py-1 " +
            $"bg-transparent px-3 {FontSizes.sm} font-medium {Colors.textPrimary} transition-colors touch-manipulation " +
            (if inactive then "opacity-40 cursor-not-allowed" else "hover:bg-surface-hover active:scale-[0.98]")
        )
        prop.onMouseDown (fun e -> e.stopPropagation ())
        prop.onClick (fun e ->
            e.stopPropagation ()
            if not inactive then onClick ())
        prop.children [
            match icon with
            | Some i -> Html.span [ prop.className "inline-flex shrink-0"; prop.ariaHidden true; prop.children [ i ] ]
            | None -> ()
            // Ein langer Text bricht bei schmaler Breite um (320 px, „Rückfrage zurücknehmen“), statt abzuschneiden.
            Html.span [ prop.className "min-w-0 text-center leading-tight"; prop.text label ]
        ]
    ]

/// Die Knopfzeile (Prototyp `.btnrow`): Knöpfe gleich breit nebeneinander, darunter die Gründe gesperrter Knöpfe.
/// `extra`: Eigenschaften der Hülle, etwa `Badge.lenaFlagScope` für die Farbe der Flagge.
/// `hints`: (testId, Grund) je gesperrtem Knopf, gedämpft unter der Zeile.
/// `notes`: weitere Zeilen unter den Gründen, etwa die Bestätigung unter „Lena fragen“ (`Feedback.fieldNoteWith`,
/// ledger-enjex), je mit demselben Abstand.
let actionRowWith
    (extra: IReactProperty list)
    (actions: ReactElement list)
    (hints: (string * string) list)
    (notes: ReactElement list)
    : ReactElement =
    block "detail-actions" extra [
        yield Html.div [
            prop.className "grid auto-cols-fr grid-flow-col gap-2"
            prop.children actions
        ]
        for (testId, text) in hints do
            yield Html.p [
                prop.key testId
                prop.custom ("data-testid", testId)
                prop.className $"mt-1.5 {ListRoles.muted}"
                prop.text text
            ]
        for note in notes do
            yield Html.div [ prop.className "mt-1.5"; prop.children [ note ] ]
    ]

/// Die Knopfzeile ohne weitere Zeilen (`actionRowWith`).
let actionRow (extra: IReactProperty list) (actions: ReactElement list) (hints: (string * string) list) : ReactElement =
    actionRowWith extra actions hints []

/// Die Herkunft als kleine gedämpfte Zeile („Herkunft: YNAB-Spiegel“, Prototyp Version 17).
let originLine (text: string) : ReactElement =
    Html.p [
        prop.custom ("data-testid", "detail-origin")
        prop.className $"px-4 detail-inset py-2 {FontSizes.xs} {Colors.textMuted}"
        prop.text text
    ]

/// Die Historie im Detail: eine aufklappbare Zeile „Historie (n)“ (`Button.disclosureSectionHead`), darunter der
/// Block, zu `hidden`. `content`: die Einträge (`HistoryList.list`), der Leerzustand oder Laden und Fehler.
let historySection (label: string) (isOpen: bool) (onToggle: unit -> unit) (content: ReactElement) : ReactElement =
    Html.div [
        prop.className "px-4 detail-inset detail-history"
        prop.children [
            Button.disclosureSectionHead "detail-history-toggle" "detail-history" label isOpen onToggle
            Html.div [
                prop.id "detail-history"
                prop.custom ("data-testid", "detail-history")
                prop.hidden (not isOpen)
                prop.children [ content ]
            ]
        ]
    ]
