module RuhigeListenTests

// design-system-q7rkm (ADR 0049, Ruhige Listen): reine Klassenfunktionen der neuen
// DS-Bausteine. Feliz-Elemente lassen sich unter .NET nicht inspizieren, deshalb
// liegt jede Rolle als Klassenzeichenkette in einer öffentlichen Funktion.

open System.IO
open Expecto
open Client.DesignSystem
open Client.DesignSystem.Tokens

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readDs (file: string) =
    File.ReadAllText(Path.Combine(repoRoot, "src", "Client", "DesignSystem", file))

let private has (needle: string) (cls: string) = cls.Split(' ') |> Array.contains needle

let private tests =
    testList "Ruhige Listen (ADR 0049)" [
        testList "Money.listAmountClass" [
            test "negativ: text-primary, kein Rot, kein Glow" {
                let cls = Money.listAmountClass -48.12m
                Expect.isTrue (has Colors.textPrimary cls) cls
                Expect.isFalse (cls.Contains "neon-red") cls
                Expect.isFalse (cls.Contains "neon-green") cls
                Expect.isFalse (cls.Contains "text-shadow") cls
            }
            test "null: text-primary" {
                let cls = Money.listAmountClass 0m
                Expect.isTrue (has Colors.textPrimary cls) cls
                Expect.isFalse (cls.Contains "neon-") cls
            }
            test "positiv: Teal, weder Grün noch Glow noch text-primary" {
                let cls = Money.listAmountClass 4000m
                Expect.isTrue (has "text-neon-teal" cls) cls
                Expect.isFalse (cls.Contains "neon-green") cls
                Expect.isFalse (cls.Contains "text-shadow") cls
                Expect.isFalse (has Colors.textPrimary cls) cls
            }
            test "Mono, Gewicht 500, tabular-nums, Primär-Größe" {
                let cls = Money.listAmountClass -1m
                for c in [ "font-mono"; "font-medium"; "tabular-nums" ] do
                    Expect.isTrue (has c cls) $"{c} fehlt: {cls}"
                Expect.isTrue (cls.Contains ListRoles.primarySize) cls
            }
        ]

        testList "Money.accountBalanceClass" [
            test "positiver Saldo ist nicht mehr grün" {
                for isActive in [ true; false ] do
                    let cls = Money.accountBalanceClass isActive 1250.40m
                    Expect.isFalse (cls.Contains "neon-green") cls
                    Expect.isFalse (cls.Contains "neon-red") cls
            }
            test "aktiv text-primary, inaktiv text-secondary" {
                Expect.isTrue (has Colors.textPrimary (Money.accountBalanceClass true 10m)) "aktiv"
                Expect.isTrue (has Colors.textSecondary (Money.accountBalanceClass false 10m)) "inaktiv"
            }
            test "negativer Saldo ist Red, aktiv wie inaktiv" {
                for isActive in [ true; false ] do
                    let cls = Money.accountBalanceClass isActive -173.28m
                    Expect.isTrue (has Colors.neonRed cls) cls
                    Expect.isFalse (has Colors.textPrimary cls) cls
            }
        ]

        testList "Table Ausrichtung" [
            test "cell: left, right, center ergeben die wörtlichen Klassen" {
                // Regression: `$"text-{a}"` stand nie als ganze Zeichenkette im Quelltext,
                // der Tailwind-Scan erzeugte `text-right` deshalb nicht und der Betrag
                // stand linksbündig, obwohl Align = Some "right" gesetzt war.
                Expect.equal (Table.cellAlignClass (Some "right")) "text-right" "right"
                Expect.equal (Table.cellAlignClass (Some "left")) "text-left" "left"
                Expect.equal (Table.cellAlignClass (Some "center")) "text-center" "center"
            }
            test "headerCell: left, right, center, Vorgabe links" {
                // Regression: gleiche Ursache wie bei cell (Tailwind-Scan findet kein `text-{a}`).
                Expect.equal (Table.headerAlignClass (Some "right")) "text-right" "right"
                Expect.equal (Table.headerAlignClass (Some "left")) "text-left" "left"
                Expect.equal (Table.headerAlignClass (Some "center")) "text-center" "center"
                Expect.equal (Table.headerAlignClass None) "text-left" "Vorgabe"
            }
            test "Table.fs enthält die Klassen wörtlich und kein $\"text-{ mehr" {
                let src = readDs "Table.fs"
                for c in [ "\"text-right\""; "\"text-left\""; "\"text-center\"" ] do
                    Expect.isTrue (src.Contains c) $"{c} steht nicht wörtlich in Table.fs"
                Expect.isFalse (src.Contains "$\"text-{") "interpolierte text-{..}-Klasse in Table.fs"
            }
        ]

        testList "Table Zellrollen" [
            test "Payee: primär 15px/14px, 500, text-primary" {
                let cls = Table.cellRoleClass Table.Payee
                for c in [ "text-[15px]"; "md:text-sm"; "font-medium"; Colors.textPrimary ] do
                    Expect.isTrue (has c cls) $"{c} fehlt: {cls}"
            }
            test "Datum: Mono 12px, 400, text-muted" {
                let cls = Table.cellRoleClass Table.DateCell
                for c in [ "font-mono"; "text-xs"; "font-normal"; Colors.textMuted ] do
                    Expect.isTrue (has c cls) $"{c} fehlt: {cls}"
            }
            test "Sekundär: 12px, text-secondary" {
                let cls = Table.cellRoleClass Table.Secondary
                for c in [ "text-xs"; Colors.textSecondary ] do
                    Expect.isTrue (has c cls) $"{c} fehlt: {cls}"
            }
            test "Gedämpft: 12px, text-muted" {
                let cls = Table.cellRoleClass Table.Muted
                for c in [ "text-xs"; Colors.textMuted ] do
                    Expect.isTrue (has c cls) $"{c} fehlt: {cls}"
            }
            test "Betrag: rechtsbündig und Money-Listenklasse" {
                let cls = Table.amountCellClass -5m
                Expect.isTrue (has "text-right" cls) cls
                Expect.isTrue (cls.Contains (Money.listAmountClass -5m)) cls
                Expect.isTrue (Table.amountCellClass 5m |> has "text-neon-teal") "positiv Teal"
            }
            test "Kopfzelle: Mono 600 10px, gesperrt, text-muted, Höhe 42px" {
                let cls = Table.calmHeaderClass (Some "right")
                for c in [ "font-mono"; "font-semibold"; "text-[10px]"; "tracking-[0.12em]"; Colors.textMuted; "h-[42px]"; "text-right" ] do
                    Expect.isTrue (has c cls) $"{c} fehlt: {cls}"
            }
            test "Zeilenhöhe der ruhigen Tabelle ist 44px" {
                Expect.isTrue (has "h-11" (Table.calmRowClass false)) (Table.calmRowClass false)
            }
        ]

        testList "Statuskante" [
            test "Table.rowClass mit StatusEdge: 3px-Kante in Pink als inset-Schatten, keine Spalte" {
                let on = Table.rowClass { Table.defaultRowProps with StatusEdge = true }
                let off = Table.rowClass Table.defaultRowProps
                Expect.isTrue (on.Contains ListRoles.statusEdge) on
                Expect.isFalse (off.Contains ListRoles.statusEdge) off
                Expect.isTrue (ListRoles.statusEdge.Contains "inset_3px_0_0_0") ListRoles.statusEdge
                Expect.isTrue (ListRoles.statusEdge.Contains "neon-pink") ListRoles.statusEdge
            }
        ]

        testList "Container und PageHeader" [
            test "Container.fullClass hat keine Breitenbegrenzung, aber die Seitenränder von view" {
                Expect.isFalse (Primitives.Container.fullClass.Contains "max-w-") Primitives.Container.fullClass
                Expect.isTrue (Primitives.Container.fullClass.Contains "px-4 md:px-6") Primitives.Container.fullClass
                Expect.isTrue (Primitives.Container.viewClass.Contains "px-4 md:px-6") Primitives.Container.viewClass
                Expect.isTrue (Primitives.Container.viewClass.Contains "max-w-7xl") "view bleibt begrenzt"
            }
            test "PageHeader Calm: 20px, 600, ohne Verlauf und ohne md:text-4xl" {
                let cls = PageHeader.titleClass PageHeader.Calm
                for c in [ "text-xl"; "font-semibold"; "font-display"; Colors.textPrimary ] do
                    Expect.isTrue (has c cls) $"{c} fehlt: {cls}"
                Expect.isFalse (cls.Contains "gradient") cls
                Expect.isFalse (cls.Contains "md:text-4xl") cls
            }
            test "PageHeader Calm: Untertitel klein und gedämpft, Aktionen in derselben Zeile" {
                let sub = PageHeader.subtitleClass PageHeader.Calm
                Expect.isTrue (has "text-xs" sub && has Colors.textMuted sub) sub
                let box = PageHeader.containerClass PageHeader.Calm
                Expect.isFalse (box.Contains "flex-col") box
            }
            test "PageHeader Gradient und Standard bleiben unverändert" {
                Expect.equal (PageHeader.titleClass PageHeader.Gradient)
                    "text-2xl md:text-4xl font-bold font-display bg-gradient-to-br from-neon-teal to-neon-green bg-clip-text text-transparent" "Gradient"
                Expect.equal (PageHeader.titleClass PageHeader.Standard)
                    "text-2xl md:text-4xl font-bold font-display text-text-primary" "Standard"
            }
        ]
        testList "Button.callerSetsDisplay (design-system-qz7b6)" [
            test "ohne eigene Anzeige-Klasse bleibt inline-flex (Aufrufer setzt keine)" {
                Expect.isFalse (Button.callerSetsDisplay "") "leer"
                Expect.isFalse (Button.callerSetsDisplay "md:hidden") "nur Breakpoint-Klasse"
                Expect.isFalse (Button.callerSetsDisplay "mt-2 w-full") "ohne Anzeige"
            }
            test "hidden ohne Breakpoint verdrängt das konkurrierende inline-flex" {
                // Ursache des Handy-Fehlers: inline-flex gewann im erzeugten CSS gegen hidden.
                Expect.isTrue (Button.callerSetsDisplay "hidden md:inline-flex") "hidden md:inline-flex"
            }
            test "andere Anzeige-Klassen ohne Breakpoint verdrängen inline-flex ebenfalls" {
                Expect.isTrue (Button.callerSetsDisplay "block") "block"
                Expect.isTrue (Button.callerSetsDisplay "flex md:hidden") "flex"
            }
        ]
    ]

[<Tests>]
let allTests = tests
