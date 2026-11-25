using System.Collections;
using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Lifecycle;
using DevWeave.Application.Planning;
using DevWeave.Application.Specifications;
using DevWeave.Contracts.Planning.V1;
using DevWeave.Domain.Specifications;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Schema;
using Microsoft.Extensions.Logging;

namespace DevWeave.Mcp.Protocol;

/// <summary>
/// Project-scoped MCP protocol adapter. Tools map to canonical coordination ports;
/// durable channel state remains authoritative (SRS §17, ARCH §10.2).
/// </summary>
public sealed class McpServer
{
    private readonly ICoordinationChannels _channels;
    private readonly IRuntimeRegistry _runtimeRegistry;
    private readonly ILeaseAuthority _leases;
    private readonly IPlanBaselines _baselines;
    private readonly IParticipationScheduler _scheduler;
    private readonly ICodebaseRegistry _codebases;
    private readonly IIntegrationQueue _integration;
    private readonly IWorkStatusReader _status;
    private readonly PlanDesignService _planning;
    private readonly PlanImportService _planImport;
    private readonly ListWorkItems _listWorkItems;
    private readonly TransitionWorkItem _transitionWorkItem;
    private readonly SpecificationService _specs;
    private readonly IAgentGrantRepository _grants;
    private readonly ICoordinationRepository _coordination;
    private readonly ISchemaDefinitionRepository _schemas;
    private readonly IKnowledgeTypeDefinitionRepository _knowledgeTypes;
    private readonly IRelationTypeRepository _relationTypes;
    private readonly IProjectReporting _reporting;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _options;

    public const string ProtocolVersion = "2026-07-28";

    public McpServer(
        ICoordinationChannels channels,
        IRuntimeRegistry runtimeRegistry,
        ILeaseAuthority leases,
        IPlanBaselines baselines,
        IParticipationScheduler scheduler,
        ICodebaseRegistry codebases,
        IIntegrationQueue integration,
        IWorkStatusReader status,
        PlanDesignService planning,
        PlanImportService planImport,
        ListWorkItems listWorkItems,
        TransitionWorkItem transitionWorkItem,
        SpecificationService specs,
        IAgentGrantRepository grants,
        ICoordinationRepository coordination,
        ISchemaDefinitionRepository schemas,
        IKnowledgeTypeDefinitionRepository knowledgeTypes,
        IRelationTypeRepository relationTypes,
        IProjectReporting reporting,
        ILogger<McpServer> logger)
    {
        _channels = channels;
        _runtimeRegistry = runtimeRegistry;
        _leases = leases;
        _baselines = baselines;
        _scheduler = scheduler;
        _codebases = codebases;
        _integration = integration;
        _status = status;
        _planning = planning;
        _planImport = planImport;
        _listWorkItems = listWorkItems;
        _transitionWorkItem = transitionWorkItem;
        _specs = specs;
        _grants = grants;
        _coordination = coordination;
        _schemas = schemas;
        _knowledgeTypes = knowledgeTypes;
        _relationTypes = relationTypes;
        _reporting = reporting;
        _logger = logger;
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
    }

    public async Task<JsonRpcResponse> HandleRequestAsync(
        AuthorizedScope scope,
        Guid runtimeId,
        JsonRpcRequest request,
        CancellationToken ct = default)
    {
        try
        {
            object? result = request.Method switch
            {
                McpMethods.Initialize => HandleInitialize(),
                McpMethods.ListTools => await HandleListToolsAsync(scope, ct),
                McpMethods.CallTool => await HandleCallToolAsync(scope, runtimeId, request, ct),
                McpMethods.ListResources => HandleListResources(),
                McpMethods.ReadResource => throw new NotFoundException("No MCP resources are published by this server."),
                McpMethods.ListPrompts => HandleListPrompts(),
                McpMethods.GetPrompt => throw new NotFoundException("No MCP prompts are published by this server."),
                McpMethods.SendMessage => await HandleSendMessageAsync(scope, runtimeId, request, ct),
                McpMethods.ReplayMessages => await HandleReplayAsync(scope, request, ct),
                _ => throw new InvalidOperationException($"Method not found: {request.Method}")
            };

            return new JsonRpcResponse("2.0", request.Id, result, null);
        }
        catch (Exception ex) when (DescribeError(ex) is { } known)
        {
            _logger.LogInformation("MCP request {Method} refused: {Kind}", request.Method, known.Data);
            return new JsonRpcResponse("2.0", request.Id, null, known);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP request {Method}", request.Method);
            return new JsonRpcResponse("2.0", request.Id, null, new JsonRpcError(-32603, ex.Message, null));
        }
    }

    private static object HandleInitialize() => new
    {
        protocolVersion = ProtocolVersion,
        capabilities = new
        {
            tools = new { listChanged = false },
            resources = new { subscribe = false, listChanged = false },
            prompts = new { listChanged = false }
        },
        serverInfo = new
        {
            name = "DevWeave-Coordination-Plane",
            version = "1.4.0"
        },
        // Agent-facing Foundation MVP constraints (TEN/GOV/ART/EXP). Keep tools/list descriptions aligned.
        instructions =
            "DevWeave project MCP is project-scoped and identity-bound: every request must carry a verified " +
            "tenant, project, principal, and runtime — never trust clientInfo for scope. " +
            "Knowledge and plan reads honor the caller's grant classification ceiling " +
            "(Owner/Admin humans reach Restricted; Member/Viewer humans and agents are capped by role or grant). " +
            "Confidential and Restricted objects are omitted, not errored, when above the ceiling. " +
            "Tool success acknowledges a durable control-plane commit only — not recipient delivery, human approval, " +
            "verification, or merge. Change sets are repository references only; never paste source code or secrets. " +
            "Export/import manifests include artifact checksums; mismatched checksums are rejected. " +
            "Production humans authenticate via OIDC; agents use issued credentials and active project grants. " +
            "Use a Planning agent (ReadKnowledge/WriteKnowledge) for propose_initial_plan and design tools; " +
            "use an Execution agent (ExecuteWorkflow) for leases and change sets. " +
            "Platform support access to tenant data requires a time-bounded TEN-009 grant (no standing access). " +
            "Empty-project Knowledge, Relations, outline, and Work are published only through propose_initial_plan " +
            "plus Owner approval in Studio Plan import. send_project_message is a Coordination log and never " +
            "materializes those tabs. Procedure: (1) read_plan_outline — if empty, import here; " +
            "(2) list_schemas and copy typeId plus schemaVersion from an active published schema — if schemas is " +
            "empty, stop and ask the Owner to register and publish schemas (agents cannot); " +
            "(3) list_relation_types when submitting relations — use an active code; sourceRef and targetRef must " +
            "be knowledge refs from this bundle, never file paths; " +
            "(4) propose_initial_plan with a unique idempotencyKey, requireEmptyProject true when the outline is " +
            "empty (false skips the empty check and still cannot add a second outline root), knowledge[], " +
            "outline[] with exactly one root (parentRef null), and optional codebaseTargets[] git identities " +
            "(provider, owner/name or remote URL, branch, commit SHA — never clones, blobs, diffs, or source); " +
            "(5) success returns status Proposed — ask the Owner to Approve and materialize. You cannot approve " +
            "your own proposal. Proposal status is only Proposed, Approved, or Rejected; there is no Done, and " +
            "work items in the bundle have no completion field. After approval, use propose_plan_expansion for incremental children and " +
            "register_codebase_target / list_codebase_targets for additional git pointers. Git hosting remains authoritative. " +
            "Planning agents update Work item status with list_work_items then report_work_item (reported/done) " +
            "or update_work_item_plan_state (planned). Verify and approve stay human-only in Studio. " +
            "You are one desktop agent (Cursor, Claude, or similar) among many, on any device, sharing one git " +
            "codebase. DevWeave is the control plane, not the IDE and not the git host. Pick one case: " +
            "C0 connect — register_runtime, list_codebase_targets, then get_plan_status or read_plan_outline; " +
            "C1 brainstorm and C3 empty plan — Planning only, propose_initial_plan, never send_project_message; " +
            "C3 existing build — the git repo already has the product: if read_plan_outline is empty, propose_initial_plan " +
            "with requireEmptyProject true and one work item per built, partial, or unstarted slice; do not send status Done. " +
            "A project has one outline root, so requireEmptyProject false does not attach another tree. " +
            "Change a requirement by a human revision; read_plan_outline keeps each work link on the previous revision until a human re-pins it. " +
            "There is no MCP tool to add a revision. propose_plan_expansion is refused until that new revision is human-reviewed, and it still only files a child proposal. " +
            "After the Owner approves, " +
            "list_work_items (new items are Planned / NotStarted / Unverified / Pending) and report_work_item done for a slice " +
            "already in git, InProgress for a partial slice, and leave NotStarted when work has not begun. " +
            "Send expectedVersion from that list; a stale version is -32009. Then get_project_metrics: " +
            "work.completion_estimate is reported-complete items divided by all work items, and work.reported_completed_count is the count. " +
            "closedloop.evidence_backed_completion stays 0 until a human verifies. Reported completion is not acceptance. " +
            "C2 specifications — propose_spec_* and wait until a human binds them; " +
            "C4 later design — propose_plan_expansion; " +
            "C5 plan moved — if get_work_status says canProgress false, stop coding, report_workthrough Blocked, release_lease; " +
            "C6 code — one execution agent, one live lease, one disjoint resourceScope, commit in git, never paste source here; " +
            "C7 test and C8 quality — run locally, claim in report_workthrough or report_work_item; you cannot verify or approve; " +
            "C9 handoff — release_lease, then send_project_message the SHA, paths, and remaining scope before another device continues; " +
            "C10 integrate — request_integration queues a reference, it does not merge; " +
            "C11 production — there is no deploy tool; deploy only after a human has verified and approved in Studio or the API. " +
            "C12 refused — -32009 conflict, -32004 not-found, -32003 forbidden, -32602 invalid, -32601 unknown tool; do not retry a conflict. " +
            "A second device must register its own runtime and must not heartbeat a lease held elsewhere."
    };

    // The compatibility metadata is deliberately explicit. It is harmless to standard MCP clients and
    // prevents strict 2026-07-28 client models from treating optional discovery metadata as absent.
    private static object HandleListResources() => new
    {
        resources = Array.Empty<object>(),
        cacheScope = "public",
        resultType = "complete",
        ttlMs = 0,
    };

    private static object HandleListPrompts() => new
    {
        prompts = Array.Empty<object>(),
        cacheScope = "public",
        resultType = "complete",
        ttlMs = 0,
    };

    private async Task<object> HandleListToolsAsync(AuthorizedScope scope, CancellationToken ct)
    {
        var capabilities = scope.ProjectId is null
            ? new HashSet<Domain.Agents.AgentCapability>()
            : await McpToolAuthorization.LoadActiveCapabilitiesAsync(
                _grants, scope.TenantId, scope.ProjectId, scope.ActorId, ct);

        var tools = AllTools()
            .Where(tool => McpToolAuthorization.IsAllowed(tool.Name, capabilities))
            .Select(tool => tool.Descriptor)
            .ToArray();
        return new { tools };
    }

    private static (string Name, object Descriptor)[] AllTools() =>
    [
        NamedTool("send_project_message", "Send a durable coordination log to a channel. Does not create Knowledge, Relations, Work, or a Plan import proposal. Empty-project design must use propose_initial_plan on a Planning agent instead.",
            new { channelId = UuidProp(), messageType = StringProp(), payload = new { type = "object" }, idempotencyKey = StringProp() },
            new[] { "channelId", "messageType", "payload", "idempotencyKey" }),
        NamedTool("read_messages_after_cursor", "Replay durable channel messages after a sequence cursor",
            new { channelId = UuidProp(), afterSequence = new { type = "integer" }, limit = new { type = "integer" } },
            new[] { "channelId" }),
        NamedTool("list_messages", "Alias of read_messages_after_cursor: list durable messages on a channel after a sequence cursor",
            new { channelId = UuidProp(), afterSequence = new { type = "integer" }, limit = new { type = "integer" } },
            new[] { "channelId" }),
        NamedTool("acknowledge_messages", "Acknowledge processed channel sequence for reconnect recovery",
            new { channelId = UuidProp(), processedThroughSequence = new { type = "integer" } },
            new[] { "channelId", "processedThroughSequence" }),
        NamedTool("open_channel", "Open a project coordination channel",
            new { channelType = StringProp(), campaignId = UuidProp(), workPackageId = UuidProp() },
            new[] { "channelType" }),
        NamedTool("list_channels", "List coordination channels in this project so execution clients can discover channel ids",
            new { }, Array.Empty<string>()),
        NamedTool("register_runtime", "Register this agent runtime instance for the project",
            new { runtimeClass = StringProp(), protocolVersion = StringProp(), capabilities = new { type = "array", items = new { type = "string" } }, attestationRef = StringProp() },
            new[] { "runtimeClass", "protocolVersion", "attestationRef" }),
        NamedTool("heartbeat_lease", "Renew an exclusive work-package lease fencing token",
            new { leaseId = UuidProp(), expectedFence = new { type = "integer" }, expiresAt = StringProp() },
            new[] { "leaseId", "expectedFence", "expiresAt" }),
        NamedTool("request_assignment", "Acquire an exclusive lease for one disjoint resourceScope. One desktop agent and one device hold it. Another agent or device must choose a different scope or wait. Do not code before this succeeds.",
            new { workPackageId = UuidProp(), resourceScope = StringProp(), expiresAt = StringProp(), idempotencyKey = StringProp() },
            new[] { "workPackageId", "resourceScope", "expiresAt", "idempotencyKey" }),
        NamedTool("get_plan_status", "Current plan version and every campaign with the plan version it runs on. Call at start-up and whenever a request is refused",
            new { }, Array.Empty<string>()),
        NamedTool("list_work_packages", "List a campaign's work packages with status, declared scope and whether each can still progress (and why not)",
            new { campaignId = UuidProp() }, new[] { "campaignId" }),
        NamedTool("get_work_status", "Check whether one work package can still progress, and the reason if it cannot (for example a newer plan version was locked)",
            new { workPackageId = UuidProp() }, new[] { "workPackageId" }),
        NamedTool("create_work_package", "Create a work package under a campaign's plan version. Declare what it implements (approved knowledge revisions from that version, work items, specification topics) so plan changes can be traced to it",
            new
            {
                campaignId = UuidProp(), baselineId = UuidProp(), objective = StringProp(),
                implementsRevisionIds = new { type = "array", items = UuidProp() },
                workItemIds = new { type = "array", items = UuidProp() },
                specTopics = new { type = "array", items = StringProp() },
            },
            new[] { "campaignId", "baselineId", "objective" }),
        NamedTool("release_lease", "Release a lease you hold. Always allowed, including after the plan changed",
            new { leaseId = UuidProp(), expectedFence = new { type = "integer" } },
            new[] { "leaseId", "expectedFence" }),
        NamedTool("submit_change_set", "Record a change set for an open work package as repository references only (providerRef, commit SHA, paths) — never source code or secrets. Subject to content policy",
            new
            {
                workPackageId = UuidProp(), codebaseTargetId = UuidProp(), providerRef = StringProp(),
                commitSha = StringProp(), changedPaths = new { type = "array", items = StringProp() },
            },
            new[] { "workPackageId", "codebaseTargetId", "providerRef", "commitSha" }),
        NamedTool("report_workthrough", "Case C7/C8 claim for a work package you leased: kind Succeeded (outcome + whatSucceeded) or Blocked (outcome + blockedReason). Use this for local test and quality results. Not verification, approval, merge, or production deploy. Allowed even when canProgress is false. Do not paste source, secrets, or secret-bearing logs",
            new
            {
                workPackageId = UuidProp(),
                kind = StringProp(),
                outcome = StringProp(),
                whatSucceeded = StringProp(),
                blockedReason = StringProp(),
                changeSetId = UuidProp(),
                steps = new { type = "array", items = StringProp() },
            },
            new[] { "workPackageId", "kind", "outcome" }),
        NamedTool("request_integration", "Queue a change set for integration. The decision re-checks the plan; work the plan has moved past is rejected, not merged. Ref-only change sets; no source payloads",
            new { changeSetId = UuidProp(), idempotencyKey = StringProp() },
            new[] { "changeSetId", "idempotencyKey" }),
        NamedTool("list_ready_work", "Deprecated alias of create_work_package: despite its name it creates a package. Use create_work_package",
            new
            {
                campaignId = UuidProp(), baselineId = UuidProp(), objective = StringProp(),
                implementsRevisionIds = new { type = "array", items = UuidProp() },
                workItemIds = new { type = "array", items = UuidProp() },
                specTopics = new { type = "array", items = StringProp() },
            },
            new[] { "campaignId", "baselineId", "objective" }),
        NamedTool("get_plan_baseline", "Describe a locked plan baseline as an export-style manifest (stable revision ids). Artifact checksum integrity is enforced on tenant export/import paths; do not invent checksums",
            new { baselineId = UuidProp() },
            new[] { "baselineId" }),
        NamedTool("read_plan_outline", "Read the published project outline, revision-pinned Work links, and your own proposals within your ReadKnowledge grant and classification ceiling. Items above the ceiling are omitted (not errored). A newer requirement revision does not move a work link; the link stays on the previous revision until a human re-pins it. If the outline is empty, submit design with propose_initial_plan — do not post it on Coordination.",
            new { }, Array.Empty<string>()),
        NamedTool("propose_plan_expansion", "Submit a child Knowledge idea under an already published parent for human review on Plan outline. Does not create or approve an item, and does not revise the parent. Refused while the parent's current revision is not human-reviewed. Use propose_initial_plan instead when the project outline is empty. Requires WriteKnowledge grant within your classification ceiling",
            new { parentItemId = UuidProp(), key = StringProp(), title = StringProp(), typeId = StringProp(), rationale = StringProp() },
            new[] { "parentItemId", "key", "title", "typeId", "rationale" }),
        NamedTool("propose_initial_plan",
            "Publish a Knowledge/outline/Relations/Work bundle for Owner approval. This is the only MCP path that can populate those Studio tabs. send_project_message never creates plan records. " +
            "Procedure: call read_plan_outline (empty tree => import here, including an existing git product DevWeave has not recorded); call list_schemas and copy typeId/schemaVersion (if schemas is empty, stop — the Owner must publish schemas); call list_relation_types for relationTypeCode if you include relations (endpoints must be knowledge refs, not file paths); call this tool; then tell the Owner to open Studio → Plan import → Approve and materialize. Success returns id with status Proposed. You cannot approve your own proposal. Proposal status is Proposed, Approved, or Rejected — there is no Done. Default requireEmptyProject is true. A project has one outline root, so false does not add another tree or edit an existing requirement. A changed requirement is a new human revision; this tool has no revision field, and work links stay on the previous revision. Work items have no status field; they materialize as Planned / NotStarted. After approval, record how much of an existing build is finished with report_work_item (done, InProgress, or NotStarted) and read work.completion_estimate from get_project_metrics. That estimate is not verification. " +
            "Each knowledge item needs ref, key, title, typeId, schemaVersion, classification (Public|Internal|Confidential|Restricted within your ceiling), representationKind (Text|StructuredData|Diagram|Chart|Document|Media|ExternalReference; never SourceCode), content, and reason. Outline needs exactly one root with parentRef null; every itemRef is a knowledge ref. Work priority is Low|Normal|High|Critical. workLinks.knowledgeRef must be in the outline. Optional codebaseTargets[] record git-host identity only (provider, repositoryId, targetBranch, baseCommitSha). Requires WriteKnowledge on a Planning agent.",
            new
            {
                idempotencyKey = StringProp("Stable client key. Repeating the same key with the same bundle returns the existing proposal."),
                requireEmptyProject = new
                {
                    type = "boolean",
                    description = "Default true, including an existing git product whose DevWeave outline is still empty. Refuse when outline nodes already exist. False skips that check and still cannot add a second outline root or change an existing requirement.",
                },
                knowledge = ArrayOf(
                    ObjectSchema(
                        new
                        {
                            @ref = StringProp("Client-local id unique in this bundle (max 100 characters). Referenced by outline, relations, and workLinks."),
                            key = StringProp("Stable knowledge key unique in the project."),
                            title = StringProp(),
                            typeId = StringProp("Must match list_schemas.typeId (schema namespace). Do not invent values."),
                            schemaVersion = StringProp("Must match list_schemas.schemaVersion."),
                            classification = StringProp("Public, Internal, Confidential, or Restricted. Must not exceed the agent grant ceiling."),
                            representationKind = StringProp("Text, StructuredData, Diagram, Chart, Document, Media, or ExternalReference. SourceCode is refused."),
                            content = StringProp("Item body. Not source code or secrets. Max 100000 characters."),
                            reason = StringProp("Why this item belongs in the initial plan."),
                            description = StringProp("Optional longer summary."),
                        },
                        ["ref", "key", "title", "typeId", "schemaVersion", "classification", "representationKind", "content", "reason"]),
                    "At least one knowledge draft; at most 100."),
                outline = ArrayOf(
                    ObjectSchema(
                        new
                        {
                            itemRef = StringProp("Knowledge ref from this bundle."),
                            parentRef = StringProp("Knowledge ref of the parent, or omit/null for the single root."),
                            position = new { type = "integer", description = "Nonnegative sibling order." },
                        },
                        ["itemRef", "position"]),
                    "Connected tree with exactly one root (parentRef null). At most 100 nodes."),
                relations = ArrayOf(
                    ObjectSchema(
                        new
                        {
                            sourceRef = StringProp("Knowledge ref from this bundle, not a file path."),
                            targetRef = StringProp("Knowledge ref from this bundle, not a file path."),
                            relationTypeCode = StringProp("Active code from list_relation_types."),
                        },
                        ["sourceRef", "targetRef", "relationTypeCode"]),
                    "Optional. At most 200. Both endpoints must be knowledge refs."),
                milestones = ArrayOf(
                    ObjectSchema(
                        new
                        {
                            @ref = StringProp("Client-local milestone id unique in this bundle."),
                            key = StringProp(),
                            title = StringProp(),
                            dueAt = StringProp("Optional ISO-8601 due date."),
                        },
                        ["ref", "key", "title"]),
                    "Optional. At most 50."),
                workItems = ArrayOf(
                    ObjectSchema(
                        new
                        {
                            @ref = StringProp("Client-local work id unique in this bundle."),
                            key = StringProp(),
                            title = StringProp(),
                            description = StringProp(),
                            milestoneRef = StringProp("Optional milestone ref from this bundle."),
                            priority = StringProp("Low, Normal, High, or Critical."),
                            dependsOnRefs = new { type = "array", items = StringProp("Other work item refs in this bundle. Must be acyclic.") },
                        },
                        ["ref", "key", "title", "description", "priority"]),
                    "Optional. At most 200. No status or Done field: completion is reported after Owner approval with report_work_item."),
                workLinks = ArrayOf(
                    ObjectSchema(
                        new
                        {
                            workItemRef = StringProp("Work item ref from this bundle."),
                            knowledgeRef = StringProp("Knowledge ref that also appears in outline."),
                        },
                        ["workItemRef", "knowledgeRef"]),
                    "Optional links from work items to outline knowledge."),
                codebaseTargets = ArrayOf(
                    ObjectSchema(
                        new
                        {
                            @ref = StringProp("Client-local id unique in this bundle."),
                            provider = StringProp("Git host, for example github."),
                            repositoryId = StringProp("owner/name or a clean remote URL. No credentials, local paths, clones, or file contents."),
                            targetBranch = StringProp("Protected or default branch name."),
                            baseCommitSha = StringProp("7-40 character hex SHA currently observed."),
                            reason = StringProp("Optional note for the Owner."),
                        },
                        ["ref", "provider", "repositoryId", "targetBranch", "baseCommitSha"]),
                    "Optional git-host pointers. At most 10. Never source."),
            },
            ["idempotencyKey", "knowledge", "outline"]),
        NamedTool("list_codebase_targets", "List registered git repository pointers for this project (provider, repositoryId, branch, SHA, canonical ref, browse URL). References only — DevWeave never stores clones or source. Planning and execution agents may call this.",
            new { }, Array.Empty<string>()),
        NamedTool("register_codebase_target", "Record a git repository identity as a references-only codebase target. Accepts owner/name or a clean GitHub remote URL, plus branch and commit SHA. Never send clones, blobs, diffs, or source. Does not start a campaign. Idempotent for the same provider/repo/branch/SHA. Requires WriteKnowledge.",
            new
            {
                provider = StringProp("Git host, for example github."),
                repositoryId = StringProp("owner/name or https://github.com/owner/name.git — no credentials."),
                targetBranch = StringProp(),
                baseCommitSha = StringProp("7-40 character hex SHA."),
            },
            ["provider", "repositoryId", "targetBranch", "baseCommitSha"]),
        NamedTool("list_schemas", "List active published Knowledge schemas plus catalogue metadata (isCanonical, storageKind). Prefer canonical typeIds such as requirements.functional / requirements.nonfunctional for new plans; legacy plan.* schemas remain valid. Call before propose_initial_plan and copy typeId (namespace) and schemaVersion. Aggregate-mapped catalogue concepts (planning.work-item, execution.activity-report, verification.result) are not Knowledge schemas.",
            new { }, Array.Empty<string>()),
        NamedTool("list_relation_types", "List active relation types for plan-import relations. Call before propose_initial_plan if you include relations. Use code as relationTypeCode. sourceRef and targetRef must be knowledge refs from the same bundle, not file paths.",
            new { }, Array.Empty<string>()),
        NamedTool("get_project_metrics", "Read authorized project metrics, including closed-loop verification, traceability, and evidence-backed completion. Each dimension is separate. For an existing build, work.completion_estimate is reported-complete work items divided by all work items, and work.reported_completed_count is that numerator. closedloop.evidence_backed_completion counts only items a human has verified. Reported completion is an estimate and is not verification. Read-only. Requires ReadKnowledge.",
            new { }, Array.Empty<string>()),
        NamedTool("list_work_items", "List this project's Work items with independent planned, reported, verification, and approval states. Use after Owner Plan import. A newly imported item is Planned / NotStarted / Unverified / Pending even when the git slice already exists; reportedState becomes Completed only after report_work_item done. ReadKnowledge. Verify and approve are not MCP tools — humans do those in Studio.",
            new { }, Array.Empty<string>()),
        NamedTool("report_work_item", "Planning agent claim of Work item progress (reported state). Does not verify or approve. After an existing build is imported, call this once per work item: done when that slice already exists in git, InProgress when it is partial, and omit the call to leave NotStarted. Pass workItemId or key from list_work_items. state: NotStarted, InProgress, Blocked, Completed, Failed; aliases done=Completed and not-done=reopen or leave unfinished. There is no Done value. Legal jumps: NotStarted→InProgress|Blocked|Completed|Failed; InProgress/Blocked→Blocked|Completed|Failed|InProgress; Completed/Failed→InProgress only. Optional expectedVersion (defaults to current). Requires WriteKnowledge. Then read work.completion_estimate. Humans still verify and approve in Studio.",
            new
            {
                workItemId = UuidProp(),
                key = StringProp("Work item key from list_work_items or plan import."),
                state = StringProp("ReportedExecutionState or done / not-done."),
                reason = StringProp("Why this report is true. References only — no source or secrets."),
                expectedVersion = new { type = "integer", description = "CAS version from list_work_items. Defaults to the current version. A mismatch is -32009 conflict." },
            },
            ["state", "reason"]),
        NamedTool("update_work_item_plan_state", "Planning agent update of Work item planned state (backlog/workflow, not execution claim). Pass workItemId or key. state: Planned, Ready, InProgress, Blocked, Cancelled. Legal jumps: Planned→Ready|Cancelled; Ready→Planned|InProgress|Cancelled; InProgress→Blocked|Cancelled; Blocked→InProgress|Cancelled; Cancelled→Planned. Optional expectedVersion. Requires WriteKnowledge. Verify and approve stay human-only.",
            new
            {
                workItemId = UuidProp(),
                key = StringProp("Work item key from list_work_items or plan import."),
                state = StringProp("PlannedWorkState."),
                reason = StringProp("Why the planned state is changing."),
                expectedVersion = new { type = "integer", description = "CAS version from list_work_items. Defaults to the current version. A mismatch is -32009 conflict." },
            },
            ["state", "reason"]),
        NamedTool("resolve_spec", "Resolve the single binding specification decision for a topic in this project, as data. Returns origin, revision ids and any conflicts. Honors ReadKnowledge grant",
            new { topic = StringProp() }, new[] { "topic" }),
        NamedTool("get_effective_spec", "Read every binding specification decision for this project, plus undecided topics and incompatibilities. Honors ReadKnowledge grant",
            new { }, Array.Empty<string>()),
        NamedTool("list_spec_topics", "List this project's specification topics (built-in packs plus approved custom ones) with legal choices and typed parameters",
            new { }, Array.Empty<string>()),
        NamedTool("get_spec_catalog", "Read the effective specification catalog: packs, topics, profiles and any overlay problems. Honors ReadKnowledge grant",
            new { }, Array.Empty<string>()),
        NamedTool("list_spec_options", "List ranked options for a topic, marking which are recommended and which are compatible with decisions that already bind. Choose from this list",
            new { topic = StringProp() }, new[] { "topic" }),
        NamedTool("next_open_topics", "List topics that still need a decision, in the order they should be decided",
            new { }, Array.Empty<string>()),
        NamedTool("get_spec_pack_upgrades", "List available upgrades for built-in packs this project is held back on, with a diff and affected decisions",
            new { }, Array.Empty<string>()),
        NamedTool("propose_spec_profile", "Propose a whole profile (or a custom list of decisions) as one bundle for a single human approval. Each decision is validated against its topic. Requires WriteKnowledge grant",
            new { profile = StringProp(), level = StringProp(), decisions = new { type = "array", items = new { type = "object" } }, rationale = StringProp() },
            new[] { "level" }),
        NamedTool("propose_spec_overlay", "Propose a catalog extension (custom topics, options, profiles, hidden or deprecated options). Needs human approval before it applies. Requires WriteKnowledge grant",
            new { overlay = new { type = "object" }, rationale = StringProp() }, new[] { "overlay" }),
        NamedTool("propose_spec_decision", "Propose a specification decision for a topic that has no binding decision. It is validated against the topic definition and needs human approval before it binds. Requires WriteKnowledge grant",
            new { topic = StringProp(), level = StringProp(), choice = StringProp(), @params = new { type = "object" }, rationale = StringProp() },
            new[] { "topic", "level", "choice" }),
    ];

    private async Task<object> HandleCallToolAsync(
        AuthorizedScope scope,
        Guid runtimeId,
        JsonRpcRequest request,
        CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(request.Params?.ToString() ?? "{}");
        var root = doc.RootElement;
        if (!root.TryGetProperty("name", out var nameEl) || nameEl.GetString() is not { Length: > 0 } name)
            throw new ArgumentException("tools/call requires name.");
        var args = root.TryGetProperty("arguments", out var rawArgs) ? rawArgs : default;

        // Arguments are always a JSON object (or absent). Anything else is a client bug and is refused
        // with a clear message instead of being guessed at.
        if (args.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined or JsonValueKind.Null))
            throw new ArgumentException(
                "tools/call 'arguments' must be a JSON object, for example {\"workPackageId\": \"...\"}. " +
                "Arrays and other shapes are not accepted.");

        if (scope.ProjectId is null)
            throw new UnauthorizedTenantException("MCP tools require a project scope.");
        await McpToolAuthorization.EnsureCanCallAsync(
            _grants, scope.TenantId, scope.ProjectId, scope.ActorId, name, ct);

        object payload = name switch
        {
            "send_project_message" or "send_message" => await SendFromArgsAsync(scope, runtimeId, args, ct),
            "read_messages_after_cursor" or "list_messages" => await ReplayFromArgsAsync(scope, args, ct),
            "acknowledge_messages" => await AckFromArgsAsync(scope, args, ct),
            "open_channel" => await OpenChannelFromArgsAsync(scope, args, ct),
            "list_channels" => await ListChannelsAsync(scope, ct),
            "register_runtime" => await RegisterRuntimeFromArgsAsync(scope, args, ct),
            "heartbeat_lease" => await RenewLeaseFromArgsAsync(scope, runtimeId, args, ct),
            "request_assignment" => await AcquireLeaseFromArgsAsync(scope, runtimeId, args, ct),
            "list_ready_work" or "create_work_package" => await CreatePackageFromArgsAsync(scope, args, ct),
            "get_plan_status" => await _status.GetPlanStatusAsync(scope, ct),
            "list_work_packages" => new
            {
                workPackages = await _status.ListWorkPackagesAsync(
                    scope, args.GetProperty("campaignId").GetGuid(), ct),
            },
            "get_work_status" => await _status.GetWorkStatusAsync(scope, args.GetProperty("workPackageId").GetGuid(), ct),
            "release_lease" => await ReleaseLeaseFromArgsAsync(scope, runtimeId, args, ct),
            "submit_change_set" => await SubmitChangeSetFromArgsAsync(scope, args, ct),
            "report_workthrough" => await ReportWorkthroughFromArgsAsync(scope, args, ct),
            "request_integration" => new
            {
                queueItemId = await _integration.EnqueueAsync(scope, args.GetProperty("changeSetId").GetGuid(),
                    args.GetProperty("idempotencyKey").GetString()!, ct),
            },
            "get_plan_baseline" => await DescribeBaselineFromArgsAsync(scope, args, ct),
            "read_plan_outline" => PlanDesignProjection.Outline(
                await _planning.ReadAsync(scope.TenantId, scope.ProjectId!, scope.ActorId, ct)),
            "propose_plan_expansion" => PlanDesignProjection.Proposal(
                await _planning.ProposeAsync(scope.TenantId, scope.ProjectId!,
                scope.ActorId, new KnowledgeItemId(args.GetProperty("parentItemId").GetGuid()),
                args.GetProperty("key").GetString()!, args.GetProperty("title").GetString()!,
                args.GetProperty("typeId").GetString()!, args.GetProperty("rationale").GetString()!, ct)),
            "propose_initial_plan" => await ProposeInitialPlanFromArgsAsync(scope, args, ct),
            "list_codebase_targets" => await ListCodebaseTargetsFromArgsAsync(scope, ct),
            "register_codebase_target" => await RegisterCodebaseFromArgsAsync(scope, args, ct),
            "list_schemas" => await ListSchemasAsync(ct),
            "list_relation_types" => await ListRelationTypesAsync(ct),
            "list_work_items" => await ListWorkItemsFromArgsAsync(scope, ct),
            "get_project_metrics" => new
            {
                metrics = await _reporting.GetMetricsAsync(scope, DateTimeOffset.UtcNow, ct),
            },
            "report_work_item" => await ReportWorkItemFromArgsAsync(scope, args, ct),
            "update_work_item_plan_state" => await UpdateWorkItemPlanStateFromArgsAsync(scope, args, ct),
            "resolve_spec" => SpecProjection.Resolution(await _specs.ResolveAsync(
                scope.TenantId, scope.ProjectId!, scope.ActorId, args.GetProperty("topic").GetString()!, ct)),
            "get_effective_spec" => SpecProjection.Effective(await _specs.EffectiveAsync(
                scope.TenantId, scope.ProjectId!, scope.ActorId, ct)),
            "list_spec_topics" => new
            {
                topics = (await _specs.CatalogAsync(scope.TenantId, scope.ProjectId!, scope.ActorId, ct))
                    .Topics.Select(SpecProjection.Topic).ToArray(),
            },
            "get_spec_catalog" => SpecProjection.Catalog(await _specs.CatalogAsync(
                scope.TenantId, scope.ProjectId!, scope.ActorId, ct)),
            "list_spec_options" => SpecProjection.Options(await _specs.OptionsAsync(
                scope.TenantId, scope.ProjectId!, scope.ActorId, args.GetProperty("topic").GetString()!, ct)),
            "next_open_topics" => new
            {
                topics = SpecProjection.OpenTopics(await _specs.OpenTopicsAsync(
                    scope.TenantId, scope.ProjectId!, scope.ActorId, ct)),
            },
            "propose_spec_overlay" => await ProposeOverlayFromArgsAsync(scope, args, ct),
            "propose_spec_profile" => await ProposeBundleFromArgsAsync(scope, args, ct),
            "get_spec_pack_upgrades" => new
            {
                upgrades = SpecProjection.Upgrades(await _specs.UpgradesAsync(
                    scope.TenantId, scope.ProjectId!, scope.ActorId, ct)),
            },
            "propose_spec_decision" => await ProposeSpecFromArgsAsync(scope, args, ct),
            _ => throw new InvalidOperationException($"Unknown tool: {name}")
        };
        return ToCallToolResult(payload);
    }

    private async Task<object> ListChannelsAsync(AuthorizedScope scope, CancellationToken ct)
    {
        var channels = await _coordination.ListChannelsByProjectAsync(scope.TenantId, scope.ProjectId!, ct);
        return new
        {
            channels = channels
                .OrderByDescending(channel => channel.CreatedAt)
                .Select(channel => new
                {
                    id = channel.Id.Value,
                    type = channel.ChannelType,
                    campaignId = channel.CampaignId?.Value,
                    workPackageId = channel.WorkPackageId?.Value,
                    channelType = channel.ChannelType,
                    visibility = channel.Visibility,
                    sequenceHighWatermark = channel.SequenceHighWatermark,
                    createdAt = channel.CreatedAt,
                })
                .ToArray(),
        };
    }

    private async Task<object> ListSchemasAsync(CancellationToken ct)
    {
        var schemas = await _schemas.ListAsync(ct);
        var types = (await _knowledgeTypes.ListAsync(ct))
            .ToDictionary(type => type.TypeId, StringComparer.Ordinal);
        return new
        {
            schemas = schemas
                .Where(schema => schema.IsActive)
                .OrderBy(schema => schema.Namespace, StringComparer.Ordinal)
                .ThenBy(schema => schema.Version, StringComparer.Ordinal)
                .Select(schema =>
                {
                    types.TryGetValue(schema.Namespace, out var type);
                    return new
                    {
                        id = schema.Id.Value,
                        typeId = schema.Namespace,
                        schemaVersion = schema.Version,
                        schema.Description,
                        isCanonical = type?.IsCanonical ?? false,
                        storageKind = type?.StorageKind.ToString() ?? KnowledgeStorageKind.KnowledgeEnvelope.ToString(),
                    };
                })
                .ToArray(),
            knowledgeTypes = types.Values
                .OrderByDescending(type => type.IsCanonical)
                .ThenBy(type => type.TypeId, StringComparer.Ordinal)
                .Select(type => new
                {
                    typeId = type.TypeId,
                    type.Description,
                    storageKind = type.StorageKind.ToString(),
                    type.IsCanonical,
                    defaultClassification = type.DefaultClassification.ToString(),
                })
                .ToArray(),
        };
    }

    private async Task<object> ListRelationTypesAsync(CancellationToken ct)
    {
        var types = await _relationTypes.ListActiveAsync(ct);
        return new
        {
            relationTypes = types
                .OrderBy(type => type.Code, StringComparer.Ordinal)
                .Select(type => new
                {
                    id = type.Id.Value,
                    type.Code,
                    type.Name,
                    allowedSourceTypeIds = type.AllowedSourceTypeIds,
                    allowedTargetTypeIds = type.AllowedTargetTypeIds,
                    direction = type.Direction.ToString(),
                    cardinality = type.Cardinality.ToString(),
                })
                .ToArray(),
        };
    }

    private async Task<object> ProposeInitialPlanFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<InitialPlanProposalV1>(args.GetRawText(), _options)
            ?? throw new ArgumentException("propose_initial_plan arguments must be a plan bundle object.");
        payload = payload with
        {
            RequireEmptyProject = args.TryGetProperty("requireEmptyProject", out _)
                ? payload.RequireEmptyProject
                : true,
            Knowledge = payload.Knowledge ?? Array.Empty<PlanImportKnowledgeDraftV1>(),
            Outline = payload.Outline ?? Array.Empty<PlanImportOutlineDraftV1>(),
            Relations = payload.Relations ?? Array.Empty<PlanImportRelationDraftV1>(),
            Milestones = payload.Milestones ?? Array.Empty<PlanImportMilestoneDraftV1>(),
            WorkItems = payload.WorkItems ?? Array.Empty<PlanImportWorkItemDraftV1>(),
            WorkLinks = payload.WorkLinks ?? Array.Empty<PlanImportWorkLinkDraftV1>(),
            CodebaseTargets = payload.CodebaseTargets ?? Array.Empty<PlanImportCodebaseTargetDraftV1>(),
        };
        var proposal = await _planImport.ProposeAsync(scope.TenantId, scope.ProjectId!, scope.ActorId, payload, ct);
        return new
        {
            id = proposal.Id.Value,
            proposal.IdempotencyKey,
            proposal.PayloadHash,
            status = proposal.Status.ToString(),
            proposal.RequireEmptyProject,
            proposedAt = proposal.ProposedAt,
        };
    }

    private async Task<object> ListCodebaseTargetsFromArgsAsync(AuthorizedScope scope, CancellationToken ct)
    {
        var targets = await _codebases.ListAsync(scope, ct);
        return new { codebaseTargets = targets };
    }

    private async Task<object> RegisterCodebaseFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var id = await _codebases.RegisterAsync(
            scope,
            args.GetProperty("provider").GetString()!,
            args.GetProperty("repositoryId").GetString()!,
            args.GetProperty("targetBranch").GetString()!,
            args.GetProperty("baseCommitSha").GetString()!,
            ct);
        var listed = await _codebases.ListAsync(scope, ct);
        var target = listed.First(t => t.Id == id);
        return target;
    }

    private async Task<object> ProposeBundleFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        if (!Enum.TryParse<SpecScopeLevel>(args.GetProperty("level").GetString(), ignoreCase: true, out var level))
            throw new ArgumentException("level must be tenant or project.");

        var profile = args.TryGetProperty("profile", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        var lines = args.TryGetProperty("decisions", out var d) && d.ValueKind == JsonValueKind.Array
            ? SpecCatalogJson.ReadDecisions(args)
            : null;
        var item = await _specs.ProposeBundleAsync(
            scope.TenantId, scope.ProjectId!, scope.ActorId, profile, level, lines,
            args.TryGetProperty("rationale", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString()! : "Proposed by agent.", ct);
        return new { itemId = item.Id.Value, item.Key, state = item.State.ToString() };
    }

    private async Task<object> ProposeOverlayFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var item = await _specs.ProposeOverlayAsync(
            scope.TenantId, scope.ProjectId!, scope.ActorId, args.GetProperty("overlay").GetRawText(),
            args.TryGetProperty("rationale", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString()! : "Proposed by agent.", ct);
        return new { itemId = item.Id.Value, item.Key, state = item.State.ToString() };
    }

    private async Task<object> ProposeSpecFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var topic = args.GetProperty("topic").GetString()!;
        var levelText = args.GetProperty("level").GetString();
        if (!Enum.TryParse<SpecScopeLevel>(levelText, ignoreCase: true, out var level))
            throw new ArgumentException("level must be tenant or project.");

        var decision = new Dictionary<string, object?>
        {
            ["topic"] = topic,
            ["scope"] = new { level = SpecDecisionJson.ToLevelString(level) },
            ["decision"] = new
            {
                choice = args.GetProperty("choice").GetString(),
                @params = args.TryGetProperty("params", out var p) ? JsonSerializer.Deserialize<JsonElement>(p.GetRawText()) : default(JsonElement?),
            },
        };
        if (args.TryGetProperty("rationale", out var rationale) && rationale.ValueKind == JsonValueKind.String)
            decision["rationale"] = rationale.GetString();

        var item = await _specs.ProposeAsync(
            scope.TenantId, scope.ProjectId!, scope.ActorId, topic, level,
            JsonSerializer.Serialize(decision), "Proposed by agent.", ct);
        return new { itemId = item.Id.Value, item.Key, state = item.State.ToString() };
    }

    private async Task<object> HandleSendMessageAsync(
        AuthorizedScope scope, Guid runtimeId, JsonRpcRequest request, CancellationToken ct)
    {
        var draft = JsonSerializer.Deserialize<MessageDraft>(request.Params?.ToString() ?? "{}", _options)
            ?? throw new ArgumentException("Invalid message draft.");
        return await _channels.SendAsync(scope, runtimeId, draft, ct);
    }

    private async Task<object> HandleReplayAsync(AuthorizedScope scope, JsonRpcRequest request, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(request.Params?.ToString() ?? "{}");
        var channelId = doc.RootElement.GetProperty("channelId").GetGuid();
        var after = doc.RootElement.TryGetProperty("afterSequence", out var afterEl) ? afterEl.GetInt64() : 0L;
        var limit = doc.RootElement.TryGetProperty("limit", out var limitEl) ? limitEl.GetInt32() : 50;
        return MessagePage(await _channels.ReplayAsync(scope, channelId, after, limit, ct));
    }

    private async Task<object> SendFromArgsAsync(AuthorizedScope scope, Guid runtimeId, JsonElement args, CancellationToken ct)
    {
        var draft = new MessageDraft(
            args.GetProperty("channelId").GetGuid(),
            args.GetProperty("messageType").GetString()!,
            "1.0",
            args.TryGetProperty("payload", out var payload) ? payload.GetRawText() : "{}",
            Array.Empty<ResourceRef>(),
            null,
            args.GetProperty("idempotencyKey").GetString()!);
        return await _channels.SendAsync(scope, runtimeId, draft, ct);
    }

    private async Task<object> ReplayFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var channelId = args.GetProperty("channelId").GetGuid();
        var after = args.TryGetProperty("afterSequence", out var afterEl) ? afterEl.GetInt64() : 0L;
        var limit = args.TryGetProperty("limit", out var limitEl) ? limitEl.GetInt32() : 50;
        return MessagePage(await _channels.ReplayAsync(scope, channelId, after, Math.Clamp(limit, 1, 200), ct));
    }

    private static object MessagePage(Page<MessageView> page) => new
    {
        messages = page.Items,
        items = page.Items,
        nextCursor = page.NextCursor,
    };

    private async Task<object> AckFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        await _channels.AcknowledgeAsync(
            scope,
            args.GetProperty("channelId").GetGuid(),
            args.GetProperty("processedThroughSequence").GetInt64(),
            ct);
        return new { acknowledged = true };
    }

    private async Task<object> OpenChannelFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var channelType = args.GetProperty("channelType").GetString()!;
        Guid? campaignId = args.TryGetProperty("campaignId", out var c) && c.ValueKind != JsonValueKind.Null ? c.GetGuid() : null;
        Guid? workPackageId = args.TryGetProperty("workPackageId", out var w) && w.ValueKind != JsonValueKind.Null ? w.GetGuid() : null;
        var id = await _channels.OpenAsync(scope, channelType, campaignId, workPackageId, ct);
        return new { id };
    }

    private async Task<object> RegisterRuntimeFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var capabilities = new List<string>();
        if (args.TryGetProperty("capabilities", out var caps) && caps.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in caps.EnumerateArray())
                capabilities.Add(item.GetString() ?? string.Empty);
        }

        var registration = new RuntimeRegistration(
            scope.ActorId.Value,
            args.GetProperty("runtimeClass").GetString()!,
            args.GetProperty("protocolVersion").GetString()!,
            capabilities,
            args.GetProperty("attestationRef").GetString()!);
        var id = await _runtimeRegistry.RegisterAsync(scope, registration, ct);
        return new { id };
    }

    private async Task<object> ReleaseLeaseFromArgsAsync(
        AuthorizedScope scope, Guid runtimeId, JsonElement args, CancellationToken ct)
    {
        await _leases.ReleaseAsync(
            scope,
            args.GetProperty("leaseId").GetGuid(),
            args.GetProperty("expectedFence").GetInt64(),
            runtimeId,
            ct);
        return new { released = true };
    }

    private async Task<object> SubmitChangeSetFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var paths = args.TryGetProperty("changedPaths", out var p) && p.ValueKind == JsonValueKind.Array
            ? p.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
            : Array.Empty<string>();
        var id = await _codebases.RecordChangeSetAsync(scope, new ChangeSetDraft(
            args.GetProperty("workPackageId").GetGuid(),
            args.GetProperty("codebaseTargetId").GetGuid(),
            args.GetProperty("providerRef").GetString()!,
            args.GetProperty("commitSha").GetString()!,
            paths), ct);
        return new { changeSetId = id };
    }

    private async Task<object> ReportWorkthroughFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        if (args.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("report_workthrough arguments must be a JSON object.");

        Guid? changeSetId = args.TryGetProperty("changeSetId", out var cs) && cs.ValueKind == JsonValueKind.String
            ? cs.GetGuid()
            : null;
        var steps = args.TryGetProperty("steps", out var s) && s.ValueKind == JsonValueKind.Array
            ? s.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
            : null;
        var whatSucceeded = args.TryGetProperty("whatSucceeded", out var ws) && ws.ValueKind == JsonValueKind.String
            ? ws.GetString()
            : null;
        var blockedReason = args.TryGetProperty("blockedReason", out var br) && br.ValueKind == JsonValueKind.String
            ? br.GetString()
            : null;
        return await _status.ReportWorkthroughAsync(
            scope,
            args.GetProperty("workPackageId").GetGuid(),
            args.GetProperty("kind").GetString()!,
            args.GetProperty("outcome").GetString()!,
            whatSucceeded,
            blockedReason,
            changeSetId,
            steps,
            ct);
    }

    /// <summary>
    /// Maps expected refusals to stable JSON-RPC codes so agents can react without parsing text:
    /// -32004 not found, -32009 conflict (for example the plan moved on), -32003 forbidden,
    /// -32602 invalid arguments (with issues when available).
    /// </summary>
    private static JsonRpcError? DescribeError(Exception ex) => ex switch
    {
        NotFoundException => new JsonRpcError(-32004, ex.Message, new { kind = "not-found" }),
        ConflictException => new JsonRpcError(-32009, ex.Message, new { kind = "conflict" }),
        WorkItemConcurrencyException => new JsonRpcError(-32009, ex.Message, new { kind = "conflict" }),
        UnauthorizedTenantException => new JsonRpcError(-32003, ex.Message, new { kind = "forbidden" }),
        SpecDecisionRejectedException rejected => new JsonRpcError(-32602, ex.Message,
            new { kind = "invalid", issues = rejected.Issues.Select(i => new { i.Path, i.Message }) }),
        PayloadValidationException => new JsonRpcError(-32602, ex.Message, new { kind = "invalid" }),
        ProhibitedContentException => new JsonRpcError(-32602, ex.Message, new { kind = "prohibited-content" }),
        InvalidOperationException when ex.Message.StartsWith("Unknown tool", StringComparison.Ordinal)
            => new JsonRpcError(-32601, ex.Message, new { kind = "unknown-tool" }),
        InvalidOperationException when ex.Message.StartsWith("Method not found", StringComparison.Ordinal)
            => new JsonRpcError(-32601, ex.Message, new { kind = "method-not-found" }),
        InvalidOperationException => new JsonRpcError(-32009, ex.Message, new { kind = "conflict" }),
        ArgumentException or KeyNotFoundException or FormatException => new JsonRpcError(-32602, ex.Message, new { kind = "invalid" }),
        _ => null,
    };

    private async Task<object> RenewLeaseFromArgsAsync(
        AuthorizedScope scope, Guid runtimeId, JsonElement args, CancellationToken ct)
    {
        var leaseId = args.GetProperty("leaseId").GetGuid();
        var fence = args.GetProperty("expectedFence").GetInt64();
        var expiresAt = DateTimeOffset.Parse(args.GetProperty("expiresAt").GetString()!);
        return await _leases.RenewAsync(scope, leaseId, fence, expiresAt, runtimeId, ct);
    }

    private async Task<object> AcquireLeaseFromArgsAsync(AuthorizedScope scope, Guid runtimeId, JsonElement args, CancellationToken ct)
    {
        var request = new LeaseRequest(
            args.GetProperty("workPackageId").GetGuid(),
            runtimeId,
            args.GetProperty("resourceScope").GetString()!,
            DateTimeOffset.Parse(args.GetProperty("expiresAt").GetString()!),
            0,
            args.GetProperty("idempotencyKey").GetString()!);
        return await _leases.AcquireAsync(scope, request, ct);
    }

    private async Task<object> CreatePackageFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var draft = new PackageDraft(
            args.GetProperty("campaignId").GetGuid(),
            args.GetProperty("baselineId").GetGuid(),
            args.GetProperty("objective").GetString()!,
            Array.Empty<Guid>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            GuidArray(args, "implementsRevisionIds"),
            GuidArray(args, "workItemIds"),
            args.TryGetProperty("specTopics", out var topics) && topics.ValueKind == JsonValueKind.Array
                ? topics.EnumerateArray().Select(t => t.GetString() ?? string.Empty).ToArray()
                : Array.Empty<string>());
        var id = await _scheduler.CreatePackageAsync(scope, draft, ct);
        return new { id };
    }

    private static Guid[] GuidArray(JsonElement args, string name) =>
        args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(v => v.GetGuid()).ToArray()
            : Array.Empty<Guid>();

    private async Task<object> ListWorkItemsFromArgsAsync(AuthorizedScope scope, CancellationToken ct)
    {
        var items = await _listWorkItems.ExecuteAsync(scope.TenantId, scope.ProjectId!, scope.ActorId, ct);
        return new { workItems = items.Select(MapWorkItem).ToArray() };
    }

    private async Task<object> ReportWorkItemFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var details = await ResolveWorkItemFromArgsAsync(scope, args, ct);
        var reason = RequireString(args, "reason");
        var expectedVersion = OptionalInt(args, "expectedVersion") ?? details.Item.Version;
        var target = ResolveReportedTarget(RequireString(args, "state"), details.Item.ReportedState);
        if (target is null)
            return MapWorkChange(details.Item, details.DependsOn, Array.Empty<WorkItemTransition>());

        var change = await _transitionWorkItem.ReportAsync(
            scope.TenantId, scope.ProjectId!, details.Item.Id, target.Value, scope.ActorId, reason, expectedVersion, ct);
        return MapWorkChange(change);
    }

    private async Task<object> UpdateWorkItemPlanStateFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var details = await ResolveWorkItemFromArgsAsync(scope, args, ct);
        var reason = RequireString(args, "reason");
        var expectedVersion = OptionalInt(args, "expectedVersion") ?? details.Item.Version;
        var target = ParsePlannedState(RequireString(args, "state"));
        if (target == details.Item.PlannedState)
            return MapWorkChange(details.Item, details.DependsOn, Array.Empty<WorkItemTransition>());

        var change = await _transitionWorkItem.ChangePlannedAsync(
            scope.TenantId, scope.ProjectId!, details.Item.Id, target, scope.ActorId, reason, expectedVersion, ct);
        return MapWorkChange(change);
    }

    private async Task<WorkItemDetails> ResolveWorkItemFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var listed = await _listWorkItems.ExecuteAsync(scope.TenantId, scope.ProjectId!, scope.ActorId, ct);
        var id = OptionalGuid(args, "workItemId");
        var key = OptionalString(args, "key");
        if (id is null && string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Provide workItemId or key from list_work_items.");

        WorkItemDetails? match = null;
        if (id is Guid itemId)
            match = listed.FirstOrDefault(row => row.Item.Id.Value == itemId);
        if (match is null && !string.IsNullOrWhiteSpace(key))
            match = listed.FirstOrDefault(row =>
                string.Equals(row.Item.Key, key, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new NotFoundException(
            $"Work item '{id?.ToString() ?? key}' was not found within project '{scope.ProjectId}'.");
    }

    private static ReportedExecutionState? ResolveReportedTarget(string raw, ReportedExecutionState current)
    {
        var token = NormalizeStateToken(raw);
        if (token is "notdone")
        {
            return current is ReportedExecutionState.Completed or ReportedExecutionState.Failed
                ? ReportedExecutionState.InProgress
                : null;
        }

        var state = token switch
        {
            "completed" or "complete" or "done" => ReportedExecutionState.Completed,
            "notstarted" or "todo" => ReportedExecutionState.NotStarted,
            "inprogress" or "wip" or "started" => ReportedExecutionState.InProgress,
            "blocked" => ReportedExecutionState.Blocked,
            "failed" or "fail" => ReportedExecutionState.Failed,
            _ => throw new ArgumentException(
                $"Unknown reported state '{raw}'. Use NotStarted, InProgress, Blocked, Completed, Failed, done, or not-done."),
        };
        return state == current ? null : state;
    }

    private static PlannedWorkState ParsePlannedState(string raw)
    {
        var token = NormalizeStateToken(raw);
        return token switch
        {
            "planned" => PlannedWorkState.Planned,
            "ready" => PlannedWorkState.Ready,
            "inprogress" or "wip" or "started" => PlannedWorkState.InProgress,
            "blocked" => PlannedWorkState.Blocked,
            "cancelled" or "canceled" => PlannedWorkState.Cancelled,
            _ => throw new ArgumentException(
                $"Unknown planned state '{raw}'. Use Planned, Ready, InProgress, Blocked, or Cancelled."),
        };
    }

    private static string NormalizeStateToken(string raw) =>
        raw.Trim().Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();

    private static object MapWorkItem(WorkItemDetails details) => MapWorkItem(details.Item, details.DependsOn);

    private static object MapWorkItem(WorkItem item, IReadOnlyList<WorkItemId> dependsOn) => new
    {
        id = item.Id.Value,
        key = item.Key,
        title = item.Title,
        description = item.Description,
        milestoneId = item.MilestoneId?.Value,
        ownerId = item.OwnerId?.Value,
        dueAt = item.DueAt,
        priority = item.Priority.ToString(),
        plannedState = item.PlannedState.ToString(),
        reportedState = item.ReportedState.ToString(),
        verificationState = item.VerificationState.ToString(),
        approvalState = item.ApprovalState.ToString(),
        version = item.Version,
        dependsOn = dependsOn.Select(d => d.Value).ToArray(),
        createdAt = item.CreatedAt,
        updatedAt = item.UpdatedAt,
    };

    private static object MapWorkChange(WorkItemChange change) =>
        MapWorkChange(change.Item, change.DependsOn, change.Transitions);

    private static object MapWorkChange(WorkItem item, IReadOnlyList<WorkItemId> dependsOn,
        IReadOnlyList<WorkItemTransition> transitions) => new
    {
        item = MapWorkItem(item, dependsOn),
        transitions = transitions.Select(t => new
        {
            id = t.Id.Value,
            workItemId = t.WorkItemId.Value,
            dimension = t.Dimension.ToString(),
            fromState = t.FromState,
            toState = t.ToState,
            actorId = t.ActorId.Value,
            reason = t.Reason,
            recordedAt = t.RecordedAt,
            version = t.Version,
        }).ToArray(),
    };

    private static string RequireString(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw new ArgumentException($"{name} is required.");
        return value.GetString()!;
    }

    private static string? OptionalString(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : throw new ArgumentException($"{name} must be a string.");
    }

    private static Guid? OptionalGuid(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind == JsonValueKind.String)
            return Guid.TryParse(value.GetString(), out var parsed)
                ? parsed
                : throw new ArgumentException($"{name} must be a UUID.");
        return value.GetGuid();
    }

    private static int? OptionalInt(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.GetInt32();
    }

    private async Task<object> DescribeBaselineFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        return await _baselines.DescribeAsync(scope, args.GetProperty("baselineId").GetGuid(), ct);
    }

    private static (string Name, object Descriptor) NamedTool(string name, string description, object properties, string[] required) =>
        (name, Tool(name, description, properties, required));

    private static object Tool(string name, string description, object properties, string[] required) => new
    {
        name,
        description,
        inputSchema = new
        {
            type = "object",
            properties,
            required
        }
    };

    private static object UuidProp() => new { type = "string", format = "uuid" };
    private static object StringProp() => new { type = "string" };
    private static object StringProp(string description) => new { type = "string", description };
    private static object ArrayOf(object items, string description) => new { type = "array", description, items };
    private static object ObjectSchema(object properties, string[] required) => new { type = "object", properties, required };

    /// <summary>
    /// MCP tools/call must return a CallToolResult object. JSON-RPC <c>result</c> cannot be a
    /// JSON array; <c>structuredContent</c> also has to be an object. Named payload fields are
    /// copied onto <c>result</c> so clients that read <c>result.channels</c> (instead of
    /// <c>result.structuredContent.channels</c>) still work.
    /// </summary>
    private object ToCallToolResult(object payload)
    {
        var structured = EnsureStructuredObject(payload);
        var json = JsonSerializer.SerializeToElement(structured, _options);
        var result = new Dictionary<string, object?>
        {
            ["content"] = new object[]
            {
                new { type = "text", text = json.GetRawText() },
            },
            ["structuredContent"] = structured,
            ["isError"] = false,
        };
        if (json.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in json.EnumerateObject())
            {
                if (property.Name is "content" or "structuredContent" or "isError")
                    continue;
                result[property.Name] = property.Value.Clone();
            }
        }

        return result;
    }

    private static object EnsureStructuredObject(object payload)
    {
        switch (payload)
        {
            case JsonElement { ValueKind: JsonValueKind.Object }:
                return payload;
            case JsonElement { ValueKind: JsonValueKind.Array } array:
                return new { items = array };
            case JsonElement other:
                return new { value = other };
            case string text:
                return new { value = text };
            case IDictionary:
                return payload;
            case Array array:
                return new { items = array };
            case System.Collections.IEnumerable enumerable:
                return new { items = enumerable };
            default:
                return payload;
        }
    }
}
