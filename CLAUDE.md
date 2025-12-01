# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Imperium is an F# implementation of the Imperial board game, featuring a domain-driven design with separate layers for game rules and web hosting.

## Architecture

**Solution Structure:**
- `Imperium.sln` - Main solution file
- `src/Imperium/` - Core F# library with domain logic (Primitives, Gameplay, Economy, Rondel modules)
- `src/Imperium.Web/` - ASP.NET Core web host (references core library)
- `tests/Imperium.UnitTests/` - Expecto-based unit tests (references core library)
- `docs/` - Reference rulebooks and design documentation

**Core Modules (build order):**
- `Primitives.fs` - Foundational types with no domain logic; provides reusable `Id` type (struct wrapping `Guid` with validation); no `.fsi` file (intentionally public)
- `Gameplay.fs/.fsi` - GameId and NationId types; nation definitions (Germany, Great Britain, France, Russia, Austria-Hungary, Italy) with parsing and display logic
- `Economy.fs/.fsi` - Monetary system using measured struct `Amount` (wraps `int<M>`) with guarded construction
- `Rondel.fs/.fsi` - CQRS bounded context for rondel game mechanics; commands identified by GameId; RondelBillingId type for invoice tracking; nations move clockwise through 8 spaces (Investor, Import, ProductionOne, ManeuverOne, Taxation, Factory, ProductionTwo, ManeuverTwo); movement costs 2M per space beyond 3 free spaces; publishes integration events for cross-domain communication

**Domain Model Patterns:**
- Use `.fsi` signature files to define public interfaces (except infrastructure modules like Primitives)
- **CQRS Bounded Context Pattern**: Domain modules expose command handlers and event handlers identified by aggregate IDs
  - Commands take `GameId` (or other aggregate ID) as first parameter
  - Internal state managed by module, indexed by aggregate ID (hidden from public API)
  - Commands return `Result<Event list, Error>` for integration events
  - Example: `Rondel` module with commands `setToStartPositions`, `move` and event handlers `onInvoicedPaid`, `onInvoicePaymentFailed`
- **Domain ID Pattern**: Struct DUs wrapping the `Id` primitive from Primitives module
  - Example: `type GameId = private GameId of Id`
  - Use mapper helpers: `Id.createMap GameId`, `Id.tryParseMap GameId`
  - Expose standard API: `create`, `newId`, `value`, `toString`, `tryParse`
  - Current implementations: `GameId` (Gameplay.fs:9-16), `RondelBillingId` (Rondel.fs:15-24)
- **Integration Events Pattern**: Simple discriminated unions for cross-bounded-context communication
  - Each event carries aggregate ID (e.g., `gameId:GameId`) and relevant domain data
  - Example: `PositionedAtStart of gameId:GameId`, `ActionDetermined of gameId:GameId * nationId:NationId * action:Action`
- Enum-based IDs use `[<RequireQualifiedAccess>]` DUs (e.g., `NationId`)
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
- Test modules mirror source: `Imperium.UnitTests.Gameplay` tests `Imperium.Gameplay`, etc.
- Use `[<Tests>]` attribute for test discovery
- Test files: `GameplayTests.fs`, `RondelTests.fs`, `Main.fs`
- **Current coverage (22 tests passing):**
  - GameId: 9 tests (2 create, 2 newId, 1 toString, 4 tryParse, 2 property-based)
  - RondelBillingId: 9 tests (same pattern)
- **Standard ID type test pattern:** Each ID type gets 9 tests covering construction, generation, serialization, parsing, and roundtrip invariants

## Module Development Process

Follow three-phase interface-first, test-driven approach documented in `docs/module_design_process.md`:

1. **Phase 1: Interface Definition** - Define complete `.fsi` signature file, create `.fs` with dummy implementations that compile
2. **Phase 2: Test Implementation** - Write unit tests against the interface (tests fail initially)
3. **Phase 3: Functional Implementation** - Implement actual functionality until tests pass

**Critical workflow rules:**

- Always define `.fsi` before implementing `.fs` for domain modules
- Infrastructure modules (like `Primitives.fs`) may omit `.fsi` when all types should be public
- Never modify `.fsi` during Phase 3 (indicates poor planning)
- Tests target public interfaces only, not implementation details
- Dummy implementations must compile but can use `failwith "Not implemented"` or `invalidOp`

## Code Style

- F# standard formatting: 4-space indentation, `PascalCase` for types/modules, `camelCase` for functions
- Prefer expression-based code and pattern matching over mutable state
- Group related functions into modules matching file names
- Minimize public API surface
- Run `dotnet tool run fantomas` before committing (if configured)

## F# Signature File (.fsi) Conventions

**Computed Values vs Function Definitions:**
F# distinguishes between function definitions and computed function values in signature files. This matters for:

- Binary compatibility (values → static fields, functions → methods in IL)
- Optimization (function definitions can be inlined)
- Initialization semantics (values computed at module load, functions at call time)

```fsharp
// ❌ WRONG: Computed value (partial application) doesn't match signature
let tryParse = Id.tryParseMap GameId  // Signature: val tryParse : string -> Result<T, string>

// ✅ CORRECT Option 1: Use explicit function definition
let tryParse raw = Id.tryParseMap GameId raw

// ✅ CORRECT Option 2: Parenthesize type in signature to indicate computed value
val tryParse : (string -> Result<T, string>)  // Note the parentheses
```

**Why this matters:**

- Prevents accidental breaking changes to library consumers
- Makes initialization order explicit
- Allows compiler to optimize more aggressively
- Forces developer to think about when computation happens

When using mapper functions like `Id.tryParseMap`, prefer Option 1 (explicit function definition) for consistency.

## Domain Knowledge

**Rondel Mechanics (from Imperial board game):**

- Nations move clockwise through 8 spaces, cannot stay put
- Movement: 1-3 spaces free, 4-6 spaces cost 2M each to bank
- First turn can start at any space
- Key spaces: Factory (build for 5M), Production (units in home factories), Import (buy up to 3 units @ 1M), Maneuver (move armies/fleets), Investor (bond interest/investment), Taxation (collect 2M per factory, 1M per flag, minus 1M per unit)
- Game ends at 25 power points; final score = bond interest × nation factor + cash

**Current Implementation Status:**

- **Infrastructure:** Primitives module with reusable `Id` type (Guid wrapper with validation)
- **Domain IDs:** GameId and RondelBillingId fully implemented and tested (22 passing tests)
- **Domain types:** NationId, Amount, Space, Action defined
- **Rondel bounded context (CQRS):**
  - **Commands:** `setToStartPositions` (initialize nations), `move` (initiate movement)
  - **Event handlers:** `onInvoicedPaid` (handle payment success), `onInvoicePaymentFailed` (handle payment failure)
  - **Integration events:** `PositionedAtStart`, `ActionDetermined`, `MovementToActionRejected`
  - **Implementation:** All command/event handlers currently stubbed (`invalidOp`)
- **Architecture notes:** Rondel type is temporary and will be removed; module will manage internal state indexed by GameId
- **Next steps:** Implement rondel movement mechanics, add internal state management, expand test coverage to game logic

## Important Files

- `AGENTS.md` - Detailed project structure, module documentation, rondel rules reference
- `docs/module_design_process.md` - Three-phase development methodology
- `docs/official_rules/Imperial_English_Rules.pdf` - Official game rules
