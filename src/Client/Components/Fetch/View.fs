module Components.Fetch.View

open Feliz
open Client.DesignSystem
open Components.Fetch.Types

// Das Sheet „Abrufen“ (Prototyp Version 17, Sheet `import`): am Handy ein `BottomSheet`, ab 1024 px ein
// Dialog (`Modal`), jeweils über der aktuellen Seite (ADR 0043, `Viewport.isInspectorPanelViewport`).

let private title = "Abrufen"
let private subtitle = "Comdirect"

let private footerButtons (secondaryText: string) (onSecondary: unit -> unit) (primary: (string * (unit -> unit) * bool) option) =
    Html.div [
        prop.className "grid grid-cols-2 gap-3 w-full"
        prop.children [
            Button.view
                { Button.defaultProps with
                    Text = secondaryText
                    Variant = Button.Secondary
                    ClassName = Some "w-full"
                    OnClick = onSecondary }
            match primary with
            | Some (text, onPrimary, busy) ->
                Button.view
                    { Button.defaultProps with
                        Text = text
                        Variant = Button.Primary
                        IsBusy = busy
                        ClassName = Some "w-full"
                        OnClick = onPrimary }
            | None -> ()
        ]
    ]

/// Inhalt und Fuß des Sheets je Zustand.
let private content (model: Model) (daysToFetch: int) (dispatch: Msg -> unit) : ReactElement list * ReactElement option =
    match sheetScreen model with
    | TanScreen ->
        [ Html.div [
              prop.custom ("data-testid", "fetch-tan")
              prop.className "flex flex-col gap-4"
              prop.children [
                  ErrorDisplay.noticeWithLead "Push-TAN bestätigen." "Öffne die Comdirect-App und gib den Zugriff frei."
                  Html.p [
                      prop.className "text-sm text-text-secondary"
                      prop.text (tanExplanation daysToFetch)
                  ]
              ]
          ] ],
        Some (
            footerButtons "Abbrechen" (fun () -> dispatch CancelSync) (Some ("Ich habe bestätigt", (fun () -> dispatch ConfirmTan), model.IsTanConfirming))
        )
    | RunningScreen ->
        [ Card.sheetMessage "fetch-running" false "Rufe ab…" "Umsätze werden geladen und mit Regeln kategorisiert." ],
        None
    | DoneScreen counts ->
        [ Card.sheetMessage "fetch-done" (counts.NewEntries > 0) (resultTitle counts) (resultSentence counts) ],
        Some (
            footerButtons "Später" (fun () -> dispatch Closed) (Some ("Jetzt durchgehen", (fun () -> dispatch ReviewRequested), false))
        )
    | ErrorScreen message ->
        [ Html.div [
              prop.custom ("data-testid", "fetch-error")
              prop.children [ ErrorDisplay.card message None ]
          ] ],
        Some (footerButtons "Schließen" (fun () -> dispatch Closed) None)

let view (model: Model) (daysToFetch: int) (dispatch: Msg -> unit) : ReactElement =
    if not model.IsOpen then
        Html.none
    else
        let body, footer = content model daysToFetch dispatch
        let close () = dispatch Closed
        if Viewport.isInspectorPanelViewport () then
            Modal.view
                { Modal.defaultProps with
                    IsOpen = true
                    OnClose = close
                    Size = Modal.Large
                    Title = Some title
                    Subtitle = Some subtitle }
                [ Modal.body body
                  match footer with
                  | Some f -> Modal.footer [ f ]
                  | None -> () ]
        else
            BottomSheet.view
                { IsOpen = true
                  OnClose = close
                  Title = title
                  Subtitle = Some subtitle
                  Footer = footer |> Option.map List.singleton }
                body
