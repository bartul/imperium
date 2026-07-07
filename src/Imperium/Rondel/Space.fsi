namespace Imperium.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Spaces
// ──────────────────────────────────────────────────────────────────────────

/// The eight spaces on the rondel wheel, arranged clockwise.
/// Production and Maneuver each appear twice on the track.
[<RequireQualifiedAccess>]
type Space =
    | Investor
    | Import
    | ProductionOne
    | ManeuverOne
    | Taxation
    | Factory
    | ProductionTwo
    | ManeuverTwo

module Space =
    /// Maps a rondel space to its corresponding action.
    val internal toAction: Space -> Action

    /// Assembly-internal: clockwise distance between two spaces (0..7).
    val internal distance: Space -> Space -> int

    /// Assembly-internal: serialise a space to its persisted string form.
    val internal toString: Space -> string

    /// Assembly-internal: parse a space from its persisted string form.
    val internal fromString: string -> Result<Space, string>
