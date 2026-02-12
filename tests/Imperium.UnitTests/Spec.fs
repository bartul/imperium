module Imperium.UnitTests.Spec

open Expecto

// ────────────────────────────────────────────────────────────────────────────────
// Marker Types
// ────────────────────────────────────────────────────────────────────────────────

/// Marker type for contexts with no state (F# doesn't allow unit as generic return type)
type NoState = NoState

// ────────────────────────────────────────────────────────────────────────────────
// Action DU
// ────────────────────────────────────────────────────────────────────────────────

/// Action that can be performed on a context
type Action<'cmd, 'evt> =
    | Execute of 'cmd
    | Handle of 'evt
    | ClearEvents
    | ClearCommands

// ────────────────────────────────────────────────────────────────────────────────
// Expectation
// ────────────────────────────────────────────────────────────────────────────────

/// Expectation - just context predicate (state is for runner reporting only)
type Expectation<'ctx> = { Description: string; Predicate: 'ctx -> bool }

// ────────────────────────────────────────────────────────────────────────────────
// Specification
// ────────────────────────────────────────────────────────────────────────────────

type Specification<'ctx, 'cmd, 'evt> =
    { Name: string; On: unit -> 'ctx; Actions: Action<'cmd, 'evt> list; Expectations: Expectation<'ctx> list }

// ────────────────────────────────────────────────────────────────────────────────
// CE Builder
// ────────────────────────────────────────────────────────────────────────────────

type SpecificationBuilder<'ctx, 'cmd, 'evt>(name) =
    member _.Yield _ =
        { Name = name; On = (fun () -> Unchecked.defaultof<_>); Actions = []; Expectations = [] }

    [<CustomOperation("on")>]
    member _.On(spec, setup) = { spec with On = setup }

    [<CustomOperation("when_")>]
    member _.When(spec, actions) = { spec with Actions = actions }

    [<CustomOperation("expect")>]
    member _.Expect(spec, description, predicate) =
        { spec with Expectations = spec.Expectations @ [ { Description = description; Predicate = predicate } ] }

let spec<'ctx, 'cmd, 'evt> name =
    SpecificationBuilder<'ctx, 'cmd, 'evt>(name)

// ────────────────────────────────────────────────────────────────────────────────
// Runner Interface
// ────────────────────────────────────────────────────────────────────────────────

/// Runner interface - context-specific execution + state capture
type ISpecRunner<'ctx, 'state, 'cmd, 'evt> =
    abstract Execute: 'ctx -> 'cmd -> unit
    abstract Handle: 'ctx -> 'evt -> unit
    abstract ClearEvents: 'ctx -> unit
    abstract ClearCommands: 'ctx -> unit
    abstract CaptureState: 'ctx -> 'state // For runner reporting, not expectations

// ────────────────────────────────────────────────────────────────────────────────
// Runner Helpers
// ────────────────────────────────────────────────────────────────────────────────

/// Run all actions on context using provided runner
let runActions (runner: ISpecRunner<'ctx, 'state, 'cmd, 'evt>) ctx actions =
    for action in actions do
        match action with
        | Execute cmd -> runner.Execute ctx cmd
        | Handle evt -> runner.Handle ctx evt
        | ClearEvents -> runner.ClearEvents ctx
        | ClearCommands -> runner.ClearCommands ctx

/// Convert Specification to Expecto testList where each expectation is its own testCase.
/// Each testCase runs the full on/when_ sequence independently for isolation.
let toExpecto (runner: ISpecRunner<'ctx, 'state, 'cmd, 'evt>) (spec: Specification<'ctx, 'cmd, 'evt>) =
    let expectationTests =
        spec.Expectations
        |> List.map (fun expectation ->
            testCase expectation.Description
            <| fun _ ->
                let ctx = spec.On()
                let _initialState = runner.CaptureState ctx
                runActions runner ctx spec.Actions
                let _finalState = runner.CaptureState ctx
                let passed = expectation.Predicate ctx
                Expect.isTrue passed expectation.Description)

    testList spec.Name expectationTests
