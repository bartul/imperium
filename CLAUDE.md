# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Imperium is an F# implementation of the Imperial board game, featuring a domain-driven design with separate layers for game rules and web hosting.

## Architecture

**Solution Structure:**
- `Imperium.sln` - Main solution file
- `src/Imperium/` - Core F# library with domain logic (Primitives, Contract, Gameplay, Accounting, Rondel modules)
- `src/Imperium.Web/` - ASP.NET Core web host (references core library)
- `tests/Imperium.UnitTests/` - Expecto-based unit tests (references core library)
- `docs/` - Reference rulebooks and design documentation

**Core Modules (build order):**
- `Primitives.fs` - Foundational types with no domain logic; provides reusable `Id` and `Amount` types (struct wrapping `Guid`/`int<M>` with validation); no `.fsi` file (intentionally public)
- `Contract.fs` - Cross-bounded-context communication types; defines commands, events, and function types for inter-domain messaging; organized by domain (Accounting, Rondel, Gameplay); no `.fsi` file (intentionally public)
- `Gameplay.fs/.fsi` - Internal types for GameId and NationId; nation definitions (Germany, Great Britain, France, Russia, Austria-Hungary, Italy) with parsing and display logic; no public API currently
- `Accounting.fs/.fsi` - Internal bounded context for monetary operations; no public API currently
- `Rondel.fs/.fsi` - CQRS bounded context for rondel game mechanics; exposes command handlers accepting contract types; internal types include RondelBillingId, Space, Action; all commands use dependency injection via contract function types

**Domain Model Patterns:**
- Use `.fsi` signature files to define public interfaces (except infrastructure modules like Primitives)
- **CQRS Bounded Context Pattern**: Domain modules expose command handlers and event handlers accepting contract types
  - Commands accept contract command types (records) and dependency function types
  - Internal state managed by module, indexed by aggregate ID (hidden from public API)
  - Commands return `Result<unit, string>` for synchronous execution
  - Example: `Rondel.setToStartingPositions : SetToStartingPositionsCommand -> Result<unit, string>`
  - Example with DI: `Rondel.move : ChargeNationForRondelMovement -> MoveCommand -> Result<unit, string>`
  - Event handlers accept contract event types: `onInvoicedPaid : RondelInvoicePaid -> Result<unit, string>`
- **Contract Types Pattern**: All cross-domain types defined in `Imperium.Contract` module
  - Commands: Record types with required data (e.g., `SetToStartingPositionsCommand`, `MoveCommand`)
  - Events: DU with record types (e.g., `RondelEvent`, `AccountingEvent`)
  - Function types: For dependency injection (e.g., `ChargeNationForRondelMovement = ChargeNationForRondelMovementCommand -> Result<unit, string>`)
  - Event unions: Aggregate all events for a domain (e.g., `RondelEvent`, `AccountingEvent`)
- **Domain ID Pattern**: Struct DUs wrapping the `Id` primitive from Primitives module (internal to bounded contexts)
  - Example: `type GameId = private GameId of Id` (internal, not exported)
  - Use mapper helpers: `Id.createMap GameId`, `Id.tryParseMap GameId`
  - Standard API: `create`, `newId`, `value`, `toString`, `tryParse`
  - Domain IDs are implementation details, not exposed in public APIs
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
- **Current coverage:** No tests currently (internal types no longer exposed in public APIs)
- **Testing approach:** Tests target public command/event handler APIs, not internal implementation details

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

- **Infrastructure:**
  - `Primitives` - Provides `Id` (Guid wrapper) and `Amount` (int<M> wrapper) foundational types
  - `Contract` - Defines all cross-domain communication types (commands, events, function types)
- **Domain modules:** All use CQRS pattern with internal state, exposing only command/event handlers
  - `Gameplay` - No public API yet; internal GameId and NationId types
  - `Accounting` - No public API yet; internal Bank and Investor types
  - `Rondel` - Public command handlers and event handlers accepting contract types
- **Contract types defined:**
  - `Contract.Rondel`: SetToStartingPositionsCommand, MoveCommand, RondelEvent (PositionedAtStart, ActionDetermined, MoveToActionSpaceRejected)
  - `Contract.Accounting`: ChargeNationForRondelMovementCommand, AccountingEvent (RondelInvoicePaid, RondelInvoicePaymentFailed)
  - Function types for dependency injection (ChargeNationForRondelMovement, SetToStartingPositions, Move)
- **Rondel public API:**
  - `setToStartingPositions : SetToStartingPositionsCommand -> Result<unit, string>`
  - `move : ChargeNationForRondelMovement -> MoveCommand -> Result<unit, string>`
  - `onInvoicedPaid : RondelInvoicePaid -> Result<unit, string>`
  - `onInvoicePaymentFailed : RondelInvoicePaymentFailed -> Result<unit, string>`
  - All implementations currently stubbed (`invalidOp "Not implemented"`)
- **Next steps:** Implement command/event handler logic, add internal state management, write integration tests

## Important Files

- `AGENTS.md` - Detailed project structure, module documentation, rondel rules reference
- `docs/module_design_process.md` - Three-phase development methodology
- `docs/official_rules/Imperial_English_Rules.pdf` - Official game rules
