namespace Imperium.Terminal.Rondel

open Imperium.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Direct Commit (terminal sandbox)
// ──────────────────────────────────────────────────────────────────────────

/// Direct (non-durable) commit adapter for RondelEffects.
/// Sequences state save → integration-event publish → outbound-command dispatch.
/// Failures propagate as exceptions, mirroring prior `Rondel.materialize` semantics.
[<RequireQualifiedAccess>]
module RondelDirectCommit =
    let create
        (save: RondelState -> Async<Result<unit, string>>)
        (publish: RondelEvent -> Async<unit>)
        (dispatch: RondelOutboundCommand -> Async<Result<unit, string>>)
        : CommitRondelEffects =
        fun effects ->
            async {
                match effects.State with
                | Some state ->
                    let! result = save state

                    match result with
                    | Error e -> return failwith $"Failed to save state: {e}"
                    | Ok() -> ()
                | None -> ()

                for event in effects.IntegrationEvents do
                    do! publish event

                for command in effects.OutboundCommands do
                    let! result = dispatch command

                    match result with
                    | Error e -> return failwith $"Failed to dispatch command: {e}"
                    | Ok() -> ()
            }
