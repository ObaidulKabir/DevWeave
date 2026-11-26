# DevWeave version policy

Status: engineering release policy, effective 2026-11-26.
Baseline: DWV-SRS-001 v1.5, still a proposed baseline. Product-owner and
engineering-lead approval of the SRS remain pending.

This file decides **when** a capability is built. It does not change what an
SRS requirement means. Requirement IDs keep the priority and release tag in
the SRS. Where this file and the SRS disagree about meaning, the SRS wins.
Where they disagree about schedule, this file wins until it is revised here.

## Active version

**Foundation MVP** is the only open version.

Close target: **2026-12-11**.

Until that date, or until this file records a later close, new work is limited
to the close checklist below and to defects in behavior that checklist depends
on. Coordination campaigns, commercial metering, managed runners, extra MCP
protocol versions, and agency demos are later versions. Code for them that is
already in the tree stays. It is not a reason to add the next case.

## How a version moves

1. One version is active. The next version starts after the active version's
   checklist is recorded in this file, with a date and the evidence named.
2. A pull request names the version it serves. A change that adds an R1 or R2
   behavior while MVP is active is out of policy, even when a test would pass.
3. An SRS `Must` tagged R1 or later is not pulled into MVP to make the product
   feel complete. It waits for its version.
4. A waiver needs an owner, a reason, and an expiry, in
   `docs/mvp-evidence-register.md`. EXP-004 stays waived until 2027-06-01.
5. Server version stays `1.4.0` for this MVP. MCP `initialize` accepts protocol
   version `2026-07-28` only. A new protocol version is a new version decision,
   not a drive-by change.

## Version map

| Version | What it is | Starts | Closes when |
| --- | --- | --- | --- |
| Foundation MVP | Knowledge control plane: tenant, project, knowledge, revisions, schemas, relations, provenance, artifacts, agent context, project search, native export, Owner Studio, health and backup | Now | Close checklist below, target 2026-12-11 |
| R1 commercial pilot | Sellable coordination on customer-managed runtimes: plan lock, one code-host reference adapter, work packages, leases, project MCP messaging, integration queue, basic plan and usage visibility, two client projects | After MVP is recorded closed | SRS §16.10 recorded on the chosen identity, backlog B1–B5 each recorded, billing still excluded |
| R2 hybrid growth | Optional managed runners, billing automation, client portal, more than one code host, MCP operational diagnostics | After R1 is recorded closed | Managed compute stays optional and separately metered |
| R3 / FUT | Private cloud, federation, residency, JSON-LD interchange, partner attribution | After R2, or on the dated waiver | EXP-004 no earlier than 2027-06-01 |

SRS §13.1 is the source of that split. R1 increments stay in the order already
in SRS §16.11: R1-A plan lock, R1-B runtimes and packages, R1-C leases, R1-D
integration queue. One increment at a time.

## MVP boundary

In this close:

- A tenant owns projects. A member who is not on a project does not see it.
- Knowledge items, immutable revisions, published schemas, and typed relations.
- Provenance and activities. Evidence review is separate from the claim.
- Artifacts stored with checksums. Source code and secrets stay out of the
  control plane.
- An agent principal can assemble context and propose a plan. The Owner
  approves it. Reported completion is separate from human verification.
- Project-scoped full-text search.
- Native export and import of knowledge and permitted artifact checksums.
- Owner Studio for project, knowledge, relations, and work.
- Health endpoints and the backup runbook.

The evidence register already marks 101 of 102 MVP rows pass and waives
EXP-004. That register is the MVP list. Do not add rows to it for coordination
or commercial behavior.

## Parked until a later version

These are real product goals. They are not part of closing MVP. Several
already have code and tests; that code stays, and the next slice waits.

**R1, after 2026-12-11**

- Two runtimes on one campaign, including a recorded DevWeave-managed location.
- Leases, kill switch, and a production network latency measurement.
- Integration stages Reviewed, Tested, Merged, and Verified. DevWeave still
  does not merge git.
- Plan catalogue, usage by project and execution mode, capacity rules, and
  stored entitlement decisions.
- Training opt-in. The default remains excluded.
- Client viewer separation for agent channels.
- A live production-OIDC two-runtime run and a filmed two-client walkthrough.

**R2**

- Provisioning a managed runner. `IManagedRuntimeProvider` stays unimplemented.
- Billing automation and subscription cancellation.
- A client portal.
- MCP-036 operational health diagnostics.
- A second code-host adapter, and a GitHub adapter that fetches blobs or diffs.

**R3 / FUT**

- JSON-LD export (EXP-004), through 2027-06-01.
- Private cloud, federation, residency, and partner or reseller attribution.

## Close checklist

MVP is closed only when each line has a date and a named record. Items 1, 3, and 4
are recorded. Items 2 and 5 are still open, so the version stays open and R1
stays parked.

| # | Record | Current fact | What closes it |
| --- | --- | --- | --- |
| 1 | Evidence register | Recorded 2026-11-26: 101 pass, 1 waived, 0 partial, 0 fail | Leave the counts. A new Must row is a scope change, not a close task |
| 2 | Production human identity | Open. Production requires a real OIDC authority. Development with an empty authority still mints pilot JWTs. The register cites the cutover runbook | A recorded production smoke from `docs/ops/production-oidc-cutover.md`, or a waiver with owner and expiry |
| 3 | Restore claim | Recorded. Local compose drill on 2026-11-20: RPO within 24 hours, RTO within 45 minutes. WAL archiving is off | That drill is the MVP claim. The SRS §12.3 planning target of RPO within 1 hour stays a later operations drill. Do not enable WAL to chase it during this close |
| 4 | Owner Studio | Recorded 2026-11-26. Signed-in Development pass: tenant “MVP Close Studio”, project “MVP Close” (`mvp-close`), knowledge `K-01` revision 1 (`requirements.functional`), plan import `mvp-close-plan-1` approved and materialized as `GOAL-01` (Approved) and work `W-CLOSE` (Planned, reported NotStarted, Unverified, Pending). Usage was not part of this gate | One signed-in pass of project, knowledge, and plan import |
| 5 | Baseline signature | Open. SRS approval is pending | Product owner and engineering lead sign DWV-SRS-001 v1.5, or this file records that MVP ships against the proposed baseline |

If item 2 or item 5 is still open on 2026-12-11, MVP stays the active version.
The date moves only by editing this file. R1 does not start to fill the gap.

## After the close

Record the close date at the top of this file, then open R1-A only: a campaign
from an approved plan and a pinned codebase commit. R1-B, R1-C, and R1-D follow
in that order. Billing automation, managed runners, and JSON-LD stay on their
later versions.
