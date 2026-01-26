namespace Imperium.Terminal.Rondel

open Imperium.Accounting
open Imperium.Rondel
open Imperium.Terminal
open Imperium.Terminal.Rondel.Store

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
    // Factory
    // ──────────────────────────────────────────────────────────────────────────

    /// Creates a new RondelHost with MailboxProcessor serialization
    let create (store: RondelStore) (bus: IBus) (dispatchToAccounting: DispatchToAccounting) : RondelHost =
        failwith "Not implemented"
