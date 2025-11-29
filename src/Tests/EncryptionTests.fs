module EncryptionTests

open Expecto
open Persistence.Encryption

[<Tests>]
let encryptionTests =
    testList "Encryption" [
        testCase "encrypt and decrypt roundtrip for simple text" <| fun () ->
            let plaintext = "Hello, World!"
            match encrypt plaintext with
            | Error err -> failtestf "Encryption failed: %s" err
            | Ok encrypted ->
                match decrypt encrypted with
                | Error err -> failtestf "Decryption failed: %s" err
                | Ok decrypted ->
                    Expect.equal decrypted plaintext "Decrypted text should match original"

        testCase "encrypt and decrypt roundtrip for sensitive data" <| fun () ->
            let plaintext = "sk_live_abc123xyz789_secret_token"
            match encrypt plaintext with
            | Error err -> failtestf "Encryption failed: %s" err
            | Ok encrypted ->
                Expect.notEqual encrypted plaintext "Encrypted text should not match plaintext"
                match decrypt encrypted with
                | Error err -> failtestf "Decryption failed: %s" err
                | Ok decrypted ->
                    Expect.equal decrypted plaintext "Decrypted secret should match original"

        testCase "encrypt empty string returns empty string" <| fun () ->
            match encrypt "" with
            | Error err -> failtestf "Encryption failed: %s" err
            | Ok encrypted ->
                Expect.equal encrypted "" "Empty string should encrypt to empty string"

        testCase "decrypt empty string returns empty string" <| fun () ->
            match decrypt "" with
            | Error err -> failtestf "Decryption failed: %s" err
            | Ok decrypted ->
                Expect.equal decrypted "" "Empty string should decrypt to empty string"

        testCase "encrypt produces different ciphertext each time (due to random IV)" <| fun () ->
            let plaintext = "Same text"
            match encrypt plaintext, encrypt plaintext with
            | Ok encrypted1, Ok encrypted2 ->
                Expect.notEqual encrypted1 encrypted2 "Same plaintext should produce different ciphertext (random IV)"
            | Error err1, _ -> failtestf "First encryption failed: %s" err1
            | _, Error err2 -> failtestf "Second encryption failed: %s" err2

        testCase "encrypt and decrypt roundtrip for long text" <| fun () ->
            let plaintext = String.replicate 1000 "Lorem ipsum dolor sit amet, consectetur adipiscing elit. "
            match encrypt plaintext with
            | Error err -> failtestf "Encryption failed: %s" err
            | Ok encrypted ->
                match decrypt encrypted with
                | Error err -> failtestf "Decryption failed: %s" err
                | Ok decrypted ->
                    Expect.equal decrypted plaintext "Long text should roundtrip correctly"

        testCase "encrypt and decrypt roundtrip for text with special characters" <| fun () ->
            let plaintext = "Special chars: äöü ß € @ # $ % & * () [] {} <> / \\ | ~ ` ' \" \n \t"
            match encrypt plaintext with
            | Error err -> failtestf "Encryption failed: %s" err
            | Ok encrypted ->
                match decrypt encrypted with
                | Error err -> failtestf "Decryption failed: %s" err
                | Ok decrypted ->
                    Expect.equal decrypted plaintext "Text with special characters should roundtrip correctly"

        testCase "decrypt invalid base64 returns error" <| fun () ->
            match decrypt "not-valid-base64!!!" with
            | Ok _ -> failtest "Should not successfully decrypt invalid base64"
            | Error err ->
                Expect.stringContains err "Decryption failed" "Error message should indicate decryption failure"

        testCase "decrypt corrupted ciphertext returns error" <| fun () ->
            // Create valid ciphertext then corrupt it
            match encrypt "test" with
            | Error err -> failtestf "Encryption failed: %s" err
            | Ok encrypted ->
                // Change last character to corrupt the ciphertext
                let corrupted = encrypted.Substring(0, encrypted.Length - 1) + "X"
                match decrypt corrupted with
                | Ok _ -> failtest "Should not successfully decrypt corrupted ciphertext"
                | Error err ->
                    Expect.stringContains err "Decryption failed" "Error message should indicate decryption failure"

        testCase "encrypt and decrypt roundtrip for unicode text" <| fun () ->
            let plaintext = "Unicode: 你好世界 🚀 مرحبا العالم Здравствуй мир"
            match encrypt plaintext with
            | Error err -> failtestf "Encryption failed: %s" err
            | Ok encrypted ->
                match decrypt encrypted with
                | Error err -> failtestf "Decryption failed: %s" err
                | Ok decrypted ->
                    Expect.equal decrypted plaintext "Unicode text should roundtrip correctly"

        testCase "encrypted text is base64 encoded" <| fun () ->
            let plaintext = "Test string"
            match encrypt plaintext with
            | Error err -> failtestf "Encryption failed: %s" err
            | Ok encrypted ->
                // Base64 should only contain alphanumeric, +, /, and = characters
                let isBase64 = encrypted |> Seq.forall (fun c ->
                    System.Char.IsLetterOrDigit(c) || c = '+' || c = '/' || c = '=')
                Expect.isTrue isBase64 "Encrypted text should be valid base64"
    ]
