module LedgerReviewButtonVisibilityTests

// ledger-mhe2s: Die Prüf-Schaltfläche der PC-Zeile ist nur bei Hover oder Fokus sichtbar
// und immer neutral. Sichtbarkeit, Breite, Farbe und Trefferfläche misst der Browser
// (`scripts/ledger-review-button-visibility.test.mjs`, `scripts/ledger-compact-mouse.test.mjs`,
// `scripts/ledger-review-mark.test.mjs`); hier bleibt der Text der Schaltfläche.

open Expecto
open Shared.Domain
open Components.Ledger.View

[<Tests>]
let tests =
    testList "Prüf-Schaltfläche nur bei Hover oder Fokus (ledger-mhe2s)" [
        test "Tooltip nennt die Aktion" {
            Expect.equal (reviewMarkTooltip Reviewed) "Als geprüft markieren" "geprüft"
            Expect.equal (reviewMarkTooltip Unreviewed) "Als ungeprüft markieren" "ungeprüft"
        }
    ]
