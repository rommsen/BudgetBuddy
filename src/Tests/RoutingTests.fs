module RoutingTests

open Expecto
open Types

[<Tests>]
let routingTests =
    testList "Routing" [

        testList "parseUrl" [
            testCase "empty segments returns SyncFlow" <| fun () ->
                let result = Routing.parseUrl []
                Expect.equal result SyncFlow "Empty URL should route to SyncFlow"

            testCase "rules segment returns Rules" <| fun () ->
                let result = Routing.parseUrl ["rules"]
                Expect.equal result Rules "rules URL should route to Rules"

            testCase "settings segment returns Settings" <| fun () ->
                let result = Routing.parseUrl ["settings"]
                Expect.equal result Settings "settings URL should route to Settings"

            testCase "unknown segment returns SyncFlow (fallback)" <| fun () ->
                let result = Routing.parseUrl ["unknown"]
                Expect.equal result SyncFlow "Unknown URL should fallback to SyncFlow"

            testCase "multiple unknown segments returns SyncFlow" <| fun () ->
                let result = Routing.parseUrl ["foo"; "bar"; "baz"]
                Expect.equal result SyncFlow "Multiple unknown segments should fallback to SyncFlow"
        ]

        testList "toUrlSegments" [
            testCase "SyncFlow returns empty list" <| fun () ->
                let result = Routing.toUrlSegments SyncFlow
                Expect.equal result [] "SyncFlow should be empty segments (root)"

            testCase "Rules returns rules" <| fun () ->
                let result = Routing.toUrlSegments Rules
                Expect.equal result ["rules"] "Rules should be ['rules']"

            testCase "Settings returns settings" <| fun () ->
                let result = Routing.toUrlSegments Settings
                Expect.equal result ["settings"] "Settings should be ['settings']"
        ]

        testList "roundtrip (parseUrl <-> toUrlSegments)" [
            testCase "SyncFlow roundtrips correctly" <| fun () ->
                let segments = Routing.toUrlSegments SyncFlow
                let parsed = Routing.parseUrl segments
                Expect.equal parsed SyncFlow "SyncFlow should roundtrip correctly"

            testCase "Rules roundtrips correctly" <| fun () ->
                let segments = Routing.toUrlSegments Rules
                let parsed = Routing.parseUrl segments
                Expect.equal parsed Rules "Rules should roundtrip correctly"

            testCase "Settings roundtrips correctly" <| fun () ->
                let segments = Routing.toUrlSegments Settings
                let parsed = Routing.parseUrl segments
                Expect.equal parsed Settings "Settings should roundtrip correctly"
        ]
    ]
