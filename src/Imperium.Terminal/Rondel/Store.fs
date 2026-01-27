namespace Imperium.Terminal

open System.Collections.Concurrent
open Imperium.Primitives
open Imperium.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Types
// ──────────────────────────────────────────────────────────────────────────

/// Store for Rondel bounded context persistence
type RondelStore = { Load: LoadRondelState; Save: SaveRondelState }

// ──────────────────────────────────────────────────────────────────────────
// In-Memory Implementation
// ──────────────────────────────────────────────────────────────────────────

module InMemoryRondelStore =

    /// Creates a new in-memory RondelStore instance
    let create () : RondelStore =
        let states = ConcurrentDictionary<Id, RondelState>()

        { Load =
            fun id ->
                async {
                    match states.TryGetValue(id) with
                    | true, state -> return Some state
                    | false, _ -> return None
                }
          Save =
            fun state ->
                async {
                    states.[state.GameId] <- state
                    return Ok()
                } }
