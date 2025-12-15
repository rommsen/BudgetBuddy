module RemoteDataTests

open Expecto
open Types

[<Tests>]
let remoteDataTests =
    testList "RemoteData Helper Functions" [

        testList "map" [
            testCase "maps over Success value" <| fun () ->
                let result = RemoteData.map ((+) 1) (Success 5)
                Expect.equal result (Success 6) "Should increment the value"

            testCase "preserves Loading" <| fun () ->
                let result = RemoteData.map ((+) 1) Loading
                Expect.equal result Loading "Should preserve Loading"

            testCase "preserves NotAsked" <| fun () ->
                let result = RemoteData.map ((+) 1) NotAsked
                Expect.equal result NotAsked "Should preserve NotAsked"

            testCase "preserves Failure" <| fun () ->
                let result = RemoteData.map ((+) 1) (Failure "error")
                Expect.equal result (Failure "error") "Should preserve Failure with message"
        ]

        testList "bind" [
            testCase "binds over Success value" <| fun () ->
                let result = RemoteData.bind (fun x -> Success (x * 2)) (Success 5)
                Expect.equal result (Success 10) "Should double the value"

            testCase "can produce Failure from Success" <| fun () ->
                let result = RemoteData.bind (fun _ -> Failure "failed") (Success 5)
                Expect.equal result (Failure "failed") "Should produce Failure"

            testCase "preserves Loading" <| fun () ->
                let result = RemoteData.bind (fun x -> Success (x * 2)) Loading
                Expect.equal result Loading "Should preserve Loading"

            testCase "preserves NotAsked" <| fun () ->
                let result = RemoteData.bind (fun x -> Success (x * 2)) NotAsked
                Expect.equal result NotAsked "Should preserve NotAsked"

            testCase "preserves Failure" <| fun () ->
                let result = RemoteData.bind (fun x -> Success (x * 2)) (Failure "error")
                Expect.equal result (Failure "error") "Should preserve Failure"
        ]

        testList "isLoading" [
            testCase "returns true for Loading" <| fun () ->
                Expect.isTrue (RemoteData.isLoading Loading) "Loading should be loading"

            testCase "returns false for Success" <| fun () ->
                Expect.isFalse (RemoteData.isLoading (Success 1)) "Success should not be loading"

            testCase "returns false for NotAsked" <| fun () ->
                Expect.isFalse (RemoteData.isLoading NotAsked) "NotAsked should not be loading"

            testCase "returns false for Failure" <| fun () ->
                Expect.isFalse (RemoteData.isLoading (Failure "err")) "Failure should not be loading"
        ]

        testList "isSuccess" [
            testCase "returns true for Success" <| fun () ->
                Expect.isTrue (RemoteData.isSuccess (Success 1)) "Success should be success"

            testCase "returns false for Loading" <| fun () ->
                Expect.isFalse (RemoteData.isSuccess Loading) "Loading should not be success"

            testCase "returns false for NotAsked" <| fun () ->
                Expect.isFalse (RemoteData.isSuccess NotAsked) "NotAsked should not be success"

            testCase "returns false for Failure" <| fun () ->
                Expect.isFalse (RemoteData.isSuccess (Failure "err")) "Failure should not be success"
        ]

        testList "isFailure" [
            testCase "returns true for Failure" <| fun () ->
                Expect.isTrue (RemoteData.isFailure (Failure "err")) "Failure should be failure"

            testCase "returns false for Success" <| fun () ->
                Expect.isFalse (RemoteData.isFailure (Success 1)) "Success should not be failure"

            testCase "returns false for Loading" <| fun () ->
                Expect.isFalse (RemoteData.isFailure Loading) "Loading should not be failure"

            testCase "returns false for NotAsked" <| fun () ->
                Expect.isFalse (RemoteData.isFailure NotAsked) "NotAsked should not be failure"
        ]

        testList "isNotAsked" [
            testCase "returns true for NotAsked" <| fun () ->
                Expect.isTrue (RemoteData.isNotAsked NotAsked) "NotAsked should be not asked"

            testCase "returns false for Success" <| fun () ->
                Expect.isFalse (RemoteData.isNotAsked (Success 1)) "Success should not be not asked"

            testCase "returns false for Loading" <| fun () ->
                Expect.isFalse (RemoteData.isNotAsked Loading) "Loading should not be not asked"

            testCase "returns false for Failure" <| fun () ->
                Expect.isFalse (RemoteData.isNotAsked (Failure "err")) "Failure should not be not asked"
        ]

        testList "toOption" [
            testCase "returns Some for Success" <| fun () ->
                Expect.equal (RemoteData.toOption (Success 42)) (Some 42) "Should return Some value"

            testCase "returns None for Loading" <| fun () ->
                Expect.equal (RemoteData.toOption Loading) None "Should return None for Loading"

            testCase "returns None for NotAsked" <| fun () ->
                Expect.equal (RemoteData.toOption NotAsked) None "Should return None for NotAsked"

            testCase "returns None for Failure" <| fun () ->
                Expect.equal (RemoteData.toOption (Failure "err")) None "Should return None for Failure"
        ]

        testList "withDefault" [
            testCase "returns value for Success" <| fun () ->
                Expect.equal (RemoteData.withDefault 0 (Success 42)) 42 "Should return success value"

            testCase "returns default for Loading" <| fun () ->
                Expect.equal (RemoteData.withDefault 0 Loading) 0 "Should return default for Loading"

            testCase "returns default for NotAsked" <| fun () ->
                Expect.equal (RemoteData.withDefault 0 NotAsked) 0 "Should return default for NotAsked"

            testCase "returns default for Failure" <| fun () ->
                Expect.equal (RemoteData.withDefault 0 (Failure "err")) 0 "Should return default for Failure"
        ]

        testList "mapError" [
            testCase "maps error message" <| fun () ->
                let result = RemoteData.mapError (fun e -> $"Error: {e}") (Failure "test")
                Expect.equal result (Failure "Error: test") "Should transform error message"

            testCase "preserves Success" <| fun () ->
                let result = RemoteData.mapError (fun e -> $"Error: {e}") (Success 1)
                Expect.equal result (Success 1) "Should preserve Success"

            testCase "preserves Loading" <| fun () ->
                let result = RemoteData.mapError (fun e -> $"Error: {e}") Loading
                Expect.equal result Loading "Should preserve Loading"

            testCase "preserves NotAsked" <| fun () ->
                let result = RemoteData.mapError (fun e -> $"Error: {e}") NotAsked
                Expect.equal result NotAsked "Should preserve NotAsked"
        ]

        testList "recover" [
            testCase "recovers from Failure with default" <| fun () ->
                let result = RemoteData.recover 0 (Failure "err")
                Expect.equal result (Success 0) "Should recover to Success with default"

            testCase "preserves Success" <| fun () ->
                let result = RemoteData.recover 0 (Success 42)
                Expect.equal result (Success 42) "Should preserve Success"

            testCase "preserves Loading" <| fun () ->
                let result = RemoteData.recover 0 Loading
                Expect.equal result Loading "Should preserve Loading"

            testCase "preserves NotAsked" <| fun () ->
                let result = RemoteData.recover 0 NotAsked
                Expect.equal result NotAsked "Should preserve NotAsked"
        ]

        testList "recoverWith" [
            testCase "recovers from Failure using error" <| fun () ->
                let result = RemoteData.recoverWith (fun e -> $"Recovered: {e}") (Failure "test")
                Expect.equal result (Success "Recovered: test") "Should recover using error message"

            testCase "preserves Success" <| fun () ->
                let result = RemoteData.recoverWith (fun _ -> "fallback") (Success "original")
                Expect.equal result (Success "original") "Should preserve Success"
        ]

        testList "map2" [
            testCase "combines two Success values" <| fun () ->
                let result = RemoteData.map2 (+) (Success 1) (Success 2)
                Expect.equal result (Success 3) "Should add values"

            testCase "returns first Failure" <| fun () ->
                let result = RemoteData.map2 (+) (Failure "first") (Success 2)
                Expect.equal result (Failure "first") "Should return first failure"

            testCase "returns second Failure when first is Success" <| fun () ->
                let result = RemoteData.map2 (+) (Success 1) (Failure "second")
                Expect.equal result (Failure "second") "Should return second failure"

            testCase "returns Loading when first is Loading" <| fun () ->
                let result = RemoteData.map2 (+) Loading (Success 2)
                Expect.equal result Loading "Should return Loading"

            testCase "returns Loading when second is Loading" <| fun () ->
                let result = RemoteData.map2 (+) (Success 1) Loading
                Expect.equal result Loading "Should return Loading"

            testCase "returns NotAsked when first is NotAsked" <| fun () ->
                let result = RemoteData.map2 (+) NotAsked (Success 2)
                Expect.equal result NotAsked "Should return NotAsked"
        ]

        testList "toError" [
            testCase "returns Some for Failure" <| fun () ->
                Expect.equal (RemoteData.toError (Failure "err")) (Some "err") "Should return error message"

            testCase "returns None for Success" <| fun () ->
                Expect.equal (RemoteData.toError (Success 1)) None "Should return None"

            testCase "returns None for Loading" <| fun () ->
                Expect.equal (RemoteData.toError Loading) None "Should return None"

            testCase "returns None for NotAsked" <| fun () ->
                Expect.equal (RemoteData.toError NotAsked) None "Should return None"
        ]

        testList "fold" [
            testCase "handles NotAsked" <| fun () ->
                let result = RemoteData.fold "na" "loading" string (fun e -> $"err:{e}") NotAsked
                Expect.equal result "na" "Should return NotAsked value"

            testCase "handles Loading" <| fun () ->
                let result = RemoteData.fold "na" "loading" string (fun e -> $"err:{e}") Loading
                Expect.equal result "loading" "Should return Loading value"

            testCase "handles Success" <| fun () ->
                let result = RemoteData.fold "na" "loading" string (fun e -> $"err:{e}") (Success 42)
                Expect.equal result "42" "Should apply success function"

            testCase "handles Failure" <| fun () ->
                let result = RemoteData.fold "na" "loading" string (fun e -> $"err:{e}") (Failure "test")
                Expect.equal result "err:test" "Should apply failure function"
        ]

        testList "fromResult" [
            testCase "converts Ok to Success" <| fun () ->
                let result = RemoteData.fromResult (Ok 42)
                Expect.equal result (Success 42) "Should convert to Success"

            testCase "converts Error to Failure" <| fun () ->
                let result = RemoteData.fromResult (Error "test")
                Expect.equal result (Failure "test") "Should convert to Failure"
        ]

        testList "fromOption" [
            testCase "converts Some to Success" <| fun () ->
                let result = RemoteData.fromOption (Some 42)
                Expect.equal result (Success 42) "Should convert to Success"

            testCase "converts None to NotAsked" <| fun () ->
                let result = RemoteData.fromOption None
                Expect.equal result NotAsked "Should convert to NotAsked"
        ]

        testList "fromOptionWithError" [
            testCase "converts Some to Success" <| fun () ->
                let result = RemoteData.fromOptionWithError "not found" (Some 42)
                Expect.equal result (Success 42) "Should convert to Success"

            testCase "converts None to Failure with message" <| fun () ->
                let result = RemoteData.fromOptionWithError "not found" None
                Expect.equal result (Failure "not found") "Should convert to Failure with message"
        ]
    ]
