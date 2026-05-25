module Imperium.UnitTests.Rondel.Specs

open Expecto
open Imperium.Rondel
open Imperium.Primitives
open Imperium.Testing.Spec
open Imperium.Testing.Spec.Specification
open Imperium.UnitTests.Rondel.Assertions

// ────────────────────────────────────────────────────────────────────────────────
// Runner
// ────────────────────────────────────────────────────────────────────────────────

let private runner: SpecRunner<Context, RondelState, RondelState option, RondelCommand, RondelInboundEvent> =
    { SpecRunner.empty with
        Execute = fun ctx cmd -> Rondel.execute ctx.Deps cmd |> Async.RunSynchronously
        Handle = fun ctx evt -> Rondel.handle ctx.Deps evt |> Async.RunSynchronously
        ClearEvents = fun ctx -> ctx.Events.Clear()
        ClearCommands = fun ctx -> ctx.Commands.Clear()
        SeedState = fun ctx state -> ctx.Store[ctx.GameId] <- state
        CaptureState =
            Some(fun ctx ->
                match ctx.Store.TryGetValue(ctx.GameId) with
                | true, state -> Some state
                | false, _ -> None)
        FormatState = Some StateFormatting.format }

// ────────────────────────────────────────────────────────────────────────────────
// Specs
// ────────────────────────────────────────────────────────────────────────────────

let private specifications =
    let gameId = Id.newId ()
    let nations = Set.ofList [ "France"; "Austria" ]
    let spec = specOn (fun () -> Context.create gameId)

    [ spec "starting setup places nations at their opening positions" {
          when_command (SetToStartingPositions { GameId = gameId; Nations = nations })

          expect "opening positions are set" (assertStartingPositionsSet gameId)
      }

      spec "starting setup can be applied only once per game" {
          state (RondelState.create gameId nations)

          when_command (SetToStartingPositions { GameId = gameId; Nations = nations })

          expect "second setup attempt is ignored" (assertNoStartingPositionsSet gameId)
      }

      spec "any attempted move is rejected until nations are set to starting positions" {
          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Factory })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor })

          expect
              "reject the move"
              (assertExactEvent
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Factory })
                  "move should be rejected")

          expect "no action determined" assertNoActionDetermined
          expect "no payment required" assertNoChargeCommand
      }

      spec "moving a nation for the first time does not require payment regardless of destination" {
          when_command (SetToStartingPositions { GameId = gameId; Nations = nations })
          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Factory })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor })

          expect
              "action is determined"
              (assertExactEvent
                  (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory })
                  "action should be determined")

          expect "no payment required" assertNoChargeCommand
      }

      spec "moving a nation to its current position is rejected (stay put)" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPositions [ "France", Space.Factory; "Austria", Space.Investor ]
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Factory })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor })

          expect
              "rejects the move"
              (assertExactEvent
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Factory })
                  "move should be rejected")

          expect "no action determined" assertNoActionDetermined
          expect "no payment required" assertNoChargeCommand
      }

      spec "moving a nation 1-3 spaces is free" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.Investor
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.ProductionOne })

          expect "action is determined" assertActionDetermined
          expect "no payment required" assertNoChargeCommand
      }

      spec "moving a nation 4 spaces requires payment of 2M" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ProductionOne
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo })

          expect "no action determined" assertNoActionDetermined
          expect "payment is required" assertChargeCommand
          expect "payment amount is 2M" (assertChargeCommandOfM 2)
      }

      spec "moving a nation 5 spaces requires payment of 4M" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ManeuverOne
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Investor })

          expect "no action determined" assertNoActionDetermined
          expect "payment is required" assertChargeCommand
          expect "payment amount is 4M" (assertChargeCommandOfM 4)
      }

      spec "moving a nation 6 spaces requires payment of 6M" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.Investor
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo })

          expect "no action determined" assertNoActionDetermined
          expect "payment is required" assertChargeCommand
          expect "payment amount is 6M" (assertChargeCommandOfM 6)
      }

      spec "moving a nation 7 spaces is rejected as exceeding maximum distance" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ProductionOne
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Import })

          expect
              "rejects the move"
              (assertExactEvent
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Import })
                  "move should be rejected")

          expect "no action determined" assertNoActionDetermined
          expect "no payment required" assertNoChargeCommand
      }

      let previousBillingId = newBillingId ()

      spec "moving a nation with a pending paid move to another paid move voids the old charge" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ProductionOne
              |> RondelState.withPendingMove "France" Space.ProductionTwo previousBillingId
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.ManeuverTwo })

          expect
              "pending move is rejected"
              (assertExactEvent
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo })
                  "pending move should be rejected")

          expect
              "previous charge is voided"
              (assertExactCommand
                  (VoidCharge { GameId = gameId; BillingId = previousBillingId })
                  "void command should be dispatched")

          expect "new payment is required" assertChargeCommand
          expect "payment amount is 4M" (assertChargeCommandOfM 4)
          expect "no action determined" assertNoActionDetermined
      }

      let previousBillingIdForFreeMove = newBillingId ()

      spec "moving a nation with a pending paid move to a free move voids the old charge and completes immediately" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ManeuverOne
              |> RondelState.withPendingMove "France" Space.Investor previousBillingIdForFreeMove
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Factory })

          expect
              "pending move is rejected"
              (assertExactEvent
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Investor })
                  "pending move should be rejected")

          expect
              "previous charge is voided"
              (assertExactCommand
                  (VoidCharge { GameId = gameId; BillingId = previousBillingIdForFreeMove })
                  "void command should be dispatched")

          expect "no payment is required" assertNoChargeCommand
          expect "action is determined" assertActionDetermined
      }

      let invoicePaidBillingId = newBillingId ()

      spec "paying a pending movement completes it and determines action" {
          state (
              RondelState.create gameId (Set.ofList [ "Austria" ])
              |> RondelState.withNationPosition "Austria" Space.ManeuverOne
              |> RondelState.withPendingMove "Austria" Space.Investor invoicePaidBillingId
          )

          when_event (InvoicePaid { GameId = gameId; BillingId = invoicePaidBillingId })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Import })

          expect
              "action is determined from pending movement"
              (assertExactEvent
                  (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Investor })
                  "investor action should be determined")

          expect
              "subsequent move starts from updated position"
              (assertExactEvent
                  (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Import })
                  "import action should be determined")
      }

      spec "paying the same pending movement twice completes it only once" {
          state (
              RondelState.create gameId (Set.ofList [ "Austria" ])
              |> RondelState.withNationPosition "Austria" Space.ManeuverOne
              |> RondelState.withPendingMove "Austria" Space.Investor invoicePaidBillingId
          )

          when_event (InvoicePaid { GameId = gameId; BillingId = invoicePaidBillingId })
          when_event (InvoicePaid { GameId = gameId; BillingId = invoicePaidBillingId })

          expect
              "action is determined from pending movement, only once"
              (assertExactEventCount
                  (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Investor })
                  1
                  "action should be determined exactly once")
      }

      let voidedBillingId = newBillingId ()

      spec "payment for a voided pending movement is ignored" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.Taxation
          )

          given_command (Move { Nation = "France"; Space = Space.Investor; GameId = gameId })
          given_event (InvoicePaymentFailed { BillingId = invoicePaidBillingId; GameId = gameId })

          when_event (InvoicePaid { GameId = gameId; BillingId = voidedBillingId })
          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Factory })

          expect
              "late payment preserves the already completed movement"
              (assertExactEvent
                  (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory })
                  "factory action should be determined")

          expect
              "late payment does not determine the action"
              (events.HasNone
                  (fun e -> e = ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Investor })
                  "investor action should not be determined")
      }

      let invoicePaymentFailedBillingId = newBillingId ()

      spec "payment failure rejects pending movement and keeps original position" {
          state (
              RondelState.create gameId (Set.ofList [ "Austria" ])
              |> RondelState.withNationPosition "Austria" Space.ManeuverOne
              |> RondelState.withPendingMove "Austria" Space.Investor invoicePaymentFailedBillingId
          )

          when_event (InvoicePaymentFailed { GameId = gameId; BillingId = invoicePaymentFailedBillingId })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Factory })

          expect
              "pending movement is rejected"
              (assertExactEvent
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "Austria"; Space = Space.Investor })
                  "pending move should be rejected")

          expect
              "failed payment does not determine pending action"
              (events.HasNone
                  (fun e -> e = ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Investor })
                  "investor action should not be determined")

          expect
              "subsequent move starts from original position"
              (assertExactEvent
                  (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Factory })
                  "factory action should be determined")
      }

      let invoicePaymentFailedTwiceBillingId = newBillingId ()

      spec "processing the same payment failure twice rejects pending movement only once" {
          state (
              RondelState.create gameId (Set.ofList [ "Austria" ])
              |> RondelState.withNationPosition "Austria" Space.ManeuverOne
              |> RondelState.withPendingMove "Austria" Space.ManeuverTwo invoicePaymentFailedTwiceBillingId
          )

          when_event (InvoicePaymentFailed { GameId = gameId; BillingId = invoicePaymentFailedTwiceBillingId })
          when_event (InvoicePaymentFailed { GameId = gameId; BillingId = invoicePaymentFailedTwiceBillingId })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Factory })

          expect
              "pending movement is rejected only once"
              (assertExactEventCount
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "Austria"; Space = Space.ManeuverTwo })
                  1
                  "rejection should occur exactly once")

          expect
              "subsequent move starts from original position"
              (assertExactEvent
                  (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Factory })
                  "factory action should be determined")
      }

      let voidedChargeFailureBillingId = newBillingId ()

      spec "payment failure for a voided charge is ignored" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ProductionOne
              |> RondelState.withPendingMove "France" Space.ProductionTwo voidedChargeFailureBillingId
          )

          given_command (Move { GameId = gameId; Nation = "France"; Space = Space.Taxation })
          preserve

          when_event (InvoicePaymentFailed { GameId = gameId; BillingId = voidedChargeFailureBillingId })
          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Factory })

          expect
              "voided pending movement is not rejected again"
              (assertExactEventCount
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo })
                  1
                  "rejection should occur exactly once")

          expect
              "late failure preserves the already completed movement"
              (assertExactEvent
                  (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory })
                  "factory action should be determined")
      }

      let paymentThenFailureBillingId = newBillingId ()

      spec "payment failure after successful payment is ignored" {
          state (
              RondelState.create gameId (Set.ofList [ "Britain" ])
              |> RondelState.withNationPosition "Britain" Space.Import
              |> RondelState.withPendingMove "Britain" Space.ProductionTwo paymentThenFailureBillingId
          )

          given_event (InvoicePaid { GameId = gameId; BillingId = paymentThenFailureBillingId })

          when_event (InvoicePaymentFailed { GameId = gameId; BillingId = paymentThenFailureBillingId })
          when_command (Move { GameId = gameId; Nation = "Britain"; Space = Space.ManeuverTwo })

          expect
              "late failure does not reject already completed movement"
              (assertExactEventCount
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "Britain"; Space = Space.ProductionTwo })
                  0
                  "rejection count should be zero")

          expect
              "subsequent move starts from paid target space"
              (assertExactEvent
                  (ActionDetermined { GameId = gameId; Nation = "Britain"; Action = Action.Maneuver })
                  "maneuver action should be determined")
      }

      spec "query nation positions returns none for unknown game" {
          expect "no positions are returned" assertNoNationPositions
      }

      spec "query nation positions returns initialized nations before any move" {
          when_command (SetToStartingPositions { GameId = gameId; Nations = nations })

          expect "positions are returned" assertNationPositions
          expect "query result belongs to current game" (assertNationPositionsForGameId gameId)
          expect "all initialized nations are present" (assertNationPositionsCount 2)
          expect "nation has no current or pending space" (assertNationPosition "France" None None)
      }

      spec "query nation positions returns current space after a free move" {
          when_command (SetToStartingPositions { GameId = gameId; Nations = nations })
          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Factory })

          expect "positions are returned" assertNationPositions
          expect "nation's current space is updated" (assertNationPosition "France" (Some Space.Factory) None)
      }

      spec "query nation positions returns pending space for an unpaid move" {
          when_command (SetToStartingPositions { GameId = gameId; Nations = Set.ofList [ "Austria" ] })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Factory })

          expect "positions are returned" assertNationPositions

          expect
              "nation shows current and pending spaces"
              (assertNationPosition "Austria" (Some Space.Investor) (Some Space.Factory))
      }

      spec "query rondel overview returns none for unknown game" {
          expect "no overview is returned" assertNoRondelOverview
      }

      spec "query rondel overview returns initialized nation names" {
          when_command (
              SetToStartingPositions { GameId = gameId; Nations = Set.ofList [ "France"; "Germany"; "Austria" ] }
          )

          expect "overview is returned" assertRondelOverview
          expect "query result belongs to current game" (assertRondelOverviewForGameId gameId)

          expect
              "overview contains initialized nations"
              (assertRondelOverviewNationNames [ "Austria"; "France"; "Germany" ])
      } ]

let renderMarkdown
    (options: Markdown.RenderOptions)
    (filter: SpecFilter.Predicate)
    (rootPath: string list)
    : string option =
    specifications
    |> SpecFilter.apply filter (rootPath @ [ "Rondel" ])
    |> Markdown.render options "Rondel" runner

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList "Rondel" (specifications |> List.map (SpecRunner.toExpectoTestList runner))
