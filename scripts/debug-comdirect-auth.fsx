#!/usr/bin/env dotnet fsi

// ============================================
// Comdirect Auth Debug Script
// ============================================
//
// This script shows you EXACTLY what credentials
// are being sent to Comdirect, so you can verify
// they're correct.

#load "EnvLoader.fsx"

open System
open System.Web
open EnvLoader

printfn "==================================================="
printfn "Comdirect Credentials Debug"
printfn "==================================================="
printfn ""

let env = EnvLoader.loadProjectEnv()

let clientId = EnvLoader.getRequired env "COMDIRECT_CLIENT_ID"
let clientSecret = EnvLoader.getRequired env "COMDIRECT_CLIENT_SECRET"
let username = EnvLoader.getRequired env "COMDIRECT_USERNAME"
let password = EnvLoader.getRequired env "COMDIRECT_PASSWORD"

printfn "Loaded from .env:"
printfn $"  COMDIRECT_CLIENT_ID: {clientId}"
printfn $"  COMDIRECT_CLIENT_SECRET: {clientSecret.Substring(0, min 8 clientSecret.Length)}..."
printfn $"  COMDIRECT_USERNAME: {username}"
let maskedPassword = String.replicate password.Length "*"
printfn $"  COMDIRECT_PASSWORD: {maskedPassword}"
printfn ""

// Build the OAuth request body
let body =
    sprintf "client_id=%s&client_secret=%s&username=%s&password=%s&grant_type=password"
        clientId
        clientSecret
        username
        password

printfn "OAuth Request Body (raw):"
printfn $"  {body}"
printfn ""

// Check for special characters that need URL encoding
let needsEncoding (s: string) =
    s.ToCharArray()
    |> Array.exists (fun c ->
        not (Char.IsLetterOrDigit(c) || c = '-' || c = '_' || c = '.' || c = '~')
    )

printfn "URL Encoding Check:"
printfn $"  Client ID needs encoding: {needsEncoding clientId}"
printfn $"  Client Secret needs encoding: {needsEncoding clientSecret}"
printfn $"  Username needs encoding: {needsEncoding username}"
printfn $"  Password needs encoding: {needsEncoding password}"
printfn ""

if needsEncoding username || needsEncoding password then
    printfn "⚠️  WARNING: Your username or password contains special characters!"
    printfn "   These need to be URL-encoded in the OAuth request."
    printfn ""
    printfn "   URL-encoded values:"
    printfn $"   Username: {HttpUtility.UrlEncode(username)}"
    printfn $"   Password: {HttpUtility.UrlEncode(password)}"
    printfn ""

printfn "==================================================="
printfn "Checklist:"
printfn "==================================================="
printfn ""
printfn "1. Verify Client ID and Client Secret"
printfn "   - These come from Comdirect's API portal"
printfn "   - Make sure they match exactly (no extra spaces)"
printfn ""
printfn "2. Verify Username and Password"
printfn "   - This is your Comdirect login username"
printfn "   - Not your Zugangsnummer (that's different!)"
printfn "   - Password is case-sensitive"
printfn ""
printfn "3. Check API Access"
printfn "   - Is your Comdirect API access enabled?"
printfn "   - Have you accepted the API terms?"
printfn ""
printfn "Common Issues:"
printfn "- Client ID and Secret don't match"
printfn "- Using Zugangsnummer instead of username"
printfn "- Special characters in password not URL-encoded"
printfn "- API access not yet activated"
printfn ""
