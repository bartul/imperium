namespace Imperium.Accounting

/// Public facade for the Accounting bounded context. Routes commands to internal handlers.
[<RequireQualifiedAccess>]
module Accounting =

    /// Execute an accounting command. Routes to the appropriate command handler.
    /// CancellationToken flows implicitly through Async context.
    val execute: AccountingDependencies -> AccountingCommand -> Async<unit>
