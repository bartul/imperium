module Imperium.UnitTests.CollectionAssertTests

open Expecto
open Imperium.Testing.Spec

[<Tests>]
let tests =
    testList
        "CollectionAssert"
        [ testCase "HasAny failure includes actual items" (fun _ ->
              let collection =
                  CollectionAssert.forAccessor (fun (items: int list) -> items :> seq<int>)

              try
                  collection.HasAny ((=) 4) "number should exist" [ 1; 2; 3 ]
                  failtest "expected assertion failure"
              with :? AssertException as ex ->
                  Expect.stringContains ex.Message "number should exist" "message should include assertion context"
                  Expect.stringContains ex.Message "Actual items: 1; 2; 3" "message should include actual items")

          testCase "HasNone failure includes matching items" (fun _ ->
              let collection =
                  CollectionAssert.forAccessor (fun (items: int list) -> items :> seq<int>)

              try
                  collection.HasNone (fun item -> item % 2 = 0) "number should not be even" [ 1; 2; 3; 4 ]
                  failtest "expected assertion failure"
              with :? AssertException as ex ->
                  Expect.stringContains
                      ex.Message
                      "number should not be even"
                      "message should include assertion context"

                  Expect.stringContains ex.Message "found: 2; 4" "message should include matching items"
                  Expect.stringContains ex.Message "Actual items: 1; 2; 3; 4" "message should include actual items") ]
