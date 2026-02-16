# 🚧 Imperium

An experimental project exploring **AI-assisted software development** techniques and advanced F# design patterns — built around implementing the engine for [Imperial](https://boardgamegeek.com/boardgame/24181/imperial), a strategy board game by Mac Gerdts where players act as investors controlling European nations in the early 20th century.

> **Work in Progress** — This project is far from complete. Only the **Rondel** bounded context (the central movement mechanic of the game) is implemented to any satisfactory level. Other areas like gameplay, scoring, and full game flow remain unimplemented or skeletal.

## Designs & Techniques

This codebase serves as a showcase for several design patterns and development techniques:

- **F# signature files (`.fsi`) as enforceable API contracts** — public surface is defined in signature files; implementations cannot widen it, giving compile-time boundary enforcement
- **CQRS with command/event routers** — separate `execute` (commands) and `handle` (inbound events) routers as single entry points, with dedicated query handlers
- **Dependency injection via records of functions** — no IoC container; dependencies are plain `Async<_>` function values bundled in records, enabling implicit `CancellationToken` propagation
- **Pure business logic separated from IO** — handler internals return `(state, events, commands)` tuples; a shared `materialize` function sequences all side effects
- **Bounded context isolation through Contract DTOs** — cross-BC communication uses plain primitive types (`Guid`, `string`, `int`); transformation modules validate at each boundary
- **`Decision<'State,'Outcome>` monad** — a custom computation expression for chaining validation steps in business rule pipelines
- **CE-based declarative test specs** — [Simple.Testing](https://github.com/gregoryyoung/Simple.Testing)-inspired `on`/`when_`/`expect` syntax where each expectation becomes its own isolated test case
- **Three-phase module development process** — define `.fsi` interface first, write failing tests against it, then implement until green
- **`MailboxProcessor` for serialized writes** — terminal app uses F# agents to serialize state mutations per bounded context while allowing concurrent reads

## Project Structure

```
src/
  Imperium/              Core domain library (Rondel, Accounting, Contracts, Primitives)
  Imperium.Terminal/     Terminal UI app using Terminal.Gui v2
  Imperium.Web/          ASP.NET Core web host (placeholder)
tests/
  Imperium.UnitTests/    83 tests (Expecto + CE-based specs)
docs/                    Design documents and official game rules
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build & Development

```bash
# Restore tools (Fantomas formatter)
dotnet tool restore

# Build
dotnet build

# Run tests
dotnet test

# Format code
dotnet fantomas .

# Run terminal app
dotnet run --project src/Imperium.Terminal
```

## Tech Stack

| | |
|---|---|
| **Language** | F# on .NET 10 |
| **Terminal UI** | Terminal.Gui v2 |
| **Testing** | Expecto, FsCheck |
| **Utilities** | FsToolkit.ErrorHandling |
| **Formatting** | Fantomas |

## License

This project is licensed under the [MIT License](LICENSE).

## Disclaimer

*Imperial is a board game designed by Mac Gerdts and published by PD-Verlag and Rio Grande Games. This project is an independent fan implementation for educational and research purposes and is not affiliated with, endorsed by, or sponsored by the designer or publishers.*
