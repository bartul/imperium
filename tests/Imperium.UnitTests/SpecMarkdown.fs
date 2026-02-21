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

let private formatAction action =
    match action with
    | Execute command -> Some(sprintf "Command `%A`" command)
    | Handle event -> Some(sprintf "Event `%A`" event)

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
    let initialStateText = formatState initialState

    runActions runner context spec.Actions

    let finalState = runner.CaptureState context
    let finalStateText = formatState finalState

    let givenActionItems =
        spec.GivenActions |> List.choose formatAction |> List.map escapeCell

    let givenActionRows =
        match givenActionItems with
        | [] -> []
        | _ -> captionRows "" givenActionItems

    let whenItems = spec.Actions |> List.choose formatAction |> List.map escapeCell

    let thenItems =
        [ $"State `{finalStateText}`" |> escapeCell
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
           sprintf "| Given | State %s |" (escapeCell $"`{initialStateText}`") ]
         @ givenActionRows
         @ captionRows "When" whenItems
         @ captionRows "Then" thenItems
         @ [ "" ])

let toMarkdownDocument options runner specifications =
    specifications
    |> List.map (toMarkdown options runner)
    |> String.concat Environment.NewLine
