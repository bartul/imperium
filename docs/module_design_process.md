# Module Development Process

This document describes the three-phase development process for F# modules in the Imperium project.

## Overview

All module development should follow an interface-first, test-driven approach with three distinct phases:

1. **Interface Definition** - Define the public API
2. **Test Implementation** - Specify expected behavior
3. **Functional Implementation** - Deliver working code

This process ensures clear API boundaries, testable design, and confidence that implementations meet requirements.

---

## Phase 1: Interface Definition

Define the module signature in the `.fsi` file with complete type definitions and function signatures. Create a corresponding `.fs` file with dummy implementations that compile but contain no real functionality.

### Goals
- Establish the public API contract
- Define all types and function signatures
- Ensure the module compiles

### Guidelines
- Write complete type definitions in the `.fsi` file
- Include XML documentation comments for public APIs
- Implement dummy functions in the `.fs` file that:
  - Match the signatures exactly
  - Compile successfully
  - Use placeholders like `failwith "Not implemented"` or return default values
- Focus on the "what" (interface), not the "how" (implementation)

### Example

**Gameplay.fsi:**
```fsharp
namespace Imperium

module Gameplay =
    [<RequireQualifiedAccess>]
    type NationId =
        | Germany
        | GreatBritain
        | France
        | Russia
        | AustriaHungary
        | Italy

    module NationId =
        /// Returns the set of all valid nation IDs
        val all : Set<NationId>

        /// Converts a nation ID to its display string
        val toString : NationId -> string

        /// Attempts to parse a string into a nation ID
        val tryParse : string -> Result<NationId, string>
```

**Gameplay.fs (Phase 1 - dummy implementation):**
```fsharp
namespace Imperium

open System

module Gameplay =
    [<RequireQualifiedAccess>]
    type NationId =
        | Germany
        | GreatBritain
        | France
        | Russia
        | AustriaHungary
        | Italy

    module NationId =
        let all = Set.empty

        let toString _ = ""

        let tryParse _ = Error "Not implemented"
```

---

## Phase 2: Test Implementation

Write unit tests targeting the interface defined in the `.fsi` file. Tests will initially fail since implementations are dummy stubs.

### Goals
- Define expected behavior through tests
- Document use cases and edge cases
- Provide executable specifications

### Guidelines
- Write tests against the public interface only
- Cover happy paths and error cases
- Use descriptive test names that explain intent
- Tests should fail initially (red phase of TDD)
- Consider property-based tests for general invariants

### Example

**GameplayTests.fs:**
```fsharp
module Imperium.Tests.GameplayTests

open Expecto
open Imperium.Gameplay

[<Tests>]
let nationIdTests =
    testList "NationId" [
        test "all contains exactly six nations" {
            Expect.equal (Set.count NationId.all) 6 "Should have 6 nations"
        }

        test "toString returns proper display name for Germany" {
            let result = NationId.toString NationId.Germany
            Expect.equal result "Germany" "Should return 'Germany'"
        }

        test "tryParse accepts valid nation name" {
            let result = NationId.tryParse "Germany"
            Expect.isOk result "Should parse valid nation name"
            match result with
            | Ok nation -> Expect.equal nation NationId.Germany "Should parse to Germany"
            | Error _ -> failtest "Should not error"
        }

        test "tryParse is case insensitive" {
            let result = NationId.tryParse "germany"
            Expect.isOk result "Should parse case-insensitively"
        }

        test "tryParse rejects invalid nation name" {
            let result = NationId.tryParse "Invalid"
            Expect.isError result "Should reject invalid nation name"
        }

        test "tryParse rejects empty string" {
            let result = NationId.tryParse ""
            Expect.isError result "Should reject empty string"
        }
    ]
```

---

## Phase 3: Functional Implementation

Implement actual functionality in the `.fs` file and iterate until all unit tests pass.

### Goals
- Deliver working code that satisfies the interface contract
- Make all tests pass (green phase of TDD)
- Maintain clean, maintainable code

### Guidelines
- Implement one function at a time
- Run tests frequently to verify progress
- Keep implementations simple and focused
- Refactor for clarity once tests pass
- Do not modify the `.fsi` interface (unless tests reveal design issues)
- Add implementation comments only where logic is non-obvious

### Example

**Gameplay.fs (Phase 3 - functional implementation):**
```fsharp
namespace Imperium

open System

module Gameplay =
    [<RequireQualifiedAccess>]
    type NationId =
        | Germany
        | GreatBritain
        | France
        | Russia
        | AustriaHungary
        | Italy

    module NationId =
        let all =
            [ NationId.Germany
              NationId.GreatBritain
              NationId.France
              NationId.Russia
              NationId.AustriaHungary
              NationId.Italy ]
            |> Set.ofList

        let toString =
            function
            | NationId.Germany -> "Germany"
            | NationId.GreatBritain -> "Great Britain"
            | NationId.France -> "France"
            | NationId.Russia -> "Russia"
            | NationId.AustriaHungary -> "Austria-Hungary"
            | NationId.Italy -> "Italy"

        let tryParse (raw: string) =
            if String.IsNullOrWhiteSpace raw then
                Error "Nation cannot be empty."
            else
                let normalized = raw.Trim().ToLowerInvariant()

                let isMatch nation =
                    let name = toString nation
                    name.ToLowerInvariant() = normalized

                match all |> Seq.tryFind isMatch with
                | Some nation -> Ok nation
                | None ->
                    let expected = all |> Seq.map toString |> String.concat ", "
                    Error $"Unknown nation '{raw}'. Expected one of: {expected}."
```

---

## Benefits

This three-phase approach provides:

1. **Clear API Contracts** - The `.fsi` file serves as documentation and enforces boundaries
2. **Testability** - Tests are written against stable interfaces, not implementation details
3. **Design Feedback** - Writing tests before implementation reveals design issues early
4. **Confidence** - Passing tests prove the implementation meets requirements
5. **Refactoring Safety** - Tests protect against regressions during refactoring
6. **Documentation** - Tests serve as executable examples of module usage

---

## Anti-Patterns to Avoid

- **Skipping Phase 1** - Implementing without defining the interface first leads to unclear APIs
- **Writing Tests After Implementation** - Reduces test quality and biases tests toward existing code
- **Modifying .fsi During Phase 3** - Indicates the interface wasn't well thought out; return to Phase 1
- **Dummy Implementations That Don't Compile** - Phase 1 must produce compilable code
- **Testing Implementation Details** - Tests should target the public interface only
- **Partial Test Coverage** - Write comprehensive tests in Phase 2 before implementing

---

## Related Documentation

- See `AGENTS.md` for project structure and module organization
- See test project structure guidelines (when tests are added)
- Follow F# coding conventions outlined in `AGENTS.md`
