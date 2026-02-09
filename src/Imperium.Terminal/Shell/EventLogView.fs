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

module private EventLogView =

    let formatRondelEvent =
        function
        | PositionedAtStart _ -> "Rondel initialized - nations at starting positions"
        | ActionDetermined e -> sprintf "%s moved to %A" e.Nation e.Action
        | MoveToActionSpaceRejected e -> sprintf "%s move to %A REJECTED" e.Nation e.Space

    let formatAccountingEvent =
        function
        | RondelInvoicePaid e -> sprintf "Payment confirmed (BillingId: %s)" (Id.toString e.BillingId)
        | RondelInvoicePaymentFailed e -> sprintf "Payment FAILED (BillingId: %s)" (Id.toString e.BillingId)

type EventLogView(app: IApplication, bus: IBus) as this =
    inherit FrameView()

    let maxEntries = 100
    let logList = new ListView()
    let displayItems = ObservableCollection<string>()

    let addEntry category message =
        UI.invokeOnMainThread app (fun () ->
            let line =
                sprintf "[%s] [%s] %s" (DateTime.Now.ToString "HH:mm:ss") category message

            displayItems.Insert(0, line)

            if displayItems.Count > maxEntries then
                displayItems.RemoveAt(displayItems.Count - 1))

    do
        this.Title <- "Log"
        logList.X <- Pos.Absolute 0
        logList.Y <- Pos.Absolute 0
        logList.Width <- Dim.Fill()
        logList.Height <- Dim.Fill()
        logList.SetSource displayItems
        this.Add logList |> ignore

        bus.Subscribe<RondelEvent>(fun event_ -> async { EventLogView.formatRondelEvent event_ |> addEntry "Rondel" })

        bus.Subscribe<AccountingEvent>(fun event_ ->
            async { EventLogView.formatAccountingEvent event_ |> addEntry "Accounting" })

    /// Add a log entry (thread-safe, marshals to UI thread)
    member _.AddEntry(category: string, message: string) = addEntry category message

    /// Clear all entries
    member _.Clear() =
        UI.invokeOnMainThread app (fun () -> displayItems.Clear())
