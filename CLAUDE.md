# Imperium — Codebase Guide

## Repository Layout

```text
src/
  Imperium/               # Core domain (bounded contexts: Accounting, Rondel, Gameplay)
  Imperium.Terminal/      # Terminal host: actors, bus, stores, shell UI
  Imperium.Web/           # Web API host
tests/
  Imperium.UnitTests/     # Single test project: spec-based + Expecto tests
```

## Test Project Structure

`tests/Imperium.UnitTests/` mirrors the source layout and uses a package-ready spec support layer:

```text
Support/
  Spec/
    Specification.fs   -- Core spec types, CE builder (module Imperium.Testing.Spec.Specification)
    Runner.fs          -- SpecRunner, toExpecto, runExpectation (module Imperium.Testing.Spec.Runner)
    Filter.fs          -- SpecFilter [RequireQualifiedAccess] (module Imperium.Testing.Spec.SpecFilter)
    CollectionAssert.fs -- Typed collection assertions (module Imperium.Testing.Spec.CollectionAssert)
    Markdown.fs         -- Spec markdown rendering [RequireQualifiedAccess] (module Imperium.Testing.Spec.SpecMarkdown)
  Spec.Tests/
    SpecificationTests.fs
    FilterTests.fs
    MarkdownTests.fs

Imperium/
  Contract/
    AccountingContractTests.fs
    RondelContractTests.fs
  Accounting/
    Context.fs    -- AccountingContext, createContext, runner
    Assertions.fs -- Typed assertion helpers
    Specs.fs      -- Specs list, renderSpecMarkdown, [<Tests>] let tests  (module Imperium.UnitTests.Accounting.Specs)
  Gameplay/
    GameplayTests.fs
  Rondel/
    StateFormatting.fs -- ASCII board renderer for spec markdown
    Context.fs         -- RondelContext, createContext, runner
    Assertions.fs      -- Typed assertion helpers
    Specs.fs           -- Specs list, renderSpecMarkdown, [<Tests>] let tests  (module Imperium.UnitTests.Rondel.Specs)

Imperium.Terminal/
  BusTests.fs
  Rondel/
    StoreTests.fs
    DirectCommitTests.fs
    HostTests.fs
  Accounting/
    HostTests.fs

Main.fs   -- Entry point, native runner, markdown renderer
           -- Uses module abbreviations: Accounting = Imperium.UnitTests.Accounting.Specs
           --                            Rondel = Imperium.UnitTests.Rondel.Specs
```

### Spec Support Namespace

Spec support modules live in namespace `Imperium.Testing.Spec`. Consumer files open:

```fsharp
open Imperium.Testing.Spec              // brings SpecFilter, SpecMarkdown, CollectionAssert into scope by short name
open Imperium.Testing.Spec.Specification // brings spec, specOn, NoState, Action, etc.
open Imperium.Testing.Spec.Runner       // brings SpecRunner, toExpecto, runExpectation, etc.
```

### Running Tests

```bash
dotnet build
dotnet test
dotnet run --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj
dotnet run --no-build --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj -- --render-spec-markdown
dotnet run --no-build --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj -- --render-spec-markdown --filter Imperium.Rondel
```

## Source Conventions

- `namespace Imperium.Xxx` — used for bounded context files in `src/`; multiple files share the same namespace
- `[<RequireQualifiedAccess>]` — used on public facade modules (`Rondel`, `Accounting`, `SpecFilter`, `SpecMarkdown`)
- Compile order in `.fsproj` is explicit and topological — F# requires this
- `dotnet fantomas` is available locally (`.config/dotnet-tools.json`) and enforces formatting
