namespace Imperium.Terminal.Shell

open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
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

    type AppState = { mutable CurrentGameId: Id option; mutable NationNames: string list }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────────────────────
    // Main Application
    // ──────────────────────────────────────────────────────────────────────────

    let create (app: IApplication) (rondelHost: RondelHost) (_accountingHost: AccountingHost) (bus: IBus) =
        let state = { CurrentGameId = None; NationNames = [] }

        // Create views - positioned below menu bar (Y=1)
        let statusView =
            new RondelStatusView(app, rondelHost, fun () -> state.CurrentGameId)

        statusView.X <- Pos.Absolute 0
        statusView.Y <- Pos.Absolute 1 // Below menu
        statusView.Width <- Dim.Fill()
        statusView.Height <- Dim.Percent 50
        statusView.CanFocus <- true
        statusView.TabStop <- TabBehavior.TabGroup

        let eventLogView = EventLogView.create app bus
        eventLogView.X <- Pos.Absolute 0
        eventLogView.Y <- Pos.Bottom statusView
        eventLogView.Width <- Dim.Fill()
        eventLogView.Height <- Dim.Fill()
        eventLogView.CanFocus <- true
        eventLogView.TabStop <- TabBehavior.TabGroup

        // Menu handlers
        let handleNewGame () =
            let nationsStr = String.concat ", " defaultNations

            let result =
                MessageBox.Query(
                    app,
                    "New Game",
                    sprintf "Start new game with 6 nations?\n\n%s" nationsStr,
                    "Yes",
                    "No"
                )

            if result.HasValue && result.Value = 0 then
                let gameId = Id.newId ()
                state.CurrentGameId <- Some gameId
                state.NationNames <- defaultNations |> Set.toList |> List.sort

                async {
                    do!
                        SetToStartingPositions { GameId = gameId; Nations = defaultNations }
                        |> rondelHost.Execute

                    do! bus.Publish NewGameStarted
                    UI.invokeOnMainThread app (fun () -> statusView.Refresh())
                }
                |> Async.Start

        let handleMoveNation () =
            match state.CurrentGameId with
            | None ->
                MessageBox.ErrorQuery(app, "Error", "No game initialized. Start a new game first.", "OK")
                |> ignore
            | Some gameId ->
                match MoveDialog.show app state.NationNames with
                | None -> ()
                | Some result ->
                    async {
                        do!
                            Move { GameId = gameId; Nation = result.Nation; Space = result.Space }
                            |> rondelHost.Execute

                        UI.invokeOnMainThread app (fun () -> statusView.Refresh())
                    }
                    |> Async.Start

        let handleEndGame () =
            match state.CurrentGameId with
            | None -> MessageBox.ErrorQuery(app, "Error", "No game in progress.", "OK") |> ignore
            | Some _ ->
                let result = MessageBox.Query(app, "End Game", "End the current game?", "Yes", "No")

                if result.HasValue && result.Value = 0 then
                    state.CurrentGameId <- None
                    state.NationNames <- []
                    bus.Publish GameEnded |> Async.RunSynchronously

        let handleQuit () = app.RequestStop()

        let handleRefresh () = statusView.Refresh()

        // Create menu bar
        let menu =
            UI.menuBar
                [ "_Game",
                  [ "_New Game", handleNewGame
                    "_Move Nation", handleMoveNation
                    "_End Game", handleEndGame
                    "_Quit", handleQuit ]
                  "_View", [ "_Refresh", handleRefresh ] ]

        // Refresh status view on domain events
        bus.Subscribe<RondelEvent>(fun _ -> async { UI.invokeOnMainThread app (fun () -> statusView.Refresh()) })

        bus.Subscribe<AccountingEvent>(fun _ -> async { UI.invokeOnMainThread app (fun () -> statusView.Refresh()) })

        bus.Subscribe<SystemEvent>(fun _ -> async { UI.invokeOnMainThread app (fun () -> statusView.Refresh()) })

        // StatusBar with keyboard shortcuts
        // Note: Ctrl+M is Enter in terminals (both 0x0D), so use F2 for Move
        // F6/Shift+F6 switches between panels (TabGroup default)
        let statusBar = new StatusBar()

        statusBar.Add(
            UI.shortcut (Key.Q.WithCtrl) "Quit" handleQuit,
            UI.shortcut (Key.N.WithCtrl) "New Game" handleNewGame,
            UI.shortcut Key.F2 "Move" handleMoveNation,
            UI.shortcut Key.F5 "Refresh" handleRefresh
        )
        |> ignore

        // Initial log entry
        bus.Publish AppStarted |> Async.RunSynchronously

        // Assemble top-level
        let top = new Window()
        top.Add(menu, statusView, eventLogView, statusBar) |> ignore
        top

    /// Run the application
    let run (rondelHost: RondelHost) (accountingHost: AccountingHost) (bus: IBus) : unit =
        use app = (Application.Create()).Init()
        let top = create app rondelHost accountingHost bus
        app.Run top |> ignore
        top.Dispose()
        app.Dispose()
