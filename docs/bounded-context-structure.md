# Bounded context blueprint

Use this guide when creating or extending a bounded context in `src/Imperium`. Gameplay shows a stateful context with pure handlers and a read projection. Rondel shows how to split more involved rules into internal modules. Accounting shows the current stateless case with a command router and an injected publisher.

## Files and compile order

Give the context a PascalCase folder, an `Imperium.<Context>` namespace, and a same-named facade module. The filenames below are stems for `.fsi` and `.fs` pairs, except where the extension is shown explicitly.

| File | Contents |
| --- | --- |
| `<Context>` | The public facade with `execute`, `handle` when the context consumes inbound events, and named queries when it offers reads. |
| `<Context>Command` | The command routing union, its payload records, and inbound contract conversions. |
| `<Context>Event` | The integration events the context publishes, their payload records, and conversion to contracts. |
| `<Context>InboundEvent` | The inbound event routing union, its payload records, and conversions from the producer's contracts. |
| `<Context>OutboundCommand` | Commands the context asks other contexts to execute, with conversions to the receiving context's contracts. |
| `<Context>State` | The state record and related types, along with construction, updates, and persistence mappings where needed. |
| `<Context>Effects` | The write-side output record containing an optional state update, integration events, and outbound commands. |
| `<Context>Dependencies` | The injected function types and dependency records for writes and queries. |
| `Handlers.fs` | The internal `Handlers` module for command and inbound event processing. |
| `Queries` | Query inputs, the internal `Queries` module, and view types that belong to those queries. |
| `Projections` | View types and pure projection functions when they are used separately from query handlers. |
| `<TypeName>` | A standalone domain type and its companion module, such as Gameplay's `GameId` or Rondel's `Space`. |

Use singular filenames for routing types even when the union has several cases. `RondelInboundEvent.fsi` and `GameplayInboundEvent.fsi` follow this rule. Add a file when the context gains the capability it describes. Accounting currently uses the facade, commands, events, dependencies, and handlers.

List source files explicitly in `Imperium.fsproj`. Put each `.fsi` immediately before its `.fs`. Compile primitives and contracts before context code. Within each context, put types before their consumers, including a projection type before any dependency that returns it. Compile internal rule modules before the handlers that call them and the facade after its dependencies, handlers, and queries. Gameplay and Rondel compile `Queries` on different sides of `Handlers` because both orders satisfy their dependencies. [Project compile order](../src/Imperium/Imperium.fsproj)

## Public API and domain types

Declare domain types at namespace scope and expose routing through the same-named `[<RequireQualifiedAccess>]` facade. Use a discriminated union for routing and named record payloads for the individual cases. Keep a type and its companion module together in the same pair. Use private constructors and validated creation functions when a value has an invariant. [Gameplay command signature](../src/Imperium/Gameplay/GameplayCommand.fsi) · [GameId signature](../src/Imperium/Gameplay/GameId.fsi)

The Gameplay facade takes the dependency record first and the request second. Callers use qualified names such as `Gameplay.execute` and `Gameplay.getGameplayStatus`. [Gameplay facade signature](../src/Imperium/Gameplay/Gameplay.fsi)

```fsharp
namespace Imperium.Gameplay

[<RequireQualifiedAccess>]
module Gameplay =
    val execute: GameplayDependencies -> GameplayCommand -> Async<unit>
    val handle: GameplayDependencies -> GameplayInboundEvent -> Async<unit>
    val getGameplayStatus: GameplayQueryDependencies -> GetGameplayStatusQuery -> Async<GameplayStatusView option>
```

Use `.fsi` files to define the API and document input, output, and failure behavior. Declare a helper as `val internal` in the signature when another file in the assembly needs it. Keep helpers local to the implementation file when their callers are all there. Prefer descriptive internal rule modules once a behavior needs its own file, as Rondel does with `Movement.fs` and `Invoices.fs`. [Space signature](../src/Imperium/Rondel/Space.fsi) · [Rondel handlers](../src/Imperium/Rondel/Handlers.fs)

## Write-side dependencies and effects

Inject dependencies as records of F# functions. A stateful context loads the current state by ID and commits a named effects record through functions provided by the host. Gameplay uses this shape. [Gameplay dependencies](../src/Imperium/Gameplay/GameplayDependencies.fsi) · [Gameplay effects](../src/Imperium/Gameplay/GameplayEffects.fsi)

```fsharp
type GameplayEffects =
    { State: GameplayState option
      IntegrationEvents: GameplayEvent list
      OutboundCommands: GameplayOutboundCommand list }

type LoadGameplayState = GameId -> Async<GameplayState option>
type CommitGameplayEffects = GameplayEffects -> Async<unit>
type GameplayDependencies = { Load: LoadGameplayState; Commit: CommitGameplayEffects }
```

Expose the effects record so the host can apply it. Keep construction helpers internal when they are used only by domain handlers, as with `GameplayEffects.withEvent`. [Gameplay effects signature](../src/Imperium/Gameplay/GameplayEffects.fsi)

1. The facade routes a command or inbound event to the matching handler. Load the current state through `Load` in the facade, as Gameplay does, or in the handler adapter, as Rondel does.
2. Run the business rules on the typed input and loaded state. Gameplay's handlers build effects directly. Rondel's handlers call pure rule functions and convert their results into `RondelEffects`.
3. Call `Commit` once after the handler succeeds. An empty effect can represent an idempotent request. `State = None` means there is no state update, and integration events and outbound commands can still be present.

Use `Async<_>` at the IO boundary so cancellation flows through the workflow. Handler and dependency exceptions propagate through the facade's `Async<unit>`. The host owns how a batch of effects is applied and what durability it provides. Rondel's terminal adapter saves state, publishes integration events, then dispatches commands sequentially. A failure stops the remaining steps, and earlier steps may already have taken effect. Accounting's stateless handler uses its injected `Publish` function directly. [Gameplay facade](../src/Imperium/Gameplay/Gameplay.fs) · [Rondel facade](../src/Imperium/Rondel/Rondel.fs) · [Terminal commit adapter](../src/Imperium.Terminal/Rondel/DirectCommit.fs) · [Accounting handlers](../src/Imperium/Accounting/Handlers.fs)

## Query dependencies

Define a separate `<Context>QueryDependencies` record with only the functions the reads need. Expose a query input and a view through a named facade function, and keep the implementation in `module internal Queries`. Return `Async<View option>` for a lookup that can miss. Gameplay loads a status view through `LoadStatus` and exposes a pure `GameplayStatusProjection.fromState` mapper. Rondel uses its read-side `Load` function and computes views within `Queries`. [Gameplay queries](../src/Imperium/Gameplay/Queries.fs) · [Gameplay projection](../src/Imperium/Gameplay/Projections.fs) · [Rondel queries](../src/Imperium/Rondel/Queries.fs)

## Contracts and host wiring

Put DTOs in `src/Imperium/Contract/<Context>.fs`, in a module named for the context under `Imperium.Contract`. Contract files expose their types directly and use `.fs` alone. Use serializable fields such as `Guid` and `string` and shared values such as `Amount` where appropriate. At a contract boundary, validate inputs with `fromContract : ContractType -> Result<DomainType, string>` on the receiving payload's companion module. Use `toContract` for integration events and outbound commands, with commands targeting the receiving context's contract. [Gameplay contracts](../src/Imperium/Contract/Gameplay.fs) · [Gameplay inbound event conversion](../src/Imperium/Gameplay/GameplayInboundEvent.fs) · [Gameplay outbound command conversion](../src/Imperium/Gameplay/GameplayOutboundCommand.fs)

Keep the core dependency direction toward shared primitives and contracts. The host composes stores, publishers, and dispatch functions and translates messages between contexts. The terminal host can map between domain messages directly in-process. For example, it converts an `AccountingEvent` to a `RondelInboundEvent` before enqueueing it for `Rondel.handle`. [Terminal Rondel host](../src/Imperium.Terminal/Rondel/Host.fs) · [Terminal composition root](../src/Imperium.Terminal/Program.fs)

## Test structure

The test project mirrors the source projects. Put domain tests in `tests/Imperium.UnitTests/Imperium/<Context>/` and follow the `Imperium.UnitTests.<Context>` namespace or module prefix. [Unit test project](../tests/Imperium.UnitTests/Imperium.UnitTests.fsproj)

```text
tests/Imperium.UnitTests/
├── Imperium/
│   ├── Accounting/
│   ├── Gameplay/
│   ├── Rondel/
│   └── Contract/
├── Imperium.Terminal/
├── Support/
│   ├── Spec/
│   └── Spec.Tests/
└── Main.fs
```

| Location | Contents |
| --- | --- |
| `Imperium/<Context>/Context.fs` | The `Context` test fixture and `Context.create` factory. Gameplay and Rondel inject in-memory `Load` and `Commit`, capture events and outbound commands, and expose query helpers. Accounting injects `Publish` and captures events. |
| `Imperium/<Context>/Assertions.fs` | Reusable Expecto assertions over the fixture's events, commands, and query results. |
| `Imperium/<Context>/Specs.fs` or `Specification.fs` | Behavior scenarios and the context-specific runner that calls the public facade. Accounting and Rondel use `Specs.fs`. Gameplay uses `Specification.fs` and shares its runner with `QuerySpecification.fs`. Rondel keeps its query scenarios in `Specs.fs`. |
| `Imperium/<Context>/<TypeName>Tests.fs` | Direct Expecto tests of a standalone public type, such as `Gameplay/GameIdTests.fs`. |
| `Imperium/Contract/<Context>ContractTests.fs` | Direct Expecto tests of the context's contract transformations, including successful mappings and invalid inputs where applicable. |
| `Imperium.Terminal/` | Host and infrastructure tests, including bus, store, and direct commit behavior. |
| `Support/Spec/` and `Support/Spec.Tests/` | The shared specification computation expression, runner, filtering, assertions, and Markdown rendering, with their framework tests in `Spec.Tests`. |

### Behavior specifications

Define the public shape in `.fsi` first and add a matching `.fs` implementation that compiles. Add one failing behavior test through the public facade, implement the behavior, and verify it before moving to the next case. [Module development process](architecture.md#module-development-process)

Build the context-specific runner from `SpecRunner.empty`. Wire `Execute` and `Handle` to the public routers using `Async.RunSynchronously` as applicable. Add callbacks to seed and capture state and to clear recorded events and commands. The runner clears recorded effects after setup by default. Use `preserve` when the expectation also needs setup effects. Use `specOn` with the fixture factory so each `expect` runs against a fresh context. Use `given_command` or `given_event` for setup and `when_command` or `when_event` for the behavior under test. Query specs can set up state through the same runner and call the public query through a fixture helper inside the assertion. [Gameplay fixture](../tests/Imperium.UnitTests/Imperium/Gameplay/Context.fs) · [Gameplay behavior specs](../tests/Imperium.UnitTests/Imperium/Gameplay/Specification.fs) · [Gameplay query specs](../tests/Imperium.UnitTests/Imperium/Gameplay/QuerySpecification.fs) · [Shared spec runner](../tests/Imperium.UnitTests/Support/Spec/SpecRunner.fs)

### Compile order and registration

List new files explicitly in `Imperium.UnitTests.fsproj`. Compile shared support before its consumers and each context's `Context.fs` before `Assertions.fs` and its specs. Compile a context's spec module before any query spec module that reuses its runner. Keep `Main.fs` last. [Unit test compile order](../tests/Imperium.UnitTests/Imperium.UnitTests.fsproj)

Expose an Expecto `Test` value marked `[<Tests>]` for test SDK discovery. For behavior specs, build the test list with `SpecRunner.toExpectoTestList`. Add the test list to `Main.fs` for the native runner, and register its renderer in `renderMarkdown` when the specifications should appear in the generated report. [Native runner and Markdown registration](../tests/Imperium.UnitTests/Main.fs) · [Specification test architecture](architecture.md#specification-test-architecture)

```sh
dotnet test tests/Imperium.UnitTests/Imperium.UnitTests.fsproj
dotnet run --no-build --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj -- --render-spec-markdown
```
