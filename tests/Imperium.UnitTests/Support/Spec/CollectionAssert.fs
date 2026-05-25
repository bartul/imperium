module Imperium.Testing.Spec.CollectionAssert

open Expecto

// ────────────────────────────────────────────────────────────────────────────────
// Collection Assertions
// ────────────────────────────────────────────────────────────────────────────────

type Accessor<'ctx, 'item> =
    { Has: 'item -> string -> 'ctx -> unit
      HasAny: ('item -> bool) -> string -> 'ctx -> unit
      HasNone: ('item -> bool) -> string -> 'ctx -> unit
      Count: 'item -> int -> string -> 'ctx -> unit
      HasSize: int -> string -> 'ctx -> unit }

let private formatItems items =
    match items with
    | [] -> "<none>"
    | _ -> items |> List.map (sprintf "%A") |> String.concat "; "

let forAccessor (accessor: 'ctx -> seq<'item>) : Accessor<'ctx, 'item> =
    { Has = fun item message ctx -> Expect.contains (accessor ctx) item message
      HasAny =
        fun predicate message ctx ->
            let items = accessor ctx |> Seq.toList

            if not (items |> List.exists predicate) then
                failtestf
                    "%s. Expected at least one matching item, but none matched. Actual items: %s"
                    message
                    (formatItems items)
      HasNone =
        fun predicate message ctx ->
            let items = accessor ctx |> Seq.toList
            let matchingItems = items |> List.filter predicate

            if not (List.isEmpty matchingItems) then
                failtestf
                    "%s. Expected no matching items, but found: %s. Actual items: %s"
                    message
                    (formatItems matchingItems)
                    (formatItems items)
      Count = fun item n message ctx -> Expect.hasCountOf (accessor ctx) (uint32 n) (fun x -> x = item) message
      HasSize = fun n message ctx -> Expect.hasLength (accessor ctx) n message }
