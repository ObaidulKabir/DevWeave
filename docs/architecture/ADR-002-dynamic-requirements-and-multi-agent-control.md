# ADR-002: Dynamic requirements and multi-agent control

Status: Accepted  
Date: 2026-11-21

## Context

Requirements can change while several agents plan, implement, test, or integrate related work.
Agent credentials, leases, and fencing tokens prevent some unauthorized or concurrent actions, but
they do not establish that an agent's baseline and input context are still semantically current.

Suspending every campaign on a previous plan is safe but unnecessarily coarse. Continuing work based
only on an agent assertion is unsafe.

## Decision

1. Requirement changes are immutable, typed revision transitions with explicit valid and recorded
   times, actor, reason, and idempotency.
2. Every WorkPackage is pinned to an immutable baseline and explicit requirement/work/specification
   scope. Unscoped packages are never eligible for automatic continuation.
3. Locking a new baseline suspends older campaigns before impact analysis.
4. A version-aware impact engine computes and explains the affected closure through semantic
   Relations, Plan Work links, Work dependencies, package scope, specifications, Evidence, releases,
   and deployments.
5. Impact analysis produces a governed claim. Only an authorized rebase decision may allow a package
   to continue on a new baseline.
6. Continuation creates a replacement/revision linked to the prior package; it never rewrites
   historical baseline identity.
7. Execution eligibility is the conjunction of authorization, baseline/rebase currency, dependency
   satisfaction, lease validity, fencing validity, context currency, conflict resolution, and kill
   switch state.
8. Context packages are immutable. Affected input revisions make active contexts stale and block
   execution until refreshed after rebase/replan.
9. Semantic conflicts preserve every competing proposal and provenance. Resolution is explicit and
   auditable; last-writer-wins is prohibited.
10. Reporting, verification, approval, integration, and deployment remain independent facts.

## Consequences

- Some unaffected work can safely continue after plan changes without weakening the default stop.
- Impact rules and policy versions become preserved project facts.
- Agents need durable plan-change notifications, but notification delivery never replaces
  authoritative baseline/package/context checks.
- Integration must re-evaluate every eligibility condition even if earlier execution calls passed.
- Evidence and verification tied to superseded requirements become stale or require reverification;
  they are not deleted.
- Tests must cover concurrency, stale contexts, retries, partial impact, and conflicting proposals.

## Rejected alternatives

- Let agents self-declare unaffected work: rejected because it conflates a claim with governance.
- Retarget packages in place: rejected because it destroys which plan authorized historical work.
- Continue every package until a merge conflict occurs: rejected because semantic invalidity can
  exist without a repository conflict.
- Cancel all historical work permanently on every change: rejected because it is safe but prevents
  explainable, authorized continuation of unaffected work.
