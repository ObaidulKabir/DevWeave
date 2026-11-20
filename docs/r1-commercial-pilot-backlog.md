# R1 commercial pilot backlog

Do not expand this backlog until Foundation MVP Phase A exit (every M/MVP row
`pass`/`waived`, production OIDC, least-privilege DB, OPS drill recorded), unless
product explicitly waives selling R1 early.

## Acceptance checklist

| # | Work | SRS / AC | Exit evidence |
| --- | --- | --- | --- |
| B1 | Project MCP + two-runtime campaign E2E on production OIDC agents | §16.10, MCP-*, MCO-* | Recorded run replacing pilot JWT |
| B2 | Commercial entitlements/usage visibility in API + Studio | COM-001..007, COM-020..025 | Integration tests + Studio screen |
| B3 | First real code-host adapter (GitHub) with protected integration queue | MCO-BR-09 | PR/ref-only change-set proof |
| B4 | Kill switch / revocation latency measurement | MCO-048, MCP-034 | Measured time to halt leases/subscriptions |
| B5 | Agency multi-client isolation demo | COM-016, BUS-AC-01 | Two client projects, no cross-leak |

## Non-goals (remain R2+)

- Managed DevWeave compute
- Billing automation
- Next.js migration without ADR approval
