# Modifications, November 2026

This note records the control-plane and deploy changes made after the dynamic project-state work.
DevWeave remains the control plane. Git remains the codebase. A successful tool call is a durable
control-plane record. It is not approval, verification, merge, or production deploy.

## Where the changes stand

| Change | Commit | Status |
| --- | --- | --- |
| Lease bound to the holding runtime, released scopes can be leased again, agent case guide | `98e2fb8` | Merged in pull request 18 |
| Provenance export query that Entity Framework can translate | `98e2fb8` | Merged in pull request 18 |
| Workthrough identity checked before the plan-change gate | `98e2fb8` | Merged in pull request 18 |
| Production host frees disk before the image build, one service at a time | `ebaef06` | Pull request 19. Production deploy of that merge succeeded (Actions run 45057114036) |
| Expired Active lease is marked Expired so the scope can be taken | working tree | Local, not committed |
| REST renew and release return 403 when the runtime is missing or wrong | working tree | Local, not committed |
| MCP initialize names case C12 and its refusal codes | working tree | Local, not committed |
| Coverage for the rows above | working tree | Local, not committed |

Apply migration `20261121084347_LeaseScopeUniqueWhileActive` before the lease behavior is used.
It is included in `98e2fb8`. Startup migration applies it when `Database__MigrateOnStartup=true`.

## Lease identity

Renew and release require the runtime id that holds the lease (`X-Runtime-Id`). An empty id or a
different runtime is rejected with “Only the runtime that holds this lease can renew or release it.”
Renew still requires the work package to be open. Release is allowed after the plan suspends the
campaign, so a stopped agent can drop the scope.

The same runtime may renew its own unexpired lease. A different runtime that asks for the same work
package and `resourceScope` while that lease is unexpired receives a conflict.

Index `IX_task_leases_WorkPackageId_ResourceScope` is unique only while `Status = 'Active'`. A
released or expired row no longer blocks the next lease of that scope.

`register_runtime` stores the attestation string as the runtime session key. That key is unique.
A second registration must use a different attestation.

## Expired leases

`GetActiveLeaseAsync` already ignored a row whose `ExpiresAt` had passed. The unique index still
treated that row as Active, so the next acquire failed on save.

Acquire now loads the occupying Active row, including one past its expiry. If `ExpiresAt` is in the
past, the row is marked `Expired` and saved before the new Active lease is inserted. An unexpired
row for the same runtime is returned as-is. An unexpired row for another runtime is a conflict.

## REST responses

Renew and release take `X-Runtime-Id` as an optional header. A missing header is treated as an empty
runtime id and follows the holding-runtime check. The lease JSON field is `state`, not `status`.

These agent denials now return HTTP 403 and `{ "error": "..." }` instead of the bearer Forbid result,
which the API was turning into HTTP 500:

| Call | Message |
| --- | --- |
| Renew or release by a non-holder, or with no runtime id | Only the runtime that holds this lease can renew or release it. |
| List codebase targets without `ReadKnowledge` or `ExecuteWorkflow` | This agent is not allowed to read codebase targets. |
| List channels without `ExecuteWorkflow` | This agent is not allowed to read these channels. |

## Workthrough after a plan change

`report_workthrough` checks that the caller held a lease on the package before it checks whether the
plan is still open. An agent that never held the lease gets `-32003` even when the plan has also
moved. A holder may still file a `Blocked` report after the plan suspends the package. A `Succeeded`
report still requires the package to be open, and then returns `-32009` when it is not.

## Provenance

Export lists the project activities, then loads agent execution metadata for those activity ids.
The query compares `ActivityId` itself. Comparing `ActivityId.Value` inside `Contains` could not be
translated and returned HTTP 500 once metadata rows existed. An activity with no metadata still
exports the PROV-O shape without agent fields.

## Agent cases

`docs/agent-guide.md` is the case router C0–C12: connect, brainstorm, specification, first plan,
later design, plan change, code, test, quality, handoff, integrate, production, and refusal.
Initialize instructions on MCP server `1.4.0` name the same cases. The C12 sentence (conflict
`-32009`, not-found `-32004`, forbidden `-32003`, invalid `-32602`, unknown tool `-32601`) is in
the local working tree. The full steps stay in the guide. There is still no MCP tool to merge,
verify, approve, or deploy.

## Production deploy

Deploy runs only on a push to `main`, over SSH, with `docker compose -f docker-compose.yml --profile=app`.

The host ran out of disk while publishing the API and workers images together. Cleanup that ran
after the build did not prevent that. The script now:

1. Prints disk use.
2. Prunes stopped containers, unused images, and build cache.
3. Leaves volumes in place. Postgres data is `devweave-postgres-data`. Object storage data is `devweave-object-storage-data`.
4. Builds `api`, then `workers`, then `studio`.
5. Starts the app profile with `--no-build`.
6. Waits on `studio` for `http://api:8080/health/ready` (36 attempts, 5 seconds apart).

## Tests that cover this

Run from the repository root:

```text
dotnet test tests/DevWeave.Api.IntegrationTests/DevWeave.Api.IntegrationTests.csproj --filter "FullyQualifiedName~McpCasePlaybookTests|FullyQualifiedName~PRO_008_provenance_export"
```

The last local run passed 9 tests.

| Test | What it locks in |
| --- | --- |
| `Part1_handshake_lists_cases_and_hides_human_gates` | Initialize lists C0–C12 and does not expose human gates as agent tools |
| `Part2_two_devices_keep_disjoint_leases_and_hand_off` | Two devices lease different scopes and hand off |
| `Part3_plan_change_allows_blocked_report_and_refuses_further_work` | A holder can report Blocked after the plan moves; further work is refused |
| `Part4_channel_message_does_not_become_a_plan` | A channel message does not publish a plan |
| `Part5_rest_renew_and_release_require_the_holding_runtime` | Missing or other runtime gets 403; the holder renews, then release returns 204 |
| `Part6_holding_runtime_can_heartbeat_while_the_plan_is_open` | Heartbeat extends an open-plan lease |
| `Part7_expired_active_lease_frees_its_scope` | A past-expiry Active row becomes Expired and another runtime can lease the scope |
| `PRO_008_provenance_export_projects_the_prov_o_shape` | Export with no agent metadata |
| `PRO_008_provenance_export_includes_agent_execution_metadata` | Export includes model, configuration, tool, run, and grant fields |

The playbook class shares one database. Each `register_runtime` in that class uses its own
attestation, because that string is the session key.

The deploy shell script is not in this suite. The check for it is the production run of the merge.
