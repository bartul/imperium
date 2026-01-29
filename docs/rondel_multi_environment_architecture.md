# Rondel Multi-Environment Architecture

This document describes the architecture for completing the rondel query implementation with support for two runtime environments.

Last updated: 2026-01-27

---

## Current Implementation Status

Status of query implementation (from `rondel_query_implementation.md`):

| Step | Status | Notes |
|------|--------|-------|
| Domain Types | ✅ Complete | Query types in `Rondel.fsi` |
| Tests & Handlers | ✅ Complete | 28 tests passing |
| Infrastructure Abstractions | ❌ Not started | No storage abstractions |
| Web API & Contracts | ⚠️ Partial | Bare-bones Web project exists |

**Implementation notes:**
- Uses "View" suffix convention (`RondelPositionsView`, `RondelView`) instead of "Result" suffix from original doc
- Two separate query handlers (`getNationPositions`, `getRondelOverview`) instead of single router
- Handlers return `Async<'T option>` directly

---

## Target Runtime Environments

### Environment 1: Terminal UI Application

- **Persistence:** In-memory (development), file-based JSON (production)
- **Domain hosting:** MailboxProcessor or similar agent model
- **Message exchange:** In-process mediator pattern
- **Use case:** Local development, single-player testing, demos

### Environment 2: Web Application

- **Persistence:** SQL database (PostgreSQL via Marten or similar)
- **Domain hosting:** Actor framework (Akka.NET, Orleans, or similar)
- **Message exchange:** External service bus
- **Use case:** Production deployment, multiplayer, distributed

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Imperium (existing domain)                         │
│  Rondel.fs: execute, handle, getNationPositions, getRondelOverview          │
│  RondelDependencies, RondelQueryDependencies (injected)                     │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
         ┌─────────────────────────────┼─────────────────────────────┐
         │                             │                             │
         ▼                             ▼                             ▼
┌─────────────────────────┐  ┌─────────────────────────┐  ┌─────────────────────────┐
│ Imperium.Hosting        │  │ Imperium.Infrastructure │  │ Imperium.Contract       │
│ (NEW)                   │  │ (NEW)                   │  │ (extend existing)       │
│                         │  │                         │  │                         │
│ • IRondelHost           │  │ • InMemoryRondelStore   │  │ • Query request DTOs    │
│ • MailboxProcessor      │  │ • FileRondelStore       │  │ • Query response DTOs   │
│   host (terminal)       │  │ • MartenRondelStore     │  │                         │
│ • Actor-based host      │  │ • (other stores)        │  │                         │
│   (web)                 │  │                         │  │                         │
└────────────┬────────────┘  └────────────┬────────────┘  └────────────┬────────────┘
             │                            │                            │
             └────────────────────────────┼────────────────────────────┘
                                          │
                    ┌─────────────────────┴─────────────────────┐
                    │                                           │
                    ▼                                           ▼
        ┌───────────────────────────┐           ┌───────────────────────────┐
        │ Imperium.Terminal (NEW)   │           │ Imperium.Web (extend)     │
        │                           │           │                           │
        │ • In-process messaging    │           │ • ASP.NET Minimal API     │
        │ • MailboxProcessor host   │           │ • Service bus integration │
        │ • In-memory/file store    │           │ • Actor-based hosting     │
        │ • Terminal UI             │           │ • Document/SQL storage    │
        └───────────────────────────┘           └───────────────────────────┘
```

---

## New Projects

### Imperium.Infrastructure

Storage implementations providing `RondelDependencies` and `RondelQueryDependencies`.

```
src/Imperium.Infrastructure/
├── Imperium.Infrastructure.fsproj
├── Store.fs                          # IRondelStore interface
├── InMemory/
│   └── InMemoryRondelStore.fs        # ConcurrentDictionary-based
├── File/
│   └── FileRondelStore.fs            # JSON file persistence
└── Marten/                           # Or other document store
    └── MartenRondelStore.fs
```

**Core abstraction:**

```fsharp
/// Unified store providing both write and query dependencies
type IRondelStore =
    abstract WriteDependencies: RondelDependencies
    abstract QueryDependencies: RondelQueryDependencies
```

---

### Imperium.Hosting

Domain logic hosting abstractions for different execution models.

```
src/Imperium.Hosting/
├── Imperium.Hosting.fsproj
├── Host.fs                           # IRondelHost interface
├── Mailbox/
│   └── MailboxRondelHost.fs          # F# MailboxProcessor
└── Actor/
    └── ActorRondelHost.fs            # Actor framework implementation
```

**Core abstractions:**

```fsharp
/// Messages for hosted rondel processing
type RondelMessage =
    | ExecuteCommand of RondelCommand * AsyncReplyChannel<Result<unit, string>>
    | HandleEvent of RondelInboundEvent * AsyncReplyChannel<Result<unit, string>>
    | QueryPositions of GetNationPositionsQuery * AsyncReplyChannel<RondelPositionsView option>
    | QueryOverview of GetRondelOverviewQuery * AsyncReplyChannel<RondelView option>

/// Hosted rondel processor (per-game instance)
type IRondelHost =
    abstract ExecuteCommand: RondelCommand -> Async<Result<unit, string>>
    abstract HandleEvent: RondelInboundEvent -> Async<Result<unit, string>>
    abstract QueryPositions: GetNationPositionsQuery -> Async<RondelPositionsView option>
    abstract QueryOverview: GetRondelOverviewQuery -> Async<RondelView option>
    abstract Dispose: unit -> unit

/// Factory for creating game-specific hosts
type IRondelHostFactory =
    abstract Create: Id -> IRondelHost
```

---

### Imperium.Messaging

Message exchange abstractions for cross-bounded-context communication.

```
src/Imperium.Messaging/
├── Imperium.Messaging.fsproj
├── Bus.fs                            # Core interfaces
├── InProcess/
│   └── InProcessBus.fs               # Direct or mediator-based
└── ServiceBus/
    └── ServiceBusAdapter.fs          # External bus integration
```

**Core abstractions:**

```fsharp
/// Sends commands to other bounded contexts (e.g., Rondel → Accounting)
type ICommandBus =
    abstract Send<'TCommand> : 'TCommand -> Async<Result<unit, string>>

/// Publishes domain events for subscribers
type IEventBus =
    abstract Publish<'TEvent> : 'TEvent -> Async<unit>
```

---

### Imperium.Terminal

Terminal UI application for local/single-player use.

```
src/Imperium.Terminal/
├── Imperium.Terminal.fsproj
├── Program.fs                        # Entry point, DI composition
├── GameSession.fs                    # Game orchestration
└── UI/
    ├── RondelView.fs                 # Rondel display
    └── Commands.fs                   # User input handling
```

---

### Imperium.Web (extend existing)

Web API application for distributed/multiplayer use.

```
src/Imperium.Web/
├── Imperium.Web.fsproj
├── Program.fs                        # DI composition, middleware
├── Endpoints/
│   └── RondelEndpoints.fs            # Minimal API routes
├── Handlers/
│   └── AccountingEventHandler.fs     # Inbound event processing
└── Configuration/
    └── ServiceRegistration.fs        # Environment-specific DI
```

**API Endpoints:**

```fsharp
// Commands
POST /api/games/{gameId}/rondel/start
POST /api/games/{gameId}/rondel/move

// Queries
GET  /api/games/{gameId}/rondel/positions
GET  /api/games/{gameId}/rondel/overview
```

---

## Contract Extensions

Extend `Contract.Rondel.fs` with query DTOs for API serialization.

```fsharp
// Query request DTOs
type GetNationPositionsRequest = { GameId: Guid }
type GetRondelOverviewRequest = { GameId: Guid }

// Query response DTOs
type NationPositionDto = {
    Nation: string
    CurrentSpace: string option
    PendingSpace: string option
}

type RondelPositionsResponse = {
    GameId: Guid
    Positions: NationPositionDto list
}

type RondelOverviewResponse = {
    GameId: Guid
    Nations: string list
    IsInitialized: bool
}
```

**Transformations in `Rondel.fs`:**

```fsharp
module GetNationPositionsQuery =
    val fromContract: Contract.Rondel.GetNationPositionsRequest -> Result<GetNationPositionsQuery, string>

module RondelPositionsView =
    val toContract: RondelPositionsView -> Contract.Rondel.RondelPositionsResponse

module GetRondelOverviewQuery =
    val fromContract: Contract.Rondel.GetRondelOverviewRequest -> Result<GetRondelOverviewQuery, string>

module RondelView =
    val toContract: RondelView -> Contract.Rondel.RondelOverviewResponse
```

---

## Technology Choices

### Terminal (Decided)

| Component | Choice | Rationale |
|-----------|--------|-----------|
| **Event Bus** | IBus Interface | Generic `Publish<'T>` and `Subscribe<'T>` methods. Dictionary-based type-keyed dispatch. No boxing at API boundary. |
| **TUI Framework** | Hex1b | React/Flutter-inspired declarative widgets. Persistent layout with diff-based updates. Good for real-time game dashboard. |
| **TUI Fallback** | Spectre.Console + FsSpectre | If Hex1b proves problematic (pre-1.0, C# API friction), fall back to Spectre with computation expressions. |

### Terminal (Pending)

| Component | Options | Notes |
|-----------|---------|-------|
| **File Persistence Format** | JSON (System.Text.Json), SQLite, MessagePack | For future iteration when adding file-based persistence |

### Web (Pending Analysis)

| Option | Description |
|--------|-------------|
| Akka.NET | Mature, feature-rich, complex |
| Microsoft Orleans | Virtual actors, simpler model |
| Proto.Actor | Lightweight, modern |
| Dapr Actors | Cloud-native, platform-agnostic |
| None (stateless) | Simple request/response, no actor model |

### Web Document/SQL Storage

| Option | Description |
|--------|-------------|
| Marten | PostgreSQL document store + event sourcing |
| Entity Framework Core | Traditional ORM |
| Dapper | Micro-ORM, manual SQL |
| Raw Npgsql | Direct PostgreSQL access |

### Web Service Bus

| Option | Description |
|--------|-------------|
| MassTransit + RabbitMQ | Self-hosted, mature |
| MassTransit + Azure Service Bus | Cloud-managed |
| Wolverine | Lightweight, F# friendly |
| NServiceBus | Enterprise, commercial |
| Rebus | Simple, lightweight |

### File Persistence Format (Terminal)

| Option | Description |
|--------|-------------|
| JSON (System.Text.Json) | Standard, human-readable |
| JSON (Newtonsoft) | More features, slower |
| MessagePack | Binary, fast, compact |
| SQLite | Embedded database |

---

## Environment Composition Examples

### Terminal Environment

```fsharp
let configureTerminalServices (services: IServiceCollection) =
    // Storage
    services.AddSingleton<IRondelStore, InMemoryRondelStore>()  // or FileRondelStore

    // Hosting
    services.AddSingleton<IRondelHostFactory, MailboxRondelHostFactory>()

    // Messaging (choice pending)
    // Option A: MediatR
    // Option B: Direct calls
    // Option C: Custom mediator
```

### Web Environment

```fsharp
let configureWebServices (services: IServiceCollection) =
    // Storage (choice pending)
    // services.AddMarten(...) or services.AddDbContext(...)

    // Hosting (choice pending)
    // services.AddAkka(...) or services.AddOrleans(...) or stateless

    // Messaging (choice pending)
    // services.AddMassTransit(...) or services.AddWolverine(...)
```

---

## Open Questions

1. **Event sourcing:** Should web environment use event sourcing (Marten, Akka.Persistence) or simple state storage?

2. **Read model separation:** Should queries use a separate read model/projection, or query the write model directly?

3. **Game lifecycle:** How are games created, discovered, and cleaned up across environments?

4. **Authentication/Authorization:** How will players be identified and authorized (especially in web)?

5. **Real-time updates:** Should web environment support real-time notifications (SignalR, WebSockets)?

---

## Phase 1: Terminal Application

First implementation phase - build a working terminal app to validate the architecture.

### Implementation Status

| Component | Status | Notes |
|-----------|--------|-------|
| `Bus.fs` | ✅ Complete | Generic `Publish<'T>`/`Subscribe<'T>`, typed handler lists (no boxing) |
| `Rondel/Store.fs` | ✅ Complete | `RondelStore` record + `InMemoryRondelStore` (ConcurrentDictionary) |
| `Rondel/Host.fs` | ✅ Complete | MailboxProcessor, event subscriptions, query handlers, thunk dispatch |
| `Accounting/Host.fs` | ✅ Complete | MailboxProcessor, publishes inner event types to bus |
| `Program.fs` | ✅ Complete | Composition root with lazy hosts, REPL for testing |
| TUI (Hex1b) | ❌ Not started | UI layer (future work) |

**Tests:** 14 Terminal tests passing (4 Bus, 3 Store, 5 RondelHost, 2 AccountingHost)

**Future work:**
- Integration tests exercising full RondelHost ↔ AccountingHost flow (paid move → charge → payment → completion)
- File-based persistence for production use
- Hex1b TUI for interactive gameplay

### Goals

1. Validate domain integration with MailboxProcessor hosting
2. Validate Hex1b TUI for game state display
3. Create foundation for later iterations (file persistence, web app)

### Components to Build

```
src/Imperium.Terminal/
├── Imperium.Terminal.fsproj
├── Bus.fs                            # IBus interface and factory (Dictionary-based)
├── Rondel/
│   ├── Store.fs                      # RondelStore record + InMemoryRondelStore factory
│   └── Host.fs                       # RondelHost with DispatchToAccounting thunk
├── Accounting/
│   └── Host.fs                       # AccountingHost skeleton
├── Program.fs                        # Entry point, composition root
└── UI/                               # (future)
    ├── Widgets.fs                    # F# wrappers for Hex1b widgets
    ├── RondelBoard.fs                # Rondel wheel visualization
    ├── NationList.fs                 # Nation positions panel
    └── App.fs                        # Main app widget composition
```

**Design approach:** Records of functions with factory modules. Matches domain style (`RondelDependencies`), easy to mock for testing, composable.

### Architecture

**Key constraints:**
- MailboxProcessor serializes commands and inbound events (writes) per bounded context
- Queries access stores directly for minimal latency
- Bus enables cross-context event communication (events only)
- Command dispatch uses direct function calls via thunk injection

### Events vs Commands

| Concern | Mechanism | Rationale |
|---------|-----------|-----------|
| **Events** | Bus (pub/sub) | Broadcast to multiple subscribers, loose coupling |
| **Commands** | Direct function call | Targeted single handler, type safe, no boxing |

Terminal uses **domain event types** directly (not contract types) for cross-BC communication since everything is in-process. This simplifies the architecture by avoiding transformation layers.

### Breaking Circular Dependencies

F# doesn't allow circular module dependencies. When RondelHost dispatches commands to AccountingHost (and potentially vice versa in the future), we use **lazy/thunk injection**:

```fsharp
// Host accepts a thunk that resolves the target at call time
type DispatchToAccounting = unit -> (AccountingCommand -> Async<Result<unit, string>>)

module RondelHost =
    let create store bus (getAccountingExecute: DispatchToAccounting) : RondelHost = ...

// Composition root uses recursive lazy values to break the cycle
let rec rondelHost =
    lazy (RondelHost.create store bus (fun () -> accountingHost.Value.Execute))
and accountingHost =
    lazy (AccountingHost.create bus (Some (fun () -> rondelHost.Value.Execute)))
```

**Benefits:**
- Type safe (no boxing/casting for commands)
- Direct function calls at runtime
- Easy to stub for testing
- F# idiomatic pattern for breaking cycles

```
┌───────────────────────────────────────────────────────────────────────────┐
│                             Hex1b App Loop                                │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────────────────────┐  │
│  │ RondelBoard │     │ NationList  │     │ CommandPanel                │  │
│  │   Widget    │     │   Widget    │     │ (Move, End Turn, Quit)      │  │
│  └──────┬──────┘     └──────┬──────┘     └──────────────┬──────────────┘  │
└─────────┼───────────────────┼───────────────────────────┼─────────────────┘
          │ query             │ query                     │ command
          ▼                   ▼                           ▼
┌─────────────────────────────────────┐  ┌─────────────────────────────────┐
│           RondelHost                │  │         AccountingHost          │
│                                     │  │                                 │
│  Subscribes to events:              │  │  Receives commands via thunk:   │
│  • RondelInvoicePaidEvent →         │  │  • ChargeMovement →             │
│    Rondel.handle(InvoicePaid)       │  │    process & publish            │
│  • RondelInvoicePaymentFailedEvent →│  │    RondelInvoicePaidEvent       │
│    Rondel.handle(PaymentFailed)     │  │  • VoidCharge →                 │
│                                     │  │    process void                 │
│  Publishes via bus:                 │  │                                 │
│  • RondelEvent (domain events)      │  │  Publishes via bus:             │
│                                     │  │  • RondelInvoicePaidEvent       │
│  Dispatches via thunk:              │  │  • RondelInvoicePaymentFailed   │
│  • ChargeMovement → Accounting      │  │                                 │
│  • VoidCharge → Accounting          │  │                                 │
│                                     │  │                                 │
│  ┌───────────────────────────────┐  │  │  ┌───────────────────────────┐  │
│  │ MailboxProcessor (sequential) │  │  │  │ MailboxProcessor          │  │
│  └───────────────────────────────┘  │  │  └───────────────────────────┘  │
└───────────┬───────────────┬─────────┘  └────────────┬────────────────────┘
            │               │                         │
            │ dispatch      │ publish/subscribe       │ publish
            │ (direct call) │ (events)                │ (events)
            │               ▼                         ▼
            │    ┌────────────────────────────────────────────────────────┐
            │    │                    Bus (events only)                   │
            │    │                                                        │
            │    │  • Publish: obj -> Async<unit>                         │
            │    │  • Register: Type -> (obj -> Async<unit>) -> unit      │
            │    └────────────────────────────────────────────────────────┘
            │
            └──────────────────────────► AccountingHost.Execute (thunk)

                 │                                       │
                 │ load/save                             │ (stateless)
                 ▼                                       ▼
┌─────────────────────────────────────┐  ┌─────────────────────────────────┐
│      RondelStore (record)           │  │      (no store - auto-approve)  │
│      • InMemoryRondelStore.create() │  │                                 │
└─────────────────────────────────────┘  └─────────────────────────────────┘
```

### Key Types (Functional Style)

Uses records of functions with factory modules - matches domain patterns (`RondelDependencies`).

```fsharp
// ──────────────────────────────────────────────────────────────────────────
// Bus.fs
// ──────────────────────────────────────────────────────────────────────────

// Cross-cutting event bus for bounded context communication
type IBus =
    abstract Publish<'T> : 'T -> Async<unit>
    abstract Subscribe<'T> : ('T -> Async<unit>) -> unit

module Bus =
    open System
    open System.Collections.Concurrent

    /// Creates a new IBus instance
    /// Uses typed handler lists to avoid boxing events on publish
    let create () : IBus =
        let handlers = ConcurrentDictionary<Type, obj>()

        { new IBus with
            member _.Publish<'T>(event: 'T) =
                async {
                    match handlers.TryGetValue(typeof<'T>) with
                    | true, list ->
                        let typedList = list :?> ResizeArray<'T -> Async<unit>>

                        for handler in typedList do
                            do! handler event
                    | false, _ -> ()
                }

            member _.Subscribe<'T>(handler: 'T -> Async<unit>) =
                let list =
                    handlers.GetOrAdd(typeof<'T>, fun _ -> ResizeArray<'T -> Async<unit>>() :> obj)
                    :?> ResizeArray<'T -> Async<unit>>

                list.Add(handler) }

// ──────────────────────────────────────────────────────────────────────────
// Infrastructure/InMemoryRondelStore.fs
// ──────────────────────────────────────────────────────────────────────────

/// Store as record of functions (no interface needed)
type RondelStore = {
    Load: Id -> Async<RondelState option>
    Save: RondelState -> Async<Result<unit, string>>
}

module InMemoryRondelStore =
    open System.Collections.Concurrent

    let create () : RondelStore =
        let states = ConcurrentDictionary<Id, RondelState>()
        { Load = fun id -> async {
              return match states.TryGetValue(id) with
                     | true, state -> Some state
                     | false, _ -> None }
          Save = fun state -> async {
              states.[state.GameId] <- state
              return Ok () } }

// ──────────────────────────────────────────────────────────────────────────
// Hosting/RondelHost.fs
// ──────────────────────────────────────────────────────────────────────────

/// Host as record of functions
type RondelHost = {
    /// SEQUENCED - goes through MailboxProcessor
    ExecuteCommand: RondelCommand -> Async<Result<unit, string>>
    /// DIRECT - bypasses mailbox, queries store directly
    QueryPositions: GetNationPositionsQuery -> Async<RondelPositionsView option>
    /// DIRECT - bypasses mailbox, queries store directly
    QueryOverview: GetRondelOverviewQuery -> Async<RondelView option>
}

type private HostMessage =
    | ExecuteCommand of RondelCommand * AsyncReplyChannel<Result<unit, string>>
    | HandleInboundEvent of RondelInboundEvent * AsyncReplyChannel<Result<unit, string>>

module RondelHost =
    let create (store: RondelStore) (bus: Bus) : RondelHost =

        // Build domain dependencies
        let deps : RondelDependencies = {
            Load = store.Load
            Save = store.Save
            Publish = Bus.publish bus
            Dispatch = fun cmd ->
                // Dispatch outbound commands via bus
                match cmd with
                | ChargeMovement c -> Bus.publish bus c
                | VoidCharge v -> Bus.publish bus v
                async { return Ok () }
        }

        let queryDeps : RondelQueryDependencies = { Load = store.Load }

        // Agent serializes commands/events
        let agent = MailboxProcessor.Start(fun inbox ->
            let rec loop () = async {
                match! inbox.Receive() with
                | ExecuteCommand(cmd, reply) ->
                    let! result = async {
                        try
                            do! Rondel.execute deps cmd
                            return Ok ()
                        with ex -> return Error ex.Message
                    }
                    reply.Reply(result)

                | HandleInboundEvent(evt, reply) ->
                    let! result = async {
                        try
                            do! Rondel.handle deps evt
                            return Ok ()
                        with ex -> return Error ex.Message
                    }
                    reply.Reply(result)

                return! loop ()
            }
            loop ())

        // Helper to send inbound events through agent
        let handleInbound evt = async {
            let! _ = agent.PostAndAsyncReply(fun r -> HandleInboundEvent(evt, r))
            ()
        }

        // Register subscriptions to events from other bounded contexts
        bus.Register typeof<Contract.Accounting.InvoicePaidEvent>
            (Bus.subscription
                InvoicePaidInboundEvent.fromContract
                (fun e -> handleInbound (InvoicePaid e)))

        bus.Register typeof<Contract.Accounting.PaymentFailedEvent>
            (Bus.subscription
                InvoicePaymentFailedInboundEvent.fromContract
                (fun e -> handleInbound (InvoicePaymentFailed e)))

        // Return host record
        { ExecuteCommand = fun cmd ->
              agent.PostAndAsyncReply(fun r -> ExecuteCommand(cmd, r))
          QueryPositions = fun q -> Rondel.getNationPositions queryDeps q
          QueryOverview = fun q -> Rondel.getRondelOverview queryDeps q }

// ──────────────────────────────────────────────────────────────────────────
// Hosting/AccountingHost.fs (minimal for terminal)
// ──────────────────────────────────────────────────────────────────────────

type AccountingHost = {
    // Minimal - just processes charges and publishes results
}

module AccountingHost =
    let create (bus: Bus) : AccountingHost =

        // Subscribe to charge requests from Rondel
        bus.Register typeof<Rondel.ChargeMovementOutboundCommand>
            (fun (o: obj) -> async {
                let cmd = o :?> Rondel.ChargeMovementOutboundCommand
                // Simple: auto-approve all charges
                let paidEvent : Contract.Accounting.InvoicePaidEvent = {
                    InvoiceId = Guid.NewGuid()
                    BillingId = Rondel.RondelBillingId.value cmd.BillingId
                }
                do! Bus.publish bus paidEvent
            })

        bus.Register typeof<Rondel.VoidChargeOutboundCommand>
            (fun (o: obj) -> async {
                // Void is a no-op for now
                ()
            })

        { }

// ──────────────────────────────────────────────────────────────────────────
// Program.fs - Composition
// ──────────────────────────────────────────────────────────────────────────

module App =
    let create () =
        let bus = Bus.create ()
        let rondelStore = InMemoryRondelStore.create ()

        // Create hosts - they register their subscriptions with bus
        let rondelHost = RondelHost.create rondelStore bus
        let accountingHost = AccountingHost.create bus

        // UI can also subscribe to events for refresh
        bus.Register typeof<Rondel.RondelEvent>
            (fun (o: obj) -> async {
                // Trigger Hex1b re-render
                ()
            })

        rondelHost

    let run () =
        let host = create ()
        // Start Hex1b app loop with host functions
        // ...
```

### Hex1b Integration Notes

Hex1b is C#-oriented. Create thin F# wrappers:

```fsharp
// UI/Widgets.fs - F# helpers for Hex1b
module Imperium.Terminal.UI.Widgets

open Hex1b
open Hex1b.Widgets

let vstack children = VStackWidget(children |> List.toArray)
let hstack children = HStackWidget(children |> List.toArray)
let text content = TextBlockWidget(content)
let textStyled content style = TextBlockWidget(content, style)
let button label action = ButtonWidget(label, Action(action))
let empty () = EmptyWidget()

let grid (rows: Widget list list) =
    GridWidget(rows |> List.map (fun row -> row |> List.toArray) |> List.toArray)
```

### Event Bus Fallback: Explicit Channels

If the subscription builder pattern proves problematic (boxing overhead, runtime type errors), fall back to explicit typed channels:

```fsharp
// Fallback: Each event type has its own strongly-typed channel
type Channel<'T> = {
    Publish: 'T -> Async<unit>
    Subscribe: ('T -> Async<unit>) -> unit
}

module Channel =
    let create<'T> () : Channel<'T> =
        let subscribers = ResizeArray<'T -> Async<unit>>()
        { Publish = fun event -> async { for s in subscribers do do! s event }
          Subscribe = fun handler -> subscribers.Add(handler) }

// Bus becomes a record of typed channels
type Bus = {
    // From Accounting
    InvoicePaid: Channel<Contract.Accounting.InvoicePaidEvent>
    PaymentFailed: Channel<Contract.Accounting.PaymentFailedEvent>
    // From Rondel
    RondelEvent: Channel<Rondel.RondelEvent>
    ChargeMovement: Channel<Rondel.ChargeMovementOutboundCommand>
    VoidCharge: Channel<Rondel.VoidChargeOutboundCommand>
}

// Usage in RondelHost - fully type-safe, no boxing
bus.InvoicePaid.Subscribe(fun contractEvent -> async {
    match InvoicePaidInboundEvent.fromContract contractEvent with
    | Ok e -> do! handleInbound (InvoicePaid e)
    | Error _ -> ()
})
```

**Trade-off:** More boilerplate (channel per event type) but full compile-time type safety.

### Minimum Viable Features

1. **Display rondel board** - Show 8 spaces in circular arrangement
2. **Display nation positions** - Show where each nation is / pending moves
3. **Start game** - Initialize with nations
4. **Move nation** - Select nation, select target space
5. **Show move result** - Success, rejection, or pending payment

### Dependencies

```xml
<!-- Imperium.Terminal.fsproj -->
<PackageReference Include="Hex1b" Version="0.48.0" />
<ProjectReference Include="../Imperium/Imperium.fsproj" />
```

---

## Related Documentation

- `docs/rondel_query_implementation.md` - Original query implementation guide (domain types, tests, infrastructure, web API steps)
- `docs/module_design_process.md` - Three-phase development process
- `AGENTS.md` - Project structure and conventions
