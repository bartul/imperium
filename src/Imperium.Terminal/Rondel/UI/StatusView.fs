namespace Imperium.Terminal.Rondel.UI

open System.Collections.ObjectModel
open Terminal.Gui
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Terminal.Rondel
open Imperium.Terminal.Shell

// ──────────────────────────────────────────────────────────────────────────
// Rondel Status View
// ──────────────────────────────────────────────────────────────────────────

type RondelStatusView(rondelHost: RondelHost, getCurrentGameId: unit -> Id option) as this =
    inherit FrameView()

    let positionsList = new ListView()
    let displayItems = ObservableCollection<string>()

    let summaryLabel = new Label()

    do
        this.Title <- "Rondel"

        positionsList.X <- Pos.Absolute(0)
        positionsList.Y <- Pos.Absolute(0)
        positionsList.Width <- Dim.Fill()
        positionsList.Height <- Dim.Fill() - Dim.Absolute(1)
        positionsList.SetSource(displayItems)

        summaryLabel.X <- Pos.Absolute(0)
        summaryLabel.Y <- Pos.AnchorEnd(1)
        summaryLabel.Width <- Dim.Fill()
        summaryLabel.Text <- "No game initialized"

        this.Add(positionsList, summaryLabel) |> ignore

    let formatSpace (space: Space option) =
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

    let formatPosition (p: NationPositionView) =
        let current = formatSpace p.CurrentSpace

        let pending =
            match p.PendingSpace with
            | Some s -> sprintf " -> %s (pending)" (formatSpace (Some s))
            | None -> ""

        sprintf "  %s: %s%s" p.Nation current pending

    /// Refresh the display with current positions (thread-safe)
    member _.Refresh() =
        match getCurrentGameId () with
        | None ->
            Interop.invokeOnMainThread (fun () ->
                displayItems.Clear()
                displayItems.Add("No game initialized")
                summaryLabel.Text <- "")
        | Some gameId ->
            async {
                let! result = rondelHost.QueryPositions { GameId = gameId }

                Interop.invokeOnMainThread (fun () ->
                    displayItems.Clear()

                    match result with
                    | None ->
                        displayItems.Add("Game not found")
                        summaryLabel.Text <- ""
                    | Some view ->
                        view.Positions |> List.iter (fun p -> displayItems.Add(formatPosition p))

                        let pendingCount =
                            view.Positions |> List.filter (fun p -> p.PendingSpace.IsSome) |> List.length

                        let totalNations = view.Positions.Length
                        summaryLabel.Text <- sprintf "Nations: %d | Pending: %d" totalNations pendingCount)
            }
            |> Async.Start
