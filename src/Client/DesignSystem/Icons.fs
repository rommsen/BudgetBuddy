module Client.DesignSystem.Icons

open Feliz
open Client.DesignSystem.Tokens

// ============================================
// Icon Size Variants
// ============================================

type IconSize =
    | XS    // 16px - inline with small text
    | SM    // 20px - inline with body text
    | MD    // 24px - standard icons
    | LG    // 32px - featured icons
    | XL    // 48px - hero icons

let private sizeToClass = function
    | XS -> "w-4 h-4"
    | SM -> "w-5 h-5"
    | MD -> "w-6 h-6"
    | LG -> "w-8 h-8"
    | XL -> "w-12 h-12"

let private sizeToTextClass = function
    | XS -> "text-sm"
    | SM -> "text-base"
    | MD -> "text-xl"
    | LG -> "text-2xl"
    | XL -> "text-4xl"

// ============================================
// Icon Color Variants
// ============================================

type IconColor =
    | Default       // text-base-content/70
    | Primary       // text-base-content
    | NeonGreen     // text-neon-green
    | NeonOrange    // text-neon-orange
    | NeonTeal      // text-neon-teal
    | NeonPurple    // text-neon-purple
    | NeonPink      // text-neon-pink
    | NeonRed       // text-neon-red
    | Success       // same as NeonGreen
    | Warning       // same as NeonOrange
    | Error         // same as NeonRed
    | Info          // same as NeonTeal

let private colorToClass = function
    | Default -> "text-base-content/70"
    | Primary -> "text-base-content"
    | NeonGreen | Success -> "text-neon-green"
    | NeonOrange | Warning -> "text-neon-orange"
    | NeonTeal | Info -> "text-neon-teal"
    | NeonPurple -> "text-neon-purple"
    | NeonPink -> "text-neon-pink"
    | NeonRed | Error -> "text-neon-red"

// ============================================
// SVG Icon Component
// ============================================

/// Render an SVG icon with specified size and color
let private svgIcon (size: IconSize) (color: IconColor) (paths: ReactElement list) =
    Svg.svg [
        svg.className $"{sizeToClass size} {colorToClass color} inline-block flex-shrink-0"
        svg.fill "none"
        svg.viewBox (0, 0, 24, 24)
        svg.stroke "currentColor"
        svg.strokeWidth 1.5
        svg.children paths
    ]

// ============================================
// Navigation Icons (Heroicons Outline)
// ============================================

/// Dashboard icon (chart bar)
let dashboard (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 0 1 3 19.875v-6.75ZM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V8.625ZM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 3.504 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V4.125Z"
        ]
    ]

/// Sync icon (arrow path)
let sync (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0 3.181 3.183a8.25 8.25 0 0 0 13.803-3.7M4.031 9.865a8.25 8.25 0 0 1 13.803-3.7l3.181 3.182m0-4.991v4.99"
        ]
    ]

/// Rules icon (list bullet)
let rules (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M8.25 6.75h12M8.25 12h12m-12 5.25h12M3.75 6.75h.007v.008H3.75V6.75Zm.375 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0ZM3.75 12h.007v.008H3.75V12Zm.375 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Zm-.375 5.25h.007v.008H3.75v-.008Zm.375 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Z"
        ]
    ]

/// Settings icon (cog)
let settings (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.325.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.296 2.247a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.241-.438.613-.43.992a7.723 7.723 0 0 1 0 .255c-.008.378.137.75.43.991l1.004.827c.424.35.534.955.26 1.43l-1.298 2.247a1.125 1.125 0 0 1-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.47 6.47 0 0 1-.22.128c-.331.183-.581.495-.644.869l-.213 1.281c-.09.543-.56.94-1.11.94h-2.594c-.55 0-1.019-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 0 1-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.297-2.247a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.991a6.932 6.932 0 0 1 0-.255c.007-.38-.138-.751-.43-.992l-1.004-.827a1.125 1.125 0 0 1-.26-1.43l1.297-2.247a1.125 1.125 0 0 1 1.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.086.22-.128.332-.183.582-.495.644-.869l.214-1.28Z"
        ]
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
        ]
    ]

// ============================================
// Action Icons
// ============================================

/// Plus icon (add)
let plus (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M12 4.5v15m7.5-7.5h-15"
        ]
    ]

/// Check icon (success)
let check (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "m4.5 12.75 6 6 9-13.5"
        ]
    ]

/// X icon (close/error)
let x (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M6 18 18 6M6 6l12 12"
        ]
    ]

/// Edit icon (pencil)
let edit (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0 1 15.75 21H5.25A2.25 2.25 0 0 1 3 18.75V8.25A2.25 2.25 0 0 1 5.25 6H10"
        ]
    ]

/// Trash icon (delete)
let trash (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0"
        ]
    ]

// ============================================
// Status Icons
// ============================================

/// Exclamation triangle (warning)
let warning (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z"
        ]
    ]

/// Information circle
let info (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "m11.25 11.25.041-.02a.75.75 0 0 1 1.063.852l-.708 2.836a.75.75 0 0 0 1.063.853l.041-.021M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9-3.75h.008v.008H12V8.25Z"
        ]
    ]

/// Check circle (success)
let checkCircle (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
        ]
    ]

/// X circle (error)
let xCircle (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "m9.75 9.75 4.5 4.5m0-4.5-4.5 4.5M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
        ]
    ]

// ============================================
// UI Icons
// ============================================

/// Chevron down
let chevronDown (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "m19.5 8.25-7.5 7.5-7.5-7.5"
        ]
    ]

/// Chevron right
let chevronRight (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "m8.25 4.5 7.5 7.5-7.5 7.5"
        ]
    ]

/// External link
let externalLink (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M13.5 6H5.25A2.25 2.25 0 0 0 3 8.25v10.5A2.25 2.25 0 0 0 5.25 21h10.5A2.25 2.25 0 0 0 18 18.75V10.5m-10.5 6L21 3m0 0h-5.25M21 3v5.25"
        ]
    ]

/// Download
let download (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5M16.5 12 12 16.5m0 0L7.5 12m4.5 4.5V3"
        ]
    ]

/// Upload
let upload (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5m-13.5-9L12 3m0 0 4.5 4.5M12 3v13.5"
        ]
    ]

/// Search/Magnifying glass
let search (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z"
        ]
    ]

// ============================================
// Finance Icons
// ============================================

/// Currency dollar
let dollar (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M12 6v12m-3-2.818.879.659c1.171.879 3.07.879 4.242 0 1.172-.879 1.172-2.303 0-3.182C13.536 12.219 12.768 12 12 12c-.725 0-1.45-.22-2.003-.659-1.106-.879-1.106-2.303 0-3.182s2.9-.879 4.006 0l.415.33M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
        ]
    ]

/// Bank notes
let banknotes (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M2.25 18.75a60.07 60.07 0 0 1 15.797 2.101c.727.198 1.453-.342 1.453-1.096V18.75M3.75 4.5v.75A.75.75 0 0 1 3 6h-.75m0 0v-.375c0-.621.504-1.125 1.125-1.125H20.25M2.25 6v9m18-10.5v.75c0 .414.336.75.75.75h.75m-1.5-1.5h.375c.621 0 1.125.504 1.125 1.125v9.75c0 .621-.504 1.125-1.125 1.125h-.375m1.5-1.5H21a.75.75 0 0 0-.75.75v.75m0 0H3.75m0 0h-.375a1.125 1.125 0 0 1-1.125-1.125V15m1.5 1.5v-.75A.75.75 0 0 0 3 15h-.75M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm3 0h.008v.008H18V10.5Zm-12 0h.008v.008H6V10.5Z"
        ]
    ]

/// Credit card
let creditCard (size: IconSize) (color: IconColor) =
    svgIcon size color [
        Svg.path [
            svg.strokeLineCap "round"
            svg.strokeLineJoin "round"
            svg.d "M2.25 8.25h19.5M2.25 9h19.5m-16.5 5.25h6m-6 2.25h3m-3.75 3h15a2.25 2.25 0 0 0 2.25-2.25V6.75A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25v10.5A2.25 2.25 0 0 0 4.5 19.5Z"
        ]
    ]

// ============================================
// Spinner/Loading Icon
// ============================================

/// Animated loading spinner
let spinner (size: IconSize) (color: IconColor) =
    Svg.svg [
        svg.className $"{sizeToClass size} {colorToClass color} inline-block flex-shrink-0 animate-spin"
        svg.fill "none"
        svg.viewBox (0, 0, 24, 24)
        svg.children [
            Svg.circle [
                svg.className "opacity-25"
                svg.cx 12
                svg.cy 12
                svg.r 10
                svg.stroke "currentColor"
                svg.strokeWidth 4
            ]
            Svg.path [
                svg.className "opacity-75"
                svg.fill "currentColor"
                svg.d "M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            ]
        ]
    ]

// ============================================
// Emoji Fallback Icons (for nav items)
// ============================================

/// Emoji icon wrapper for consistent sizing
let emoji (size: IconSize) (color: IconColor) (emojiChar: string) =
    Html.span [
        prop.className $"{sizeToTextClass size} {colorToClass color} inline-flex items-center justify-center"
        prop.text emojiChar
    ]

// Common emoji shortcuts (for quick prototyping)
let emojiDashboard size color = emoji size color "📊"
let emojiSync size color = emoji size color "🔄"
let emojiRules size color = emoji size color "📋"
let emojiSettings size color = emoji size color "⚙️"
let emojiSuccess size color = emoji size color "✅"
let emojiWarning size color = emoji size color "⚠️"
let emojiError size color = emoji size color "❌"
