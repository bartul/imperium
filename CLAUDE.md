# CLAUDE.md

This file guides Claude Code (claude.ai/code) for this repository. For shared repo facts, module summaries, and commands, see `AGENTS.md`.
Last verified: 2026-01-14

## Quick Status (last verified: current)

- Rondel public API: Two routers (`execute` for `RondelCommand`, `handle` for `RondelInboundEvent`) return `Async<unit>` for implicit CancellationToken propagation. All internal handlers accept unified `RondelDependencies` record (contains Load, Save, Publish, Dispatch - all `Async<_>` based).
- Rondel internal structure: Handlers follow `load → execute → materialize` pattern using `async {}` CE. Pure business logic isolated in internal modules (`Move.execute`, `SetToStartingPositions.execute`) returning `(state, events, commands)` tuples. Shared `materialize` function uses `async {}` to sequence IO side effects (save, publish, dispatch).
- Rondel internal handlers: `setToStartingPositions` (complete - delegates to `SetToStartingPositions.execute`), `move` (complete - delegates to `Move.execute` which handles 1-3 space free moves, 4-6 space paid moves with charge dispatch and pending state, rejects 0 and 7+ space moves; automatically voids old charges and rejects old pending moves when a nation initiates a new move before previous payment completes; PendingMovements map keyed by nation for efficient lookups), `onInvoicePaid` (complete - idempotent payment confirmation handler; ignores duplicate payment events; gracefully handles late payments for voided charges; fails fast on state corruption), `onInvoicePaymentFailed` (stubbed).
- Rondel outbound commands: Domain types (`ChargeMovementOutboundCommand`, `VoidChargeOutboundCommand`, `RondelOutboundCommand` DU) with per-command `toContract` transformations targeting Accounting bounded context.
- Accounting contract: ChargeNationForRondelMovementCommand, VoidRondelChargeCommand (commands), AccountingCommand (routing DU), AccountingEvent (payment result events).
- Rondel state: handlers load/save domain `RondelState` by `Id`; persistence adapters map to/from `Contract.Rondel.RondelState` via `RondelState.toContract/fromContract`.
- Gameplay and Accounting modules expose no public API yet.
- AsyncExtensions module: Provides `Async.AwaitTaskWithCT` helper for calling Task-based libraries with implicit CancellationToken from async context.
- Tests organized by concern: `RondelContractTests.fs` (5 transformation validation tests: SetToStartingPositionsCommand/MoveCommand `fromContract` input validation) and `RondelTests.fs` (15 handler behavior tests using router pattern). Handler tests use `Rondel` record helper with `Execute`/`Handle` routers (sync wrappers using `Async.RunSynchronously`), `createRondel()` factory returns router record + observable collections (events, commands) for verification. Tests call routers with union types (e.g., `rondel.Execute <| SetToStartingPositions cmd`). Property tests validate move behavior across random nations/spaces (15 iterations each). Total: 20 tests (18 passing, 2 failing until onInvoicePaymentFailed handler implemented).

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
