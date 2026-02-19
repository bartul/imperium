module Imperium.UnitTests.Rondel

open System
open System.Collections.Generic
open System.Security.AccessControl
open Expecto
open Spec
open Imperium.Rondel
open Imperium.Primitives

// ────────────────────────────────────────────────────────────────────────────────
// Context
// ────────────────────────────────────────────────────────────────────────────────

type RondelContext =
    { Deps: RondelDependencies
      Events: ResizeArray<RondelEvent>
      Commands: ResizeArray<RondelOutboundCommand>
      Store: Dictionary<Id, RondelState>
      GameId: Id }

let private createContext gameId =
    let store = Dictionary<Id, RondelState>()
    let events = ResizeArray<RondelEvent>()
    let commands = ResizeArray<RondelOutboundCommand>()

    let load id =
        async {
            return
                match store.TryGetValue(id) with
                | true, state -> Some state
                | false, _ -> None
        }

    let save (state: RondelState) =
        async {
            store[state.GameId] <- state
            return Ok()
        }

    let publish event = async { events.Add event }

    let dispatch command =
        async {
            commands.Add command
            return Ok()
        }

    { Deps = { Load = load; Save = save; Publish = publish; Dispatch = dispatch }
      Events = events
      Commands = commands
      Store = store
      GameId = gameId }

// ────────────────────────────────────────────────────────────────────────────────
// Runner
// ────────────────────────────────────────────────────────────────────────────────

let private runner =
    { new ISpecRunner<RondelContext, RondelState, RondelState option, RondelCommand, RondelInboundEvent> with
        member _.Execute ctx cmd =
            execute ctx.Deps cmd |> Async.RunSynchronously

        member _.Handle ctx evt =
            handle ctx.Deps evt |> Async.RunSynchronously

        member _.ClearEvents ctx = ctx.Events.Clear()
        member _.ClearCommands ctx = ctx.Commands.Clear()
        member _.SeedState ctx state = ctx.Store[ctx.GameId] <- state
        member _.SeedFor _ = None

        member _.CaptureState ctx =
            match ctx.Store.TryGetValue(ctx.GameId) with
            | true, state -> Some state
            | false, _ -> None }

// ────────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────────

let hasExactEvent event_ ctx =
    ctx.Events |> Seq.exists (fun item -> item = event_)

let private hasStartingPositionsSet gameId =
    hasExactEvent (PositionedAtStart { GameId = gameId })

let private hasEvent predicate ctx = ctx.Events |> Seq.exists predicate

let private hasActionDetermined ctx =
    hasEvent
        (function
        | ActionDetermined _ -> true
        | _ -> false)
        ctx

let private hasRejection ctx =
    hasEvent
        (function
        | MoveToActionSpaceRejected _ -> true
        | _ -> false)
        ctx

let private hasChargeCommand ctx =
    ctx.Commands
    |> Seq.exists (function
        | ChargeMovement _ -> true
        | _ -> false)

let private hasChargeCommandOf (amount: Amount) ctx =
    ctx.Commands
    |> Seq.exists (function
        | ChargeMovement cmd when cmd.Amount = amount -> true
        | _ -> false)

let private hasChargeCommandOfM millions =
    hasChargeCommandOf (Amount.unsafe millions)

let private hasExactCommand command ctx =
    ctx.Commands |> Seq.exists (fun item -> item = command)

let private countExactEvent event_ ctx =
    ctx.Events |> Seq.filter (fun item -> item = event_) |> Seq.length

let private hasVoidCommand ctx =
    ctx.Commands
    |> Seq.exists (function
        | VoidCharge _ -> true
        | _ -> false)

let private chargeCount ctx =
    ctx.Commands
    |> Seq.filter (function
        | ChargeMovement _ -> true
        | _ -> false)
    |> Seq.length

// ────────────────────────────────────────────────────────────────────────────────
// Specs: move
// ────────────────────────────────────────────────────────────────────────────────

let private rondelSpecs =
    let gameId = Guid.NewGuid() |> Id
    let nations = Set.ofList [ "France"; "Austria" ]

    [ spec "starting setup places nations at their opening positions" {
          on (fun () -> createContext gameId)

          when_ [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute ]

          expect "opening positions are set" (hasStartingPositionsSet gameId)
      }

      spec "starting setup can be applied only once per game" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("France", None); ("Austria", None) ]
                PendingMovements = Map.empty }

          when_ [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute ]

          expect "second setup attempt is ignored" (hasStartingPositionsSet gameId >> not)
      }

      spec "any attempted move is rejected until nations are set to starting positions" {
          on (fun () -> createContext gameId)

          when_
              [ Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute
                Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor } |> Execute ]

          expect
              "reject the move"
              (hasExactEvent (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Factory }))

          expect "no action determined" (hasActionDetermined >> not)
          expect "no payment required" (hasChargeCommand >> not)
      }

      spec "moving a nation for the first time does not require payment regardless of destination" {
          on (fun () -> createContext gameId)

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute
                Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute
                Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor } |> Execute ]

          expect
              "action is determined"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory }))

          expect "no payment required" (hasChargeCommand >> not)
      }

      spec "moving a nation to its current position is rejected (stay put)" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("France", Some Space.Factory); ("Austria", Some Space.Investor) ]
                PendingMovements = Map.empty }

          when_
              [ Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute
                Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor } |> Execute ]

          expect
              "rejects the move"
              (hasExactEvent (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Factory }))

          expect "no action determined" (hasActionDetermined >> not)
          expect "no payment required" (hasChargeCommand >> not)
      }

      spec "moving a nation 1-3 spaces is free" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("France", Some Space.Investor); ("Austria", None) ]
                PendingMovements = Map.empty }

          when_
              [ Move { GameId = gameId; Nation = "France"; Space = Space.ProductionOne }
                |> Execute ]

          expect "action is determined" hasActionDetermined
          expect "no payment required" (hasChargeCommand >> not)
      }

      spec "moving a nation 4 spaces requires payment of 2M" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("France", Some Space.ProductionOne); ("Austria", None) ]
                PendingMovements = Map.empty }

          when_
              [ Move { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo }
                |> Execute ]

          expect "no action determined" (hasActionDetermined >> not)
          expect "payment is required" hasChargeCommand
          expect "payment amount is 2M" (hasChargeCommandOfM 2)
      }

      spec "moving a nation 5 spaces requires payment of 4M" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("France", Some Space.ManeuverOne); ("Austria", None) ]
                PendingMovements = Map.empty }

          when_ [ Move { GameId = gameId; Nation = "France"; Space = Space.Investor } |> Execute ]

          expect "no action determined" (hasActionDetermined >> not)
          expect "payment is required" hasChargeCommand
          expect "payment amount is 4M" (hasChargeCommandOfM 4)
      }

      spec "moving a nation 6 spaces requires payment of 6M" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("France", Some Space.Investor); ("Austria", None) ]
                PendingMovements = Map.empty }

          when_
              [ Move { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo }
                |> Execute ]

          expect "no action determined" (hasActionDetermined >> not)
          expect "payment is required" hasChargeCommand
          expect "payment amount is 6M" (hasChargeCommandOfM 6)
      }

      spec "moving a nation 7 spaces is rejected as exceeding maximum distance" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("France", Some Space.ProductionOne); ("Austria", None) ]
                PendingMovements = Map.empty }

          when_ [ Move { GameId = gameId; Nation = "France"; Space = Space.Import } |> Execute ]

          expect
              "rejects the move"
              (hasExactEvent (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Import }))

          expect "no action determined" (hasActionDetermined >> not)
          expect "no payment required" (hasChargeCommand >> not)
      }

      let previousBillingId = Guid.NewGuid() |> Id |> RondelBillingId.ofId

      spec "moving a nation with a pending paid move to another paid move voids the old charge" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("France", Some Space.ProductionOne); ("Austria", None) ]
                PendingMovements =
                  Map
                      [ ("France",
                         { Nation = "France"; TargetSpace = Space.ProductionTwo; BillingId = previousBillingId }) ] }

          when_
              [ Move { GameId = gameId; Nation = "France"; Space = Space.ManeuverTwo }
                |> Execute ]

          expect
              "pending move is rejected"
              (hasExactEvent (
                  MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo }
              ))

          expect
              "previous charge is voided"
              (hasExactCommand (VoidCharge { GameId = gameId; BillingId = previousBillingId }))

          expect "new payment is required" hasChargeCommand
          expect "payment amount is 4M" (hasChargeCommandOfM 4)
          expect "no action determined" (hasActionDetermined >> not)
      }

      let previousBillingIdForFreeMove = Guid.NewGuid() |> Id |> RondelBillingId.ofId

      spec "moving a nation with a pending paid move to a free move voids the old charge and completes immediately" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("France", Some Space.ManeuverOne); ("Austria", None) ]
                PendingMovements =
                  Map
                      [ ("France",
                         { Nation = "France"; TargetSpace = Space.Investor; BillingId = previousBillingIdForFreeMove }) ] }

          when_ [ Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute ]

          expect
              "pending move is rejected"
              (hasExactEvent (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Investor }))

          expect
              "previous charge is voided"
              (hasExactCommand (VoidCharge { GameId = gameId; BillingId = previousBillingIdForFreeMove }))

          expect "no payment is required" (hasChargeCommand >> not)
          expect "action is determined" hasActionDetermined
      }

      let invoicePaidBillingId = Guid.NewGuid() |> Id |> RondelBillingId.ofId

      spec "paying a pending movement completes it and determines action" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("Austria", Some Space.ManeuverOne) ]
                PendingMovements =
                  Map
                      [ ("Austria",
                         { Nation = "Austria"; TargetSpace = Space.Investor; BillingId = invoicePaidBillingId }) ] }

          when_
              [ InvoicePaid { GameId = gameId; BillingId = invoicePaidBillingId } |> Handle
                Move { GameId = gameId; Nation = "Austria"; Space = Space.Import } |> Execute ]

          expect
              "action is determined from pending movement"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Investor }))

          expect
              "subsequent move uses starts from updated position"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Import }))
      }

      spec "paying the same pending movement twice completes it only once" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("Austria", Some Space.ManeuverOne) ]
                PendingMovements =
                  Map
                      [ ("Austria",
                         { Nation = "Austria"; TargetSpace = Space.Investor; BillingId = invoicePaidBillingId }) ] }

          when_
              [ InvoicePaid { GameId = gameId; BillingId = invoicePaidBillingId } |> Handle
                InvoicePaid { GameId = gameId; BillingId = invoicePaidBillingId } |> Handle ]

          expect "action is determined from pending movement, only once" (fun ctx ->
              countExactEvent (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Investor }) ctx = 1)
      }

      let voidedBillingId = Guid.NewGuid() |> Id |> RondelBillingId.ofId

      spec "payment for a voided pending movement is ignored" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("France", Some Space.Taxation); ("Austria", None) ]
                PendingMovements = Map.empty }
          
          actions
              [ Move { Nation = "France"; Space = Space.Investor; GameId = gameId } |> Execute
                InvoicePaymentFailed { BillingId = invoicePaidBillingId; GameId = gameId  } |> Handle ]

          when_
              [ InvoicePaid { GameId = gameId; BillingId = voidedBillingId } |> Handle
                Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute ]

          expect
              "late payment preserves the already completed movement"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory }))

          expect
              "late payment does not determine the action"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Investor }) >> not)
      } ]

let renderSpecMarkdown options =
    SpecMarkdown.toMarkdownDocument options runner rondelSpecs

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList "Rondel" (rondelSpecs |> List.map (toExpecto runner)) 
