namespace Imperium.Gameplay

open Imperium
// ──────────────────────────────────────────────────────────────────────────
// Outbound Commands
// ──────────────────────────────────────────────────────────────────────────

type GameplayOutboundCommand = SetRondelToStartingPositions of SetRondelToStartingPositionsOutboundCommand

and SetRondelToStartingPositionsOutboundCommand = { GameId: GameId; Nations: Set<NationId> }

module SetRondelToStartingPositionsOutboundCommand =
    let toContract
        (command: SetRondelToStartingPositionsOutboundCommand)
        : Contract.Rondel.SetToStartingPositionsCommand =
        { GameId = command.GameId |> GameId.value
          Nations = command.Nations |> Set.toArray |> Array.map NationId.toString }
