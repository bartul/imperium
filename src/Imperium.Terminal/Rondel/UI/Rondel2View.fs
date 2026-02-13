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
// RondelCanvas — custom drawn view
// ──────────────────────────────────────────────────────────────────────────

type RondelCanvas(app: IApplication, bus: IBus, rondelHost: RondelHost) =
    inherit View()

    let mutable positions: NationPositionView list = []
    let mutable currentGameId: Id option = None
    let mutable selectedIndex: int = 0
    let mutable selectingForNation: string option = None

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
        positions |> List.filter (fun p -> p.CurrentSpace = Some space)

    /// Get nations at start (no current space).
    let nationsAtStart () =
        positions |> List.filter (fun p -> p.CurrentSpace.IsNone)

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

        let isSelected = selectingForNation.IsSome && selectedIndex = index

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
        else if positions.IsEmpty then
            drawCentered this rect (rect.Height / 2) "No game"

    // ──────────────────────────────────────────────────────────────────────
    // Public state management
    // ──────────────────────────────────────────────────────────────────────

    member _.UpdatePositions(newPositions: NationPositionView list) = positions <- newPositions
    // Not calling SetNeedsDraw here — caller should use invokeOnMainThread

    member _.SetGameId(id: Id option) = currentGameId <- id

    member this.EnterSelectionMode(nation: string) =
        selectingForNation <- Some nation

        // Start selection at the nation's current space, or index 0
        let currentIdx =
            positions
            |> List.tryFind (fun p -> p.Nation = nation)
            |> Option.bind (fun p -> p.CurrentSpace)
            |> Option.bind (fun space -> RondelLayout.spaces |> Array.tryFindIndex ((=) space))
            |> Option.defaultValue 0

        selectedIndex <- currentIdx
        this.CanFocus <- true
        this.SetFocus() |> ignore
        this.SetNeedsDraw()

    member this.ExitSelectionMode() =
        selectingForNation <- None
        this.SetNeedsDraw()

    // ──────────────────────────────────────────────────────────────────────
    // Drawing
    // ──────────────────────────────────────────────────────────────────────

    override this.OnDrawingContent(context: DrawContext) =
        base.OnDrawingContent(context) |> ignore
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
        match selectingForNation with
        | None -> base.OnKeyDown(key)
        | Some nation ->
            if key = Key.CursorRight || key = Key.CursorDown then
                selectedIndex <- (selectedIndex + 1) % 8
                this.SetNeedsDraw()
                key.Handled <- true
                true
            elif key = Key.CursorLeft || key = Key.CursorUp then
                selectedIndex <- (selectedIndex + 7) % 8
                this.SetNeedsDraw()
                key.Handled <- true
                true
            elif key = Key.Enter then
                let space = RondelLayout.spaces.[selectedIndex]
                this.ExitSelectionMode()

                match currentGameId with
                | Some gameId ->
                    async { do! Move { GameId = gameId; Nation = nation; Space = space } |> rondelHost.Execute }
                    |> Async.Start
                | None -> ()

                key.Handled <- true
                true
            elif key = Key.Esc then
                this.ExitSelectionMode()
                async { do! bus.Publish MoveSelectionCancelled } |> Async.Start
                key.Handled <- true
                true
            else
                base.OnKeyDown(key)

    // ──────────────────────────────────────────────────────────────────────
    // Mouse
    // ──────────────────────────────────────────────────────────────────────

    override this.OnMouseEvent(mouse: Mouse) =
        match selectingForNation with
        | None -> base.OnMouseEvent(mouse)
        | Some _ ->
            if mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked) && mouse.Position.HasValue then
                let vp = this.Viewport
                let pos = mouse.Position.Value

                match indexFromPoint vp pos.X pos.Y with
                | Some idx ->
                    selectedIndex <- idx
                    this.SetNeedsDraw()
                    mouse.Handled <- true
                    true
                | None -> base.OnMouseEvent(mouse)
            else
                base.OnMouseEvent(mouse)

// ──────────────────────────────────────────────────────────────────────────
// Rondel2View Module
// ──────────────────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
module Rondel2View =

    let create (app: IApplication) (bus: IBus) (rondelHost: RondelHost) =
        let canvas = new RondelCanvas(app, bus, rondelHost)
        canvas.Width <- Dim.Fill()
        canvas.Height <- Dim.Fill()

        let frame = new FrameView()
        frame.Title <- "Rondel"
        frame.Add canvas |> ignore

        let mutable currentGameId: Id option = None

        let refresh () =
            match currentGameId with
            | None ->
                UI.invokeOnMainThread app (fun () ->
                    canvas.UpdatePositions([])
                    canvas.SetNeedsDraw())
            | Some gameId ->
                async {
                    let! result = rondelHost.QueryPositions { GameId = gameId }

                    UI.invokeOnMainThread app (fun () ->
                        match result with
                        | None -> canvas.UpdatePositions([])
                        | Some view -> canvas.UpdatePositions(view.Positions)

                        canvas.SetNeedsDraw())
                }
                |> Async.Start

        bus.Subscribe<RondelEvent>(fun event_ ->
            async {
                refresh ()

                match event_ with
                | ActionDetermined _
                | MoveToActionSpaceRejected _ -> UI.invokeOnMainThread app (fun () -> frame.Title <- "Rondel")
                | _ -> ()
            })

        bus.Subscribe<AccountingEvent>(fun _ -> async { refresh () })

        bus.Subscribe<SystemEvent>(fun event_ ->
            async {
                match event_ with
                | NewGameStarted gameId ->
                    currentGameId <- Some gameId
                    canvas.SetGameId(Some gameId)
                    refresh ()
                | GameEnded ->
                    currentGameId <- None
                    canvas.SetGameId None

                    UI.invokeOnMainThread app (fun () ->
                        canvas.ExitSelectionMode()
                        canvas.UpdatePositions([])
                        canvas.SetNeedsDraw()
                        frame.Title <- "Rondel")
                | MoveNationRequested nation ->
                    UI.invokeOnMainThread app (fun () ->
                        canvas.EnterSelectionMode(nation)
                        frame.Title <- sprintf "Rondel \u2014 Move %s to..." nation)
                | MoveSelectionCancelled ->
                    UI.invokeOnMainThread app (fun () ->
                        canvas.ExitSelectionMode()
                        frame.Title <- "Rondel")
                | AppStarted -> ()
            })

        frame
