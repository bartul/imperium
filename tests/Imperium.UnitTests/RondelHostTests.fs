module Imperium.UnitTests.RondelHostTests

open Expecto
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Terminal
open Imperium.Terminal.Rondel

module Accounting = Imperium.Accounting

// ──────────────────────────────────────────────────────────────────────────
// Test Helpers
// ──────────────────────────────────────────────────────────────────────────

let private waitFor (check: unit -> bool) =
    let rec loop delay attempts =
        if check () then
            ()
        elif attempts <= 0 then
            failwith "waitFor timed out"
        else
            System.Threading.Thread.Sleep(delay: int)
            loop (delay * 2) (attempts - 1)

    loop 5 12

let private createRondelHost (dispatchToAccounting: DispatchToAccounting) =
    let publishedEvents = ResizeArray<obj>()
    let innerBus = Bus.create ()

    let bus =
        { new IBus with
            member _.Publish<'T>(event: 'T) =
                publishedEvents.Add(box event)
                innerBus.Publish event

            member _.Subscribe<'T>(handler: 'T -> Async<unit>) = innerBus.Subscribe<'T> handler }

    let store = InMemoryRondelStore.create ()
    let host = RondelHost.create store bus dispatchToAccounting

    {| Execute = fun cmd -> host.Execute cmd |> Async.RunSynchronously
       QueryPositions = fun q -> host.QueryPositions q |> Async.RunSynchronously
       QueryOverview = fun q -> host.QueryOverview q |> Async.RunSynchronously |},
    publishedEvents,
    {| Load = fun id -> store.Load id |> Async.RunSynchronously |},
    {| Publish = fun event -> bus.Publish event |> Async.RunSynchronously |}

let private createRondelHostWithDefaults () =
    let dispatchedCommands = ResizeArray<Accounting.AccountingCommand>()

    let stubDispatch: DispatchToAccounting =
        fun () ->
            fun cmd ->
                async {
                    dispatchedCommands.Add cmd
                    return Ok()
                }

    let host, publishedEvents, store, bus = createRondelHost stubDispatch
    host, publishedEvents, dispatchedCommands, store, bus

// ──────────────────────────────────────────────────────────────────────────
// Tests - Plumbing verification only
// ──────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList
        "Terminal.RondelHost"
        [ testCase "wires command execution to domain"
          <| fun _ ->
              let host, _, _, store, _ = createRondelHostWithDefaults ()
              let gameId = Id.newId ()

              SetToStartingPositions { GameId = gameId; Nations = set [ "A" ] }
              |> host.Execute

              waitFor (fun () -> (store.Load gameId).IsSome)
              Expect.isSome (store.Load gameId) "command should reach domain and persist"

          testCase "wires domain events to bus"
          <| fun _ ->
              let host, publishedEvents, _, _, _ = createRondelHostWithDefaults ()
              let gameId = Id.newId ()

              SetToStartingPositions { GameId = gameId; Nations = set [ "A" ] }
              |> host.Execute

              Move { GameId = gameId; Nation = "A"; Space = Space.ManeuverOne }
              |> host.Execute

              waitFor (fun () -> publishedEvents.Count > 0)
              Expect.isNonEmpty publishedEvents "domain events should flow to bus"

          testCase "wires outbound commands to dispatch thunk"
          <| fun _ ->
              let host, _, dispatchedCommands, _, _ = createRondelHostWithDefaults ()
              let gameId = Id.newId ()

              SetToStartingPositions { GameId = gameId; Nations = set [ "A" ] }
              |> host.Execute

              Move { GameId = gameId; Nation = "A"; Space = Space.Taxation } |> host.Execute

              Move { GameId = gameId; Nation = "A"; Space = Space.Import } |> host.Execute

              waitFor (fun () -> dispatchedCommands.Count > 0)
              Expect.isNonEmpty dispatchedCommands "outbound commands should flow to thunk"

          testCase "wires bus events to domain handler"
          <| fun _ ->
              let host, publishedEvents, dispatchedCommands, _, bus =
                  createRondelHostWithDefaults ()

              let gameId = Id.newId ()

              SetToStartingPositions { GameId = gameId; Nations = set [ "A" ] }
              |> host.Execute

              Move { GameId = gameId; Nation = "A"; Space = Space.Taxation } |> host.Execute

              Move { GameId = gameId; Nation = "A"; Space = Space.Import } |> host.Execute

              waitFor (fun () -> dispatchedCommands.Count > 0)

              let billingId =
                  dispatchedCommands
                  |> Seq.choose (function
                      | Accounting.ChargeNationForRondelMovement chargeCmd -> Some chargeCmd.BillingId
                      | _ -> None)
                  |> Seq.tryHead
                  |> Option.defaultWith (fun () -> failwith "charge command not dispatched")

              Accounting.RondelInvoicePaid { GameId = gameId; BillingId = billingId }
              |> bus.Publish

              let hasActionDetermined () =
                  publishedEvents
                  |> Seq.exists (fun e ->
                      match e with
                      | :? RondelEvent as rc ->
                          match rc with
                          | ActionDetermined _ -> true
                          | _ -> false
                      | _ -> false)

              waitFor hasActionDetermined
              Expect.isTrue (hasActionDetermined ()) "domain should receive bus event"

          testCase "wires queries to store"
          <| fun _ ->
              let host, _, _, store, _ = createRondelHostWithDefaults ()

              let gameId = Id.newId ()

              SetToStartingPositions { GameId = gameId; Nations = set [ "A" ] }
              |> host.Execute

              waitFor (fun () -> (store.Load gameId).IsSome)

              let result = host.QueryPositions { GameId = gameId }

              Expect.isSome result "query should read from store"

          testCase "keeps processing commands after a handler failure"
          <| fun _ ->
              let mutable shouldFail = true

              let failingDispatch: DispatchToAccounting =
                  fun () ->
                      fun _ ->
                          async {
                              if shouldFail then
                                  shouldFail <- false
                                  return Error "dispatch failed"
                              else
                                  return Ok()
                          }

              let host, _, store, _ = createRondelHost failingDispatch
              let firstGameId = Id.newId ()
              let secondGameId = Id.newId ()

              SetToStartingPositions { GameId = firstGameId; Nations = set [ "A" ] }
              |> host.Execute

              Move { GameId = firstGameId; Nation = "A"; Space = Space.Taxation }
              |> host.Execute

              Move { GameId = firstGameId; Nation = "A"; Space = Space.Import }
              |> host.Execute

              SetToStartingPositions { GameId = secondGameId; Nations = set [ "B" ] }
              |> host.Execute

              waitFor (fun () -> (store.Load secondGameId).IsSome)
              Expect.isSome (store.Load secondGameId) "mailbox should continue processing later commands" ]
