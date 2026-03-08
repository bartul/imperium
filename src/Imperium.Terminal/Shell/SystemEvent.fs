namespace Imperium.Terminal.Shell

open Imperium.Primitives

type NotificationSeverity =
    | Info
    | Warning
    | Error

type NotificationSource =
    | App
    | RondelHost
    | AccountingHost

type SystemNotification = { Severity: NotificationSeverity; Source: NotificationSource; Message: string }

type SystemEvent =
    | AppStarted
    | NewGameStarted of Id
    | GameEnded
    | MoveNationRequested of string
