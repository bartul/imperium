namespace Imperium.Rondel

open Imperium
open Imperium.Primitives
open FsToolkit.ErrorHandling

// ──────────────────────────────────────────────────────────────────────────
// Domain State
// ──────────────────────────────────────────────────────────────────────────

type RondelState =
    { GameId: Id; NationPositions: Map<string, Space option>; PendingMovements: Map<string, PendingMovement> }

and PendingMovement = { Nation: string; TargetSpace: Space; BillingId: RondelBillingId }

module PendingMovement =
    let create nation targetSpace billingId : PendingMovement =
        { Nation = nation; TargetSpace = targetSpace; BillingId = billingId }

    let toContract (pending: PendingMovement) : Contract.Rondel.PendingMovement =
        { Nation = pending.Nation
          TargetSpace = Space.toString pending.TargetSpace
          BillingId = pending.BillingId |> RondelBillingId.value }

    let fromContract (pending: Contract.Rondel.PendingMovement) : Result<PendingMovement, string> =
        result {
            let! space = Space.fromString pending.TargetSpace
            let! billingId = RondelBillingId.create pending.BillingId

            return create pending.Nation space billingId
        }

module RondelState =
    let private createResult gameId nations : Result<RondelState, string> =
        if Set.isEmpty nations then
            Error "Cannot create rondel state with zero nations."
        else
            Ok
                { GameId = gameId
                  NationPositions = nations |> Seq.map (fun nation -> nation, None) |> Map.ofSeq
                  PendingMovements = Map.empty }

    let private requireNationInState nation (state: RondelState) : Result<RondelState, string> =
        if state.NationPositions |> Map.containsKey nation then
            Ok state
        else
            Error $"Nation '{nation}' is not part of this rondel state."

    let private withPendingMoveResult nation targetSpace billingId (state: RondelState) : Result<RondelState, string> =
        requireNationInState nation state
        |> Result.map (fun currentState ->
            { currentState with
                PendingMovements =
                    currentState.PendingMovements
                    |> Map.add nation (PendingMovement.create nation targetSpace billingId) })

    let create gameId nations : RondelState =
        match createResult gameId nations with
        | Ok state -> state
        | Error error -> failwith error

    let withNationPosition nation position (state: RondelState) : RondelState =
        match requireNationInState nation state with
        | Ok currentState ->
            { currentState with NationPositions = currentState.NationPositions |> Map.add nation (Some position) }
        | Error error -> failwith error

    let withNationPositions positions (state: RondelState) : RondelState =
        positions
        |> Seq.fold (fun currentState (nation, position) -> currentState |> withNationPosition nation position) state

    let withPendingMove nation targetSpace billingId (state: RondelState) : RondelState =
        match withPendingMoveResult nation targetSpace billingId state with
        | Ok currentState -> currentState
        | Error error -> failwith error

    let withoutPendingMove nation (state: RondelState) : RondelState =
        match requireNationInState nation state with
        | Ok currentState -> { currentState with PendingMovements = currentState.PendingMovements |> Map.remove nation }
        | Error error -> failwith error

    let toContract (state: RondelState) : Contract.Rondel.RondelState =
        { GameId = state.GameId |> Id.value
          NationPositions =
            state.NationPositions
            |> Map.map (fun _ position -> position |> Option.map Space.toString)
          PendingMovements =
            state.PendingMovements
            |> Map.map (fun _ pending -> PendingMovement.toContract pending) }

    let fromContract (state: Contract.Rondel.RondelState) : Result<RondelState, string> =
        let nationPositions =
            result {
                let mutable positions = Map.empty

                for nation, position in state.NationPositions |> Map.toSeq do
                    match position with
                    | None -> positions <- positions |> Map.add nation None
                    | Some value ->
                        let! space = Space.fromString value
                        positions <- positions |> Map.add nation (Some space)

                return positions
            }

        let pendingMovements =
            result {
                let mutable movements = Map.empty

                for nation, pending in state.PendingMovements |> Map.toSeq do
                    if pending.Nation <> nation then
                        return! Error $"Pending movement nation mismatch for {nation}."
                    else
                        let! mapped = PendingMovement.fromContract pending
                        movements <- movements |> Map.add nation mapped

                return movements
            }

        result {
            let! gameId = Id.create state.GameId
            let! positions = nationPositions
            let! pending = pendingMovements

            let! initialState = createResult gameId (positions |> Map.keys |> Set.ofSeq)

            let stateWithPositions =
                positions
                |> Map.toSeq
                |> Seq.fold
                    (fun currentState (nation, position) ->
                        match position with
                        | None -> currentState
                        | Some space -> currentState |> withNationPosition nation space)
                    initialState

            return!
                pending
                |> Map.toSeq
                |> Seq.fold
                    (fun currentResult (nation, pendingMove) ->
                        currentResult
                        |> Result.bind (withPendingMoveResult nation pendingMove.TargetSpace pendingMove.BillingId))
                    (Ok stateWithPositions)
        }
