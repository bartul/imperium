namespace Imperium.Rondel

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Queries
// ──────────────────────────────────────────────────────────────────────────

type GetNationPositionsQuery = { GameId: Id }

type GetRondelOverviewQuery = { GameId: Id }

// ──────────────────────────────────────────────────────────────────────────
// Query Results
// ──────────────────────────────────────────────────────────────────────────

type RondelPositionsView = { GameId: Id; Positions: NationPositionView list }

and NationPositionView = { Nation: string; CurrentSpace: Space option; PendingSpace: Space option }

type RondelView = { GameId: Id; NationNames: string list }

// ──────────────────────────────────────────────────────────────────────────
// Query Handlers
// ──────────────────────────────────────────────────────────────────────────

/// Internal query handlers for the Rondel bounded context.
/// Routers in the public Rondel facade delegate to these.
module internal Queries =

    let getNationPositions
        (deps: RondelQueryDependencies)
        (query: GetNationPositionsQuery)
        : Async<RondelPositionsView option> =
        let mapPosition nation position pendingMovement =
            { Nation = nation; CurrentSpace = position; PendingSpace = pendingMovement |> Option.map _.TargetSpace }

        let mapPositions currentPositions pendingMovements =
            currentPositions
            |> Map.toList
            |> List.map (fun (nation, currentPosition) ->
                mapPosition nation currentPosition (pendingMovements |> Map.tryFind nation))

        async {
            let! state = deps.Load query.GameId

            return
                state
                |> Option.map (fun s ->
                    { GameId = query.GameId; Positions = mapPositions s.NationPositions s.PendingMovements })
        }

    let getRondelOverview (deps: RondelQueryDependencies) (query: GetRondelOverviewQuery) : Async<RondelView option> =
        async {
            let! state = deps.Load query.GameId

            return
                state
                |> Option.map (fun s ->
                    { GameId = query.GameId; NationNames = s.NationPositions |> Map.toList |> List.map fst })
        }
