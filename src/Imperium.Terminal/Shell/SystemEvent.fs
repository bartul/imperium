namespace Imperium.Terminal.Shell

open Imperium.Primitives

/// Indicates the importance of a UI-visible system notification.
type NotificationSeverity =
    /// Informational message that does not indicate a problem.
    | Info
    /// Warning message that indicates a non-fatal concern.
    | Warning
    /// Error message that indicates an operation failed.
    | Error

/// Identifies which terminal subsystem raised a system notification.
type NotificationSource =
    /// Notification originated from the shell or application layer.
    | App
    /// Notification originated from the Rondel terminal host.
    | RondelHost
    /// Notification originated from the Accounting terminal host.
    | AccountingHost

/// UI-visible notification published through the terminal bus.
type SystemNotification =
    {
        /// The importance level of the notification.
        Severity: NotificationSeverity
        /// The subsystem that raised the notification.
        Source: NotificationSource
        /// Human-readable message rendered by the UI.
        Message: string
    }

type SystemEvent =
    | AppStarted
    | NewGameStarted of Id
    | GameEnded
    | MoveNationRequested of string
