namespace Imperium.Accounting

/// Public facade for the Accounting bounded context. Routes commands to internal handlers.
[<RequireQualifiedAccess>]
module Accounting =

    /// <summary>Execute an accounting command by routing it to the appropriate internal handler.</summary>
    /// <param name="deps">
    /// Accounting dependencies required by command handlers. The current stateless implementation uses these
    /// dependencies to publish accounting events produced while processing commands.
    /// </param>
    /// <param name="cmd">
    /// Domain accounting command to execute. Accepted commands are rondel movement charges and rondel charge
    /// voids; commands built from contract input are expected to have passed the Accounting transformation layer.
    /// </param>
    /// <returns>
    /// An async operation that completes with unit when the selected handler succeeds. Handler or publication
    /// failures are propagated as exceptions, and cancellation flows through the implicit async CancellationToken.
    /// </returns>
    val execute: deps: AccountingDependencies -> cmd: AccountingCommand -> Async<unit>
