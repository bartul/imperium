namespace Imperium.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Effects
// ──────────────────────────────────────────────────────────────────────────

type RondelEffects =
    { State: RondelState option; IntegrationEvents: RondelEvent list; OutboundCommands: RondelOutboundCommand list }

type CommitRondelEffects = RondelEffects -> Async<unit>
