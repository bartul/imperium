namespace Imperium.Rondel

[<RequireQualifiedAccess>]
module Rondel =

    let execute (deps: RondelDependencies) (command: RondelCommand) : Async<unit> =
        async {
            let! effects =
                match command with
                | SetToStartingPositions cmd -> Handlers.setToStartingPositions deps.Load cmd
                | Move cmd -> Handlers.move deps.Load cmd

            do! deps.Commit effects
        }

    let handle (deps: RondelDependencies) (event: RondelInboundEvent) : Async<unit> =
        async {
            let! effects =
                match event with
                | InvoicePaid evt -> Handlers.onInvoicePaid deps.Load evt
                | InvoicePaymentFailed evt -> Handlers.onInvoicePaymentFailed deps.Load evt

            do! deps.Commit effects
        }

    let getNationPositions
        (deps: RondelQueryDependencies)
        (query: GetNationPositionsQuery)
        : Async<RondelPositionsView option> =
        Queries.getNationPositions deps query

    let getRondelOverview (deps: RondelQueryDependencies) (query: GetRondelOverviewQuery) : Async<RondelView option> =
        Queries.getRondelOverview deps query
