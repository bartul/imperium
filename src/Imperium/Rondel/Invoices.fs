namespace Imperium.Rondel

/// Internal pure module for invoice-payment-confirmation handling.
module internal OnInvoicePaid =
    let private failIfNotInitialized (state: RondelState option, event: InvoicePaidInboundEvent) =
        match state with
        | Some s -> s, event
        | None -> failwith "Rondel not initialized for game."

    let private registerPaymentAndCompleteMovement (state: RondelState, event: InvoicePaidInboundEvent) =
        let pendingMovement =
            state.PendingMovements
            |> Map.toSeq
            |> Seq.tryFind (fun (_, pm) -> pm.BillingId = event.BillingId)
            |> Option.map snd

        let emptyCommands: RondelOutboundCommand list = []

        match pendingMovement with
        | None ->
            // No pending movement found - idempotent handling for duplicates or already completed/voided.
            None, [], emptyCommands
        | Some pending ->
            let action = Space.toAction pending.TargetSpace

            let newState =
                state
                |> RondelState.withNationPosition pending.Nation pending.TargetSpace
                |> RondelState.withoutPendingMove pending.Nation

            let actionDeterminedEvent =
                ActionDetermined { GameId = state.GameId; Nation = pending.Nation; Action = action }

            Some newState, [ actionDeterminedEvent ], emptyCommands

    let handle event state =
        failIfNotInitialized (state, event) |> registerPaymentAndCompleteMovement

/// Internal pure module for invoice-payment-failure handling.
module internal OnInvoicePaymentFailed =
    let private failIfNotInitialized (state: RondelState option, event: InvoicePaymentFailedInboundEvent) =
        match state with
        | Some s -> s, event
        | None -> failwith "Rondel not initialized for game."

    let handle
        (event: InvoicePaymentFailedInboundEvent)
        (state: RondelState option)
        : RondelState option * RondelEvent list * RondelOutboundCommand list =
        let state, _ = failIfNotInitialized (state, event)

        let pendingMovement =
            state.PendingMovements
            |> Map.toSeq
            |> Seq.tryFind (fun (_, pending) -> pending.BillingId = event.BillingId)
            |> Option.map snd

        match pendingMovement with
        | None ->
            // No pending movement found - idempotent handling for duplicates or already completed/voided.
            None, [], []
        | Some pending ->
            let newState = state |> RondelState.withoutPendingMove pending.Nation

            let moveRejectedEvent =
                MoveToActionSpaceRejected
                    { GameId = state.GameId; Nation = pending.Nation; Space = pending.TargetSpace }

            Some newState, [ moveRejectedEvent ], []
