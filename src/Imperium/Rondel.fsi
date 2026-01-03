namespace Imperium

open System
open Imperium.Primitives
open Imperium.Contract.Accounting
open Imperium.Contract.Rondel

module Rondel =

    // State DTOs for persistence
    module Dto =
        /// A movement pending payment confirmation from Accounting domain.
        /// Rondel state for a game - tracks nation positions and pending movements.
        type RondelState =
            { GameId: Guid
              NationPositions: Map<string, string option>
              PendingMovements: Map<string, PendingMovement> }

        and PendingMovement =
            { Nation: string
              TargetSpace: string
              BillingId: Guid }

    // Dependency function types

    /// Load Rondel state by GameId. Returns None if game not initialized.
    type LoadRondelState = Guid -> Dto.RondelState option

    /// Save Rondel state. Returns Error if persistence fails.
    type SaveRondelState = Dto.RondelState -> Result<unit, string>

    /// Publishes Rondel integration events to the event bus.
    type PublishRondelEvent = RondelEvent -> unit

    [<RequireQualifiedAccess>]
    type Space =
        | Investor
        | Import
        | ProductionOne
        | ManeuverOne
        | Taxation
        | Factory
        | ProductionTwo
        | ManeuverTwo

    // Commands

    type RondelCommand = 
        | SetToStartingPositions of SetToStartingPositionsCommand
        | Move of MoveCommand
    and SetToStartingPositionsCommand =
        { GameId: Id
          Nations: Set<string> }
    and MoveCommand =
        { GameId: Id
          Nation: string
          Space: Space }

    // Transformation modules: Contract → Domain

    /// Transforms Contract types to Domain types for SetToStartingPositions
    module SetToStartingPositions =
        /// Transform Contract SetToStartingPositionsCommand to Domain SetToStartingPositionsCommand.
        /// Returns Error if GameId is invalid (Guid.Empty).
        val toDomain: Contract.Rondel.SetToStartingPositionsCommand -> Result<SetToStartingPositionsCommand, string>

    /// Transforms Contract types to Domain types for Move
    module Move =
        /// Transform Contract MoveCommand to Domain MoveCommand.
        /// Returns Error if GameId is invalid or Space name is unknown.
        val toDomain: Contract.Rondel.MoveCommand -> Result<MoveCommand, string>

    /// Command: Initialize rondel for the specified game with the given nations.
    /// All nations are positioned at their starting positions.
    /// Publishes PositionedAtStart integration event. Fails if nation set is empty.
    val setToStartingPositions: LoadRondelState -> SaveRondelState -> PublishRondelEvent -> SetToStartingPositionsCommand -> unit

    /// Command: Move a nation to the specified space on the rondel.
    /// Determines movement cost and charges via injected Accounting dependency.
    /// Integration event ActionDetermined published based on payment requirement.
    val move:
        LoadRondelState ->
        SaveRondelState ->
        PublishRondelEvent ->
        ChargeNationForRondelMovement ->
        VoidRondelCharge ->
            MoveCommand -> unit

    // Event handlers

    /// Event handler: Processes invoice payment confirmation from Accounting domain.
    /// Completes the nation's movement and publishes ActionDetermined integration event.
    /// If no pending movement exists for the BillingId, the event is ignored (idempotent behavior).
    val onInvoicedPaid:
        LoadRondelState -> SaveRondelState -> PublishRondelEvent -> RondelInvoicePaid -> Result<unit, string>

    /// Event handler: Processes invoice payment failure from Accounting domain.
    /// Rejects the movement and publishes MoveToActionSpaceRejected integration event.
    val onInvoicePaymentFailed:
        LoadRondelState -> SaveRondelState -> PublishRondelEvent -> RondelInvoicePaymentFailed -> Result<unit, string>
