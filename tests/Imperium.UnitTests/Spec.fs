module Imperium.UnitTests.Spec

open System.Runtime.ExceptionServices
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

/// Expectation - assertion function that throws on failure
type Expectation<'ctx> = { Description: string; Assert: 'ctx -> unit }

// ────────────────────────────────────────────────────────────────────────────────
// Execution Result Types
// ────────────────────────────────────────────────────────────────────────────────

type ExpectationOutcome =
    | Passed
    | Failed of exn

type ExpectationRunResult<'state> =
    { Description: string; InitialState: 'state option; FinalState: 'state option; Outcome: ExpectationOutcome }

// ────────────────────────────────────────────────────────────────────────────────
// Specification
// ────────────────────────────────────────────────────────────────────────────────

type Specification<'ctx, 'seed, 'cmd, 'evt> =
    {
        Name: string
        On: unit -> 'ctx
        GivenState: 'seed option
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

type SpecificationBuilder<'ctx, 'seed, 'cmd, 'evt>(name, defaultOn: (unit -> 'ctx) option) =
    member _.Yield _ =
        { Name = name
          On = defaultOn |> Option.defaultValue (fun () -> Unchecked.defaultof<_>)
          GivenState = None
          GivenActions = []
          Preserve = false
          Actions = []
          Expectations = [] }

    [<CustomOperation("on")>]
    member _.On(spec, setup) = { spec with On = setup }

    [<CustomOperation("state")>]
    member _.State(spec, state) = { spec with GivenState = Some state }

    [<CustomOperation("given_command")>]
    member _.GivenCommand(spec, cmd: 'cmd) =
        { spec with GivenActions = spec.GivenActions @ [ Execute cmd ] }

    [<CustomOperation("given_event")>]
    member _.GivenEvent(spec, evt: 'evt) =
        { spec with GivenActions = spec.GivenActions @ [ Handle evt ] }

    [<CustomOperation("preserve")>]
    member _.Preserve spec = { spec with Preserve = true }

    [<CustomOperation("when_command")>]
    member _.WhenCommand(spec, cmd: 'cmd) =
        { spec with Actions = spec.Actions @ [ Execute cmd ] }

    [<CustomOperation("when_event")>]
    member _.WhenEvent(spec, evt: 'evt) =
        { spec with Actions = spec.Actions @ [ Handle evt ] }

    [<CustomOperation("expect")>]
    member _.Expect(spec, description, assertion: 'ctx -> unit) =
        { spec with Expectations = spec.Expectations @ [ { Description = description; Assert = assertion } ] }

let spec<'ctx, 'seed, 'cmd, 'evt> name =
    SpecificationBuilder<'ctx, 'seed, 'cmd, 'evt>(name, None)

let specOn<'ctx, 'seed, 'cmd, 'evt> (contextFactory: unit -> 'ctx) name =
    SpecificationBuilder<'ctx, 'seed, 'cmd, 'evt>(name, Some contextFactory)

module Specification =
    /// Add state seed outside CE definition.
    let withGivenState
        (state: 'seed)
        (specification: Specification<'ctx, 'seed, 'cmd, 'evt>)
        : Specification<'ctx, 'seed, 'cmd, 'evt> =
        { specification with GivenState = Some state }

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
// Collection Assertions
// ────────────────────────────────────────────────────────────────────────────────

module CollectionAssert =
    type Accessor<'ctx, 'item> =
        { Has: 'item -> string -> 'ctx -> unit
          HasAny: ('item -> bool) -> string -> 'ctx -> unit
          HasNone: ('item -> bool) -> string -> 'ctx -> unit
          Count: 'item -> int -> string -> 'ctx -> unit
          HasSize: int -> string -> 'ctx -> unit }

    let forAccessor (accessor: 'ctx -> seq<'item>) : Accessor<'ctx, 'item> =
        { Has = fun item message ctx -> Expect.isTrue (accessor ctx |> Seq.exists (fun x -> x = item)) message
          HasAny = fun predicate message ctx -> Expect.isTrue (accessor ctx |> Seq.exists predicate) message
          HasNone = fun predicate message ctx -> Expect.isFalse (accessor ctx |> Seq.exists predicate) message
          Count =
            fun item n message ctx ->
                Expect.equal (accessor ctx |> Seq.filter (fun x -> x = item) |> Seq.length) n message
          HasSize = fun n message ctx -> Expect.equal (accessor ctx |> Seq.length) n message }

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
