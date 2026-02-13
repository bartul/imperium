module Imperium.UnitTests.SpecMarkdown

open System
open System.Text.RegularExpressions
open Spec

// ────────────────────────────────────────────────────────────────────────────────
// Markdown Rendering
// ────────────────────────────────────────────────────────────────────────────────

let private escapeCell (value: string) =
    value
        .Replace("\r\n", " ")
        .Replace("\n", " ")
        .Replace("\r", " ")
        .Replace("|", "\\|")
    |> fun text -> Regex.Replace(text, @"\s+", " ").Trim()

let private formatAction action =
    match action with
    | Execute command -> Some(sprintf "Command `%A`" command)
    | Handle event -> Some(sprintf "Event `%A`" event)
    | ClearEvents
    | ClearCommands -> None

let private captionRows caption items =
    match items with
    | [] -> [ sprintf "| %s | %s |" caption (escapeCell "_none_") ]
    | head :: tail ->
        [ $"| %s{caption} | %s{head} |" ]
        @ (tail |> List.map (fun item -> $"| | %s{item} |"))

let toMarkdown (runner: ISpecRunner<'ctx, 'state, 'cmd, 'evt>) (spec: Specification<'ctx, 'cmd, 'evt>) =
    let context = spec.On()
    let initialState = runner.CaptureState context

    runActions runner context spec.Actions

    let finalState = runner.CaptureState context

    let whenItems = spec.Actions |> List.choose formatAction |> List.map escapeCell

    let thenItems =
        [ $"State `%A{finalState}`" |> escapeCell
          yield!
              spec.Expectations
              |> List.map (fun expectation ->
                  let result = if expectation.Predicate context then "✅" else "❌"
                  $"%s{result} %s{expectation.Description}" |> escapeCell) ]

    String.concat
        Environment.NewLine
        ([ $"### %s{spec.Name}"
           ""
           "| | |"
           "| --- | --- |"
           sprintf "| Given | State %s |" (escapeCell $"`%A{initialState}`") ]
         @ captionRows "When" whenItems
         @ captionRows "Then" thenItems
         @ [ "" ])

let toMarkdownDocument runner specifications =
    specifications
    |> List.map (toMarkdown runner)
    |> String.concat Environment.NewLine
