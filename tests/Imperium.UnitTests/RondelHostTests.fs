module Imperium.UnitTests.RondelHostTests

open Expecto
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Terminal
open Imperium.Terminal.Rondel
open Imperium.Terminal.Rondel.Store

module Accounting = Imperium.Accounting

// ──────────────────────────────────────────────────────────────────────────
// Test Helpers
// ──────────────────────────────────────────────────────────────────────────

let private createRondelHost () =
    let publishedEvents = ResizeArray<obj>()
    let dispatchedCommands = ResizeArray<Accounting.AccountingCommand>()
    let innerBus = Bus.create ()

    let bus =
        { new IBus with
            member _.Publish<'T>(event: 'T) =
                publishedEvents.Add(box event)
                innerBus.Publish event

            member _.Subscribe<'T>(handler: 'T -> Async<unit>) = innerBus.Subscribe<'T> handler }

    let store = InMemoryRondelStore.create ()

    let stubDispatch: DispatchToAccounting =
        fun () ->
            fun cmd ->
                async {
                    dispatchedCommands.Add cmd
                    return Ok()
                }

    let host = RondelHost.create store bus stubDispatch

    {| Execute = fun cmd -> host.Execute cmd |> Async.RunSynchronously
       QueryPositions = fun q -> host.QueryPositions q |> Async.RunSynchronously
       QueryOverview = fun q -> host.QueryOverview q |> Async.RunSynchronously |},
    publishedEvents,
    dispatchedCommands,
    {| Load = fun id -> store.Load id |> Async.RunSynchronously |},
    {| Publish = fun event -> bus.Publish event |> Async.RunSynchronously |}

// ──────────────────────────────────────────────────────────────────────────
// Tests - Plumbing verification only
// ──────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList
        "Terminal.RondelHost"
        [ ptestCase "wires command execution to domain"
          <| fun _ ->
              let host, _, _, store, _ = createRondelHost ()
              let gameId = Id.newId ()

              SetToStartingPositions { GameId = gameId; Nations = set [ "A" ] }
              |> host.Execute

              Expect.isSome (store.Load gameId) "command should reach domain and persist"

          ptestCase "wires domain events to bus"
          <| fun _ ->
              let host, publishedEvents, _, _, _ = createRondelHost ()
              let gameId = Id.newId ()

              SetToStartingPositions { GameId = gameId; Nations = set [ "A" ] }
              |> host.Execute

              Move { GameId = gameId; Nation = "A"; Space = Space.ManeuverOne }
              |> host.Execute

              Expect.isNonEmpty publishedEvents "domain events should flow to bus"

          ptestCase "wires outbound commands to dispatch thunk"
          <| fun _ ->
              let host, _, dispatchedCommands, _, _ = createRondelHost ()
              let gameId = Id.newId ()

              SetToStartingPositions { GameId = gameId; Nations = set [ "A" ] }
              |> host.Execute

              Move { GameId = gameId; Nation = "A"; Space = Space.Taxation } |> host.Execute

              Move { GameId = gameId; Nation = "A"; Space = Space.Import } |> host.Execute

              Expect.isNonEmpty dispatchedCommands "outbound commands should flow to thunk"

          ptestCase "wires bus events to domain handler"
          <| fun _ ->
              let host, publishedEvents, dispatchedCommands, _, bus = createRondelHost ()
              let gameId = Id.newId ()

              SetToStartingPositions { GameId = gameId; Nations = set [ "A" ] }
              |> host.Execute

              Move { GameId = gameId; Nation = "A"; Space = Space.Taxation } |> host.Execute

              Move { GameId = gameId; Nation = "A"; Space = Space.Import } |> host.Execute

              let billingId =
                  dispatchedCommands
                  |> Seq.choose (function
                      | Accounting.ChargeNationForRondelMovement chargeCmd -> Some chargeCmd.BillingId
                      | _ -> None)
                  |> Seq.tryHead
                  |> Option.defaultWith (fun () -> failwith "charge command not dispatched")

              bus.Publish(Accounting.RondelInvoicePaid { GameId = gameId; BillingId = billingId })

              let move =
                  publishedEvents
                  |> Seq.tryFind (fun e ->
                      match e with
                      | :? RondelEvent as rc ->
                          match rc with
                          | ActionDetermined _ -> true
                          | _ -> false
                      | _ -> false)

              Expect.isSome move "domain should receive bus event"

          ptestCase "wires queries to store"
          <| fun _ ->
              let host, _, _, _, _ = createRondelHost ()

              let gameId = Id.newId ()

              SetToStartingPositions { GameId = gameId; Nations = set [ "A" ] }
              |> host.Execute

              let result = host.QueryPositions { GameId = Id.newId () }

              Expect.isSome result "query should read from store (not empty = Some)" ]
