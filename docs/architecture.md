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

All command/event handlers follow the same four-step pattern:

1. **Load** — Fetch current state from the store via the injected `Load` dependency
2. **Execute** — Pure business logic in internal modules (e.g., `Move.execute`) returns a `(state, events, commands)` tuple
3. **Return effects** — Handlers wrap the tuple as a `RondelEffects` record and return it; no IO is performed inside the handler
4. **Commit** — The public router (`execute`, `handle`) invokes the injected `Commit` once per command/event; infrastructure decides how to apply the effects atomically

This separates pure domain logic from IO and concentrates the commit policy in a single seam.

## Dependency Injection

Dependencies are records of `Async<_>` functions — no IoC container:

```fsharp
type RondelDependencies =
    { Load: LoadRondelState
      Commit: CommitRondelEffects }
```

The terminal sandbox composes `Commit` from `RondelDirectCommit.create store.Save bus.Publish dispatch`, which sequences save → publish → dispatch with `failwith` failure semantics. Future hosts substitute their own commit implementation (durable outbox, transactional session, actor persistence) at the same seam. `CancellationToken` flows implicitly through the `Async` context without explicit threading.

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

Both `SpecRunner.toExpectoTestList` and markdown rendering share the `SpecRunner.runExpectation` execution path:

- **`SpecRunner.toExpectoTestList`** — creates Expecto `testCase` values that call `runExpectation` inside each test thunk. Failed outcomes are rethrown via `ExceptionDispatchInfo.Capture(ex).Throw()` to preserve stack traces and exception types.
- **`Markdown.render`** — calls `runExpectation` for every expectation and renders all results. Failures do not abort rendering; each expectation result is shown with pass/fail status and failure messages.

### Filtering

The unit test runner exposes Expecto's filter flags (`--filter`, `--filter-test-list`, `--filter-test-case`, `--run`, `--join-with`) for selecting which specs run. The same flags also apply to `--render-spec-markdown` so the rendered document can be scoped to a subset of specs.

The filter operates on the hierarchical path `[ "Imperium"; bcName; specName; expectationDescription ]`, matching Expecto's semantics:

- `--filter HIERA` — case-sensitive `StartsWith` prefix match on the joined path (default separator `.`, configurable via `--join-with`).
- `--filter-test-list NAME` — case-sensitive `Contains` substring match against any non-leaf segment (root, BC, spec name).
- `--filter-test-case NAME` — case-sensitive `Contains` substring match against the leaf (expectation description) only.
- `--run PATH...` — case-sensitive match against one or more full joined paths. Values are consumed until the next `--`-prefixed flag. Expectation paths match by exact equality, and this codebase adds a hierarchical extension: a path ending at any segment boundary (for example root, BC, or spec) matches all expectations below it.
- `--join-with SEP` — configures the path separator used by `--filter` and `--run`. Supported values are `.` (default) and `/`; when multiple `--join-with` flags are present, the last one wins.
- Multiple filter flags compose via last-wins: each new `--filter`, `--filter-test-list`, `--filter-test-case`, or `--run` flag overrides any prior filter flag. Multiple `--run` flags also use last-wins behavior. An empty `--run` value list matches nothing.

In the markdown renderer, BC sections containing no surviving specs are omitted entirely. When every section is empty, the output reduces to the title plus `_no specs match the filter_`.

The implementation lives in `SpecFilter` (parser + apply transform) and `Markdown.render` (renders a section or omits it). `Main.fs` orchestrates: parses args via `SpecFilter.fromArgs`, calls each BC's `renderMarkdown` to filter+render its section, drops `None` sections, and assembles the document.

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
