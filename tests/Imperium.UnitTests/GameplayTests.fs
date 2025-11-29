module Imperium.UnitTests.Gameplay

open System
open Expecto
open Imperium.Gameplay

[<Tests>]
let tests =
    testList "Gameplay" [
        testList "GameId" [
            testList "create" [
                test "accepts valid GUID" {
                    let guid = Guid.NewGuid()
                    let result = GameId.create guid
                    Expect.isOk result "Should accept valid GUID"
                }

                test "rejects empty GUID" {
                    let result = GameId.create Guid.Empty
                    Expect.isError result "Should reject empty GUID"
                }
            ]

            testList "newId" [
                test "generates non-empty GUID" {
                    let id = GameId.newId ()
                    let guid = GameId.value id
                    Expect.notEqual guid Guid.Empty "Should generate non-empty GUID"
                }

                test "generates unique IDs" {
                    let id1 = GameId.newId ()
                    let id2 = GameId.newId ()
                    let guid1 = GameId.value id1
                    let guid2 = GameId.value id2
                    Expect.notEqual guid1 guid2 "Should generate unique IDs"
                }
            ]

            testList "toString" [
                test "returns valid GUID string format" {
                    let id = GameId.newId ()
                    let str = GameId.toString id
                    let success, _ = Guid.TryParse str
                    Expect.isTrue success "Should return valid GUID string format"
                }
            ]

            testList "tryParse" [
                test "accepts valid GUID strings" {
                    let guidStr = "a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d"
                    let result = GameId.tryParse guidStr
                    Expect.isOk result "Should accept valid GUID string"
                }

                test "rejects invalid GUID strings" {
                    let result = GameId.tryParse "not-a-guid"
                    Expect.isError result "Should reject invalid GUID string"
                }

                test "rejects empty strings" {
                    let result = GameId.tryParse ""
                    Expect.isError result "Should reject empty string"
                }

                test "rejects null string" {
                    let result = GameId.tryParse null
                    Expect.isError result "Should reject null string"
                }
            ]

            testList "property-based" [
                testProperty "tryParse roundtrip with toString for valid GUIDs" (fun (guid: Guid) ->
                    match GameId.create guid with
                    | Ok id ->
                        let str = GameId.toString id
                        let parsed = GameId.tryParse str
                        parsed = Ok id
                    | Error _ ->
                        // Skip empty GUIDs (they're rejected by create)
                        true
                )

                testProperty "value roundtrip with create for valid GUIDs" (fun (guid: Guid) ->
                    match GameId.create guid with
                    | Ok id ->
                        let extractedGuid = GameId.value id
                        extractedGuid = guid
                    | Error _ ->
                        // Skip empty GUIDs (they're rejected by create)
                        true
                )
            ]
        ]
    ]
