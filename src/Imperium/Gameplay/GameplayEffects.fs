namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Effects
// ──────────────────────────────────────────────────────────────────────────

type GameplayEffects =
    { State: GameplayState option
      IntegrationEvents: GameplayEvent list
      OutboundCommands: GameplayOutboundCommand list }

module internal GameplayEffects =
    let none = { State = None; IntegrationEvents = []; OutboundCommands = [] }
    let withState state effects = { effects with State = Some state }
    let create state = none |> withState state

    let withEvent event effects =
        { effects with IntegrationEvents = effects.IntegrationEvents @ [ event ] }

    let withCommand command effects =
        { effects with OutboundCommands = effects.OutboundCommands @ [ command ] }
