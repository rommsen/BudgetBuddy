# BudgetBuddy - Tech Stack

## Frontend
- **Framework**: Elmish.React + Feliz (F# React bindings)
- **State Management**: MVU (Model-View-Update) / Elmish
- **Styling**: TailwindCSS 4.3, DaisyUI
- **Build Tool**: Vite + Fable plugin
- **Design System**: Custom components in `src/Client/DesignSystem/`

## Backend
- **Framework**: Giraffe (F# web framework on ASP.NET Core)
- **API**: Fable.Remoting (type-safe RPC)
- **Database**: SQLite + Dapper
- **Runtime**: .NET 8+

## Shared
- **Language**: F# (shared types between frontend and backend)
- **Serialization**: Fable.Remoting handles JSON automatically

## Testing
- **Framework**: Expecto
- **Location**: `src/Tests/`

## Deployment
- **Containerization**: Docker
- **Networking**: Tailscale for secure access
