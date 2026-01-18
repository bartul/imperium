# Rondel Query Implementation Guide

This document provides the complete implementation plan for adding query capabilities to the Rondel domain following CQRS patterns.

## Overview

### Queries to Implement

**Query 1: GetNationPositions**
- Input: `GameId`
- Output: List of nation positions with pending movement info

**Query 2: GetRondelOverview**
- Input: `GameId`
- Output: Basic game info with list of nations

### Design Principles

1. **Domain-first**: Query types live in domain layer, contracts added only for web API
2. **TDD**: Write tests before implementation
3. **Symmetric API**: `query` router mirrors `execute` router pattern
4. **Infrastructure agnostic**: Domain defines interfaces, infrastructure implements

---

## Phase 1: Domain Types

**Goal**: Add query types to `Rondel.fsi` and stub implementation to `Rondel.fs`

### Step 1.1: Add to `Rondel.fsi`

Add after the Dependencies section, before Transformations:

```fsharp
// ──────────────────────────────────────────────────────────────────────────
// Queries
// ──────────────────────────────────────────────────────────────────────────

/// Queries for reading rondel state without side effects.
type RondelQuery =
    | GetNationPositions of GetNationPositionsQuery
    | GetRondelOverview of GetRondelOverviewQuery

/// Query for nation positions in a game.
and GetNationPositionsQuery = { GameId: Id }

/// Query for basic rondel overview.
and GetRondelOverviewQuery = { GameId: Id }

// ──────────────────────────────────────────────────────────────────────────
// Query Results
// ──────────────────────────────────────────────────────────────────────────

/// A nation's position on the rondel.
type NationPosition = {
    Nation: string
    CurrentSpace: Space option
    PendingSpace: Space option
}

/// Result of GetNationPositions query.
type NationPositionsResult = {
    GameId: Id
    Positions: NationPosition list
}

/// Result of GetRondelOverview query.
type RondelOverviewResult = {
    GameId: Id
    Nations: string list
    IsInitialized: bool
}

/// Union of all query results for type-safe routing.
type RondelQueryResult =
    | NationPositionsResult of NationPositionsResult option
    | RondelOverviewResult of RondelOverviewResult option

// ──────────────────────────────────────────────────────────────────────────
// Query Dependencies
// ──────────────────────────────────────────────────────────────────────────

/// Load rondel state for queries. Same as write-side Load.
/// Infrastructure may optimize with dedicated read store.
type LoadRondelStateForQuery = Id -> Async<RondelState option>

/// Dependencies for query handlers.
type RondelQueryDependencies = {
    Load: LoadRondelStateForQuery
}

// ──────────────────────────────────────────────────────────────────────────
// Query Handler
// ──────────────────────────────────────────────────────────────────────────

/// Execute a query against rondel state.
/// CancellationToken flows implicitly through Async context.
val query: RondelQueryDependencies -> RondelQuery -> Async<RondelQueryResult>
```

### Step 1.1: Add stub to `Rondel.fs`

Add after the Public Routers section:

```fsharp
// ──────────────────────────────────────────────────────────────────────────
// Queries
// ──────────────────────────────────────────────────────────────────────────

type RondelQuery =
    | GetNationPositions of GetNationPositionsQuery
    | GetRondelOverview of GetRondelOverviewQuery

and GetNationPositionsQuery = { GameId: Id }

and GetRondelOverviewQuery = { GameId: Id }

// ──────────────────────────────────────────────────────────────────────────
// Query Results
// ──────────────────────────────────────────────────────────────────────────

type NationPosition = {
    Nation: string
    CurrentSpace: Space option
    PendingSpace: Space option
}

type NationPositionsResult = {
    GameId: Id
    Positions: NationPosition list
}

type RondelOverviewResult = {
    GameId: Id
    Nations: string list
    IsInitialized: bool
}

type RondelQueryResult =
    | NationPositionsResult of NationPositionsResult option
    | RondelOverviewResult of RondelOverviewResult option

// ──────────────────────────────────────────────────────────────────────────
// Query Dependencies
// ──────────────────────────────────────────────────────────────────────────

type LoadRondelStateForQuery = Id -> Async<RondelState option>

type RondelQueryDependencies = {
    Load: LoadRondelStateForQuery
}

// ──────────────────────────────────────────────────────────────────────────
// Query Handler (stub)
// ──────────────────────────────────────────────────────────────────────────

let query (deps: RondelQueryDependencies) (q: RondelQuery) : Async<RondelQueryResult> =
    failwith "Not implemented"
```

### Commit Message
```
Add rondel query types with stub implementation

- Add RondelQuery DU with GetNationPositions and GetRondelOverview cases
- Add query result types: NationPosition, NationPositionsResult, RondelOverviewResult
- Add RondelQueryDependencies record for query handler injection
- Add query function signature to .fsi and stub to .fs

Part of rondel CQRS query implementation (Phase 1).
```

---

## Phase 2: Tests & Implementation

### Step 2.1: Add Query Tests to `RondelTests.fs`

Add new test list after existing tests:

```fsharp
/// Helper to create query test context with shared state store
let private createRondelWithQuery () =
    let store = Collections.Generic.Dictionary<Id, RondelState>()

    let load (gameId: Id) : Async<RondelState option> =
        async {
            return
                match store.TryGetValue(gameId) with
                | true, state -> Some state
                | false, _ -> None
        }

    let save (state: RondelState) : Async<Result<unit, string>> =
        async {
            store.[state.GameId] <- state
            return Ok()
        }

    let publishedEvents = ResizeArray<RondelEvent>()
    let publish (event: RondelEvent) : Async<unit> = async { publishedEvents.Add event }

    let dispatchedCommands = ResizeArray<RondelOutboundCommand>()
    let dispatch (command: RondelOutboundCommand) : Async<Result<unit, string>> =
        async {
            dispatchedCommands.Add command
            return Ok()
        }

    let writeDeps: RondelDependencies =
        { Load = load
          Save = save
          Publish = publish
          Dispatch = dispatch }

    let queryDeps: RondelQueryDependencies =
        { Load = load }

    { Execute = fun cmd -> execute writeDeps cmd |> Async.RunSynchronously
      Handle = fun evt -> handle writeDeps evt |> Async.RunSynchronously },
    (fun q -> query queryDeps q |> Async.RunSynchronously),
    publishedEvents,
    dispatchedCommands

[<Tests>]
let queryTests =
    testList
        "Rondel.query"
        [
          testList
              "GetNationPositions"
              [
                testCase "returns None for unknown game"
                <| fun _ ->
                    let _, runQuery, _, _ = createRondelWithQuery ()
                    let gameId = Guid.NewGuid() |> Id

                    let result = runQuery (GetNationPositions { GameId = gameId })

                    match result with
                    | NationPositionsResult None -> ()
                    | _ -> failtest "Expected NationPositionsResult None"

                testCase "returns empty positions for initialized game with no moves"
                <| fun _ ->
                    let rondel, runQuery, _, _ = createRondelWithQuery ()
                    let gameId = Guid.NewGuid() |> Id
                    let nations = Set.ofList [ "France"; "Germany" ]

                    rondel.Execute <| SetToStartingPositions { GameId = gameId; Nations = nations }

                    let result = runQuery (GetNationPositions { GameId = gameId })

                    match result with
                    | NationPositionsResult (Some r) ->
                        Expect.equal r.GameId gameId "GameId should match"
                        Expect.equal r.Positions.Length 2 "Should have 2 nations"

                        let france = r.Positions |> List.find (fun p -> p.Nation = "France")
                        Expect.isNone france.CurrentSpace "France should have no current space"
                        Expect.isNone france.PendingSpace "France should have no pending space"

                        let germany = r.Positions |> List.find (fun p -> p.Nation = "Germany")
                        Expect.isNone germany.CurrentSpace "Germany should have no current space"
                        Expect.isNone germany.PendingSpace "Germany should have no pending space"
                    | _ -> failtest "Expected NationPositionsResult Some"

                testCase "returns current position after free move"
                <| fun _ ->
                    let rondel, runQuery, _, _ = createRondelWithQuery ()
                    let gameId = Guid.NewGuid() |> Id

                    rondel.Execute <| SetToStartingPositions { GameId = gameId; Nations = Set.ofList [ "France" ] }
                    rondel.Execute <| Move { GameId = gameId; Nation = "France"; Space = Space.Factory }

                    let result = runQuery (GetNationPositions { GameId = gameId })

                    match result with
                    | NationPositionsResult (Some r) ->
                        let france = r.Positions |> List.find (fun p -> p.Nation = "France")
                        Expect.equal france.CurrentSpace (Some Space.Factory) "France should be at Factory"
                        Expect.isNone france.PendingSpace "France should have no pending space"
                    | _ -> failtest "Expected NationPositionsResult Some"

                testCase "returns pending space for paid move awaiting payment"
                <| fun _ ->
                    let rondel, runQuery, _, _ = createRondelWithQuery ()
                    let gameId = Guid.NewGuid() |> Id

                    rondel.Execute <| SetToStartingPositions { GameId = gameId; Nations = Set.ofList [ "Austria" ] }
                    // First move to establish position
                    rondel.Execute <| Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor }
                    // Second move: 5 spaces (paid) - Investor to Factory
                    rondel.Execute <| Move { GameId = gameId; Nation = "Austria"; Space = Space.Factory }

                    let result = runQuery (GetNationPositions { GameId = gameId })

                    match result with
                    | NationPositionsResult (Some r) ->
                        let austria = r.Positions |> List.find (fun p -> p.Nation = "Austria")
                        Expect.equal austria.CurrentSpace (Some Space.Investor) "Austria should still be at Investor"
                        Expect.equal austria.PendingSpace (Some Space.Factory) "Austria should have pending move to Factory"
                    | _ -> failtest "Expected NationPositionsResult Some"
              ]

          testList
              "GetRondelOverview"
              [
                testCase "returns None for unknown game"
                <| fun _ ->
                    let _, runQuery, _, _ = createRondelWithQuery ()
                    let gameId = Guid.NewGuid() |> Id

                    let result = runQuery (GetRondelOverview { GameId = gameId })

                    match result with
                    | RondelOverviewResult None -> ()
                    | _ -> failtest "Expected RondelOverviewResult None"

                testCase "returns overview for initialized game"
                <| fun _ ->
                    let rondel, runQuery, _, _ = createRondelWithQuery ()
                    let gameId = Guid.NewGuid() |> Id
                    let nations = Set.ofList [ "France"; "Germany"; "Austria" ]

                    rondel.Execute <| SetToStartingPositions { GameId = gameId; Nations = nations }

                    let result = runQuery (GetRondelOverview { GameId = gameId })

                    match result with
                    | RondelOverviewResult (Some r) ->
                        Expect.equal r.GameId gameId "GameId should match"
                        Expect.equal r.IsInitialized true "Should be initialized"
                        Expect.equal (r.Nations |> List.sort) ([ "Austria"; "France"; "Germany" ]) "Nations should match"
                    | _ -> failtest "Expected RondelOverviewResult Some"
              ]
        ]
```

### Commit Message
```
Add rondel query handler tests

- Add GetNationPositions tests: unknown game, initialized game, after move, pending move
- Add GetRondelOverview tests: unknown game, initialized game
- Add createRondelWithQuery helper for query test context

Tests currently fail (stub throws). Implementation in next commit.

Part of rondel CQRS query implementation (Phase 2).
```

### Step 2.2: Implement Query Handler in `Rondel.fs`

Replace the stub with actual implementation:

```fsharp
// ──────────────────────────────────────────────────────────────────────────
// Query Handlers (Internal)
// ──────────────────────────────────────────────────────────────────────────

module internal QueryHandlers =

    let getNationPositions (state: RondelState option) (q: GetNationPositionsQuery) : NationPositionsResult option =
        state
        |> Option.map (fun s ->
            let positions =
                s.NationPositions
                |> Map.toList
                |> List.map (fun (nation, currentSpace) ->
                    let pendingSpace =
                        s.PendingMovements
                        |> Map.tryFind nation
                        |> Option.map (fun pm -> pm.TargetSpace)
                    { Nation = nation
                      CurrentSpace = currentSpace
                      PendingSpace = pendingSpace })
            { GameId = s.GameId
              Positions = positions })

    let getRondelOverview (state: RondelState option) (q: GetRondelOverviewQuery) : RondelOverviewResult option =
        state
        |> Option.map (fun s ->
            { GameId = s.GameId
              Nations = s.NationPositions |> Map.keys |> Seq.toList
              IsInitialized = not (Map.isEmpty s.NationPositions) })

// ──────────────────────────────────────────────────────────────────────────
// Query Router
// ──────────────────────────────────────────────────────────────────────────

let query (deps: RondelQueryDependencies) (q: RondelQuery) : Async<RondelQueryResult> =
    async {
        match q with
        | GetNationPositions q ->
            let! state = deps.Load q.GameId
            return NationPositionsResult (QueryHandlers.getNationPositions state q)
        | GetRondelOverview q ->
            let! state = deps.Load q.GameId
            return RondelOverviewResult (QueryHandlers.getRondelOverview state q)
    }
```

### Commit Message
```
Implement rondel query handler

- Add QueryHandlers module with getNationPositions and getRondelOverview
- Implement query router function
- All query tests now pass

Part of rondel CQRS query implementation (Phase 2).
```

### PR #2 Description
```markdown
## Summary
- Add query handler tests for GetNationPositions and GetRondelOverview
- Implement query handler with proper state projection

## Test plan
- [x] All existing tests pass
- [x] New query tests pass
- [x] Build succeeds
```

---

## Phase 3: Infrastructure Abstractions

### Step 3.1: Create `Imperium.Infrastructure` Project

**File: `src/Imperium.Infrastructure/Imperium.Infrastructure.fsproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="RondelStore.fs" />
    <Compile Include="InMemory/InMemoryRondelStore.fs" />
    <Compile Include="Marten/MartenRondelStore.fs" />
    <Compile Include="Akka/AkkaRondelStore.fs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Imperium\Imperium.fsproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Marten" Version="7.0.0" />
    <PackageReference Include="Akka.Persistence" Version="1.5.0" />
  </ItemGroup>
</Project>
```

**File: `src/Imperium.Infrastructure/RondelStore.fs`**

Shared abstractions:

```fsharp
namespace Imperium.Infrastructure

open Imperium.Rondel

/// Unified store interface providing both write and query dependencies.
/// Infrastructure implementations provide this interface.
type IRondelStore =
    /// Dependencies for command handlers (write side).
    abstract member WriteDependencies: RondelDependencies
    /// Dependencies for query handlers (read side).
    abstract member QueryDependencies: RondelQueryDependencies
```

### Commit Message
```
Create Imperium.Infrastructure project with store abstraction

- Add IRondelStore interface for unified write/query dependencies
- Set up project structure for InMemory, Marten, Akka implementations

Part of rondel CQRS query implementation (Phase 3).
```

### Step 3.2: Add InMemory Implementation

**File: `src/Imperium.Infrastructure/InMemory/InMemoryRondelStore.fs`**

```fsharp
namespace Imperium.Infrastructure.InMemory

open System.Collections.Concurrent
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Infrastructure

/// In-memory rondel store for standalone apps and testing.
/// Uses ConcurrentDictionary for thread-safe state storage.
type InMemoryRondelStore() =
    let states = ConcurrentDictionary<Id, RondelState>()
    let publishedEvents = ResizeArray<RondelEvent>()
    let dispatchedCommands = ResizeArray<RondelOutboundCommand>()

    let load (gameId: Id) : Async<RondelState option> =
        async {
            return
                match states.TryGetValue(gameId) with
                | true, state -> Some state
                | false, _ -> None
        }

    let save (state: RondelState) : Async<Result<unit, string>> =
        async {
            states.[state.GameId] <- state
            return Ok()
        }

    let publish (event: RondelEvent) : Async<unit> =
        async { publishedEvents.Add event }

    let dispatch (command: RondelOutboundCommand) : Async<Result<unit, string>> =
        async {
            dispatchedCommands.Add command
            return Ok()
        }

    interface IRondelStore with
        member _.WriteDependencies =
            { Load = load
              Save = save
              Publish = publish
              Dispatch = dispatch }

        member _.QueryDependencies =
            { Load = load }

    /// Access to published events for testing/debugging.
    member _.PublishedEvents = publishedEvents :> seq<RondelEvent>

    /// Access to dispatched commands for testing/debugging.
    member _.DispatchedCommands = dispatchedCommands :> seq<RondelOutboundCommand>

    /// Clear all state (useful for testing).
    member _.Clear() =
        states.Clear()
        publishedEvents.Clear()
        dispatchedCommands.Clear()

/// Factory module for creating in-memory stores.
module InMemoryRondelStore =
    /// Create a new in-memory rondel store.
    let create () = InMemoryRondelStore() :> IRondelStore
```

### Commit Message
```
Add InMemoryRondelStore implementation

- ConcurrentDictionary-based state storage
- Implements IRondelStore for unified write/query access
- Exposes events and commands for testing
- Thread-safe for concurrent access

Part of rondel CQRS query implementation (Phase 3).
```

### Step 3.3: Add Marten Implementation

**File: `src/Imperium.Infrastructure/Marten/MartenRondelStore.fs`**

```fsharp
namespace Imperium.Infrastructure.Marten

open System.Threading.Tasks
open Marten
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Infrastructure

/// Marten-based rondel store using PostgreSQL document storage.
type MartenRondelStore(session: IDocumentSession, publish: RondelEvent -> Async<unit>, dispatch: RondelOutboundCommand -> Async<Result<unit, string>>) =

    let load (gameId: Id) : Async<RondelState option> =
        async {
            let! ct = Async.CancellationToken
            let! doc = session.LoadAsync<Contract.Rondel.RondelState>(Id.value gameId, ct) |> Async.AwaitTask
            return
                doc
                |> Option.ofObj
                |> Option.bind (fun contract ->
                    match RondelState.fromContract contract with
                    | Ok state -> Some state
                    | Error _ -> None)
        }

    let save (state: RondelState) : Async<Result<unit, string>> =
        async {
            try
                let contract = RondelState.toContract state
                session.Store(contract)
                let! ct = Async.CancellationToken
                do! session.SaveChangesAsync(ct) |> Async.AwaitTask
                return Ok()
            with ex ->
                return Error ex.Message
        }

    interface IRondelStore with
        member _.WriteDependencies =
            { Load = load
              Save = save
              Publish = publish
              Dispatch = dispatch }

        member _.QueryDependencies =
            { Load = load }

/// Factory module for creating Marten stores.
module MartenRondelStore =
    /// Create a Marten-based rondel store.
    /// Publish and dispatch functions must be provided for integration with message bus.
    let create
        (session: IDocumentSession)
        (publish: RondelEvent -> Async<unit>)
        (dispatch: RondelOutboundCommand -> Async<Result<unit, string>>)
        : IRondelStore =
        MartenRondelStore(session, publish, dispatch) :> IRondelStore

/// Marten configuration extensions.
module MartenConfiguration =
    open Marten.Schema

    /// Configure Marten schema for rondel state.
    let configureRondelSchema (options: StoreOptions) =
        options.Schema.For<Contract.Rondel.RondelState>()
            .Identity(fun x -> x.GameId)
            .Index(fun x -> x.GameId :> obj)
        |> ignore
```

### Commit Message
```
Add MartenRondelStore implementation

- PostgreSQL document storage via Marten
- Async state load/save with CancellationToken support
- Contract transformation for persistence
- Schema configuration helper

Part of rondel CQRS query implementation (Phase 3).
```

### Step 3.4: Add Akka.NET Implementation

**File: `src/Imperium.Infrastructure/Akka/AkkaRondelStore.fs`**

```fsharp
namespace Imperium.Infrastructure.Akka

open System
open Akka.Actor
open Akka.Persistence
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Infrastructure

/// Messages for Rondel persistent actor.
type RondelActorMessage =
    | ExecuteCommand of RondelCommand
    | HandleEvent of RondelInboundEvent
    | GetState
    | StateResponse of RondelState option

/// Persistent actor maintaining rondel state for a single game.
type RondelPersistentActor(gameId: Id, publish: RondelEvent -> Async<unit>, dispatch: RondelOutboundCommand -> Async<Result<unit, string>>) as this =
    inherit ReceivePersistentActor()

    let mutable state: RondelState option = None

    let deps: RondelDependencies =
        { Load = fun _ -> async { return state }
          Save = fun s -> async { state <- Some s; return Ok() }
          Publish = publish
          Dispatch = dispatch }

    do
        this.Command<RondelActorMessage>(fun msg ->
            match msg with
            | ExecuteCommand cmd ->
                execute deps cmd |> Async.RunSynchronously
            | HandleEvent evt ->
                handle deps evt |> Async.RunSynchronously
            | GetState ->
                this.Sender.Tell(StateResponse state)
            | StateResponse _ -> ())

        this.Recover<RondelState>(fun s ->
            state <- Some s)

    override _.PersistenceId = $"rondel-{Id.value gameId}"

/// Akka-based rondel store using actor model.
type AkkaRondelStore(system: ActorSystem, gameId: Id, publish: RondelEvent -> Async<unit>, dispatch: RondelOutboundCommand -> Async<Result<unit, string>>) =

    let actorRef =
        let props = Props.Create(fun () -> RondelPersistentActor(gameId, publish, dispatch))
        system.ActorOf(props, $"rondel-{Id.value gameId}")

    let load (gameId: Id) : Async<RondelState option> =
        async {
            let! response = actorRef.Ask<RondelActorMessage>(GetState, TimeSpan.FromSeconds(5.0)) |> Async.AwaitTask
            return
                match response with
                | StateResponse s -> s
                | _ -> None
        }

    let save (state: RondelState) : Async<Result<unit, string>> =
        async {
            // State is managed by actor internally
            return Ok()
        }

    let executeCommand (cmd: RondelCommand) : Async<unit> =
        async {
            actorRef.Tell(ExecuteCommand cmd)
        }

    let handleEvent (evt: RondelInboundEvent) : Async<unit> =
        async {
            actorRef.Tell(HandleEvent evt)
        }

    interface IRondelStore with
        member _.WriteDependencies =
            { Load = load
              Save = save
              Publish = publish
              Dispatch = dispatch }

        member _.QueryDependencies =
            { Load = load }

    /// Execute a command via the actor.
    member _.ExecuteCommand(cmd: RondelCommand) = executeCommand cmd

    /// Handle an inbound event via the actor.
    member _.HandleEvent(evt: RondelInboundEvent) = handleEvent evt

/// Factory module for creating Akka stores.
module AkkaRondelStore =
    /// Create an Akka-based rondel store for a specific game.
    let create
        (system: ActorSystem)
        (gameId: Id)
        (publish: RondelEvent -> Async<unit>)
        (dispatch: RondelOutboundCommand -> Async<Result<unit, string>>)
        : AkkaRondelStore =
        AkkaRondelStore(system, gameId, publish, dispatch)
```

### Commit Message
```
Add AkkaRondelStore implementation

- Persistent actor for rondel state per game
- Actor-based command execution and event handling
- Ask pattern for state queries
- Factory for creating game-specific stores

Part of rondel CQRS query implementation (Phase 3).
```

### PR #3 Description
```markdown
## Summary
- Create Imperium.Infrastructure project
- Add IRondelStore abstraction for unified write/query access
- Implement InMemoryRondelStore for standalone apps
- Implement MartenRondelStore for PostgreSQL persistence
- Implement AkkaRondelStore for actor-based distributed state

## Test plan
- [x] All existing tests pass
- [x] Infrastructure project builds
- [x] InMemory store works in test context
```

---

## Phase 4: Web API & Contracts

### Step 4.1: Add Query Contracts to `Contract.Rondel.fs`

Add after existing contract types:

```fsharp
// ──────────────────────────────────────────────────────────────────────────
// Query Contracts
// ──────────────────────────────────────────────────────────────────────────

/// Request for nation positions query.
type GetNationPositionsQuery = { GameId: Guid }

/// Request for rondel overview query.
type GetRondelOverviewQuery = { GameId: Guid }

// ──────────────────────────────────────────────────────────────────────────
// Query Response Contracts
// ──────────────────────────────────────────────────────────────────────────

/// A nation's position in query response.
type NationPositionDto = {
    Nation: string
    CurrentSpace: string option
    PendingSpace: string option
}

/// Response for GetNationPositions query.
type NationPositionsResponse = {
    GameId: Guid
    Positions: NationPositionDto list
}

/// Response for GetRondelOverview query.
type RondelOverviewResponse = {
    GameId: Guid
    Nations: string list
    IsInitialized: bool
}
```

### Commit Message
```
Add query contracts to Contract.Rondel

- Add GetNationPositionsQuery and GetRondelOverviewQuery request types
- Add NationPositionDto, NationPositionsResponse, RondelOverviewResponse types
- Contracts use primitive types (Guid, string) for serialization

Part of rondel CQRS query implementation (Phase 4).
```

### Step 4.2: Add Contract Transformations to `Rondel.fsi` / `Rondel.fs`

**Add to `Rondel.fsi`** (after existing transformations):

```fsharp
/// Transforms Contract GetNationPositionsQuery to Domain type.
module GetNationPositionsQuery =
    val fromContract: Contract.Rondel.GetNationPositionsQuery -> Result<GetNationPositionsQuery, string>

/// Transforms Contract GetRondelOverviewQuery to Domain type.
module GetRondelOverviewQuery =
    val fromContract: Contract.Rondel.GetRondelOverviewQuery -> Result<GetRondelOverviewQuery, string>

/// Transforms Domain NationPositionsResult to Contract type.
module NationPositionsResult =
    val toContract: NationPositionsResult -> Contract.Rondel.NationPositionsResponse

/// Transforms Domain RondelOverviewResult to Contract type.
module RondelOverviewResult =
    val toContract: RondelOverviewResult -> Contract.Rondel.RondelOverviewResponse
```

**Add to `Rondel.fs`** (in Transformations section):

```fsharp
module GetNationPositionsQuery =
    let fromContract (contract: Contract.Rondel.GetNationPositionsQuery) : Result<GetNationPositionsQuery, string> =
        result {
            let! gameId = Id.create contract.GameId
            return { GameId = gameId }
        }

module GetRondelOverviewQuery =
    let fromContract (contract: Contract.Rondel.GetRondelOverviewQuery) : Result<GetRondelOverviewQuery, string> =
        result {
            let! gameId = Id.create contract.GameId
            return { GameId = gameId }
        }

module NationPositionsResult =
    let toContract (result: NationPositionsResult) : Contract.Rondel.NationPositionsResponse =
        { GameId = Id.value result.GameId
          Positions =
              result.Positions
              |> List.map (fun p ->
                  { Contract.Rondel.NationPositionDto.Nation = p.Nation
                    CurrentSpace = p.CurrentSpace |> Option.map Space.toString
                    PendingSpace = p.PendingSpace |> Option.map Space.toString }) }

module RondelOverviewResult =
    let toContract (result: RondelOverviewResult) : Contract.Rondel.RondelOverviewResponse =
        { GameId = Id.value result.GameId
          Nations = result.Nations
          IsInitialized = result.IsInitialized }
```

### Commit Message
```
Add query contract transformations

- Add fromContract for query request validation
- Add toContract for query response serialization
- Follow existing transformation patterns

Part of rondel CQRS query implementation (Phase 4).
```

### Step 4.3: Add Minimal API Endpoints to `Imperium.Web`

**Update `Imperium.Web.fsproj`**:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Endpoints/RondelEndpoints.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Imperium\Imperium.fsproj" />
    <ProjectReference Include="..\Imperium.Infrastructure\Imperium.Infrastructure.fsproj" />
  </ItemGroup>
</Project>
```

**File: `src/Imperium.Web/Endpoints/RondelEndpoints.fs`**

```fsharp
module Imperium.Web.Endpoints.Rondel

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Imperium.Rondel

/// Map rondel query endpoints to the web application.
let mapRondelEndpoints (app: WebApplication) (queryDeps: RondelQueryDependencies) =

    // GET /api/rondel/{gameId}/positions
    app.MapGet("/api/rondel/{gameId:guid}/positions", Func<Guid, _>(fun gameId ->
        async {
            let contractQuery: Contract.Rondel.GetNationPositionsQuery = { GameId = gameId }

            match GetNationPositionsQuery.fromContract contractQuery with
            | Error msg ->
                return Results.BadRequest({| error = msg |})
            | Ok domainQuery ->
                let! result = query queryDeps (GetNationPositions domainQuery)

                return
                    match result with
                    | NationPositionsResult (Some r) ->
                        Results.Ok(NationPositionsResult.toContract r)
                    | NationPositionsResult None ->
                        Results.NotFound({| error = "Game not found" |})
                    | _ ->
                        Results.BadRequest({| error = "Unexpected result type" |})
        }
        |> Async.StartAsTask))
        .WithName("GetNationPositions")
        .WithOpenApi()
    |> ignore

    // GET /api/rondel/{gameId}/overview
    app.MapGet("/api/rondel/{gameId:guid}/overview", Func<Guid, _>(fun gameId ->
        async {
            let contractQuery: Contract.Rondel.GetRondelOverviewQuery = { GameId = gameId }

            match GetRondelOverviewQuery.fromContract contractQuery with
            | Error msg ->
                return Results.BadRequest({| error = msg |})
            | Ok domainQuery ->
                let! result = query queryDeps (GetRondelOverview domainQuery)

                return
                    match result with
                    | RondelOverviewResult (Some r) ->
                        Results.Ok(RondelOverviewResult.toContract r)
                    | RondelOverviewResult None ->
                        Results.NotFound({| error = "Game not found" |})
                    | _ ->
                        Results.BadRequest({| error = "Unexpected result type" |})
        }
        |> Async.StartAsTask))
        .WithName("GetRondelOverview")
        .WithOpenApi()
    |> ignore

    app
```

### Commit Message
```
Add rondel query API endpoints

- GET /api/rondel/{gameId}/positions - returns nation positions
- GET /api/rondel/{gameId}/overview - returns rondel overview
- Contract transformation at API boundary
- OpenAPI metadata for documentation

Part of rondel CQRS query implementation (Phase 4).
```

### Step 4.4: Add DI Configuration

**File: `src/Imperium.Web/Program.fs`**

```fsharp
open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Imperium.Rondel
open Imperium.Infrastructure
open Imperium.Infrastructure.InMemory
open Imperium.Web.Endpoints.Rondel

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    // Configure infrastructure based on environment
    let store =
        // For now, use in-memory store
        // TODO: Add configuration for Marten/Akka based on settings
        InMemoryRondelStore() :> IRondelStore

    // Register dependencies
    builder.Services.AddSingleton<IRondelStore>(store) |> ignore
    builder.Services.AddSingleton<RondelDependencies>(store.WriteDependencies) |> ignore
    builder.Services.AddSingleton<RondelQueryDependencies>(store.QueryDependencies) |> ignore

    // Add OpenAPI
    builder.Services.AddEndpointsApiExplorer() |> ignore
    builder.Services.AddSwaggerGen() |> ignore

    let app = builder.Build()

    // Configure middleware
    if app.Environment.IsDevelopment() then
        app.UseSwagger() |> ignore
        app.UseSwaggerUI() |> ignore

    // Map endpoints
    let queryDeps = app.Services.GetRequiredService<RondelQueryDependencies>()
    mapRondelEndpoints app queryDeps |> ignore

    app.Run()
    0
```

### Commit Message
```
Add DI configuration for switchable infrastructure

- Register IRondelStore and dependencies via DI
- Default to InMemoryRondelStore
- Add Swagger/OpenAPI support
- Wire up rondel query endpoints

Part of rondel CQRS query implementation (Phase 4).
```

### PR #4 Description
```markdown
## Summary
- Add query contracts to Contract.Rondel.fs
- Add contract transformations for queries
- Add minimal API endpoints for rondel queries
- Add DI configuration with in-memory default

## Endpoints
- `GET /api/rondel/{gameId}/positions` - Get nation positions
- `GET /api/rondel/{gameId}/overview` - Get rondel overview

## Test plan
- [x] All existing tests pass
- [x] Web project builds
- [x] API endpoints return expected responses
- [x] Swagger UI shows endpoints
```

---

## Summary

| Phase | Steps | PR |
|-------|-------|-----|
| 1. Domain Types | Add query types + stub | PR #1 |
| 2. Tests & Implementation | Add tests, implement handler | PR #2 |
| 3. Infrastructure | InMemory, Marten, Akka stores | PR #3 |
| 4. Web API | Contracts, endpoints, DI | PR #4 |

Each step is independently buildable and testable.
