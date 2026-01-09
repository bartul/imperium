# CLAUDE.md

This file guides Claude Code (claude.ai/code) for this repository. For shared repo facts, module summaries, and commands, see `AGENTS.md`.
Last verified: 2026-01-04

## Quick Status (last verified: current)

- Rondel public API: Two routers (`execute` for `RondelCommand`, `handle` for `RondelInboundEvent`) provide single entry points. All internal handlers accept unified `RondelDependencies` record (contains Load, Save, Publish, Dispatch).
- Rondel internal handlers: `setToStartingPositions` (complete), `move` (complete - handles 1-3 space free moves, 4-6 space paid moves with charge dispatch and pending state, rejects 0 and 7+ space moves; automatically voids old charges and rejects old pending moves when a nation initiates a new move before previous payment completes; PendingMovements map keyed by nation for efficient lookups), `onInvoicedPaid` (complete - idempotent payment confirmation handler; ignores duplicate payment events; fails fast on state corruption), `onInvoicePaymentFailed` (stubbed).
- Rondel outbound commands: Domain types (`ChargeMovementOutboundCommand`, `VoidChargeOutboundCommand`, `RondelOutboundCommand` DU) with per-command `toContract` transformations targeting Accounting bounded context.
- Accounting contract: ChargeNationForRondelMovementCommand, VoidRondelChargeCommand (commands), AccountingCommand (routing DU), AccountingEvent (payment result events).
- Rondel state: handlers load/save domain `RondelState` by `Id`; persistence adapters map to/from `Contract.Rondel.RondelState` via `RondelState.toContract/fromContract`.
- Gameplay and Accounting modules expose no public API yet.
- Tests organized by concern: `RondelContractTests.fs` (5 transformation validation tests: SetToStartingPositionsCommand/MoveCommand `fromContract` input validation) and `RondelTests.fs` (11 handler behavior tests using router pattern). Handler tests use `Rondel` record helper with `Execute`/`Handle` routers, `createRondel()` factory returns router record + observable collections (events, commands) for verification. Tests call routers with union types (e.g., `rondel.Execute <| SetToStartingPositions cmd`). Property tests validate move behavior across random nations/spaces (15 iterations each). Total: 16 passing, 0 failing.

## Agent Priorities

- Follow the three-phase process in `docs/module_design_process.md`: define `.fsi`, write tests, then implement.
- Handlers accept unified `RondelDependencies` record for consistency. When adding new handlers, use the same pattern.
- Public API uses routers (`execute`, `handle`) as single entry points; individual handlers are internal implementation details.
- Prefer minimal public surface; align `.fs` to `.fsi` without widening the API.

## CQRS & Contract Patterns (anchor details in AGENTS.md)

- Contracts live in `Imperium.Contract`; commands/events are records/DUs; function types for DI return `Result<unit, string>` with plain-string errors.
- Domain modules hide internal IDs and state; use two-layer architecture: transformation modules validate Contract → Domain (return `Result`), handlers accept domain types plus injected dependencies.
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
