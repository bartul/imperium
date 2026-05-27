namespace Imperium.Contract

// Contract types for Gameplay bounded context communication.
// Intentionally public - no .fsi file needed for infrastructure layer.

module Gameplay =
    open System

    // Commands

    /// Start a new game lifecycle and begin setup for participating bounded contexts.
    type StartGameCommand = { GameId: Guid; Nations: string array; PlayerIds: Guid array }

    /// Union of all commands that can be dispatched to Gameplay bounded context.
    /// Used for infrastructure routing and dispatch.
    type GameplayCommand = StartGame of StartGameCommand

    // Events

    /// Integration events published by Gameplay bounded context.
    type GameplayEvent = SetupCompleted of SetupCompleted

    /// Game setup completed and play can begin.
    and SetupCompleted = { GameId: Guid }
