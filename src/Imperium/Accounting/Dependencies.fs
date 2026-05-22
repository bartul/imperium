namespace Imperium.Accounting

type PublishAccountingEvent = AccountingEvent -> Async<unit>

type AccountingDependencies = { Publish: PublishAccountingEvent }
