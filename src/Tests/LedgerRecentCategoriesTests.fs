module LedgerRecentCategoriesTests

// ledger-d4msf: Abschnitt "Zuletzt verwendet" im Kategorie-Picker des Kassenbuchs. Die Liste ist
// dieselbe wie im Kategorie-Filter (ledger-v9rq3). Fixtures tragen das Kürzel d4msf.

open System
open System.IO
open Expecto
open Shared.Domain
open Client.DesignSystem
open Client.DesignSystem.CategoryPicker
open Components.Ledger.Types

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private read (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

let private guidOf (n: int) = Guid.Parse $"d4d4d4d4-0000-7000-8000-0000000004{n:D2}"
let private idOf (n: int) = YnabCategoryId (guidOf n)

let private cat (n: int) (hidden: bool) : YnabCategory =
    { Id = idOf n
      Name = $"Kategorie {n}"
      GroupName = "Gruppe"
      Available = None
      Hidden = hidden }

let private categories = [ for n in 1..8 -> cat n (n = 8) ]

[<Tests>]
let tests =
    testList "Ledger Zuletzt verwendet im Picker (d4msf)" [

        testCase "höchstens fünf, neueste zuerst, ohne Dubletten" <| fun () ->
            let recent = [ idOf 3; idOf 1; idOf 3; idOf 2; idOf 4; idOf 5; idOf 6; idOf 7 ]
            let result = recentCategories categories None recent |> List.map (fun c -> c.Id)
            Expect.equal result [ idOf 3; idOf 1; idOf 2; idOf 4; idOf 5 ] "Reihenfolge der Liste, fünf Stück"

        testCase "unbekannte, versteckte und aktuelle Kategorien fehlen" <| fun () ->
            let recent = [ idOf 99; idOf 8; idOf 2; idOf 1 ]
            let result = recentCategories categories (Some (idOf 2)) recent |> List.map (fun c -> c.Id)
            Expect.equal result [ idOf 1 ] "nur die bekannte, sichtbare, nicht aktuelle"

        testCase "eine Kategorie steht zugleich unter Vorgeschlagen und Zuletzt verwendet" <| fun () ->
            let l = layoutForRecent PickerReady "" None [ idOf 1 ] [ idOf 1; idOf 2 ] categories
            Expect.equal (l.Suggested |> List.map (fun c -> c.Id)) [ idOf 1 ] "Vorgeschlagen"
            Expect.equal (l.Recent |> List.map (fun c -> c.Id)) [ idOf 1; idOf 2 ] "Zuletzt verwendet"

        testCase "ohne bisherige Kategorien fehlt der Abschnitt, bei Suche ebenfalls" <| fun () ->
            Expect.isEmpty (layoutForRecent PickerReady "" None [] [] categories).Recent "leer"
            Expect.isEmpty (layoutForRecent PickerReady "Kat" None [] [ idOf 1 ] categories).Recent "Suche blendet aus"

        testCase "Zeilen des Abschnitts liegen für die Pfeiltasten hinter Vorgeschlagen" <| fun () ->
            let l = layoutForRecent PickerReady "" None [ idOf 1 ] [ idOf 2 ] categories
            let rows = navigableRows false l |> List.choose (function Category c -> Some c.Id | _ -> None)
            Expect.equal (List.take 2 rows) [ idOf 1; idOf 2 ] "erst Vorgeschlagen, dann Zuletzt verwendet"

        testCase "Alle Kategorien als Kopf, wenn nur Zuletzt verwendet darüber steht" <| fun () ->
            Expect.isTrue (layoutForRecent PickerReady "" None [] [ idOf 1 ] categories).ShowAllHeader "Kopf"

        testCase "im Picker vergebene Kategorie rückt an die Spitze der Filter-Liste" <| fun () ->
            let recent = [ guidOf 1; guidOf 2; guidOf 3; guidOf 4; guidOf 5 ]
            let pushed = rememberPickedCategory (Some (idOf 3)) recent
            Expect.equal pushed [ guidOf 3; guidOf 1; guidOf 2; guidOf 4; guidOf 5 ] "nach vorn, ohne Dublette"
            let full = rememberPickedCategory (Some (idOf 9)) recent
            Expect.equal full [ guidOf 9; guidOf 1; guidOf 2; guidOf 3; guidOf 4 ] "gekappt auf fünf"
            Expect.equal (rememberPickedCategory None recent) recent "Ohne Kategorie ändert nichts"

        testCase "im Filter gewählte Kategorie erscheint im Picker" <| fun () ->
            let applied = pushRecentCategories (Set.singleton (guidOf 4)) [ guidOf 1 ]
            let shown = recentPickerIds applied |> recentCategories categories None |> List.map (fun c -> c.Id)
            Expect.equal shown [ idOf 4; idOf 1 ] "gleiche Liste"

        testCase "Verdrahtung: Einzelwahl und Auswahl merken, beide Picker bekommen die Liste" <| fun () ->
            let state = read [ "src"; "Client"; "Components"; "Ledger"; "State.fs" ]
            Expect.stringContains state "RecentCategories = rememberPickedCategory newId model.RecentCategories" "Einzelwahl"
            Expect.stringContains state "RecentCategories = rememberPickedCategory target model.RecentCategories" "Auswahl"
            let view = read [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            let count = view.Split("Recent = recentPickerIds model.RecentCategories").Length - 1
            Expect.equal count 2 "Kategorie-Feld und Auswahl-Picker"
    ]
