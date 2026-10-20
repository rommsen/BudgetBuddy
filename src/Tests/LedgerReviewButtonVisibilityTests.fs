module LedgerReviewButtonVisibilityTests

// ledger-mhe2s: Die Prüf-Schaltfläche der PC-Zeile ist nur bei Hover oder Fokus sichtbar
// und immer neutral. Die Klassen sind rein und laufen ohne DOM; die Quelltext-Zusicherungen
// prüfen die Verdrahtung (Zeile trägt `group`, Haken ist nicht Teal).

open System.IO
open Expecto
open Shared.Domain
open Components.Ledger.View

let private viewSource () =
    let rec up (dir: DirectoryInfo) =
        let f = Path.Combine(dir.FullName, "src", "Client", "Components", "Ledger", "View.fs")
        if File.Exists f then f else up dir.Parent
    File.ReadAllText(up (DirectoryInfo(System.AppContext.BaseDirectory)))

[<Tests>]
let tests =
    testList "Prüf-Schaltfläche nur bei Hover oder Fokus (ledger-mhe2s)" [
        test "PC: ohne Hover und Fokus unsichtbar, Breite bleibt (opacity-0, kein hidden)" {
            let c = reviewMarkButtonClassName true false
            Expect.stringContains c "opacity-0" "unsichtbar"
            Expect.isFalse (c.Contains "hidden") "keine Breitenänderung"
            Expect.stringContains c "h-11 w-11 md:h-7 md:w-7" "Größe bleibt"
        }
        test "PC: sichtbar bei Zeilen-Hover, Fokus in der Zeile und eigenem Fokus" {
            let c = reviewMarkButtonClassName true false
            Expect.stringContains c "group-hover:opacity-100" "Hover"
            Expect.stringContains c "group-focus-within:opacity-100" "Fokus in der Zeile"
            Expect.stringContains c "focus-visible:opacity-100" "eigener Fokus"
        }
        test "PC: Zeile mit Tastaturcursor zeigt die Schaltfläche dauerhaft" {
            let c = reviewMarkButtonClassName true true
            Expect.stringContains c "opacity-100" "sichtbar"
            Expect.isFalse (c.Contains "opacity-0") "nicht unsichtbar"
        }
        test "Handy: immer sichtbar, 44px Trefferfläche, neutral" {
            let c = reviewMarkButtonClassName false false
            Expect.isFalse (c.Contains "opacity-0") "sichtbar"
            Expect.stringContains c "h-11 w-11" "44px"
            Expect.stringContains c "text-text-secondary" "neutral"
        }
        test "Farbe ist neutral, nie Teal" {
            for hoverOnly in [ true; false ] do
                let c = reviewMarkButtonClassName hoverOnly false
                Expect.stringContains c "text-text-secondary" "neutral"
                Expect.stringContains c "hover:text-text-primary" "Hover"
                Expect.isFalse (c.Contains "teal") "kein Teal"
        }
        test "Haken und Rückgängig-Pfeil nutzen Icons.Default statt NeonTeal" {
            let src = viewSource ()
            Expect.stringContains src "| Reviewed -> Icons.check Icons.SM Icons.Default" "Haken neutral"
            Expect.isFalse (src.Contains "Icons.check Icons.SM Icons.NeonTeal") "kein Teal-Haken"
        }
        test "Tooltip nennt die Aktion" {
            Expect.equal (reviewMarkTooltip Reviewed) "Als geprüft markieren" "geprüft"
            Expect.equal (reviewMarkTooltip Unreviewed) "Als ungeprüft markieren" "ungeprüft"
        }
        test "PC-Zeile trägt die Klasse group, damit group-hover wirkt" {
            Expect.stringContains (viewSource ()) "ClassName = Some (\"group \" + selectableRowClassName" "group an der Zeile"
        }
    ]
