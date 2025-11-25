namespace Imperium

open System

module Economy =
    [<Measure>]
    type M // million

    // Shared amount representation across economic flows (millions).
    type Amount = private Amount of int<M>

    module Amount =
        [<RequireQualifiedAccess>]
        type Error =
            | NegativeAmount of string

        let create (millions: int) =
            if millions < 0 then
                Error(NegativeAmount "Amount cannot be negative (millions).")
            else
                Ok (Amount(millions * 1<M>))

        let unsafe (millions: int) = Amount(millions * 1<M>)
        let value (Amount v) = int v

        let zero = Amount 0<M>
        let (+) (Amount a) (Amount b) = Amount(a + b)
        let (-) (Amount a) (Amount b) = Amount(a - b)
    type Bank = Bank of string
    type Investor = Investor of string
