# CLAUDE.md

This file guides Claude Code (claude.ai/code) for this repository. For shared repo facts, module summaries, and commands, see `AGENTS.md`.
Last verified: 2025-02-22

## Quick Status (last verified: current)
- Rondel handlers (`setToStartingPositions`, `move`, `onInvoicedPaid`, `onInvoicePaymentFailed`) are stubbed placeholders.
- Gameplay and Accounting modules expose no public API yet.
- Tests only cover Rondel `setToStartingPositions` validation/event publication.

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
