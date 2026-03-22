---
paths:
  - "src/Tests/E2E/**"
  - "src/Shared/Api.fs"
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

## Grep-Checks

```bash
# DateTimeOffset ohne UTC
grep -rn 'DateTimeOffset(' src/Server/ | grep -v 'TimeSpan.Zero'

# style.lineHeight (wahrscheinlich falsch)
grep -rn 'style.lineHeight' src/Client/
```
