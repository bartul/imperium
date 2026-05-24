module Imperium.Testing.Spec.Runner

open System.Runtime.ExceptionServices
open Expecto
open Imperium.Testing.Spec.Specification

// ────────────────────────────────────────────────────────────────────────────────
// Runner Record
// ────────────────────────────────────────────────────────────────────────────────

/// Runner record - context-specific execution + optional state capture
type SpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt> =
    { Execute: 'ctx -> 'cmd -> unit
      Handle: 'ctx -> 'evt -> unit
      ClearEvents: 'ctx -> unit
      ClearCommands: 'ctx -> unit
      SeedState: 'ctx -> 'seed -> unit
      CaptureState: ('ctx -> 'state) option
      FormatState: ('state -> string) option }

module SpecRunner =
    let empty<'ctx, 'seed, 'state, 'cmd, 'evt> : SpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt> =
        { Execute = fun _ _ -> ()
          Handle = fun _ _ -> ()
          ClearEvents = fun _ -> ()
          ClearCommands = fun _ -> ()
          SeedState = fun _ _ -> ()
          CaptureState = None
          FormatState = None }

// ────────────────────────────────────────────────────────────────────────────────
// Runner Helpers
// ────────────────────────────────────────────────────────────────────────────────

/// Run all actions on context using provided runner
let runActions (runner: SpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>) ctx actions =
    for action in actions do
        match action with
        | Execute cmd -> runner.Execute ctx cmd
        | Handle evt -> runner.Handle ctx evt

/// Build context and run optional setup phases before when_.
let prepareContext
    (runner: SpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>)
    (specification: Specification<'ctx, 'seed, 'cmd, 'evt>)
    =
    let context = specification.On()
    specification.GivenState |> Option.iter (runner.SeedState context)
    runActions runner context specification.GivenActions

    if not specification.Preserve then
        runner.ClearEvents context
        runner.ClearCommands context

    context

/// Shared execution primitive: runs a single expectation through the full spec flow
/// and captures the outcome without rethrowing.
let runExpectation
    (runner: SpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>)
    (specification: Specification<'ctx, 'seed, 'cmd, 'evt>)
    (expectation: Expectation<'ctx>)
    : ExpectationRunResult<'state> =

    let mutable initialState: 'state option = None
    let mutable finalState: 'state option = None

    let outcome =
        try
            let ctx = prepareContext runner specification
            initialState <- runner.CaptureState |> Option.map (fun capture -> capture ctx)
            runActions runner ctx specification.Actions
            finalState <- runner.CaptureState |> Option.map (fun capture -> capture ctx)
            expectation.Assert ctx
            Passed
        with ex ->
            Failed ex

    { Description = expectation.Description; InitialState = initialState; FinalState = finalState; Outcome = outcome }

/// Convert Specification to Expecto testList where each expectation is its own testCase.
/// Each testCase runs the full on/when_ sequence independently for isolation.
let toExpecto
    (runner: SpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>)
    (specification: Specification<'ctx, 'seed, 'cmd, 'evt>)
    =
    let expectationTests =
        specification.Expectations
        |> List.map (fun expectation ->
            testCase expectation.Description
            <| fun _ ->
                let result = runExpectation runner specification expectation

                match result.Outcome with
                | Passed -> ()
                | Failed ex -> ExceptionDispatchInfo.Capture(ex).Throw())

    testList specification.Name expectationTests
