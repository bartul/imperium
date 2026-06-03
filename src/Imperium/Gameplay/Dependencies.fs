namespace Imperium.Gameplay

open Imperium.Primitives

// ──────────────────────────────────────────────────────────────────────────
// Dependencies
// ──────────────────────────────────────────────────────────────────────────

type LoadGameplayState = GameId -> Async<GameplayState option>

type GameplayEffects =
    { State: GameplayState option
      IntegrationEvents: GameplayEvent list
      OutboundCommands: GameplayOutboundCommand list }

module GameplayEffects =
    let empty = { State = None; IntegrationEvents = []; OutboundCommands = [] }
    let withState state effects = { effects with State = Some state }
    let create state = empty |> withState state

    let withEvent event effects =
        { effects with IntegrationEvents = effects.IntegrationEvents @ [ event ] }

    let withCommand command effects =
        { effects with OutboundCommands = effects.OutboundCommands @ [ command ] }

type CommitGameplayEffects = GameplayEffects -> Async<unit>

type GameplayDependencies = { Load: LoadGameplayState; Commit: CommitGameplayEffects }
