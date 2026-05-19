module Imperium.UnitTests.RondelDirectCommitTests

open System.Collections.Generic
open Expecto
open Imperium.Primitives
open Imperium.Rondel
open Imperium.Terminal.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Fixtures
// ──────────────────────────────────────────────────────────────────────────

let private sampleGameId = Id.newId ()

let private sampleState: RondelState =
    { GameId = sampleGameId
      NationPositions = Map.ofList [ ("Austria", Some Space.Factory) ]
      PendingMovements = Map.empty }

let private sampleEvent: RondelEvent =
    PositionedAtStart { GameId = sampleGameId }

let private sampleCommand: RondelOutboundCommand =
    VoidCharge
        { GameId = sampleGameId
          BillingId = RondelBillingId.ofId (Id.newId ()) }

let private sampleEffects: RondelEffects =
    { State = Some sampleState
      IntegrationEvents = [ sampleEvent ]
      OutboundCommands = [ sampleCommand ] }

// ──────────────────────────────────────────────────────────────────────────
// Tests
// ──────────────────────────────────────────────────────────────────────────

[<Tests>]
let tests =
    testList
        "Terminal.RondelDirectCommit"
        [ testCase "saves state, then publishes events, then dispatches commands"
          <| fun _ ->
              let log = List<string>()

              let save _ =
                  async {
                      log.Add "save"
                      return Ok()
                  }

              let publish _ = async { log.Add "publish" }

              let dispatch _ =
                  async {
                      log.Add "dispatch"
                      return Ok()
                  }

              let commit = RondelDirectCommit.create save publish dispatch
              commit sampleEffects |> Async.RunSynchronously

              Expect.sequenceEqual log [ "save"; "publish"; "dispatch" ] "commit ordering"

          testCase "save Error stops the pipeline and skips publish and dispatch"
          <| fun _ ->
              let log = List<string>()

              let save _ = async { return Error "disk full" }
              let publish _ = async { log.Add "publish" }

              let dispatch _ =
                  async {
                      log.Add "dispatch"
                      return Ok()
                  }

              let commit = RondelDirectCommit.create save publish dispatch

              Expect.throws
                  (fun () -> commit sampleEffects |> Async.RunSynchronously)
                  "save failure raises"

              Expect.isEmpty log "publish and dispatch never ran"

          testCase "dispatch Error surfaces after save and publish have completed"
          <| fun _ ->
              let log = List<string>()

              let save _ =
                  async {
                      log.Add "save"
                      return Ok()
                  }

              let publish _ = async { log.Add "publish" }
              let dispatch _ = async { return Error "accounting down" }

              let commit = RondelDirectCommit.create save publish dispatch

              Expect.throws
                  (fun () -> commit sampleEffects |> Async.RunSynchronously)
                  "dispatch failure raises"

              Expect.sequenceEqual log [ "save"; "publish" ] "save and publish completed before failure" ]
