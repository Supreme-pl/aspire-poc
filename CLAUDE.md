# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repo status

Phase 5 complete: one end-to-end integration test using `Aspire.Hosting.Testing`, spinning up the whole AppHost (Redis + RedisInsight + Redpanda + Redpanda Console + reference-service + app1 + app2) and asserting a known batch flows through the pipeline with correct discount calculation landing in a per-test-run CSV file. Producer is disabled in tests via `--Producer:Enabled=false`. Topic and consumer group are uniquified per run via `--Kafka:Topic` and `--Kafka:ConsumerGroup` args to prevent cross-test interference. Runtime ~20–90s depending on whether container images are already pulled.

## Running tests

```bash
dotnet test integration-tests/src/AspirePoc.IntegrationTests.csproj
```

Configuration overrides are passed to `DistributedApplicationTestingBuilder.CreateAsync` as `string[] args` in `--Key=Value` form. This is the *only* reliable way to override config values — setting `appHost.Configuration[...]` after `CreateAsync` is too late because AppHost's Program.cs has already executed and built the resource graph with default values.

Each test:
- Creates a unique `runId` (8-char GUID)
- Computes a unique temp output folder path
- Passes `Kafka:Topic`, `Kafka:ConsumerGroup`, `Output:Path`, and `Producer:Enabled=false` as args
- Waits for `app1` and `app2` resources to reach `Healthy` state via `app.ResourceNotifications.WaitForResourceHealthyAsync`
- POSTs a test batch, polls the CSV file until expected lines appear (fixed timeout, no fixed sleeps per architect plan)

AppHost was refactored to read these config keys and propagate them to child projects via `WithEnvironment`, and to conditionally add the producer only when enabled. Default values preserve the original "dotnet run" experience.

## Previous phase status

Phase 3d: the ETL runs hands-off. `AspirePoc.Producer` is a .NET worker (`BackgroundService`) that generates synthetic batches on a `PeriodicTimer` and POSTs them to app1's `/process` endpoint. The graph self-drives — no manual curl needed.

- Producer resolves app1 via Aspire service discovery (`http://app1` → actual endpoint at runtime)
- Customer pool is weighted toward `C-100`/`C-101` so cache hits are visible in the dashboard and Redpanda Console consumer-group lag; `C-999` shows up occasionally to keep the unknown-customer (skip-warn) path exercised
- Amounts are generated from integer cents to keep every decimal exact, matching the project's money discipline
- Configurable via `Producer:IntervalSeconds` (default 10) and `Producer:BatchSize` (default 5). First batch fires immediately on startup, subsequent batches every interval.
- `Batch` and `Transaction` are **duplicated** in producer/src/ (not shared with app1 — per the no-shared-code rule; in a real split-repo world, producer would be a third-party system entirely)

AppHost: producer has `WithReference(app1)` and `WaitFor(app1)` so it starts only after app1 is ready.

## Previous phase status

Phase 3c: app2 is now a Kafka consumer closing the ETL loop.

- `KafkaConsumerService` (BackgroundService) subscribes to `transactions.enriched`, reads messages with `AutoOffsetReset.Earliest` so restarts replay the topic from the beginning
- Deserializes each `EnrichedTransaction`, hands it to `TransactionProcessor`
- `TransactionProcessor` wraps incoming `decimal Amount` + `string Currency` into a `Money` value object, applies the discount with banker's rounding (`Math.Round(..., 2, MidpointRounding.ToEven)`), formats all numeric fields with `CultureInfo.InvariantCulture`, and appends a row to the CSV output
- CSV output path resolution: `Output:Path` from config, else `{Path.GetTempPath()}/aspire-poc/transactions.csv`. Full path is logged at startup
- CSV header written on first write when the file does not exist
- Per-record string fields are CSV-escaped (quotes + embedded comma/quote/newline handling)
- Consumer group is fixed at `app2-consumer-group` (will need uniquing per integration test run in a later phase)
- Both apps call `KafkaTopicEnsurer.EnsureAsync` at startup to idempotently create `transactions.enriched` before producer/consumer initialize. The helper is **duplicated** in app1 and app2 per the no-shared rule. The fix addresses the librdkafka behavior where `topic.metadata.refresh.interval.ms` defaults to 5 minutes — without pre-creation, a consumer that subscribes before the topic exists would not discover it for up to 5 minutes after it's created by the producer's first write.
- The CSV output file at `{Path.GetTempPath()}/aspire-poc/transactions.csv` is **deleted on app2 startup** so each AppHost run produces a clean file. The file is not managed by Aspire (it lives outside the container graph), so without this, rows accumulate across runs. Intentional truncation avoids cross-run confusion during demo.

### Development GUIs

Two visual tools are wired into the AppHost for local inspection:

- **RedisInsight** — added via `AddRedis("cache").WithRedisInsight()`. Aspire handles the wiring; the UI resource points at the `cache` Redis instance automatically. Use it to browse keys (`customer:C-100`, etc.) and inspect values.
- **Redpanda Console** — added as a raw `AddContainer` for `redpandadata/console:v2.7.0`. Connects to Redpanda via the **internal listener** `redpanda:9093` — Aspire creates a Docker network DNS alias matching each resource name, so the Console resolves `redpanda` to the Redpanda container within the shared network. Use it to browse topics, inspect messages, and watch consumer group lag in real time.

Redpanda is configured with **two Kafka listeners**:
- `external` on host port 9092 advertised as `localhost:9092` — for apps running on the host
- `internal` on 9093 advertised as `redpanda:9093` — for container-to-container traffic (Console, future producers/consumers running as containers)

This split is the standard Kafka networking pattern. Clients initiating via one listener receive metadata referring to *that listener's* advertised address, so there's no cross-network confusion. Without this split, a single advertised address (e.g. `localhost:9092`) works for one client class but breaks the other.

Both open via their endpoints in the Aspire dashboard's Resources tab.

### Known observability gap

`Confluent.Kafka` 2.14 does not emit OpenTelemetry spans by default for produce/consume calls, so the Kafka hops between app1 and app2 do **not** appear in the dashboard's Traces tab. The trace starts at `POST /process` on app1 and ends at the last Redis/HTTP span. app2's message processing generates its own separate traces keyed on the consume call. Closing the trace continuity across Kafka requires either the `OpenTelemetry.Instrumentation.ConfluentKafka` package or manual `ActivitySource` wiring in the producer and consumer — not addressed in current phases.

`Money` and `EnrichedTransaction` are **duplicated** between app1 and app2 by design — see the "No shared-code project" note in the Folder layout section. Keep the two copies in sync manually when either shape changes.

AppHost: both `app1` and `app2` now `WaitFor(kafka)` so they start after the Redpanda container reports running. This does not guarantee Kafka is accepting connections at that instant — the Confluent clients retry internally, which handles the remaining race.

Previous phases: app1 enrichment with cache-aside (3b), reference-service fixture API (3a), Redpanda + Redis in AppHost (2), project scaffolding (1). `AspirePoc.slnx` builds clean. No producer service yet, no frontend, no tests.

## Build and run

```bash
dotnet build AspirePoc.slnx
dotnet run --project apphost/src
```

Running the AppHost prints a dashboard URL (http://localhost:17xxx) and a login token in the console. Open the URL, paste the token, and you see the resource graph, logs, traces, and metrics.

Solution format is the new XML-based `.slnx` (not legacy `.sln`). `dotnet` CLI handles it natively.

### First-time local setup

The Aspire dashboard's OTLP ingest endpoint runs over HTTPS with the .NET dev cert. If the cert is not trusted, child apps silently fail to export telemetry — HTTP endpoints still respond, but the Traces/Metrics/Structured logs tabs stay empty. Fix once per machine:

```bash
dotnet dev-certs https --trust
```

### Local infra requirements

Redpanda is declared with `AddContainer` (per architect plan) pinned to `redpandadata/redpanda:v24.2.4` and binds host port **9092** for Kafka. Redis uses the Aspire-managed container. Docker must be running. If port 9092 is already in use, AppHost fails to start the Redpanda container — free the port or change the binding in `AppHost.cs`.

## What this is

A .NET Aspire POC demonstrating a streaming ETL pipeline. Goal is to showcase Aspire's dev/demo value (one-command startup, dashboard with OTel traces/logs/metrics across services) on a realistic but self-contained data flow. It is not production code and is not intended to become production code.

## Source of truth for design

`plan_from_gpt.md` captures the intent of the project architect (not AI suggestions). Technology choices documented there (Redpanda, Redis, GHCR if used, `/process` endpoint pattern, image pinning, etc.) are approved decisions — do not propose replacements without the user asking. The Jira ticket (ALCO-60) is intentionally terse; `plan_from_gpt.md` takes precedence over it.

Deviations from `plan_from_gpt.md` agreed with the user so far. Each row notes source lineage and whether the architect has confirmed:

| Deviation | Lineage | Architect confirmed? |
|---|---|---|
| **Monorepo layout** (one repo, folders for each app) instead of three repos | User decision, explicit | No — user intends to discuss, but this is a user preference for POC speed |
| **Mock producer** replaces literal "file drop" Extract step | Interpretation of plan's `/process` endpoint trigger clause | **No — pending** |
| **Mock reference-service** as the "stubbed dependency" | Plan says "any stubbed dependency needed for reference-data enrichment"; interpreted as a separate HTTP service rather than bundled fixture or Redis-preload | **No — pending** |
| **Redis cache** for reference-data lookup (cache-aside) | Not in plan body. Sourced from the Jira ticket appended to `plan_from_gpt.md`: *"Consider Redis cache in 1 of APIs"*. Tacitly accepted through design conversation | **No — pending** |

**Important:** items 2-4 above were derived during conversation, not directly from the plan body. They should be explicitly reviewed with the architect before the POC is presented as "what the plan said". If the architect's intent was something different (e.g. reference data baked into Redis on startup, or into app1 as a bundled fixture), the architecture collapses accordingly and reference-service + cache-aside can be removed.

## Target architecture

Streaming ETL, event-driven, HTTP ingest:

```
Mock producer ──interval HTTP POST──> app1 ──publish──> Redpanda ──> app2 ──> output.csv
                                       │
                                       ├──cache-aside──> Redis
                                       │
                                       └──on miss──> reference-service
```

Resources in the Aspire graph:
- `producer` — .NET project, generates synthetic batches on interval, pushes to app1 via HTTP
- `app1` — .NET minimal API, exposes `POST /process`, enriches with reference data (cache-aside via Redis), publishes enriched events to Redpanda
- `app2` — .NET minimal API/consumer, consumes from Redpanda, calculates, writes CSV
- `reference-service` — .NET minimal API, serves customer/reference master data from local fixture (source of truth for enrichment)
- `cache` — Redis container, cache-aside populated by app1
- `redpanda` — Kafka-wire-compatible broker between app1 and app2
- `frontend` — Angular + TypeScript app, demonstrates Aspire dashboard value end-to-end (trace from UI click → backend pipeline)

Apps are **Aspire-agnostic**: they read connection details from `IConfiguration` and run standalone with `dotnet run` given the right env vars. The AppHost is a thin orchestration layer on top — not a dependency of the apps themselves.

Redis caches reference data (e.g. `customer:C-123` → customer master), not producer payloads. Producer payloads flow through the pipeline but are never cached.

## Folder layout

Current:
```
aspire-poc/
├── AspirePoc.slnx
├── CLAUDE.md
├── plan_from_gpt.md
├── apphost/src/            AspirePoc.AppHost (Aspire.AppHost.Sdk)
├── service-defaults/src/   AspirePoc.ServiceDefaults (shared OTel, health, service discovery)
├── app1/src/               AspirePoc.App1 (ASP.NET Core empty)
├── app2/src/               AspirePoc.App2 (ASP.NET Core empty)
└── reference-service/src/  AspirePoc.ReferenceService (customer master data fixture + API)
```

Planned additions (later phases):
- `integration-tests/src/` + `integration-tests/fixtures/` — Phase 5 (in progress)
- `.github/workflows/aspire-poc.yml` — Phase 6 CI
- `indexer/src/` — Phase 7, new Kafka consumer that indexes `transactions.enriched` into OpenSearch (see "OpenSearch integration" below)
- `app1/tests/`, `app2/tests/`, `reference-service/tests/` — per-project unit tests (no date)

**Frontend is no longer planned.** The Jira ticket suggested "Consider... frontend app as well to show Aspire features". Since the user applies the rule "do not use Jira as source of requirements" and the plan body never mentions a frontend, frontend work is cancelled.

### OpenSearch integration (Phase 7, future)

Once tests + CI are green, a new service will be added that consumes the `transactions.enriched` topic (different consumer group from `app2-consumer-group`, so both consumers run in parallel and independently) and indexes each event into OpenSearch. This sits alongside the CSV sink, not replacing it — demonstrates Aspire orchestrating a second Kafka consumer and shows the value of event streaming (fan-out to multiple sinks).

- **Local dev**: OpenSearch container via `AddContainer("opensearch", "opensearchproject/opensearch:2.x")` with single-node discovery enabled
- **Production**: AWS OpenSearch Service endpoint injected via env var (user will provision separately)
- **Client**: `OpenSearch.Client` (official .NET SDK) or `OpenSearch.Net`
- **New service name**: `AspirePoc.Indexer`
- **What's indexed**: `EnrichedTransaction` payload with the computed `finalAmount` (so the index represents fully-processed transactions, not raw events). Alternative: index raw and let OpenSearch compute via runtime fields — to decide later.

Same no-shared-code rule applies: the indexer gets its own copies of `EnrichedTransaction` and any calc logic it needs.

**No shared-code project.** Domain types needed by both app1 and app2 (e.g. `Money`, transaction DTOs) are duplicated in each app rather than extracted — this mirrors the real-world scenario where the two apps live in separate repos. `ServiceDefaults` and `ReferenceService` are shared infrastructure and do not fall under this rule.

Project name convention: `AspirePoc.<Component>`. The generated `Projects.AspirePoc_App1` type (used in `AppHost.cs` as `builder.AddProject<Projects.AspirePoc_App1>("app1")`) is produced automatically by the Aspire SDK from the AppHost's `<ProjectReference>` — dots in project names become underscores in the generated type.

CI workflow will live at `../.github/workflows/aspire-poc.yml` at the UNIVERSAL repo root with a `paths: aspire-poc/**` filter, so only changes under this folder trigger it.

## Stack

- **.NET 10** (`dotnet --list-sdks` must show 10.x)
- **Angular + TypeScript** for the frontend (project standard — not negotiable, do not substitute Blazor/React)
- **Aspire**: `Aspire.Hosting.AppHost`, `Aspire.Hosting.Redis`, `Aspire.Hosting.NodeJs`, `Aspire.Hosting.Testing` for tests
- **Redis** via `StackExchange.Redis`
- **Kafka client** (e.g. `Confluent.Kafka`) against Redpanda
- **Docker** (user has it; do not propose alternatives)

## Code conventions (strict)

### No comments
Do not write comments in any language (C#, TypeScript, Angular templates, YAML, etc.). Code must be self-documenting: expressive names, small functions, obvious control flow. The only exception is when a comment captures a non-obvious *why* that a reader cannot derive from the code itself (a subtle invariant, a workaround for a specific external bug, a hidden constraint). Never comment *what* the code does, never reference tasks/PRs/tickets, never leave `// TODO` markers — use the issue tracker.

### Clean code principles
These are enforced both during implementation and during any code review pass:

- **SOLID**:
    - **SRP**: one reason to change per class/module. If a class needs `and` in its description, split.
    - **OCP**: extend via new types/handlers, not by editing existing logic when it already works for existing cases.
    - **LSP**: subtypes must be substitutable; no "throws NotImplementedException" in overrides.
    - **ISP**: narrow interfaces; consumers should not depend on methods they do not use.
    - **DIP**: depend on abstractions at module boundaries (Redis client, Kafka producer/consumer, reference-service client, file writer) — so app1/app2 can be unit-tested without Aspire or real infra.
- **KISS**: prefer the obvious solution. No premature generalization, no speculative abstraction, no hypothetical-future-requirement scaffolding.
- **DRY**: extract genuine duplication, but only after it appears ≥2-3 times and the duplicated pieces actually represent the same concept. Coincidental similarity is not duplication.
- **YAGNI**: do not add configuration, flags, error handling, or extension points for scenarios that are not currently required.

### Boundaries of validation
Validate at system boundaries only — HTTP ingress (`/process` in app1), Kafka consume in app2, reference-service responses. Internal code trusts its callers. No defensive null-checks on values the type system already guarantees.

### Logging and tracing
Rely on OpenTelemetry via Aspire defaults (structured logs, traces, metrics land in the dashboard automatically for .NET projects that register `AddServiceDefaults`). Do not roll custom logging abstractions. Use semantic log scopes (`using var scope = logger.BeginScope(...)`) for correlating multi-step operations.

### Error handling
Let exceptions propagate unless you have a concrete recovery strategy. Do not wrap in try/catch to "log and rethrow" — OTel already captures the exception with the trace. Validate input at boundaries (400 on bad HTTP request, DLQ on bad Kafka message) rather than scattering guards.

## When building, prefer Aspire-agnostic first

When implementing a new service (app1, app2, etc.), make it runnable standalone with `dotnet run` and a local `appsettings.Development.json` before wiring it into AppHost. The AppHost only adds orchestration convenience — the app must not require it to function.

## Testing philosophy

- **Unit tests** per project: pure logic (enrichment mapping, calculation rules, producer batch assembly). Fast, no infra.
- **Integration tests** in `integration-tests/` use `Aspire.Hosting.Testing` to spin up the full AppHost with temp folders + unique topic per test run. Start with **one** happy-path test (per architect plan), expand only after it is stable. No fixed sleeps — poll with explicit timeout.
- Never mock Redis or Redpanda in integration tests — use the real containers via Aspire.

## References

- `plan_from_gpt.md` — architect's design document (source of truth for all architectural decisions)
- Memory at `/Users/JJar/.claude/projects/-Users-JJar-dev-UNIVERSAL-aspire-poc/` — persistent cross-session context (user profile, feedback rules, project state)

## Update this file when

- New resources are added to AppHost (Redis, Redpanda, reference-service, producer, frontend) → update "Target architecture" and "Folder layout"
- CI workflow is written → document how to run it locally and what it gates on
- Tests appear → add `dotnet test` usage and any per-project isolation notes
- Any architectural decision changes with architect approval → update the "Deviations" and "Target architecture" sections
