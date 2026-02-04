namespace Imperium.Terminal.Shell

open System
open System.Collections.ObjectModel
open Terminal.Gui

// ──────────────────────────────────────────────────────────────────────────
// Types
// ──────────────────────────────────────────────────────────────────────────

type LogEntry =
    { Timestamp: DateTime
      Category: string
      Message: string }

// ──────────────────────────────────────────────────────────────────────────
// Event Log View
// ──────────────────────────────────────────────────────────────────────────

type EventLogView() as this =
    inherit FrameView()

    let maxEntries = 100
    let entries = ResizeArray<LogEntry>()
    let logList = new ListView()
    let displayItems = ObservableCollection<string>()

    do
        this.Title <- "Log"
        logList.X <- Pos.Absolute(0)
        logList.Y <- Pos.Absolute(0)
        logList.Width <- Dim.Fill()
        logList.Height <- Dim.Fill()
        logList.SetSource(displayItems)
        this.Add(logList) |> ignore

    member private _.RefreshDisplay() =
        displayItems.Clear()

        entries
        |> Seq.rev // Most recent first
        |> Seq.iter (fun e ->
            displayItems.Add(sprintf "[%s] [%s] %s" (e.Timestamp.ToString("HH:mm:ss")) e.Category e.Message))

    /// Add a log entry (thread-safe, marshals to UI thread)
    member this.AddEntry(category: string, message: string) =
        Interop.invokeOnMainThread (fun () ->
            entries.Add(
                { Timestamp = DateTime.Now
                  Category = category
                  Message = message }
            )

            if entries.Count > maxEntries then
                entries.RemoveAt(0)

            this.RefreshDisplay())

    /// Clear all entries
    member this.Clear() =
        Interop.invokeOnMainThread (fun () ->
            entries.Clear()
            this.RefreshDisplay())
