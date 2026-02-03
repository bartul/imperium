namespace Imperium.Terminal.Shell

open Terminal.Gui
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Accounting
open Imperium.Terminal
open Imperium.Terminal.Rondel
open Imperium.Terminal.Rondel.UI
open Imperium.Terminal.Accounting

// ──────────────────────────────────────────────────────────────────────────
// Constants
// ──────────────────────────────────────────────────────────────────────────

module App =

    let defaultNations =
        set [ "Austria-Hungary"; "Great Britain"; "Russia"; "Italy"; "Germany"; "France" ]

    // ──────────────────────────────────────────────────────────────────────────
    // Types
    // ──────────────────────────────────────────────────────────────────────────

    type AppState =
        { mutable CurrentGameId: Id option
          mutable NationNames: string list }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    let private formatRondelEvent (evt: RondelEvent) =
        match evt with
        | PositionedAtStart _ -> "Game initialized - nations at starting positions"
        | ActionDetermined e -> sprintf "%s moved to %A" e.Nation e.Action
        | MoveToActionSpaceRejected e -> sprintf "%s move to %A REJECTED" e.Nation e.Space

    // ──────────────────────────────────────────────────────────────────────────
    // Main Application
    // ──────────────────────────────────────────────────────────────────────────

    let create (rondelHost: RondelHost) (_accountingHost: AccountingHost) (bus: IBus) =
        let state =
            { CurrentGameId = None
              NationNames = [] }

        // Create the top-level window
        let window = new Window()
        window.Title <- "Imperium Terminal"
        window.X <- Pos.Absolute 0
        window.Y <- Pos.Absolute 1 // Leave room for menu
        window.Width <- Dim.Fill()
        window.Height <- Dim.Fill()

        // Create views
        let statusView = new RondelStatusView(rondelHost, fun () -> state.CurrentGameId)
        statusView.X <- Pos.Absolute 0
        statusView.Y <- Pos.Absolute 0
        statusView.Width <- Dim.Fill()
        statusView.Height <- Dim.Percent 50

        let eventLogView = new EventLogView()
        eventLogView.X <- Pos.Absolute 0
        eventLogView.Y <- Pos.Bottom statusView
        eventLogView.Width <- Dim.Fill()
        eventLogView.Height <- Dim.Fill()

        window.Add(statusView, eventLogView) |> ignore

        // Menu handlers
        let handleNewGame () =
            let nationsStr = String.concat ", " defaultNations

            let result =
                MessageBox.Query("New Game", sprintf "Start new game with 6 nations?\n\n%s" nationsStr, "Yes", "No")

            if result = 0 then
                let gameId = Id.newId ()
                state.CurrentGameId <- Some gameId
                state.NationNames <- defaultNations |> Set.toList |> List.sort

                async {
                    do! SetToStartingPositions { GameId = gameId; Nations = defaultNations } |> rondelHost.Execute

                    Interop.invokeOnMainThread (fun () ->
                        statusView.Refresh()
                        eventLogView.AddEntry("System", "New game started"))
                }
                |> Async.Start

        let handleMoveNation () =
            match state.CurrentGameId with
            | None ->
                let _ = MessageBox.ErrorQuery("Error", "No game initialized. Start a new game first.", "OK")
                ()
            | Some gameId ->
                match MoveDialog.show state.NationNames with
                | None -> ()
                | Some result ->
                    async {
                        do! Move { GameId = gameId; Nation = result.Nation; Space = result.Space } |> rondelHost.Execute
                        Interop.invokeOnMainThread (fun () -> statusView.Refresh())
                    }
                    |> Async.Start

        let handleQuit () = Application.RequestStop()

        let handleRefresh () = statusView.Refresh()

        // Create menu bar
        let menu =
            Interop.menuBar
                [ "_Game",
                  [ "_New Game", handleNewGame
                    "_Move Nation", handleMoveNation
                    "_Quit", handleQuit ]
                  "_View", [ "_Refresh", handleRefresh ] ]

        // Subscribe to Rondel events for UI updates
        bus.Subscribe<RondelEvent>(fun evt ->
            async {
                Interop.invokeOnMainThread (fun () ->
                    eventLogView.AddEntry("Rondel", formatRondelEvent evt)
                    statusView.Refresh())
            })

        // Subscribe to Accounting events for UI updates
        bus.Subscribe<RondelInvoicePaidEvent>(fun evt ->
            async {
                Interop.invokeOnMainThread (fun () ->
                    eventLogView.AddEntry("Accounting", sprintf "Payment confirmed (BillingId: %s)" (Id.toString evt.BillingId))
                    statusView.Refresh())
            })

        bus.Subscribe<RondelInvoicePaymentFailedEvent>(fun evt ->
            async {
                Interop.invokeOnMainThread (fun () ->
                    eventLogView.AddEntry("Accounting", sprintf "Payment FAILED (BillingId: %s)" (Id.toString evt.BillingId))
                    statusView.Refresh())
            })

        // Initial log entry
        eventLogView.AddEntry("System", "Imperium Terminal started. Use Game > New Game to begin.")

        // Return top-level with menu and window
        let top = Application.Top
        top.Add(menu, window) |> ignore
        top

    /// Run the application
    let run (rondelHost: RondelHost) (accountingHost: AccountingHost) (bus: IBus) : unit =
        Application.Init()

        try
            create rondelHost accountingHost bus |> ignore
            Application.Run() |> ignore
        finally
            Application.Shutdown()
