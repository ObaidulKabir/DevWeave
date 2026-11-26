# R1 commercial pilot backlog

Foundation MVP closed 2026-11-26. R1-A is recorded: a campaign stores the
approved baseline and the pinned codebase commit. R1-B is recorded: two remote
runtimes receive coherent packages on one baseline after accepted offers.
[docs/version-policy.md](version-policy.md) opens the next increment at R1-C.
R1-C has not started. Do not add R1-D or a later campaign, commercial, or
agency case ahead of it. Code already in the tree stays. The live-host identity
waiver expires 2026-12-11 and is not the production-OIDC run in B1.

## Acceptance checklist

| # | Work | SRS / AC | Exit evidence |
| --- | --- | --- | --- |
| B1 | Project MCP + two-runtime campaign E2E on production OIDC agents | §16.10, MCP-*, MCO-*, BUS-AC-06 | BUS_AC_06: one campaign accepts a CustomerManaged runtime and a recorded DevWeaveManaged runtime under the same baseline and the same integration stage. DevWeave does not provision a runner or merge git. A recorded production-OIDC run is still open |
| B2 | Commercial entitlements/usage visibility in API + Studio | COM-001..007, COM-020..025, BUS-AC-02, BUS-AC-05, BUS-AC-08 | A human can read the active plan and usage by project and execution mode. An external agent registers as CustomerManaged with no plan. A DevWeaveManaged runtime is a recorded location and a separate meter; registration does not write knowledge or provision a runner. A tenant with no execution subscription can export its knowledge and permitted artifact checksums. A project cap of one campaign leaves the other project's remaining tenant slot available, and the block is stored. A runtime records capability, provider, location, workspace, and billing apart from the campaign. Registration does not provision a runner. Billing automation remains open |
| B3 | First real code-host adapter (GitHub) with protected integration queue | MCO-BR-09 | References only. Reviewed, Tested, Merged, and Verified are separate recorded stages. DevWeave does not merge the protected branch |
| B4 | Kill switch / revocation latency measurement | MCO-048, MCP-034 | In-process target is 2 seconds: the next MCP tool call is refused and the runtime lease is expired. A production network measurement is still open |
| B5 | Agency multi-client isolation demo | COM-016, BUS-AC-01, BUS-AC-07 | BUS_AC_01: two client members each see only their project, knowledge, and metrics. A repository write on the other project is 404. BUS_AC_07: a client viewer does not receive agent-channel messages or Restricted knowledge. A live customer walkthrough is still open |

## Non-goals (remain R2+)

- Managed DevWeave compute
- Billing automation
- Next.js migration without ADR approval
