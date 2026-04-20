# Architecture

This document describes the system architecture, design patterns, and development process for the Imperium project.

## Bounded Contexts

The domain is split into isolated bounded contexts that communicate only through Contract DTOs:

- **Rondel** — The central movement mechanic. Nations move clockwise on an 8-space wheel. Handles free moves (1-3 spaces), paid moves (4-6 spaces with charge dispatch), pending payment states, and move supersession. Fully implemented with command/event handlers and query handlers.
- **Accounting** — Processes charges and publishes payment results. Currently a skeleton that auto-approves all charges.
- **Gameplay** — Game orchestration, scoring, turn management. Not yet implemented.

Cross-BC communication uses `Contract.*` DTOs with plain primitive types (`Guid`, `string`) and shared value types (`Amount`). Domain types never leak across boundaries.

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
type RondelDependencies =
    { Load: LoadRondelState; Save: SaveRondelState; Publish: PublishRondelEvent; Dispatch: DispatchOutboundCommand }
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

## Specification Test Architecture

The unit test project uses computation expression-based specifications to define bounded-context behavior scenarios as data.

Conceptually, a spec contains:

- **Context factory (`on`)** — builds fresh test context per expectation
- **Optional seed (`state`)** — injects initial state when needed
- **Optional setup actions (`given_command`/`given_event`)** — preconditions executed before the main actions
- **Main actions (`when_command`/`when_event`)** — commands/events under test
- **Expectations (`expect`)** — assertion functions (`'ctx -> unit`) that use the full Expecto assertion API

### Execution Model

Specs use assertion-native expectations. Each expectation is an `Assert: 'ctx -> unit` function that calls Expecto assertions directly (e.g., `Expect.equal`, `Expect.isTrue`, `Expect.contains`). Failures are thrown exceptions, not boolean results.

A shared `runExpectation` function executes one expectation through the full spec flow:

1. Build context via `on`
2. Seed state from inline `state` if provided
3. Run setup `given_command`/`given_event` actions
4. Clear setup side-effects unless `preserve` is enabled
5. Capture initial state snapshot (if runner provides `CaptureState`)
6. Run `when_command`/`when_event` actions
7. Capture final state snapshot
8. Execute the assertion function
9. Return an `ExpectationRunResult` with captured state and outcome (`Passed` or `Failed of exn`)

Each `expect` is materialized as its own test case and reruns the full flow above, providing deterministic isolation between expectations.

### Renderers

Both `toExpecto` and markdown rendering share the `runExpectation` execution path:

- **`toExpecto`** — creates Expecto `testCase` values that call `runExpectation` inside each test thunk. Failed outcomes are rethrown via `ExceptionDispatchInfo.Capture(ex).Throw()` to preserve stack traces and exception types.
- **`toMarkdown`** — calls `runExpectation` for every expectation and renders all results. Failures do not abort rendering; each expectation result is shown with pass/fail status and failure messages.

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

Internally uses `ConcurrentDictionary<Type, obj>` with generic `HandlerStore<'T>` wrappers — per-type immutable handler snapshots make publish safe under concurrent subscription. Events use domain types directly in-process (no Contract round-trip).

### Breaking Circular Dependencies

F# prohibits circular module references. Cross-BC command dispatch uses thunk injection with recursive lazy values at the composition root:

```fsharp
let rec rondelHost: Lazy<RondelHost> =
    lazy
        (RondelHost.create store bus (fun () cmd ->
            async {
                do! accountingHost.Value.Execute cmd
                return Ok()
            }))

and accountingHost: Lazy<AccountingHost> = lazy (AccountingHost.create bus)
```

### Events vs Commands

| Concern      | Mechanism             | Rationale                                        |
| ------------ | --------------------- | ------------------------------------------------ |
| **Events**   | Bus (pub/sub)         | Broadcast to multiple subscribers, loose coupling |
| **Commands** | Direct function call  | Targeted single handler, type safe, no boxing     |
