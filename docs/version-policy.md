# DevWeave version policy

Status: engineering release policy, effective 2026-11-26.
Foundation MVP closed 2026-11-26.
Baseline: DWV-SRS-001 v1.5. The close ships against this proposed baseline.
Named product-owner and engineering-lead signatures are not recorded here.

This file decides **when** a capability is built. It does not change what an
SRS requirement means. Requirement IDs keep the priority and release tag in
the SRS. Where this file and the SRS disagree about meaning, the SRS wins.
Where they disagree about schedule, this file wins until it is revised here.

## Active version

**Foundation MVP** closed on 2026-11-26.

**R1-A** is recorded. A campaign is created from an approved baseline and
stores the pinned codebase commit. **R1-B** is recorded. Two remote runtimes
are authorized by accepted participation offers and assigned coherent packages
on one campaign baseline. Each assignment stores a context snapshot of that
package's revision scope, and a package dependency stays on that same baseline.
**R1-C** has not started. R1-D follows. Work beyond R1-C, commercial metering,
managed runners, extra MCP protocol versions, and agency demos stay on their
later versions. Code for them that is already in the tree stays. It is not a
reason to add the next case.

## How a version moves

1. One version is active. The next version starts after the active version's
   checklist is recorded in this file, with a date and the evidence named.
2. A pull request names the version it serves. While R1-C is the open
   increment, a change that adds R1-D or later, or an R2 behavior, is out of
   policy, even when a test would pass.
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
| Foundation MVP | Knowledge control plane: tenant, project, knowledge, revisions, schemas, relations, provenance, artifacts, agent context, project search, native export, Owner Studio, health and backup | Closed 2026-11-26 | Close checklist below, recorded 2026-11-26 |
| R1 commercial pilot | Sellable coordination on customer-managed runtimes: plan lock, one code-host reference adapter, work packages, leases, project MCP messaging, integration queue, basic plan and usage visibility, two client projects | R1-A and R1-B recorded 2026-11-26. R1-C has not started | SRS §16.10 recorded on the chosen identity, backlog B1–B5 each recorded, billing still excluded |
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

**R1, after R1-B**

- A recorded DevWeave-managed runtime on the same campaign.
- Leases, heartbeat, fencing, workspace isolation, and a production network latency measurement for the kill switch.
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

MVP closed on 2026-11-26. Each line has a date and a named record. Item 2 is
a waiver. The live host was checked the same day and still serves pilot
sign-in, so the waiver is not a passed identity-provider smoke.

| # | Record | Current fact | What closes it |
| --- | --- | --- | --- |
| 1 | Evidence register | Recorded 2026-11-26: 101 pass, 1 waived, 0 partial, 0 fail | Leave the counts. A new Must row is a scope change, not a close task |
| 2 | Production human identity | Recorded 2026-11-26 as a waiver. `https://dev-weave.prospectbdsoftware.com/auth/login-options` returned `pilotEnabled: true` and `oidcAuthority: null`. `POST /auth/pilot/bootstrap` returned 400, so the pilot route is still mapped. `GET /health/ready` returned 200. Approved by Product. Expires 2026-12-11. An authority in this tree still disables pilot minting, including a 404 on bootstrap and token, and Studio hides the GUID form | This waiver. A later production smoke does not reopen MVP. After 2026-12-11 the live host is an operations defect until `docs/ops/production-oidc-cutover.md` is recorded |
| 3 | Restore claim | Recorded. Local compose drill on 2026-11-20: RPO within 24 hours, RTO within 45 minutes. WAL archiving is off | That drill is the MVP claim. The SRS §12.3 planning target of RPO within 1 hour stays a later operations drill. Do not enable WAL to chase it during this close |
| 4 | Owner Studio | Recorded 2026-11-26. Signed-in Development pass: tenant “MVP Close Studio”, project “MVP Close” (`mvp-close`), knowledge `K-01` revision 1 (`requirements.functional`), plan import `mvp-close-plan-1` approved and materialized as `GOAL-01` (Approved) and work `W-CLOSE` (Planned, reported NotStarted, Unverified, Pending). Usage was not part of this gate | One signed-in pass of project, knowledge, and plan import |
| 5 | Baseline signature | Recorded 2026-11-26. MVP ships against the proposed DWV-SRS-001 v1.5 baseline. Named product-owner and engineering-lead signatures are not in this file | This record is the ship-against-proposed-baseline decision. A later signature does not reopen MVP |

## After the close

Closed 2026-11-26. R1-A is recorded: a campaign stores the approved baseline
and the pinned codebase commit (`R1A_a_campaign_stores_the_approved_baseline_and_the_pinned_commit`).
R1-B is recorded: two remote runtimes are assigned coherent packages on one
campaign baseline after accepted offers, with a context snapshot of each
package's revision scope (`R1B_two_runtimes_are_assigned_coherent_packages_on_one_baseline`).
R1-C has not started. R1-D follows. Billing automation, managed runners, and
JSON-LD stay on their later versions. The live-host identity waiver expires
2026-12-11 and does not count as the R1 production-OIDC run.
