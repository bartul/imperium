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
  - `Rondel.Dto.RondelState`: GameId, NationPositions (Map<string, string option>), PendingMovements (Map<string, PendingMovement> keyed by nation name for O(log n) lookups)
- `src/Imperium.Web` bootstraps the HTTP layer (`Program.fs`). Reference the core project via the existing project reference instead of duplicating logic.
- `docs/` stores reference rulebooks; official rule PDFs live in `docs/official_rules/`. Leave build artefacts inside each project's `bin/` and `obj/` directories untouched.
- Rondel spaces (board order): `Investor`, `Import`, `ProductionOne`, `ManeuverOne`, `Taxation`, `Factory`, `ProductionTwo`, `ManeuverTwo`.
- Rondel rules source: mechanic follows the boardgame "rondel" described in `docs/Imperial_English_Rules.pdf`. Keep only a quick cheat sheet here; see the PDF for full details. Key movement: clockwise, cannot stay put; 1–3 spaces free, 4–6 cost 2M per additional space beyond the first 3 free spaces (4 spaces = 2M, 5 spaces = 4M, 6 spaces = 6M; max distance 6), first turn may start anywhere. Actions: Factory (build own city for 5M, no hostile upright armies), Production (each unoccupied home factory produces 1 unit), Import (buy up to 3 units for 1M each in home provinces), Maneuver (fleets adjacent sea; armies adjacent land or via fleets; rail within home; 3 armies can destroy a factory; place flags in newly occupied regions), Investor (pay bond interest; investor card gets 2M and may invest; Swiss bank owners may also invest; passing executes investor steps 2–3), Taxation (tax: 2M per unoccupied factory, 1M per flag; dividend if tax track increases; add power points; treasury collects tax minus 1M per army/fleet). Game ends at 25 power points; score = bond interest x nation factor + personal cash.

### Handler Signature Pattern
- Commands and event handlers take dependencies explicitly, with `PublishRondelEvent` (or equivalent) as the first parameter, followed by persistence loaders/savers, then other services (e.g., accounting charging).
- Public APIs return `Result<unit, string>`; errors are plain strings.
- Signature files define public shape first; implementations should not widen the surface in `.fs`.

### Rondel Implementation Patterns
- **Handler pipeline pattern**: Both `setToStartingPositions` and `move` handlers follow a consistent pipeline: domain conversion → validation → state loading → execution → IO side effects. Use `Result.map` to thread tuples through the pipeline and unwrap for final execution.
- **Record construction**: Use type annotation for DTO construction: `let newState : Dto.RondelState = { GameId = ...; NationPositions = ...; PendingMovements = ... }`. F# records use `{ }` syntax directly, not `TypeName { }`.
- **MoveOutcome type**: Internal discriminated union with named fields carrying complete context (targetSpace, distance, nation, rejectedCommand). All cases encapsulate necessary data, eliminating closure dependencies on outer scope variables for cleaner functional design.
- **Decision chain**: Validates moves through `Decision` monad (`noMovesAllowedIfNotInitialized` → `noMovesAllowedForNationNotInGame` → `firstMoveIsFreeToAnyPosition` → `failIfPositionIsInvalid` → `decideMovementOutcome`) producing `MoveOutcome`.
- **Side-effect separation**: `handleMoveOutcome` transforms `MoveOutcome` to `(state, events, commands)` tuple; `performIO` sequences persistence, event publishing, and outbound command dispatch with `Result.bind` for error propagation (short-circuits on first error), then uses `Result.defaultWith` to unwrap and throw on IO failures.
- **Command dispatch**: Uses `List.fold` with `Result.bind` to sequence outbound commands (`ChargeNationForRondelMovement`, `VoidRondelCharge`), returning first error or `Ok ()`.

### Open Work (current)
- Rondel `setToStartingPositions` handler is complete with validation, state persistence, and event publishing.
- Rondel `move` handler is complete: clockwise distance calculation, 1-3 space free moves with immediate action determination, 4-6 space paid moves with charge dispatch and pending state storage (formula: (distance - 3) * 2M), rejects 0-space (stay put) and 7+ space (exceeds max) moves. Handler accepts `ChargeNationForRondelMovement` and `VoidRondelCharge` dependencies. When a nation initiates a new move while a previous move is pending payment, the handler automatically voids the old charge, rejects the old pending move, and proceeds with the new move.
- Implement remaining Rondel handlers (`onInvoicedPaid`, `onInvoicePaymentFailed`) to complete payment flow (move stored as pending, needs completion on payment confirmation or rejection on payment failure).
- Add public APIs for Gameplay and Accounting or trim placeholders if unused.
- Expecto test for `onInvoicedPaid` happy path added (handler stubbed); add test for `onInvoicePaymentFailed` once ready to implement.

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
  - move: superseding pending paid move with another paid move voids old charge and rejects old move (validates void command dispatched for old BillingId, old move rejected, new charge created with correct amount, no action determined for new pending move)
  - move: superseding pending paid move with free move voids charge and completes immediately (validates void command dispatched, old move rejected, no new charge, new move completes with ActionDetermined)
  - onInvoicePaid: completes pending movement and publishes ActionDetermined event (validates payment confirmation updates position, removes pending movement, publishes correct action, and allows subsequent moves from new position)

## Branch Naming Guidelines

Use branch name prefixes to categorize the type of work, following conventional commit conventions:

- `feat/` - New features or functionality (e.g., `feat/implement-invoice-paid-handler`, `feat/add-nation-selection`)
- `fix/` - Bug fixes (e.g., `fix/rondel-distance-calculation`, `fix/null-reference-in-payment`)
- `refactor/` - Code refactoring without changing behavior (e.g., `refactor/extract-validation-pipeline`, `refactor/simplify-move-handler`)
- `test/` - Adding or updating tests (e.g., `test/add-property-tests-for-moves`, `test/improve-coverage`)
- `docs/` - Documentation updates (e.g., `docs/update-rondel-rules`, `docs/add-api-examples`)
- `chore/` - Maintenance tasks: dependencies, build config, tooling (e.g., `chore/upgrade-dotnet-9`, `chore/update-fantomas`)
- `perf/` - Performance improvements (e.g., `perf/optimize-state-loading`, `perf/cache-distance-calc`)
- `style/` - Code style/formatting changes with no logic impact (e.g., `style/reformat-with-fantomas`)

**Examples:**
- Implementing payment handler: `feat/implement-invoice-paid-handler`
- Fixing a movement bug: `fix/correct-7-space-rejection`
- Adding property tests: `test/add-superseding-move-tests`
- Refactoring validation: `refactor/use-decision-monad`

## Pull Request Workflow

All changes must go through pull requests to maintain code quality and enable proper review. Direct pushes to `master` are discouraged (see Branch Protection below).

### Standard Workflow

1. **Create branch** - Use appropriate prefix from Branch Naming Guidelines
   ```bash
   git checkout -b feat/your-feature-name
   ```

2. **Make changes** - Implement your changes following the three-phase module development process
   - Write tests first when adding new functionality
   - Keep commits focused and well-described

3. **Verify locally** - Ensure all checks pass before pushing
   ```bash
   dotnet build Imperium.sln --configuration Release
   dotnet test Imperium.sln --configuration Release
   ```

4. **Push and create PR** - Push your branch and open a pull request
   ```bash
   git push -u origin feat/your-feature-name
   gh pr create --fill  # or use GitHub web UI
   ```

5. **CI must pass** - The "Continuous Integration" workflow serves as the required quality gate
   - Build must succeed with no warnings
   - All tests must pass
   - If CI fails, fix issues and push updates

6. **Code review** - Wait for review approval
   - Address review comments
   - Push additional commits as needed

7. **Merge** - Once approved and CI passes, merge to master
   - Use "Squash and merge" for clean history (preferred)
   - Use "Merge commit" to preserve detailed commit history
   - Delete branch after merging

### Branch Protection (Workaround)

GitHub branch protection rules require GitHub Pro for private repositories. Until upgraded:

- **Team Agreement**: Never push directly to `master` except for emergencies
- **CI as Quality Gate**: The "Continuous Integration" workflow provides automated quality checks
- **Self-Review**: Before merging your own PR, verify:
  - CI passes (green checkmark)
  - Code follows project conventions
  - Tests provide adequate coverage
  - Documentation is updated if needed

**Future**: When GitHub Pro is enabled, configure these branch protection rules:
- Require pull request before merging
- Require status checks to pass (Continuous Integration workflow)
- Require conversation resolution before merging
- Do not allow bypassing the above settings

## Commit & Pull Request Guidelines

- Follow the existing history: imperative, concise subject lines (`Update to dotnet 9`, `Add web`).
- Keep commits scoped to one concern; describe "what" and "why" in the body when context is non-trivial.
- PRs should link relevant issues, outline test evidence (command outputs or screenshots), and call out any manual steps for deployment.
- Use the PR template (`.github/PULL_REQUEST_TEMPLATE.md`) to structure your PR description.
- Request review from domain owners when altering core rule logic or public web endpoints.
