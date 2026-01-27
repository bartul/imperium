module Imperium.UnitTests.TerminalBusTests

open Expecto
open Imperium.Terminal

// ──────────────────────────────────────────────────────────────────────────
// Test Types
// ──────────────────────────────────────────────────────────────────────────

type TestEventA = { Value: int }
type TestEventB = { Message: string }

// ──────────────────────────────────────────────────────────────────────────
// Tests
// ──────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList
        "Terminal.Bus"
        [ ptestCase "publish invokes subscribed handler"
          <| fun _ ->
              let bus = Bus.create ()
              let received = ResizeArray<TestEventA>()

              bus.Subscribe<TestEventA>(fun e -> async { received.Add e })
              bus.Publish { Value = 42 } |> Async.RunSynchronously

              Expect.equal received.Count 1 "handler should be called once"
              Expect.equal received.[0].Value 42 "handler should receive event"

          ptestCase "publish invokes multiple subscribers"
          <| fun _ ->
              let bus = Bus.create ()
              let received1 = ResizeArray<TestEventA>()
              let received2 = ResizeArray<TestEventA>()

              bus.Subscribe<TestEventA>(fun e -> async { received1.Add e })
              bus.Subscribe<TestEventA>(fun e -> async { received2.Add e })
              bus.Publish { Value = 1 } |> Async.RunSynchronously

              Expect.equal received1.Count 1 "first handler should be called"
              Expect.equal received2.Count 1 "second handler should be called"

          ptestCase "publish with no subscribers succeeds"
          <| fun _ ->
              let bus = Bus.create ()

              // Should not throw
              bus.Publish { Value = 99 } |> Async.RunSynchronously

          ptestCase "different event types are isolated"
          <| fun _ ->
              let bus = Bus.create ()
              let receivedA = ResizeArray<TestEventA>()
              let receivedB = ResizeArray<TestEventB>()

              bus.Subscribe<TestEventA>(fun e -> async { receivedA.Add e })
              bus.Subscribe<TestEventB>(fun e -> async { receivedB.Add e })

              bus.Publish { Value = 1 } |> Async.RunSynchronously

              Expect.equal receivedA.Count 1 "type A handler should be called"
              Expect.equal receivedB.Count 0 "type B handler should not be called" ]
