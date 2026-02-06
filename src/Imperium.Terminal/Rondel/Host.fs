namespace Imperium.Terminal.Rondel

open Imperium.Accounting
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Terminal

// ──────────────────────────────────────────────────────────────────────────
// Types
// ──────────────────────────────────────────────────────────────────────────

/// Thunk for dispatching commands to Accounting - resolves at call time to break circular deps
type DispatchToAccounting = unit -> AccountingCommand -> Async<Result<unit, string>>

/// Host for Rondel bounded context with MailboxProcessor serialization
type RondelHost =
    {
        /// Commands serialized through MailboxProcessor
        Execute: RondelCommand -> Async<unit>
        /// Queries bypass mailbox, read directly from the store
        QueryPositions: GetNationPositionsQuery -> Async<RondelPositionsView option>
        /// Queries bypass mailbox, read directly from the store
        QueryOverview: GetRondelOverviewQuery -> Async<RondelView option>
    }

module RondelHost =
    // ──────────────────────────────────────────────────────────────────────────
    // Internal Types
    // ──────────────────────────────────────────────────────────────────────────

    type private HostMessage =
        | Command of RondelCommand
        | InboundEvent of RondelInboundEvent

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    let private toInboundEvent (evt: AccountingEvent) : RondelInboundEvent =
        match evt with
        | RondelInvoicePaid e -> InvoicePaid { GameId = e.GameId; BillingId = RondelBillingId.ofId e.BillingId }
        | RondelInvoicePaymentFailed e ->
            InvoicePaymentFailed { GameId = e.GameId; BillingId = RondelBillingId.ofId e.BillingId }

    // ──────────────────────────────────────────────────────────────────────────
    // Factory
    // ──────────────────────────────────────────────────────────────────────────

    /// Creates a new RondelHost with MailboxProcessor serialization
    let create (store: RondelStore) (bus: IBus) (dispatchToAccounting: DispatchToAccounting) : RondelHost =
        let dispatch (outbound: RondelOutboundCommand) =
            let accountingCmd =
                match outbound with
                | ChargeMovement cmd ->
                    ChargeNationForRondelMovement
                        { GameId = cmd.GameId
                          Nation = cmd.Nation
                          Amount = cmd.Amount
                          BillingId = Id(RondelBillingId.value cmd.BillingId) }
                | VoidCharge cmd ->
                    VoidRondelCharge { GameId = cmd.GameId; BillingId = Id(RondelBillingId.value cmd.BillingId) }

            dispatchToAccounting () accountingCmd

        let deps: RondelDependencies =
            { Load = store.Load; Save = store.Save; Publish = bus.Publish; Dispatch = dispatch }

        let mailbox =
            MailboxProcessor.Start(fun inbox ->
                let rec loop () =
                    async {
                        let! msg = inbox.Receive()

                        match msg with
                        | Command cmd -> do! execute deps cmd
                        | InboundEvent evt -> do! handle deps evt

                        return! loop ()
                    }

                loop ())

        // Subscribe to Accounting events and convert to Rondel inbound events
        bus.Subscribe<AccountingEvent>(fun evt -> async { toInboundEvent evt |> InboundEvent |> mailbox.Post })

        let queryDeps: RondelQueryDependencies = { Load = store.Load }

        { Execute = fun cmd -> async { Command cmd |> mailbox.Post }
          QueryPositions = fun query -> getNationPositions queryDeps query
          QueryOverview = fun query -> getRondelOverview queryDeps query }
