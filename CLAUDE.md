# CLAUDE.md

This file guides Claude Code (claude.ai/code) for this repository. For shared repo facts, module summaries, and commands, see `AGENTS.md`.
Last verified: 2025-12-15

## Quick Status (last verified: current)

- Rondel handlers: `setToStartingPositions` (complete), `move` (complete - handles 1-3 space free moves, 4-6 space paid moves with charge dispatch and pending state, rejects 0 and 7+ space moves), `onInvoicedPaid`, `onInvoicePaymentFailed` (stubbed).
- Gameplay and Accounting modules expose no public API yet.
- Tests cover Rondel starting positions validation/signaling, `move` first-move-to-any-space (property test with 15 iterations), rejection of moves to current position (property test with 15 iterations), multiple consecutive moves of 1-3 spaces (property test with 15 iterations), rejection of 7-space moves as exceeding maximum distance (property test with 15 iterations), and moves of 4-6 spaces requiring payment with correct amount formula (property test with 15 iterations).

## Agent Priorities

- Follow the three-phase process in `docs/module_design_process.md`: define `.fsi`, write tests, then implement.
- Keep dependency order consistent: publisher first, then persistence, then external services (e.g., accounting).
- Prefer minimal public surface; align `.fs` to `.fsi` without widening the API.

## CQRS & Contract Patterns (anchor details in AGENTS.md)

- Contracts live in `Imperium.Contract`; commands/events are records/DUs; function types for DI return `Result<unit, string>` with plain-string errors.
- Domain modules hide internal IDs and state; public handlers accept contract types plus injected dependencies.
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
- Run `dotnet build`/`dotnet test` locally; format with `dotnet tool run fantomas` if available.
