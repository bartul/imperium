namespace Imperium.Rondel

/// Internal pure module for the SetToStartingPositions command.
module internal SetToStartingPositions =
    let execute
        (command: SetToStartingPositionsCommand)
        (state: RondelState option)
        : RondelState option * RondelEvent list * RondelOutboundCommand list =
        match state with
        | Some _ -> None, [], [] // Already initialized, no-op
        | None ->
            let newState = RondelState.create command.GameId command.Nations

            let positionedAtStartEvent = PositionedAtStart { GameId = command.GameId }

            Some newState, [ positionedAtStartEvent ], []

/// Internal command and event handlers. Each follows the load -> pure execute -> effects pattern.
module internal Handlers =

    /// Lift a pure handler's (state, events, commands) tuple into the named effect record.
    let private toEffects (state, events, commands) : RondelEffects =
        { State = state; IntegrationEvents = events; OutboundCommands = commands }

    let setToStartingPositions (load: LoadRondelState) (command: SetToStartingPositionsCommand) : Async<RondelEffects> =
        async {
            let! state = load command.GameId
            return SetToStartingPositions.execute command state |> toEffects
        }

    let move (load: LoadRondelState) (command: MoveCommand) : Async<RondelEffects> =
        async {
            let! state = load command.GameId
            return Movement.execute command state |> toEffects
        }

    let onInvoicePaid (load: LoadRondelState) (event: InvoicePaidInboundEvent) : Async<RondelEffects> =
        async {
            let! state = load event.GameId
            return OnInvoicePaid.handle event state |> toEffects
        }

    let onInvoicePaymentFailed
        (load: LoadRondelState)
        (event: InvoicePaymentFailedInboundEvent)
        : Async<RondelEffects> =
        async {
            let! state = load event.GameId
            return OnInvoicePaymentFailed.handle event state |> toEffects
        }
