module Imperium.UnitTests.Rondel

open System
open Expecto
open Imperium.Rondel

[<Tests>]
let tests =
    testList "Rondel" [
        testList "RondelInvoiceId" [
            testList "create" [
                test "accepts valid GUID" {
                    let guid = Guid.NewGuid()
                    let result = RondelInvoiceId.create guid
                    Expect.isOk result "Should accept valid GUID"
                }

                test "rejects empty GUID" {
                    let result = RondelInvoiceId.create Guid.Empty
                    Expect.isError result "Should reject empty GUID"
                }
            ]

            testList "newId" [
                test "generates non-empty GUID" {
                    let id = RondelInvoiceId.newId ()
                    let guid = RondelInvoiceId.value id
                    Expect.notEqual guid Guid.Empty "Should generate non-empty GUID"
                }

                test "generates unique IDs" {
                    let id1 = RondelInvoiceId.newId ()
                    let id2 = RondelInvoiceId.newId ()
                    let guid1 = RondelInvoiceId.value id1
                    let guid2 = RondelInvoiceId.value id2
                    Expect.notEqual guid1 guid2 "Should generate unique IDs"
                }
            ]

            testList "toString" [
                test "returns valid GUID string format" {
                    let id = RondelInvoiceId.newId ()
                    let str = RondelInvoiceId.toString id
                    let success, _ = Guid.TryParse str
                    Expect.isTrue success "Should return valid GUID string format"
                }
            ]

            testList "tryParse" [
                test "accepts valid GUID strings" {
                    let guidStr = "a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d"
                    let result = RondelInvoiceId.tryParse guidStr
                    Expect.isOk result "Should accept valid GUID string"
                }

                test "rejects invalid GUID strings" {
                    let result = RondelInvoiceId.tryParse "not-a-guid"
                    Expect.isError result "Should reject invalid GUID string"
                }

                test "rejects empty strings" {
                    let result = RondelInvoiceId.tryParse ""
                    Expect.isError result "Should reject empty string"
                }

                test "rejects null string" {
                    let result = RondelInvoiceId.tryParse null
                    Expect.isError result "Should reject null string"
                }
            ]

            testList "property-based" [
                testProperty "tryParse roundtrip with toString for valid GUIDs" (fun (guid: Guid) ->
                    match RondelInvoiceId.create guid with
                    | Ok id ->
                        let str = RondelInvoiceId.toString id
                        let parsed = RondelInvoiceId.tryParse str
                        parsed = Ok id
                    | Error _ ->
                        // Skip empty GUIDs (they're rejected by create)
                        true
                )

                testProperty "value roundtrip with create for valid GUIDs" (fun (guid: Guid) ->
                    match RondelInvoiceId.create guid with
                    | Ok id ->
                        let extractedGuid = RondelInvoiceId.value id
                        extractedGuid = guid
                    | Error _ ->
                        // Skip empty GUIDs (they're rejected by create)
                        true
                )
            ]
        ]
    ]
