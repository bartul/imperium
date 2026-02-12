module Imperium.UnitTests.SpecMarkdown

open System
open Spec

// ────────────────────────────────────────────────────────────────────────────────
// Markdown Rendering
// ────────────────────────────────────────────────────────────────────────────────

let private escapeCell (value: string) =
    value.Replace("|", "\\|").Trim()

let private formatAction action =
    match action with
    | Execute command -> Some(sprintf "Command `%A`" command)
    | Handle event -> Some(sprintf "Event `%A`" event)
    | ClearEvents
    | ClearCommands -> None

let private captionRows caption items =
    match items with
    | [] -> [ sprintf "| %s | - _none_ |" caption ]
    | head :: tail ->
        [ sprintf "| %s | - %s |" caption (escapeCell head) ]
        @ (tail |> List.map (fun item -> sprintf "|  | - %s |" (escapeCell item)))

let toMarkdown (runner: ISpecRunner<'ctx, 'state, 'cmd, 'evt>) (spec: Specification<'ctx, 'cmd, 'evt>) =
    let context = spec.On()
    let initialState = runner.CaptureState context

    runActions runner context spec.Actions

    let finalState = runner.CaptureState context

    let whenItems =
        spec.Actions |> List.choose formatAction

    let thenItems =
        [ sprintf "Final state `%A`" finalState
          yield!
              spec.Expectations
              |> List.map (fun expectation ->
                  let result = if expectation.Predicate context then "✅" else "❌"
                  sprintf "%s %s" result expectation.Description) ]

    String.concat
        Environment.NewLine
        ([ sprintf "### %s" spec.Name
           ""
           "| Caption | Data |"
           "|---|---|"
           sprintf "| Given | %s |" (escapeCell (sprintf "%A" initialState)) ]
         @ captionRows "When" whenItems
         @ captionRows "Then" thenItems
         @ [ "" ])

let toMarkdownDocument runner specifications =
    specifications |> List.map (toMarkdown runner) |> String.concat Environment.NewLine
