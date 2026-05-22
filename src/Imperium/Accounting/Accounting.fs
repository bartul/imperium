namespace Imperium.Accounting

[<RequireQualifiedAccess>]
module Accounting =

    let execute (deps: AccountingDependencies) (cmd: AccountingCommand) : Async<unit> =
        match cmd with
        | ChargeNationForRondelMovement c -> Handlers.chargeNationForRondelMovement deps c
        | VoidRondelCharge c -> Handlers.voidRondelCharge deps c
