[<RequireQualifiedAccess>]
module Imperium.UnitTests.SpecMarkdown

open System
open System.Text.RegularExpressions
open Spec

// ────────────────────────────────────────────────────────────────────────────────
// Markdown Rendering
// ────────────────────────────────────────────────────────────────────────────────

type HeaderWeight =
    | H1
    | H2
    | H3
    | H4
    | H5
    | H6

type MarkdownRenderOptions = { ParentHeader: HeaderWeight }

let private toLevel =
    function
    | H1 -> 1
    | H2 -> 2
    | H3 -> 3
    | H4 -> 4
    | H5 -> 5
    | H6 -> 6

let private childHeader =
    function
    | H1 -> H2
    | H2 -> H3
    | H3 -> H4
    | H4 -> H5
    | H5 -> H6
    | H6 -> H6

let private renderHeader weight text =
    String.replicate (toLevel weight) "#" + " " + text

let private escapeCell (value: string) =
    value.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Replace("|", "\\|")
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

let toMarkdown
    (options: MarkdownRenderOptions)
    (runner: ISpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>)
    (spec: Specification<'ctx, 'seed, 'cmd, 'evt>)
    =
    let context = prepareContext runner spec
    let initialState = runner.CaptureState context

    runActions runner context spec.Actions

    let finalState = runner.CaptureState context

    let givenActionItems =
        spec.GivenActions |> List.choose formatAction |> List.map escapeCell

    let whenItems = spec.Actions |> List.choose formatAction |> List.map escapeCell

    let thenItems =
        [ $"State `%A{finalState}`" |> escapeCell
          yield!
              spec.Expectations
              |> List.map (fun expectation ->
                  let result = if expectation.Predicate context then "✅" else "❌"
                  $"%s{result} %s{expectation.Description}" |> escapeCell) ]

    let specHeader = renderHeader (childHeader options.ParentHeader) $"📋 %s{spec.Name}"

    String.concat
        Environment.NewLine
        ([ specHeader
           ""
           "| Step | Details |"
           "| --- | --- |"
           sprintf "| Given | State %s |" (escapeCell $"`%A{initialState}`") ]
         @ captionRows "Given actions" givenActionItems
         @ captionRows "When" whenItems
         @ captionRows "Then" thenItems
         @ [ "" ])

let toMarkdownDocument options runner specifications =
    specifications
    |> List.map (toMarkdown options runner)
    |> String.concat Environment.NewLine
