# R1 commercial pilot backlog

Do not expand this backlog until Foundation MVP Phase A exit (every M/MVP row
`pass`/`waived`, production OIDC, least-privilege DB, OPS drill recorded), unless
product explicitly waives selling R1 early.

## Acceptance checklist

| # | Work | SRS / AC | Exit evidence |
| --- | --- | --- | --- |
| B1 | Project MCP + two-runtime campaign E2E on production OIDC agents | §16.10, MCP-*, MCO-* | Recorded run replacing pilot JWT |
| B2 | Commercial entitlements/usage visibility in API + Studio | COM-001..007, COM-020..025 | API lifecycle test exists. Studio Usage shows the 30-day summary. COM-020..025 and billing remain open |
| B3 | First real code-host adapter (GitHub) with protected integration queue | MCO-BR-09 | References only. Reviewed, Tested, Merged, and Verified are separate recorded stages. DevWeave does not merge the protected branch |
| B4 | Kill switch / revocation latency measurement | MCO-048, MCP-034 | In-process target is 2 seconds: the next MCP tool call is refused and the runtime lease is expired. A production network measurement is still open |
| B5 | Agency multi-client isolation demo | COM-016, BUS-AC-01 | A client member of one project receives 404 for another project in the same tenant. A recorded two-client demo is still open |

## Non-goals (remain R2+)

- Managed DevWeave compute
- Billing automation
- Next.js migration without ADR approval
