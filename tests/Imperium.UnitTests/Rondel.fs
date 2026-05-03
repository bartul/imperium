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
        | StartCell -> "↻"

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

            String.concat "\n" (boardLines @ [ border ])

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
    CollectionAssert.forAccessor (fun (ctx: RondelContext) -> ctx.Events :> seq<_>)

let private commands =
    CollectionAssert.forAccessor (fun (ctx: RondelContext) -> ctx.Commands :> seq<_>)

let private assertExactEvent event_ message = events.Has event_ message

let private assertStartingPositionsSet gameId =
    assertExactEvent (PositionedAtStart { GameId = gameId }) "starting positions should be set"

let private assertNoStartingPositionsSet gameId =
    events.HasNone (fun e -> e = PositionedAtStart { GameId = gameId }) "starting positions should not be set"

let private assertActionDetermined =
    events.HasAny
        (function
        | ActionDetermined _ -> true
        | _ -> false)
        "action should be determined"

let private assertNoActionDetermined =
    events.HasNone
        (function
        | ActionDetermined _ -> true
        | _ -> false)
        "no action should be determined"

let private assertChargeCommand =
    commands.HasAny
        (function
        | ChargeMovement _ -> true
        | _ -> false)
        "charge command should be dispatched"

let private assertNoChargeCommand =
    commands.HasNone
        (function
        | ChargeMovement _ -> true
        | _ -> false)
        "no charge command should be dispatched"

let private assertChargeCommandOfM millions =
    let amount = Amount.unsafe millions

    commands.HasAny
        (function
        | ChargeMovement cmd when cmd.Amount = amount -> true
        | _ -> false)
        $"charge command of %d{millions}M should be dispatched"

let private assertExactCommand command message = commands.Has command message

let private assertExactEventCount event_ expectedCount message =
    events.Count event_ expectedCount message

let private getNationPositionsResult ctx = ctx.GetNationPositions()

let private newBillingId () = Id.newId () |> RondelBillingId.ofId

let private assertNoNationPositions ctx =
    Expect.isNone (getNationPositionsResult ctx) "no positions should be returned"

let private assertNationPositions ctx =
    Expect.isSome (getNationPositionsResult ctx) "positions should be returned"

let private assertNationPositionsForGameId gameId ctx =
    let result = getNationPositionsResult ctx
    Expect.isSome result "positions should be returned"
    Expect.equal result.Value.GameId gameId "positions should belong to expected game"

let private assertNationPositionsCount expectedCount ctx =
    let result = getNationPositionsResult ctx
    Expect.isSome result "positions should be returned"
    Expect.equal (List.length result.Value.Positions) expectedCount "nation position count should match"

let private assertNationPosition nation currentSpace pendingSpace ctx =
    let result = getNationPositionsResult ctx
    Expect.isSome result "positions should be returned"

    let position = result.Value.Positions |> List.tryFind (fun p -> p.Nation = nation)

    Expect.isSome position $"position for %s{nation} should exist"
    Expect.equal position.Value.CurrentSpace currentSpace $"%s{nation} current space should match"
    Expect.equal position.Value.PendingSpace pendingSpace $"%s{nation} pending space should match"

let private getRondelOverviewResult ctx = ctx.GetRondelOverview()

let private assertNoRondelOverview ctx =
    Expect.isNone (getRondelOverviewResult ctx) "no overview should be returned"

let private assertRondelOverview ctx =
    Expect.isSome (getRondelOverviewResult ctx) "overview should be returned"

let private assertRondelOverviewForGameId gameId ctx =
    let result = getRondelOverviewResult ctx
    Expect.isSome result "overview should be returned"
    Expect.equal result.Value.GameId gameId "overview should belong to expected game"

let private assertRondelOverviewNationNames expectedNames ctx =
    let result = getRondelOverviewResult ctx
    Expect.isSome result "overview should be returned"
    Expect.equal (result.Value.NationNames |> List.sort) (expectedNames |> List.sort) "nation names should match"

// ────────────────────────────────────────────────────────────────────────────────
// Specs: move
// ────────────────────────────────────────────────────────────────────────────────

let private rondelSpecs =
    let gameId = Id.newId ()
    let nations = Set.ofList [ "France"; "Austria" ]
    let spec = specOn (fun () -> createContext gameId)

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
              |> RondelState.withNationPositions [ ("France", Space.Factory); ("Austria", Space.Investor) ]
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
              "subsequent move uses starts from updated position"
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

let renderSpecMarkdown
    (options: SpecMarkdown.MarkdownRenderOptions)
    (filter: SpecFilter.T)
    (rootPath: string list)
    : string option =
    rondelSpecs
    |> SpecFilter.apply filter (rootPath @ [ "Rondel" ])
    |> SpecMarkdown.render options "Rondel" runner

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests = testList "Rondel" (rondelSpecs |> List.map (toExpecto runner))
