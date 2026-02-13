namespace Imperium.Terminal.Shell

open Imperium.Primitives

type SystemEvent =
    | AppStarted
    | NewGameStarted of Id
    | GameEnded
    | MoveNationRequested of string
