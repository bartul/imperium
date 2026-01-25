module Imperium.UnitTests.RondelHostTests

open Expecto
open Imperium.Primitives
open Imperium.Rondel
module Bus = Imperium.Terminal.Bus
module RondelStore = Imperium.Terminal.Rondel.Store
module RondelHost = Imperium.Terminal.Rondel.Host

// ──────────────────────────────────────────────────────────────────────────
// Test Helpers
// ──────────────────────────────────────────────────────────────────────────

let private createRondelHost () =
    let publishedEvents = ResizeArray<obj>()
    let bus = Bus.create ()
    let store = RondelStore.InMemoryRondelStore.create ()
    let host = RondelHost.create store bus

    {| ExecuteCommand = fun cmd -> host.ExecuteCommand cmd |> Async.RunSynchronously
       QueryPositions = fun q -> host.QueryPositions q |> Async.RunSynchronously
       QueryOverview = fun q -> host.QueryOverview q |> Async.RunSynchronously |},
    publishedEvents,
    {| Load = fun id -> store.Load id |> Async.RunSynchronously
       Save = fun state -> store.Save state |> Async.RunSynchronously |}

// ──────────────────────────────────────────────────────────────────────────
// Tests
// ──────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList
        "Terminal.RondelHost"
        [ testList
              "commandExecution"
              [ testCase "executes SetToStartingPositions and persists state"
                <| fun _ ->
                    let host, _, store = createRondelHost ()
                    let gameId = Id.newId ()

                    let cmd: SetToStartingPositionsCommand =
                        { GameId = gameId
                          Nations = set [ "Austria"; "Italy" ] }

                    let result = host.ExecuteCommand <| SetToStartingPositions cmd

                    Expect.isOk result "command should succeed"

                    let state = store.Load gameId
                    Expect.isSome state "state should be persisted" ] ]
