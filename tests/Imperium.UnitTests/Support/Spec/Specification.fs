namespace Imperium.Testing.Spec

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

// ────────────────────────────────────────────────────────────────────────────────
// Specification Module
// ────────────────────────────────────────────────────────────────────────────────

module Specification =
    let spec<'ctx, 'seed, 'cmd, 'evt> name =
        SpecificationBuilder<'ctx, 'seed, 'cmd, 'evt>(name, None)

    let specOn<'ctx, 'seed, 'cmd, 'evt> (contextFactory: unit -> 'ctx) name =
        SpecificationBuilder<'ctx, 'seed, 'cmd, 'evt>(name, Some contextFactory)
