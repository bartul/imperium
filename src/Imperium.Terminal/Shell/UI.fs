namespace Imperium.Terminal.Shell

open System
open System.Collections.ObjectModel
open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views

// ──────────────────────────────────────────────────────────────────────────
// F# Wrappers for Terminal.Gui v2
// ──────────────────────────────────────────────────────────────────────────

module UI =

    /// Wrap F# function as Action delegate
    let action (f: unit -> unit) = Action(f)

    /// Thread-safe UI update from background thread
    /// Must be called from background threads to safely update UI
    let invokeOnMainThread (app: IApplication) (f: unit -> unit) = app.Invoke(action f)

    /// Create an ObservableCollection from a list (for ListView)
    let toObservable (items: 'T list) =
        let coll = ObservableCollection<'T>()
        items |> List.iter coll.Add
        coll

    /// Create a MenuBar from F# list structure
    /// Each menu is (title, items) where items is list of (label, handler)
    let menuBar (menus: (string * (string * (unit -> unit)) list) list) =
        let menuItems =
            menus
            |> List.map (fun (title, items) ->
                let subItems =
                    items
                    |> List.map (fun (label, handler) ->
                        let item = new MenuItem()
                        item.Title <- label
                        item.Action <- action handler
                        item :> View)

                new MenuBarItem(title, subItems))
            |> List.toArray

        let bar = new MenuBar()
        bar.Menus <- menuItems
        bar

    /// Create a FrameView with title
    let frameView (title: string) =
        let view = new FrameView()
        view.Title <- title
        view

    /// Create a Label
    let label (text: string) =
        let lbl = new Label()
        lbl.Text <- text
        lbl

    /// Create a ListView from string items
    let listView (items: string list) =
        let view = new ListView()
        view.SetSource(toObservable items)
        view

    /// Create a Button with click handler
    let button (text: string) (onClick: unit -> unit) =
        let btn = new Button()
        btn.Text <- text
        btn.Accepting.AddHandler(fun _ _ -> onClick ())
        btn

    /// Create a Shortcut binding a key to an action with a display title.
    /// Used in StatusBar, MenuBar, or any view that hosts Shortcut children.
    let shortcut (key: Key) (title: string) (handler: unit -> unit) =
        let s = new Shortcut()
        s.Key <- key
        s.Title <- title
        s.Accepting.AddHandler(fun _ _ -> handler ())
        s
