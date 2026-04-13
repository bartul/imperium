module Imperium.UnitTests.Rondel

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

module private RondelStateFormatting =
    type BoardCell =
        | SpaceCell of Space
        | StartCell

    type CellToken = { Nation: string; Text: string }

    let private abbreviateNation =
        function
        | "Austria" -> "AH"
        | "Austria-Hungary" -> "AH"
        | "France" -> "FR"
        | "Britain" -> "GB"
        | "Germany" -> "GE"
        | "Great Britain" -> "GB"
        | "Italy" -> "IT"
        | "Russia" -> "RU"
        | name when name.Length >= 2 -> name[..1].ToUpperInvariant()
        | name -> name.ToUpperInvariant()

    let private boardCellName =
        function
        | SpaceCell Space.Investor -> "Investor"
        | SpaceCell Space.Import -> "Import"
        | SpaceCell Space.ProductionOne
        | SpaceCell Space.ProductionTwo -> "Production"
        | SpaceCell Space.ManeuverOne
        | SpaceCell Space.ManeuverTwo -> "Maneuver"
        | SpaceCell Space.Taxation -> "Taxation"
        | SpaceCell Space.Factory -> "Factory"
        | StartCell -> "Start (o)"

    let private boardCellForPosition =
        function
        | Some space -> SpaceCell space
        | None -> StartCell

    let private boardRows =
        [ [ SpaceCell Space.ManeuverTwo
            SpaceCell Space.Investor
            SpaceCell Space.Import ]
          [ SpaceCell Space.ProductionTwo; StartCell; SpaceCell Space.ProductionOne ]
          [ SpaceCell Space.Factory
            SpaceCell Space.Taxation
            SpaceCell Space.ManeuverOne ] ]

    let private addToken cell token tokensByCell =
        let existing = tokensByCell |> Map.tryFind cell |> Option.defaultValue []
        tokensByCell |> Map.add cell (token :: existing)

    let private cellContent tokensByCell cell =
        tokensByCell
        |> Map.tryFind cell
        |> Option.defaultValue []
        |> List.sortBy (fun token -> token.Nation)
        |> List.map (fun token -> token.Text)
        |> String.concat " "

    let private center width (text: string) =
        let padding = max 0 (width - text.Length)
        let left = padding / 2
        let right = padding - left
        String.replicate left " " + text + String.replicate right " "

    let private renderBoardRow width cells tokensByCell =
        let renderLine renderCell =
            cells |> List.map renderCell |> String.concat "|" |> (fun line -> $"|{line}|")

        let titleLine = renderLine (fun cell -> $" {center width (boardCellName cell)} ")

        let contentLine =
            renderLine (fun cell -> $" {center width (cellContent tokensByCell cell)} ")

        [ titleLine; contentLine ]

    let format (state: RondelState option) : string =
        match state with
        | None -> "No rondel state"
        | Some state ->
            let tokensByCell =
                state.NationPositions
                |> Map.toList
                |> List.fold
                    (fun currentTokens (nation, currentSpace) ->
                        let abbreviation = abbreviateNation nation
                        let originCell = boardCellForPosition currentSpace

                        match state.PendingMovements |> Map.tryFind nation with
                        | Some pending ->
                            currentTokens
                            |> addToken originCell { Nation = nation; Text = $"{abbreviation}->" }
                            |> addToken (SpaceCell pending.TargetSpace) { Nation = nation; Text = $"->{abbreviation}" }
                        | None -> currentTokens |> addToken originCell { Nation = nation; Text = abbreviation })
                    Map.empty

            let cellWidth =
                boardRows
                |> List.collect id
                |> List.collect (fun cell -> [ boardCellName cell; cellContent tokensByCell cell ])
                |> List.map String.length
                |> List.max
                |> max 12

            let border =
                [ 1..3 ]
                |> List.map (fun _ -> String.replicate (cellWidth + 2) "-")
                |> String.concat "+"
                |> fun line -> $"+{line}+"

            let boardLines =
                boardRows
                |> List.collect (fun row -> border :: renderBoardRow cellWidth row tokensByCell)

            String.concat
                "\n"
                (boardLines
                 @ [ border
                     "Legend: FR = current position, FR-> = pending move origin, ->FR = pending move target" ])

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
                | false, _ -> None)
        FormatState = Some RondelStateFormatting.format }

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

let private newBillingId () = Id.newId () |> RondelBillingId.ofId

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
    let gameId = Id.newId ()
    let nations = Set.ofList [ "France"; "Austria" ]
    let spec = specOn (fun () -> createContext gameId)

    [ spec "starting setup places nations at their opening positions" {
          when_command (SetToStartingPositions { GameId = gameId; Nations = nations })

          expect "opening positions are set" (hasStartingPositionsSet gameId)
      }

      spec "starting setup can be applied only once per game" {
          state (RondelState.create gameId nations)

          when_command (SetToStartingPositions { GameId = gameId; Nations = nations })

          expect "second setup attempt is ignored" (hasStartingPositionsSet gameId >> not)
      }

      spec "any attempted move is rejected until nations are set to starting positions" {
          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Factory })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor })

          expect
              "reject the move"
              (hasExactEvent (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Factory }))

          expect "no action determined" (hasActionDetermined >> not)
          expect "no payment required" (hasChargeCommand >> not)
      }

      spec "moving a nation for the first time does not require payment regardless of destination" {
          when_command (SetToStartingPositions { GameId = gameId; Nations = nations })
          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Factory })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor })

          expect
              "action is determined"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory }))

          expect "no payment required" (hasChargeCommand >> not)
      }

      spec "moving a nation to its current position is rejected (stay put)" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPositions [ ("France", Space.Factory); ("Austria", Space.Investor) ]
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Factory })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor })

          expect
              "rejects the move"
              (hasExactEvent (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Factory }))

          expect "no action determined" (hasActionDetermined >> not)
          expect "no payment required" (hasChargeCommand >> not)
      }

      spec "moving a nation 1-3 spaces is free" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.Investor
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.ProductionOne })

          expect "action is determined" hasActionDetermined
          expect "no payment required" (hasChargeCommand >> not)
      }

      spec "moving a nation 4 spaces requires payment of 2M" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ProductionOne
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo })

          expect "no action determined" (hasActionDetermined >> not)
          expect "payment is required" hasChargeCommand
          expect "payment amount is 2M" (hasChargeCommandOfM 2)
      }

      spec "moving a nation 5 spaces requires payment of 4M" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ManeuverOne
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Investor })

          expect "no action determined" (hasActionDetermined >> not)
          expect "payment is required" hasChargeCommand
          expect "payment amount is 4M" (hasChargeCommandOfM 4)
      }

      spec "moving a nation 6 spaces requires payment of 6M" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.Investor
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo })

          expect "no action determined" (hasActionDetermined >> not)
          expect "payment is required" hasChargeCommand
          expect "payment amount is 6M" (hasChargeCommandOfM 6)
      }

      spec "moving a nation 7 spaces is rejected as exceeding maximum distance" {
          state (
              RondelState.create gameId nations
              |> RondelState.withNationPosition "France" Space.ProductionOne
          )

          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Import })

          expect
              "rejects the move"
              (hasExactEvent (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Import }))

          expect "no action determined" (hasActionDetermined >> not)
          expect "no payment required" (hasChargeCommand >> not)
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
              (hasExactEvent (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Investor }))

          expect
              "previous charge is voided"
              (hasExactCommand (VoidCharge { GameId = gameId; BillingId = previousBillingIdForFreeMove }))

          expect "no payment is required" (hasChargeCommand >> not)
          expect "action is determined" hasActionDetermined
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
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Investor }))

          expect
              "subsequent move uses starts from updated position"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Import }))
      }

      spec "paying the same pending movement twice completes it only once" {
          state (
              RondelState.create gameId (Set.ofList [ "Austria" ])
              |> RondelState.withNationPosition "Austria" Space.ManeuverOne
              |> RondelState.withPendingMove "Austria" Space.Investor invoicePaidBillingId
          )

          when_event (InvoicePaid { GameId = gameId; BillingId = invoicePaidBillingId })
          when_event (InvoicePaid { GameId = gameId; BillingId = invoicePaidBillingId })

          expect "action is determined from pending movement, only once" (fun ctx ->
              hasExactEventCount
                  (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Investor })
                  1
                  ctx)
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
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory }))

          expect
              "late payment does not determine the action"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Investor })
               >> not)
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
              (hasExactEvent (MoveToActionSpaceRejected { GameId = gameId; Nation = "Austria"; Space = Space.Investor }))

          expect
              "failed payment does not determine pending action"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Investor })
               >> not)

          expect
              "subsequent move starts from original position"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Factory }))
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
              (hasExactEventCount
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "Austria"; Space = Space.ManeuverTwo })
                  1)

          expect
              "subsequent move starts from original position"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Austria"; Action = Action.Factory }))
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
              (hasExactEventCount
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo })
                  1)

          expect
              "late failure preserves the already completed movement"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory }))
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
              (hasExactEventCount
                  (MoveToActionSpaceRejected { GameId = gameId; Nation = "Britain"; Space = Space.ProductionTwo })
                  0)

          expect
              "subsequent move starts from paid target space"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "Britain"; Action = Action.Maneuver }))
      }

      spec "query nation positions returns none for unknown game" {
          expect "no positions are returned" hasNoNationPositions
      }

      spec "query nation positions returns initialized nations before any move" {
          when_command (SetToStartingPositions { GameId = gameId; Nations = nations })

          expect "positions are returned" hasNationPositions
          expect "query result belongs to current game" (hasNationPositionsForGameId gameId)
          expect "all initialized nations are present" (hasNationPositionsCount 2)
          expect "nation has no current or pending space" (hasNationPosition "France" None None)
      }

      spec "query nation positions returns current space after a free move" {
          when_command (SetToStartingPositions { GameId = gameId; Nations = nations })
          when_command (Move { GameId = gameId; Nation = "France"; Space = Space.Factory })

          expect "positions are returned" hasNationPositions
          expect "nation's current space is updated" (hasNationPosition "France" (Some Space.Factory) None)
      }

      spec "query nation positions returns pending space for an unpaid move" {
          when_command (SetToStartingPositions { GameId = gameId; Nations = Set.ofList [ "Austria" ] })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor })
          when_command (Move { GameId = gameId; Nation = "Austria"; Space = Space.Factory })

          expect "positions are returned" hasNationPositions

          expect
              "nation shows current and pending spaces"
              (hasNationPosition "Austria" (Some Space.Investor) (Some Space.Factory))
      }

      spec "query rondel overview returns none for unknown game" {
          expect "no overview is returned" hasNoRondelOverview
      }

      spec "query rondel overview returns initialized nation names" {
          when_command (
              SetToStartingPositions { GameId = gameId; Nations = Set.ofList [ "France"; "Germany"; "Austria" ] }
          )

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
