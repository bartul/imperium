namespace Imperium.Terminal.Rondel.UI

open System.Collections.ObjectModel
open Terminal.Gui.App
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Accounting
open Imperium.Terminal
open Imperium.Terminal.Rondel
open Imperium.Terminal.Shell

// ──────────────────────────────────────────────────────────────────────────
// Rondel Status View
// ──────────────────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
module RondelView =

    let private formatSpace (space: Space option) =
        match space with
        | None -> "(start)"
        | Some s ->
            match s with
            | Space.Investor -> "Investor"
            | Space.Import -> "Import"
            | Space.ProductionOne -> "Production I"
            | Space.ManeuverOne -> "Maneuver I"
            | Space.Taxation -> "Taxation"
            | Space.Factory -> "Factory"
            | Space.ProductionTwo -> "Production II"
            | Space.ManeuverTwo -> "Maneuver II"

    let private formatPosition (p: NationPositionView) =
        let current = formatSpace p.CurrentSpace

        let pending =
            match p.PendingSpace with
            | Some s -> sprintf " -> %s (pending)" (formatSpace (Some s))
            | None -> ""

        sprintf "  %s: %s%s" p.Nation current pending

    let create (app: IApplication) (bus: IBus) (rondelHost: RondelHost) =
        let mutable currentGameId: Id option = None
        let displayItems = ObservableCollection<string>()

        let summaryLabel = UI.label "No game initialized"
        summaryLabel.X <- Pos.Absolute 0
        summaryLabel.Y <- Pos.AnchorEnd 1
        summaryLabel.Width <- Dim.Fill()

        let refresh () =
            match currentGameId with
            | None ->
                UI.invokeOnMainThread app (fun () ->
                    displayItems.Clear()
                    displayItems.Add "No game initialized"
                    summaryLabel.Text <- "")
            | Some gameId ->
                async {
                    let! result = rondelHost.QueryPositions { GameId = gameId }

                    UI.invokeOnMainThread app (fun () ->
                        displayItems.Clear()

                        match result with
                        | None ->
                            displayItems.Add "Game not found"
                            summaryLabel.Text <- ""
                        | Some view ->
                            view.Positions |> List.iter (fun p -> displayItems.Add(formatPosition p))

                            let pendingCount =
                                view.Positions |> List.filter (fun p -> p.PendingSpace.IsSome) |> List.length

                            summaryLabel.Text <- sprintf "Nations: %d | Pending: %d" view.Positions.Length pendingCount)
                }
                |> Async.Start

        bus.Subscribe<RondelEvent>(fun _ -> async { refresh () })
        bus.Subscribe<AccountingEvent>(fun _ -> async { refresh () })

        bus.Subscribe<SystemEvent>(fun event_ ->
            async {
                match event_ with
                | NewGameStarted gameId -> currentGameId <- Some gameId
                | GameEnded -> currentGameId <- None
                | AppStarted -> ()
                | MoveNationRequested _ -> ()

                refresh ()
            })

        UI.frameView
            "Rondel"
            [| UI.listView (Dim.Fill()) (Dim.Fill() - Dim.Absolute 1) displayItems
               summaryLabel |]
