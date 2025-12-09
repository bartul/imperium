namespace Imperium.Contract

// Contract types for cross-bounded-context communication.
// Intentionally public - no .fsi file needed for infrastructure layer.

module Accounting =
    open System
    open Imperium.Primitives

    // Commands
    
    /// Charge a nation for rondel movement. 
    type ChargeNationForRondelMovement = ChargeNationForRondelMovementCommand -> Result<unit, string>
    and ChargeNationForRondelMovementCommand = { GameId: Guid; Nation: string; Amount: Amount; BillingId: Guid }

    // Events
    /// Integration events published by Accounting domain to notify other bounded contexts of payment outcomes.
    type AccountingEvent =
        | RondelInvoicePaid of RondelInvoicePaid
        | RondelInvoicePaymentFailed of RondelInvoicePaymentFailed
    /// Invoice payment succeeded. 
    and RondelInvoicePaid = { GameId: Guid; BillingId: Guid }
    /// Invoice payment failed due to insufficient funds or validation error. 
    and RondelInvoicePaymentFailed = { GameId: Guid; BillingId: Guid }
        
module Rondel =
    open System

    // Commands

    /// Initialize rondel positions for all nations in a game. Called once at game start.
    type SetToStartingPositions = SetToStartingPositionsCommand -> Result<unit, string>
    and SetToStartingPositionsCommand = { GameId: Guid; Nations: string array }

    /// Move a nation to a rondel space. Determines cost and may invoke Accounting dependency.
    type Move = MoveCommand -> Result<unit, string>
    and MoveCommand = { GameId: Guid; Nation: string; Space: string }

    // Events
    /// Integration events published by Rondel bounded context to inform other domains of Rondel movement changes.
    type RondelEvent =
        | PositionedAtStart of PositionedAtStart
        | ActionDetermined of ActionDetermined
        | MoveToActionSpaceRejected of MoveToActionSpaceRejected
    /// Nations positioned at starting positions, rondel ready for movement commands.
    and PositionedAtStart = { GameId: Guid }
    /// Nation successfully moved to a space and the corresponding action was determined.
    and ActionDetermined = { GameId: Guid; Nation: string; Action: string }
    /// Nation's movement rejected due to payment failure.
    and MoveToActionSpaceRejected = { GameId: Guid; Nation: string; Space: string }

module Gameplay =
    // Placeholder module for future Gameplay contract types.
    // Will contain commands, events, and function types for game-level coordination.
    let Foo = "Bar"