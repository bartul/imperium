module Imperium.Terminal.Rondel.Host

open Imperium.Rondel
open Imperium.Terminal.Bus
open Imperium.Terminal.Rondel.Store

// ──────────────────────────────────────────────────────────────────────────
// Types
// ──────────────────────────────────────────────────────────────────────────

/// Host for Rondel bounded context with MailboxProcessor serialization
type RondelHost =
    { /// Commands serialized through MailboxProcessor
      ExecuteCommand: RondelCommand -> Async<Result<unit, string>>
      /// Queries bypass mailbox, read directly from store
      QueryPositions: GetNationPositionsQuery -> Async<RondelPositionsView option>
      /// Queries bypass mailbox, read directly from store
      QueryOverview: GetRondelOverviewQuery -> Async<RondelView option> }

// ──────────────────────────────────────────────────────────────────────────
// Factory
// ──────────────────────────────────────────────────────────────────────────

/// Creates a new RondelHost with MailboxProcessor serialization
let create (store: RondelStore) (bus: Bus) : RondelHost =
    failwith "Not implemented"
