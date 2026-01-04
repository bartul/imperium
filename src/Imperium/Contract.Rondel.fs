namespace Imperium.Contract

// Contract types for Rondel bounded context communication.
// Intentionally public - no .fsi file needed for infrastructure layer.

module Rondel =
    open System

    // Commands
    type SetToStartingPositionsCommand = { GameId: Guid; Nations: string array }

    and MoveCommand =
        { GameId: Guid
          Nation: string
          Space: string }

    // Events
    /// Integration events published by Rondel bounded context to inform other domains of Rondel movement changes.
    type RondelEvent =
        | PositionedAtStart of PositionedAtStart
        | ActionDetermined of ActionDetermined
        | MoveToActionSpaceRejected of MoveToActionSpaceRejected

    /// Nations positioned at starting positions, rondel ready for movement commands.
    and PositionedAtStart = { GameId: Guid }

    /// Nation successfully moved to a space and the corresponding action was determined.
    and ActionDetermined =
        { GameId: Guid
          Nation: string
          Action: string }

    /// Nation's movement rejected due to payment failure.
    and MoveToActionSpaceRejected =
        { GameId: Guid
          Nation: string
          Space: string }

    // State for persistence

    type RondelState =
        { GameId: Guid
          NationPositions: Map<string, string option>
          PendingMovements: Map<string, PendingMovement> }

    and PendingMovement =
        { Nation: string
          TargetSpace: string
          BillingId: Guid }
