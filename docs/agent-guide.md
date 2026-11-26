# DevWeave guide for AI agents (MCP)

DevWeave is the control plane for one codebase worked by many desktop agents at once. Agents may be
Cursor, Claude, or similar, on different devices and in different places. Git stays the source of
truth. You propose, lease, and report through MCP. Humans supervise and approve in Studio or the API.
Pick a case in section 4 before you call a tool.

## 1. Rules that always apply

1. **DevWeave decides, you propose.** Nothing you propose (a decision, a catalog change, a plan expansion)
   binds until a person approves it. Never act on your own proposal as if it were approved.
2. **Follow the binding specification.** Before choosing a technology, convention or pattern, resolve it
   (`resolve_spec`, `get_effective_spec`). If a topic is undecided, propose a decision; do not improvise.
3. **Work only on the current plan.** Every piece of work belongs to a plan version. When a newer version is
   locked, work on the old one is stopped. Check before you start, and stop when told.
4. **Declare what you implement.** When you create a work package, list the approved knowledge revisions,
   work items and specification topics it covers, so a plan change can be traced to your work.
5. **Never send source code to DevWeave.** Change sets carry repository references only (provider ref,
   commit SHA, changed paths). Source, diffs and secrets are refused.
6. **Release what you hold.** Releasing a lease is always allowed. Do it when you finish, fail, or are stopped.

## 2. Connecting

| Item | Value |
| --- | --- |
| Endpoint | `POST /tenants/{tenantId}/projects/{projectId}/mcp/rpc` |
| Protocol | JSON-RPC 2.0, MCP protocol version `2026-07-28` |
| Authentication | `Authorization: Bearer <agent token>` (an AI-agent principal with an active credential) |
| Runtime identity | `X-Runtime-Id: <runtime id>` on every call after `register_runtime` |
| Methods | `initialize`, `tools/list`, `tools/call`, `resources/list`, `prompts/list` |
| Arguments | Always a JSON **object** (or omitted). Arrays and other shapes are refused with `-32602`. Check your client, for example PowerShell must send a hashtable, not `$args` |

DevWeave does not publish MCP resources or prompts yet. `resources/list` and `prompts/list` return compliant
empty collections with explicit cache metadata; clients should not patch or invent entries.

`tools/call` returns a JSON-RPC **object** (never a top-level JSON array). The `result` is an MCP
CallToolResult: `content` (text JSON), `structuredContent` (the same payload as an object), and
`isError`. List collections are also copied onto `result` itself (`result.channels`, `result.messages`)
so JSON-RPC clients that do not unwrap `structuredContent` can still read them. List tools wrap
collections:

| Tool | `structuredContent` shape |
| --- | --- |
| `list_channels` | `{ "channels": [ { "id", "type", "channelType", ... } ] }` |
| `list_messages` / `read_messages_after_cursor` | `{ "messages": [ ... ], "nextCursor" }` |
| `list_schemas` | `{ "schemas": [ { typeId, schemaVersion, isCanonical, storageKind } ], "knowledgeTypes": [ ... ] }` — prefer canonical ids such as `requirements.functional` / `requirements.nonfunctional`; legacy `plan.*` remains valid |
| `list_relation_types` | `{ "relationTypes": [ ... ] }` |
| `list_codebase_targets` | `{ "codebaseTargets": [ { "id", "provider", "repositoryId", "targetBranch", "baseCommitSha", "canonicalRef", "browseUrl" } ] }` |
| `list_work_items` | `{ "workItems": [ { "id", "key", "plannedState", "reportedState", "verificationState", "approvalState", "version", ... } ] }` |
| `list_work_packages` | `{ "workPackages": [ ... ] }` |
| `list_spec_topics` / `next_open_topics` | `{ "topics": [ ... ] }` |

A JSON-RPC success omits `error`; a failure omits `result`. Coordination messages (Knowledge / Relations /
Work payloads on a channel) are durable logs. They do **not** publish Knowledge, Relations, or Work items —
use `propose_initial_plan` and Owner Plan import for that.

**Access you need** (granted by a project Owner in the dashboard):

- Active membership in the project.
- Use **two separate agents** (separate credentials):
  - **Planning**: `ReadKnowledge` + `WriteKnowledge` (optional `TraverseRelations`) for outline/spec/`propose_initial_plan` and Work item status (`list_work_items`, `report_work_item`, `update_work_item_plan_state`).
  - **Execution**: `ReadKnowledge` + `AssembleContext` + `ExecuteWorkflow` for runtimes, leases, and change sets.
- Tools are capability-gated: a planning key cannot take leases; an execution key cannot submit plan imports.
- Your grant's classification ceiling limits which knowledge you can see.

Example call:

```json
{ "jsonrpc": "2.0", "id": 7, "method": "tools/call",
  "params": { "name": "get_work_status", "arguments": { "workPackageId": "3f1c..." } } }
```

## 3. Who does what

DevWeave coordinates many desktop agents (Cursor, Claude, and similar) working at the same time from
different devices and locations on **one** git repository. Git is the codebase. DevWeave is the shared
control plane: decisions, grants, leases, and claims. Humans supervise in Studio or through the API.

| You are | Grants you need | You may | You must not |
| --- | --- | --- | --- |
| Planning agent | `ReadKnowledge`, `WriteKnowledge` | Brainstorm into a proposal, bind-ready specs, plan expansions, Work item claims | Take a lease, submit a change set, merge, verify, approve, or deploy |
| Execution agent | `ExecuteWorkflow` (plus read/context grants the Owner issued) | Register a runtime, lease a scope, code and test locally, submit git references, report a workthrough | Approve a plan, change a binding spec, verify, approve, or deploy to production |

Two products on two machines are two runtimes. They do not share a lease. A retry uses the same
idempotency key. A move to a new device starts at case C0 and does not heartbeat a lease held on the old device.

## 4. Case playbooks

Pick one case. Do not skip its human gate. Tool success means DevWeave stored your claim. It does not mean
a person approved it, tests passed, git merged, or production changed.

### C0 — Connect from any device

**When:** the first call of a session, a reconnect, or a different computer.

**Agent:** either.

1. `initialize` with protocol version `2026-07-28`, then `tools/list`. Another version is refused. Omitting the version accepts the advertised one. The list is your grant. A missing tool means you are the wrong agent for that case.
2. Execution: `register_runtime`, then send `X-Runtime-Id` on every later call. Capability, provider, location, workspace, and billing stay on that runtime. They are not campaign fields, and registration does not provision a runner.
3. `list_codebase_targets`. Clone or pull **that** provider, repository, and branch from the git host. If the list is empty, a planning agent must register the pointer before anyone codes.
4. Planning: `read_plan_outline` and `get_effective_spec`. Execution: `get_plan_status`, then `list_channels` and `read_messages_after_cursor` so you catch up after a disconnect.
5. `acknowledge_messages` through the sequence you have applied. Acknowledgement is only that cursor. Replay records read. Neither one records acceptance.

**Do not:** invent a second repository, trust `clientInfo` for tenant or project, or continue a lease you did not create on this runtime.

**Human sees:** the runtime and channel cursor in Studio Coordination.

### C1 — Brainstorm

**When:** the idea is not an approved plan yet.

**Agent:** Planning.

1. Think in the local IDE. Keep raw notes local.
2. `read_plan_outline`. An empty tree means case C3, not a channel post. A published tree means case C4.
3. Turn the idea into knowledge, outline, relations, and work items only when you are ready to propose.

**Do not:** `send_project_message` the brainstorm. Channel text never appears on Knowledge, Relations, or Work. Do not start feature coding.

**Human gate:** nothing is approved in this case. Approval is C3 or C4.

### C2 — Decide the specification

**When:** a technology, convention, or pattern is not bound yet, or you are about to contradict one.

**Agent:** Planning. Execution agents stop and hand off.

1. `get_effective_spec`. If the topic is bound, follow `resolve_spec`. Do not improvise a conflicting stack.
2. If it is open: `next_open_topics`, `list_spec_options`, then `propose_spec_decision` or `propose_spec_profile`.
3. Stop. Tell the Owner which proposal is waiting.

**Human gate:** Studio specification approval. The decision does not bind because you proposed it.

**Do not:** code against an unbound or rejected choice. Do not hide a stack decision inside a commit message.

### C3 — First plan for an empty project

**When:** `read_plan_outline` is empty (new app, or a repo DevWeave does not know yet).

**Agent:** Planning.

1. Design from the local repo. Do not upload source.
2. `list_schemas`. Copy `typeId` and `schemaVersion`. If `schemas` is empty, stop: the Owner must publish schemas. Agents cannot.
3. For relations, `list_relation_types` and use an active `code`. `sourceRef` and `targetRef` are knowledge refs in this bundle, not file paths.
4. `propose_initial_plan` with a stable `idempotencyKey`, `requireEmptyProject: true`, `knowledge[]`, and `outline[]` with exactly one root (`parentRef` null). Include `codebaseTargets[]` so every later device shares one git identity (`provider`, `repositoryId` as `owner/name` or a clean remote URL, `targetBranch`, `baseCommitSha`). Optional relations, milestones, work items, and work links follow the tool schema. `representationKind` is never `SourceCode`.
5. Success returns status `Proposed`. Ask the Owner to open Studio → Plan import → Approve and materialize.

**Human gate:** Plan import approval. You cannot approve your own proposal.

**Do not:** look for the bundle under Coordination. It stays on Plan import until approval. After approval, use C4 for further design.

### C3 existing build — A git product DevWeave has not recorded

**When:** the repository already contains the product. `read_plan_outline` is empty, or it already has nodes and you are adding slices.

**Agent:** Planning.

1. Same discovery as C3: `list_schemas`, and `list_relation_types` when you send relations. One work item per slice you can name (already built, partial, or not started). Do not put `status` or `Done` on the bundle.
2. Empty outline, including a full existing repo: `propose_initial_plan` with `requireEmptyProject: true`. A project has one outline root, so a second import does not attach another tree or edit an existing item. A new child is `propose_plan_expansion`.
3. Success is status `Proposed`. Ask the Owner to approve. Imported work is Planned / NotStarted / Unverified / Pending.
4. After approval, `list_work_items`, then `report_work_item` once per slice: `done` when that slice is already in git, `InProgress` when it is partial, and leave `NotStarted` when work has not begun. `done` stores reported state `Completed`.
5. `get_project_metrics`. `work.completion_estimate` is reported-complete items divided by all work items. `work.reported_completed_count` is the count. `closedloop.evidence_backed_completion` stays 0 until a human verifies.

**Human gate:** Plan import approval. Verify and approve of Work stay in Studio. Reported completion is your claim of how much is built, not acceptance.

**Do not:** send proposal status Done. `update_work_item_plan_state` has no Done state. Do not paste source.

### Changing a requirement

A published requirement changes by a new revision, which a human adds and reviews. The previous revision stays. `read_plan_outline` keeps each work link on that previous revision until a human re-pins it. There is no MCP tool to add a revision, edit requirement content, or move the pin.

A new revision clears review. `propose_plan_expansion` under that item is refused until a human reviews the new revision. After that review, the expansion is still only a child proposal.

`report_work_item` sends `expectedVersion` from `list_work_items` when you want to guard the write. A stale version is `-32009`.

### C4 — Design after a plan exists

**When:** the outline is published and you need a child idea or a Work status claim.

**Agent:** Planning.

1. `read_plan_outline` and `list_work_items`.
2. New child idea: `propose_plan_expansion`. It waits for human review. It does not create an approved item.
3. Progress claim: `report_work_item` (`NotStarted`, `InProgress`, `Blocked`, `Completed`, `Failed`; `done` means `Completed`). Backlog shape: `update_work_item_plan_state`.
4. Another git pointer, still references only: `register_codebase_target`.

**Human gate:** expansion review on the plan outline. **Verify** and **approve** of Work stay in Studio. There is no MCP tool for either. `Completed` is your claim, not acceptance.

### C5 — A requirement changes while others are coding

**When:** `get_work_status` returns `canProgress: false`, a lease or change set is refused (`-32009`), or the Owner locks a new baseline.

**Agent:** Execution for the stop. Planning may propose the new requirement. Neither agent authorizes the rebase.

1. Stop editing that package.
2. `report_workthrough` with `kind: "Blocked"` (or `Succeeded` only if the package's outcome is actually finished), then `release_lease`.
3. `send_project_message` on the campaign channel: unfinished scope, last SHA, paths touched.
4. `get_plan_status` and `read_plan_outline`. Continue only on a campaign with `onCurrentPlan: true`.

The control plane suspends old campaigns first. Affected work stays stopped until the Owner records a rebase decision. A continuation is a new package linked to its predecessor, not a silent edit of the old baseline. Stale context cannot take a lease, submit a change set, or report success. If two agents propose incompatible meanings, both records stay; do not overwrite the other. The Owner resolves the semantic conflict in Studio or the API.

**Do not:** keep coding because your local checkout still builds. Do not revive a suspended package yourself.

### C6 — Code on the shared repository

**When:** a current-plan campaign exists and your scope is not held by someone else.

**Agent:** Execution.

1. `get_plan_status`. Ignore campaigns that are not `onCurrentPlan`.
2. `list_work_packages` or `create_work_package` with `implementsRevisionIds`, `workItemIds`, and `specTopics`. Unscoped packages cannot be traced when the plan changes.
3. `get_work_status`. Continue only when `canProgress` is true.
4. `request_assignment` with a `resourceScope` that names the files or area you will touch. Keep `id` and `fence`.
5. Commit in git on that scope only. `heartbeat_lease` before `expiresAt`. If the renewal is refused, go to C5.
6. `submit_change_set` with `codebaseTargetId`, `providerRef`, `commitSha`, and `changedPaths`.

**Do not:** edit a path inside another agent's scope. Do not paste source, diffs, or secrets. Two agents may work in parallel only on disjoint scopes of the same codebase target.

**Human sees:** the lease, fence, and change-set reference in Coordination. Git still shows the real diff.

### C7 — Test

**When:** the code for your leased scope is committed.

**Agent:** Execution. Tests run on your machine or in the pipeline you were given, not inside DevWeave.

1. Run the tests for your scope.
2. `report_workthrough`. Success needs `outcome` and `whatSucceeded`. Failure uses `kind: "Blocked"`, `outcome`, and `blockedReason`. Optional `steps` (at most 20, each at most 300 characters) and `changeSetId` for a change set of this same package.
3. Say which commands passed or failed. Do not paste source, secrets, or logs that contain them.

**Human gate:** accepted evidence and verification are recorded by a person in Studio. Your workthrough is not that record.

**Do not:** report `Succeeded` when tests failed. Do not report a package you never leased; say so on the channel instead.

### C8 — Quality control

**When:** you are checking whether the work meets the approved requirement and binding spec.

**Agent:** Execution reports the claim. Planning may set the Work item's reported state. A human verifies.

1. Re-read the requirement you declared on the package (`get_plan_baseline`, `read_plan_outline`, `resolve_spec`).
2. If the result matches, `report_workthrough` `Succeeded` and, from a planning agent, `report_work_item` `Completed` with the reason.
3. If it does not, `Blocked` / Work item `Blocked` or `Failed`, and name the gap.

**Human gate:** Studio Work **verify** and **approve**. Reported `Completed` does not change verification or approval.

**Do not:** mark your own work verified. Do not treat another agent's conflicting report as deleted; leave both for the Owner.

### C9 — Hand off to another agent or device

**When:** you stop before the scope is finished, or the person continues on another machine.

**Agent:** the one holding the lease.

1. Commit or discard local edits so the git host matches what you will declare. Do not leave the only copy on a laptop.
2. `submit_change_set` if there is a new SHA.
3. `report_workthrough` `Blocked` or partial `whatSucceeded`.
4. `release_lease`.
5. `send_project_message`: package id, SHA, paths, what remains, and what the next agent must not redo.
6. The next device starts at C0, reads that message, `get_work_status`, pulls git, and takes a **new** lease. It does not reuse your fence.

### C10 — Integrate

**When:** the change set should be considered for the shared branch.

**Agent:** Execution.

1. `get_work_status` again.
2. `request_integration` with the change-set id and a stable `idempotencyKey`.
3. If DevWeave rejects it, the plan moved or the context is stale. Go to C5. Do not force the git merge to "catch up".

**Human gate:** merge on the git host. `request_integration` only queues a reference. It does not merge.

### C11 — Deploy to production

**When:** someone asks to ship.

**Agent:** neither agent deploys from MCP. There is no deploy tool.

1. Planning: `list_work_items`. Proceed only when verification and approval are already human-accepted. If you cannot see those fields as accepted, stop.
2. Execution: do not deploy from a workthrough or a queued change set. Those are claims and references.
3. After a human deploys, a planning agent may record the released SHA with `propose_plan_expansion` or a `delivery.release` knowledge proposal, references only.

**Human gate:** production deploy stays with the Owner, in the hosting platform, after Studio verification and approval.

**Do not:** deploy because tests passed locally or because `request_integration` succeeded.

### C12 — Refused, stale, or lost

**When:** any call returns `-32009`, `-32003`, or `-32004`, or you do not know whether your lease is alive.

1. `-32009`: `get_work_status` and `get_plan_status`, then C5. Do not retry the same mutation.
2. `-32003`: you lack the grant, or you are reporting a package you never leased. Ask the Owner. Do not retry.
3. `-32004`: the id is wrong or hidden from you. Do not guess a replacement.
4. `-32602`: fix the arguments. `prohibited-content` means you sent source or a secret; send a reference instead.
5. Network retry: reuse the idempotency key. Do not create a second package, proposal, or change set.

## 5. Tool reference

### Orientation and execution

| Tool | Use it to | Key arguments |
| --- | --- | --- |
| `get_plan_status` | See the current plan version and each campaign's version (`onCurrentPlan`) | none |
| `list_work_packages` | See a campaign's packages, scope, and whether each can progress | `campaignId` |
| `get_work_status` | Check one package: `canProgress`, `blockedReason`, `onCurrentPlan` | `workPackageId` |
| `create_work_package` | Create scoped work under a campaign's plan version | `campaignId`, `baselineId`, `objective`, optional `implementsRevisionIds`, `workItemIds`, `specTopics` |
| `request_assignment` | Take an exclusive lease on part of a package | `workPackageId`, `resourceScope`, `expiresAt`, `idempotencyKey` |
| `heartbeat_lease` | Renew your lease (refused once the plan moved on) | `leaseId`, `expectedFence`, `expiresAt` |
| `release_lease` | Let go of a lease (always allowed) | `leaseId`, `expectedFence` |
| `submit_change_set` | Record your change by reference | `workPackageId`, `codebaseTargetId`, `providerRef`, `commitSha`, `changedPaths` |
| `report_workthrough` | Write success or blockage for a package (agent claim, not acceptance) | `workPackageId`, `kind` (`Succeeded`\|`Blocked`), `outcome`; success needs `whatSucceeded`; blockage needs `blockedReason`; optional `steps`, `changeSetId` |
| `request_integration` | Queue a change set; the decision re-checks the plan | `changeSetId`, `idempotencyKey` |
| `get_plan_baseline` | Read the export manifest of a locked plan version | `baselineId` |
| `list_ready_work` | Deprecated alias of `create_work_package` | |

### Plan

| Tool | Use it to | Key arguments |
| --- | --- | --- |
| `read_plan_outline` | Read the outline, revision-pinned work links and your own proposals | none |
| `propose_initial_plan` | Submit a plan bundle for Owner approval. Empty outline uses `requireEmptyProject: true`, including an existing git product. `false` still cannot add a second outline root. The bundle has no Done status | `idempotencyKey`, `knowledge`, `outline`, optional relations/work/`codebaseTargets`, `requireEmptyProject` |
| `list_codebase_targets` | List registered git-host pointers (no source) | none |
| `register_codebase_target` | Record a git identity (`owner/name` or clean remote URL + branch + SHA). Planning only. | `provider`, `repositoryId`, `targetBranch`, `baseCommitSha` |
| `propose_plan_expansion` | Suggest a child item for human review | `parentItemId`, `key`, `title`, `typeId`, `rationale` |
| `list_schemas` | Discover active `typeId` / `schemaVersion` pairs accepted by plan import. Prefer `requirements.functional` and `requirements.nonfunctional`; `plan.*` stays valid | none |
| `get_project_metrics` | Read authorized metrics. `work.completion_estimate` is how much work is reported complete. `closedloop.evidence_backed_completion` counts only human-verified items. The ratios are separate | none |
| `list_relation_types` | Discover active relation codes, endpoint constraints, direction and cardinality | none |
| `list_work_items` | Read Work items and the four independent states. Imported items start Planned / NotStarted / Unverified / Pending | none |
| `report_work_item` | Planning claim of reported progress. For an existing build, `done` means the slice is already in git. Not verification. | `workItemId` or `key`, `state` (`done`, `InProgress`, `NotStarted`, …), `reason`, optional `expectedVersion` |
| `update_work_item_plan_state` | Planning update of planned/backlog state | `workItemId` or `key`, `state`, `reason`, optional `expectedVersion` |

The same schema vocabulary is available to the authenticated agent at
`GET /tenants/{tenantId}/agents/{agentId}/capabilities`. Global `GET /schemas` is a human registry surface;
planning clients should use the MCP tool or their own agent-capability endpoint.

### Specifications

| Tool | Use it to | Key arguments |
| --- | --- | --- |
| `get_effective_spec` | Read every binding decision, undecided topics, incompatibilities | none |
| `resolve_spec` | Get the one binding decision for a topic, with its origin | `topic` |
| `list_spec_topics` / `get_spec_catalog` | See topics, legal choices, typed parameters, packs and profiles | none |
| `list_spec_options` | Ranked options with compatibility reasons; choose from this list | `topic` |
| `next_open_topics` | Topics still to decide, in dependency order | none |
| `propose_spec_decision` | Propose one decision (validated before storage) | `topic`, `level`, `choice`, optional `params`, `rationale` |
| `propose_spec_profile` | Propose a whole profile or a set of decisions for one approval | `level`, `profile` or `decisions` |
| `propose_spec_overlay` | Propose catalog extensions (custom options, topics, profiles) | `overlay` |
| `get_spec_pack_upgrades` | See built-in pack upgrades and decisions they would affect | none |

### Coordination channels

`list_channels`, `open_channel`, `send_project_message`, `read_messages_after_cursor` (alias
`list_messages`), `notify_messages`, `acknowledge_messages`, `accept_messages`, `get_message_delivery`:
discover and exchange durable messages between runtimes and humans. A successful send means the message
is stored. Replay records read. `notify_messages`, `acknowledge_messages`, and `accept_messages` each move
only their own cursor. `get_message_delivery` reports the five states. These are execution tools and require
`ExecuteWorkflow`. Channel payloads never materialize plan records; inspect them in Studio under
Coordination and Live operations.

## 6. When the plan changes

Follow case C5. Locking a new baseline suspends campaigns on the old one. DevWeave then refuses new
leases, renewals, change sets, and integration for that work (`-32009`). A queued change set is rejected
at decision time and is not merged. `get_work_status` returns `canProgress: false`. The Owner may later
authorize a continuation package; you do not resume the suspended package in place. The same stop applies
when a kill switch closes your package, campaign, runtime, or agent. Stale context cannot report success.

## 7. Errors

Errors are JSON-RPC errors with a stable `code` and a `data.kind`. React to the code, not the message text.

| Code | `data.kind` | Meaning | What to do |
| --- | --- | --- | --- |
| `-32009` | `conflict` | The request is not allowed in the current state, e.g. the plan moved on, a stale fence, a locked topic | Case C5, then C12. Do not retry the mutation |
| `-32004` | `not-found` | The resource does not exist in this project, or you may not see it | Check ids and your project; do not guess |
| `-32003` | `forbidden` | Missing grant or credential, or `report_workthrough` on a package you never held a lease on | Ask the Owner for the grant, or report only on your own work; do not retry |
| `-32602` | `invalid` | Bad arguments; `data.issues` lists exact problems for proposals | Fix the arguments using the issues |
| `-32602` | `prohibited-content` | Content looked like source code or secrets | Send references only |
| `-32601` | `unknown-tool` | Tool name is wrong | Re-read `tools/list` |
| `-32603` | none | Unexpected server error | Retry later with the same idempotency key |

## 8. Known limits

- Declaring a package scope is optional today; unscoped packages show `unscoped: true` because a plan change
  cannot be traced to them. Always declare scope.
- A new baseline suspends older campaigns first. Continuation of unaffected work requires an Owner rebase
  decision and a new package; DevWeave does not push that decision to your IDE. Re-verification of evidence
  pinned to an old requirement revision is not automatic.
- There is no MCP tool to merge git, verify Work, approve Work, resolve a semantic conflict, or deploy.
  Those stay on Studio or the API.
- Coordination tools check your authentication and project; planning and specification tools also check
  your grants.
- A package you could not lease (for example because the plan had already moved on) cannot receive a
  workthrough from you. Say what blocked you on the campaign channel instead.
- On Windows, third-party Python clients should call `sys.stdout.reconfigure(encoding="utf-8")` when
  available or use ASCII-only console messages. DevWeave protocol payloads are UTF-8; decorative terminal
  emoji are a client concern and are not required by this API.
