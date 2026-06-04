namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Domain Values
// ──────────────────────────────────────────────────────────────────────────

/// Canonical nation identity participating in the game.
[<RequireQualifiedAccess>]
type NationId =
    | Germany
    | GreatBritain
    | France
    | Russia
    | AustriaHungary
    | Italy

/// Nation identity construction and formatting helpers.
module NationId =
    /// All canonical nations supported by the game.
    val all: Set<NationId>

    /// Convert a canonical nation identity to its display name.
    val toString: NationId -> string

    /// Parse a canonical nation identity from a display name.
    val tryParse: string -> Result<NationId, string>
