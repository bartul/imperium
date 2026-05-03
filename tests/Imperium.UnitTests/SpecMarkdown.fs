[<RequireQualifiedAccess>]
module Imperium.UnitTests.SpecMarkdown

open System
open System.Collections
open System.Text.RegularExpressions
open Microsoft.FSharp.Reflection
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

let private isOptionType (valueType: Type) =
    valueType.IsGenericType
    && valueType.GetGenericTypeDefinition() = typedefof<option<_>>

let private isMapType (valueType: Type) =
    valueType.IsGenericType
    && valueType.GetGenericTypeDefinition() = typedefof<Map<_, _>>

let rec private formatValue (value: obj) (valueType: Type) =
    if isOptionType valueType then
        if obj.ReferenceEquals(value, null) then
            "None"
        else
            let unionCase, fields = FSharpValue.GetUnionFields(value, valueType)

            match unionCase.Name, fields with
            | "Some", [| inner |] ->
                let innerType = valueType.GetGenericArguments().[0]
                $"Some({formatValue inner innerType})"
            | _ -> sprintf "%A" value
    elif obj.ReferenceEquals(value, null) then
        "null"
    elif isMapType valueType then
        let entries =
            value :?> IEnumerable
            |> Seq.cast<obj>
            |> Seq.map (fun entry ->
                let entryType = entry.GetType()
                let keyProperty = entryType.GetProperty("Key")
                let valueProperty = entryType.GetProperty("Value")

                if isNull keyProperty || isNull valueProperty then
                    sprintf "%A" entry
                else
                    let keyText =
                        keyProperty.GetValue(entry)
                        |> fun key -> formatValue key keyProperty.PropertyType

                    let valueText =
                        valueProperty.GetValue(entry)
                        |> fun entryValue -> formatValue entryValue valueProperty.PropertyType

                    $"({keyText}, {valueText})")
            |> String.concat "; "

        $"map [{entries}]"
    elif FSharpType.IsRecord valueType then
        let fields = FSharpType.GetRecordFields valueType
        let values = FSharpValue.GetRecordFields value

        let formattedFields =
            Array.zip fields values
            |> Array.map (fun (field, fieldValue) -> $"{field.Name} = {formatValue fieldValue field.PropertyType}")
            |> String.concat "; "

        $"{{ {formattedFields} }}"
    else
        sprintf "%A" value

let private formatState (state: 'state) = formatValue (box state) typeof<'state>

let private renderState (runner: SpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>) (state: 'state) =
    match runner.FormatState with
    | Some formatter -> formatter state
    | None -> formatState state

let private formatActionRow action =
    match action with
    | Execute command -> Some("👉", sprintf "`%A`" command)
    | Handle event -> Some("🔔", sprintf "`%A`" event)

let private renderTableRows rows =
    let bodyRows =
        rows
        |> List.map (fun (left, right) -> $"| {escapeCell left} | {escapeCell right} |")

    [ "| | |"; "| --- | --- |" ] @ bodyRows

let private renderSection weight title stateText rows =
    [ renderHeader weight title; ""; "```text"; stateText; "```"; "" ]
    @ if List.isEmpty rows then [] else renderTableRows rows

let private renderActionSection weight title rows =
    [ renderHeader weight title; "" ]
    @ if List.isEmpty rows then [] else renderTableRows rows

let private toMarkdown
    (options: MarkdownRenderOptions)
    (runner: SpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>)
    (spec: Specification<'ctx, 'seed, 'cmd, 'evt>)
    =
    let results = spec.Expectations |> List.map (runExpectation runner spec)

    let initialStateText =
        results
        |> List.tryPick (fun r -> r.InitialState)
        |> Option.map (renderState runner)
        |> Option.defaultValue "_no state capture_"

    let finalStateText =
        results
        |> List.tryPick (fun r -> r.FinalState)
        |> Option.map (renderState runner)
        |> Option.defaultValue "_no state capture_"

    let givenRows = spec.GivenActions |> List.choose formatActionRow

    let whenRows = spec.Actions |> List.choose formatActionRow

    let thenRows =
        results
        |> List.map (fun result ->
            match result.Outcome with
            | Passed -> "✅", result.Description
            | Failed ex -> "❌", $"{result.Description} — {escapeCell ex.Message}")

    let specHeaderWeight = childHeader options.ParentHeader
    let sectionHeaderWeight = childHeader specHeaderWeight
    let specHeader = renderHeader specHeaderWeight $"📋 %s{spec.Name}"

    String.concat
        Environment.NewLine
        ([ specHeader; "" ]
         @ renderSection sectionHeaderWeight "Given" initialStateText givenRows
         @ [ "" ]
         @ renderActionSection sectionHeaderWeight "When" whenRows
         @ [ "" ]
         @ renderSection sectionHeaderWeight "Then" finalStateText thenRows
         @ [ "" ])

let toMarkdownDocument options runner specifications =
    specifications
    |> List.map (toMarkdown options runner)
    |> String.concat Environment.NewLine

let render
    (options: MarkdownRenderOptions)
    (sectionName: string)
    (runner: SpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>)
    (specs: Specification<'ctx, 'seed, 'cmd, 'evt> list)
    : string option =
    if List.isEmpty specs then
        None
    else
        let sectionHeaderWeight = childHeader options.ParentHeader
        let header = renderHeader sectionHeaderWeight sectionName
        let childOptions = { options with ParentHeader = sectionHeaderWeight }
        let body = toMarkdownDocument childOptions runner specs
        Some $"{header}{Environment.NewLine}{Environment.NewLine}{body}{Environment.NewLine}"
