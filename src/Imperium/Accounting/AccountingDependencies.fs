namespace Imperium.Accounting

// ──────────────────────────────────────────────────────────────────────────
// Dependencies
// ──────────────────────────────────────────────────────────────────────────

type PublishAccountingEvent = AccountingEvent -> Async<unit>

type AccountingDependencies = { Publish: PublishAccountingEvent }
