[<AutoOpen>]
module Imperium.AsyncExtensions

open System.Threading
open System.Threading.Tasks

type Async with
    /// Await a Task-returning function, passing the implicit CancellationToken.
    static member AwaitTaskWithCT(makeTask: CancellationToken -> Task<'a>) : Async<'a> =
        async {
            let! ct = Async.CancellationToken
            return! makeTask ct |> Async.AwaitTask
        }

    /// Await a unit Task-returning function, passing the implicit CancellationToken.
    static member AwaitTaskWithCT(makeTask: CancellationToken -> Task) : Async<unit> =
        async {
            let! ct = Async.CancellationToken
            do! makeTask ct |> Async.AwaitTask
        }
