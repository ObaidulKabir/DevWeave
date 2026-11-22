# ADR-001: Cross-aggregate project graph and event journal

Status: Accepted  
Date: 2026-11-21

## Context

The mathematical project graph includes Knowledge, Work, Activities, agents, Evidence, artifacts,
baselines, implementation references, releases, and deployments. DevWeave currently has strong
canonical aggregates for these concepts, but `Relationship` connects only Knowledge items and
historical coverage varies by aggregate.

A single generic entity/edge table would duplicate domain state and weaken foreign-key, lifecycle,
and authorization rules. Converting the platform to full event sourcing would require a risky
rewrite and is not necessary to preserve deterministic transition history.

## Decision

1. Existing domain aggregates remain canonical write models.
2. Add a read-only `ProjectGraphProjection` that emits typed resource references and edges from
   canonical repositories:
   - Knowledge `Relationship` edges.
   - Activity input/output/actor provenance.
   - Evidence origin and criterion revision.
   - Plan Work links and Work dependencies.
   - Baseline/package/change-set/codebase references.
   - Approval, release, and deployment references.
3. Add an append-only, per-project sequenced `ProjectEvent` journal. Mutations append a versioned,
   allowlisted event in the same database transaction as the current-state change.
4. Current-state tables remain the efficient projections. The journal is not the only data source
   and does not replace aggregate repositories.
5. The transactional outbox publishes committed events to external consumers. Journal persistence
   and message delivery remain separate states.
6. PlanBaseline v2 stores an immutable checksummed graph manifest and predecessor reference.
7. PostgreSQL remains authoritative. Graph-database adoption requires measured traversal/load
   evidence and must not change canonical identities or semantics.

## Consequences

- Project-wide traversal can cross aggregate boundaries without copying payloads.
- Authorization can filter vertices and edges before a stakeholder projection is assembled.
- Historical reconstruction gains a common ordered event source while preserving aggregate-specific
  immutable revisions and transition records.
- Every new mutation must define its event schema and idempotency behavior.
- Legacy history may have explicitly reduced temporal precision; migrations must not invent facts.
- Projection adapters require consistency tests against their canonical repositories.

## Rejected alternatives

- Store every concept as a Knowledge item: rejected because Work, Evidence, Activities, leases, and
  integration state have different invariants and transaction behavior.
- Add one polymorphic relationship table for every resource type: rejected because it creates weak
  references and a second source of truth.
- Rewrite as pure event sourcing: rejected because it is unnecessary for the required model and
  would destabilize existing accepted domain behavior.
- Introduce a graph database immediately: rejected until relational traversal is measured and shown
  insufficient.
