module Imperium.Terminal.Rondel.Store

open Imperium.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Types
// ──────────────────────────────────────────────────────────────────────────

/// Store for Rondel bounded context persistence
type RondelStore =
    { Load: LoadRondelState
      Save: SaveRondelState }

// ──────────────────────────────────────────────────────────────────────────
// In-Memory Implementation
// ──────────────────────────────────────────────────────────────────────────

module InMemoryRondelStore =

    /// Creates a new in-memory RondelStore instance
    let create () : RondelStore = failwith "Not implemented"
