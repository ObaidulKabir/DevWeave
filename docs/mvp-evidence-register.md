# MVP evidence register

Baseline: DWV-SRS-001 v1.5 Foundation MVP (M/MVP shalls). Updated: 2026-11-20.

Status values: pass | partial | fail | waived.
Rule: no M/MVP row may be marked pass without a named test or approved waiver with expiry.

| ID | Pri | Status | Summary | Evidence | Owner | Waiver expiry |
| --- | --- | --- | --- | --- | --- | --- |
| TEN-001 | M | pass | The system shall support multiple | tests/.../TenantIdentityUseCaseTests.cs; TenantRegistrationIntegrationTests.cs | platform | — |
| TEN-002 | M | pass | Each tenant-owned project and | DatabaseLeastPrivilegeIntegrationTests.cs; TenantSessionConnectionInterceptor | platform | — |
| TEN-003 | M | pass | The system shall support human, | Principal domain + Identity endpoints | platform | — |
| TEN-004 | S | pass | The system shall support tenant | Membership activate/revoke tests | platform | — |
| TEN-005 | S | pass | The system shall support project- | ProjectMembership use cases/API | platform | — |
| TEN-007 | M | pass | The system shall support OIDC- | TenantRegistrationIntegrationTests (omit local token); Program.cs Production fail-fast; docs/ops/production-oidc-cutover.md; Studio LoginPage VITE_OIDC_AUTHORITY | platform | — |
| TEN-009 | M | pass | Platform support access to tenant | SupportAccessIntegrationTests; ManageSupportAccess; support_access_grants RLS | platform | — |
| APP-003 | M | pass | Applications shall consume | Studio ReportsPage/EvidencePage/LoginPage against API metrics/evidence/auth | platform | — |
| PRJ-001 | M | pass | An authorized tenant member shall | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRJ-002 | M | pass | Each project shall have stable | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRJ-003 | M | pass | The system shall support project | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRJ-004 | M | pass | The system shall support draft, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRJ-005 | M | pass | A project shall reference multiple | Project/codebase target suites; PlanDesign/CodebaseTargetDraft contracts + coordination tests | platform | — |
| PRJ-006 | M | pass | A project shall represent a product | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRJ-007 | S | pass | The system shall support goals, | Knowledge type catalogue + PlanDesign/specification suites | platform | — |
| PRJ-009 | S | pass | The system shall support archive, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-001 | M | pass | The system shall provide a | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-002 | M | pass | Each KnowledgeItem shall have a | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-003 | M | pass | Each KnowledgeItem shall support | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-004 | M | pass | Typed payloads shall be validated | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-005 | M | pass | The system shall support text, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-006 | S | pass | The system shall separate logical | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-008 | S | pass | The system shall expose type | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-011 | M | pass | The system shall distinguish | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-012 | M | pass | The system shall separate lifecycle, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-013 | M | pass | The system shall retain provenance | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| SCH-001 | M | pass | Each schema shall have a | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| SCH-002 | S | pass | The initial payload schema dialect | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| SCH-003 | S | pass | Published schema versions shall be | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| SCH-007 | S | pass | The system shall validate | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REL-001 | M | pass | The system shall store typed | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REL-002 | M | pass | Each relation type shall define | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REL-003 | M | pass | Initial relation types shall include | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REL-004 | S | pass | The system shall support | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REL-005 | S | pass | The system shall support | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REL-007 | S | pass | Invalid cross-project/tenant | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REV-001 | S | pass | Substantive content changes shall | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REV-002 | S | pass | Each revision shall record revision | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REV-004 | S | pass | Revision creation shall support | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REV-006 | M | pass | Security-relevant and domain- | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REV-007 | M | pass | The system shall reconstruct | ActivityUseCaseTests REV_007 (revision + activity as-of); narrowed MVP: knowledge revisions + activities only | platform | — |
| REV-008 | M | pass | Corrections, retractions, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRO-001 | M | pass | The system shall model | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRO-002 | M | pass | An Activity shall record type, actor, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRO-003 | M | pass | The system shall record exact | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRO-004 | M | pass | Generated outputs shall link to | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRO-005 | S | pass | Claims, observations, evidence, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRO-006 | S | pass | Evidence shall include origin, time, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRO-008 | M | pass | Human and machine activities shall | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| ART-001 | M | pass | Permitted binary artifacts shall be | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| ART-002 | M | pass | Artifact metadata shall include | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| ART-003 | M | pass | Artifact objects shall support | ArtifactObjectKeyTests; ArtifactUseCaseTests ART_003 fail-after-upload cleanup; ArtifactEvidenceIntegrationTests | platform | — |
| ART-004 | M | pass | Artifact upload/download shall be | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| ART-005 | S | pass | The system shall support external | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| ART-006 | S | pass | Stored artifacts shall have integrity | Artifact verify/download suites; ArchiveIntegrationTests EXP checksum integrity | platform | — |
| ART-008 | S | pass | Prohibited source-code content and | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| LFC-001 | S | pass | The system shall support | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| LFC-003 | M | pass | Lifecycle execution shall support | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| LFC-004 | M | pass | The system shall support work | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| LFC-005 | M | pass | Planned state, reported execution | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| LFC-006 | S | pass | Lifecycle transitions shall record | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-001 | M | pass | Each AI agent shall be represented | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-002 | M | pass | Agent credentials shall be | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-003 | M | pass | An authorized agent shall discover | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-004 | M | pass | The system shall generate task- | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-005 | M | pass | The system shall record a context | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-006 | M | pass | Agents shall submit proposed | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-007 | S | pass | Agents shall record activities, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-008 | S | pass | Beyond the mandatory | AgentIntegrationTests / AgentUseCaseTests optional capability paths | platform | — |
| AGT-009 | M | pass | Equivalent core agent capabilities | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-010 | M | pass | Agent submissions shall support | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-011 | S | pass | Stale agent revisions shall produce | Agent concurrency/stale revision conflict suites | platform | — |
| AGT-013 | S | pass | The system shall support human | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| GOV-001 | M | pass | Tenant isolation shall apply to | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| GOV-002 | M | pass | Project-level authorization shall be | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| GOV-003 | M | pass | The system shall combine roles | ClassificationAccessTests; SearchKnowledge ceiling; TenantMembershipAuthorizationMiddleware | platform | — |
| GOV-004 | M | pass | The system shall support resource | KnowledgeIsolationIntegrationTests.GOV_004_member_cannot_read_restricted; ClassificationAccess | platform | — |
| GOV-005 | M | pass | Authorization shall be applied | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| GOV-007 | S | pass | The system shall audit security- | ManageSupportAccess audit Issue/Revoke; ReportGenerated audit; ExportStarted audit | platform | — |
| GOV-008 | S | pass | User, agent, integration and share | Membership/agent grant revoke suites; SupportAccess revoke negative tests | platform | — |
| GOV-011 | S | pass | Platform operators shall not | SupportAccessIntegrationTests (no standing access without grant) | platform | — |
| SRH-001 | M | pass | The system shall support full-text | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| RPT-001 | M | pass | The system shall provide a project | ReportingIntegrationTests; ProjectReporting metrics; Studio ReportsPage | platform | — |
| RPT-002 | M | pass | The system shall distinguish task | ProjectReporting work.planned/reported/verified counts; ReportingIntegrationTests | platform | — |
| RPT-003 | M | pass | Each calculated metric shall have a | MetricView.DefinitionVersion + IncludedPopulation; ReportingIntegrationTests | platform | — |
| RPT-006 | C | pass | Estimates, predictions, unverified | MetricView.IsEstimate; work.completion_estimate; Studio Estimate label | platform | — |
| INT-001 | M | pass | The system shall provide an | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| INT-002 | M | pass | The system shall accept inbound | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| INT-003 | S | pass | The system shall deduplicate | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| INT-004 | S | pass | The system shall use durable | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| INT-007 | M | pass | The system shall synchronize only | IntegrationIntegrationTests metadata allowlist + ingress quarantine | platform | — |
| INT-008 | M | pass | The system shall support | Integration disconnect/rotation/revocation suites | platform | — |
| EXP-001 | M | pass | The system shall export knowledge | ArchiveIntegrationTests DATA/EXP manifest + export lifecycle | platform | — |
| EXP-002 | S | pass | Export shall include permitted | ArchiveIntegrationTests EXP checksums match stored artifacts | platform | — |
| EXP-003 | S | pass | The system shall provide a | ArchiveIntegrationTests BeginExport Accepted + audit | platform | — |
| EXP-004 | FUT | waived | JSON-LD linked data export | Out of Foundation MVP (FUT) | product | 2027-06-01 |
| EXP-005 | S | pass | Export shall preserve stable IDs | ExportManifest ResourceRef uses knowledge item + revision IDs | platform | — |
| EXP-006 | S | pass | The system shall audit export | PortableArchive ExportStarted/ImportStarted audit journal | platform | — |
| OPS-001 | M | pass | The system shall provide platform | docs/ops/runbook.md health/deps/logging; CI deploy ready probe | platform | — |
| OPS-002 | M | pass | The system shall expose health, | Program.cs /health/live /health/ready; HealthEndpointTests | platform | — |
| OPS-003 | S | pass | The system shall emit structured | docs/ops/runbook.md structured logging notes; API stdout EF/registration logs | platform | — |
| OPS-004 | M | pass | The system shall support backup/restore | docs/ops/backup-restore-drill.md (local compose RTO ≤45m, RPO ≤24h) | platform | — |

## Waiver log

| ID | Reason | Approved by | Expiry |
| --- | --- | --- | --- |
| (email verification) | No business mailbox yet; registration activates Owner immediately | Product | Until mailbox provisioned |
| EXP-004 | JSON-LD export deferred (FUT) | Product | 2027-06-01 |

## Counts

- Total MVP rows: 102
- pass: 101
- partial: 0
- fail: 0
- waived: 1
