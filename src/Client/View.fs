module View

open Feliz
open State

/// Main application view
let view (_model: Model) (_dispatch: Msg -> unit) =
    Html.div [
        prop.className "min-h-screen bg-gradient-to-br from-blue-50 to-indigo-100 flex items-center justify-center p-4"
        prop.children [
            Html.div [
                prop.className "bg-white rounded-2xl shadow-xl p-8 max-w-2xl w-full"
                prop.children [
                    Html.h1 [
                        prop.className "text-4xl font-bold text-gray-800 mb-4 text-center"
                        prop.text "BudgetBuddy"
                    ]
                    Html.p [
                        prop.className "text-gray-600 text-center mb-8"
                        prop.text "Sync bank transactions from Comdirect to YNAB"
                    ]
                    Html.div [
                        prop.className "bg-blue-50 border border-blue-200 rounded-lg p-6 text-center"
                        prop.children [
                            Html.p [
                                prop.className "text-blue-800 mb-2"
                                prop.text "🚀 Ready to implement"
                            ]
                            Html.p [
                                prop.className "text-sm text-blue-600"
                                prop.text "See docs/MILESTONE-PLAN.md to get started"
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]
