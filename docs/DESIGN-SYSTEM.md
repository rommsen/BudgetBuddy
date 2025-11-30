# BudgetBuddy Design System

## Neon Glow Dark Mode Theme

A modern, sleek dark mode design system with vibrant neon accent colors. Mobile-first approach with the depth of dark interfaces and the energy of glowing neon colors - green, orange, and teal.

---

## 1. Design Philosophy

### Core Principles

1. **Mobile-First**: Design for phone screens first, enhance for larger screens
2. **Dark-First**: Deep, rich dark backgrounds that make neon colors pop
3. **Neon Energy**: Vibrant glowing accents that bring life and fun to financial data
4. **Glassmorphism**: Frosted glass effects for depth and modern appeal
5. **Micro-interactions**: Subtle animations and hover effects for engagement

### Mood

- **Sleek & Professional**: Clean lines, generous whitespace
- **Fun & Energetic**: Neon glows, playful animations
- **Trustworthy**: Consistent, polished, no harsh surprises

---

## 2. Color Palette

### Dark Backgrounds (Base Colors)

```css
/* Deep space backgrounds */
--bg-void: #0a0a0f;        /* Deepest background - app shell */
--bg-dark: #0f1117;        /* Primary surface - cards */
--bg-surface: #161922;     /* Elevated surface - modals */
--bg-elevated: #1c1f2a;    /* Higher elevation - dropdowns */
--bg-subtle: #252836;      /* Subtle backgrounds - inputs */
```

### Neon Accent Colors

```css
/* Primary: Neon Green - Success, Growth, Positive */
--neon-green: #00ff88;
--neon-green-dim: #00cc6a;
--neon-green-glow: rgba(0, 255, 136, 0.5);
--neon-green-subtle: rgba(0, 255, 136, 0.1);

/* Secondary: Electric Orange - Energy, Action, Alerts */
--neon-orange: #ff6b2c;
--neon-orange-dim: #e55a1f;
--neon-orange-glow: rgba(255, 107, 44, 0.5);
--neon-orange-subtle: rgba(255, 107, 44, 0.1);

/* Accent: Cyber Teal - Info, Navigation, Interactive */
--neon-teal: #00d4aa;
--neon-teal-dim: #00b894;
--neon-teal-glow: rgba(0, 212, 170, 0.5);
--neon-teal-subtle: rgba(0, 212, 170, 0.1);

/* Supporting: Electric Purple - Special, Premium */
--neon-purple: #a855f7;
--neon-purple-dim: #9333ea;
--neon-purple-glow: rgba(168, 85, 247, 0.5);
--neon-purple-subtle: rgba(168, 85, 247, 0.1);

/* Warning: Hot Pink - Attention needed */
--neon-pink: #ff2e97;
--neon-pink-dim: #e6177f;
--neon-pink-glow: rgba(255, 46, 151, 0.5);
--neon-pink-subtle: rgba(255, 46, 151, 0.1);

/* Error: Neon Red */
--neon-red: #ff3b5c;
--neon-red-dim: #e6324f;
--neon-red-glow: rgba(255, 59, 92, 0.5);
--neon-red-subtle: rgba(255, 59, 92, 0.1);
```

### Text Colors

```css
--text-primary: #f0f2f5;      /* Primary text - high emphasis */
--text-secondary: #9ca3af;     /* Secondary text - medium emphasis */
--text-muted: #6b7280;         /* Muted text - low emphasis */
--text-disabled: #4b5563;      /* Disabled state */
--text-inverse: #0a0a0f;       /* Text on bright backgrounds */
```

### Semantic Mapping

| Purpose | Color | Usage |
|---------|-------|-------|
| Success/Income | Neon Green | Positive amounts, completed states |
| Action/CTA | Neon Orange | Primary buttons, important actions |
| Navigation/Info | Neon Teal | Links, active states, information |
| Special/Premium | Neon Purple | Badges, special features |
| Warning | Neon Pink | Needs attention, uncategorized |
| Error/Expense | Neon Red | Errors, negative amounts |

---

## 3. Typography

### Font Stack

```css
/* UI Text - Geometric, techy feel */
--font-sans: 'Outfit', 'Exo 2', system-ui, sans-serif;

/* Display/Headers - Futuristic */
--font-display: 'Orbitron', 'Rajdhani', sans-serif;

/* Monospace - Numbers and code */
--font-mono: 'JetBrains Mono', 'Fira Code', monospace;
```

### Type Scale (Mobile-First)

```css
/* Mobile base sizes */
--text-5xl: 2rem;      /* 32px - Hero headers (mobile) */
--text-4xl: 1.75rem;   /* 28px - Page titles (mobile) */
--text-3xl: 1.5rem;    /* 24px - Section headers */
--text-2xl: 1.25rem;   /* 20px - Card titles */
--text-xl: 1.125rem;   /* 18px - Subheadings */

/* Body */
--text-lg: 1rem;       /* 16px - Large body */
--text-base: 0.9375rem; /* 15px - Body text (mobile optimized) */
--text-sm: 0.875rem;   /* 14px - Small text */
--text-xs: 0.75rem;    /* 12px - Labels, captions */

/* Desktop scale-up (md breakpoint+) */
@media (min-width: 768px) {
  --text-5xl: 3rem;      /* 48px */
  --text-4xl: 2.25rem;   /* 36px */
  --text-3xl: 1.875rem;  /* 30px */
  --text-2xl: 1.5rem;    /* 24px */
  --text-base: 1rem;     /* 16px */
}
```

### Font Weights

- **400 Regular**: Body text
- **500 Medium**: Emphasized body, labels
- **600 Semibold**: Headings, important text
- **700 Bold**: Strong emphasis, buttons

---

## 4. Spacing System (Mobile-First)

Using an 8px base unit, with tighter spacing on mobile:

```css
/* Mobile spacing */
--space-1: 0.25rem;   /* 4px */
--space-2: 0.5rem;    /* 8px */
--space-3: 0.75rem;   /* 12px */
--space-4: 1rem;      /* 16px */
--space-5: 1.25rem;   /* 20px */
--space-6: 1.5rem;    /* 24px */
--space-8: 2rem;      /* 32px */

/* Desktop spacing (md+) */
@media (min-width: 768px) {
  --space-8: 2.5rem;   /* 40px */
  --space-10: 3rem;    /* 48px */
  --space-12: 4rem;    /* 64px */
}
```

---

## 5. Border & Radius

### Border Radius

```css
--radius-sm: 0.375rem;   /* 6px - Small elements */
--radius-md: 0.5rem;     /* 8px - Buttons, inputs */
--radius-lg: 0.75rem;    /* 12px - Cards */
--radius-xl: 1rem;       /* 16px - Modals, large cards */
--radius-2xl: 1.5rem;    /* 24px - Hero sections */
--radius-full: 9999px;   /* Circles, pills */
```

### Borders

```css
/* Subtle borders for dark mode */
--border-subtle: 1px solid rgba(255, 255, 255, 0.05);
--border-default: 1px solid rgba(255, 255, 255, 0.1);
--border-strong: 1px solid rgba(255, 255, 255, 0.15);

/* Neon borders for emphasis */
--border-glow-green: 1px solid var(--neon-green);
--border-glow-orange: 1px solid var(--neon-orange);
--border-glow-teal: 1px solid var(--neon-teal);
```

---

## 6. Shadows & Glow Effects

### Standard Shadows

```css
/* Subtle elevation shadows */
--shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.5);
--shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.4);
--shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.4);
--shadow-xl: 0 20px 25px -5px rgba(0, 0, 0, 0.5);
```

### Neon Glow Effects

```css
/* Neon glow shadows - use for focused/active states */
--glow-green-sm: 0 0 10px var(--neon-green-glow);
--glow-green-md: 0 0 20px var(--neon-green-glow), 0 0 40px var(--neon-green-glow);
--glow-green-lg: 0 0 30px var(--neon-green-glow), 0 0 60px var(--neon-green-glow), 0 0 90px var(--neon-green-glow);

--glow-orange-sm: 0 0 10px var(--neon-orange-glow);
--glow-orange-md: 0 0 20px var(--neon-orange-glow), 0 0 40px var(--neon-orange-glow);
--glow-orange-lg: 0 0 30px var(--neon-orange-glow), 0 0 60px var(--neon-orange-glow), 0 0 90px var(--neon-orange-glow);

--glow-teal-sm: 0 0 10px var(--neon-teal-glow);
--glow-teal-md: 0 0 20px var(--neon-teal-glow), 0 0 40px var(--neon-teal-glow);
--glow-teal-lg: 0 0 30px var(--neon-teal-glow), 0 0 60px var(--neon-teal-glow), 0 0 90px var(--neon-teal-glow);

/* Text glow for neon text */
--text-glow-green: 0 0 10px var(--neon-green), 0 0 20px var(--neon-green), 0 0 30px var(--neon-green-glow);
--text-glow-orange: 0 0 10px var(--neon-orange), 0 0 20px var(--neon-orange), 0 0 30px var(--neon-orange-glow);
--text-glow-teal: 0 0 10px var(--neon-teal), 0 0 20px var(--neon-teal), 0 0 30px var(--neon-teal-glow);
```

---

## 7. Mobile-First Component Specifications

### 7.1 Cards

**Standard Card**
```css
.card {
  background: var(--bg-dark);
  border: var(--border-subtle);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-md);
  padding: 1rem;           /* Mobile: tighter padding */
  transition: all 0.2s ease;
}

@media (min-width: 768px) {
  .card {
    border-radius: var(--radius-xl);
    padding: 1.5rem;       /* Desktop: more breathing room */
  }
}

.card:hover {
  border-color: rgba(255, 255, 255, 0.15);
  box-shadow: var(--shadow-lg);
  transform: translateY(-2px);
}
```

**Glass Card**
```css
.glass-card {
  background: rgba(22, 25, 34, 0.8);
  backdrop-filter: blur(20px);
  border: var(--border-subtle);
  border-radius: var(--radius-lg);
}
```

**Glowing Card (Featured)**
```css
.card-glow {
  background: var(--bg-dark);
  border: 1px solid var(--neon-teal-dim);
  border-radius: var(--radius-lg);
  box-shadow: var(--glow-teal-sm);
  transition: all 0.3s ease;
}

.card-glow:hover {
  box-shadow: var(--glow-teal-md);
}
```

### 7.2 Buttons

**Primary Button (Neon Orange) - Mobile-First**
```css
.btn-primary {
  background: linear-gradient(135deg, var(--neon-orange) 0%, var(--neon-orange-dim) 100%);
  color: var(--text-inverse);
  font-weight: 600;
  padding: 0.875rem 1.25rem;  /* Mobile: larger touch target */
  border-radius: var(--radius-md);
  border: none;
  box-shadow: var(--glow-orange-sm);
  transition: all 0.2s ease;
  width: 100%;                 /* Mobile: full width by default */
  min-height: 48px;            /* Mobile: minimum touch target */
}

@media (min-width: 768px) {
  .btn-primary {
    width: auto;
    padding: 0.75rem 1.5rem;
    min-height: auto;
  }
}

.btn-primary:hover {
  box-shadow: var(--glow-orange-md);
  transform: translateY(-1px);
}

.btn-primary:active {
  transform: scale(0.98);
}
```

**Secondary Button (Neon Teal outline)**
```css
.btn-secondary {
  background: transparent;
  color: var(--neon-teal);
  font-weight: 500;
  padding: 0.875rem 1.25rem;
  border-radius: var(--radius-md);
  border: 1px solid var(--neon-teal);
  width: 100%;
  min-height: 48px;
  transition: all 0.2s ease;
}

@media (min-width: 768px) {
  .btn-secondary {
    width: auto;
    padding: 0.75rem 1.5rem;
    min-height: auto;
  }
}

.btn-secondary:hover {
  background: var(--neon-teal-subtle);
  box-shadow: var(--glow-teal-sm);
}
```

**Ghost Button**
```css
.btn-ghost {
  background: transparent;
  color: var(--text-secondary);
  padding: 0.75rem 1rem;
  border-radius: var(--radius-md);
  border: none;
  transition: all 0.2s ease;
}

.btn-ghost:hover {
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-primary);
}
```

### 7.3 Inputs (Mobile-Optimized)

```css
.input {
  background: var(--bg-subtle);
  color: var(--text-primary);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: var(--radius-md);
  padding: 0.875rem 1rem;    /* Mobile: larger touch target */
  font-size: 16px;            /* Prevents iOS zoom on focus */
  min-height: 48px;
  width: 100%;
  transition: all 0.2s ease;
}

.input:focus {
  border-color: var(--neon-teal);
  box-shadow: var(--glow-teal-sm);
  outline: none;
}

.input::placeholder {
  color: var(--text-muted);
}
```

### 7.4 Status Badges

```css
/* Success/Imported */
.badge-success {
  background: var(--neon-green-subtle);
  color: var(--neon-green);
  border: 1px solid var(--neon-green-dim);
  padding: 0.25rem 0.75rem;
  border-radius: var(--radius-full);
  font-size: var(--text-xs);
  font-weight: 500;
}

/* Warning/Review Needed */
.badge-warning {
  background: var(--neon-pink-subtle);
  color: var(--neon-pink);
  border: 1px solid var(--neon-pink-dim);
}

/* Info/Auto-categorized */
.badge-info {
  background: var(--neon-teal-subtle);
  color: var(--neon-teal);
  border: 1px solid var(--neon-teal-dim);
}

/* Error/Pending */
.badge-error {
  background: var(--neon-red-subtle);
  color: var(--neon-red);
  border: 1px solid var(--neon-red-dim);
}
```

### 7.5 Money Display

```css
/* Positive amounts (income) - with glow */
.money-positive {
  color: var(--neon-green);
  font-family: var(--font-mono);
  font-weight: 600;
  text-shadow: var(--text-glow-green);
}

/* Negative amounts (expenses) */
.money-negative {
  color: var(--neon-red);
  font-family: var(--font-mono);
  font-weight: 600;
}
```

### 7.6 Navigation (Mobile-First)

**Mobile Bottom Navigation**
```css
.mobile-nav {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  background: rgba(15, 17, 23, 0.95);
  backdrop-filter: blur(20px);
  border-top: var(--border-subtle);
  padding: 0.5rem;
  padding-bottom: calc(0.5rem + env(safe-area-inset-bottom));
  z-index: 50;
}

.mobile-nav-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
  padding: 0.5rem;
  color: var(--text-muted);
  font-size: var(--text-xs);
  transition: all 0.2s ease;
}

.mobile-nav-item.active {
  color: var(--neon-teal);
}
```

**Desktop Top Navbar (hidden on mobile)**
```css
@media (min-width: 768px) {
  .navbar {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    background: rgba(15, 17, 23, 0.9);
    backdrop-filter: blur(20px);
    border-bottom: var(--border-subtle);
    padding: 0.5rem 1.5rem;
    z-index: 50;
  }

  .mobile-nav {
    display: none;
  }
}
```

**Nav Item (Desktop)**
```css
.nav-item {
  color: var(--text-secondary);
  padding: 0.75rem 1rem;
  border-radius: var(--radius-md);
  transition: all 0.2s ease;
}

.nav-item:hover {
  color: var(--text-primary);
  background: rgba(255, 255, 255, 0.05);
}

.nav-item.active {
  color: var(--neon-teal);
  background: var(--neon-teal-subtle);
}
```

### 7.7 Stats Cards (Mobile-First)

```css
.stat-card {
  background: var(--bg-dark);
  border: var(--border-subtle);
  border-radius: var(--radius-lg);
  padding: 1rem;
  position: relative;
  overflow: hidden;
}

@media (min-width: 768px) {
  .stat-card {
    padding: 1.5rem;
  }
}

/* Decorative gradient accent */
.stat-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  background: linear-gradient(90deg, var(--neon-teal), var(--neon-green), var(--neon-orange));
}

.stat-value {
  font-family: var(--font-mono);
  font-size: 1.5rem;        /* Mobile */
  font-weight: 700;
  color: var(--text-primary);
}

@media (min-width: 768px) {
  .stat-value {
    font-size: 2rem;        /* Desktop */
  }
}

.stat-label {
  font-size: var(--text-xs);
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--text-muted);
}
```

### 7.8 Toast Notifications

```css
.toast {
  background: var(--bg-elevated);
  border: var(--border-default);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-xl);
  margin: 0.5rem;            /* Mobile: margin from edges */
}

.toast-success {
  border-left: 3px solid var(--neon-green);
}

.toast-error {
  border-left: 3px solid var(--neon-red);
}

.toast-warning {
  border-left: 3px solid var(--neon-orange);
}
```

---

## 8. Mobile-First Layout Patterns

### Container
```css
.container {
  width: 100%;
  padding-left: 1rem;
  padding-right: 1rem;
  margin-left: auto;
  margin-right: auto;
}

@media (min-width: 640px) {
  .container {
    max-width: 640px;
    padding-left: 1.5rem;
    padding-right: 1.5rem;
  }
}

@media (min-width: 768px) {
  .container {
    max-width: 768px;
  }
}

@media (min-width: 1024px) {
  .container {
    max-width: 1024px;
  }
}

@media (min-width: 1280px) {
  .container {
    max-width: 1280px;
  }
}
```

### Page Layout
```css
/* Mobile: bottom nav, so content needs bottom padding */
.page-content {
  padding-top: 1rem;
  padding-bottom: calc(5rem + env(safe-area-inset-bottom)); /* Space for bottom nav */
  min-height: 100vh;
}

/* Desktop: top nav, so content needs top padding */
@media (min-width: 768px) {
  .page-content {
    padding-top: 5rem;       /* Space for top navbar */
    padding-bottom: 2rem;
  }
}
```

### Grid System
```css
/* Mobile: single column */
.grid-cards {
  display: grid;
  gap: 0.75rem;
  grid-template-columns: 1fr;
}

/* Tablet: 2 columns */
@media (min-width: 640px) {
  .grid-cards {
    gap: 1rem;
    grid-template-columns: repeat(2, 1fr);
  }
}

/* Desktop: 3 columns */
@media (min-width: 1024px) {
  .grid-cards {
    gap: 1.5rem;
    grid-template-columns: repeat(3, 1fr);
  }
}
```

---

## 9. Animation Guidelines

### Transitions

```css
--transition-fast: 150ms ease;
--transition-base: 200ms ease;
--transition-slow: 300ms ease;
--transition-spring: 500ms cubic-bezier(0.34, 1.56, 0.64, 1);
```

### Keyframe Animations

**Fade In**
```css
@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}
```

**Slide Up**
```css
@keyframes slideUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
```

**Scale In**
```css
@keyframes scaleIn {
  from {
    opacity: 0;
    transform: scale(0.95);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}
```

**Neon Pulse (for loading states)**
```css
@keyframes neonPulse {
  0%, 100% {
    box-shadow: 0 0 10px var(--neon-teal-glow);
  }
  50% {
    box-shadow: 0 0 25px var(--neon-teal-glow), 0 0 50px var(--neon-teal-glow);
  }
}
```

**Gradient Flow (background animation)**
```css
@keyframes gradientFlow {
  0% { background-position: 0% 50%; }
  50% { background-position: 100% 50%; }
  100% { background-position: 0% 50%; }
}

.animated-gradient {
  background: linear-gradient(-45deg,
    var(--neon-teal),
    var(--neon-green),
    var(--neon-purple),
    var(--neon-orange));
  background-size: 400% 400%;
  animation: gradientFlow 15s ease infinite;
}
```

---

## 10. DaisyUI Theme Configuration

Update `tailwind.config.js`:

```javascript
module.exports = {
  content: ['./src/Client/**/*.{fs,html}'],
  theme: {
    extend: {
      colors: {
        'neon-green': '#00ff88',
        'neon-orange': '#ff6b2c',
        'neon-teal': '#00d4aa',
        'neon-purple': '#a855f7',
        'neon-pink': '#ff2e97',
        'neon-red': '#ff3b5c',
      },
      fontFamily: {
        sans: ['Outfit', 'Exo 2', 'system-ui', 'sans-serif'],
        display: ['Orbitron', 'Rajdhani', 'sans-serif'],
        mono: ['JetBrains Mono', 'Fira Code', 'monospace'],
      },
      boxShadow: {
        'glow-green': '0 0 20px rgba(0, 255, 136, 0.5)',
        'glow-orange': '0 0 20px rgba(255, 107, 44, 0.5)',
        'glow-teal': '0 0 20px rgba(0, 212, 170, 0.5)',
        'glow-purple': '0 0 20px rgba(168, 85, 247, 0.5)',
      },
    },
  },
  plugins: [require('daisyui')],
  daisyui: {
    themes: [
      {
        neon: {
          // Base colors
          'primary': '#ff6b2c',          // Neon Orange
          'primary-content': '#0a0a0f',   // Dark text on primary
          'secondary': '#00d4aa',         // Neon Teal
          'secondary-content': '#0a0a0f',
          'accent': '#a855f7',            // Neon Purple
          'accent-content': '#0a0a0f',
          'neutral': '#1c1f2a',
          'neutral-content': '#f0f2f5',

          // Background colors
          'base-100': '#0f1117',          // Primary surface
          'base-200': '#161922',          // Secondary surface
          'base-300': '#1c1f2a',          // Tertiary surface
          'base-content': '#f0f2f5',      // Text on base

          // Semantic colors
          'info': '#00d4aa',              // Teal
          'info-content': '#0a0a0f',
          'success': '#00ff88',           // Neon Green
          'success-content': '#0a0a0f',
          'warning': '#ff2e97',           // Neon Pink
          'warning-content': '#0a0a0f',
          'error': '#ff3b5c',             // Neon Red
          'error-content': '#ffffff',

          // Component styling
          '--rounded-box': '0.75rem',
          '--rounded-btn': '0.5rem',
          '--rounded-badge': '9999px',
          '--animation-btn': '0.2s',
          '--animation-input': '0.2s',
          '--btn-focus-scale': '0.98',
        },
      },
    ],
    darkTheme: 'neon',
  },
}
```

---

## 11. CSS Implementation (styles.css)

```css
@import "tailwindcss";

/* ============================================
   BudgetBuddy - Neon Glow Dark Theme
   Mobile-First Design System
   ============================================ */

/* Google Fonts Import */
@import url('https://fonts.googleapis.com/css2?family=Outfit:wght@400;500;600;700&family=Orbitron:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500;600&display=swap');

/* ============================================
   CSS Custom Properties
   ============================================ */
:root {
  /* Neon Colors */
  --neon-green: #00ff88;
  --neon-green-dim: #00cc6a;
  --neon-green-glow: rgba(0, 255, 136, 0.5);
  --neon-orange: #ff6b2c;
  --neon-orange-dim: #e55a1f;
  --neon-orange-glow: rgba(255, 107, 44, 0.5);
  --neon-teal: #00d4aa;
  --neon-teal-dim: #00b894;
  --neon-teal-glow: rgba(0, 212, 170, 0.5);
  --neon-purple: #a855f7;
  --neon-purple-glow: rgba(168, 85, 247, 0.5);
  --neon-pink: #ff2e97;
  --neon-pink-glow: rgba(255, 46, 151, 0.5);
  --neon-red: #ff3b5c;
  --neon-red-glow: rgba(255, 59, 92, 0.5);

  /* Typography */
  --font-sans: 'Outfit', system-ui, sans-serif;
  --font-display: 'Orbitron', sans-serif;
  --font-mono: 'JetBrains Mono', monospace;

  /* Transitions */
  --transition-fast: 150ms ease;
  --transition-base: 200ms ease;
  --transition-slow: 300ms ease;
}

/* ============================================
   Base Styles
   ============================================ */
html {
  font-family: var(--font-sans);
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  /* Prevent iOS text size adjustment */
  -webkit-text-size-adjust: 100%;
}

body {
  background: linear-gradient(180deg, #0a0a0f 0%, #0f1117 100%);
  min-height: 100vh;
  /* Smooth scrolling on iOS */
  -webkit-overflow-scrolling: touch;
}

h1, h2, h3, h4, h5, h6 {
  font-family: var(--font-display);
  font-weight: 600;
  letter-spacing: 0.02em;
}

.font-mono {
  font-family: var(--font-mono);
}

/* ============================================
   Neon Glow Utilities
   ============================================ */
.glow-green {
  box-shadow: 0 0 20px var(--neon-green-glow);
}

.glow-orange {
  box-shadow: 0 0 20px var(--neon-orange-glow);
}

.glow-teal {
  box-shadow: 0 0 20px var(--neon-teal-glow);
}

.glow-purple {
  box-shadow: 0 0 20px var(--neon-purple-glow);
}

.text-glow-green {
  text-shadow: 0 0 10px var(--neon-green), 0 0 20px var(--neon-green-glow);
}

.text-glow-orange {
  text-shadow: 0 0 10px var(--neon-orange), 0 0 20px var(--neon-orange-glow);
}

.text-glow-teal {
  text-shadow: 0 0 10px var(--neon-teal), 0 0 20px var(--neon-teal-glow);
}

/* Gradient text effect */
.gradient-text {
  background: linear-gradient(135deg, var(--neon-teal), var(--neon-green), var(--neon-orange));
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

/* ============================================
   Component Overrides
   ============================================ */

/* Cards with subtle border glow on hover */
.card {
  border: 1px solid rgba(255, 255, 255, 0.05);
  transition: all var(--transition-base);
}

.card:hover {
  border-color: rgba(255, 255, 255, 0.1);
  transform: translateY(-2px);
}

/* Primary buttons with neon glow */
.btn-primary {
  background: linear-gradient(135deg, var(--neon-orange) 0%, var(--neon-orange-dim) 100%);
  border: none;
  box-shadow: 0 0 15px var(--neon-orange-glow);
  transition: all var(--transition-base);
}

.btn-primary:hover {
  box-shadow: 0 0 25px var(--neon-orange-glow), 0 0 50px var(--neon-orange-glow);
  transform: translateY(-1px);
}

.btn-primary:active {
  transform: scale(0.98);
}

/* Secondary buttons with teal accent */
.btn-secondary {
  background: transparent;
  border: 1px solid var(--neon-teal);
  color: var(--neon-teal);
}

.btn-secondary:hover {
  background: rgba(0, 212, 170, 0.1);
  box-shadow: 0 0 15px var(--neon-teal-glow);
}

/* Input focus states */
.input:focus,
.select:focus,
.textarea:focus {
  border-color: var(--neon-teal);
  box-shadow: 0 0 0 2px var(--neon-teal-glow);
  outline: none;
}

/* Prevent iOS zoom on input focus */
input, select, textarea {
  font-size: 16px;
}

/* Money display */
.money-positive {
  color: var(--neon-green);
  font-family: var(--font-mono);
  font-weight: 600;
}

.money-negative {
  color: var(--neon-red);
  font-family: var(--font-mono);
  font-weight: 600;
}

/* Navbar with glass effect */
.navbar {
  background: rgba(15, 17, 23, 0.85);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

/* Stat cards with accent line */
.stat {
  position: relative;
}

.stat::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  background: linear-gradient(90deg, var(--neon-teal), var(--neon-green), var(--neon-orange));
  opacity: 0;
  transition: opacity var(--transition-base);
}

.stat:hover::before {
  opacity: 1;
}

/* Safe area padding for mobile devices */
.safe-area-pb {
  padding-bottom: env(safe-area-inset-bottom);
}

.safe-area-pt {
  padding-top: env(safe-area-inset-top);
}

/* ============================================
   Animations
   ============================================ */
.animate-fade-in {
  animation: fadeIn 0.3s ease forwards;
}

.animate-slide-up {
  animation: slideUp 0.5s cubic-bezier(0.34, 1.56, 0.64, 1) forwards;
}

.animate-scale-in {
  animation: scaleIn 0.3s cubic-bezier(0.34, 1.56, 0.64, 1) forwards;
}

.animate-neon-pulse {
  animation: neonPulse 2s ease-in-out infinite;
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

@keyframes slideUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes scaleIn {
  from {
    opacity: 0;
    transform: scale(0.95);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

@keyframes neonPulse {
  0%, 100% {
    box-shadow: 0 0 10px var(--neon-teal-glow);
  }
  50% {
    box-shadow: 0 0 25px var(--neon-teal-glow), 0 0 50px var(--neon-teal-glow);
  }
}

/* Animated gradient background */
.gradient-bg {
  background: linear-gradient(-45deg, #0a0a0f, var(--neon-teal-dim), #0a0a0f, var(--neon-purple));
  background-size: 400% 400%;
  animation: gradientFlow 15s ease infinite;
}

@keyframes gradientFlow {
  0% { background-position: 0% 50%; }
  50% { background-position: 100% 50%; }
  100% { background-position: 0% 50%; }
}

/* Loading spinner with neon color */
.loading-spinner {
  border-color: var(--neon-teal) transparent transparent transparent;
}

/* ============================================
   Scrollbar (Desktop only - hidden on mobile)
   ============================================ */
@media (min-width: 768px) {
  ::-webkit-scrollbar {
    width: 8px;
    height: 8px;
  }

  ::-webkit-scrollbar-track {
    background: #161922;
    border-radius: 999px;
  }

  ::-webkit-scrollbar-thumb {
    background: #252836;
    border-radius: 999px;
  }

  ::-webkit-scrollbar-thumb:hover {
    background: #363a4a;
  }
}
```

---

## 12. Icon Guidelines

Use simple, clean icons. Recommended: Heroicons or Phosphor Icons (outline style).

For emoji icons (current approach), consider migrating to SVG icons for:
- Better scaling
- Color customization with neon colors
- Consistency across platforms

**Suggested Icon Colors:**
- Default: `var(--text-secondary)`
- Hover: `var(--text-primary)`
- Active: `var(--neon-teal)`
- Success: `var(--neon-green)`
- Warning: `var(--neon-orange)`
- Error: `var(--neon-red)`

---

## 13. Implementation Checklist

### Phase 1: Foundation
- [ ] Update `tailwind.config.js` with new theme and fonts
- [ ] Replace `styles.css` with neon theme styles
- [ ] Update `index.html` to use `data-theme="neon"`
- [ ] Test on mobile device (or Chrome DevTools mobile emulation)

### Phase 2: Components (Mobile-First)
- [ ] Update `View.fs` (main layout, mobile bottom nav, desktop top nav)
- [ ] Update `Dashboard/View.fs` (stats cards, action card - stack on mobile)
- [ ] Update `SyncFlow/View.fs` (transaction cards - full width on mobile)
- [ ] Update `Rules/View.fs` (rule cards, modal - full screen on mobile)
- [ ] Update `Settings/View.fs` (form inputs - full width, larger touch targets)

### Phase 3: Polish
- [ ] Add neon glow effects to interactive elements
- [ ] Implement hover state animations (desktop)
- [ ] Implement active/tap states (mobile)
- [ ] Add loading state animations with neon pulse
- [ ] Test on actual mobile device

### Phase 4: Enhancement
- [ ] Add subtle grid/scanline background effect (optional)
- [ ] Add micro-interactions on button presses
- [ ] Add haptic feedback hints in CSS (optional)

---

## 14. Example Component Updates (Mobile-First F#)

### Before (Current Style)
```fsharp
Html.div [
    prop.className "card bg-base-100 shadow-lg"
    prop.children [ ... ]
]
```

### After (Neon Theme - Mobile-First)
```fsharp
Html.div [
    prop.className "card bg-base-100 border border-white/5 p-4 md:p-6 hover:border-white/10 hover:shadow-glow-teal/20 transition-all"
    prop.children [ ... ]
]
```

### Button Example (Mobile-First)
```fsharp
// Primary action button - full width on mobile, auto on desktop
Html.button [
    prop.className "btn btn-primary w-full md:w-auto min-h-[48px] md:min-h-0 shadow-glow-orange hover:shadow-glow-orange/80"
    prop.text "Start Sync"
]

// Secondary outline button
Html.button [
    prop.className "btn btn-ghost w-full md:w-auto border border-neon-teal text-neon-teal hover:bg-neon-teal/10 hover:shadow-glow-teal"
    prop.text "Cancel"
]
```

### Money Display
```fsharp
Html.span [
    prop.className (
        "font-mono font-semibold text-lg md:text-xl " +
        if amount < 0m then "text-neon-red" else "text-neon-green text-glow-green"
    )
    prop.text (formatAmount amount)
]
```

### Page Header (Mobile-First)
```fsharp
Html.div [
    prop.className "space-y-1 mb-4 md:mb-6"
    prop.children [
        Html.h1 [
            prop.className "text-xl md:text-3xl font-bold font-display"
            prop.text "Dashboard"
        ]
        Html.p [
            prop.className "text-sm md:text-base text-base-content/60"
            prop.text "Welcome back! Here's your sync overview."
        ]
    ]
]
```

### Stats Grid (Mobile-First)
```fsharp
Html.div [
    prop.className "grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 md:gap-4"
    prop.children [
        // Stats cards...
    ]
]
```

---

This mobile-first design system gives BudgetBuddy a modern, sleek, and fun visual identity optimized for phone use while scaling beautifully to desktop. The neon glow effects add energy, and the Outfit + Orbitron font combination gives it a techy, futuristic feel.
