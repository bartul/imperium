# Repository Guidelines

## Project Structure & Module Organization
- `Imperium.sln` stitches together the core F# library and the ASP.NET Core web host.
- `src/Imperium` contains the domain modules (`Gameplay.fs`, `Economy.fs`, `Rondel.fs`); keep shared rules and calculations here.
- `Economy.Amount` is a measured struct wrapper (`int<M>`) with guarded construction; errors are plain strings (e.g., negative amount); includes `tryParse` for string inputs.
- `Rondel.RondelInvoiceId` is a struct DU wrapping `Guid` with guarded creation/parse helpers; errors are plain strings for empty GUID or parse failures.
- `Gameplay.NationId` is a DU for Imperial nations (Germany, Great Britain, France, Russia, Austria-Hungary, Italy) with `toString`/`tryParse` helpers; `all` is a `Set<NationId>`.
- `src/Imperium.Web` bootstraps the HTTP layer (`Program.fs`). Reference the core project via the existing project reference instead of duplicating logic.
- `docs/` stores reference rulebooks; official rule PDFs live in `docs/official_rules/`. Leave build artefacts inside each project’s `bin/` and `obj/` directories untouched.
- Rondel domain context: rondel slots belong to nations (not players). `Space` cases cover the board order, with `ProductionOne/Two` and `ManeuverOne/Two` mapping to the same `Action`. Current events: `RondelCreated`, `NationMovementInvoiced`, and `NationActionDetermined` (carries `NationId * Action`). `createRondel` takes the set of participating `NationId`s; move/payment flows are stubbed for now.
- Rondel spaces (board order): `Investor`, `Factory`, `Import`, `ManeuverOne`, `ProductionOne`, `ManeuverTwo`, `ProductionTwo`, `Taxation`.
- Rondel rules source: mechanic follows the boardgame “rondel” described in `docs/Imperial_English_Rules.pdf`. Key rules: nations move clockwise, cannot stay put; 1–3 spaces are free, each extra space costs 2M to the bank (max 6), first turn may start anywhere. Factory: build in own city without hostile upright armies for 5M. Production: each unoccupied home factory produces 1 unit in its province. Import: buy up to 3 units for 1M each in home provinces. Maneuver: fleets move to adjacent sea; armies move to adjacent land or via fleets; rail within home; 3 armies can destroy a factory; place flags in newly occupied regions. Investor: pay bond interest, investor card gains 2M and may invest, Swiss bank owners may also invest; passing executes investor steps 2–3. Taxation: record tax (2M per unoccupied factory, 1M per flag), dividend if tax track increases, add power points, then treasury collects tax minus 1M per army/fleet. Game ends when a nation reaches 25 power points; score = bond interest x nation factor + personal cash.

## Build, Test, and Development Commands
- Restore dependencies: `dotnet restore Imperium.sln`.
- Compile everything: `dotnet build Imperium.sln` (fails fast on warnings-as-errors configured per project).
- Run the web host locally: `dotnet run --project src/Imperium.Web/Imperium.Web.fsproj`.
- Live reload during UI work: `dotnet watch --project src/Imperium.Web/Imperium.Web.fsproj run`.

## Coding Style & Naming Conventions
- Use the default F# formatting (4-space indentation, modules and types in `PascalCase`, functions and values in `camelCase`).
- Group related functions into modules that mirror file names (`Rondel`, `MonetarySystem`); expose a minimal public surface.
- Prefer expression-based code and pattern matching over mutable branches.
- Before committing, run `dotnet tool run fantomas` if the formatter is configured; otherwise keep diffs tidy and minimal.

## Testing Guidelines
- Unit and property tests belong in a future `tests/Imperium.Tests` F# project that references `src/Imperium`.
- Mirror the module under test (e.g., `RondelTests.fs` for `Rondel.fs`) and use `Expecto` or `xUnit` consistently.
- Execute `dotnet test` from the repository root; aim to cover decision-heavy rules like movement costs and monetary transfers.
- When adding new behaviour, include regression tests that fail prior to the change.

## Commit & Pull Request Guidelines
- Follow the existing history: imperative, concise subject lines (`Update to dotnet 9`, `Add web`).
- Keep commits scoped to one concern; describe “what” and “why” in the body when context is non-trivial.
- PRs should link relevant issues, outline test evidence (command outputs or screenshots), and call out any manual steps for deployment.
- Request review from domain owners when altering core rule logic or public web endpoints.
