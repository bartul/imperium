namespace Imperium.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Actions
// ──────────────────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
type Action =
    | Investor
    | Import
    | Production
    | Maneuver
    | Taxation
    | Factory

module Action =
    let toString action =
        match action with
        | Action.Investor -> "Investor"
        | Action.Import -> "Import"
        | Action.Production -> "Production"
        | Action.Maneuver -> "Maneuver"
        | Action.Taxation -> "Taxation"
        | Action.Factory -> "Factory"
