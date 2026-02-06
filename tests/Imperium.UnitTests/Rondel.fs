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

let private createContext () =
    let store = Dictionary<Id, RondelState>()
    let events = ResizeArray<RondelEvent>()
    let commands = ResizeArray<RondelOutboundCommand>()
    let gameId = Guid.NewGuid() |> Id

    let load (id: Id) : Async<RondelState option> =
        async {
            return
                match store.TryGetValue(id) with
                | true, state -> Some state
                | false, _ -> None
        }

    let save (state: RondelState) : Async<Result<unit, string>> =
        async {
            store.[state.GameId] <- state
            return Ok()
        }

    let publish (event: RondelEvent) : Async<unit> = async { events.Add event }

    let dispatch (command: RondelOutboundCommand) : Async<Result<unit, string>> =
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

let private runner: ISpecRunner<RondelContext, RondelState option, RondelCommand, RondelInboundEvent> =
    { new ISpecRunner<RondelContext, RondelState option, RondelCommand, RondelInboundEvent> with
        member _.Execute ctx cmd =
            execute ctx.Deps cmd |> Async.RunSynchronously

        member _.Handle ctx evt =
            handle ctx.Deps evt |> Async.RunSynchronously

        member _.ClearEvents ctx = ctx.Events.Clear()
        member _.ClearCommands ctx = ctx.Commands.Clear()

        member _.CaptureState ctx =
            match ctx.Store.TryGetValue(ctx.GameId) with
            | true, state -> Some state
            | false, _ -> None }

// ────────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────────

let private hasEvent (predicate: RondelEvent -> bool) (ctx: RondelContext) = ctx.Events |> Seq.exists predicate

let private hasActionDetermined (ctx: RondelContext) =
    hasEvent
        (function
        | ActionDetermined _ -> true
        | _ -> false)
        ctx

let private hasRejection (ctx: RondelContext) =
    hasEvent
        (function
        | MoveToActionSpaceRejected _ -> true
        | _ -> false)
        ctx

let private hasChargeCommand (ctx: RondelContext) =
    ctx.Commands
    |> Seq.exists (function
        | ChargeMovement _ -> true
        | _ -> false)

let private hasVoidCommand (ctx: RondelContext) =
    ctx.Commands
    |> Seq.exists (function
        | VoidCharge _ -> true
        | _ -> false)

let private chargeCount (ctx: RondelContext) =
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

    [ spec "move cannot begin before starting positions are chosen" {
          on createContext

          when_ [ Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute ]

          expect "rejects the move" hasRejection
          expect "no action determined" (hasActionDetermined >> not)
          expect "no charge dispatched" (hasChargeCommand >> not)
      }

      spec "first move to any space is free" {
          on createContext

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute
                ClearEvents
                Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute ]

          expect "action is determined" hasActionDetermined
          expect "no charge dispatched" (hasChargeCommand >> not)
      }

      spec "rejects move to current position (stay put)" {
          on createContext

          when_
              [ SetToStartingPositions { GameId = gameId; Nations = nations } |> Execute
                Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute
                ClearEvents
                ClearCommands
                Move { GameId = gameId; Nation = "France"; Space = Space.Factory } |> Execute ]

          expect "rejects the move" hasRejection
          expect "no action determined" (hasActionDetermined >> not)
          expect "no charge dispatched" (hasChargeCommand >> not)
      }

      spec "move of 1-3 spaces is free" {
          on createContext

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
          on createContext

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
          on createContext

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
          on createContext

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
          on createContext

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
          on createContext

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
          on createContext

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

// ────────────────────────────────────────────────────────────────────────────────
// Test Registration
// ────────────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList "Rondel" [ testList "move" (moveSpecs |> List.map (toExpecto runner)) ]
