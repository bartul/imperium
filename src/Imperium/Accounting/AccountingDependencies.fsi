namespace Imperium.Accounting

// ──────────────────────────────────────────────────────────────────────────
// Dependencies
// ──────────────────────────────────────────────────────────────────────────

/// Publish accounting domain events to the event bus.
/// CancellationToken flows implicitly through Async context.
type PublishAccountingEvent = AccountingEvent -> Async<unit>

/// Unified dependencies for all Accounting handlers.
type AccountingDependencies = { Publish: PublishAccountingEvent }
