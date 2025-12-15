# Repository Guidelines
Last verified: 2025-12-15

## Project Structure & Module Organization
- `Imperium.sln` stitches together the core F# library, ASP.NET Core web host, and unit test project.
- `src/Imperium` contains domain modules (build order: `Primitives.fs`, `Contract.fs`, `Gameplay.fs/.fsi`, `Accounting.fs/.fsi`, `Rondel.fs/.fsi`).
- `tests/Imperium.UnitTests` contains Expecto-based unit tests; test modules mirror source structure (e.g., `RondelTests.fs` tests `Rondel.fs`).
- **Primitives module:** Foundational types with no `.fsi` file (intentionally public)
  - `Id` - Struct wrapping `Guid` with validation; provides `create`, `newId`, `value`, `toString`, `tryParse`, and mapper helpers
  - `Amount` - Measured struct wrapper (`int<M>`) with guarded construction; errors are plain strings; includes `tryParse`
- **Contract module:** Cross-bounded-context communication types; no `.fsi` file (intentionally public)
  - `Contract.Rondel`: SetToStartingPositionsCommand, MoveCommand, RondelEvent (PositionedAtStart, ActionDetermined, MoveToActionSpaceRejected)
  - `Contract.Accounting`: ChargeNationForRondelMovementCommand, VoidRondelChargeCommand, AccountingEvent (RondelInvoicePaid, RondelInvoicePaymentFailed)
  - Function types for dependency injection (e.g., `ChargeNationForRondelMovement = ChargeNationForRondelMovementCommand -> Result<unit, string>`, `VoidRondelCharge = VoidRondelChargeCommand -> Result<unit, string>`)
  - Events use record types (e.g., `RondelEvent = | PositionedAtStart of PositionedAtStart` where `PositionedAtStart = { GameId: Guid }`)
- **Domain modules:** CQRS bounded contexts with `.fsi` files defining public APIs
  - Internal types (GameId, NationId, RondelBillingId, Space, Action, Bank, Investor) hidden from public APIs
  - Public APIs expose only command handlers and event handlers accepting contract types
  - All handlers take `PublishRondelEvent` (event publisher) as first parameter for explicit dependency injection
  - `Gameplay` and `Accounting` have no public API currently (placeholder values only)
  - `Rondel` exposes: PublishRondelEvent type, setToStartingPositions (implemented), move, onInvoicedPaid, onInvoicePaymentFailed (stubbed)
- `src/Imperium.Web` bootstraps the HTTP layer (`Program.fs`). Reference the core project via the existing project reference instead of duplicating logic.
- `docs/` stores reference rulebooks; official rule PDFs live in `docs/official_rules/`. Leave build artefacts inside each project's `bin/` and `obj/` directories untouched.
- Rondel spaces (board order): `Investor`, `Import`, `ProductionOne`, `ManeuverOne`, `Taxation`, `Factory`, `ProductionTwo`, `ManeuverTwo`.
- Rondel rules source: mechanic follows the boardgame "rondel" described in `docs/Imperial_English_Rules.pdf`. Keep only a quick cheat sheet here; see the PDF for full details. Key movement: clockwise, cannot stay put; 1–3 spaces free, 4–6 cost 2M per additional space beyond the first 3 free spaces (4 spaces = 2M, 5 spaces = 4M, 6 spaces = 6M; max distance 6), first turn may start anywhere. Actions: Factory (build own city for 5M, no hostile upright armies), Production (each unoccupied home factory produces 1 unit), Import (buy up to 3 units for 1M each in home provinces), Maneuver (fleets adjacent sea; armies adjacent land or via fleets; rail within home; 3 armies can destroy a factory; place flags in newly occupied regions), Investor (pay bond interest; investor card gets 2M and may invest; Swiss bank owners may also invest; passing executes investor steps 2–3), Taxation (tax: 2M per unoccupied factory, 1M per flag; dividend if tax track increases; add power points; treasury collects tax minus 1M per army/fleet). Game ends at 25 power points; score = bond interest x nation factor + personal cash.

### Handler Signature Pattern
- Commands and event handlers take dependencies explicitly, with `PublishRondelEvent` (or equivalent) as the first parameter, followed by persistence loaders/savers, then other services (e.g., accounting charging).
- Public APIs return `Result<unit, string>`; errors are plain strings.
- Signature files define public shape first; implementations should not widen the surface in `.fs`.

### Open Work (current)
- Rondel `setToStartingPositions` handler is complete with validation, state persistence, and event publishing.
- Rondel `move` handler is complete: clockwise distance calculation, 1-3 space free moves with immediate action determination, 4-6 space paid moves with charge dispatch and pending state storage (formula: (distance - 3) * 2M), rejects 0-space (stay put) and 7+ space (exceeds max) moves. Handler accepts `ChargeNationForRondelMovement` and `VoidRondelCharge` dependencies (VoidRondelCharge currently unused, reserved for canceling pending charges).
- Implement remaining Rondel handlers (`onInvoicedPaid`, `onInvoicePaymentFailed`) to complete payment flow (move stored as pending, needs completion on payment confirmation or rejection on payment failure).
- Add public APIs for Gameplay and Accounting or trim placeholders if unused.
- Add Expecto tests for payment flow handlers (`onInvoicedPaid`, `onInvoicePaymentFailed`) once implemented.

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
- **Testing approach:** Tests target public handler APIs using contract types and injected publishers/dispatchers to verify the rondel signals the right outcomes and charges the right costs.
- Current test coverage:
  - starting positions: rejects missing game id; rejects empty roster; ignores duplicate nations; rondel signals that starting positions are set
  - move: before starting positions are chosen, the move is denied and no movement fee is due
  - move: nation's first move may choose any rondel space (free); chosen rondel space determines the action (property test, 15 iterations)
  - move: rejects move to nation's current position repeatedly (property test, 15 iterations; validates rejection stability across multiple attempts, no charges, no action determined)
  - move: multiple consecutive moves of 1-3 spaces are free (property test, 15 iterations; validates 3 consecutive moves per nation with correct action determination and no charges, includes wraparound)
  - move: rejects moves of 7 spaces as exceeding maximum distance (property test, 15 iterations; validates all nations and starting positions reject 7-space moves with MoveToActionSpaceRejected, no charges)
  - move: moves of 4-6 spaces require payment (property test, 15 iterations; validates charge command dispatched with correct amount (distance - 3) * 2M, no premature action determination, move not rejected)

## Commit & Pull Request Guidelines

- Follow the existing history: imperative, concise subject lines (`Update to dotnet 9`, `Add web`).
- Keep commits scoped to one concern; describe “what” and “why” in the body when context is non-trivial.
- PRs should link relevant issues, outline test evidence (command outputs or screenshots), and call out any manual steps for deployment.
- Request review from domain owners when altering core rule logic or public web endpoints.
