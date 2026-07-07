namespace Imperium.Rondel

// ──────────────────────────────────────────────────────────────────────────
// Spaces
// ──────────────────────────────────────────────────────────────────────────

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
    /// Spaces in clockwise board order for distance calculation.
    let private spacesInOrder =
        [| Space.Investor
           Space.Import
           Space.ProductionOne
           Space.ManeuverOne
           Space.Taxation
           Space.Factory
           Space.ProductionTwo
           Space.ManeuverTwo |]

    /// Calculate clockwise distance between two spaces.
    let distance fromSpace toSpace =
        let fromIndex = Array.findIndex ((=) fromSpace) spacesInOrder
        let toIndex = Array.findIndex ((=) toSpace) spacesInOrder

        if toIndex >= fromIndex then
            toIndex - fromIndex
        else
            (Array.length spacesInOrder - fromIndex) + toIndex

    let toString space =
        match space with
        | Space.Investor -> "Investor"
        | Space.Import -> "Import"
        | Space.ProductionOne -> "ProductionOne"
        | Space.ManeuverOne -> "ManeuverOne"
        | Space.Taxation -> "Taxation"
        | Space.Factory -> "Factory"
        | Space.ProductionTwo -> "ProductionTwo"
        | Space.ManeuverTwo -> "ManeuverTwo"

    let fromString s =
        match s with
        | "Investor" -> Ok Space.Investor
        | "Import" -> Ok Space.Import
        | "ProductionOne" -> Ok Space.ProductionOne
        | "ManeuverOne" -> Ok Space.ManeuverOne
        | "Taxation" -> Ok Space.Taxation
        | "Factory" -> Ok Space.Factory
        | "ProductionTwo" -> Ok Space.ProductionTwo
        | "ManeuverTwo" -> Ok Space.ManeuverTwo
        | _ -> Error $"Invalid rondel space: {s}"

    let toAction space =
        match space with
        | Space.Investor -> Action.Investor
        | Space.Import -> Action.Import
        | Space.ProductionOne
        | Space.ProductionTwo -> Action.Production
        | Space.ManeuverOne
        | Space.ManeuverTwo -> Action.Maneuver
        | Space.Taxation -> Action.Taxation
        | Space.Factory -> Action.Factory
