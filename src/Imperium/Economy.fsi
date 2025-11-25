namespace Imperium

open System

module Economy =
    /// Shared amount representation across economic flows.
    type Amount = int
    type Bank = Bank of string
    type Investor = Investor of string
