# Plan an application with DevWeave

Use this guide when a person or agent needs to turn an application idea into a reviewable delivery plan. DevWeave separates **Knowledge** (what is known and agreed), **Relations** (how knowledge items affect each other), and **Work** (what will be delivered). All three belong to a tenant and project. A proposed plan is not an approved plan, and reported work is not verified work.

## The three elements

| Element | What it means | Create it for | Do not use it for |
| --- | --- | --- | --- |
| Knowledge | A typed project information item with a stable ID, schema version, classification, independent lifecycle/review/verification states, and immutable content revisions. | Goals, requirements, constraints, decisions, risks, and acceptance criteria when their types and schemas are registered. | Source-code storage, ephemeral chat, or a substitute for a delivery task. |
| Relation | A typed, directed connection **between two knowledge items in the same project**. Its code must be registered and active; endpoint types and cardinality must be valid. | Dependencies, justification, or impact links when a suitable relation type exists. | A link to a work item, an arbitrary unregistered label, or a cross-project shortcut. |
| Work | A project milestone or work item with an owner, priority, due date, optional milestone and work-item dependencies. Planned, reported, verified, and approved states are separate. | Specific deliverables that can be assigned, executed, tested, and reviewed. | A canonical requirement or evidence that something is approved. |

There is a fourth, distinct execution concept: a **WorkPackage** belongs to a coordination campaign and baseline. It is not the same entity as a planning **WorkItem**. There is no automatic WorkItem-to-WorkPackage conversion; a WorkItem-to-Knowledge reference is a separately created, revision-pinned Plan Work link.

## From an idea to an outline

The **Plan outline** is structural decomposition, not a semantic Relation. Start with one Knowledge item describing the application outcome; add its schema-valid first revision, then attach it as the project's root. Attach revised modules, requirements, decisions, and acceptance criteria below a parent item. Each Knowledge item can be attached once and every child has exactly one parent. Non-root items can be **moved** (reparented; the subtree moves with them) or **detached** when they have no children. The root cannot be moved or detached. A project has at most one root; unplaced Knowledge items remain valid and can be attached later. New Knowledge revisions do not silently change the outline or the Work links. Moves and detaches are audited and do not rewrite locked PlanBaselines.

**Plan Work links** connect a WorkItem to the exact immutable, *current and human-reviewed* Knowledge revision used to justify it. A new Knowledge revision makes an older link historically accurate but potentially stale: review the change and explicitly pin a new link. Do not treat an outline edge as evidence that a semantic relation or delivery dependency exists.

An agent with an active project membership, credential, and approved `ReadKnowledge` grant may read only outline entries within its classification ceiling; an approved `WriteKnowledge` grant allows it to submit an expansion proposal under a reviewed parent. Proposal text is content-inspected. Proposals do not create Knowledge, edges, or Work. An authorized human reviewer other than the proposer creates a matching item through the normal published-schema/revision process, approves its revision, then accepts the proposal; an owner/admin may also reject it. Agents cannot attach nodes, link Work, or decide proposals.

## Planning procedure

1. **Set scope.** Choose the tenant and project, identify the requester and intended users, and write a one-sentence outcome. Do not borrow objects from another project.
2. **Discover types.** Check published knowledge schemas and active relation types. Use their real IDs, versions, permitted endpoints, and payload shapes. If a needed type is absent, request its governed registration; do not invent a type code and write as though it exists.
3. **Capture knowledge first.** Create separate items for the objective, each testable requirement, important constraint, design decision, risk, and acceptance criterion. Give each a stable key and clear title. Add a schema-valid revision with rationale/source attribution. Avoid mixing unrelated claims in one item. An agent's proposed content remains proposed until human review.
4. **Connect knowledge.** Define each relation as `source item ID -> active relation type -> target item ID`, with a plain-language explanation of why the link matters. Check direction and endpoint types. Flag missing, contradictory, or unsupported claims before work is scheduled.
5. **Derive work.** Group deliverables into milestones. For each work item write its outcome, owner/role, priority, dependencies on other work items, and completion checks. After approving the supporting Knowledge revision, create a revision-pinned Plan Work link. Text mentions in descriptions remain useful context, not governed links.
6. **Review and lock.** A human checks scope, dependencies, security/privacy constraints, acceptance evidence, and the precise revisions being relied on. Approval is a decision on one specific revision: `POST /tenants/{tenantId}/knowledge/{itemId}/approve` (or `/reject`) takes `{ "revisionId": "...", "comment": "..." }`. If the item has a newer revision than the one named, the server answers 409 `stale-review` and nothing changes, so a reviewer can never approve text they did not read. A rejection requires a reason. Every decision records the reviewer, time, revision and comment in the audit trail; read them with `GET /tenants/{tenantId}/knowledge/{itemId}/reviews`, and read every saved version with `GET /tenants/{tenantId}/knowledge/{itemId}/revisions`. When the coordination workflow is appropriate, lock a PlanBaseline. The server builds it from the **current revision of every outline item** and refuses (409) while the outline is empty or any outline item has no revision or an unapproved current revision. `GET .../coordination/baselines/preview` shows exactly what would be locked and what blocks it. If the lock request names revisions, they must match that set exactly; otherwise the plan changed since it was reviewed and the lock is refused. Locking is limited to human project Owners/Admins and is audited. A new revision does not silently update a locked baseline; reassess and create a new approved baseline when the plan changes.
7. **Execute and verify separately.** Work can be planned, reported complete, verified, and approved at different times. Attach qualifying evidence and have an authorized reviewer verify and approve. A message, agent self-report, or successful tool call alone is not acceptance.

### Example: TaskBoard Lite

Goal (v1): "A signed-in user can create and list their own tasks."  
Goal (v2): same outcome plus "unauthenticated requests are rejected."

| Element | Illustrative plan entry |
| --- | --- |
| Knowledge K-01 | Goal (outline **root**): users manage personal tasks. Item **description** states the application outcome. |
| Knowledge K-API | Module under K-01: Personal tasks API (create/list; no UI/sharing). |
| Knowledge K-02 | Requirement under K-API: create a task with a title; reject empty title. |
| Knowledge K-03 | Requirement under K-API: list only the authenticated user's tasks (r2 also ties isolation to the auth gate). |
| Knowledge K-04 | Decision under K-API: persist tasks in PostgreSQL with owner identity. |
| Knowledge K-05 | Constraint: no source/secrets in DevWeave; store refs only. **Moved** under K-API in v2 (was under K-01 in v1). |
| Knowledge K-AUTH | Module under K-01 (v2): Authentication gate before create/list. |
| Knowledge K-06 | Requirement under K-AUTH: reject unauthenticated create/list with 401. |
| Knowledge K-07 | Decision under K-AUTH: validate JWT bearer tokens at the API edge. |
| Knowledge K-DRAFT | Deferred shared-lists idea: attached then **detached** — remains Knowledge, not on the outline. |
| Outline (v2) | `K-01 → K-API → {K-02, K-03, K-04, K-05}` and `K-01 → K-AUTH → {K-06, K-07}`. Structural parent/child only. |
| Relation | K-04 `supports` K-03; K-07 `supports` K-06 (**semantic** edges). Not implied by the outline. |
| Milestone M-01 | TaskBoard MVP: create, list, and isolate tasks. |
| Work W-01 | Implement create-task API; **Plan Work link** pins K-02's reviewed current revision. |
| Work W-02 | Implement user-scoped list API; depends on W-01; links pin K-03 and K-04 (r1 link on K-03 stays historically accurate after r2). |
| Work W-03 | Enforce JWT auth on create/list; depends on W-01/W-02; links pin K-06 and K-07. |
| PlanBaseline | Bootstrap locks **v1**, evolves the living plan (move/detach/add/revise), then locks **v2** (OD-07 suspends the prior campaign) and starts a new campaign on v2. |

The table is a **planning example**. The pilot bootstrap (`scripts/pilot/bootstrap.ps1`) materializes this shape after registering/publishing the schemas and the `supports` relation type.

### Example: Online Cake Shop

Second pilot project for capability testing. Goal (v1): "A signed-in customer can place a cake order and list only their own orders." Goal (v2): same plus unauthenticated rejection.

| Element | Illustrative plan entry |
| --- | --- |
| Knowledge K-01 | Goal root: customers manage cake orders. |
| Knowledge K-ORD | Module under K-01: Orders API (place/list; no storefront/payments). |
| Knowledge K-02 | Requirement under K-ORD: place order with cake type + quantity ≥ 1. |
| Knowledge K-03 | Requirement under K-ORD: list only authenticated customer's orders (r2 ties to auth). |
| Knowledge K-04 | Decision under K-ORD: persist orders in PostgreSQL with customer identity. |
| Knowledge K-05 | Constraint: no source/secrets in DevWeave; **moved** under K-ORD in v2. |
| Knowledge K-AUTH / K-06 / K-07 | Auth gate module, 401 requirement, JWT decision (v2). |
| Knowledge K-DRAFT | Shared wedding-registry idea: attached then **detached**. |
| Outline (v2) | `K-01 → K-ORD → {K-02, K-03, K-04, K-05}` and `K-01 → K-AUTH → {K-06, K-07}`. |
| Relation | K-04 `supports` K-03; K-07 `supports` K-06. |
| Work | W-01 place-order → K-02; W-02 list-orders → K-03/K-04; W-03 JWT → K-06/K-07. |
| PlanBaseline | Bootstrap locks v1 then v2; packages declare `implementsRevisionIds` + `workItemIds`. |

Materialized by `scripts/pilot/bootstrap-online-cake-shop.ps1` against sandbox `online-cake-shop`. See `docs/pilot/online-cake-shop-runbook.md`.

## For Owner Studio users

Use a live tenant with a human bearer token. Preview mode is not a persisted planning workspace.

The Overview page presents three linked cards in planning order: **Knowledge** ("what and why"), **Relations** ("how do facts connect"), and **Work** ("what must be delivered"). **View whole plan diagram** opens the read-only **Plan map** for the selected project. Each destination repeats its definition and an illustrative example. Read the badges as different decisions, not as steps that happen automatically.

### Read the Plan map

- **Left column: Knowledge.** Each node shows a stable key, title and abbreviated state. Selecting it shows its type, current revision number, review and verification.
- **Middle column: Relations.** A solid teal path runs from the source knowledge node through a registered relation type to the target knowledge node. Revoked relations are hidden by default; the checkbox reveals them for historical inspection. A missing path is not an implied relationship.
- **Right column: Milestones and Work.** Work items appear inside their milestone band; items with no current matching milestone appear under "No milestone". Solid amber paths run from a prerequisite work item to the dependent work item when the API returns `DependsOn`.
- **Across the columns: Planning references.** Solid blue edges are outline parent-child links; violet edges are revision-pinned Work links (dashed violet when the Knowledge item has a newer revision). A dashed gray path only means a description mentions a Knowledge **key** without a governed link.
- **Scope and status.** The map displays the current project's fetched records, not a signed-off PlanBaseline. It is read-only, scrolls sideways on small screens, and can show partial data if an API query fails. Select a node to see its available details and open its dedicated page.

1. Select the intended project. In **Knowledge**, inspect existing items and the current revision. **Create a knowledge item** asks for a stable project key, readable title, published type ID, and schema version. It creates the identity, not the content. **Add a content revision** asks for schema-valid content and a change reason. A new revision does not inherit prior review or verification; inspect these badges separately and use approval only after review. Confirm the schema ID/version before entering content.
2. In **Relations**, read each row from **Source** through its registered type code to **Target**. Check whether the proposed plan captures important dependencies. The current Relations screen is an **explorer only**: it does not create or revoke edges. An authorized client can use the project relationship REST routes. Relation types are listed under `/relation-types`; type registration is platform-admin governed.
3. In **Plan outline**, attach the first revised idea as root, then revised items under their parents. Submit or review expansion proposals here; acceptance requires a different owner/admin and a matching Knowledge item with a human-reviewed revision. In **Work**, create milestones and deliverables, then return to Plan outline to pin reviewed Knowledge revisions to Work. The Work board still shows independent planned/reported/verified/approved badges; "Report complete" does not verify or approve.
4. On the Knowledge page, the **Review this revision** panel approves or rejects the current revision with a comment, and lists every revision and earlier decision. On the Coordination page, the lock form lists the approved plan items it will lock and any blockers. Before starting execution, verify the proposed Knowledge-to-Work mapping with a human reviewer. The **Coordination** page shows coordination state, but does not turn a list of WorkItems into an approved PlanBaseline automatically.

Relevant REST routes (substitute actual IDs):

```text
GET  /schemas
GET  /relation-types
POST /tenants/{tenantId}/projects/{projectId}/knowledge
POST /tenants/{tenantId}/knowledge/{itemId}/revisions
POST /tenants/{tenantId}/projects/{projectId}/relationships
POST /tenants/{tenantId}/projects/{projectId}/milestones
POST /tenants/{tenantId}/projects/{projectId}/work-items
GET  /tenants/{tenantId}/projects/{projectId}/plan/
POST /tenants/{tenantId}/projects/{projectId}/plan/outline
POST /tenants/{tenantId}/projects/{projectId}/plan/work-links
POST /tenants/{tenantId}/projects/{projectId}/plan/proposals
POST /tenants/{tenantId}/projects/{projectId}/plan/proposals/{proposalId}/decide
POST /tenants/{tenantId}/projects/{projectId}/coordination/baselines
```

These routes have different authorization policies. Use the authenticated principal's authority; do not put credentials in knowledge revisions or coordination messages.

## For MCP-connected agents

The project MCP endpoint is `/tenants/{tenantId}/projects/{projectId}/mcp/rpc` and requires an authenticated **agent** identity. Use `read_plan_outline` with a `ReadKnowledge` grant to inspect the permitted hierarchy and pinned Work links. Use `propose_plan_expansion` with a `WriteKnowledge` grant to submit `{parentItemId, key, title, typeId, rationale}` for human review. These tools do **not** create/revise/approve Knowledge, Relations, Milestones, or WorkItems. Request a human/API handoff for those writes and retain the returned IDs.

1. Prepare a structured proposal using the template below. Identify each proposed item and relation separately from persisted IDs. Ask for missing requirements rather than filling them with invented facts.
2. Hand Knowledge, Relation, and planning Work creation/review to an authorized human path. Read back resulting IDs, revision numbers, and approval states before treating the plan as authoritative.
3. After the human-controlled baseline is established, use `get_plan_baseline` to inspect its recorded snapshot. The tool named `list_ready_work` currently **creates a WorkPackage** under a campaign; it is not a read-only list of planned WorkItems. Use it only with explicit authorization and verified campaign/baseline IDs.
4. For execution, register the runtime, use `request_assignment` for a fenced lease, `heartbeat_lease` to maintain it, and durable messages/cursors to coordinate. Scope every call to the project; do not send source code, patches, secrets, or private raw payloads through the knowledge or coordination plane.

**Current enforcement caveat:** the baseline API currently persists supplied revision and dependency IDs without checking each revision's approval/project ownership in `LockAsync`. Treat those checks as a mandatory human/application precondition, not as a guarantee that the endpoint already enforces them.

## Reusable plan output

An agent or user preparing a plan should produce this reviewable artifact before creating delivery work:

```text
Project: <tenant ID / project ID>
Outcome and users: <one-sentence outcome, personas>
Scope: <included / excluded / open questions>
Knowledge:
  - <key, registered type ID + schema version, title, proposed content,
     source, classification, revision ID/number if persisted, review state>
Relations:
  - <source knowledge ID, registered active type code, target knowledge ID,
     direction, rationale, persisted ID or "proposed">
Milestones:
  - <key, outcome, target date, success criteria>
Work items:
  - <key, milestone, deliverable, owner/role, priority, due date,
     depends-on work item IDs, supporting knowledge revision IDs,
     acceptance tests/evidence, persisted ID or "proposed">
Risks and unresolved decisions: <owner, next action>
Baseline: <not requested / pending human review / approved revision IDs and
           baseline ID; never imply approval from creation alone>
```

**Ready-to-execute check:** every deliverable has an accountable owner, explicit acceptance evidence, and a reviewed knowledge basis; all references resolve within the project; dependencies are coherent; authorization is confirmed; and the current approved revisions match the intended baseline. Otherwise the output is a draft for review, not an instruction to launch a campaign.

## When the plan changes during implementation

Every execution step checks that the work is still part of the current plan:

- A **work package declares what it implements**: approved knowledge revisions from its own plan version
  (`implementsRevisionIds`), planning work items (`workItemIds`) and specification topics (`specTopics`).
  Revisions from any other plan version are refused. Packages without a declared scope are allowed but
  reported as `unscoped`, because a plan change cannot be traced to them.
- **Locking a new plan version suspends** every campaign that was started, or only proposed, on an older version.
- While a campaign is suspended, completed, failed or stopped by a kill switch, or its package is closed,
  DevWeave **refuses** new leases, lease renewals and change sets. Work already in the integration queue is
  **rejected at decision time** with the reason recorded, never merged. Releasing a lease is always allowed.
- Kill switches on a runtime or an agent stop that runtime or agent from taking new leases.

Next steps, not built yet: an impact report at lock time that lets unaffected work continue on the new
version, agent notification, and re-verification of finished work whose plan items changed.

## Implementation references

- Knowledge identity and revisions: `src/DevWeave.Domain/Knowledge/KnowledgeItem.cs`, `KnowledgeRevision.cs`
- Relation validation: `src/DevWeave.Application/Relations/CreateRelationship.cs`
- Planning work and independent states: `src/DevWeave.Domain/Lifecycle/WorkItem.cs`
- Baseline and execution work: `src/DevWeave.Domain/Coordination/PlanBaseline.cs`, `WorkPackage.cs`
- MCP tool surface: `src/DevWeave.Mcp/Protocol/McpServer.cs`
- Existing pilot walkthrough: `docs/pilot/taskboard-lite-runbook.md`
