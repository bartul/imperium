namespace Imperium.Terminal.Rondel.UI

open System.Collections.ObjectModel
open Terminal.Gui
open Imperium.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Types
// ──────────────────────────────────────────────────────────────────────────

type MoveNationResult = { Nation: string; Space: Space }

// ──────────────────────────────────────────────────────────────────────────
// Move Dialog
// ──────────────────────────────────────────────────────────────────────────

module MoveDialog =

    let private spaceOptions =
        [| "Investor", Space.Investor
           "Import", Space.Import
           "Production I", Space.ProductionOne
           "Maneuver I", Space.ManeuverOne
           "Taxation", Space.Taxation
           "Factory", Space.Factory
           "Production II", Space.ProductionTwo
           "Maneuver II", Space.ManeuverTwo |]

    /// Show dialog to select nation and target space
    /// Returns None if cancelled or no valid selection
    let show (nations: string list) : MoveNationResult option =
        if List.isEmpty nations then
            let _ = MessageBox.ErrorQuery("Error", "No nations available. Start a new game first.", "OK")
            None
        else
            let mutable result: MoveNationResult option = None

            let dialog = new Dialog()
            dialog.Title <- "Move Nation"
            dialog.Width <- Dim.Absolute 50
            dialog.Height <- Dim.Absolute 16

            // Nation selection
            let nationLabel = new Label()
            nationLabel.Text <- "Select Nation:"
            nationLabel.X <- Pos.Absolute 1
            nationLabel.Y <- Pos.Absolute 1

            let nationList = new ListView()
            nationList.X <- Pos.Absolute 1
            nationList.Y <- Pos.Absolute 2
            nationList.Width <- Dim.Fill() - Dim.Absolute 2
            nationList.Height <- Dim.Absolute 4

            let nationSource = ObservableCollection<string>()
            nations |> List.iter nationSource.Add
            nationList.SetSource nationSource

            // Space selection
            let spaceLabel = new Label()
            spaceLabel.Text <- "Select Target Space:"
            spaceLabel.X <- Pos.Absolute 1
            spaceLabel.Y <- Pos.Absolute 7

            let spaceList = new ListView()
            spaceList.X <- Pos.Absolute 1
            spaceList.Y <- Pos.Absolute 8
            spaceList.Width <- Dim.Fill() - Dim.Absolute 2
            spaceList.Height <- Dim.Absolute 4

            let spaceSource = ObservableCollection<string>()
            spaceOptions |> Array.iter (fun (name, _) -> spaceSource.Add name)
            spaceList.SetSource spaceSource

            let okButton = new Button()
            okButton.Text <- "Move"

            okButton.Accepting.AddHandler(fun _ _ ->
                let nationIdx = nationList.SelectedItem
                let spaceIdx = spaceList.SelectedItem

                if nationIdx >= 0 && spaceIdx >= 0 then
                    let nation = nations.[nationIdx]
                    let _, space = spaceOptions.[spaceIdx]
                    result <- Some { Nation = nation; Space = space }

                Application.RequestStop dialog)

            let cancelButton = new Button()
            cancelButton.Text <- "Cancel"

            cancelButton.Accepting.AddHandler(fun _ _ -> Application.RequestStop dialog)

            dialog.Add(nationLabel, nationList, spaceLabel, spaceList) |> ignore
            dialog.AddButton okButton
            dialog.AddButton cancelButton

            Application.Run dialog |> ignore
            dialog.Dispose()
            result
