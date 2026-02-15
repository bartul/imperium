module Imperium.Terminal.Program

#nowarn "40" // Recursive object references checked at runtime (intentional lazy pattern)

open Imperium.Terminal
open Imperium.Terminal.Rondel
open Imperium.Terminal.Accounting
open Imperium.Terminal.Shell

// ──────────────────────────────────────────────────────────────────────────
// Composition Root
// ──────────────────────────────────────────────────────────────────────────

let private createHosts () =
    let bus = Bus.create ()
    let store = InMemoryRondelStore.create ()

    let rec rondelHost: Lazy<RondelHost> =
        lazy
            (RondelHost.create store bus (fun () cmd ->
                async {
                    try
                        do! accountingHost.Value.Execute cmd
                        return Ok()
                    with ex ->
                        return Error ex.Message
                }))

    and accountingHost: Lazy<AccountingHost> = lazy (AccountingHost.create bus)

    // Force initialization to fail fast on configuration errors
    let rondel = rondelHost.Force()
    let accounting = accountingHost.Force()

    rondel, accounting, bus

// ──────────────────────────────────────────────────────────────────────────
// Entry Point
// ──────────────────────────────────────────────────────────────────────────

[<EntryPoint>]
let main _ =
    let rondelHost, accountingHost, bus = createHosts ()
    App.run rondelHost accountingHost bus
    0
