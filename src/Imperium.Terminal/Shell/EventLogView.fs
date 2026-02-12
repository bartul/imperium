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
        | PositionedAtStart _ -> "Rondel initialized - nations at starting positions"
        | ActionDetermined e -> sprintf "%s moved to %A" e.Nation e.Action
        | MoveToActionSpaceRejected e -> sprintf "%s move to %A REJECTED" e.Nation e.Space

    let private formatAccountingEvent =
        function
        | RondelInvoicePaid e -> sprintf "Payment confirmed (BillingId: %s)" (Id.toString e.BillingId)
        | RondelInvoicePaymentFailed e -> sprintf "Payment FAILED (BillingId: %s)" (Id.toString e.BillingId)

    let private formatSystemEvent =
        function
        | AppStarted -> "Imperium started. Use Game > New Game to begin."
        | NewGameStarted -> "New game started"
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
