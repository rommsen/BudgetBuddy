module Client.DesignSystem.Truncate

open Feliz

/// Gekürzter Text auf einer Zeile (Styleguide §4): `truncate min-w-0` kürzt mit Auslassungspunkten, `title` trägt
/// den ganzen Text, damit er beim Zeigen lesbar bleibt (Layout-Sweep, infrastructure-nx14s).

/// Klasse und `title` für ein Element, dessen Inhalt gekürzt wird. Für Elemente mit eigenen Kindern (etwa ein
/// Kürzel vor dem Namen) oder wenn der ganze Text vom sichtbaren abweicht.
let props (className: string) (title: string) : IReactProperty list =
    [ prop.className $"{className} truncate min-w-0"; prop.title title ]

/// Klasse, `title` und Text, wenn `title` und Text derselbe Wert sind. Zum Einbetten in ein anderes Element
/// (`Html.h2`, `Html.p`) oder neben weiteren Eigenschaften wie einer `data-testid`.
let textProps (className: string) (text: string) : IReactProperty list =
    props className text @ [ prop.text text ]

/// Ein gekürzter Text als `span`, `title` und Text sind derselbe Wert.
let text (className: string) (text: string) : ReactElement =
    Html.span (textProps className text)
