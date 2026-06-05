namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Effects
// ──────────────────────────────────────────────────────────────────────────

/// Named effect shape returned by Gameplay handlers.
type GameplayEffects =
    { State: GameplayState option
      IntegrationEvents: GameplayEvent list
      OutboundCommands: GameplayOutboundCommand list }

/// Internal builders for constructing and accumulating GameplayEffects in a pipeline style.
module internal GameplayEffects =
    /// No effects: no state change, no events, no commands.
    val none: GameplayEffects

    /// Returns effects with the given state applied.
    val withState: GameplayState -> GameplayEffects -> GameplayEffects

    /// Creates effects containing the given state and nothing else.
    val create: GameplayState -> GameplayEffects

    /// Appends an integration event to effects.
    val withEvent: GameplayEvent -> GameplayEffects -> GameplayEffects

    /// Appends an outbound command to effects.
    val withCommand: GameplayOutboundCommand -> GameplayEffects -> GameplayEffects
