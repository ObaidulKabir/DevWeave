# DevWeave guide for AI agents (MCP)

DevWeave is the **single decision and control plane** for the projects you work on. You talk to it through
MCP. Humans make and approve decisions in the DevWeave dashboard (Owner Studio). This guide tells you how to
connect, what to read before you work, how to execute work, and what to do when the plan changes under you.

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
| Methods | `initialize`, `tools/list`, `tools/call` (`{ "name": ..., "arguments": {...} }`) |
| Arguments | Always a JSON **object** (or omitted). Arrays and other shapes are refused with `-32602`. Check your client, for example PowerShell must send a hashtable, not `$args` |

**Access you need** (granted by a project Owner in the dashboard):

- Active membership in the project.
- `ReadKnowledge` grant to read the outline and specifications; `WriteKnowledge` to propose anything.
- Your grant's classification ceiling limits which knowledge you can see.

Example call:

```json
{ "jsonrpc": "2.0", "id": 7, "method": "tools/call",
  "params": { "name": "get_work_status", "arguments": { "workPackageId": "3f1c..." } } }
```

## 3. The session loop

1. **Start up**: `initialize`, `tools/list`, then `register_runtime` and send `X-Runtime-Id` from then on.
2. **Orient**: `get_plan_status` (current plan version and campaigns), `read_plan_outline` (what is being
   built), `get_effective_spec` (every binding decision).
3. **Fill gaps** if your work needs a decision that is missing: `next_open_topics`, `list_spec_options`,
   then `propose_spec_decision` (or `propose_spec_profile` for a whole stack). Wait for approval.
4. **Pick or create work** on a campaign whose `onCurrentPlan` is true: `list_work_packages`, or
   `create_work_package` with `implementsRevisionIds` / `workItemIds` / `specTopics`.
5. **Lease** the part you will change: `request_assignment` (keep the returned `id` and `fence`).
6. **Work in the repository** outside DevWeave. Renew with `heartbeat_lease` before the lease expires.
7. **Submit**: `submit_change_set` (references only), then `request_integration`.
8. **Workthrough**: `report_workthrough` with `kind` `Succeeded` or `Blocked`. For success: `outcome` + `whatSucceeded`. For blockage: `outcome` + `blockedReason` (optional `whatSucceeded` for partial progress). Optional `steps` (at most 20, each at most 300 characters; too many or too long is refused, never cut) and `changeSetId` (it must be a change set of this same package). You may report only on a package you **held a lease on**. The text goes through the same content policy as everything else: describe what happened, never paste source code or secrets. This is your claim — **not** verification or approval.
9. **Finish**: `release_lease`. You may still `report_workthrough` after the plan stops the package (final success or blockage).

Before steps 5, 6 and 7, and whenever a call is refused, call `get_work_status`. If `canProgress` is false,
stop and follow section 5.

## 4. Tool reference

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
| `propose_plan_expansion` | Suggest a child item for human review | `parentItemId`, `key`, `title`, `typeId`, `rationale` |

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

`open_channel`, `send_project_message`, `read_messages_after_cursor`, `acknowledge_messages`: durable
messages between runtimes and humans. Acknowledge what you processed so you can resume after a reconnect.

## 5. When the plan changes

A project Owner can change the plan and lock a new plan version at any time. When that happens:

- Campaigns on older versions are **suspended**.
- On suspended work DevWeave **refuses** new leases, lease renewals and change sets (error `-32009`).
- Work already queued for integration is **rejected at decision time** with the reason recorded. It is
  never merged.
- `get_work_status` returns `canProgress: false`, `onCurrentPlan: false` and a `blockedReason`.

What you do:

1. Stop changing the repository for that package.
2. `report_workthrough` with `kind: "Blocked"` (or `Succeeded` if you finished), `outcome`, `blockedReason` and/or `whatSucceeded` as appropriate (final claim is still allowed).
3. `release_lease` for every lease you hold on it.
4. Post a short status on the campaign channel if one is open (what is unfinished).
5. Call `get_plan_status` and `read_plan_outline` to see the new version.
6. Continue only on a campaign with `onCurrentPlan: true`. Do not try to revive suspended work.

The same applies when a work package is closed or a kill switch stops your campaign, runtime or agent.

## 6. Errors

Errors are JSON-RPC errors with a stable `code` and a `data.kind`. React to the code, not the message text.

| Code | `data.kind` | Meaning | What to do |
| --- | --- | --- | --- |
| `-32009` | `conflict` | The request is not allowed in the current state, e.g. the plan moved on, a stale fence, a locked topic | `get_work_status` / `get_plan_status`, then follow section 5 or refresh and retry |
| `-32004` | `not-found` | The resource does not exist in this project, or you may not see it | Check ids and your project; do not guess |
| `-32003` | `forbidden` | Missing grant or credential, or `report_workthrough` on a package you never held a lease on | Ask the Owner for the grant, or report only on your own work; do not retry |
| `-32602` | `invalid` | Bad arguments; `data.issues` lists exact problems for proposals | Fix the arguments using the issues |
| `-32602` | `prohibited-content` | Content looked like source code or secrets | Send references only |
| `-32601` | `unknown-tool` | Tool name is wrong | Re-read `tools/list` |
| `-32603` | none | Unexpected server error | Retry later with the same idempotency key |

## 7. Known limits

- Declaring a package scope is optional today; unscoped packages show `unscoped: true` because a plan change
  cannot be traced to them. Always declare scope.
- A new plan version currently suspends **all** work on older versions, even work the change does not touch.
  Impact-based continuation, push notifications to agents and re-verification of finished work are planned.
- Coordination tools check your authentication and project; planning and specification tools also check
  your grants.
- A package you could not lease (for example because the plan had already moved on) cannot receive a
  workthrough from you. Say what blocked you on the campaign channel instead.
