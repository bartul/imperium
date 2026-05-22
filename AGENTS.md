# Repository Guidelines
Last verified: 2026-05-05

## Agent Priorities

- Follow the three-phase process in `docs/architecture.md`: define `.fsi`, write tests, then implement.
- Handlers accept unified `RondelDependencies` record for consistency. When adding new handlers, use the same pattern.
- Public API uses routers (`execute`, `handle`) as single entry points; individual handlers are internal implementation details.
- Prefer minimal public surface; align `.fs` to `.fsi` without widening the API.

## Decision & Improvement Tracking

- Use **GitHub issues** as the system of record for architecture decisions and improvement proposals.
- Do **not** create ADR markdown files in `docs/` for this workflow.
- Create one umbrella tracking issue, then link child issues for:
  - Decision records (`type = decision` in title/body)
  - Actionable improvements (`type = improvement` in title/body)
- Reuse existing repository labels. For priority, use only:
  - `priority: high`
  - `priority: medium`
  - `priority: low`
- Each decision/improvement issue must include:
  - Parent issue reference
  - Context/problem
  - Decision/proposed change
  - Acceptance criteria or consequences
  - Explicit status (`approved`, `proposed`, or `deferred`)

## Project Structure & Module Organization
- `Imperium.slnx` stitches together the core F# library, ASP.NET Core web host, and unit test project.
- `src/Imperium` is organized as one folder per bounded context, each owning its own namespace. Compile order (mirrors `Imperium.fsproj`):
  - `Primitives/` — `Primitives.fs` (namespace `Imperium.Primitives`), `AsyncExtensions.fs` (`[<AutoOpen>] module Imperium.AsyncExtensions`)
  - `Contract/` — `Contract.fs`, `Accounting.fs`, `Rondel.fs` (all in `namespace Imperium.Contract`, intentionally no `.fsi`)
  - `Gameplay/` — `Gameplay.fsi/.fs` (`namespace Imperium.Gameplay`, `[<RequireQualifiedAccess>] module Gameplay`)
  - `Accounting/` — `Commands.fsi/.fs`, `Events.fsi/.fs`, `Dependencies.fsi/.fs`, `Handlers.fs`, `Accounting.fsi/.fs` (`namespace Imperium.Accounting`, `[<RequireQualifiedAccess>] module Accounting` facade)
  - `Rondel/` — `Types.fsi/.fs`, `Commands.fsi/.fs`, `Events.fsi/.fs`, `State.fsi/.fs`, `Dependencies.fsi/.fs`, `Movement.fs`, `Invoices.fs`, `Handlers.fs`, `Queries.fsi/.fs`, `Rondel.fsi/.fs` (`namespace Imperium.Rondel`, `[<RequireQualifiedAccess>] module Rondel` facade)
- **Namespace + facade convention.** Each bounded context lives in a `namespace Imperium.<BC>` that spans multiple files. Public domain types (commands, events, state, dependencies) sit directly at the namespace level. Routers and query handlers are exposed only through a same-named `[<RequireQualifiedAccess>] module <BC>` facade. Standard caller idiom: `open Imperium.Rondel` then `Rondel.execute deps cmd` / `Rondel.handle deps evt` / `Rondel.getNationPositions deps q`. Bare `execute` is intentionally not in scope.
- **Type-companion modules live with their type.** A module that shares a type's name (e.g. `module MoveCommand` providing `MoveCommand.fromContract`, `module AccountingEvent` providing `AccountingEvent.toContract`) must be declared in the *same* `.fsi`/`.fs` pair as the type — typically `Types.fsi/.fs` or `State.fsi/.fs`. They cannot be hoisted into a sibling `Transformations.fsi/.fs`.
- **Assembly-internal helpers in `.fsi`.** Once a file has a signature file, anything not in the `.fsi` becomes private to the implementation file. Cross-file helpers that should remain hidden from external consumers but visible to sibling files in the same assembly (e.g. `Space.toString`, `Space.fromString`, `Space.distance`, `RondelBillingId.create`, `RondelBillingId.newId`) are declared as `val internal name: ...` in the relevant `.fsi`.
- `tests/Imperium.UnitTests` contains Expecto-based unit tests; bounded-context behavior specs live in `Rondel.fs` and `Accounting.fs`, transformation validation lives in `*ContractTests.fs`, and infrastructure plumbing tests live in `*HostTests.fs` plus terminal bus/store test modules.
- **Primitives module:** Foundational types with no `.fsi` file (intentionally public)
  - `Id` - Struct wrapping `Guid` with validation; provides `create`, `newId`, `value`, `toString`, `tryParse`, and mapper helpers
  - `Amount` - Measured struct wrapper (`int<M>`) with guarded construction; errors are plain strings; includes `tryParse`
- **Contract modules:** Cross-bounded-context communication types in `namespace Imperium.Contract`; no `.fsi` files (intentionally public); organized by bounded context under `src/Imperium/Contract/`
  - `Contract.Gameplay` (Contract/Contract.fs): Placeholder for future game-level coordination types
  - `Contract.Accounting` (Contract/Accounting.fs): ChargeNationForRondelMovementCommand, VoidRondelChargeCommand, AccountingCommand (routing DU), AccountingEvent (RondelInvoicePaid, RondelInvoicePaymentFailed)
  - `Contract.Rondel` (Contract/Rondel.fs): SetToStartingPositionsCommand, MoveCommand, RondelEvent (PositionedAtStart, ActionDetermined, MoveToActionSpaceRejected), RondelState, PendingMovement
  - Contract function types for dependency injection (e.g., `ChargeNationForRondelMovement = ChargeNationForRondelMovementCommand -> Result<unit, string>`) - domain handlers in Rondel emit `RondelOutboundCommand` values via the returned `RondelEffects`; the host's `Commit` adapter (`RondelDirectCommit` in terminal) is responsible for translating them to the appropriate contract dispatch
  - Events use record types (e.g., `RondelEvent = | PositionedAtStart of PositionedAtStart` where `PositionedAtStart = { GameId: Guid }`)
- **Domain modules:** CQRS bounded contexts with `.fsi` files defining public APIs
  - Internal types (GameId, NationId, Bank, Investor) hidden from public APIs; `Action`, `RondelBillingId`, and `Space` are exposed in `Rondel.fsi`; `RondelBillingId.ofId` enables creating billing IDs from `Id` for in-process event conversion
  - **Two-layer architecture:** Transformation modules (accept Contract types, return `Result<DomainType, string>`) + Command/Event handlers (accept Domain types, return `Async<unit>`)
  - Transformation modules: Named after domain types (e.g., `SetToStartingPositionsCommand.fromContract`, `MoveCommand.fromContract`, `InvoicePaidInboundEvent.fromContract`) using directional naming (`fromContract` for Contract → Domain, `toContract` for Domain → Contract)
  - Command handlers: Accept domain types directly, throw exceptions for business rule violations, return `Async<unit>`
  - Event handlers: Accept domain event types (after transformation), return `Async<unit>` (throw exceptions for errors)
  - All handlers take dependency injections explicitly (e.g., `load`, `save`, `publish`, specialized services)
  - `Imperium.Gameplay` has no public API currently — `Gameplay/Gameplay.fsi/.fs` wraps an internal placeholder, an internal `GameId` struct, and an internal `NationId` DU inside `[<RequireQualifiedAccess>] module Gameplay`
  - `Imperium.Accounting` exposes (at namespace level): transformation modules (ChargeNationForRondelMovementCommand, VoidRondelChargeCommand, AccountingEvent), domain command types (ChargeNationForRondelMovementCommand with `Id` GameId/BillingId, VoidRondelChargeCommand), domain event types (RondelInvoicePaidEvent, RondelInvoicePaymentFailedEvent with `Id` GameId/BillingId), command routing DU (AccountingCommand), event routing DU (AccountingEvent), dependency types (PublishAccountingEvent, AccountingDependencies record). The `[<RequireQualifiedAccess>] module Accounting` facade exposes `val execute: AccountingDependencies -> AccountingCommand -> Async<unit>` (called as `Accounting.execute`)
  - `Imperium.Rondel` exposes (at namespace level): transformation modules (SetToStartingPositionsCommand, MoveCommand, InvoicePaidInboundEvent, InvoicePaymentFailedInboundEvent, ChargeMovementOutboundCommand, VoidChargeOutboundCommand, RondelEvent, RondelState, PendingMovement), domain command types (SetToStartingPositionsCommand with `Set<string>` Nations, MoveCommand with Space), domain outbound command types (ChargeMovementOutboundCommand, VoidChargeOutboundCommand, RondelOutboundCommand DU), domain inbound event types (InvoicePaidInboundEvent, InvoicePaymentFailedInboundEvent with `Id` and `RondelBillingId`), command routing DU (RondelCommand), inbound event routing DU (RondelInboundEvent), Space type, RondelBillingId type with value accessor, dependency types (LoadRondelState, RondelEffects record, CommitRondelEffects, RondelDependencies record), query types (GetNationPositionsQuery, GetRondelOverviewQuery, NationPositionView, RondelPositionsView, RondelView), query dependency types (LoadRondelStateForQuery, RondelQueryDependencies). The `[<RequireQualifiedAccess>] module Rondel` facade exposes `val execute`, `val handle`, `val getNationPositions`, `val getRondelOverview` (called as `Rondel.execute` / `Rondel.handle` / `Rondel.getNationPositions` / `Rondel.getRondelOverview`)
  - `Contract.Rondel.RondelState`: Serializable DTOs (Guid/string) for persistence. NationPositions is `Map<string, string option>` at the serialization boundary and PendingMovements is keyed by nation name for O(log n) lookups.
  - `Rondel.RondelState`: Domain state uses strong types (`Id`, `Space option`, `RondelBillingId`). NationPositions is `Map<string, Space option>` and PendingMovement uses `Space` TargetSpace + `RondelBillingId` BillingId. State construction/update helpers plus contract transformations live in `Rondel/State.fs` (`RondelState.toContract/fromContract`), not in a separate adapter.
- `src/Imperium.Web` bootstraps the HTTP layer (`Program.fs`). Reference the core project via the existing project reference instead of duplicating logic.
- `src/Imperium.Terminal`: Terminal UI app with Terminal.Gui v2, MailboxProcessor hosting, in-memory store, cross-context Bus. See `docs/architecture.md` for design.
- `docs/` stores reference rulebooks; official rule PDFs live in `docs/official_rules/`. Architecture docs in `docs/architecture.md`, pending technology choices in `docs/future_decisions.md`. Leave build artefacts inside each project's `bin/` and `obj/` directories untouched.
- Rondel spaces (board order): `Investor`, `Import`, `ProductionOne`, `ManeuverOne`, `Taxation`, `Factory`, `ProductionTwo`, `ManeuverTwo`.
- Rondel rules source: mechanic follows the boardgame "rondel" described in `docs/official_rules/Imperial_English_Rules.pdf`. Keep only a quick cheat sheet here; see the PDF for full details. Key movement: clockwise, cannot stay put; 1–3 spaces free, 4–6 cost 2M per additional space beyond the first 3 free spaces (4 spaces = 2M, 5 spaces = 4M, 6 spaces = 6M; max distance 6), first turn may start anywhere. Actions: Factory (build own city for 5M, no hostile upright armies), Production (each unoccupied home factory produces 1 unit), Import (buy up to 3 units for 1M each in home provinces), Maneuver (fleets adjacent sea; armies adjacent land or via fleets; rail within home; 3 armies can destroy a factory; place flags in newly occupied regions), Investor (pay bond interest; investor card gets 2M and may invest; Swiss bank owners may also invest; passing executes investor steps 2–3), Taxation (tax: 2M per unoccupied factory, 1M per flag; dividend if tax track increases; add power points; treasury collects tax minus 1M per army/fleet). Game ends at 25 power points; score = bond interest x nation factor + personal cash.

### Handler Signature Pattern
- **Transformation modules** (`SetToStartingPositionsCommand.fromContract`, `MoveCommand.fromContract`, `InvoicePaidInboundEvent.fromContract`, `InvoicePaymentFailedInboundEvent.fromContract`): Modules named after domain types; accept Contract types, validate inputs, return `Result<DomainType, string>` with plain string errors
- **Router functions (public API)** are exposed only from the `[<RequireQualifiedAccess>] module Rondel` facade in `Rondel/Rondel.fsi/.fs` and called as `Rondel.execute` / `Rondel.handle`:
  - `Rondel.execute`: Routes `RondelCommand` union type to appropriate internal command handler; accepts `RondelDependencies` record, then `RondelCommand`; returns `Async<unit>` for implicit CancellationToken propagation; throws exceptions for business rule violations
  - `Rondel.handle`: Routes `RondelInboundEvent` union type to appropriate internal event handler; accepts `RondelDependencies` record, then `RondelInboundEvent`; returns `Async<unit>`; throws exceptions on errors
- **Internal command handlers** (`setToStartingPositions`, `move`): Accept a `LoadRondelState` function, then domain command types; return `Async<RondelEffects>`; throw exceptions for business rule violations; marked `internal`, not exposed in `.fsi`. Routers commit the returned effects.
- **Internal event handlers** (`onInvoicePaid`, `onInvoicePaymentFailed`): Accept a `LoadRondelState` function, then domain event types (after transformation from Contract types); return `Async<RondelEffects>`; throw exceptions on errors; marked `internal`, not exposed in `.fsi`. Routers commit the returned effects.
- **Unified dependencies**: All Rondel handlers accept a single `RondelDependencies` record with `Async<_>` based dependencies (`{ Load: Id -> Async<RondelState option>; Commit: RondelEffects -> Async<unit> }`). `Load` resolves current state; `Commit` durably applies the resulting effects (state, integration events, outbound commands) as an atomic unit. Implementations use `async {}` CE with `let!`/`do!` bindings. The split lets each host (terminal sandbox, future durable runtimes) own the commit-boundary primitive without re-shaping the core surface.
- **Public API surface**: Only the facade `Rondel` module is exposed in `Rondel/Rondel.fsi` (and similarly `Accounting` in `Accounting/Accounting.fsi`); individual handlers live in `Handlers.fs` and pure logic in `Movement.fs` / `Invoices.fs` as `module internal`. This provides a clean, minimal API with single entry points for commands and events.
- Dependency composition: the host wires `Load` directly and builds `Commit` from its own persistence/publish/dispatch primitives. The terminal sandbox uses `RondelDirectCommit.create store.Save bus.Publish dispatch`, which sequences save → publish → dispatch with `failwith` failure semantics. Load uses domain `RondelState` and `Id`; persistence adapters map to/from `Contract.Rondel.RondelState`. Outbound commands use domain types (`RondelOutboundCommand`) with per-command `toContract` transformations targeting appropriate bounded contexts.
- Signature files define public shape first; implementations should not widen the surface in `.fs`.
- **AsyncExtensions module**: Provides `Async.AwaitTaskWithCT` helper for calling Task-based libraries (e.g., EF Core, Marten, Azure SDK) with the implicit CancellationToken from async context. Usage: `let! result = Async.AwaitTaskWithCT (fun ct -> library.MethodAsync(arg, ct))`.

### Rondel Implementation Patterns
- **Two-layer architecture**: Transformation layer (validates inputs, returns `Result`) + Handler layer (validates business rules, throws exceptions, returns `Async<unit>`)
- **Transformation modules** (`SetToStartingPositionsCommand`, `MoveCommand`): Named after domain command types; use `Result` monad to validate Contract inputs (GameId, Space names) and construct domain types
- **Handler pipeline pattern**: Internal handlers in `Rondel/Handlers.fs` follow a `load state → execute pure logic → return effects` shape using `async {}` CE. The public routers (`Rondel.execute`, `Rondel.handle`) orchestrate the pipeline by collecting the returned effects and invoking `deps.Commit` exactly once per command/event. Internal handlers delegate to pure functions in dedicated files (`Movement.execute` in `Rondel/Movement.fs`, `SetToStartingPositions.execute` in `Rondel/Handlers.fs`, `OnInvoicePaid.handle` / `OnInvoicePaymentFailed.handle` in `Rondel/Invoices.fs`), preserving the separation between business rules and IO.
- **Pure execution modules**: Each command/event has a corresponding `module internal` containing pure business logic (`Movement`, `SetToStartingPositions`, `OnInvoicePaid`, `OnInvoicePaymentFailed`). The `execute`/`handle` function in each module accepts command/event and state, returns `(state, events, commands)` tuple with no IO. The `Handlers` module lifts each tuple into a named `RondelEffects` record via its private `toEffects` helper. This enables testing business rules without mocking IO dependencies.
- **Commit boundary**: `RondelEffects = { State; IntegrationEvents; OutboundCommands }` names the data shape returned by handlers; `CommitRondelEffects = RondelEffects -> Async<unit>` is the infrastructure-owned commit function. The terminal layer supplies the direct (non-durable) implementation via `RondelDirectCommit.create save publish dispatch` (`src/Imperium.Terminal/Rondel/DirectCommit.fs`), which sequences save → publish → dispatch and propagates failures as exceptions. Future durable hosts plug their own outbox-backed commit into the same seam.
- **Record construction**: Use type annotation for contract state construction: `let newState : Contract.Rondel.RondelState = { GameId = ...; NationPositions = ...; PendingMovements = ... }`. F# records use `{ }` syntax directly, not `TypeName { }`.
- **MoveOutcome type**: Internal discriminated union with named fields carrying complete context (targetSpace, distance, nation, rejectedCommand). All cases encapsulate necessary data, eliminating closure dependencies on outer scope variables for cleaner functional design.
- **Decision chain**: Validates moves through `Decision` monad (`noMovesAllowedIfNotInitialized` → `noMovesAllowedForNationNotInGame` → `firstMoveIsFreeToAnyPosition` → `decideMovementOutcome`) producing `MoveOutcome`. Lives in `Rondel/Movement.fs`.
- **Side-effect separation**: `handleMoveOutcome` transforms `MoveOutcome` to a `(state, events, commands)` tuple inside the `Movement` module; the `Handlers.move` adapter lifts that to `RondelEffects` and returns it. All IO lives in the injected `Commit` (terminal: `RondelDirectCommit`), invoked by the facade router.
- **Command dispatch**: Uses `List.fold` with `Result.bind` to sequence outbound commands (`RondelOutboundCommand` with `ChargeMovement` and `VoidCharge` cases), returning first error or `Ok ()`. Infrastructure layer receives domain commands and calls per-command `toContract` transformations to dispatch to appropriate bounded contexts.

### Open Work (current)
- Rondel public API: Two routers (`Rondel.execute` for commands, `Rondel.handle` for events) plus two query handlers (`Rondel.getNationPositions`, `Rondel.getRondelOverview`) on the `[<RequireQualifiedAccess>] module Rondel` facade; all return `Async<unit>` / `Async<_>` for implicit CancellationToken propagation. Individual command/event handlers are internal implementation details in `Rondel/Handlers.fs`.
- Rondel internal structure: Internal handlers in `Rondel/Handlers.fs` follow `load → execute → return effects` using `async {}` CE; public facade routers commit the effects via `deps.Commit`. Pure business logic isolated in internal modules (`Movement.execute` in `Movement.fs`, `SetToStartingPositions.execute` in `Handlers.fs`, `OnInvoicePaid.handle` / `OnInvoicePaymentFailed.handle` in `Invoices.fs`) returning `(state, events, commands)` tuples that `Handlers.toEffects` lifts to `RondelEffects`. The terminal sandbox supplies the direct commit implementation (`RondelDirectCommit.create`) sequencing save → publish → dispatch with `failwith` failure semantics.
- Rondel internal handlers: `setToStartingPositions` complete (delegates to `SetToStartingPositions.execute` for pure validation and state creation); `move` complete (delegates to `Movement.execute` for clockwise distance calculation, 1-3 space free moves with immediate action determination, 4-6 space paid moves with charge dispatch and pending state storage (formula: (distance - 3) * 2M), rejects 0-space (stay put) and 7+ space (exceeds max) moves, automatically voids old charges and rejects old pending moves when a nation initiates a new move before previous payment completes); `onInvoicePaid` complete with idempotent payment confirmation processing (ignores events for non-existent pending movements, handles duplicate payment events or already-completed/voided movements, fails fast on state corruption); `onInvoicePaymentFailed` complete (delegates to `OnInvoicePaymentFailed.handle` for payment failure processing; removes pending movements when payment fails; emits `MoveToActionSpaceRejected` event; includes idempotent handling for duplicate or already-processed events).
- Accounting public API: One router (`Accounting.execute` for commands) on the `[<RequireQualifiedAccess>] module Accounting` facade returns `Async<unit>` for implicit CancellationToken propagation; skeleton implementation for Phase 1.
- Accounting internal structure: Stateless skeleton with no persistent state, split across `Accounting/Commands.fsi/.fs` (commands and command transformations), `Accounting/Events.fsi/.fs` (events and event transformations), `Accounting/Dependencies.fsi/.fs` (dependency types), `Accounting/Handlers.fs` (internal command handlers), and `Accounting/Accounting.fsi/.fs` (public facade). `Handlers.chargeNationForRondelMovement` auto-approves charges (immediately publishes `RondelInvoicePaid` event), `Handlers.voidRondelCharge` is no-op. Future implementation can add balance tracking, transaction history, and payment validation.
- Add public API for Gameplay or trim placeholder if unused.
- Tests use helper pattern: private record types (`Rondel`, `Accounting`) with routers (sync wrappers using `Async.RunSynchronously`), factory functions return router record with async dependencies + observable collections for verification.

### Multi-Environment Architecture (Phase 1: Terminal App)
- See `docs/architecture.md` for full architecture and design decisions.
- **Terminal app** (`Imperium.Terminal`): In-process app with Terminal.Gui v2 (`2.0.0-develop.5259`), MailboxProcessor hosting, in-memory store.
- **Key patterns:**
  - `IBus` interface for cross-bounded-context **events** (pub/sub with generic `Publish<'T>` and `Subscribe<'T>`); implementation uses `ConcurrentDictionary<Type, obj>` storing typed handler lists to avoid boxing events on publish
  - Thunk injection for cross-BC **commands** (breaks circular dependencies, type-safe direct calls)
  - Each BC has a `Host` (e.g., `RondelHost`, `AccountingHost`) with `Execute` entry point
  - MailboxProcessor serializes commands/events per BC with fire-and-forget `Post` (no reply channel); queries bypass for direct store access
  - `SupervisedMailbox` centralizes per-message `try/with` so terminal hosts remain alive after individual handler failures; mailbox error handlers are `Async<unit>` and currently publish `SystemNotification` events through the bus
  - Domain events used directly (not contract types) since everything is in-process; `RondelBillingId.ofId` enables domain-to-domain event conversion without contract layer
  - `SystemEvent` DU remains focused on UI lifecycle events (`AppStarted`, `NewGameStarted`, `GameEnded`, `MoveNationRequested`); `SystemNotification` carries UI-visible notifications such as mailbox processing failures
  - UI views are module functions (`RondelView.create`, `EventLogView.create`) returning `FrameView` — all state managed via shared mutable records, views subscribe to bus events
- **Project structure:**
  ```
  src/Imperium.Terminal/
  ├── Bus.fs                    # IBus interface and factory
  ├── SupervisedMailbox.fs      # Shared mailbox supervision helper for terminal hosts
  ├── Rondel/
  │   ├── Store.fs              # RondelStore with InMemoryRondelStore
  │   ├── Host.fs               # RondelHost with MailboxProcessor, event subscriptions, query handlers
  │   └── UI/
  │       └── RondelView.fs     # Stateless canvas grid with SelectionMode and shared RondelViewState
  ├── Accounting/
  │   └── Host.fs               # AccountingHost with MailboxProcessor, publishes inner events
  ├── Shell/
  │   ├── UI.fs                 # Shared UI helpers (invokeOnMainThread, frameView, mkAttr)
  │   ├── SystemEvent.fs        # UI lifecycle events plus SystemNotification types
  │   ├── EventLogView.fs       # Bus-driven event log panel
  │   └── App.fs                # Application layout, menu bar, keyboard shortcuts
  └── Program.fs                # Entry point, wiring hosts and bus
  ```
- **Execute acknowledgment semantics (decision #74):** Host `Execute` methods use fire-and-forget `Post` — they return `Async<unit>` immediately after the command enters the mailbox queue (enqueue acknowledgment only). Callers do not await processing completion. Processing errors are caught by `SupervisedMailbox` and reported via `SystemNotification` events on the bus. This is intentional: the two-layer architecture validates inputs synchronously (transformation layer returns `Result`) before enqueue, so invalid commands never reach the mailbox. For the future web API, this means: transform contract → domain (400 on validation error), then `Execute` + return 202 Accepted. Completion acknowledgment is not provided and not needed.
- **RondelHost implementation:** `SupervisedMailbox` processes `Command` and `InboundEvent` messages; subscribes to `AccountingEvent`; converts using `RondelBillingId.ofId`; dispatches to Accounting via thunk; queries call domain handlers directly. Mailbox failures publish `SystemNotification` with source `RondelHost`; severity is `Error` for command failures and `Warning` for inbound event failures (e.g., events targeting unknown/uninitialized games are expected in sandbox phase).
- **AccountingHost implementation:** `SupervisedMailbox` processes commands; publishes `AccountingEvent` values directly to the bus for RondelHost subscriptions. Mailbox failures publish `SystemNotification` with source `AccountingHost`.
- **Technology choices (terminal):** Terminal.Gui v2 (TUI framework with views, menu bars, keyboard/mouse support), direct function calls for in-process messaging.
- **RondelView architecture:** Stateless `RondelCanvas` (zero mutable fields) reads from shared `RondelViewState` record with mutable fields (`CurrentGame`, `Selection`, `Positions`). `SelectionMode` record (`Nation` + `Space`) replaces separate selection tracking — single `Option` makes state transitions atomic. `SyncFocus()` method toggles canvas focus based on selection state. Navigation uses `RondelLayout.nextSpace`/`prevSpace` helpers. `onSpaceSelected: Space -> unit` callback replaces direct `RondelHost` dependency. Color scheme: Investor=teal, Import=orange, Production=grey, Maneuver=green, Taxation=yellow, Factory=blue. Emoji flags (🇦🇹🇫🇷🇩🇪🇬🇧🇮🇹🇷🇺) on nation abbreviations with `displayWidth` helper for correct terminal centering.

## Agent Skills

### research-issue

- **Canonical workflow:** `.agents/workflows/research-issue/WORKFLOW.md`
- **Claude wrapper:** `.claude/skills/research-issue/SKILL.md`
- **Codex repo skill wrapper:** `.agents/skills/research-issue/SKILL.md`
- **Invoke in Claude:** `/research-issue <issue-number-or-description>`
- **Invoke in Codex:** ask for the `research-issue` skill or request in-depth issue research.
- **Purpose:** In-depth research and analysis of a GitHub issue or free-text problem description before implementation. Produces a structured report with multiple competing approaches, code sketches, pro/con analysis, and architecture alignment assessment.
- **Read-only:** The skill cannot modify project files — it only produces analysis and a research report at `/tmp/research-issue-{number}.md` or `/tmp/research-topic.md`.
- **Interactive:** Runs inline with explicit pause points between phases for Q&A. Ask clarifying questions at any time.
- **Phases:** Problem understanding → Architecture mapping → External research → Approach development (minimum 3) → Final report

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
4. `dotnet run --no-build --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj -- --render-spec-markdown` — ensure specification markdown still renders successfully

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
| 7 | **Dependencies** | `LoadRondelState` function type plus the effect shape (`RondelEffects`) and commit boundary (`CommitRondelEffects`) using `Async<_>` for implicit CancellationToken propagation, and the unified dependency record (`RondelDependencies = { Load; Commit }`) |
| 8 | **Transformations** | Modules with `fromContract` (Contract → Domain), `toContract` (Domain → Contract) functions (including per-outbound-command `toContract`) |
| 9 | **Handlers (Internal Types)** | `.fs` only: file-private `RondelEffects.ofTuple` helper (lifts the `(state, events, commands)` tuple to the named effect record), internal modules with pure `execute` functions (`Move.execute`, `SetToStartingPositions.execute`), internal DUs for routing/outcomes (`MoveOutcome`) |
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
- Test modules are organized by concern: bounded-context behavior specs in `Imperium.UnitTests.Rondel` and `Imperium.UnitTests.Accounting`, transformation validation in `*ContractTests.fs`, and infrastructure plumbing in `*HostTests.fs` plus terminal bus/store tests.
- Use `[<Tests>]` attribute on test values for discovery by YoloDev.Expecto.TestSdk (enables VS Code Test Explorer integration).
- Execute `dotnet test` (via TestSdk) or `dotnet run --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj` (native Expecto runner with colorized output).
- Execute `dotnet run --no-build --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj -- --render-spec-markdown` to verify the spec-markdown rendering path and regenerate the specification document output when needed.
- The native Expecto runner and `--render-spec-markdown` accept Expecto's filter flags (`--filter`, `--filter-test-list`, `--filter-test-case`, `--run`, `--join-with`). `--run` supports exact expectation paths plus the project-specific hierarchical extension documented in `docs/architecture.md`; multiple filter flags use last-wins behavior. `dotnet test` uses VSTest filtering instead, e.g. `dotnet test --filter SpecFilter`. The markdown renderer omits BC sections whose specs all fail the filter and prints `_no specs match the filter_` when the entire document is empty.
- Test organization: group related tests with `testList`, use descriptive test names in lowercase ("accepts valid GUID", not "AcceptsValidGuid").
- Cover edge cases: null inputs, empty strings, invalid formats, boundary conditions.

### CE-Based Testing (Simple.Testing style)

The `Spec.fs` module provides a computation expression-based testing approach inspired by Gregory Young's Simple.Testing pattern. Use declarative `on`/`when_`/`expect` syntax for readable, isolated test specifications with assertion-native expectations.

**Core types:**
- `Action<'cmd, 'evt>`: DU with `Execute`, `Handle` cases
- `Expectation<'ctx>`: Record with `Description: string` and `Assert: 'ctx -> unit` (uses full Expecto assertion API)
- `ExpectationOutcome`: DU with `Passed` and `Failed of exn` cases
- `ExpectationRunResult<'state>`: Record capturing `Description`, `InitialState`, `FinalState`, and `Outcome`
- `Specification<'ctx, 'seed, 'cmd, 'evt>`: Pure data describing a test scenario
- `SpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>`: Record-of-functions for context-specific execution, with `SpecRunner.empty` providing no-op defaults
- `CollectionAssert`: Accessor-bound helper for reusable assertion functions over context collections such as events and commands
- `NoState`: Marker type for stateless contexts (F# doesn't allow `unit` as generic return type)

**Usage pattern:**
```fsharp
let private specs =
    let spec = specOn createContext

    [ spec "chargeNationForRondelMovement auto-approves" {
          when_command (ChargeNationForRondelMovement cmd)
          expect "publishes exactly one event" (fun ctx ->
              Expect.equal ctx.Events.Count 1 "should publish one event")
          expect "event is RondelInvoicePaid" (fun ctx ->
              Expect.isTrue
                  (match ctx.Events.[0] with RondelInvoicePaid _ -> true | _ -> false)
                  "first event should be RondelInvoicePaid")
      }

      spec "one-off context override stays available" {
          on createSpecialContext
          when_command (ChargeNationForRondelMovement specialCmd)
          expect "special context is used" (fun ctx ->
              Expect.equal ctx.Events.Count 1 "should publish one event")
      } ]

[<Tests>]
let tests = testList "Accounting" (specs |> List.map (toExpecto runner))
```

**Key design decisions:**
- **Assertion-native expectations**: Expectations are `'ctx -> unit` functions that call Expecto assertions directly (`Expect.equal`, `Expect.isTrue`, `Expect.contains`, etc.). Failures are thrown exceptions with rich diagnostic messages.
- **Shared execution via `runExpectation`**: Both `toExpecto` and markdown rendering use the same execution primitive that captures outcomes as `ExpectationRunResult` records.
- **Each expectation is its own testCase**: `toExpecto` creates a `testList` where each expectation runs the full `on`/`when_` sequence independently for isolation. Failed outcomes are rethrown via `ExceptionDispatchInfo.Capture(ex).Throw()` preserving stack traces.
- **Actions as data**: `when_` collects actions declaratively; runner controls execution (enables logging, timing, etc.).
- **State capture for reporting**: Runner's `CaptureState` provides initial/final state snapshots in `ExpectationRunResult`, used for markdown state rendering.
- **Collection assertion reuse**: Prefer `CollectionAssert.forAccessor` to bind `events`, `commands`, or similar collections once per spec module, then compose assertion helpers via `Has`, `HasAny`, `HasNone`, `Count`, and `HasSize` instead of repeating `Seq.exists`/`Seq.filter` helpers per domain.
- **Markdown continuation**: Markdown rendering executes all expectations and renders all results (pass and fail) without aborting on the first failure.

**Reference implementation:** See `Accounting.fs` and `Rondel.fs` for complete examples.

- **Testing approach:**
  - **Transformation validation tests** (in `*ContractTests.fs`): Test `fromContract` transformations with Contract types to verify input validation returns appropriate errors; use domain types directly in test setup
  - **Behavior specs** (in `Rondel.fs` and `Accounting.fs`): Use CE-based `spec` definitions with `on`, optional `state`, optional setup `actions`, `when_`, and multiple `expect` assertions using the full Expecto API
  - **Runner pattern**: Use `{ SpecRunner.empty with ... }` to define runners that execute commands/events, optionally seed state, and capture state snapshots for reporting
  - **Assertion helper pattern**: For repeated collection checks, define accessors like `let private events = CollectionAssert.forAccessor (fun (ctx: MyContext) -> ctx.Events :> seq<_>)` and compose module-local assertion helpers from that accessor using `Has`, `HasAny`, `HasNone`, `Count`, `HasSize`. Module-local helper functions use the `assert*` prefix (e.g., `assertExactEvent`, `assertNationPosition`) so call sites read as assertions rather than predicates.
  - **Separation**: Keep transformation layer tests independent from behavior specs to reduce boilerplate and keep intent explicit
- Current test coverage snapshot:
  - **AccountingContractTests.fs** (6 transformation validation tests):
    - ChargeNationForRondelMovementCommand.fromContract: requires valid GameId; requires valid BillingId; accepts valid command
    - VoidRondelChargeCommand.fromContract: requires valid GameId; requires valid BillingId; accepts valid command
  - **Accounting.fs** (5 CE-based spec expectations across 2 specs):
    - charging a nation for paid movement confirms payment
    - voiding a charge records no accounting outcome
  - **RondelContractTests.fs** (5 transformation validation tests):
    - SetToStartingPositionsCommand.fromContract: rejects Guid.Empty; rejects empty nations array; accepts duplicate nations (Set deduplicates to 2 from 3)
    - MoveCommand.fromContract: rejects unknown rondel space; rejects Guid.Empty
  - **Rondel.fs** (60 CE-based spec expectations across 25 specs):
    - starting setup for nation roster (initial placement and idempotency)
    - move validation and pricing rules (initialization required, stay-put rejection, free vs paid thresholds, max-distance rejection)
    - superseding pending paid movements (voiding prior charge, rejecting stale target, completing free supersession)
    - payment inbound events (`onInvoicePaid`, `onInvoicePaymentFailed`) including idempotency and voided/superseded payment handling
    - query handlers (`getNationPositions`, `getRondelOverview`) for unknown and initialized game states
  - **TerminalBusTests.fs** (6 Bus tests):
    - publish with no subscribers does nothing
    - subscriber receives published event
    - multiple subscribers receive same event
    - different event types are isolated
    - failing subscriber does not block later subscribers
    - subscriber added during publish only affects later publishes
  - **TerminalRondelStoreTests.fs** (3 store tests):
    - load returns None for unknown game
    - save then load returns saved state
    - save overwrites existing state
  - **RondelHostTests.fs** (7 plumbing tests with exponential backoff):
    - wires command execution to domain
    - wires domain events to bus
    - wires outbound commands to dispatch thunk
    - wires bus events to domain handler
    - wires queries to store
    - keeps processing commands after a handler failure
    - logs warning when inbound event targets unknown game
  - **AccountingHostTests.fs** (3 plumbing tests):
    - wires command execution to domain
    - publishes events to bus
    - keeps processing commands after a handler failure
  - **SpecTests.fs** (31 spec infrastructure, filter, and markdown tests):
    - context factory behavior (specOn default, explicit on override, last-on-wins, plain spec compatibility)
    - assertion expectations pass and fail correctly
    - runExpectation captures action failures, state snapshots, preserve behavior, and exception types
    - `SpecFilter` parsing and application for `--filter`, `--filter-test-list`, `--filter-test-case`, `--run`, `--join-with`, hierarchical `--run`, empty `--run`, and last-wins behavior
    - `SpecMarkdown.render` empty and non-empty rendering behavior
  - **GameplayTests.fs** (placeholder module):
    - currently no executable test cases

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
