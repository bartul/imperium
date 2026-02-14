# CLAUDE.md

This file guides Claude Code (claude.ai/code) for this repository. For shared repo facts, module summaries, and commands, see `AGENTS.md`.
Last verified: 2026-02-15

## Quick Status (last verified: current)

- Rondel public API: Two routers (`execute` for `RondelCommand`, `handle` for `RondelInboundEvent`) return `Async<unit>` for implicit CancellationToken propagation. All internal handlers accept unified `RondelDependencies` record (contains Load, Save, Publish, Dispatch - all `Async<_>` based). Two query handlers (`getNationPositions`, `getRondelOverview`) accept `RondelQueryDependencies` record and return `Async<Result option>`. `RondelBillingId.ofId` enables creating billing IDs from `Id` for in-process event conversion.
- Rondel internal structure: Handlers follow `load → execute → materialize` pattern using `async {}` CE. Pure business logic isolated in internal modules (`Move.execute`, `SetToStartingPositions.execute`) returning `(state, events, commands)` tuples. Shared `materialize` function uses `async {}` to sequence IO side effects (save, publish, dispatch).
- Rondel internal handlers: `setToStartingPositions` (complete - delegates to `SetToStartingPositions.execute`), `move` (complete - delegates to `Move.execute` which handles 1-3 space free moves, 4-6 space paid moves with charge dispatch and pending state, rejects 0 and 7+ space moves; automatically voids old charges and rejects old pending moves when a nation initiates a new move before previous payment completes; PendingMovements map keyed by nation for efficient lookups), `onInvoicePaid` (complete - idempotent payment confirmation handler; ignores duplicate payment events; gracefully handles late payments for voided charges; fails fast on state corruption), `onInvoicePaymentFailed` (complete - delegates to `OnInvoicePaymentFailed.handle`; removes pending movements when payment fails; emits `MoveToActionSpaceRejected` event; idempotent handling for duplicates and already-processed events).
- Rondel outbound commands: Domain types (`ChargeMovementOutboundCommand`, `VoidChargeOutboundCommand`, `RondelOutboundCommand` DU) with per-command `toContract` transformations targeting Accounting bounded context.
- Accounting contract: ChargeNationForRondelMovementCommand, VoidRondelChargeCommand (commands), AccountingCommand (routing DU), AccountingEvent (payment result events).
- Rondel state: handlers load/save domain `RondelState` by `Id`; persistence adapters map to/from `Contract.Rondel.RondelState` via `RondelState.toContract/fromContract`.
- Accounting public API: One router (`execute` for `AccountingCommand`) returns `Async<unit>` for implicit CancellationToken propagation. Handlers accept unified `AccountingDependencies` record (contains Publish - `Async<_>` based). Skeleton implementation: `chargeNationForRondelMovement` auto-approves charges (immediately publishes `RondelInvoicePaid`), `voidRondelCharge` is no-op.
- Accounting internal structure: Stateless skeleton with no persistent state. Handlers follow simple pattern: receive command → publish event (or do nothing). Pure auto-approve behavior suitable for Phase 1 terminal app; future implementation can add balance tracking, transaction history, and payment validation.
- Gameplay module exposes no public API yet.
- Terminal app (Phase 1 complete): `Imperium.Terminal` project using Terminal.Gui v2 (`2.0.0-develop.5027`). `IBus` interface (generic `Publish<'T>`/`Subscribe<'T>` for events), thunk injection for cross-BC commands (`DispatchToAccounting`), `RondelHost` (MailboxProcessor, event subscriptions, query handlers), `AccountingHost` (MailboxProcessor, publishes inner event types to bus), `InMemoryRondelStore` (ConcurrentDictionary-based). UI layer: `App.fs` (menu bar, keyboard shortcuts F1/F2/F4, layout), `RondelView.fs` (stateless canvas grid with `SelectionMode` record and shared `RondelViewState`), `EventLogView.fs` (bus-driven log), `MoveDialog.fs` (nation selection), `SystemEvent.fs` (UI lifecycle events: `AppStarted`, `NewGameStarted`, `GameEnded`, `MoveNationRequested`), `UI.fs` (helpers including `invokeOnMainThread`). See `docs/rondel_multi_environment_architecture.md`.
- AsyncExtensions module: Provides `Async.AwaitTaskWithCT` helper for calling Task-based libraries with implicit CancellationToken from async context.
- Tests organized by concern: `RondelContractTests.fs` (5 transformation validation tests), `RondelTests.fs` (23 handler behavior tests), `AccountingContractTests.fs` (6 transformation validation tests), `AccountingTests.fs` (2 handler behavior tests), `Accounting.fs` (3 CE-based spec tests), `TerminalBusTests.fs` (4 Bus tests), `TerminalRondelStoreTests.fs` (3 store tests), `RondelHostTests.fs` (5 plumbing tests), `AccountingHostTests.fs` (2 plumbing tests), `Rondel.fs` (30 CE-based spec tests). Total: 83 tests (all passing).
- CE-based testing (`Spec.fs`): Simple.Testing-style declarative specs with `on`/`when_`/`expect` syntax. Each expectation becomes its own testCase. Uses `NoState` marker type for stateless contexts. See `AGENTS.md` → "CE-Based Testing" for usage patterns.

## Agent Priorities

- Follow the three-phase process in `docs/module_design_process.md`: define `.fsi`, write tests, then implement.
- Handlers accept unified `RondelDependencies` record for consistency. When adding new handlers, use the same pattern.
- Public API uses routers (`execute`, `handle`) as single entry points; individual handlers are internal implementation details.
- Prefer minimal public surface; align `.fs` to `.fsi` without widening the API.

## CQRS & Contract Patterns (anchor details in AGENTS.md)

- Contracts live in `Imperium.Contract`; commands/events are records/DUs.
- Domain dependency types use `Async<_>` for implicit CancellationToken propagation (e.g., `LoadRondelState = Id -> Async<RondelState option>`, `SaveRondelState = RondelState -> Async<Result<unit, string>>`).
- Domain modules hide internal IDs and state; use two-layer architecture: transformation modules validate Contract → Domain (return `Result`), handlers accept domain types plus injected dependencies and return `Async<unit>`.
- Rondel spaces and rules: use `AGENTS.md` and `docs/official_rules/Imperial_English_Rules.pdf` for full detail; keep tests aligned to contracts.

## Signature Files: Function vs Value

F# distinguishes function definitions from computed values. Avoid partial applications in signatures unless explicitly parenthesized.

```fsharp
// Prefer explicit function definition for tryParse helpers
let tryParse raw = Id.tryParseMap GameId raw

// If you expose a computed value, mark it in the signature:
val tryParse : (string -> Result<T, string>)
```

Reasoning: preserves IL shape, avoids unwanted module-load computation, and keeps inlining options.

## Style Reminders

- F# defaults: 4-space indent, `PascalCase` for types/modules, `camelCase` for functions.
- Favor expression-based, pattern-matching code; keep handlers small and injected dependencies explicit.
- Record construction: Use `{ Field = value }` syntax with type annotation when needed: `let x : RecordType = { ... }`. Never use `RecordType { ... }` - that's not valid F# syntax.
- Module file organization: Follow the 10-section structure in `AGENTS.md` → "Module File Organization" (Value Types → State → Commands → Events → Outbound Commands → Incoming Events → Dependencies → Transformations → Internal Types → Handlers). Use `// ────...` dividers and `///` XML doc comments.
- Type inference pitfall: When records share identical fields (e.g., `MoveCommand` and `MoveToActionSpaceRejectedEvent`), F# picks the last-defined type. Add explicit type annotations: `let fn (cmd: MoveCommand) : Result<MoveCommand, string> = ...`
- Run `dotnet build`/`dotnet test` locally; format with `dotnet fantomas .` (always available via `.config/dotnet-tools.json`).

## Launch Terminal App for Review

To launch the terminal app in a separate Ghostty window for visual review during development:

```bash
open -na Ghostty.app --args --command="dotnet run --project /Users/bartul/code/imperium-experiment-terminal-ui/src/Imperium.Terminal" --window-width=160 --window-height=50 --quit-after-last-window-closed=true
```

Use this as part of the inner development loop: make changes, launch for review, collect feedback, iterate.

## Pre-Commit Checklist

Before every commit, always run these steps in order:
1. `dotnet fantomas .` — format all F# files
2. `dotnet build` — ensure the whole solution compiles with 0 errors and 0 warnings
3. `dotnet test` — ensure all tests pass
