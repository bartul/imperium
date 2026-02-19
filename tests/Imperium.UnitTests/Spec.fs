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

// ────────────────────────────────────────────────────────────────────────────────
// Expectation
// ────────────────────────────────────────────────────────────────────────────────

/// Expectation - just context predicate (state is for runner reporting only)
type Expectation<'ctx> = { Description: string; Predicate: 'ctx -> bool }

// ────────────────────────────────────────────────────────────────────────────────
// Specification
// ────────────────────────────────────────────────────────────────────────────────

type Specification<'ctx, 'seed, 'cmd, 'evt> =
    {
        Name: string
        On: unit -> 'ctx
        State: 'seed option
        /// Setup actions that run in the on-step before when_.
        GivenActions: Action<'cmd, 'evt> list
        /// Keep events/commands produced by setup actions.
        Preserve: bool
        Actions: Action<'cmd, 'evt> list
        Expectations: Expectation<'ctx> list
    }

// ────────────────────────────────────────────────────────────────────────────────
// CE Builder
// ────────────────────────────────────────────────────────────────────────────────

type SpecificationBuilder<'ctx, 'seed, 'cmd, 'evt>(name) =
    member _.Yield _ =
        { Name = name
          On = (fun () -> Unchecked.defaultof<_>)
          State = None
          GivenActions = []
          Preserve = false
          Actions = []
          Expectations = [] }

    [<CustomOperation("on")>]
    member _.On(spec, setup) = { spec with On = setup }

    [<CustomOperation("state")>]
    member _.State(spec, state) = { spec with State = Some state }

    [<CustomOperation("actions")>]
    member _.Actions(spec, actions) = { spec with GivenActions = actions }

    [<CustomOperation("preserve")>]
    member _.Preserve(spec) = { spec with Preserve = true }

    [<CustomOperation("when_")>]
    member _.When(spec, actions) = { spec with Actions = actions }

    [<CustomOperation("expect")>]
    member _.Expect(spec, description, predicate) =
        { spec with Expectations = spec.Expectations @ [ { Description = description; Predicate = predicate } ] }

let spec<'ctx, 'seed, 'cmd, 'evt> name =
    SpecificationBuilder<'ctx, 'seed, 'cmd, 'evt>(name)

module Specification =
    /// Add state seed outside CE definition.
    let withState
        (state: 'seed)
        (specification: Specification<'ctx, 'seed, 'cmd, 'evt>)
        : Specification<'ctx, 'seed, 'cmd, 'evt> =
        { specification with State = Some state }

    /// Add setup actions outside CE definition.
    let withActions
        (actions: Action<'cmd, 'evt> list)
        (specification: Specification<'ctx, 'seed, 'cmd, 'evt>)
        : Specification<'ctx, 'seed, 'cmd, 'evt> =
        { specification with GivenActions = actions }

    /// Preserve setup side effects outside CE definition.
    let preserve (specification: Specification<'ctx, 'seed, 'cmd, 'evt>) : Specification<'ctx, 'seed, 'cmd, 'evt> =
        { specification with Preserve = true }

// ────────────────────────────────────────────────────────────────────────────────
// Runner Interface
// ────────────────────────────────────────────────────────────────────────────────

/// Runner interface - context-specific execution + state capture
type ISpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt> =
    abstract Execute: 'ctx -> 'cmd -> unit
    abstract Handle: 'ctx -> 'evt -> unit
    abstract ClearEvents: 'ctx -> unit
    abstract ClearCommands: 'ctx -> unit
    abstract SeedState: 'ctx -> 'seed -> unit
    abstract SeedFor: Specification<'ctx, 'seed, 'cmd, 'evt> -> 'seed option
    abstract CaptureState: 'ctx -> 'state // For runner reporting, not expectations

// ────────────────────────────────────────────────────────────────────────────────
// Runner Helpers
// ────────────────────────────────────────────────────────────────────────────────

/// Run all actions on context using provided runner
let runActions (runner: ISpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>) ctx actions =
    for action in actions do
        match action with
        | Execute cmd -> runner.Execute ctx cmd
        | Handle evt -> runner.Handle ctx evt

/// Build context and run optional setup phases before when_.
let prepareContext
    (runner: ISpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>)
    (specification: Specification<'ctx, 'seed, 'cmd, 'evt>)
    =
    let context = specification.On()

    let seedState =
        specification.State
        |> Option.orElseWith (fun () -> runner.SeedFor specification)

    seedState |> Option.iter (runner.SeedState context)

    runActions runner context specification.GivenActions

    if not specification.Preserve then
        runner.ClearEvents context
        runner.ClearCommands context

    context

/// Convert Specification to Expecto testList where each expectation is its own testCase.
/// Each testCase runs the full on/when_ sequence independently for isolation.
let toExpecto
    (runner: ISpecRunner<'ctx, 'seed, 'state, 'cmd, 'evt>)
    (specification: Specification<'ctx, 'seed, 'cmd, 'evt>)
    =
    let expectationTests =
        specification.Expectations
        |> List.map (fun expectation ->
            testCase expectation.Description
            <| fun _ ->
                let ctx = prepareContext runner specification
                let _initialState = runner.CaptureState ctx
                runActions runner ctx specification.Actions
                let _finalState = runner.CaptureState ctx
                let passed = expectation.Predicate ctx
                Expect.isTrue passed expectation.Description)

    testList specification.Name expectationTests
