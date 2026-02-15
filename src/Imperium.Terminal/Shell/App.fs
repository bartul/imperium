namespace Imperium.Terminal.Shell

open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Imperium.Primitives
open Imperium.Rondel
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
        let rondelView = RondelView.create app bus rondelHost
        rondelView.X <- Pos.Absolute 0
        rondelView.Y <- Pos.Absolute 1 // Below menu
        rondelView.Width <- Dim.Fill()
        rondelView.Height <- Dim.Percent 50
        rondelView.CanFocus <- true
        rondelView.TabStop <- TabBehavior.TabGroup

        let eventLogView = EventLogView.create app bus
        eventLogView.X <- Pos.Absolute 0
        eventLogView.Y <- Pos.Bottom rondelView
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

                    do! bus.Publish(NewGameStarted gameId)
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

        // Create menu bar with "Game" and "Move" as root-level menus
        let mkMenuItem label handler =
            let item = new MenuItem()
            item.Title <- label
            item.Action <- System.Action handler
            item :> View

        let gameMenu =
            new MenuBarItem(
                "_Game",
                [| mkMenuItem "_New Game" handleNewGame
                   mkMenuItem "_End Game" handleEndGame
                   mkMenuItem "_Quit" handleQuit |]
            )

        let moveMenuItems =
            defaultNations
            |> Set.toList
            |> List.sort
            |> List.map (fun nation ->
                mkMenuItem nation (fun () -> async { do! bus.Publish(MoveNationRequested nation) } |> Async.Start))
            |> List.toArray

        let moveMenu = new MenuBarItem("_Move", moveMenuItems)

        let menu = new MenuBar()
        menu.Menus <- [| gameMenu; moveMenu |]

        // StatusBar with keyboard shortcuts
        // F6/Shift+F6 switches between panels (TabGroup default)
        let statusBar = new StatusBar()

        statusBar.Add(
            UI.shortcut Key.N.WithCtrl "New Game" handleNewGame,
            UI.shortcut Key.Q.WithCtrl "Quit" handleQuit
        )
        |> ignore

        // Initial log entry
        bus.Publish AppStarted |> Async.RunSynchronously

        // Assemble top-level
        let top = new Window()
        top.Add(menu, rondelView, eventLogView, statusBar) |> ignore
        top

    /// Run the application
    let run (rondelHost: RondelHost) (accountingHost: AccountingHost) (bus: IBus) : unit =
        use app = (Application.Create()).Init()
        let top = create app rondelHost accountingHost bus
        app.Run top |> ignore
        top.Dispose()
        app.Dispose()
