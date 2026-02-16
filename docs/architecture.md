# Architecture

This document describes the system architecture, design patterns, and development process for the Imperium project.

## Bounded Contexts

The domain is split into isolated bounded contexts that communicate only through Contract DTOs:

- **Rondel** — The central movement mechanic. Nations move clockwise on an 8-space wheel. Handles free moves (1-3 spaces), paid moves (4-6 spaces with charge dispatch), pending payment states, and move supersession. Fully implemented with command/event handlers and query handlers.
- **Accounting** — Processes charges and publishes payment results. Currently a skeleton that auto-approves all charges.
- **Gameplay** — Game orchestration, scoring, turn management. Not yet implemented.

Cross-BC communication uses `Contract.*` DTOs with plain primitive types (`Guid`, `string`, `int`). Domain types never leak across boundaries.

## CQRS Pattern

Each bounded context exposes two routers as its public API:

```fsharp
val execute: RondelDependencies -> RondelCommand -> Async<unit>       // commands
val handle:  RondelDependencies -> RondelInboundEvent -> Async<unit>  // inbound events
```

Queries are separate handlers that bypass write-side serialization:

```fsharp
val getNationPositions: RondelQueryDependencies -> GetNationPositionsQuery -> Async<RondelPositionsView option>
val getRondelOverview:  RondelQueryDependencies -> GetRondelOverviewQuery  -> Async<RondelView option>
```

## Handler Pipeline

All command/event handlers follow the same three-step pattern:

1. **Load** — Fetch current state from the store via injected `Load` dependency
2. **Execute** — Pure business logic in internal modules (e.g., `Move.execute`) returns a `(state, events, commands)` tuple
3. **Materialize** — Shared function sequences IO side effects: save state, publish events, dispatch outbound commands

This separates pure domain logic from IO, making business rules testable without infrastructure.

## Dependency Injection

Dependencies are records of `Async<_>` functions — no IoC container:

```fsharp
type RondelDependencies = {
    Load:     Id -> Async<RondelState option>
    Save:     RondelState -> Async<Result<unit, string>>
    Publish:  RondelEvent -> Async<unit>
    Dispatch: RondelOutboundCommand -> Async<Result<unit, string>>
}
```

`CancellationToken` flows implicitly through the `Async` context without explicit threading.

## Contract Isolation

A two-layer transformation pattern validates data at BC boundaries:

- **Layer 1 — Transformation modules** (`fromContract`/`toContract`): Convert between Contract DTOs and domain types, returning `Result<Domain, string>` on input
- **Layer 2 — Handlers**: Accept validated domain types and injected dependencies, return `Async<unit>`

Contract types live in `Imperium.Contract` and use only primitives suitable for serialization. Domain modules hide internal IDs and state behind `.fsi` signature files.

## Signature Files as API Contracts

F# signature files (`.fsi`) define the public surface of each module. The compiler enforces that implementations cannot widen the API beyond what the signature exposes. This provides compile-time boundary enforcement without runtime overhead.

## Module Development Process

All modules follow a three-phase, interface-first, test-driven approach:

1. **Interface** — Define the public API in the `.fsi` file with complete types and XML docs. Create a matching `.fs` with `failwith "Not implemented"` stubs that compile.
2. **Tests** — Write tests against the `.fsi` interface only. Tests fail initially (red phase).
3. **Implementation** — Implement in `.fs` until all tests pass (green phase). Do not modify the `.fsi`.

Anti-patterns: skipping the interface phase, writing tests after implementation, modifying `.fsi` during implementation, testing internal details.

## Terminal App Architecture

The terminal app (`Imperium.Terminal`) hosts bounded contexts in-process using Terminal.Gui v2.

### MailboxProcessor Serialization

Each bounded context runs behind an F# `MailboxProcessor` agent that serializes write operations (commands and inbound events). Query handlers bypass the mailbox and access the store directly for low latency.

### IBus for Event Communication

A generic `IBus` interface provides typed pub/sub for cross-BC events:

```fsharp
type IBus =
    abstract Publish<'T>   : 'T -> Async<unit>
    abstract Subscribe<'T> : ('T -> Async<unit>) -> unit
```

Internally uses `ConcurrentDictionary<Type, ResizeArray<'T -> Async<unit>>>` — typed handler lists avoid boxing at the API boundary. Events use domain types directly in-process (no Contract round-trip).

### Breaking Circular Dependencies

F# prohibits circular module references. Cross-BC command dispatch uses thunk injection with recursive lazy values at the composition root:

```fsharp
let rec rondelHost: Lazy<RondelHost> =
    lazy (RondelHost.create store bus (fun () cmd -> accountingHost.Value.Execute cmd))
and accountingHost: Lazy<AccountingHost> =
    lazy (AccountingHost.create bus)
```

### Events vs Commands

| Concern      | Mechanism             | Rationale                                        |
| ------------ | --------------------- | ------------------------------------------------ |
| **Events**   | Bus (pub/sub)         | Broadcast to multiple subscribers, loose coupling |
| **Commands** | Direct function call  | Targeted single handler, type safe, no boxing     |
