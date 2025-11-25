namespace Imperium

open System

module Economy =
    [<Measure>]
    type M // million

    /// Shared amount representation across economic flows (millions).
    type Amount = private Amount of int<M>

    module Amount =
        [<RequireQualifiedAccess>]
        type Error =
            | NegativeAmount of string

        val create : millions:int -> Result<Amount, Error>
        val unsafe : millions:int -> Amount
        val value : Amount -> int
        val zero : Amount
        val (+) : Amount -> Amount -> Amount
        val (-) : Amount -> Amount -> Amount
        
    type Bank = Bank of string
    type Investor = Investor of string