# BudgetBuddy app icons

Produced by `design-system-008` (the **mark + colors** decision). The **wiring**
(manifest, `index.html` meta-tags, generator config) belongs to **`infra-002`** — do not
edit `index.html` here.

## Mark concept (locked)

"B im Sync-Ring" (Hybrid): a bold **B** (identity = BudgetBuddy) centred in a **sync ring**
of two arrowheads (function = Bank→YNAB; echoes `Icons.sync`). Signature gradient
`linear-gradient(135deg, #00d4aa 0%, #00ff88 50%, #ff6b2c 100%)` (= `.gradient-text`) plus a
subtle neon glow. At ≤32px a **simplified** variant is used: solid `#00ff88` B, no ring, no
gradient (legibility at 16/32px).

## Theme / background color (for the manifest + meta-tags)

```
theme_color      = #08081a
background_color  = #08081a
```

`#08081a` is `--bg-app` (the real app surface). This supersedes the generic `#0f172a` slate
currently in `index.html` — the correction is `infra-002`'s job.

## Source SVGs (in ../)

| File | Purpose |
|------|---------|
| `../icon-master.svg`   | Full Hybrid mark, gradient + glow, transparent. Source for 192/512/apple-touch. |
| `../icon-maskable.svg` | Full Hybrid on **opaque `#08081a`**, scaled to 76% (≥20% maskable safe-zone). Source for maskable-512. |
| `../favicon.svg`       | Simplified solid `#00ff88` B, no ring/gradient. Source for favicon-16/32 + `.ico`. |

A PWA-assets generator scales **one** source uniformly (it does not simplify), so the
favicon must point at `favicon.svg`, the maskable at `icon-maskable.svg`, the rest at
`icon-master.svg`.

## Generated raster set (this dir)

| File | Size | Background | Manifest `purpose` |
|------|------|-----------|--------------------|
| `icon-192.png`        | 192×192 | transparent | `any` |
| `icon-512.png`        | 512×512 | transparent | `any` |
| `maskable-512.png`    | 512×512 | opaque `#08081a` | `maskable` |
| `apple-touch-icon.png`| 180×180 | opaque `#08081a` | apple-touch (iOS ignores transparency) |
| `favicon-32.png`      | 32×32   | transparent | favicon |
| `favicon-16.png`      | 16×16   | transparent | favicon |
| `favicon.ico`         | 16+32   | transparent | classic favicon |

## Regenerating

No native rasterizer is required; PNGs were produced with `sharp` via
`npx sharp-cli` (and `png-to-ico` for the `.ico`). `infra-002` may instead adopt
`@vite-pwa/assets-generator` with the two-source mapping above — either is fine; the
**SVG sources and the two color values are the contract**, the raster files are derivable.
