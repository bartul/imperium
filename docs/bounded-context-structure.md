# Bounded context blueprint

Each bounded context owns its domain types and exposes its operations through a same-named facade, such as `Gameplay` or `Rondel`. On a write, the facade routes the incoming message to an internal handler. In Gameplay and Rondel, the handler describes any state update, events, and outbound commands for the host to apply. Accounting is stateless and publishes payment results through an injected function. Queries use separate read dependencies and return views.

## Files and compile order

```text
src/Imperium/
├── Primitives/    Shared values such as Id and Amount
├── Contract/      One file per context for data exchanged at its boundary
├── Accounting/    Charges and payment results
├── Gameplay/      Game lifecycle and setup
└── Rondel/        Movement, positions, and pending payments

tests/Imperium.UnitTests/Imperium/
├── Accounting/    Accounting behavior
├── Gameplay/      Gameplay behavior, queries, and value types
├── Rondel/        Rondel behavior and queries
└── Contract/      Contract transformations
```

Give each context a PascalCase folder and an `Imperium.<Context>` namespace. Add files as the context gains the capabilities below. Each filename stem stands for a matching `.fsi` signature and `.fs` implementation unless an extension is shown.

| File | Responsibility | Present in |
| --- | --- | --- |
| `<Context>` | Public facade for commands, inbound events, and queries supported by the context. | All three |
| `<Context>Command` | Commands the context accepts, their payloads, and conversions from contracts. | All three |
| `<Context>Event` | Integration events the context publishes, their payloads, and conversions to contracts. | All three |
| `<Context>Dependencies` | Records of host-supplied functions for writes and, where needed, reads. | All three |
| `Handlers.fs` | Internal command handlers and, where applicable, inbound event handlers. | All three |
| `<Context>InboundEvent` | Events consumed from other contexts, their local payloads, and inbound conversions. | Gameplay, Rondel |
| `<Context>OutboundCommand` | Commands addressed to other contexts and conversions to the recipients’ contracts. | Gameplay, Rondel |
| `<Context>State` | State, updates, and persistence mappings where needed. | Gameplay, Rondel |
| `<Context>Effects` | The optional state update, integration events, and outbound commands produced by a write. | Gameplay, Rondel |
| `<TypeName>` | A standalone domain type and its companion module, such as `GameId` or `Space`. | Gameplay, Rondel |
| `Queries` | Query inputs, internal query handlers, and any views defined alongside them. | Gameplay, Rondel |
| `Projections` | View types and projection functions used independently of query handlers. | Gameplay |
| `Movement.fs`, `Invoices.fs` | Domain rules grouped into internal modules as the handlers grow. | Rondel |

Use a singular filename for a routing union, even if it has several cases. The name reflects who owns the message. Accounting publishes `RondelInvoicePaid` as a case of `AccountingEvent`. The terminal host converts it to `InvoicePaid`, a case of `RondelInboundEvent`, and enqueues it for `Rondel.handle`. [Published event](../src/Imperium/Accounting/AccountingEvent.fsi) · [Inbound event](../src/Imperium/Rondel/RondelInboundEvent.fsi) · [Host conversion](../src/Imperium.Terminal/Rondel/Host.fs)

**Between contexts.** Compile `Primitives` and `Contract` first in `Imperium.fsproj`. The Gameplay, Accounting, and Rondel file groups can then compile in any order. Each context owns its domain types and uses shared contracts at context boundaries. A direct reference to another context’s domain type would introduce a compile-order dependency.

**Within a context.** List each `.fsi` immediately before its `.fs` and put types before their consumers, including a projection type before any dependency that returns it. Compile internal rule modules before the handlers that call them and the facade after its dependencies, handlers, and queries. Gameplay and Rondel compile `Queries` on different sides of `Handlers` because both orders satisfy their dependencies. [Project compile order](../src/Imperium/Imperium.fsproj)

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

Inject dependencies as records of F# functions. Gameplay declares both `LoadGameplayState` and `CommitGameplayEffects` in its dependencies file. A function type can also live beside its payload, as `CommitRondelEffects` does in `RondelEffects.fsi`. The Gameplay signatures below show how the host receives the result of a write. [Gameplay dependencies](../src/Imperium/Gameplay/GameplayDependencies.fsi) · [Gameplay effects](../src/Imperium/Gameplay/GameplayEffects.fsi) · [Rondel effects](../src/Imperium/Rondel/RondelEffects.fsi)

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
2. Run the business rules on the typed input and loaded state. Gameplay’s handlers build effects directly. Rondel’s handlers delegate movement and invoice rules to internal modules and convert their results into `RondelEffects`.
3. Call `Commit` once after the handler succeeds. An empty effects record can represent an idempotent request. **`State = None` skips the state update.** The same record can still contain events to publish or commands to dispatch.

Use `Async<_>` at the IO boundary so cancellation flows through the workflow. Handler and dependency exceptions propagate through the facade’s `Async<unit>`. [Gameplay facade](../src/Imperium/Gameplay/Gameplay.fs) · [Rondel handlers](../src/Imperium/Rondel/Handlers.fs) · [Rondel facade](../src/Imperium/Rondel/Rondel.fs)

## Query dependencies

Define a separate `<Context>QueryDependencies` record with only the functions the reads need. Expose a query input and a view through a named facade function, and keep the implementation in `module internal Queries`. Return `Async<View option>` for a lookup that can miss. Gameplay loads a status view through `LoadStatus` and exposes a pure `GameplayStatusProjection.fromState` mapper. Rondel uses its read-side `Load` function and computes views within `Queries`. [Gameplay queries](../src/Imperium/Gameplay/Queries.fs) · [Gameplay projection](../src/Imperium/Gameplay/Projections.fs) · [Rondel queries](../src/Imperium/Rondel/Queries.fs)

## Contracts and host wiring

Put DTOs in `src/Imperium/Contract/<Context>.fs`, in a module named for the context under `Imperium.Contract`. Contract files expose their types directly and use `.fs` alone. Use serializable fields such as `Guid` and `string` and shared values such as `Amount` where appropriate. At a contract boundary, validate inputs with `fromContract : ContractType -> Result<DomainType, string>` on the receiving payload's companion module. Use `toContract` for integration events and outbound commands, with commands targeting the receiving context's contract. [Gameplay contracts](../src/Imperium/Contract/Gameplay.fs) · [Gameplay inbound event conversion](../src/Imperium/Gameplay/GameplayInboundEvent.fs) · [Gameplay outbound command conversion](../src/Imperium/Gameplay/GameplayOutboundCommand.fs)

Context code depends on shared primitives and contracts. The host supplies stores, publishers, and dispatch functions. Because the terminal hosts run in one process, they can translate domain messages directly. [Terminal Rondel host](../src/Imperium.Terminal/Rondel/Host.fs) · [Terminal composition root](../src/Imperium.Terminal/Program.fs)

**Commit behavior belongs to the host.** Rondel’s terminal adapter saves the state when present, publishes integration events, then dispatches outbound commands. A failure stops the remaining steps. Earlier saves, publications, or dispatches may already have taken effect. [Terminal commit adapter](../src/Imperium.Terminal/Rondel/DirectCommit.fs)

## Test structure

Put domain tests in `tests/Imperium.UnitTests/Imperium/<Context>/` and use the `Imperium.UnitTests.<Context>` namespace or module prefix. The Gameplay branch below shows the files used by its behavior and query specifications. [Unit test project](../tests/Imperium.UnitTests/Imperium.UnitTests.fsproj)

```text
tests/Imperium.UnitTests/
├── Imperium/
│   ├── Accounting/
│   ├── Gameplay/
│   │   ├── Context.fs              In-memory dependencies and captured effects
│   │   ├── Assertions.fs           Expectations expressed in domain terms
│   │   ├── Specification.fs        Behavior scenarios and their runner
│   │   └── QuerySpecification.fs   Query scenarios using the same runner
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

1. **Create the fixture.** Pass the fixture factory to `specOn` so each expectation gets a fresh context. Build the context-specific runner from `SpecRunner.empty` and wire the supported routers through `Async.RunSynchronously`. Supply callbacks for the state and effects the fixture tracks. Accounting only needs `Execute` and `ClearEvents`. [Gameplay fixture](../tests/Imperium.UnitTests/Imperium/Gameplay/Context.fs) · [Accounting runner](../tests/Imperium.UnitTests/Imperium/Accounting/Specs.fs)
2. **Exercise the behavior.** Use `given_command` or `given_event` for setup and `when_command` or `when_event` for the action under test. Between setup and the action, the runner invokes the configured clearing callbacks. The callbacks on `SpecRunner.empty` do nothing. Use `preserve` to carry setup effects into the assertion. [Gameplay behavior specs](../tests/Imperium.UnitTests/Imperium/Gameplay/Specification.fs) · [Shared spec runner](../tests/Imperium.UnitTests/Support/Spec/SpecRunner.fs)
3. **Assert the outcome.** Each `expect` runs the scenario independently. Check the fixture’s recorded commands and events, or call a public query through the fixture and assert on the returned view. [Gameplay query specs](../tests/Imperium.UnitTests/Imperium/Gameplay/QuerySpecification.fs)

### Compile order and registration

List new files explicitly in `Imperium.UnitTests.fsproj`. Compile shared support before its consumers and each context's `Context.fs` before `Assertions.fs` and its specs. Compile a context's spec module before any query spec module that reuses its runner. Keep `Main.fs` last. [Unit test compile order](../tests/Imperium.UnitTests/Imperium.UnitTests.fsproj)

Expose an Expecto `Test` value marked `[<Tests>]` for test SDK discovery. For behavior specs, build the test list with `SpecRunner.toExpectoTestList`. Add the test list to `Main.fs` for the native runner, and register its renderer in `renderMarkdown` when the specifications should appear in the generated report. [Native runner and Markdown registration](../tests/Imperium.UnitTests/Main.fs) · [Specification test architecture](architecture.md#specification-test-architecture)

```sh
dotnet test tests/Imperium.UnitTests/Imperium.UnitTests.fsproj
dotnet run --no-build --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj -- --render-spec-markdown
```
