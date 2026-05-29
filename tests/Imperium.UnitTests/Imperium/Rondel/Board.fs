[<RequireQualifiedAccess>]
module Imperium.UnitTests.Rondel.Board

open Imperium.Rondel

// ────────────────────────────────────────────────────────────────────────────────
// Board Rendering
// ────────────────────────────────────────────────────────────────────────────────

type private BoardCell =
    | SpaceCell of Space
    | StartCell

type private CellToken = { Nation: string; Text: string }

let private abbreviateNation =
    function
    | "Austria" -> "AH"
    | "Austria-Hungary" -> "AH"
    | "France" -> "FR"
    | "Britain" -> "GB"
    | "Germany" -> "GE"
    | "Great Britain" -> "GB"
    | "Italy" -> "IT"
    | "Russia" -> "RU"
    | name when name.Length >= 2 -> name[..1].ToUpperInvariant()
    | name -> name.ToUpperInvariant()

let private boardCellName =
    function
    | SpaceCell Space.Investor -> "Investor"
    | SpaceCell Space.Import -> "Import"
    | SpaceCell Space.ProductionOne
    | SpaceCell Space.ProductionTwo -> "Production"
    | SpaceCell Space.ManeuverOne
    | SpaceCell Space.ManeuverTwo -> "Maneuver"
    | SpaceCell Space.Taxation -> "Taxation"
    | SpaceCell Space.Factory -> "Factory"
    | StartCell -> "↻"

let private boardCellForPosition =
    function
    | Some space -> SpaceCell space
    | None -> StartCell

let private boardRows =
    [ [ SpaceCell Space.ManeuverTwo
        SpaceCell Space.Investor
        SpaceCell Space.Import ]
      [ SpaceCell Space.ProductionTwo; StartCell; SpaceCell Space.ProductionOne ]
      [ SpaceCell Space.Factory
        SpaceCell Space.Taxation
        SpaceCell Space.ManeuverOne ] ]

let private addToken cell token tokensByCell =
    let existing = tokensByCell |> Map.tryFind cell |> Option.defaultValue []
    tokensByCell |> Map.add cell (token :: existing)

let private cellContent tokensByCell cell =
    tokensByCell
    |> Map.tryFind cell
    |> Option.defaultValue []
    |> List.sortBy (fun token -> token.Nation)
    |> List.map (fun token -> token.Text)
    |> String.concat " "

let private center width (text: string) =
    let padding = max 0 (width - text.Length)
    let left = padding / 2
    let right = padding - left
    String.replicate left " " + text + String.replicate right " "

let private renderBoardRow width cells tokensByCell =
    let renderLine renderCell =
        cells |> List.map renderCell |> String.concat "|" |> (fun line -> $"|{line}|")

    let titleLine = renderLine (fun cell -> $" {center width (boardCellName cell)} ")

    let contentLine =
        renderLine (fun cell -> $" {center width (cellContent tokensByCell cell)} ")

    [ titleLine; contentLine ]

let render (state: RondelState option) : string =
    match state with
    | None -> "No rondel state"
    | Some state ->
        let tokensByCell =
            state.NationPositions
            |> Map.toList
            |> List.fold
                (fun currentTokens (nation, currentSpace) ->
                    let abbreviation = abbreviateNation nation
                    let originCell = boardCellForPosition currentSpace

                    match state.PendingMovements |> Map.tryFind nation with
                    | Some pending ->
                        currentTokens
                        |> addToken originCell { Nation = nation; Text = $"{abbreviation}->" }
                        |> addToken (SpaceCell pending.TargetSpace) { Nation = nation; Text = $"->{abbreviation}" }
                    | None -> currentTokens |> addToken originCell { Nation = nation; Text = abbreviation })
                Map.empty

        let cellWidth =
            boardRows
            |> List.collect id
            |> List.collect (fun cell -> [ boardCellName cell; cellContent tokensByCell cell ])
            |> List.map String.length
            |> List.max
            |> max 12

        let border =
            [ 1..3 ]
            |> List.map (fun _ -> String.replicate (cellWidth + 2) "-")
            |> String.concat "+"
            |> fun line -> $"+{line}+"

        let boardLines =
            boardRows
            |> List.collect (fun row -> border :: renderBoardRow cellWidth row tokensByCell)

        String.concat "\n" (boardLines @ [ border ])
