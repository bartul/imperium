namespace Imperium

open World

module MonetarySystem =

    type Bank = Bank of string     
    type Investor = Investor of string     
    
    type Account = Bank | InvestorAccount of Investor | NationAccount of Nation

    type Payment = { Amount : int; OriginAccount : Account; DestinationAccount : Account  }
