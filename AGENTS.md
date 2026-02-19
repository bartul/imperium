# Repository Guidelines
Last verified: 2026-02-15

## Agent Priorities

- Follow the three-phase process in `docs/architecture.md`: define `.fsi`, write tests, then implement.
- Handlers accept unified `RondelDependencies` record for consistency. When adding new handlers, use the same pattern.
- Public API uses routers (`execute`, `handle`) as single entry points; individual handlers are internal implementation details.
- Prefer minimal public surface; align `.fs` to `.fsi` without widening the API.

## Project Structure & Module Organization
- `Imperium.slnx` stitches together the core F# library, ASP.NET Core web host, and unit test project.
- `src/Imperium` contains domain modules (build order: `Primitives.fs`, `AsyncExtensions.fs`, `Contract.fs`, `Contract.Accounting.fs`, `Contract.Rondel.fs`, `Gameplay.fs/.fsi`, `Accounting.fs/.fsi`, `Rondel.fs/.fsi`).
- `tests/Imperium.UnitTests` contains Expecto-based unit tests; test modules mirror source structure (e.g., `RondelTests.fs` tests `Rondel.fs` handlers, `RondelContractTests.fs` tests `Rondel.fs` transformation layer).
- **Primitives module:** Foundational types with no `.fsi` file (intentionally public)
  - `Id` - Struct wrapping `Guid` with validation; provides `create`, `newId`, `value`, `toString`, `tryParse`, and mapper helpers
  - `Amount` - Measured struct wrapper (`int<M>`) with guarded construction; errors are plain strings; includes `tryParse`
- **Contract modules:** Cross-bounded-context communication types; no `.fsi` files (intentionally public); organized by bounded context
  - `Contract.Gameplay` (Contract.fs): Placeholder for future game-level coordination types
  - `Contract.Accounting` (Contract.Accounting.fs): ChargeNationForRondelMovementCommand, VoidRondelChargeCommand, AccountingCommand (routing DU), AccountingEvent (RondelInvoicePaid, RondelInvoicePaymentFailed)
  - `Contract.Rondel` (Contract.Rondel.fs): SetToStartingPositionsCommand, MoveCommand, RondelEvent (PositionedAtStart, ActionDetermined, MoveToActionSpaceRejected), RondelState, PendingMovement
  - Contract function types for dependency injection (e.g., `ChargeNationForRondelMovement = ChargeNationForRondelMovementCommand -> Result<unit, string>`) - domain handlers use domain `DispatchOutboundCommand` dependency instead
  - Events use record types (e.g., `RondelEvent = | PositionedAtStart of PositionedAtStart` where `PositionedAtStart = { GameId: Guid }`)
- **Domain modules:** CQRS bounded contexts with `.fsi` files defining public APIs
  - Internal types (GameId, NationId, Bank, Investor) hidden from public APIs; `Action`, `RondelBillingId`, and `Space` are exposed in `Rondel.fsi`; `RondelBillingId.ofId` enables creating billing IDs from `Id` for in-process event conversion
  - **Two-layer architecture:** Transformation modules (accept Contract types, return `Result<DomainType, string>`) + Command/Event handlers (accept Domain types, return `unit` or `Result`)
  - Transformation modules: Named after domain types (e.g., `SetToStartingPositionsCommand.fromContract`, `MoveCommand.fromContract`, `InvoicePaidInboundEvent.fromContract`) using directional naming (`fromContract` for Contract → Domain, `toContract` for Domain → Contract)
  - Command handlers: Accept domain types directly, throw exceptions for business rule violations, return `unit`
  - Event handlers: Accept domain event types (after transformation), return `unit` (throw exceptions for errors)
  - All handlers take dependency injections explicitly (e.g., `load`, `save`, `publish`, specialized services)
  - `Gameplay` has no public API currently (placeholder values only)
  - `Accounting` exposes: transformation modules (ChargeNationForRondelMovementCommand, VoidRondelChargeCommand, AccountingEvent), domain command types (ChargeNationForRondelMovementCommand with `Id` GameId/BillingId, VoidRondelChargeCommand), domain event types (RondelInvoicePaidEvent, RondelInvoicePaymentFailedEvent with `Id` GameId/BillingId), command routing DU (AccountingCommand), event routing DU (AccountingEvent), dependency types (PublishAccountingEvent, AccountingDependencies record), `execute` router (routes AccountingCommand to internal handlers)
  - `Rondel` exposes: transformation modules (SetToStartingPositionsCommand, MoveCommand, InvoicePaidInboundEvent, InvoicePaymentFailedInboundEvent, ChargeMovementOutboundCommand, VoidChargeOutboundCommand), domain command types (SetToStartingPositionsCommand with `Set<string>` Nations, MoveCommand with Space), domain outbound command types (ChargeMovementOutboundCommand, VoidChargeOutboundCommand, RondelOutboundCommand DU), domain inbound event types (InvoicePaidInboundEvent, InvoicePaymentFailedInboundEvent with `Id` and `RondelBillingId`), command routing DU (RondelCommand), inbound event routing DU (RondelInboundEvent), Space type, RondelBillingId type with value accessor, dependency types (LoadRondelState, SaveRondelState, PublishRondelEvent, DispatchOutboundCommand, RondelDependencies record), `execute` router (routes RondelCommand to internal handlers), `handle` router (routes RondelInboundEvent to internal handlers), query types (GetNationPositionsQuery, GetRondelOverviewQuery, NationPositionView, RondelPositionsView, RondelView), query dependency types (LoadRondelStateForQuery, RondelQueryDependencies), query handlers (`getNationPositions`, `getRondelOverview`)
  - `Contract.Rondel.RondelState`: Serializable DTOs (Guid/string) for persistence. NationPositions is `Map<string, string option>` at the serialization boundary and PendingMovements is keyed by nation name for O(log n) lookups.
  - `Rondel.RondelState`: Domain state uses strong types (`Id`, `Space option`, `RondelBillingId`). NationPositions is `Map<string, Space option>` and PendingMovement uses `Space` TargetSpace + `RondelBillingId` BillingId. Transformations live in `Rondel.fs` (`RondelState.toContract/fromContract`), not in a separate adapter.
- `src/Imperium.Web` bootstraps the HTTP layer (`Program.fs`). Reference the core project via the existing project reference instead of duplicating logic.
- `src/Imperium.Terminal`: Terminal UI app with Terminal.Gui v2, MailboxProcessor hosting, in-memory store, cross-context Bus. See `docs/architecture.md` for design.
- `docs/` stores reference rulebooks; official rule PDFs live in `docs/official_rules/`. Architecture docs in `docs/architecture.md`, pending technology choices in `docs/future_decisions.md`. Leave build artefacts inside each project's `bin/` and `obj/` directories untouched.
- Rondel spaces (board order): `Investor`, `Import`, `ProductionOne`, `ManeuverOne`, `Taxation`, `Factory`, `ProductionTwo`, `ManeuverTwo`.
- Rondel rules source: mechanic follows the boardgame "rondel" described in `docs/official_rules/Imperial_English_Rules.pdf`. Keep only a quick cheat sheet here; see the PDF for full details. Key movement: clockwise, cannot stay put; 1–3 spaces free, 4–6 cost 2M per additional space beyond the first 3 free spaces (4 spaces = 2M, 5 spaces = 4M, 6 spaces = 6M; max distance 6), first turn may start anywhere. Actions: Factory (build own city for 5M, no hostile upright armies), Production (each unoccupied home factory produces 1 unit), Import (buy up to 3 units for 1M each in home provinces), Maneuver (fleets adjacent sea; armies adjacent land or via fleets; rail within home; 3 armies can destroy a factory; place flags in newly occupied regions), Investor (pay bond interest; investor card gets 2M and may invest; Swiss bank owners may also invest; passing executes investor steps 2–3), Taxation (tax: 2M per unoccupied factory, 1M per flag; dividend if tax track increases; add power points; treasury collects tax minus 1M per army/fleet). Game ends at 25 power points; score = bond interest x nation factor + personal cash.

### Handler Signature Pattern
- **Transformation modules** (`SetToStartingPositionsCommand.fromContract`, `MoveCommand.fromContract`, `InvoicePaidInboundEvent.fromContract`, `InvoicePaymentFailedInboundEvent.fromContract`): Modules named after domain types; accept Contract types, validate inputs, return `Result<DomainType, string>` with plain string errors
- **Router functions (public API)**:
  - `execute`: Routes `RondelCommand` union type to appropriate internal command handler; accepts `RondelDependencies` record, then `RondelCommand`; returns `Async<unit>` for implicit CancellationToken propagation; throws exceptions for business rule violations
  - `handle`: Routes `RondelInboundEvent` union type to appropriate internal event handler; accepts `RondelDependencies` record, then `RondelInboundEvent`; returns `Async<unit>`; throws exceptions on errors
- **Internal command handlers** (`setToStartingPositions`, `move`): Accept `RondelDependencies` record, then domain command types; return `Async<unit>`; throw exceptions for business rule violations; marked `internal`, not exposed in `.fsi`
- **Internal event handlers** (`onInvoicePaid`, `onInvoicePaymentFailed`): Accept `RondelDependencies` record, then domain event types (after transformation from Contract types); return `Async<unit>`, throw exceptions on errors; marked `internal`, not exposed in `.fsi`
- **Unified dependencies**: All Rondel handlers accept a single `RondelDependencies` record with `Async<_>` based dependency types (`{ Load: Id -> Async<RondelState option>; Save: RondelState -> Async<Result<unit, string>>; Publish: RondelEvent -> Async<unit>; Dispatch: RondelOutboundCommand -> Async<Result<unit, string>> }`) for consistency and implicit CancellationToken propagation. Implementations use `async {}` CE with `let!`/`do!` bindings. This provides uniform async handling and simplifies adding new dependencies in the future.
- **Public API surface**: Only routers (`execute`, `handle`) are exposed in `.fsi`; individual handlers are implementation details. This provides a clean, minimal API with single entry points for commands and events.
- Dependency injection order: persistence (load, save), publish, then dispatch (outbound commands). Load/save use domain `RondelState` and `Id`; persistence adapters map to/from `Contract.Rondel.RondelState`. Outbound commands use domain types (`RondelOutboundCommand`) with per-command `toContract` transformations targeting appropriate bounded contexts.
- Signature files define public shape first; implementations should not widen the surface in `.fs`.
- **AsyncExtensions module**: Provides `Async.AwaitTaskWithCT` helper for calling Task-based libraries (e.g., EF Core, Marten, Azure SDK) with the implicit CancellationToken from async context. Usage: `let! result = Async.AwaitTaskWithCT (fun ct -> library.MethodAsync(arg, ct))`.

### Rondel Implementation Patterns
- **Two-layer architecture**: Transformation layer (validates inputs, returns `Result`) + Handler layer (validates business rules, throws exceptions, returns `Async<unit>`)
- **Transformation modules** (`SetToStartingPositionsCommand`, `MoveCommand`): Named after domain command types; use `Result` monad to validate Contract inputs (GameId, Space names) and construct domain types
- **Handler pipeline pattern**: Handlers follow consistent three-stage pipeline using `async {}` CE: `load state (let!) → execute pure logic → materialize side effects (do!)`. Internal handlers delegate to pure functions in dedicated modules (`Move.execute`, `SetToStartingPositions.execute`), maintaining separation between business rules and IO.
- **Pure execution modules**: Each command handler has a corresponding internal module (`Move`, `SetToStartingPositions`) containing pure business logic functions. The `execute` function in each module accepts command and state, returns `(state, events, commands)` tuple with no IO. This enables testing business rules without mocking IO dependencies.
- **Materialize pattern**: Shared `materialize` function uses `async {}` CE to sequence IO side effects (save state → publish events → dispatch commands). Each dependency call uses `let!`/`do!` bindings; errors in `Result` types are pattern-matched and thrown as exceptions. All handlers use the same materialization logic for consistency. CancellationToken flows implicitly through the async context.
- **Record construction**: Use type annotation for contract state construction: `let newState : Contract.Rondel.RondelState = { GameId = ...; NationPositions = ...; PendingMovements = ... }`. F# records use `{ }` syntax directly, not `TypeName { }`.
- **MoveOutcome type**: Internal discriminated union with named fields carrying complete context (targetSpace, distance, nation, rejectedCommand). All cases encapsulate necessary data, eliminating closure dependencies on outer scope variables for cleaner functional design.
- **Decision chain**: Validates moves through `Decision` monad (`noMovesAllowedIfNotInitialized` → `noMovesAllowedForNationNotInGame` → `firstMoveIsFreeToAnyPosition` → `decideMovementOutcome`) producing `MoveOutcome`.
- **Side-effect separation**: `handleMoveOutcome` transforms `MoveOutcome` to `(state, events, commands)` tuple inside the `Move` module; `materialize` handles all IO outside the pure logic.
- **Command dispatch**: Uses `List.fold` with `Result.bind` to sequence outbound commands (`RondelOutboundCommand` with `ChargeMovement` and `VoidCharge` cases), returning first error or `Ok ()`. Infrastructure layer receives domain commands and calls per-command `toContract` transformations to dispatch to appropriate bounded contexts.

### Open Work (current)
- Rondel public API: Two routers (`execute` for commands, `handle` for events) return `Async<unit>` for implicit CancellationToken propagation; individual handlers are internal implementation details.
- Rondel internal structure: Handlers follow `load → execute → materialize` pattern using `async {}` CE. Pure business logic isolated in internal modules (`Move.execute`, `SetToStartingPositions.execute`) returning `(state, events, commands)` tuples. Shared `materialize` function uses `async {}` to sequence IO side effects (save, publish, dispatch).
- Rondel internal handlers: `setToStartingPositions` complete (delegates to `SetToStartingPositions.execute` for pure validation and state creation); `move` complete (delegates to `Move.execute` for clockwise distance calculation, 1-3 space free moves with immediate action determination, 4-6 space paid moves with charge dispatch and pending state storage (formula: (distance - 3) * 2M), rejects 0-space (stay put) and 7+ space (exceeds max) moves, automatically voids old charges and rejects old pending moves when a nation initiates a new move before previous payment completes); `onInvoicePaid` complete with idempotent payment confirmation processing (ignores events for non-existent pending movements, handles duplicate payment events or already-completed/voided movements, fails fast on state corruption); `onInvoicePaymentFailed` complete (delegates to `OnInvoicePaymentFailed.handle` for payment failure processing; removes pending movements when payment fails; emits `MoveToActionSpaceRejected` event; includes idempotent handling for duplicate or already-processed events).
- Accounting public API: One router (`execute` for commands) returns `Async<unit>` for implicit CancellationToken propagation; skeleton implementation for Phase 1.
- Accounting internal structure: Stateless skeleton with no persistent state. `chargeNationForRondelMovement` auto-approves charges (immediately publishes `RondelInvoicePaid` event), `voidRondelCharge` is no-op. Future implementation can add balance tracking, transaction history, and payment validation.
- Add public API for Gameplay or trim placeholder if unused.
- Tests use helper pattern: private record types (`Rondel`, `Accounting`) with routers (sync wrappers using `Async.RunSynchronously`), factory functions return router record with async dependencies + observable collections for verification.

### Multi-Environment Architecture (Phase 1: Terminal App)
- See `docs/architecture.md` for full architecture and design decisions.
- **Terminal app** (`Imperium.Terminal`): In-process app with Terminal.Gui v2 (`2.0.0-develop.5027`), MailboxProcessor hosting, in-memory store.
- **Key patterns:**
  - `IBus` interface for cross-bounded-context **events** (pub/sub with generic `Publish<'T>` and `Subscribe<'T>`); implementation uses `ConcurrentDictionary<Type, obj>` storing typed handler lists to avoid boxing events on publish
  - Thunk injection for cross-BC **commands** (breaks circular dependencies, type-safe direct calls)
  - Each BC has a `Host` (e.g., `RondelHost`, `AccountingHost`) with `Execute` entry point
  - MailboxProcessor serializes commands/events per BC with fire-and-forget `Post` (no reply channel); queries bypass for direct store access
  - Domain events used directly (not contract types) since everything is in-process; `RondelBillingId.ofId` enables domain-to-domain event conversion without contract layer
  - `SystemEvent` DU for UI lifecycle events (`AppStarted`, `NewGameStarted`, `GameEnded`, `MoveNationRequested`) — published on bus, views subscribe to update state
  - UI views are module functions (`RondelView.create`, `EventLogView.create`) returning `FrameView` — all state managed via shared mutable records, views subscribe to bus events
- **Project structure:**
  ```
  src/Imperium.Terminal/
  ├── Bus.fs                    # IBus interface and factory
  ├── Rondel/
  │   ├── Store.fs              # RondelStore with InMemoryRondelStore
  │   ├── Host.fs               # RondelHost with MailboxProcessor, event subscriptions, query handlers
  │   └── UI/
  │       └── RondelView.fs     # Stateless canvas grid with SelectionMode and shared RondelViewState
  ├── Accounting/
  │   └── Host.fs               # AccountingHost with MailboxProcessor, publishes inner events
  ├── Shell/
  │   ├── UI.fs                 # Shared UI helpers (invokeOnMainThread, frameView, mkAttr)
  │   ├── SystemEvent.fs        # UI lifecycle event DU
  │   ├── EventLogView.fs       # Bus-driven event log panel
  │   └── App.fs                # Application layout, menu bar, keyboard shortcuts
  └── Program.fs                # Entry point, wiring hosts and bus
  ```
- **RondelHost implementation:** MailboxProcessor handles `Command` and `InboundEvent` messages; subscribes to `RondelInvoicePaidEvent`/`RondelInvoicePaymentFailedEvent` (domain types); converts using `RondelBillingId.ofId`; dispatches to Accounting via thunk; queries call domain handlers directly.
- **AccountingHost implementation:** MailboxProcessor handles commands; publishes inner event types (`RondelInvoicePaidEvent`, `RondelInvoicePaymentFailedEvent`) directly to bus for RondelHost subscriptions.
- **Technology choices (terminal):** Terminal.Gui v2 (TUI framework with views, menu bars, keyboard/mouse support), direct function calls for in-process messaging.
- **RondelView architecture:** Stateless `RondelCanvas` (zero mutable fields) reads from shared `RondelViewState` record with mutable fields (`CurrentGame`, `Selection`, `Positions`). `SelectionMode` record (`Nation` + `Space`) replaces separate selection tracking — single `Option` makes state transitions atomic. `SyncFocus()` method toggles canvas focus based on selection state. Navigation uses `RondelLayout.nextSpace`/`prevSpace` helpers. `onSpaceSelected: Space -> unit` callback replaces direct `RondelHost` dependency. Color scheme: Investor=teal, Import=orange, Production=grey, Maneuver=green, Taxation=yellow, Factory=blue. Emoji flags (🇦🇹🇫🇷🇩🇪🇬🇧🇮🇹🇷🇺) on nation abbreviations with `displayWidth` helper for correct terminal centering.

## Build, Test, and Development Commands
- Restore dependencies: `dotnet restore Imperium.slnx`.
- Compile everything: `dotnet build Imperium.slnx` (fails fast on warnings-as-errors configured per project).
- Run the web host locally: `dotnet run --project src/Imperium.Web/Imperium.Web.fsproj`.
- Live reload during UI work: `dotnet watch --project src/Imperium.Web/Imperium.Web.fsproj run`.
- Run unit tests: `dotnet test` (VS Code integration via YoloDev.Expecto.TestSdk) or `dotnet run --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj` (native Expecto runner).

### Pre-Commit Checklist

Before every commit, always run these steps in order:
1. `dotnet fantomas .` — format all F# files
2. `dotnet build` — ensure the whole solution compiles with 0 errors and 0 warnings
3. `dotnet test` — ensure all tests pass

### Launch Terminal App for Review

To launch the terminal app in a separate Ghostty window for visual review:

```bash
open -na Ghostty.app --args --command="dotnet run --project src/Imperium.Terminal" --window-width=160 --window-height=50 --quit-after-last-window-closed=true
```

Use this as part of the inner development loop: make changes, launch for review, collect feedback, iterate.

## Coding Style & Naming Conventions
- Use the default F# formatting (4-space indentation, modules and types in `PascalCase`, functions and values in `camelCase`).
- Group related functions into modules that mirror file names (`Rondel`, `MonetarySystem`); expose a minimal public surface.
- Prefer expression-based code and pattern matching over mutable branches.
- Before committing, run `dotnet fantomas .` to format code (configured via `.config/dotnet-tools.json`); keep diffs tidy and minimal.

### Signature Files: Function vs Value

F# distinguishes function definitions from computed values. Avoid partial applications in signatures unless explicitly parenthesized.

```fsharp
// Prefer explicit function definition for tryParse helpers
let tryParse raw = Id.tryParseMap GameId raw

// If you expose a computed value, mark it in the signature:
val tryParse : (string -> Result<T, string>)
```

Reasoning: preserves IL shape, avoids unwanted module-load computation, and keeps inlining options.

### Module File Organization

Domain modules (`.fsi` and `.fs` pairs) follow a consistent sectioned structure. Use visual dividers and XML doc comments for clarity.

**Section Divider Pattern:**
```fsharp
// ──────────────────────────────────────────────────────────────────────────
// Section Name
// ──────────────────────────────────────────────────────────────────────────
```

**Section Order (both `.fsi` and `.fs`):**

| # | Section | Contents |
|---|---------|----------|
| 1 | **Value Types & Enumerations** | Struct wrappers (`RondelBillingId`), DUs (`Action`, `Space`), companion modules |
| 2 | **Domain State** | Persistent state records (`RondelState`, `PendingMovement`) |
| 3 | **Commands** | Command routing DU and individual command records |
| 4 | **Events** | Outbound event DU and individual event records (published by this domain) |
| 5 | **Outbound Commands** | Commands dispatched to other bounded contexts (`ChargeMovementOutboundCommand`, `VoidChargeOutboundCommand`, `RondelOutboundCommand` DU) |
| 6 | **Incoming Events** | Inbound event routing DU and individual event records (received from other domains) |
| 7 | **Dependencies** | Function types for DI (`LoadState`, `SaveState`, `PublishEvent`, `DispatchOutboundCommand`) using `Async<_>` for implicit CancellationToken propagation and unified dependency record (`RondelDependencies`) |
| 8 | **Transformations** | Modules with `fromContract` (Contract → Domain), `toContract` (Domain → Contract) functions (including per-outbound-command `toContract`) |
| 9 | **Handlers (Internal Types)** | `.fs` only: shared `materialize` function (sequences IO side effects), internal modules with pure `execute` functions (`Move.execute`, `SetToStartingPositions.execute`), internal DUs for routing/outcomes (`MoveOutcome`) |
| 10 | **Handlers** | Public routers (`execute`, `handle`) followed by internal command handlers (delegate to pure module functions), then internal event handlers |

**XML Documentation Comments:**
- All public types and functions require `///` doc comments
- Module-level comment explains the module's transformation purpose
- Function-level comments describe behavior, parameters, and return semantics
- Inline field comments for record fields with non-obvious semantics:
  ```fsharp
  type RondelState =
      { GameId: Id
        /// Maps nation name to current position. None indicates starting position.
        NationPositions: Map<string, Space option>
        /// Maps nation name to pending paid movement awaiting payment.
        PendingMovements: Map<string, PendingMovement> }
  ```

**Internal Types in `.fs`:**
- Mark handler-internal types with `type internal`:
  ```fsharp
  type internal MoveOutcome =
      | Rejected of rejectedCommand: MoveCommand
      | Free of targetSpace: Space * nation: string
  ```

**Type Inference Gotcha:**
- When two record types share identical field names/types (e.g., `MoveCommand` and `MoveToActionSpaceRejectedEvent` both have `GameId`, `Nation`, `Space`), F# infers the last-defined type
- Add explicit type annotations to avoid ambiguity:
  ```fsharp
  let fromContract (cmd: Contract.MoveCommand) : Result<MoveCommand, string> = ...
  let decideOutcome (state: RondelState, cmd: MoveCommand, pos) = ...
  ```

**Reference Implementation:** See `Rondel.fsi` and `Rondel.fs` for the canonical example.

## Testing Guidelines
- Unit tests live in `tests/Imperium.UnitTests` using Expecto 10.2.3 with FsCheck integration for property-based testing.
- Test modules mirror source structure with separation of concerns: `Imperium.UnitTests.RondelTests` tests handler behavior, `Imperium.UnitTests.RondelContractTests` tests transformation layer; file names use `*Tests.fs` suffix.
- Use `[<Tests>]` attribute on test values for discovery by YoloDev.Expecto.TestSdk (enables VS Code Test Explorer integration).
- Execute `dotnet test` (via TestSdk) or `dotnet run --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj` (native Expecto runner with colorized output).
- Test organization: group related tests with `testList`, use descriptive test names in lowercase ("accepts valid GUID", not "AcceptsValidGuid").
- Cover edge cases: null inputs, empty strings, invalid formats, boundary conditions.

### CE-Based Testing (Simple.Testing style)

The `Spec.fs` module provides a computation expression-based testing approach inspired by Gregory Young's Simple.Testing pattern. Use declarative `on`/`when_`/`expect` syntax for readable, isolated test specifications.

**Core types:**
- `Action<'cmd, 'evt>`: DU with `Execute`, `Handle` cases
- `Specification<'ctx, 'cmd, 'evt>`: Pure data describing a test scenario
- `ISpecRunner<'ctx, 'state, 'cmd, 'evt>`: Interface for context-specific execution
- `NoState`: Marker type for stateless contexts (F# doesn't allow `unit` as generic return type)

**Usage pattern:**
```fsharp
let private specs =
    [ spec "chargeNationForRondelMovement auto-approves" {
          on createContext
          when_ [ ChargeNationForRondelMovement cmd |> Execute ]
          expect "publishes exactly one event" (fun ctx -> ctx.Events.Count = 1)
          expect "event is RondelInvoicePaid" (fun ctx ->
              match ctx.Events.[0] with RondelInvoicePaid _ -> true | _ -> false)
      } ]

[<Tests>]
let tests = testList "Accounting" (specs |> List.map (toExpecto runner))
```

**Key design decisions:**
- **Pure boolean predicates**: Expectations return `'ctx -> bool`, not assertions. Spec name + description provide context on failure.
- **Each expectation is its own testCase**: `toExpecto` creates a `testList` where each expectation runs the full `on`/`when_` sequence independently for isolation.
- **Actions as data**: `when_` collects actions declaratively; runner controls execution (enables logging, timing, etc.).
- **State capture for reporting**: Runner's `CaptureState` provides initial/final state snapshots for failure output, not passed to expectations.

**Multi-step scenarios:**
```fsharp
actions [ SetToStartingPositions initCmd |> Execute ]
when_ [ Move moveCmd |> Execute ]
```

**Reference implementation:** See `Accounting.fs` for complete example.

- **Testing approach:**
  - **Transformation validation tests** (in `*ContractTests.fs`): Test `fromContract` transformations with Contract types to verify input validation returns appropriate errors; use domain types directly in test setup
  - **Handler behavior tests** (in `*Tests.fs`): Create domain types directly (no transformation layer), call routers (`execute`, `handle`) with union types to verify correct outcomes, events, and charges
  - **Test helper pattern**: Use private record type grouping routers (e.g., `type private Rondel = { Execute: RondelCommand -> unit; Handle: RondelInboundEvent -> unit; GetNationPositions: GetNationPositionsQuery -> RondelPositionsView option; GetRondelOverview: GetRondelOverviewQuery -> RondelView option }`) with sync wrappers (`Async.RunSynchronously`), create factory function that returns router record with async dependencies wrapped in `async {}` + observable collections (events, commands) for verification
  - **Separation**: Keep transformation layer testing separate from handler behavior testing for clearer test intent and reduced boilerplate
- Current test coverage (83 tests total, all passing):
  - **AccountingContractTests.fs** (6 transformation validation tests):
    - ChargeNationForRondelMovementCommand.fromContract: requires valid GameId; requires valid BillingId; accepts valid command
    - VoidRondelChargeCommand.fromContract: requires valid GameId; requires valid BillingId; accepts valid command
  - **AccountingTests.fs** (2 handler behavior tests):
    - chargeNationForRondelMovement: auto-approves and publishes RondelInvoicePaid
    - voidRondelCharge: does nothing (no event published)
  - **Accounting.fs** (3 CE-based spec expectations):
    - chargeNationForRondelMovement: publishes exactly one event; event is RondelInvoicePaid
    - voidRondelCharge: no events published
  - **RondelContractTests.fs** (5 transformation validation tests):
    - SetToStartingPositionsCommand.fromContract: rejects Guid.Empty; rejects empty nations array; accepts duplicate nations (Set deduplicates to 2 from 3)
    - MoveCommand.fromContract: rejects unknown rondel space; rejects Guid.Empty
  - **RondelTests.fs** (23 handler behavior tests):
    - setToStartingPositions: signals setup for roster; setting twice does not signal again
    - move: cannot begin before starting positions are chosen
    - move: nation's first move may choose any rondel space (property test, 15 iterations)
    - move: rejects move to nation's current position repeatedly (property test, 15 iterations)
    - move: multiple consecutive moves of 1-3 spaces are free (property test, 15 iterations)
    - move: rejects moves of 7 spaces as exceeding maximum distance (property test, 15 iterations)
    - move: moves of 4-6 spaces require payment with formula (distance - 3) * 2M (property test, 15 iterations)
    - move: superseding pending paid move with another paid move voids old charge and rejects old move
    - move: superseding pending paid move with free move voids charge and completes immediately
    - onInvoicePaid: completes pending movement and publishes ActionDetermined event
    - onInvoicePaid: paying twice for same movement only completes it once
    - onInvoicePaid: payment for cancelled movement is ignored
    - onInvoicePaymentFailed: payment failure removes pending movement and publishes rejection
    - onInvoicePaymentFailed: processing payment failure twice only removes pending once
    - onInvoicePaymentFailed: payment failure for voided charge is ignored
    - onInvoicePaymentFailed: payment failure after successful payment is ignored
    - getNationPositions: returns None for unknown game; returns positions for initialized game; returns current position after free move; returns pending space for paid move awaiting payment
    - getRondelOverview: returns None for unknown game; returns overview for initialized game
  - **TerminalBusTests.fs** (4 Bus tests):
    - publish with no subscribers does nothing
    - subscriber receives published event
    - multiple subscribers receive same event
    - different event types are isolated
  - **TerminalRondelStoreTests.fs** (3 store tests):
    - load returns None for unknown game
    - save then load returns saved state
    - save overwrites existing state
  - **RondelHostTests.fs** (5 plumbing tests with exponential backoff):
    - wires command execution to domain
    - wires domain events to bus
    - wires outbound commands to dispatch thunk
    - wires bus events to domain handler
    - wires queries to store
  - **AccountingHostTests.fs** (2 plumbing tests):
    - wires command execution to domain
    - publishes events to bus

## Branch Naming Guidelines

Use branch name prefixes to categorize the type of work, following conventional commit conventions:

- `feat/` - New features or functionality (e.g., `feat/implement-invoice-paid-handler`, `feat/add-nation-selection`)
- `fix/` - Bug fixes (e.g., `fix/rondel-distance-calculation`, `fix/null-reference-in-payment`)
- `refactor/` - Code refactoring without changing behavior (e.g., `refactor/extract-validation-pipeline`, `refactor/simplify-move-handler`)
- `test/` - Adding or updating tests (e.g., `test/add-property-tests-for-moves`, `test/improve-coverage`)
- `docs/` - Documentation updates (e.g., `docs/update-rondel-rules`, `docs/add-api-examples`)
- `chore/` - Maintenance tasks: dependencies, build config, tooling (e.g., `chore/upgrade-dotnet-9`, `chore/update-fantomas`)
- `perf/` - Performance improvements (e.g., `perf/optimize-state-loading`, `perf/cache-distance-calc`)
- `style/` - Code style/formatting changes with no logic impact (e.g., `style/reformat-with-fantomas`)

**Examples:**
- Implementing payment handler: `feat/implement-invoice-paid-handler`
- Fixing a movement bug: `fix/correct-7-space-rejection`
- Adding property tests: `test/add-superseding-move-tests`
- Refactoring validation: `refactor/use-decision-monad`

## Pull Request Workflow

All changes must go through pull requests to maintain code quality and enable proper review. Direct pushes to `master` are discouraged (see Branch Protection below).

### Standard Workflow

1. **Create branch** - Use appropriate prefix from Branch Naming Guidelines
   ```bash
   git checkout -b feat/your-feature-name
   ```

2. **Make changes** - Implement your changes following the three-phase module development process
   - Write tests first when adding new functionality
   - Keep commits focused and well-described

3. **Verify locally** - Ensure all checks pass before pushing
   ```bash
   dotnet build Imperium.slnx --configuration Release
   dotnet test Imperium.slnx --configuration Release
   ```

4. **Push and create PR** - Push your branch and open a pull request
   ```bash
   git push -u origin feat/your-feature-name
   gh pr create --fill  # or use GitHub web UI
   ```

5. **CI must pass** - The "Continuous Integration" workflow serves as the required quality gate
   - Build must succeed with no warnings
   - All tests must pass
   - If CI fails, fix issues and push updates

6. **Code review** - Wait for review approval
   - Address review comments
   - Push additional commits as needed

7. **Merge** - Once approved and CI passes, merge to master
   - Use "Squash and merge" for clean history (preferred)
   - Use "Merge commit" to preserve detailed commit history
   - Delete branch after merging

### Branch Protection

- Never push directly to `master` except for emergencies
- **CI as Quality Gate**: The "Continuous Integration" workflow provides automated quality checks
- **Self-Review**: Before merging your own PR, verify:
  - CI passes (green checkmark)
  - Code follows project conventions
  - Tests provide adequate coverage
  - Documentation is updated if needed

## Commit & Pull Request Guidelines

- Follow the existing history: imperative, concise subject lines (`Update to dotnet 9`, `Add web`).
- Keep commits scoped to one concern; describe "what" and "why" in the body when context is non-trivial.
- PRs should link relevant issues, outline test evidence (command outputs or screenshots), and call out any manual steps for deployment.
- Request review from domain owners when altering core rule logic or public web endpoints.

## Continuous Integration

The project uses GitHub Actions for automated quality checks on all pull requests and pushes to master.

### CI Workflow (`.github/workflows/ci.yml`)

**Workflow name:** "Continuous Integration"

**Triggers:**
- Push to `master` branch
- Pull requests targeting `master` branch

**Build Steps:**
1. **Restore tools**: `dotnet tool restore` (Fantomas from `.config/dotnet-tools.json`)
2. **Cache NuGet packages**: Speeds up builds by ~30%
3. **Restore dependencies**: `dotnet restore Imperium.slnx`
4. **Build**: `dotnet build --configuration Release` (warnings as errors)
5. **Test**: `dotnet test` with multiple loggers:
   - Console logger for CI output
   - TRX logger for test results file
   - GitHubActionsTestLogger for automatic annotations
6. **Format check**: `dotnet fantomas --check .` (continues on error)
7. **Upload artifacts**: Test results preserved for 30 days

**Test Reporting:**
- Failed tests create **GitHub annotations** in PR Files Changed view
- Job summary displays test results table (Total, Passed, Failed)
- TRX files uploaded as downloadable artifacts
- Formatting issues reported with actionable messages

**Job Summary Output:**
- ✅/❌ Build status
- 🧪 Test results table
- 🎨 Code formatting status
- Overall pass/fail indicator

**Required for merge:**
- Build must succeed
- All tests must pass
- Formatting check is informational only (continues on error)

### GitHub Repository Settings

**Auto-delete branches**: Enabled
- Merged PR branches are automatically deleted
- Keeps repository clean without manual cleanup
- Configure at: Repository Settings → General → Pull Requests

### Dependency Management

**GitHub Dependabot** automatically scans dependencies and creates pull requests for updates.

**Configuration:** `.github/dependabot.yml`

- **NuGet packages**: Weekly updates (Monday 08:00 UTC)
- **GitHub Actions workflows**: Weekly updates (Monday 08:00 UTC)
- **Review routing**: Assigned via CODEOWNERS file (no explicit reviewer configuration)
- **Labels**: All Dependabot PRs automatically tagged with `chore`
- **PR limit**: Maximum 3 open PRs per ecosystem to avoid clutter
- **Commit messages**: All commits prefixed with `chore:` to align with branch naming conventions

**Workflow:**

- Dependabot runs weekly checks for new versions
- Creates separate PRs for NuGet and Actions updates
- Each PR includes changelog and upgrade impact summary
- Commits follow the `chore:` prefix convention
- Review and merge like any other PR; CODEOWNERS ensures appropriate reviewers are notified

### Local Development Tools

**.config/dotnet-tools.json** contains:

- `fantomas` (7.0.5): F# code formatter

**Restore tools locally:**

```bash
dotnet tool restore
```

**Format code:**

```bash
dotnet fantomas .          # Format all files
dotnet fantomas --check .  # Check without modifying
```
