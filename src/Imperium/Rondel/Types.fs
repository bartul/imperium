namespace Imperium.Rondel

open Imperium
open Imperium.Primitives
open FsToolkit.ErrorHandling

// ──────────────────────────────────────────────────────────────────────────
// Value Types & Enumerations
// ──────────────────────────────────────────────────────────────────────────

[<Struct>]
type RondelBillingId = private RondelBillingId of Id

module RondelBillingId =
    let create = Id.createMap RondelBillingId
    let newId () = Id.newId () |> RondelBillingId
    let value (RondelBillingId g) = g |> Id.value
    let toString (RondelBillingId g) = g |> Id.toString
    let tryParse = Id.tryParseMap RondelBillingId
    let ofId (id: Id) = RondelBillingId id

[<RequireQualifiedAccess>]
type Action =
    | Investor
    | Import
    | Production
    | Maneuver
    | Taxation
    | Factory

module Action =
    let toString action =
        match action with
        | Action.Investor -> "Investor"
        | Action.Import -> "Import"
        | Action.Production -> "Production"
        | Action.Maneuver -> "Maneuver"
        | Action.Taxation -> "Taxation"
        | Action.Factory -> "Factory"

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

module Space =
    /// Spaces in clockwise board order for distance calculation.
    let private spacesInOrder =
        [| Space.Investor
           Space.Import
           Space.ProductionOne
           Space.ManeuverOne
           Space.Taxation
           Space.Factory
           Space.ProductionTwo
           Space.ManeuverTwo |]

    /// Calculate clockwise distance between two spaces.
    let distance fromSpace toSpace =
        let fromIndex = Array.findIndex ((=) fromSpace) spacesInOrder
        let toIndex = Array.findIndex ((=) toSpace) spacesInOrder

        if toIndex >= fromIndex then
            toIndex - fromIndex
        else
            (Array.length spacesInOrder - fromIndex) + toIndex

    let toString space =
        match space with
        | Space.Investor -> "Investor"
        | Space.Import -> "Import"
        | Space.ProductionOne -> "ProductionOne"
        | Space.ManeuverOne -> "ManeuverOne"
        | Space.Taxation -> "Taxation"
        | Space.Factory -> "Factory"
        | Space.ProductionTwo -> "ProductionTwo"
        | Space.ManeuverTwo -> "ManeuverTwo"

    let fromString s =
        match s with
        | "Investor" -> Ok Space.Investor
        | "Import" -> Ok Space.Import
        | "ProductionOne" -> Ok Space.ProductionOne
        | "ManeuverOne" -> Ok Space.ManeuverOne
        | "Taxation" -> Ok Space.Taxation
        | "Factory" -> Ok Space.Factory
        | "ProductionTwo" -> Ok Space.ProductionTwo
        | "ManeuverTwo" -> Ok Space.ManeuverTwo
        | _ -> Error $"Invalid rondel space: {s}"

    let toAction space =
        match space with
        | Space.Investor -> Action.Investor
        | Space.Import -> Action.Import
        | Space.ProductionOne
        | Space.ProductionTwo -> Action.Production
        | Space.ManeuverOne
        | Space.ManeuverTwo -> Action.Maneuver
        | Space.Taxation -> Action.Taxation
        | Space.Factory -> Action.Factory

// ──────────────────────────────────────────────────────────────────────────
// Commands
// ──────────────────────────────────────────────────────────────────────────

type RondelCommand =
    | SetToStartingPositions of SetToStartingPositionsCommand
    | Move of MoveCommand

and SetToStartingPositionsCommand = { GameId: Id; Nations: Set<string> }

and MoveCommand = { GameId: Id; Nation: string; Space: Space }

module SetToStartingPositionsCommand =
    let fromContract (command: Contract.Rondel.SetToStartingPositionsCommand) =
        result {
            let! id = Id.create command.GameId
            let nations = Set.ofArray command.Nations

            if Set.isEmpty nations then
                return! Error "Starting positions require at least one nation."
            else
                return { GameId = id; Nations = nations }
        }

module MoveCommand =
    let fromContract (command: Contract.Rondel.MoveCommand) : Result<MoveCommand, string> =
        result {
            let! id = Id.create command.GameId
            let! space = Space.fromString command.Space

            return { GameId = id; Nation = command.Nation; Space = space }
        }

// ──────────────────────────────────────────────────────────────────────────
// Events
// ──────────────────────────────────────────────────────────────────────────

type RondelEvent =
    | PositionedAtStart of PositionedAtStartEvent
    | ActionDetermined of ActionDeterminedEvent
    | MoveToActionSpaceRejected of MoveToActionSpaceRejectedEvent

and PositionedAtStartEvent = { GameId: Id }

and ActionDeterminedEvent = { GameId: Id; Nation: string; Action: Action }

and MoveToActionSpaceRejectedEvent = { GameId: Id; Nation: string; Space: Space }

module RondelEvent =
    let toContract event =
        match event with
        | PositionedAtStart e -> Contract.Rondel.PositionedAtStart { GameId = Id.value e.GameId }
        | ActionDetermined e ->
            Contract.Rondel.ActionDetermined
                { GameId = Id.value e.GameId; Nation = e.Nation; Action = Action.toString e.Action }
        | MoveToActionSpaceRejected e ->
            Contract.Rondel.MoveToActionSpaceRejected
                { GameId = Id.value e.GameId; Nation = e.Nation; Space = Space.toString e.Space }

// ──────────────────────────────────────────────────────────────────────────
// Outbound Commands (to other bounded contexts)
// ──────────────────────────────────────────────────────────────────────────

type RondelOutboundCommand =
    | ChargeMovement of ChargeMovementOutboundCommand
    | VoidCharge of VoidChargeOutboundCommand

and ChargeMovementOutboundCommand = { GameId: Id; Nation: string; Amount: Amount; BillingId: RondelBillingId }

and VoidChargeOutboundCommand = { GameId: Id; BillingId: RondelBillingId }

module ChargeMovementOutboundCommand =
    let toContract (cmd: ChargeMovementOutboundCommand) : Contract.Accounting.ChargeNationForRondelMovementCommand =
        { GameId = Id.value cmd.GameId
          Nation = cmd.Nation
          Amount = cmd.Amount
          BillingId = RondelBillingId.value cmd.BillingId }

module VoidChargeOutboundCommand =
    let toContract (cmd: VoidChargeOutboundCommand) : Contract.Accounting.VoidRondelChargeCommand =
        { GameId = Id.value cmd.GameId; BillingId = RondelBillingId.value cmd.BillingId }

// ──────────────────────────────────────────────────────────────────────────
// Incoming Events (from other bounded contexts)
// ──────────────────────────────────────────────────────────────────────────

type RondelInboundEvent =
    | InvoicePaid of InvoicePaidInboundEvent
    | InvoicePaymentFailed of InvoicePaymentFailedInboundEvent

and InvoicePaidInboundEvent = { GameId: Id; BillingId: RondelBillingId }

and InvoicePaymentFailedInboundEvent = { GameId: Id; BillingId: RondelBillingId }

module InvoicePaidInboundEvent =
    let fromContract (event: Contract.Accounting.RondelInvoicePaid) : Result<InvoicePaidInboundEvent, string> =
        result {
            let! gameId = Id.create event.GameId
            let! billingId = RondelBillingId.create event.BillingId

            return { GameId = gameId; BillingId = billingId }
        }

module InvoicePaymentFailedInboundEvent =
    let fromContract
        (event: Contract.Accounting.RondelInvoicePaymentFailed)
        : Result<InvoicePaymentFailedInboundEvent, string> =
        result {
            let! gameId = Id.create event.GameId
            let! billingId = RondelBillingId.create event.BillingId

            return { GameId = gameId; BillingId = billingId }
        }

// ──────────────────────────────────────────────────────────────────────────
// Queries
// ──────────────────────────────────────────────────────────────────────────

type GetNationPositionsQuery = { GameId: Id }

type GetRondelOverviewQuery = { GameId: Id }

// ──────────────────────────────────────────────────────────────────────────
// Query Results
// ──────────────────────────────────────────────────────────────────────────

type RondelPositionsView = { GameId: Id; Positions: NationPositionView list }

and NationPositionView = { Nation: string; CurrentSpace: Space option; PendingSpace: Space option }

type RondelView = { GameId: Id; NationNames: string list }
