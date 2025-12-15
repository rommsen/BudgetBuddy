module RoutingTests

open Expecto
open Types

[<Tests>]
let routingTests =
    testList "Routing" [

        testList "parseUrl" [
            testCase "empty segments returns Dashboard" <| fun () ->
                let result = Routing.parseUrl []
                Expect.equal result Dashboard "Empty URL should route to Dashboard"

            testCase "sync segment returns SyncFlow" <| fun () ->
                let result = Routing.parseUrl ["sync"]
                Expect.equal result SyncFlow "sync URL should route to SyncFlow"

            testCase "rules segment returns Rules" <| fun () ->
                let result = Routing.parseUrl ["rules"]
                Expect.equal result Rules "rules URL should route to Rules"

            testCase "settings segment returns Settings" <| fun () ->
                let result = Routing.parseUrl ["settings"]
                Expect.equal result Settings "settings URL should route to Settings"

            testCase "unknown segment returns Dashboard (fallback)" <| fun () ->
                let result = Routing.parseUrl ["unknown"]
                Expect.equal result Dashboard "Unknown URL should fallback to Dashboard"

            testCase "multiple unknown segments returns Dashboard" <| fun () ->
                let result = Routing.parseUrl ["foo"; "bar"; "baz"]
                Expect.equal result Dashboard "Multiple unknown segments should fallback to Dashboard"
        ]

        testList "toUrlSegments" [
            testCase "Dashboard returns empty list" <| fun () ->
                let result = Routing.toUrlSegments Dashboard
                Expect.equal result [] "Dashboard should be empty segments (root)"

            testCase "SyncFlow returns sync" <| fun () ->
                let result = Routing.toUrlSegments SyncFlow
                Expect.equal result ["sync"] "SyncFlow should be ['sync']"

            testCase "Rules returns rules" <| fun () ->
                let result = Routing.toUrlSegments Rules
                Expect.equal result ["rules"] "Rules should be ['rules']"

            testCase "Settings returns settings" <| fun () ->
                let result = Routing.toUrlSegments Settings
                Expect.equal result ["settings"] "Settings should be ['settings']"
        ]

        testList "roundtrip (parseUrl <-> toUrlSegments)" [
            testCase "Dashboard roundtrips correctly" <| fun () ->
                let segments = Routing.toUrlSegments Dashboard
                let parsed = Routing.parseUrl segments
                Expect.equal parsed Dashboard "Dashboard should roundtrip correctly"

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
