namespace Imperium.Terminal.Rondel.UI

open System.Drawing
open Terminal.Gui.App
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Accounting
open Imperium.Terminal
open Imperium.Terminal.Rondel
open Imperium.Terminal.Shell

// ──────────────────────────────────────────────────────────────────────────
// Constants
// ──────────────────────────────────────────────────────────────────────────

module private RondelLayout =

    /// Create an Attribute from two color name strings.
    /// Needed because Terminal.Gui uses inref params that F# can't pass directly.
    let mkAttr (fg: string) (bg: string) =
        let mutable fgColor = Color(fg)
        let mutable bgColor = Color(bg)
        Attribute(&fgColor, &bgColor)

    /// The 8 rondel spaces in clockwise order (index 0..7).
    let spaces =
        [| Space.Investor
           Space.Import
           Space.ProductionOne
           Space.ManeuverOne
           Space.Taxation
           Space.Factory
           Space.ProductionTwo
           Space.ManeuverTwo |]

    /// Display name for each space.
    let spaceName =
        function
        | Space.Investor -> "Investor"
        | Space.Import -> "Import"
        | Space.ProductionOne
        | Space.ProductionTwo -> "Production"
        | Space.ManeuverOne
        | Space.ManeuverTwo -> "Maneuver"
        | Space.Taxation -> "Taxation"
        | Space.Factory -> "Factory"

    /// Color attribute for each space (foreground on colored background).
    let spaceAttr =
        function
        | Space.Investor -> mkAttr "Black" "Cyan"
        | Space.Import -> mkAttr "Black" "Yellow"
        | Space.ProductionOne
        | Space.ProductionTwo -> mkAttr "White" "Gray"
        | Space.ManeuverOne
        | Space.ManeuverTwo -> mkAttr "Black" "Green"
        | Space.Taxation -> mkAttr "Black" "BrightYellow"
        | Space.Factory -> mkAttr "White" "Blue"

    /// Selected cell attribute: inverted foreground/background.
    let selectedAttr =
        function
        | Space.Investor -> mkAttr "Cyan" "Black"
        | Space.Import -> mkAttr "Yellow" "Black"
        | Space.ProductionOne
        | Space.ProductionTwo -> mkAttr "Gray" "Black"
        | Space.ManeuverOne
        | Space.ManeuverTwo -> mkAttr "Green" "Black"
        | Space.Taxation -> mkAttr "BrightYellow" "Black"
        | Space.Factory -> mkAttr "Blue" "Black"

    /// Map clockwise index (0..7) to grid position (row, col) in a 3×3 grid.
    /// Center cell (1,1) is reserved for start-position nations.
    let gridPosition =
        function
        | 0 -> 0, 1 // Investor:  top center
        | 1 -> 0, 2 // Import:    top right
        | 2 -> 1, 2 // Prod I:    middle right
        | 3 -> 2, 2 // Man I:     bottom right
        | 4 -> 2, 1 // Taxation:  bottom center
        | 5 -> 2, 0 // Factory:   bottom left
        | 6 -> 1, 0 // Prod II:   middle left
        | 7 -> 0, 0 // Man II:    top left
        | i -> failwithf "Invalid rondel index: %d" i

    /// Find the clockwise index of a space.
    let indexOf space = spaces |> Array.findIndex ((=) space)

    /// Next space clockwise.
    let nextSpace space = spaces.[(indexOf space + 1) % 8]

    /// Previous space (counter-clockwise).
    let prevSpace space = spaces.[(indexOf space + 7) % 8]

    /// Map grid position back to clockwise index, if it's a space cell.
    let indexFromGrid row col =
        match row, col with
        | 0, 1 -> Some 0
        | 0, 2 -> Some 1
        | 1, 2 -> Some 2
        | 2, 2 -> Some 3
        | 2, 1 -> Some 4
        | 2, 0 -> Some 5
        | 1, 0 -> Some 6
        | 0, 0 -> Some 7
        | _ -> None

    /// Compute the display column width of a string in the terminal.
    /// Flag emojis (regional indicator pairs) render as 2 columns each.
    let displayWidth (s: string) =
        let mutable cols = 0
        let mutable i = 0

        while i < s.Length do
            if
                System.Char.IsHighSurrogate(s.[i])
                && i + 1 < s.Length
                && System.Char.IsLowSurrogate(s.[i + 1])
            then
                let cp = System.Char.ConvertToUtf32(s.[i], s.[i + 1])

                if cp >= 0x1F1E6 && cp <= 0x1F1FF then
                    // Regional indicator symbol — part of a flag pair, 2 cols per pair
                    // Check if next surrogate pair is also a regional indicator
                    if
                        i + 3 < s.Length
                        && System.Char.IsHighSurrogate(s.[i + 2])
                        && System.Char.IsLowSurrogate(s.[i + 3])
                    then
                        let cp2 = System.Char.ConvertToUtf32(s.[i + 2], s.[i + 3])

                        if cp2 >= 0x1F1E6 && cp2 <= 0x1F1FF then
                            cols <- cols + 2
                            i <- i + 4
                        else
                            cols <- cols + 2
                            i <- i + 2
                    else
                        cols <- cols + 2
                        i <- i + 2
                else
                    // Other supplementary character — assume 2 columns wide
                    cols <- cols + 2
                    i <- i + 2
            else
                cols <- cols + 1
                i <- i + 1

        cols

    /// Abbreviate nation name to 2 characters.
    let abbreviate =
        function
        | "Austria-Hungary" -> "🇦🇹AH"
        | "France" -> "🇫🇷FR"
        | "Germany" -> "🇩🇪GE"
        | "Great Britain" -> "🇬🇧GB"
        | "Italy" -> "🇮🇹IT"
        | "Russia" -> "🇷🇺RU"
        | name when name.Length >= 2 -> name.[..1]
        | name -> name

// ──────────────────────────────────────────────────────────────────────────
// State
// ──────────────────────────────────────────────────────────────────────────

type private SelectionMode = { Nation: string; Space: Space }

type private RondelViewState =
    { mutable CurrentGame: Id option
      mutable Selection: SelectionMode option
      mutable Positions: NationPositionView list option }

// ──────────────────────────────────────────────────────────────────────────
// RondelCanvas — custom drawn view
// ──────────────────────────────────────────────────────────────────────────

type private RondelCanvas(state: RondelViewState, onSpaceSelected: Space -> unit) =
    inherit View()

    let state = state

    /// Compute the pixel rectangle for a grid cell (row 0..2, col 0..2).
    let cellRect (viewport: Rectangle) gridRow gridCol =
        let cellW = viewport.Width / 3
        let cellH = viewport.Height / 3
        let x = gridCol * cellW
        let y = gridRow * cellH
        // Last column/row absorbs remainder pixels
        let w = if gridCol = 2 then viewport.Width - x else cellW

        let h = if gridRow = 2 then viewport.Height - y else cellH

        Rectangle(x, y, w, h)

    /// Find which space index (0..7) a viewport point falls into.
    let indexFromPoint (viewport: Rectangle) px py =
        let cellW = viewport.Width / 3
        let cellH = viewport.Height / 3

        if cellW <= 0 || cellH <= 0 then
            None
        else
            let gridCol = min (px / cellW) 2
            let gridRow = min (py / cellH) 2
            RondelLayout.indexFromGrid gridRow gridCol

    /// Get nations currently on a given space.
    let nationsOnSpace (space: Space) =
        state.Positions
        |> Option.defaultValue []
        |> List.filter (fun p -> p.CurrentSpace = Some space)

    /// Get nations at start (no current space).
    let nationsAtStart () =
        state.Positions
        |> Option.defaultValue []
        |> List.filter (fun p -> p.CurrentSpace.IsNone)

    /// Draw a centered string within a rectangle at a given row offset.
    let drawCentered (this: View) (rect: Rectangle) rowOffset (text: string) =
        let w = RondelLayout.displayWidth text

        let x = rect.X + max 0 ((rect.Width - w) / 2)
        let y = rect.Y + rowOffset

        if y < rect.Y + rect.Height then
            this.Move(x, y) |> ignore
            this.AddStr(text)

    /// Draw a single space cell.
    let drawSpaceCell (this: View) (viewport: Rectangle) (index: int) =
        let space = RondelLayout.spaces.[index]
        let gridRow, gridCol = RondelLayout.gridPosition index
        let rect = cellRect viewport gridRow gridCol

        let isSelected = state.Selection |> Option.exists (fun s -> s.Space = space)

        let attr =
            if isSelected then
                RondelLayout.selectedAttr space
            else
                RondelLayout.spaceAttr space

        // Fill cell background
        this.SetAttribute(attr) |> ignore
        this.FillRect(rect, System.Text.Rune ' ')

        // Space name on first row
        drawCentered this rect 0 (RondelLayout.spaceName space)

        // Nation tokens
        let nations = nationsOnSpace space

        if not (List.isEmpty nations) then
            let tokens =
                nations
                |> List.map (fun p ->
                    let abbr = RondelLayout.abbreviate p.Nation

                    if p.PendingSpace.IsSome then abbr + "\u2192" else abbr)

            // Lay out tokens in rows of up to 3 per row
            let tokensPerRow = max 1 (rect.Width / 6)

            tokens
            |> List.chunkBySize tokensPerRow
            |> List.iteri (fun rowIdx chunk ->
                let line = chunk |> String.concat " "
                drawCentered this rect (1 + rowIdx) line)

    /// Draw the center area (start-position nations).
    let drawCenter (this: View) (viewport: Rectangle) =
        let rect = cellRect viewport 1 1
        let defaultAttr = RondelLayout.mkAttr "White" "Black"
        this.SetAttribute(defaultAttr) |> ignore
        this.FillRect(rect, System.Text.Rune ' ')

        let startNations = nationsAtStart ()

        if not (List.isEmpty startNations) then
            drawCentered this rect 0 "start:"

            let tokens = startNations |> List.map (fun p -> RondelLayout.abbreviate p.Nation)

            let tokensPerRow = max 1 (rect.Width / 6)

            tokens
            |> List.chunkBySize tokensPerRow
            |> List.iteri (fun rowIdx chunk ->
                let line = chunk |> String.concat " "
                drawCentered this rect (1 + rowIdx) line)
        else if state.CurrentGame.IsNone then
            drawCentered this rect (rect.Height / 2) "Awaiting the Great Powers to take their positions"

    member this.SyncFocus() =
        match state.Selection with
        | Some _ ->
            this.CanFocus <- true
            this.SetFocus() |> ignore
        | None -> this.CanFocus <- false

    // ──────────────────────────────────────────────────────────────────────
    // Drawing
    // ──────────────────────────────────────────────────────────────────────

    override this.OnDrawingContent(context: DrawContext) =
        base.OnDrawingContent context |> ignore
        let vp = this.Viewport

        if vp.Width > 0 && vp.Height > 0 then
            for i in 0..7 do
                drawSpaceCell this vp i

            drawCenter this vp

        true

    // ──────────────────────────────────────────────────────────────────────
    // Keyboard
    // ──────────────────────────────────────────────────────────────────────

    override this.OnKeyDown(key: Key) =
        match state.Selection with
        | None -> base.OnKeyDown key
        | Some selection ->
            if key = Key.CursorRight || key = Key.CursorDown then
                state.Selection <- Some { selection with Space = RondelLayout.nextSpace selection.Space }
                this.SetNeedsDraw()
                key.Handled <- true
                true
            elif key = Key.CursorLeft || key = Key.CursorUp then
                state.Selection <- Some { selection with Space = RondelLayout.prevSpace selection.Space }
                this.SetNeedsDraw()
                key.Handled <- true
                true
            elif key = Key.Enter then
                onSpaceSelected selection.Space
                key.Handled <- true
                true
            else
                base.OnKeyDown key

    // ──────────────────────────────────────────────────────────────────────
    // Mouse
    // ──────────────────────────────────────────────────────────────────────

    override this.OnMouseEvent(mouse: Mouse) =
        match state.Selection with
        | None -> base.OnMouseEvent mouse
        | Some selection ->
            if mouse.Flags.HasFlag MouseFlags.LeftButtonClicked && mouse.Position.HasValue then
                let vp = this.Viewport
                let pos = mouse.Position.Value

                match indexFromPoint vp pos.X pos.Y with
                | Some idx ->
                    state.Selection <- Some { selection with Space = RondelLayout.spaces.[idx] }
                    this.SetNeedsDraw()
                    mouse.Handled <- true
                    true
                | None -> base.OnMouseEvent mouse
            else
                base.OnMouseEvent mouse

// ──────────────────────────────────────────────────────────────────────────
// Rondel2View Module
// ──────────────────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
module Rondel2View =

    let create (app: IApplication) (bus: IBus) (rondelHost: RondelHost) =
        let state: RondelViewState =
            { CurrentGame = None; Selection = None; Positions = None }

        let onSpaceSelected space =
            match state.CurrentGame, state.Selection with
            | Some gameId, Some selection ->
                async {
                    do!
                        Move { GameId = gameId; Nation = selection.Nation; Space = space }
                        |> rondelHost.Execute
                }
                |> Async.Start
            | _ -> ()

        let canvas = new RondelCanvas(state, onSpaceSelected)
        canvas.Width <- Dim.Fill()
        canvas.Height <- Dim.Fill()
        let frame = UI.frameView "Rondel" [| canvas |]

        let queryPositions gameId =
            async {
                let! result = rondelHost.QueryPositions { GameId = gameId }

                return
                    match result with
                    | Some view -> Some view.Positions
                    | None -> None
            }
            |> Async.RunSynchronously

        let refresh () =
            UI.invokeOnMainThread app (fun _ ->
                frame.Title <-
                    match state.Selection with
                    | Some selection -> sprintf "Rondel :: Select a next move for %s" selection.Nation
                    | None -> "Rondel"

                canvas.SyncFocus()
                canvas.SetNeedsDraw())

        bus.Subscribe<RondelEvent>(fun event_ ->
            async {
                match event_ with
                | ActionDetermined _
                | MoveToActionSpaceRejected _ -> state.Selection <- None
                | _ -> ()

                state.Positions <- queryPositions state.CurrentGame.Value
                refresh ()
            })

        bus.Subscribe<AccountingEvent>(fun _ -> async { refresh () })

        bus.Subscribe<SystemEvent>(fun event_ ->
            async {
                match event_ with
                | NewGameStarted gameId ->
                    state.CurrentGame <- Some gameId
                    state.Selection <- None
                    state.Positions <- queryPositions gameId

                    refresh ()
                | GameEnded ->
                    state.CurrentGame <- None
                    state.Selection <- None
                    state.Positions <- None

                    refresh ()
                | MoveNationRequested nation ->
                    let currentSpace =
                        state.Positions
                        |> Option.defaultValue []
                        |> List.tryFind (fun p -> p.Nation = nation)
                        |> Option.bind (fun p -> p.CurrentSpace)
                        |> Option.defaultValue Space.Investor

                    state.Selection <- Some { Nation = nation; Space = currentSpace }

                    refresh ()
                | AppStarted -> ()
            })

        frame
