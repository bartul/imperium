# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Imperium is an F# implementation of the Imperial board game, featuring a domain-driven design with separate layers for game rules and web hosting.

## Architecture

**Solution Structure:**
- `Imperium.sln` - Main solution file
- `src/Imperium/` - Core F# library with domain logic (Gameplay, Economy, Rondel modules)
- `src/Imperium.Web/` - ASP.NET Core web host (references core library)
- `tests/Imperium.UnitTests/` - Expecto-based unit tests (references core library)
- `docs/` - Reference rulebooks and design documentation

**Key Domain Modules:**
- `Gameplay.fs/.fsi` - Nation definitions (Germany, Great Britain, France, Russia, Austria-Hungary, Italy) with parsing and display logic
- `Economy.fs/.fsi` - Monetary system using measured struct `Amount` (wraps `int<M>`) with guarded construction
- `Rondel.fs/.fsi` - Game board mechanics following the rondel pattern; nations move clockwise through spaces (Investor, Factory, Import, Maneuver, Production, Taxation); movement costs 2M per space beyond 3 free spaces

**Domain Model Patterns:**
- Use `.fsi` signature files to define public interfaces
- Domain IDs are either DUs (`NationId`) or struct DUs wrapping primitives (`RondelInvoiceId` wraps `Guid`)
- All parsers follow `tryParse : string -> Result<T, string>` convention
- Errors are plain strings, not custom exception types
- Measured types use F# units of measure (`int<M>` for money)

## Development Commands

**Build and Run:**
```bash
dotnet restore Imperium.sln
dotnet build Imperium.sln           # Warnings treated as errors
dotnet run --project src/Imperium.Web/Imperium.Web.fsproj
dotnet watch --project src/Imperium.Web/Imperium.Web.fsproj run  # Live reload
```

**Testing:**
```bash
dotnet test                          # Run via VS Code Test SDK integration
dotnet run --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj  # Native Expecto runner
```

**Test Infrastructure:**
- Expecto 10.2.3 with FsCheck for property-based testing
- YoloDev.Expecto.TestSdk 0.15.3 for VS Code Test Explorer integration
- Test modules mirror source: `Imperium.UnitTests.Rondel` tests `Imperium.Rondel`
- Use `[<Tests>]` attribute for test discovery
- Current coverage: RondelInvoiceId (9 tests passing)

## Module Development Process

Follow three-phase interface-first, test-driven approach documented in `docs/module_design_process.md`:

1. **Phase 1: Interface Definition** - Define complete `.fsi` signature file, create `.fs` with dummy implementations that compile
2. **Phase 2: Test Implementation** - Write unit tests against the interface (tests fail initially)
3. **Phase 3: Functional Implementation** - Implement actual functionality until tests pass

**Critical workflow rules:**
- Always define `.fsi` before implementing `.fs`
- Never modify `.fsi` during Phase 3 (indicates poor planning)
- Tests target public interfaces only, not implementation details
- Dummy implementations must compile but can use `failwith "Not implemented"`

## Code Style

- F# standard formatting: 4-space indentation, `PascalCase` for types/modules, `camelCase` for functions
- Prefer expression-based code and pattern matching over mutable state
- Group related functions into modules matching file names
- Minimize public API surface
- Run `dotnet tool run fantomas` before committing (if configured)

## Domain Knowledge

**Rondel Mechanics (from Imperial board game):**
- Nations move clockwise through 8 spaces, cannot stay put
- Movement: 1-3 spaces free, 4-6 spaces cost 2M each to bank
- First turn can start at any space
- Key spaces: Factory (build for 5M), Production (units in home factories), Import (buy up to 3 units @ 1M), Maneuver (move armies/fleets), Investor (bond interest/investment), Taxation (collect 2M per factory, 1M per flag, minus 1M per unit)
- Game ends at 25 power points; final score = bond interest × nation factor + cash

**Current Implementation Status:**
- Basic domain types defined
- Events: `RondelCreated`, `NationMovementInvoiced`, `NationActionDetermined`
- Movement and payment flows are stubbed
- Full game logic is work in progress

## Important Files

- `AGENTS.md` - Detailed project structure, module documentation, rondel rules reference
- `docs/module_design_process.md` - Three-phase development methodology
- `docs/official_rules/Imperial_English_Rules.pdf` - Official game rules
