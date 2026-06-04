namespace Imperium.Gameplay

// ──────────────────────────────────────────────────────────────────────────
// Domain Values
// ──────────────────────────────────────────────────────────────────────────

/// Validated roster of distinct players taking part in a game.
type PlayerRoster = private PlayerRoster of Set<PlayerId>

/// Player roster construction and access helpers.
module PlayerRoster =
    /// Build a player roster from player Guids, validating count and uniqueness.
    val create: System.Guid list -> Result<PlayerRoster, string>

    /// Return the distinct players in the roster.
    val value: PlayerRoster -> Set<PlayerId>
