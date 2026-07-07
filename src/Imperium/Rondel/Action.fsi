namespace Imperium.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Actions
// ──────────────────────────────────────────────────────────────────────────

/// The six distinct actions a nation can perform on the rondel.
/// Each action corresponds to one or two spaces on the circular track.
[<RequireQualifiedAccess>]
type Action =
    | Investor
    | Import
    | Production
    | Maneuver
    | Taxation
    | Factory

module Action =
    /// Assembly-internal: serialise an action to its persisted string form.
    val internal toString: Action -> string
