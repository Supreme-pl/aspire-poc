# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repo status

Phase 2 complete: AppHost graph wires up Redis (`cache`) and Redpanda (`redpanda`) alongside app1 and app2. App1 is referenced to `cache` and receives both the Redis connection string and Kafka bootstrap env var; app2 receives the Kafka bootstrap env var only. Apps currently just log their wiring on startup — no actual Redis client or Kafka producer/consumer in use yet. `AspirePoc.slnx` builds clean. No business logic, no frontend, no tests yet.

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

Deviations from `plan_from_gpt.md` agreed with the user so far:
- **Monorepo layout** instead of three separate repos (this repo is the single location).
- **Mock producer service** replaces the literal "file drop" Extract step — treated as an interpretation of the architect's `/process` endpoint trigger, pending architect confirmation.
- **Mock reference service** added as the source of truth for reference data (matches the architect's "stubbed dependency" note).

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

Current (Phase 1):
```
aspire-poc/
├── AspirePoc.slnx
├── CLAUDE.md
├── plan_from_gpt.md
├── apphost/src/           AspirePoc.AppHost (Aspire.AppHost.Sdk)
├── service-defaults/src/  AspirePoc.ServiceDefaults (shared OTel, health, service discovery)
├── app1/src/              AspirePoc.App1 (ASP.NET Core empty)
└── app2/src/              AspirePoc.App2 (ASP.NET Core empty)
```

Planned additions (later phases):
- `reference-service/src/` — mock source of truth for reference data
- `producer/src/` — mock producer pushing synthetic batches to app1 on interval
- `frontend/src/` — Angular + TypeScript
- `integration-tests/src/` + `integration-tests/fixtures/`
- `app1/tests/`, `app2/tests/`, `reference-service/tests/` — per-project unit tests

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
