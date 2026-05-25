[<RequireQualifiedAccess>]
module Imperium.Testing.Spec.SpecFilter

// ────────────────────────────────────────────────────────────────────────────────
// Spec Filter
// ────────────────────────────────────────────────────────────────────────────────

type Predicate = string list -> bool

type private ParsedFlag =
    | Filter of hierarchy: string
    | FilterTestList of name: string
    | FilterTestCase of name: string
    | Run of paths: string list

let none: Predicate = fun _ -> true

let private getNonLeaf (path: string list) =
    match path with
    | []
    | [ _ ] -> []
    | xs -> xs[.. xs.Length - 2]

let private getLeaf (path: string list) =
    path |> List.tryLast |> Option.defaultValue ""

let private isFlag (arg: string) = arg.StartsWith "--"

// Mirror Expecto: each --join-with overwrites the joinWith field; last wins.
let private resolveJoinWith (args: string array) =
    args
    |> Array.indexed
    |> Array.choose (fun (i, a) ->
        if a = "--join-with" then
            Array.tryItem (i + 1) args
        else
            None)
    |> Array.tryLast
    |> function
        | Some "/" -> "/"
        | _ -> "."

let private takeUntilNextFlag args =
    let rec loop values remaining =
        match remaining with
        | value :: rest when not (isFlag value) -> loop (value :: values) rest
        | _ -> List.rev values, remaining

    loop [] args

let private parse (args: string array) =
    let rec loop parsed remaining =
        match remaining with
        | [] -> List.rev parsed
        | "--filter" :: value :: rest -> loop (Filter value :: parsed) rest
        | "--filter-test-list" :: value :: rest -> loop (FilterTestList value :: parsed) rest
        | "--filter-test-case" :: value :: rest -> loop (FilterTestCase value :: parsed) rest
        | "--run" :: rest ->
            let values, remaining = takeUntilNextFlag rest
            loop (Run values :: parsed) remaining
        | _ :: rest -> loop parsed rest

    loop [] (Array.toList args)

let private matchesRunPath joinWith userPaths path =
    let joined = String.concat joinWith path

    userPaths
    |> List.exists (fun userPath -> joined = userPath || joined.StartsWith(userPath + joinWith))

let private toPredicate joinWith flag =
    match flag with
    | Filter hierarchy -> fun path -> (String.concat joinWith path).StartsWith hierarchy
    | FilterTestList name -> fun path -> getNonLeaf path |> List.exists (fun s -> s.Contains name)
    | FilterTestCase name -> fun path -> (getLeaf path).Contains name
    | Run paths -> matchesRunPath joinWith paths

let fromArgs (args: string array) : Predicate =
    let joinWith = resolveJoinWith args

    let lastPredicate =
        args |> parse |> List.tryLast |> Option.map (toPredicate joinWith)

    match lastPredicate with
    | Some predicate -> predicate
    | None -> none

let apply
    (filter: Predicate)
    (pathPrefix: string list)
    (specs: Specification<'ctx, 'seed, 'cmd, 'evt> list)
    : Specification<'ctx, 'seed, 'cmd, 'evt> list =
    specs
    |> List.choose (fun spec ->
        let specPath = pathPrefix @ [ spec.Name ]

        let matching =
            spec.Expectations
            |> List.filter (fun exp -> filter (specPath @ [ exp.Description ]))

        if List.isEmpty matching then
            None
        else
            Some { spec with Expectations = matching })
