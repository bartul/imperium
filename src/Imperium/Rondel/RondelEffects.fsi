namespace Imperium.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Effects
// ──────────────────────────────────────────────────────────────────────────

/// Named effect shape returned by Rondel handlers.
/// Represents the side effects of a single command/event:
/// optional new state, published integration events, and outbound commands.
type RondelEffects =
    { State: RondelState option; IntegrationEvents: RondelEvent list; OutboundCommands: RondelOutboundCommand list }

/// Commit boundary for Rondel effects.
/// Infrastructure-owned function that durably applies state, publishes events,
/// and dispatches outbound commands as an atomic unit.
/// Failures propagate as exceptions (Async&lt;unit&gt; semantics).
type CommitRondelEffects = RondelEffects -> Async<unit>
