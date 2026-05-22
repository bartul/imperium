namespace Imperium.Rondel

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Write-Side Dependencies
// ──────────────────────────────────────────────────────────────────────────

type LoadRondelState = Id -> Async<RondelState option>

type RondelDependencies = { Load: LoadRondelState; Commit: CommitRondelEffects }

and RondelEffects =
    { State: RondelState option; IntegrationEvents: RondelEvent list; OutboundCommands: RondelOutboundCommand list }

and CommitRondelEffects = RondelEffects -> Async<unit>

// ──────────────────────────────────────────────────────────────────────────
// Read-Side Dependencies
// ──────────────────────────────────────────────────────────────────────────

type LoadRondelStateForQuery = Id -> Async<RondelState option>

type RondelQueryDependencies = { Load: LoadRondelStateForQuery }
