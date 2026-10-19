module Tests.RulesListTests

// categorization-wnxd6 (ADR 0049, Ruhige Listen): die Regeln-Seite als ruhige Liste.
// Reine Funktionen bekommen Werttests, die Struktur der Seite wird über den Quelltext
// zugesichert (Feliz-Elemente lassen sich unter .NET nicht inspizieren).

open System
open System.IO
open Expecto
open Shared.Domain
open Components.Rules.Types
open Components.Rules.View
open Types
open Client.DesignSystem

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private rulesViewSource =
    File.ReadAllText(Path.Combine(repoRoot, "src", "Client", "Components", "Rules", "View.fs"))

let private appViewSource =
    File.ReadAllText(Path.Combine(repoRoot, "src", "Client", "View.fs"))

/// Quelltext der Listenzeilen (Tabelle und Handy), ohne Tabellenrahmen, Dialog und Leerzustand
let private listSource =
    let slice (from: string) (until: string) =
        let a = rulesViewSource.IndexOf from
        rulesViewSource.Substring(a, rulesViewSource.IndexOf until - a)
    slice "let private ruleTableRow" "let private rulesTable"
    + slice "let private ruleMobileRow" "let private rulesMobileList"

/// Quelltext des Dialogs
let private modalSource =
    let a = rulesViewSource.IndexOf "let private ruleEditModal"
    let b = rulesViewSource.IndexOf "// Page Header Actions"
    rulesViewSource.Substring(a, b - a)

let private makeRule (enabled: bool) : Rule =
    {
        Id = RuleId (Guid.NewGuid())
        Name = "Amazon"
        Pattern = "amazon"
        PatternType = Contains
        TargetField = Combined
        CategoryId = YnabCategoryId (Guid.NewGuid())
        CategoryName = "Shopping"
        PayeeOverride = None
        Priority = 0
        Enabled = enabled
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }

let private rules (total: int) (active: int) =
    [ for i in 1..total -> makeRule (i <= active) ]

let private contains (needle: string) (text: string) = text.Contains needle

let private tests =
    testList "Regeln als ruhige Liste (categorization-wnxd6)" [
        testList "Quelltext" [
            test "Kopf ruhig, Rahmen volle Breite, kein Gradient" {
                Expect.isFalse (contains "gradientWithActions" rulesViewSource) "gradientWithActions"
                Expect.isFalse (contains "PageHeader.gradient" rulesViewSource) "PageHeader.gradient"
                Expect.isTrue (contains "PageHeader.calm" rulesViewSource) "PageHeader.calm"
                Expect.isTrue (contains "Primitives.Container.full" rulesViewSource) "Container.full"
            }
            test "Tabelle mit calmHeaderCell und calmRow, Zeile ohne Karte" {
                Expect.isTrue (contains "Table.calmHeaderCell" rulesViewSource) "calmHeaderCell"
                Expect.isTrue (contains "Table.calmRow" rulesViewSource) "calmRow"
                Expect.isFalse (contains "bg-surface-card" listSource) "bg-surface-card in der Zeile"
                Expect.isFalse (contains "rounded-lg" listSource) "rounded-lg in der Zeile"
                Expect.isFalse (contains "Card." listSource) "Card. in der Zeile"
                Expect.isTrue (contains "Loading.tableSkeleton" rulesViewSource) "tableSkeleton"
                Expect.isFalse (contains "Loading.cardSkeleton" rulesViewSource) "cardSkeleton"
            }
            test "App-Rahmen: Regeln bekommen pageContentFull" {
                let a = appViewSource.IndexOf "let pageShell"
                let shell = appViewSource.Substring(a, 300)
                Expect.isTrue (contains "Rules" shell && contains "Navigation.pageContentFull" shell) shell
            }
            test "Zeile ohne Schalter, Löschen und Stift; Dialog behält den Schalter" {
                Expect.isFalse (contains "Input.toggle" listSource) "Input.toggle"
                Expect.isFalse (contains "Icons.trash" listSource) "Icons.trash"
                Expect.isFalse (contains "Icons.edit" listSource) "Icons.edit"
                Expect.isTrue (contains "Input.toggle" modalSource) "Dialog braucht den Schalter"
                Expect.isTrue (contains "\"Regel aktiv\"" modalSource) "Regel aktiv"
            }
            test "Dialog: Löschen nur im Bearbeiten-Modus, mit Bestätigung" {
                Expect.isTrue (contains "\"Regel löschen\"" modalSource) "Regel löschen"
                Expect.isTrue (contains "ConfirmDeleteRule" modalSource) "ConfirmDeleteRule"
                Expect.isTrue (contains "DeleteRule" modalSource) "DeleteRule"
                Expect.isTrue (contains "when not isNew" modalSource) "nur im Bearbeiten-Modus"
            }
            test "Deutsch: keine englischen Reste, kein Gedankenstrich im Hinweis" {
                for literal in
                    [ "Rule Name"; "Refresh rules"; "Regex pattern"; "Contains substring"
                      "Exact match"; "Pattern:" ] do
                    Expect.isFalse (contains literal rulesViewSource) literal
                Expect.isFalse (contains "—" rulesViewSource) "Gedankenstrich (lang)"
                Expect.isFalse (contains "–" rulesViewSource) "Gedankenstrich (kurz)"
                Expect.isFalse (contains "—" priorityHint) priorityHint
                Expect.equal
                    priorityHint
                    "Die Regeln werden von oben nach unten geprüft. Die erste passende, aktive Regel gewinnt."
                    "Prioritätshinweis"
            }
        ]
        testList "statusLabel" [
            test "aktiv: kein Zeichen" { Expect.equal (statusLabel true) "" "aktiv" }
            test "inaktiv: das Wort" { Expect.equal (statusLabel false) "inaktiv" "inaktiv" }
        ]
        testList "summaryText" [
            test "keine Regeln: leer" { Expect.equal (summaryText []) "" "leer" }
            test "eine Regel" { Expect.equal (summaryText (rules 1 1)) "1 Regel, 1 aktiv" "eine" }
            test "zwölf Regeln, elf aktiv" {
                Expect.equal (summaryText (rules 12 11)) "12 Regeln, 11 aktiv" "zwölf"
            }
        ]
        testList "patternTypeLabel" [
            test "Anzeigetexte" {
                Expect.equal (patternTypeLabel PatternType.Regex) "Regex" "Regex"
                Expect.equal (patternTypeLabel Contains) "Enthält" "Enthält"
                Expect.equal (patternTypeLabel Exact) "Exakt" "Exakt"
            }
            test "Rundlauf zum technischen Schlüssel ist verlustfrei" {
                for t in [ PatternType.Regex; Contains; Exact ] do
                    Expect.equal (patternTypeOfKey (patternTypeKey t)) t $"{t}"
                Expect.equal (patternTypeKey PatternType.Regex) "Regex" "Schlüssel Regex"
                Expect.equal (patternTypeKey Contains) "Contains" "Schlüssel Contains"
                Expect.equal (patternTypeKey Exact) "Exact" "Schlüssel Exact"
            }
        ]
        testList "rowClass" [
            test "inaktiv: gedämpft, ohne opacity-50 und ohne neon-Klasse" {
                let cls = rowClass false
                Expect.isTrue (contains "text-text-secondary" cls) cls
                Expect.isFalse (contains "opacity-50" cls) cls
                Expect.isFalse (contains "neon-" cls) cls
            }
            test "aktiv: hell" {
                Expect.isTrue (contains "text-text-primary" (rowClass true)) (rowClass true)
            }
        ]
        testList "Pflichtfelder" [
            test "leeres Formular nennt deutsche Labels" {
                // Regression: der Hinweis lautete "Bitte ausfüllen: Rule Name, Pattern, Category",
                // die Labels im Dialog sind aber "Regelname", "Muster", "Kategorie".
                Expect.equal
                    (Form.missingFieldsText (requiredFields RuleFormState.empty))
                    "Bitte ausfüllen: Regelname, Muster, Kategorie"
                    "Hinweis"
            }
            test "ausgefülltes Formular: nichts fehlt" {
                let form =
                    { RuleFormState.empty with
                        Name = "A"
                        Pattern = "p"
                        CategoryId = Some (YnabCategoryId (Guid.NewGuid())) }
                Expect.isEmpty (requiredFields form) "nichts fehlt"
            }
        ]
        testList "Löschen im Dialog" [
            test "ConfirmDeleteRule merkt die Regel, RuleDeleted schließt den Dialog" {
                let model, _ = Components.Rules.State.init ()
                let rule = makeRule true
                let editing =
                    { model with
                        Rules = RemoteData.Success [ rule ]
                        EditingRule = Some rule
                        Form = RuleFormState.fromRule rule }
                let confirming, _, _ = Components.Rules.State.update (ConfirmDeleteRule rule.Id) editing
                Expect.equal confirming.ConfirmingDeleteRuleId (Some rule.Id) "Bestätigung offen"
                Expect.isSome confirming.EditingRule "Dialog noch offen"
                let deleted, _, _ =
                    Components.Rules.State.update (RuleDeleted (Ok rule.Id)) confirming
                Expect.isNone deleted.EditingRule "Dialog zu"
                Expect.isFalse deleted.IsNewRule "kein Anlegen-Modus"
                Expect.isNone deleted.ConfirmingDeleteRuleId "Bestätigung zurückgesetzt"
                Expect.equal deleted.Form RuleFormState.empty "Formular leer"
                match deleted.Rules with
                | RemoteData.Success remaining -> Expect.isEmpty remaining "Regel aus der Liste"
                | other -> failtestf "Erwartet Success, war %A" other
            }
        ]
    ]

[<Tests>]
let allTests = tests
