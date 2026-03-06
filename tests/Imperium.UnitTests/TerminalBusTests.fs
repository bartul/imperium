module Imperium.UnitTests.TerminalBusTests

open System.Threading
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
        [ testCase "publish invokes subscribed handler"
          <| fun _ ->
              let bus = Bus.create ()
              let received = ResizeArray<TestEventA>()

              bus.Subscribe<TestEventA>(fun e -> async { received.Add e })
              bus.Publish { Value = 42 } |> Async.RunSynchronously

              Expect.equal received.Count 1 "handler should be called once"
              Expect.equal received.[0].Value 42 "handler should receive event"

          testCase "publish invokes multiple subscribers"
          <| fun _ ->
              let bus = Bus.create ()
              let received1 = ResizeArray<TestEventA>()
              let received2 = ResizeArray<TestEventA>()

              bus.Subscribe<TestEventA>(fun e -> async { received1.Add e })
              bus.Subscribe<TestEventA>(fun e -> async { received2.Add e })
              bus.Publish { Value = 1 } |> Async.RunSynchronously

              Expect.equal received1.Count 1 "first handler should be called"
              Expect.equal received2.Count 1 "second handler should be called"

          testCase "publish with no subscribers succeeds"
          <| fun _ ->
              let bus = Bus.create ()

              // Should not throw
              bus.Publish { Value = 99 } |> Async.RunSynchronously

          testCase "different event types are isolated"
          <| fun _ ->
              let bus = Bus.create ()
              let receivedA = ResizeArray<TestEventA>()
              let receivedB = ResizeArray<TestEventB>()

              bus.Subscribe<TestEventA>(fun e -> async { receivedA.Add e })
              bus.Subscribe<TestEventB>(fun e -> async { receivedB.Add e })

              bus.Publish { Value = 1 } |> Async.RunSynchronously

              Expect.equal receivedA.Count 1 "type A handler should be called"
              Expect.equal receivedB.Count 0 "type B handler should not be called"

          testCase "failing subscriber does not block later subscribers"
          <| fun _ ->
              let bus = Bus.create ()
              let received = ResizeArray<string>()

              bus.Subscribe<TestEventA>(fun _ ->
                  async {
                      received.Add("failing")
                      failwith "boom"
                  })

              bus.Subscribe<TestEventA>(fun _ -> async { received.Add("healthy") })

              bus.Publish { Value = 7 } |> Async.RunSynchronously

              Expect.containsAll received [ "failing"; "healthy" ] "all subscribers should be invoked"

          testCase "subscriber added during publish only affects later publishes"
          <| fun _ ->
              let bus = Bus.create ()
              let enteredPublish = new ManualResetEventSlim(false)
              let releasePublish = new ManualResetEventSlim(false)
              let received = ResizeArray<string>()

              bus.Subscribe<TestEventA>(fun event ->
                  async {
                      received.Add($"initial:{event.Value}")

                      if event.Value = 1 then
                          enteredPublish.Set()
                          releasePublish.Wait()
                  })

              let publishTask = bus.Publish { Value = 1 } |> Async.StartAsTask

              enteredPublish.Wait()

              bus.Subscribe<TestEventA>(fun event -> async { received.Add($"new:{event.Value}") })

              releasePublish.Set()
              publishTask.Wait()

              bus.Publish { Value = 2 } |> Async.RunSynchronously

              Expect.equal
                  (received |> Seq.filter ((=) "new:1") |> Seq.length)
                  0
                  "new subscriber should not observe the in-flight publish"

              Expect.equal
                  (received |> Seq.filter ((=) "initial:1") |> Seq.length)
                  1
                  "initial subscriber should observe the in-flight publish"

              Expect.equal
                  (received |> Seq.filter ((=) "initial:2") |> Seq.length)
                  1
                  "initial subscriber should observe later publishes"

              Expect.equal
                  (received |> Seq.filter ((=) "new:2") |> Seq.length)
                  1
                  "new subscriber should observe later publishes" ]
