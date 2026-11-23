# OrderFlow — Project history, architecture and handoff

**Compiled:** 2026-11-23  
**Source:** Summaries and excerpts of previous ChatGPT conversations, not a fresh inspection of the repository.  
**Purpose:** Give Codex continuity while preventing assumptions about what has actually been implemented.

## How to interpret evidence

- **User-confirmed / discussed as working** — a prior exchange explicitly indicated this behavior worked or existed at that time. It still needs comparison with the current checkout.
- **Reported in prior discussion** — prior conversation referenced a class, file, command, or behavior; exact current implementation is not established.
- **In progress / recently discussed** — user was implementing or debugging this component, but completion is not established.
- **Planned / proposed** — design direction or suggested next step, not evidence of working code.
- **Unknown / verify** — no reliable record of the current implementation.

**Always prefer repository files, migrations, tests, and runtime behavior over the historical notes below.** Do not turn a proposal into a claim of completion.

## 1. Original goal and stack

The user initiated **OrderFlow** as a portfolio application with a **.NET 10 backend** and **Angular 20 / TypeScript frontend**, multiple Web API endpoints, and asynchronous POST/order processing using **RabbitMQ and MassTransit saga orchestration**, with an **outbox**. The requested design style was **Domain-Driven Design (DDD)** and **Clean Architecture**.

Technologies discussed or used:

| Technology / principle | Historical evidence | Current status |
| --- | --- | --- |
| .NET 10 / C# / ASP.NET Core Web API | Original requested stack; active C# code troubleshooting | Reported; inspect target frameworks |
| Angular 20 / TypeScript | Original requested frontend | Planned; existence not verified |
| DDD and Clean Architecture | Explicit architecture requirement | Design intent; inspect layer boundaries |
| MassTransit 8.x | User discussed saga, consumers, bus registration, outbox | Reported in active use |
| RabbitMQ 3 in Docker | User discussed management UI, local credentials and errors | Reported in active use; verify running instance |
| EF Core with SQL Server | Moved away from temporary in-memory provider; migrations discussed | Reported in use; verify each context/provider |
| Outbox / inbox | Specific tables, lifecycle, and publisher DI discussed | Reported; verify actual registrations and migrations |
| Saga orchestration | `OrderStateMachine`, orchestrator startup and persisted state discussed | Earlier order-submission milestone described as working |
| Inventory microservice | Active implementation and troubleshooting on 2026-11-20 | In progress; end-to-end readiness unverified |

## 2. Project / layer names mentioned in conversations

**These names are historical references, not a validated directory listing.** Codex should enumerate the current `.sln` / `.slnx` and `*.csproj` files.

- `src/OrderFlow.Api` — API host and order submission functionality.
- `src/OrderFlow.Persistence` — folder cited in EF tooling; its project file was named `OrderFlow.Infrastructure.csproj` in earlier examples.
- `OrderFlow.Contracts` — integration contracts.
- Orchestrator project/host — saga state machine and its persistence / bus setup. Exact current project name not proven.
- `src/OrderFlow.Inventory` — Inventory service; `Infrastructure/InventoryDbContext.cs` was referenced in a compiler error.
- Application / Domain / Infrastructure layers — part of Clean Architecture design; exact current project boundaries need inspection.

A separate `OrderFlow.Orchestrator` name appeared in discussion examples, but must not be assumed to match an actual `.csproj` without checking.

### Layering intent

- **Domain:** entities, invariants, domain rules; avoid broker/EF/HTTP dependencies.
- **Application:** use cases, commands/handlers, interfaces/ports, orchestration of domain operations.
- **Infrastructure/Persistence:** EF Core DbContexts, repositories, MassTransit integration, external services.
- **API:** HTTP contracts, routing, dependency injection / host composition.
- **Orchestrator:** separate running process for the saga (the user asked whether it should start alongside the API).
- **Inventory:** independent message consumer/service with its own persistence concerns.

This is a design orientation, not proof that every class currently follows it.

## 3. Order submission — last described working milestone

A conversation dated around **2026-11-19** described the submission workflow as established and the next step as Inventory. The previous assistant summarized the following as the then-working flow:

1. An order starts in **Draft**.
2. Submission changes the order to **Pending** (potentially briefly).
3. A MassTransit **bus outbox** records and publishes `OrderSubmitted` reliably after database work.
4. The **orchestrator** consumes the message; the saga persists its `OrderState` and moves into **AwaitingInventory**.
5. A status consumer updates order status; in conversation the user asked why it became **Processing** after briefly being Pending.
6. Inventory reservation was identified as the next milestone, rather than a fully verified outcome of that flow.

**Evidence level:** previously described as working in chat, **not independently re-verified against the current checkout**. The exact status values, sequence, delivery guarantees, and consumer method names must be traced from source. In particular, do not assert that Inventory reservation succeeds end-to-end just because the saga reaches `AwaitingInventory`.

### Components and symbols to locate

- Order submission API action / handler / domain guard.
- `OrderSubmitted` integration event.
- `MassTransitEventPublisher` and `IPublishEndpoint` (the user asked whether `AddMassTransit` supplies this dependency).
- `OrderStateMachine`, correlation by `OrderId`, saga events and `InstanceState(x => x.CurrentState)`.
- `OrderStatusConsumer` or whichever consumer updates the order read model / status.
- Outbox registration (`AddEntityFrameworkOutbox` if used), broker endpoints, transport topology.

**Do not assume any exact code signature until located.**

## 4. Inventory microservice — most recent development

On **2026-11-20**, the user was actively building `OrderFlow.Inventory` and asking about:

- A compiler error in `Infrastructure/InventoryDbContext.cs` that the namespace `Microsoft.EntityFrameworkCore` was missing — likely a missing project/package reference, but the conversation excerpt does **not** prove the final fix.
- Whether to create the Inventory database manually before EF migrations (the intended path is ordinarily EF migrations for schema management, but current migration state is unknown).
- Reusing RabbitMQ **User Secrets** across projects and retrieving current user-secret values.
- Whether Inventory runs as a background service like the orchestrator (expected to run a host with a long-lived MassTransit consumer; verify implementation).
- Saga `Publish` versus `Send`, especially using **`Send` for `ReserveInventory`** directed to an inventory endpoint rather than publishing it as a broadcast message.
- `Ignore(OrderSubmitted)` while in `AwaitingInventory` and whether ignoring duplicate events is useful given domain-level submission guards. The user explored the distinction between domain invariants and message/saga idempotency; the actual policy should be inspected before changing it.

### Proposed Inventory command/event flow

This flow was **planned / under development**, not confirmed complete:

```text
OrderSubmitted (event)
    -> Order saga (correlate by OrderId)
    -> ReserveInventory (command; Send to Inventory endpoint)
    -> Inventory consumer loads / reserves stock transactionally
    -> InventoryReserved OR InventoryReservationFailed (outcome event)
    -> Order saga advances / fails / compensates as designed
    -> Order status updated via consumer / event
```

Potential implementation requirements that were discussed include EF-backed Inventory tables, consumer outbox, retry-safe reservations, idempotency, and concurrency protection. **These are design considerations, not claims that the code already includes them.**

### Specific verification questions

1. Does `OrderFlow.Inventory` compile, and which EF Core package versions / references does it use?
2. Does its `Program.cs` register DbContext, MassTransit consumer(s), receive endpoints, and RabbitMQ settings?
3. Do Inventory EF migrations exist and apply cleanly? Is the Inventory schema isolated from order/saga storage?
4. Is `ReserveInventory` sent to the correct endpoint and bound to exactly the intended consumer?
5. Are `InventoryReserved` and `InventoryReservationFailed` published, consumed, and correlated by order ID?
6. Is stock adjustment protected from duplicate deliveries and concurrent reservations?
7. Is `Ignore(OrderSubmitted)` intentional for the waiting saga state; what happens on retries, duplicates, and faults?

## 5. Messaging, outbox, inbox, and saga persistence

### Confirmed discussion topics / behavior

- **MassTransit `Publish` vs `Send`:** `Publish` distributes an event to each subscribed endpoint; `Send` addresses a particular endpoint/queue. A published command could reach multiple subscribers, which is why the user discussed the consequences of publishing `ReserveInventory`.
- **Successful RabbitMQ messages:** consumed messages are normally acknowledged and no longer appear as pending queue messages. RabbitMQ management does **not** display persisted saga instances; those are in the saga database.
- **Saga correlation:** discussion included `Event(() => OrderStarted, x => x.CorrelateById(m => m.Message.OrderId))` and `InstanceState(x => x.CurrentState)`; current event names may differ.
- **Idempotency:** the user compared domain-level protections against duplicate submission with saga state handling like `Ignore(OrderSubmitted)` while `AwaitingInventory`.

### MassTransit EF tables previously examined

| Table | Intended role | Verification needed |
| --- | --- | --- |
| `OutboxMessage` | Durable pending outbound messages awaiting bus delivery | Where registered; which DbContext/schema |
| `OutboxState` | Tracks outbox delivery/progress | Presence in each service using bus outbox |
| `InboxState` | Tracks consumed messages for deduplication in consumer outbox / inbox scenarios | Which consumers use it; configured cleanup/retention |
| Saga state table (often `OrderState`) | Durable saga instance and `CurrentState`/correlation | Actual entity, mapping, DB and migration |

Previous discussions also referred to **saga-prefixed / saga-host outbox tables**. Their precise names and presence should be checked in migrations rather than inferred.

### Known RabbitMQ troubleshooting history

- Local broker was described as **RabbitMQ 3 in Docker**, initially with standard `guest/guest` settings; the user asked about creating/changing credentials and storing them in User Secrets.
- On or around **2026-11-12**, the orchestrator failed with `RabbitMqConnectionException`, `Broker unreachable`, and `ACCESS_REFUSED - Login was refused using authentication mechanism PLAIN` against `localhost:5672` and the `order-saga` receive transport. That log establishes a past authentication issue, **not its current status**.
- No user-secret credentials should ever be inserted into `AGENTS.md`, this history file, source control, or Codex prompts.

## 6. EF Core, databases, and migrations

The user previously used a temporary in-memory provider and later moved to **SQL Server / EF Core migrations**. A discussed CLI command was:

```powershell
dotnet ef database update `
    --project .\src\OrderFlow.Persistence\OrderFlow.Infrastructure.csproj `
    --startup-project .\src\OrderFlow.Api\OrderFlow.Api.csproj `
    --context OrderFlowDbContext
```

**Treat that command as historical**, not guaranteed correct for the current solution or Inventory context. Confirm actual project paths, startup host, connection string, design-time factory and migration assembly first.

Related points discussed:

- Migrations apply in chronological/dependency order as tracked in `__EFMigrationsHistory`.
- Switching to an older source branch with fewer migrations can leave the DB schema ahead of the code; rollback requires care and, for shared databases, should not be improvised.
- The user asked about a nullable `Order.Id` becoming an identity-backed integer and whether the model still needs `HasKey`.
- The user discussed clearing **data, saga instances, and pending messaging state** while preserving schema; any such operations are destructive and should be separately scoped/approved.
- SQL Server schema and Inventory database creation should be driven by the application's migration/setup plan, not blind manual table creation.

## 7. Configuration / startup / dependency injection

Historical questions included:

- `services.AddMassTransit(...)` registration and whether it registers `IPublishEndpoint` for injection into `MassTransitEventPublisher` (it does in normal MassTransit DI setups; inspect this repository's setup).
- Missing `AddApplication()` extension during startup wiring.
- `UseSqlServer` package references and installing `Microsoft.EntityFrameworkCore.Design` for EF tooling.
- Adding `UserSecretsId` to the API startup project and whether the Orchestrator / Inventory need separate secret configuration.
- Starting the Orchestrator concurrently with the Orders API; the orchestrator is a separate long-running process/host rather than merely a one-shot job if it is consuming messages.
- `Directory.Packages.props` as central package management and its inclusion in Visual Studio Solution Items.

Inspect the actual `Program.cs` files, configuration providers, and `*.csproj` files before prescribing modifications.

## 8. Troubleshooting and other tracked issues

| Topic | Historical observation | Status today |
| --- | --- | --- |
| API POST returned HTTP 500: "No route matches the supplied values" | User later said this was fixed | User-reported resolved at the time |
| Orchestrator RabbitMQ auth | `ACCESS_REFUSED`, broker connection failure | Prior issue; current status unverified |
| Inventory EF namespace | `Microsoft.EntityFrameworkCore` missing in `InventoryDbContext.cs` | Recently reported; fix unverified |
| RabbitMQ queues appear empty after successful processing | Explained as acknowledgment/removal behavior | Expected behavior; not a defect by itself |
| Git origin | On 2026-11-20, `origin` fetch/push URL displayed only `https://github.com`; local `master` was ahead by two commits | Remote repair unverified; avoid pushing blindly |
| Codex Desktop shell | Initial command execution and direct read timed out; after restart PowerShell tests reportedly passed | Shell tests user-confirmed passing; project inventory not yet confirmed |

## 9. Proposed next steps at handoff

The last clearly identified milestone was **Inventory reservation integration**. Codex should not immediately implement speculative features. Instead:

1. **Inventory current source:** enumerate projects, target framework, references, and state of the Inventory host, consumer, DbContext, entities, and migrations.
2. **Trace one order:** HTTP submission → domain change → database commit and bus outbox → `OrderSubmitted` → orchestrator saga → `ReserveInventory` → outcome → status update. Mark any missing links.
3. **Check reliability:** correlation IDs, duplicate messages, retry/poison behavior, transactional reserve, concurrency, MassTransit inbox/outbox, and saga persistence.
4. **Build/test:** run appropriate builds/tests and report genuine output. Verify local dependent services (SQL Server, RabbitMQ) separately if needed.
5. **Recommend smallest next change:** propose code edits and tests; wait for user approval to modify application code.

### Suggested first Codex prompt

> Examine the actual OrderFlow repository. List every project and reference, then trace the implemented order submission and Inventory reservation path using source file paths and symbol names. Compare it against `docs/PROJECT_HISTORY.md`, distinguishing working code, partially implemented code, and historical plans. Check for missing Inventory EF packages/migrations, consumer registration, saga transitions, inbox/outbox configuration, and duplicate handling. Do not modify files; report findings and recommended next step.

## 10. Caveats and update procedure

- This is a **historical handoff**, not a comprehensive transcript or verified build report.
- Earlier chats contain exploratory questions, hypothetical designs, and assistant suggestions. Do **not** treat every suggestion as a finalized user decision.
- Specific source paths, names, database tables and status values may have changed between branches.
- If source code contradicts this history, document the actual behavior and record the discrepancy rather than modifying code merely to match these notes.
- When the user confirms a milestone and code/tests support it, update the relevant section with date, evidence, and any still-open questions. Keep `AGENTS.md` concise and put evolving details here.
