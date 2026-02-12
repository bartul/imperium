namespace Imperium.Terminal.Shell

open System
open System.Collections.ObjectModel
open Terminal.Gui.App
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Imperium.Terminal
open Imperium.Rondel
open Imperium.Accounting
open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Event Log View
// ──────────────────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
module EventLogView =

    let private formatRondelEvent =
        function
        | PositionedAtStart _ -> "Rondel ready, nations at starting positions"
        | ActionDetermined e -> sprintf "%s next action is determined to be %A" e.Nation e.Action
        | MoveToActionSpaceRejected e -> sprintf "%s intended move to space %A REJECTED" e.Nation e.Space

    let private formatAccountingEvent =
        function
        | RondelInvoicePaid e -> sprintf "Invoice paid (BillingId: %s)" (Id.toString e.BillingId)
        | RondelInvoicePaymentFailed e -> sprintf "Invoice payment FAILED (BillingId: %s)" (Id.toString e.BillingId)

    let private formatSystemEvent =
        function
        | AppStarted -> "Welcome to Imperium. Use Game > New Game to begin."
        | NewGameStarted gameId -> sprintf "New game started (GameId: %s)" (Id.toString gameId)
        | GameEnded -> "Game ended"

    let create (app: IApplication) (bus: IBus) =
        let maxEntries = 100
        let displayItems = ObservableCollection<string>()

        let addEntry category message =
            UI.invokeOnMainThread app (fun () ->
                let line =
                    sprintf "[%s] [%s] %s" (DateTime.Now.ToString "HH:mm:ss") category message

                displayItems.Insert(0, line)

                if displayItems.Count > maxEntries then
                    displayItems.RemoveAt(displayItems.Count - 1))

        bus.Subscribe<RondelEvent>(fun event_ -> async { formatRondelEvent event_ |> addEntry "Rondel" })

        bus.Subscribe<AccountingEvent>(fun event_ -> async { formatAccountingEvent event_ |> addEntry "Accounting" })

        bus.Subscribe<SystemEvent>(fun event_ ->
            async {
                formatSystemEvent event_ |> addEntry "System"

                match event_ with
                | GameEnded -> UI.invokeOnMainThread app (fun () -> displayItems.Clear())
                | _ -> ()
            })

        UI.frameView "Log" [| UI.listView (Dim.Fill()) (Dim.Fill()) displayItems |]
