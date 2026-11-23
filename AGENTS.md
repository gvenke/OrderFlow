# OrderFlow — Codex repository instructions

> Scope: this repository. These instructions summarize prior project discussions as of 2026-11-23. **The checked-out source is authoritative.** If this document conflicts with code, migrations, or tests, inspect the code and report the discrepancy rather than assuming these notes are implemented.

## Project purpose and known technology choices

OrderFlow is a portfolio / learning application for event-driven order processing using **.NET 10**, **C#**, **DDD / Clean Architecture**, **MassTransit 8.x**, **RabbitMQ**, **EF Core**, and **SQL Server**. An **Angular 20 / TypeScript** frontend was part of the original plan; verify whether it exists in the current checkout. The app is intended to demonstrate asynchronous orchestration, persistence, reliable messaging, and service separation.

The following components have been discussed or used; **verify exact paths, assembly names, and current behavior before modifying them**:

- `OrderFlow.Api`: HTTP endpoints and order-submission entry point.
- `OrderFlow.Persistence` / `OrderFlow.Infrastructure.csproj`: EF Core infrastructure, including an `OrderFlowDbContext` in earlier discussions.
- `OrderFlow.Contracts`: shared integration-message contracts.
- Orchestrator host: MassTransit saga / `OrderStateMachine`, saga persistence, and RabbitMQ bus setup.
- `OrderFlow.Inventory`: inventory service under development in the latest conversations, with an `InventoryDbContext` and inventory-reservation message handling.

Other projects, such as application/domain layers, may exist; enumerate the actual solution and project references instead of assuming a fixed structure.

## Grounding and status rules

1. **Do not mistake past plans for shipped functionality.** See `docs/PROJECT_HISTORY.md` for evidence levels and open verification questions.
2. When asked to analyze the architecture, identify current projects from `.sln`/`.slnx` and `.csproj`, trace actual call sites and consumers, and cite **file paths and relevant symbols**.
3. Distinguish: **Confirmed by prior user conversation**, **previously reported but not independently verified**, **in progress**, **planned**, **unknown**. A prior conversation is not a substitute for inspecting current source.
4. Do not silently change architectural patterns, MassTransit endpoint names, message contracts, status transitions, queue topology, or database schema.
5. Before significant edits, state affected files and expected behavior. Prefer small changes and show diffs. Do not run destructive DB/RabbitMQ reset commands unless explicitly requested and scope confirmed.
6. Do not expose secrets or copy secret values into source, documentation, logs, prompts, or commits. Prefer .NET User Secrets for local credentials; check which startup projects require their own `UserSecretsId` / configuration.
7. Never assert tests/builds pass without running them. Report commands and actual results. When local tooling is blocked, say exactly what is blocked.

## Architectural intent to preserve (verify implementation)

- Domain layer should remain independent of framework/infrastructure details; application layer holds use cases and ports/interfaces; infrastructure implements EF Core / message-broker integrations; API hosts compose them.
- Order submission follows an **event-driven, saga-orchestrated** workflow. The previously described functioning milestone was: order transitions from **Draft** through **Pending** on submission; API/bus outbox publishes `OrderSubmitted`; saga receives and persists state; workflow reaches `AwaitingInventory`; an order-status consumer synchronizes status. Exact property names and the current post-submission final status must be read from source.
- Inventory step was being built next: a directed `ReserveInventory` command; `InventoryReserved` / `InventoryReservationFailed` outcomes; durable persistence, idempotency, and outbox/inbox behavior. **Do not assume this flow is complete.**
- MassTransit `Publish` broadcasts to subscribers of a message type; `Send` targets an endpoint. Preserve the intended command-vs-event semantics; validate the actual consumer and endpoint bindings.
- EF Core / SQL Server are the intended durable persistence stack. The app temporarily used an in-memory database earlier; do not assume every service or environment uses SQL Server without examining configuration.
- Successful RabbitMQ messages are acknowledged/removed from their queues; saga instances are stored in saga persistence, not in the RabbitMQ management UI.

## Typical verification workflow

1. Inspect `git status`, `.sln`/`.slnx`, `.csproj` references, `Program.cs` files, appsettings templates (without printing secrets), package versions, and relevant `DbContext` classes.
2. Locate order submission endpoint/handler, `OrderSubmitted`, saga state machine/events/activities, inventory consumer, status consumer, and publisher/outbox registration.
3. Diagram the **implemented** sequence including message type, `Publish` vs `Send`, endpoint/consumer, database commit, retry behavior, and order/saga state transitions.
4. Check EF migrations and whether the database schema is generated from migrations. Do not manually create tables just because a migration or service is mentioned.
5. Propose targeted build/test commands. Run them only when authorized by environment/approval controls; report errors rather than fabricating success.
6. Compare findings with `docs/PROJECT_HISTORY.md`; update the latter only when new evidence confirms a status change.

## Working environment notes

- User's Windows repository location in prior discussion: `C:\Users\glenn\source\repos\OrderFlow`. This is **not** a portable path; use your current workspace instead.
- Windows 11; Visual Studio 2026 Community; local RabbitMQ was hosted in Docker in earlier work.
- Recent Codex Desktop session initially had PowerShell / filesystem timeouts. After restarting, the user reported the PowerShell checks passed. If a file read hangs, diagnose execution access before asking for a source snapshot.
- Git was reported on `master`, working tree clean, two commits ahead of `origin/master` on 2026-11-20; `origin` then incorrectly pointed to `https://github.com` (not a repository URL). **Recheck remote and status; do not assume the remote was repaired.**

## Current handoff objective

**First** inspect the actual current repository and produce a concise, source-cited inventory of projects and the real order-submission → saga → inventory message path. Identify what is finished versus stubbed, with special attention to Inventory consumer/DbContext/EF migrations, MassTransit registration, outbox/inbox, and state transitions. **Do not modify application files until the user requests a change.**

For detailed prior context, see [`docs/PROJECT_HISTORY.md`](docs/PROJECT_HISTORY.md).
