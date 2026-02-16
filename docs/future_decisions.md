# Future Decisions

Technology choices and architectural questions that remain open for unimplemented parts of the system.

## Terminal: File Persistence

For replacing in-memory storage with durable local persistence:

| Option                  | Description                    |
| ----------------------- | ------------------------------ |
| JSON (System.Text.Json) | Standard, human-readable       |
| MessagePack             | Binary, fast, compact          |
| SQLite                  | Embedded relational database   |

## Web: Actor Framework

For hosting domain logic in a distributed web environment:

| Option             | Description                          |
| ------------------ | ------------------------------------ |
| Akka.NET           | Mature, feature-rich, complex        |
| Microsoft Orleans  | Virtual actors, simpler model        |
| Proto.Actor        | Lightweight, modern                  |
| Dapr Actors        | Cloud-native, platform-agnostic      |
| None (stateless)   | Simple request/response, no actors   |

## Web: Document/SQL Storage

| Option               | Description                              |
| -------------------- | ---------------------------------------- |
| Marten               | PostgreSQL document store + event sourcing |
| Entity Framework Core | Traditional ORM                          |
| Dapper               | Micro-ORM, manual SQL                    |
| Raw Npgsql           | Direct PostgreSQL access                 |

## Web: Service Bus

For cross-BC messaging in distributed deployment:

| Option                         | Description              |
| ------------------------------ | ------------------------ |
| MassTransit + RabbitMQ         | Self-hosted, mature      |
| MassTransit + Azure Service Bus | Cloud-managed           |
| Wolverine                      | Lightweight, F# friendly |
| NServiceBus                    | Enterprise, commercial   |
| Rebus                          | Simple, lightweight      |

## Open Questions

1. **Event sourcing** — Should the web environment use event sourcing (Marten, Akka.Persistence) or simple state storage?
2. **Read model separation** — Should queries use a separate read model/projection, or query the write model directly?
3. **Game lifecycle** — How are games created, discovered, and cleaned up across environments?
4. **Authentication** — How will players be identified and authorized in the web environment?
5. **Real-time updates** — Should the web environment support real-time notifications (SignalR, WebSockets)?
