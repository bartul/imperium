namespace Imperium

open Structure
open MonetarySystem

module Rondel =

    type Position = Investor | Import | Production1 | Maneuver1 | Taxation | Factory | Production2 | Maneuver2
    type NationPosition = { Nation : Nation; Position : Position }
        
    let move (nationPosition : NationPosition) (nationPowerFactor : int) (position : Position) (payment : Payment) =
        if nationPowerFactor = 1 then
            Ok nationPosition
        else
            Error ""
