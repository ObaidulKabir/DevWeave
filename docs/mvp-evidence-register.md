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
| PRJ-001 | M | pass | An authorized tenant member shall | ProjectUseCaseTests.PRJ_001_create_rejects_an_actor_from_another_tenant; ProjectIsolationIntegrationTests | platform | — |
| PRJ-002 | M | pass | Each project shall have stable | ProjectUseCaseTests.PRJ_002_create_project_pins_a_profile_and_records_owning_tenant_and_actor; ProjectIsolationIntegrationTests.PRJ_002 | platform | — |
| PRJ-003 | M | pass | The system shall support project | ProjectMembershipAndGrantTests.PRJ_003_grant_creates_an_active_membership_with_the_requested_role; ProjectUseCaseTests.PRJ_003 | platform | — |
| PRJ-004 | M | pass | The system shall support draft, | ProjectLifecycleTests.PRJ_004_draft_flows_through_active_on_hold_completed_and_archived; ProjectUseCaseTests.PRJ_004_PRJ_009 | platform | — |
| PRJ-005 | M | pass | A project shall reference multiple | Project/codebase target suites; PlanDesign/CodebaseTargetDraft contracts + coordination tests | platform | — |
| PRJ-006 | M | pass | A project shall represent a product | ProjectReferenceTests.PRJ_006_repository_reference_carries_type_identifier_and_url; ProjectUseCaseTests.PRJ_006 | platform | — |
| PRJ-007 | S | pass | The system shall support goals, | Knowledge type catalogue + PlanDesign/specification suites | platform | — |
| PRJ-009 | S | pass | The system shall support archive, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-001 | M | pass | The system shall provide a | KnowledgeItemTests.KNW_001_identity_is_stable_across_revisions; KnowledgeIsolationIntegrationTests.KNW_001_002_003 | platform | — |
| KNW-002 | M | pass | Each KnowledgeItem shall have a | KnowledgeItemTests.KNW_002_creation_pins_type_id_repr_and_schema_version; SchemaRegistryIntegrationTests.KNW_002 | platform | — |
| KNW-003 | M | pass | Each KnowledgeItem shall support | KnowledgeItemTests.KNW_003_envelope_metadata_is_populated_and_typed; KnowledgeUseCaseTests.KNW_003 | platform | — |
| KNW-004 | M | pass | Typed payloads shall be validated | KnowledgeUseCaseTests.KNW_004_add_revision_rejects_an_invalid_payload_and_accepts_a_valid_one; SchemaRegistryIntegrationTests.KNW_004 | platform | — |
| KNW-005 | M | pass | The system shall support text, | KnowledgeItemTests.KNW_005_representation_kinds_cover_all_seven_kinds | platform | — |
| KNW-006 | S | pass | The system shall separate logical | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-008 | S | pass | The system shall expose type | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| KNW-011 | M | pass | The system shall distinguish | KnowledgeItemTests.KNW_011_creation_defaults_to_proposed_even_for_agent_created_content; KnowledgeUseCaseTests.KNW_011 | platform | — |
| KNW-012 | M | pass | The system shall separate lifecycle, | KnowledgeItemTests.KNW_012_lifecycle_review_verification_and_currency_are_independent_dimensions | platform | — |
| KNW-013 | M | pass | The system shall retain provenance | KnowledgeItemTests.KNW_013_source_attribution_is_retained_on_item_and_revision | platform | — |
| SCH-001 | M | pass | Each schema shall have a | SchemaUseCaseTests.SCH_001_register_creates_a_draft_schema; SchemaRegistryIntegrationTests.SCH_001 | platform | — |
| SCH-002 | S | pass | The initial payload schema dialect | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| SCH-003 | S | pass | Published schema versions shall be | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| SCH-007 | S | pass | The system shall validate | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REL-001 | M | pass | The system shall store typed | RelationshipTests.REL_001_creation_assigns_the_directed_envelope_and_active_status | platform | — |
| REL-002 | M | pass | Each relation type shall define | RelationshipIntegrationTests.REL_002_endpoint_type_allowlists_reject_disallowed_satisfies_edges; RelationTypeTests.REL_002 | platform | — |
| REL-003 | M | pass | Initial relation types shall include | RelationshipIntegrationTests.REL_003_the_11_canonical_relation_types_are_seeded_and_discoverable; RelationTypeTests.REL_003 | platform | — |
| REL-004 | S | pass | The system shall support | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REL-005 | S | pass | The system shall support | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REL-007 | S | pass | Invalid cross-project/tenant | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REV-001 | S | pass | Substantive content changes shall | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REV-002 | S | pass | Each revision shall record revision | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REV-004 | S | pass | Revision creation shall support | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| REV-006 | M | pass | Security-relevant and domain- | ActivityTests.REV_006_activity_is_append_only_with_no_writable_properties_or_mutators; ActivityIntegrationTests.REV_006 | platform | — |
| REV-007 | M | pass | The system shall reconstruct | ActivityUseCaseTests REV_007 (revision + activity as-of); narrowed MVP: knowledge revisions + activities only | platform | — |
| REV-008 | M | pass | Corrections, retractions, | KnowledgeItemTests.REV_008_correction_records_superseded_revision_id_and_reason; ActivityIntegrationTests.REV_008 | platform | — |
| PRO-001 | M | pass | The system shall model | ActivityTests.PRO_001_creation_captures_complete_provenance_envelope; ActivityIntegrationTests.PRO_001_002_003_004 | platform | — |
| PRO-002 | M | pass | An Activity shall record type, actor, | ActivityTests.PRO_002_the_activity_status_set_is_exactly_the_four_lifecycle_states | platform | — |
| PRO-003 | M | pass | The system shall record exact | ActivityTests.PRO_003_PRO_004_inputs_and_outputs_are_revision_pinned | platform | — |
| PRO-004 | M | pass | Generated outputs shall link to | ActivityTests.PRO_003_PRO_004_inputs_and_outputs_are_revision_pinned; ActivityIntegrationTests.PRO_001_002_003_004 | platform | — |
| PRO-005 | S | pass | Claims, observations, evidence, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRO-006 | S | pass | Evidence shall include origin, time, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| PRO-008 | M | pass | Human and machine activities shall | ActivityTests.PRO_008_a_single_activity_model_serves_human_and_machine_actors; ActivityIntegrationTests.PRO_008 | platform | — |
| ART-001 | M | pass | Permitted binary artifacts shall be | ArtifactUseCaseTests.ART_001_upload_get_and_list_round_trip_a_single_version; ArtifactEvidenceIntegrationTests.ART_001_ART_003_ART_004_ART_006 | platform | — |
| ART-002 | M | pass | Artifact metadata shall include | ArtifactTests.ART_002_metadata_is_populated_and_typed; ArtifactEvidenceIntegrationTests.ART_002 | platform | — |
| ART-003 | M | pass | Artifact objects shall support | ArtifactObjectKeyTests; ArtifactUseCaseTests ART_003 fail-after-upload cleanup; ArtifactEvidenceIntegrationTests | platform | — |
| ART-004 | M | pass | Artifact upload/download shall be | ArtifactUseCaseTests.ART_004_download_issues_a_presigned_url_and_bar_quarantined_content; ArtifactEvidenceIntegrationTests.ART_001 | platform | — |
| ART-005 | S | pass | The system shall support external | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| ART-006 | S | pass | Stored artifacts shall have integrity | Artifact verify/download suites; ArchiveIntegrationTests EXP checksum integrity | platform | — |
| ART-008 | S | pass | Prohibited source-code content and | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| LFC-001 | S | pass | The system shall support | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| LFC-003 | M | pass | Lifecycle execution shall support | WorkPlanningIntegrationTests.LFC_003_005_project_members_can_plan_report_verify_and_approve_with_audited_states | platform | — |
| LFC-004 | M | pass | The system shall support work | WorkPlanningIntegrationTests.LFC_003_005; WorkItemTests.LFC_005_dependency_cannot_reference_the_same_work_item | platform | — |
| LFC-005 | M | pass | Planned state, reported execution | WorkItemTests.LFC_005_creation_separates_planned_reported_verified_and_approved_states; WorkPlanningIntegrationTests.LFC_003_005 | platform | — |
| LFC-006 | S | pass | Lifecycle transitions shall record | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-001 | M | pass | Each AI agent shall be represented | AgentUseCaseTests.AGT_001_register_agent_creates_a_machine_principal; AgentIntegrationTests.AGT_001_002_003_013 | platform | — |
| AGT-002 | M | pass | Agent credentials shall be | AgentUseCaseTests.AGT_002_issue_credential_hashes_and_returns_plaintext_once; AgentIntegrationTests.AGT_002 | platform | — |
| AGT-003 | M | pass | An authorized agent shall discover | AgentUseCaseTests.AGT_003_discovery_exposes_permitted_schemas_relation_types_and_tools | platform | — |
| AGT-004 | M | pass | The system shall generate task- | AgentUseCaseTests.AGT_004_assemble_denies_without_an_effective_grant_and_excludes_source_code; AgentIntegrationTests.AGT_004_010 | platform | — |
| AGT-005 | M | pass | The system shall record a context | AgentUseCaseTests.AGT_005_record_execution_produces_revision_pinned_provenance | platform | — |
| AGT-006 | M | pass | Agents shall submit proposed | AgentDomainTests.AGT_006_agent_submitted_knowledge_enters_proposed_pending_review_and_never_self_approves | platform | — |
| AGT-007 | S | pass | Agents shall record activities, | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| AGT-008 | S | pass | Beyond the mandatory | AgentIntegrationTests / AgentUseCaseTests optional capability paths | platform | — |
| AGT-009 | M | pass | Equivalent core agent capabilities | AgentIntegrationTests.AGT_009_core_agent_capabilities_are_exposed_over_rest | platform | — |
| AGT-010 | M | pass | Agent submissions shall support | AgentUseCaseTests.AGT_010_context_assembly_deduplicates_on_idempotency_key; AgentIntegrationTests.AGT_004_010 | platform | — |
| AGT-011 | S | pass | Stale agent revisions shall produce | Agent concurrency/stale revision conflict suites | platform | — |
| AGT-013 | S | pass | The system shall support human | See family suites under tests/; Studio pages under web/studio/src/pages/ | platform | — |
| GOV-001 | M | pass | Tenant isolation shall apply to | KnowledgeIsolationIntegrationTests; DatabaseLeastPrivilegeIntegrationTests; TenantMembershipAuthorizationMiddleware | platform | — |
| GOV-002 | M | pass | Project-level authorization shall be | ProjectIsolationIntegrationTests; TenantProjectAccess | platform | — |
| GOV-003 | M | pass | The system shall combine roles | ClassificationAccessTests; SearchKnowledge ceiling; TenantMembershipAuthorizationMiddleware | platform | — |
| GOV-004 | M | pass | The system shall support resource | KnowledgeIsolationIntegrationTests.GOV_004_member_cannot_read_restricted; ClassificationAccess | platform | — |
| GOV-005 | M | pass | Authorization shall be applied | ClassificationAccessTests; KnowledgeIsolationIntegrationTests.GOV_004; ResourcePolicyEngineTests | platform | — |
| GOV-007 | S | pass | The system shall audit security- | ManageSupportAccess audit Issue/Revoke; ReportGenerated audit; ExportStarted audit | platform | — |
| GOV-008 | S | pass | User, agent, integration and share | Membership/agent grant revoke suites; SupportAccess revoke negative tests | platform | — |
| GOV-011 | S | pass | Platform operators shall not | SupportAccessIntegrationTests (no standing access without grant) | platform | — |
| SRH-001 | M | pass | The system shall support full-text | KnowledgeIsolationIntegrationTests.SRH_001_002_003_search_is_project_scoped_filtered_and_classification_limited | platform | — |
| RPT-001 | M | pass | The system shall provide a project | ReportingIntegrationTests; ProjectReporting metrics; Studio ReportsPage | platform | — |
| RPT-002 | M | pass | The system shall distinguish task | ProjectReporting work.planned/reported/verified counts; ReportingIntegrationTests | platform | — |
| RPT-003 | M | pass | Each calculated metric shall have a | MetricView.DefinitionVersion + IncludedPopulation; ReportingIntegrationTests | platform | — |
| RPT-006 | C | pass | Estimates, predictions, unverified | MetricView.IsEstimate; work.completion_estimate; Studio Estimate label | platform | — |
| INT-001 | M | pass | The system shall provide an | IntegrationIntegrationTests.INT_001_004_integration_lifecycle_and_event_ingress_works_end_to_end | platform | — |
| INT-002 | M | pass | The system shall accept inbound | IntegrationIntegrationTests.INT_001_004_integration_lifecycle_and_event_ingress_works_end_to_end | platform | — |
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
