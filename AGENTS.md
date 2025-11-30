# Repository Guidelines

## Project Structure & Module Organization
- `Imperium.sln` stitches together the core F# library, ASP.NET Core web host, and unit test project.
- `src/Imperium` contains the domain modules (`Gameplay.fs`, `Economy.fs`, `Rondel.fs`); keep shared rules and calculations here.
- `tests/Imperium.UnitTests` contains Expecto-based unit tests; test modules mirror source structure (e.g., `RondelTests.fs` tests `Rondel.fs`).
- `Economy.Amount` is a measured struct wrapper (`int<M>`) with guarded construction; errors are plain strings (e.g., negative amount); includes `tryParse` for string inputs.
- `Rondel.RondelInvoiceId` is a struct DU wrapping `Guid` with guarded creation/parse helpers; errors are plain strings for empty GUID or parse failures.
- `Gameplay.NationId` is a DU for Imperial nations (Germany, Great Britain, France, Russia, Austria-Hungary, Italy) with `toString`/`tryParse` helpers; `all` is a `Set<NationId>`.
- `src/Imperium.Web` bootstraps the HTTP layer (`Program.fs`). Reference the core project via the existing project reference instead of duplicating logic.
- `docs/` stores reference rulebooks; official rule PDFs live in `docs/official_rules/`. Leave build artefacts inside each project’s `bin/` and `obj/` directories untouched.
- Rondel domain context: CQRS bounded context where commands are identified by `GameId` (internal state hidden). Rondel slots belong to nations (not players). `Space` cases cover the board order, with `ProductionOne/Two` and `ManeuverOne/Two` mapping to the same `Action`. Integration events: `PositionedAtStart` (gameId), `ActionDetermined` (gameId, nationId, action), `MovementToActionRejected` (gameId, nationId, space). Commands: `setToStartPositions` initializes nations at start positions; `move` initiates movement and issues invoice to Economy domain; event handlers `onInvoicedPaid`/`onInvoicePaymentFailed` process Economy responses. All flows currently stubbed.
- Rondel spaces (board order): `Investor`, `Factory`, `Import`, `ManeuverOne`, `ProductionOne`, `ManeuverTwo`, `ProductionTwo`, `Taxation`.
- Rondel rules source: mechanic follows the boardgame “rondel” described in `docs/Imperial_English_Rules.pdf`. Key rules: nations move clockwise, cannot stay put; 1–3 spaces are free, each extra space costs 2M to the bank (max 6), first turn may start anywhere. Factory: build in own city without hostile upright armies for 5M. Production: each unoccupied home factory produces 1 unit in its province. Import: buy up to 3 units for 1M each in home provinces. Maneuver: fleets move to adjacent sea; armies move to adjacent land or via fleets; rail within home; 3 armies can destroy a factory; place flags in newly occupied regions. Investor: pay bond interest, investor card gains 2M and may invest, Swiss bank owners may also invest; passing executes investor steps 2–3. Taxation: record tax (2M per unoccupied factory, 1M per flag), dividend if tax track increases, add power points, then treasury collects tax minus 1M per army/fleet. Game ends when a nation reaches 25 power points; score = bond interest x nation factor + personal cash.

## Build, Test, and Development Commands
- Restore dependencies: `dotnet restore Imperium.sln`.
- Compile everything: `dotnet build Imperium.sln` (fails fast on warnings-as-errors configured per project).
- Run the web host locally: `dotnet run --project src/Imperium.Web/Imperium.Web.fsproj`.
- Live reload during UI work: `dotnet watch --project src/Imperium.Web/Imperium.Web.fsproj run`.
- Run unit tests: `dotnet test` (VS Code integration via YoloDev.Expecto.TestSdk) or `dotnet run --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj` (native Expecto runner).

## Coding Style & Naming Conventions
- Use the default F# formatting (4-space indentation, modules and types in `PascalCase`, functions and values in `camelCase`).
- Group related functions into modules that mirror file names (`Rondel`, `MonetarySystem`); expose a minimal public surface.
- Prefer expression-based code and pattern matching over mutable branches.
- Before committing, run `dotnet tool run fantomas` if the formatter is configured; otherwise keep diffs tidy and minimal.

## Testing Guidelines
- Unit tests live in `tests/Imperium.UnitTests` using Expecto 10.2.3 with FsCheck integration for property-based testing.
- Test modules mirror source structure: `Imperium.UnitTests.Rondel` tests `Imperium.Rondel`; file names use `*Tests.fs` suffix (e.g., `RondelTests.fs`).
- Use `[<Tests>]` attribute on test values for discovery by YoloDev.Expecto.TestSdk (enables VS Code Test Explorer integration).
- Execute `dotnet test` (via TestSdk) or `dotnet run --project tests/Imperium.UnitTests/Imperium.UnitTests.fsproj` (native Expecto runner with colorized output).
- Test organization: group related tests with `testList`, use descriptive test names in lowercase ("accepts valid GUID", not "AcceptsValidGuid").
- Cover edge cases: null inputs, empty strings, invalid formats, boundary conditions.
- Follow three-phase module development process documented in `docs/module_design_process.md`: define interface, write tests, implement functionality.
- Current test coverage: `RondelInvoiceId` (create, newId, toString, tryParse) - 9 tests passing.

## Commit & Pull Request Guidelines
- Follow the existing history: imperative, concise subject lines (`Update to dotnet 9`, `Add web`).
- Keep commits scoped to one concern; describe “what” and “why” in the body when context is non-trivial.
- PRs should link relevant issues, outline test evidence (command outputs or screenshots), and call out any manual steps for deployment.
- Request review from domain owners when altering core rule logic or public web endpoints.
