module Imperium.UnitTests.Rondel

open System
open System.Collections.Generic
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
      GameId: Id
      GetNationPositions: unit -> RondelPositionsView option
      GetRondelOverview: unit -> RondelView option }

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

    let queryDeps: RondelQueryDependencies = { Load = load }

    let getNationPositionsForGame () =
        getNationPositions queryDeps { GameId = gameId } |> Async.RunSynchronously

    let getRondelOverviewForGame () =
        getRondelOverview queryDeps { GameId = gameId } |> Async.RunSynchronously

    { Deps = { Load = load; Save = save; Publish = publish; Dispatch = dispatch }
      Events = events
      Commands = commands
      Store = store
      GameId = gameId
      GetNationPositions = getNationPositionsForGame
      GetRondelOverview = getRondelOverviewForGame }

// ────────────────────────────────────────────────────────────────────────────────
// Runner
// ────────────────────────────────────────────────────────────────────────────────

let private runner =
    { SpecRunner.empty with
        Execute = fun ctx cmd -> execute ctx.Deps cmd |> Async.RunSynchronously
        Handle = fun ctx evt -> handle ctx.Deps evt |> Async.RunSynchronously
        ClearEvents = fun ctx -> ctx.Events.Clear()
        ClearCommands = fun ctx -> ctx.Commands.Clear()
        SeedState = fun ctx state -> ctx.Store[ctx.GameId] <- state
        CaptureState =
            Some(fun ctx ->
                match ctx.Store.TryGetValue(ctx.GameId) with
                | true, state -> Some state
                | false, _ -> None) }

// ────────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────────

let private events =
    CollectionExpect.forAccessor (fun (ctx: RondelContext) -> ctx.Events :> seq<_>)

let private commands =
    CollectionExpect.forAccessor (fun (ctx: RondelContext) -> ctx.Commands :> seq<_>)

let hasExactEvent event_ = events.Has event_

let private hasStartingPositionsSet gameId =
    hasExactEvent (PositionedAtStart { GameId = gameId })

let private hasActionDetermined =
    events.HasAny (function
        | ActionDetermined _ -> true
        | _ -> false)

let private hasRejection =
    events.HasAny (function
        | MoveToActionSpaceRejected _ -> true
        | _ -> false)

let private hasChargeCommand =
    commands.HasAny (function
        | ChargeMovement _ -> true
        | _ -> false)

let private hasChargeCommandOf (amount: Amount) =
    commands.HasAny (function
        | ChargeMovement cmd when cmd.Amount = amount -> true
        | _ -> false)

let private hasChargeCommandOfM millions =
    hasChargeCommandOf (Amount.unsafe millions)

let private hasExactCommand command = commands.Has command

let private countExactEvent event_ = events.Count event_

let private hasExactEventCount event_ expectedCount = events.HasCount event_ expectedCount

let private getNationPositionsResult ctx = ctx.GetNationPositions()

let private hasNoNationPositions ctx =
    getNationPositionsResult ctx |> Option.isNone

let private hasNationPositions ctx =
    getNationPositionsResult ctx |> Option.isSome

let private hasNationPositionsForGameId gameId ctx =
    getNationPositionsResult ctx |> Option.exists (fun view -> view.GameId = gameId)

let private hasNationPositionsCount expectedCount ctx =
    getNationPositionsResult ctx
    |> Option.exists (fun view -> List.length view.Positions = expectedCount)

let private hasNationPosition nation currentSpace pendingSpace ctx =
    getNationPositionsResult ctx
    |> Option.bind (fun view -> view.Positions |> List.tryFind (fun p -> p.Nation = nation))
    |> Option.exists (fun position -> position.CurrentSpace = currentSpace && position.PendingSpace = pendingSpace)

let private getRondelOverviewResult ctx = ctx.GetRondelOverview()

let private hasNoRondelOverview ctx =
    getRondelOverviewResult ctx |> Option.isNone

let private hasRondelOverview ctx =
    getRondelOverviewResult ctx |> Option.isSome

let private hasRondelOverviewForGameId gameId ctx =
    getRondelOverviewResult ctx |> Option.exists (fun view -> view.GameId = gameId)

let private hasRondelOverviewNationNames expectedNames ctx =
    getRondelOverviewResult ctx
    |> Option.exists (fun view -> (view.NationNames |> List.sort) = (expectedNames |> List.sort))

let private hasVoidCommand =
    commands.HasAny (function
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

          state (RondelState.create gameId nations)

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

          state (
              RondelState.create gameId nations
              |> RondelState.withNationPositions [ "France", Space.Factory; "Austria", Space.Investor ]
          )

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

          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.Investor
          )

          when_
              [ Move { GameId = gameId; Nation = "France"; Space = Space.ProductionOne }
                |> Execute ]

          expect "action is determined" hasActionDetermined
          expect "no payment required" (hasChargeCommand >> not)
      }

      spec "moving a nation 4 spaces requires payment of 2M" {
          on (fun () -> createContext gameId)

          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ProductionOne
          )

          when_
              [ Move { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo }
                |> Execute ]

          expect "no action determined" (hasActionDetermined >> not)
          expect "payment is required" hasChargeCommand
          expect "payment amount is 2M" (hasChargeCommandOfM 2)
      }

      spec "moving a nation 5 spaces requires payment of 4M" {
          on (fun () -> createContext gameId)

          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ManeuverOne
          )

          when_ [ Move { GameId = gameId; Nation = "France"; Space = Space.Investor } |> Execute ]

          expect "no action determined" (hasActionDetermined >> not)
          expect "payment is required" hasChargeCommand
          expect "payment amount is 4M" (hasChargeCommandOfM 4)
      }

      spec "moving a nation 6 spaces requires payment of 6M" {
          on (fun () -> createContext gameId)

          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.Investor
          )

          when_
              [ Move { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo }
                |> Execute ]

          expect "no action determined" (hasActionDetermined >> not)
          expect "payment is required" hasChargeCommand
          expect "payment amount is 6M" (hasChargeCommandOfM 6)
      }

      spec "moving a nation 7 spaces is rejected as exceeding maximum distance" {
          on (fun () -> createContext gameId)

          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ProductionOne
          )

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

          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ProductionOne
              |> RondelState.withPendingMove "France" Space.ProductionTwo previousBillingId
          )

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

          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ManeuverOne
              |> RondelState.withPendingMove "France" Space.Investor previousBillingIdForFreeMove
          )

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

          state (
              RondelState.create gameId (Set.ofList [ "Austria" ])
              |> RondelState.withNationPosition "Austria" Space.ManeuverOne
              |> RondelState.withPendingMove "Austria" Space.Investor invoicePaidBillingId
          )

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

          state (
              RondelState.create gameId (Set.ofList [ "Austria" ])
              |> RondelState.withNationPosition "Austria" Space.ManeuverOne
              |> RondelState.withPendingMove "Austria" Space.Investor invoicePaidBillingId
          )

          when_
              [ InvoicePaid { GameId = gameId; BillingId = invoicePaidBillingId } |> Handle
                InvoicePaid { GameId = gameId; BillingId = invoicePaidBillingId } |> Handle ]

          expect "action is determined from pending movement, only once" (fun ctx ->
              hasExactEventCount
                  (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Investor })
                  1
                  ctx)
      }

      let voidedBillingId = Guid.NewGuid() |> Id |> RondelBillingId.ofId

      spec "payment for a voided pending movement is ignored" {
          on (fun () -> createContext gameId)

          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.Taxation
          )

          actions
              [ Move { Nation = "France"; Space = Space.Investor; GameId = gameId } |> Execute
                InvoicePaymentFailed { BillingId = invoicePaidBillingId; GameId = gameId }
                |> Handle ]

          when_
              [ InvoicePaid { GameId = gameId; BillingId = voidedBillingId } |> Handle
                Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute ]

          expect
              "late payment preserves the already completed movement"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory }))

          expect
              "late payment does not determine the action"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Investor })
               >> not)
      }

      let invoicePaymentFailedBillingId = Guid.NewGuid() |> Id |> RondelBillingId.ofId

      spec "payment failure rejects pending movement and keeps original position" {
          on (fun () -> createContext gameId)

          state (
              RondelState.create gameId (Set.ofList [ "Austria" ])
              |> RondelState.withNationPosition "Austria" Space.ManeuverOne
              |> RondelState.withPendingMove "Austria" Space.Investor invoicePaymentFailedBillingId
          )

          when_
              [ InvoicePaymentFailed { GameId = gameId; BillingId = invoicePaymentFailedBillingId }
                |> Handle
                Move { GameId = gameId; Nation = "Austria"; Space = Space.Factory } |> Execute ]

          expect
              "pending movement is rejected"
              (hasExactEvent (MoveToActionSpaceRejected { GameId = gameId; Nation = "Austria"; Space = Space.Investor }))

          expect
              "failed payment does not determine pending action"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Investor })
               >> not)

          expect
              "subsequent move starts from original position"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Factory }))
      }

      let invoicePaymentFailedTwiceBillingId =
          Guid.NewGuid() |> Id |> RondelBillingId.ofId

      spec "processing the same payment failure twice rejects pending movement only once" {
          on (fun () -> createContext gameId)

          state (
              RondelState.create gameId (Set.ofList [ "Austria" ])
              |> RondelState.withNationPosition "Austria" Space.ManeuverOne
              |> RondelState.withPendingMove "Austria" Space.ManeuverTwo invoicePaymentFailedTwiceBillingId
          )

          when_
              [ InvoicePaymentFailed { GameId = gameId; BillingId = invoicePaymentFailedTwiceBillingId }
                |> Handle
                InvoicePaymentFailed { GameId = gameId; BillingId = invoicePaymentFailedTwiceBillingId }
                |> Handle
                Move { GameId = gameId; Nation = "Austria"; Space = Space.Factory } |> Execute ]

          expect
              "pending movement is rejected only once"
              (hasExactEventCount
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "Austria"; Space = Space.ManeuverTwo })
                  1)

          expect
              "subsequent move starts from original position"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Factory }))
      }

      let voidedChargeFailureBillingId = Guid.NewGuid() |> Id |> RondelBillingId.ofId

      spec "payment failure for a voided charge is ignored" {
          on (fun () -> createContext gameId)

          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ProductionOne
              |> RondelState.withPendingMove "France" Space.ProductionTwo voidedChargeFailureBillingId
          )

          actions [ Move { GameId = gameId; Nation = "France"; Space = Space.Taxation } |> Execute ]
          preserve

          when_
              [ InvoicePaymentFailed { GameId = gameId; BillingId = voidedChargeFailureBillingId }
                |> Handle
                Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute ]

          expect
              "voided pending movement is not rejected again"
              (hasExactEventCount
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo })
                  1)

          expect
              "late failure preserves the already completed movement"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory }))
      }

      let paymentThenFailureBillingId = Guid.NewGuid() |> Id |> RondelBillingId.ofId

      spec "payment failure after successful payment is ignored" {
          on (fun () -> createContext gameId)

          state (
              RondelState.create gameId (Set.ofList [ "Britain" ])
              |> RondelState.withNationPosition "Britain" Space.Import
              |> RondelState.withPendingMove "Britain" Space.ProductionTwo paymentThenFailureBillingId
          )

          actions
              [ InvoicePaid { GameId = gameId; BillingId = paymentThenFailureBillingId }
                |> Handle ]

          when_
              [ InvoicePaymentFailed { GameId = gameId; BillingId = paymentThenFailureBillingId }
                |> Handle
                Move { GameId = gameId; Nation = "Britain"; Space = Space.ManeuverTwo }
                |> Execute ]

          expect
              "late failure does not reject already completed movement"
              (hasExactEventCount
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "Britain"; Space = Space.ProductionTwo })
                  0)

          expect
              "subsequent move starts from paid target space"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Britain"; Action = Action.Maneuver }))
      }

      spec "query nation positions returns none for unknown game" {
          on (fun () -> createContext gameId)

          when_ []

          expect "no positions are returned" hasNoNationPositions
      }

      spec "query nation positions returns initialized nations before any move" {
          on (fun () -> createContext gameId)

          when_ [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute ]

          expect "positions are returned" hasNationPositions
          expect "query result belongs to current game" (hasNationPositionsForGameId gameId)
          expect "all initialized nations are present" (hasNationPositionsCount 2)
          expect "nation has no current or pending space" (hasNationPosition "France" None None)
      }

      spec "query nation positions returns current space after a free move" {
          on (fun () -> createContext gameId)

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute
                Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute ]

          expect "positions are returned" hasNationPositions
          expect "nation's current space is updated" (hasNationPosition "France" (Some Space.Factory) None)
      }

      spec "query nation positions returns pending space for an unpaid move" {
          on (fun () -> createContext gameId)

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = Set.ofList [ "Austria" ] }
                |> Execute
                Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor } |> Execute
                Move { GameId = gameId; Nation = "Austria"; Space = Space.Factory } |> Execute ]

          expect "positions are returned" hasNationPositions

          expect
              "nation shows current and pending spaces"
              (hasNationPosition "Austria" (Some Space.Investor) (Some Space.Factory))
      }

      spec "query rondel overview returns none for unknown game" {
          on (fun () -> createContext gameId)

          when_ []

          expect "no overview is returned" hasNoRondelOverview
      }

      spec "query rondel overview returns initialized nation names" {
          on (fun () -> createContext gameId)

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = Set.ofList [ "France"; "Germany"; "Austria" ] }
                |> Execute ]

          expect "overview is returned" hasRondelOverview
          expect "query result belongs to current game" (hasRondelOverviewForGameId gameId)

          expect
              "overview contains initialized nations"
              (hasRondelOverviewNationNames [ "Austria"; "France"; "Germany" ])
      } ]

let renderSpecMarkdown options =
    SpecMarkdown.toMarkdownDocument options runner rondelSpecs

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests = testList "Rondel" (rondelSpecs |> List.map (toExpecto runner))
