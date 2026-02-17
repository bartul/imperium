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

let private moveSpecs =
    let gameId = Guid.NewGuid() |> Id
    let nations = Set.ofList [ "Austria"; "France"; "Germany" ]

    [ spec "any attempted move is rejected until nations are set to starting positions" {
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
                Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor } |> Execute
                Move { GameId = gameId; Nation = "Germany"; Space = Space.Import } |> Execute ]

          expect
              "action is determined"
              (hasExactEvent (ActionDetermined { GameId = gameId; Nation = "France"; Action = Action.Factory }))

          expect "no payment required" (hasChargeCommand >> not)
      }

      spec "moving a nation to its current position is rejected (stay put)" {
          on (fun () -> createContext gameId)

          state
              { GameId = gameId
                NationPositions = Map [ ("Austria", None); ("France", Some Space.Factory); ("Germany", None) ]
                PendingMovements = Map.empty }

          when_
              [ Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute
                Move { GameId = gameId; Nation = "Austria"; Space = Space.Investor } |> Execute
                Move { GameId = gameId; Nation = "Germany"; Space = Space.Import } |> Execute ]

          expect
              "rejects the move"
              (hasExactEvent (MoveToActionSpaceRejected { GameId = gameId; Nation = "France"; Space = Space.Factory }))

          expect "no action determined" (hasActionDetermined >> not)
          expect "no payment required" (hasChargeCommand >> not)
      }

      spec "move of 1-3 spaces is free" {
          on (fun () -> createContext gameId)

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute
                // First move to Investor (establishes position)
                Move { GameId = gameId; Nation = "France"; Space = Space.Investor } |> Execute
                ClearEvents
                ClearCommands
                // Second move: 2 spaces (Investor -> ProductionOne)
                Move { GameId = gameId; Nation = "France"; Space = Space.ProductionOne }
                |> Execute ]

          expect "action is determined" hasActionDetermined
          expect "no charge dispatched" (hasChargeCommand >> not)
      }

      spec "move of 4 spaces requires payment of 2M" {
          on (fun () -> createContext gameId)

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute
                // First move to ProductionOne (establishes position)
                Move { GameId = gameId; Nation = "France"; Space = Space.ProductionOne }
                |> Execute
                ClearEvents
                ClearCommands
                // Second move: 4 spaces (ProductionOne -> ProductionTwo)
                Move { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo }
                |> Execute ]

          expect "no action determined yet" (hasActionDetermined >> not)
          expect "charge dispatched" hasChargeCommand

          expect "charge amount is 2M" (fun ctx ->
              ctx.Commands
              |> Seq.choose (function
                  | ChargeMovement cmd -> Some cmd.Amount
                  | _ -> None)
              |> Seq.tryHead
              |> Option.map (fun a -> a = Amount.unsafe 2)
              |> Option.defaultValue false)
      }

      spec "move of 5 spaces requires payment of 4M" {
          on (fun () -> createContext gameId)

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute
                // First move to ManeuverOne (establishes position)
                Move { GameId = gameId; Nation = "France"; Space = Space.ManeuverOne }
                |> Execute
                ClearEvents
                ClearCommands
                // Second move: 5 spaces (ManeuverOne -> Investor)
                Move { GameId = gameId; Nation = "France"; Space = Space.Investor } |> Execute ]

          expect "no action determined yet" (hasActionDetermined >> not)
          expect "charge dispatched" hasChargeCommand

          expect "charge amount is 4M" (fun ctx ->
              ctx.Commands
              |> Seq.choose (function
                  | ChargeMovement cmd -> Some cmd.Amount
                  | _ -> None)
              |> Seq.tryHead
              |> Option.map (fun a -> a = Amount.unsafe 4)
              |> Option.defaultValue false)
      }

      spec "move of 6 spaces requires payment of 6M" {
          on (fun () -> createContext gameId)

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute
                // First move to Investor (establishes position at index 0)
                Move { GameId = gameId; Nation = "France"; Space = Space.Investor } |> Execute
                ClearEvents
                ClearCommands
                // Second move: 6 spaces (Investor(0) -> ProductionTwo(6))
                Move { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo }
                |> Execute ]

          expect "no action determined yet" (hasActionDetermined >> not)
          expect "charge dispatched" hasChargeCommand

          expect "charge amount is 6M" (fun ctx ->
              ctx.Commands
              |> Seq.choose (function
                  | ChargeMovement cmd -> Some cmd.Amount
                  | _ -> None)
              |> Seq.tryHead
              |> Option.map (fun a -> a = Amount.unsafe 6)
              |> Option.defaultValue false)
      }

      spec "move of 7 spaces exceeds maximum and is rejected" {
          on (fun () -> createContext gameId)

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute
                // First move to ProductionOne (establishes position at index 2)
                Move { GameId = gameId; Nation = "France"; Space = Space.ProductionOne }
                |> Execute
                ClearEvents
                ClearCommands
                // Second move: 7 spaces (ProductionOne -> Import, wrapping around)
                // Prod1(2) + 7 = 9, 9 % 8 = 1 = Import
                Move { GameId = gameId; Nation = "France"; Space = Space.Import } |> Execute ]

          expect "rejects the move" hasRejection
          expect "no action determined" (hasActionDetermined >> not)
          expect "no charge dispatched" (hasChargeCommand >> not)
      }

      spec "superseding pending paid move with another paid move voids old charge" {
          on (fun () -> createContext gameId)

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute
                // First move to ProductionOne (establishes position)
                Move { GameId = gameId; Nation = "France"; Space = Space.ProductionOne }
                |> Execute
                ClearEvents
                ClearCommands
                // Second move: 4 spaces (pending payment)
                Move { GameId = gameId; Nation = "France"; Space = Space.ProductionTwo }
                |> Execute
                ClearEvents
                ClearCommands
                // Third move: 5 spaces (supersedes, should void old charge)
                Move { GameId = gameId; Nation = "France"; Space = Space.ManeuverTwo }
                |> Execute ]

          expect "old move rejected" hasRejection
          expect "old charge voided" hasVoidCommand
          expect "new charge dispatched" (fun ctx -> chargeCount ctx = 1)
          expect "no action determined" (hasActionDetermined >> not)
      }

      spec "superseding pending paid move with free move voids charge and completes immediately" {
          on (fun () -> createContext gameId)

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute
                // First move to ManeuverOne (establishes position)
                Move { GameId = gameId; Nation = "Germany"; Space = Space.ManeuverOne }
                |> Execute
                ClearEvents
                ClearCommands
                // Second move: 5 spaces (pending payment) - ManeuverOne to Investor
                Move { GameId = gameId; Nation = "Germany"; Space = Space.Investor } |> Execute
                ClearEvents
                ClearCommands
                // Third move: 2 spaces (free, supersedes) - ManeuverOne to Factory
                Move { GameId = gameId; Nation = "Germany"; Space = Space.Factory } |> Execute ]

          expect "old move rejected" hasRejection
          expect "old charge voided" hasVoidCommand
          expect "no new charge dispatched" (fun ctx -> chargeCount ctx = 0)
          expect "action determined immediately" hasActionDetermined
      } ]

let renderSpecMarkdown options =
    SpecMarkdown.toMarkdownDocument options runner moveSpecs

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList "Rondel" [ testList "move" (moveSpecs |> List.map (toExpecto runner)) ]
