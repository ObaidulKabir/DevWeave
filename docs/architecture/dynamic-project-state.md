# Dynamic project state and multi-agent execution model

Status: Accepted foundation for implementation  
Applies to: DevWeave project knowledge, planning, execution, evidence, governance, and preservation

## 1. Purpose

DevWeave preserves an evolving software-project knowledge system. A requirement, implementation
claim, test result, verification decision, and deployment observation are different facts and can
change independently. Concurrent agents may work from different snapshots, so "latest write wins"
is not a valid coordination rule.

This document defines the terms and state transitions that implementation must preserve. It does
not require event sourcing, a graph database, microservices, or source-code storage.

## 2. State

At time `t`, project state is:

`S(t) = <G(t), B(t), W(t), X(t), E(t), Π(t)>`

- `G(t)`: the typed project graph, including Knowledge revisions, Activities, actors, Work,
  Evidence, artifacts, releases, deployments, and external references.
- `B(t)`: immutable approved plan baselines and their ancestry.
- `W(t)`: planning and execution work dependency graphs.
- `X(t)`: active agent executions, contexts, leases, fences, reports, and change references.
- `E(t)`: accepted, rejected, contested, or unreviewed evidence.
- `Π(t)`: authorization, classification, lifecycle, and governance policy effective at `t`.

An accepted event changes state deterministically:

`S(n+1) = δ(S(n), e(n))`

Current-state tables are efficient projections. The append-only project event journal, immutable
revisions, transition records, and baseline manifests provide the historical facts from which a
covered state can be reconstructed.

## 3. Canonical event envelope

Every preserved transition that can affect project interpretation carries:

- Event identity and versioned event type.
- Tenant and project.
- Human or agent actor.
- Declared purpose and reason.
- Valid time and platform-recorded time.
- Baseline identity when execution-related.
- Revision-pinned typed inputs and outputs.
- Correlation and idempotency keys.
- Authorization context or policy version.
- Monotonic sequence within the project event stream.

Retries with the same idempotency key resolve to the same committed fact. A duplicate delivery
does not create another semantic transition.

## 4. Requirement identity and change

A requirement has stable Knowledge identity and immutable content revisions. A prior revision is
never edited or reinterpreted as the new requirement.

Supported semantic changes are:

- Added.
- Clarified without intended scope change.
- Scope expanded or reduced.
- Quality threshold tightened or relaxed.
- Split into multiple requirements.
- Multiple requirements merged.
- Withdrawn.
- Replaced.
- Reinstated.

`RequirementChangeDescriptor` enforces unambiguous revision cardinality. Split consumes one revision
and produces at least two; merge consumes at least two and produces one; withdrawal consumes one
and produces none; ordinary changes consume one and produce one new revision.

Changes must include an actor, reason, valid time, and idempotency key. If reviewers disagree about
meaning, DevWeave records a semantic conflict rather than selecting the last submission.

## 5. Baselines

A baseline is an immutable, checksummed graph manifest, not merely a label or a list of current IDs.
It identifies:

- Exact approved requirement and other Knowledge revisions.
- Semantic relation versions used to interpret the plan.
- Specification bindings.
- Acceptance-criterion revisions.
- Work/dependency scope.
- Policy/profile version.
- Its predecessor baseline.

An execution package is pinned to one baseline and exact revision/specification scope. A package is
never silently retargeted to a newer baseline.

## 6. Change impact

For a changed requirement revision `r`, impact is the authorized, version-aware closure over:

- `satisfies`
- `implements`
- `validates`
- `depends_on`
- `derived_from`
- `supersedes`
- Plan Work links
- WorkPackage revision/work/specification scope
- releases and deployments

Each reachable resource receives one disposition:

- `Unaffected`: a recorded proof shows that the changed semantics cannot affect it.
- `ReviewRequired`: impact is possible or uncertain and a reviewer must decide.
- `Blocked`: work cannot safely progress.
- `Invalidated`: its claim or result no longer establishes the intended outcome.
- `Superseded`: a replacement is authoritative for new work.
- `Orphaned`: its justification no longer exists in the current plan.
- `ReverificationRequired`: implementation may remain, but existing verification is not enough for
  the changed revision.

Uncertainty never defaults to `Unaffected`. Unscoped work defaults to suspension.

## 7. Multi-agent execution

An agent may progress a package only when all conditions are true:

`CanExecute = Authorized ∧ CurrentOrRebasedBaseline ∧ DependenciesSatisfied`

`             ∧ LeaseValid ∧ FenceValid ∧ ContextCurrent`

`             ∧ NoUnresolvedSemanticConflict ∧ KillSwitchAllows`

Each condition produces an independent failure code. Possessing a valid credential, membership, or
lease does not override stale context or a stale baseline.

Planning, execution, verification, approval, and integration are distinct responsibilities:

- A Planning agent may propose requirements, decisions, plans, and governed status claims.
- An Execution agent may work only within an assigned package, lease, fence, and external workspace.
- Agent output and work reports are claims, not accepted project truth.
- Verification requires qualifying evidence tied to the current criterion revision.
- The reporter cannot verify or approve the same result.
- Integration rechecks baseline, package, lease/fence, policy, and unresolved impact.

## 8. Rebase decisions

Locking a new baseline suspends campaigns on older baselines first. Safety does not depend on impact
analysis succeeding.

After impact analysis, an authorized human can record one decision per affected package:

- Continue unchanged.
- Continue after refreshing context.
- Replan.
- Block.
- Cancel.
- Supersede with a replacement package.

Continuation creates a revision/replacement linked to the old package and new baseline. It does not
rewrite the old package's historical baseline.

## 9. Semantic conflicts

Repository merge conflicts and semantic conflicts are separate.

A semantic conflict preserves both competing revisions, their provenance, and the reason they are
incompatible. An authorized resolution can select either revision, combine them through a new
revision, reject both, or request revisions. Resolution is an Activity and never deletes either
proposal.

## 10. Context invalidation and notification

A context package is immutable and revision-pinned. When an input becomes affected:

1. Record the impact.
2. Mark dependent active context packages stale.
3. Suspend or block their packages.
4. Publish a durable coordination notification through the outbox.
5. Refuse lease renewal, report, change set, or integration while required preconditions fail.
6. Assemble a new context only after an approved rebase/replan decision.

Notification is a signal; the durable impact, package, and baseline state remains authoritative.

## 11. Required scenarios

Implementation is not accepted until automated tests cover:

1. Two agents execute dependent packages while a shared requirement changes. The new baseline
   suspends both first; both contexts become stale; the impact report can later distinguish their
   package dispositions, but neither may integrate before an authorized rebase decision.
2. An NFR threshold is tightened after implementation but before verification. Existing
   implementation is `ReviewRequired`, old evidence cannot verify the new threshold, and affected
   verification work becomes `ReverificationRequired`.
3. One requirement splits into two. Links to the old revision remain historical; each implementation,
   test, WorkItem, and package is reviewed against both new requirements and cannot be copied to both
   automatically.
4. Multiple requirements merge while separate packages are active. The old packages remain pinned to
   their baselines; a replacement package or explicit rebase records which combined scope remains.
5. Two planning agents propose incompatible architecture decisions. Both proposals and provenance
   remain, a semantic conflict blocks dependent execution, and only an authorized resolution creates
   the binding result.
6. An agent reports success from a stale context. The report is refused as a progress transition; an
   optional immutable rejected-attempt audit fact may be recorded without changing Work state.
7. An agent crashes after an external commit but before recording its report, then retries. The same
   idempotency/correlation key resolves to one execution fact and one change reference; no duplicate
   semantic output is created.
8. A package outside the impact closure requests continuation. It remains suspended until an
   authorized `ContinueUnchanged` rebase decision records the proof path and creates its new-baseline
   continuation identity.
9. Accepted evidence references a superseded requirement revision. It remains accepted historical
   evidence but cannot verify the current revision.
10. A duplicate event/message is replayed. The project event sequence and current projection remain
    unchanged, while delivery acknowledgement may advance independently.

## 12. Invariants

- Every resource belongs to a tenant and valid project or explicit higher scope.
- Every preserved semantic object has an identifiable type and interpretation.
- Important changes append facts; they do not silently erase prior state.
- Every execution input/output is baseline- and revision-pinned.
- Stale or ambiguous work cannot integrate.
- Reported, verified, approved, integrated, and deployed are independent claims.
- Evidence for an old revision cannot verify a new revision.
- A reporting actor cannot verify or approve the same claim.
- Both sides of a semantic conflict survive resolution.
- Authorization applies before traversal, aggregation, summarization, export, and notification.
- Source code, diffs, and secrets remain outside the preserved project model.

## 13. Existing runtime mapping

DevWeave already provides immutable Knowledge revisions, PlanBaselines, scoped WorkPackages,
campaign suspension, agent grants, leases/fences, independent Work states, and evidence-gated
Knowledge verification.

The implementation plan adds the missing cross-aggregate graph projection, project event journal,
graph-complete baseline, valid time, requirement-impact engine, per-package rebase decisions,
context invalidation, enriched agent provenance, authorized stakeholder projections, and portable
archive.
