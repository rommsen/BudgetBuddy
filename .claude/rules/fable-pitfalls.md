---
paths:
  - "src/Shared/**"
  - "src/Client/**"
  - "src/Server/**"
---

# Fable.Remoting & Feliz Pitfalls

## Fable.Remoting

### Tuple-Parameter Serialisierung
- Richtig: `serialize [| (value1, value2, value3) :> obj |]`
- Falsch: `serialize [| box value1; box value2; box value3 |]`
- Ein Tuple ist EIN Parameter, nicht N Parameter

### DateTimeOffset
- Nie `DateTimeOffset(someDateTime)` — nutzt lokale Timezone
- Immer `DateTimeOffset(date, TimeSpan.Zero)` für UTC
- Client-Input immer `.ToUniversalTime()` vor DB-Write

## Feliz

### style.lineHeight
- `style.lineHeight 1.5` erzeugt `1.5px` (nicht unitless) — nutze Tailwind `leading-*`

### prop.max
- Hat keine String-Overloads — nutze `prop.custom("max", value)`

### sprintf mit Nachkommastellen
- `sprintf "%.2f"` rundet in Fable anders als in .NET; für Beträge `System.Math.Round(x, 2).ToString("0.00")` nutzen

### Math.Round auf decimal
- Im Fable-Kompilat rundet `Math.Round` auf decimal immer zur geraden Zahl, `MidpointRounding` wird verworfen
  (fable-library `Decimal.round` nutzt fest ROUND_HALF_EVEN). Als zweites Argument gilt der Modus sogar als
  Stellenzahl: `Math.Round(x, MidpointRounding.AwayFromZero)` rundet auf eine Nachkommastelle (budgeting-q4v8n).
- Kaufmännisch in Code, der im Client läuft (`src/Shared`, `src/Client`): `Shared.Domain.roundHalfUp`, auf Cent
  `roundHalfUp (x * 100m) / 100m`. `Math.Round` mit Modus nur in Code, der allein unter .NET läuft (`toMilliunits`).
- Auch unter .NET rundet `Math.Round` auf decimal ohne Modus zur geraden Zahl (`MidpointRounding.ToEven` ist der
  Standard): `Math.Round(1234.5m, 0)` ergibt 1234. Ein Test unter .NET fängt den halben Cent deshalb schon ab. In
  `src/Client` steht kein `Math.Round` mehr, Geldanzeige und Vorbelegung runden über `roundHalfUp` (infrastructure-k84s2).

## Grep-Checks

```bash
# MidpointRounding in Code, der nach JS übersetzt wird (erlaubt: toMilliunits, läuft nur unter .NET)
grep -rn 'MidpointRounding' src/Client/ src/Shared/

# Math.Round im Client (erwartet: nichts, kaufmännisch über Shared.Domain.roundHalfUp)
grep -rn 'Math.Round' src/Client/

# DateTimeOffset ohne UTC
grep -rn 'DateTimeOffset(' src/Server/ | grep -v 'TimeSpan.Zero'

# style.lineHeight (wahrscheinlich falsch)
grep -rn 'style.lineHeight' src/Client/
```
