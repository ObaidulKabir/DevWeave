using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Planning;
using DevWeave.Application.Specifications;
using DevWeave.Domain.Specifications;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Knowledge;
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
    private readonly SpecificationService _specs;
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
        SpecificationService specs,
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
        _specs = specs;
        _logger = logger;
        _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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
                McpMethods.ListTools => HandleListTools(),
                McpMethods.CallTool => await HandleCallToolAsync(scope, runtimeId, request, ct),
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
            tools = new { },
            resources = new { },
            prompts = new { }
        },
        serverInfo = new
        {
            name = "DevWeave-Coordination-Plane",
            version = "1.0.0"
        }
    };

    private static object HandleListTools() => new
    {
        tools = new object[]
        {
            Tool("send_project_message", "Send a durable coordination message to a channel",
                new { channelId = UuidProp(), messageType = StringProp(), payload = new { type = "object" }, idempotencyKey = StringProp() },
                new[] { "channelId", "messageType", "payload", "idempotencyKey" }),
            Tool("read_messages_after_cursor", "Replay durable channel messages after a sequence cursor",
                new { channelId = UuidProp(), afterSequence = new { type = "integer" }, limit = new { type = "integer" } },
                new[] { "channelId" }),
            Tool("acknowledge_messages", "Acknowledge processed channel sequence for reconnect recovery",
                new { channelId = UuidProp(), processedThroughSequence = new { type = "integer" } },
                new[] { "channelId", "processedThroughSequence" }),
            Tool("open_channel", "Open a project coordination channel",
                new { channelType = StringProp(), campaignId = UuidProp(), workPackageId = UuidProp() },
                new[] { "channelType" }),
            Tool("register_runtime", "Register this agent runtime instance for the project",
                new { runtimeClass = StringProp(), protocolVersion = StringProp(), capabilities = new { type = "array", items = new { type = "string" } }, attestationRef = StringProp() },
                new[] { "runtimeClass", "protocolVersion", "attestationRef" }),
            Tool("heartbeat_lease", "Renew an exclusive work-package lease fencing token",
                new { leaseId = UuidProp(), expectedFence = new { type = "integer" }, expiresAt = StringProp() },
                new[] { "leaseId", "expectedFence", "expiresAt" }),
            Tool("request_assignment", "Acquire an exclusive lease for a work package resource scope",
                new { workPackageId = UuidProp(), resourceScope = StringProp(), expiresAt = StringProp(), idempotencyKey = StringProp() },
                new[] { "workPackageId", "resourceScope", "expiresAt", "idempotencyKey" }),
            Tool("get_plan_status", "Current plan version and every campaign with the plan version it runs on. Call at start-up and whenever a request is refused",
                new { }, Array.Empty<string>()),
            Tool("list_work_packages", "List a campaign's work packages with status, declared scope and whether each can still progress (and why not)",
                new { campaignId = UuidProp() }, new[] { "campaignId" }),
            Tool("get_work_status", "Check whether one work package can still progress, and the reason if it cannot (for example a newer plan version was locked)",
                new { workPackageId = UuidProp() }, new[] { "workPackageId" }),
            Tool("create_work_package", "Create a work package under a campaign's plan version. Declare what it implements (approved knowledge revisions from that version, work items, specification topics) so plan changes can be traced to it",
                new
                {
                    campaignId = UuidProp(), baselineId = UuidProp(), objective = StringProp(),
                    implementsRevisionIds = new { type = "array", items = UuidProp() },
                    workItemIds = new { type = "array", items = UuidProp() },
                    specTopics = new { type = "array", items = StringProp() },
                },
                new[] { "campaignId", "baselineId", "objective" }),
            Tool("release_lease", "Release a lease you hold. Always allowed, including after the plan changed",
                new { leaseId = UuidProp(), expectedFence = new { type = "integer" } },
                new[] { "leaseId", "expectedFence" }),
            Tool("submit_change_set", "Record a change set (repository reference only, never source) for an open work package",
                new
                {
                    workPackageId = UuidProp(), codebaseTargetId = UuidProp(), providerRef = StringProp(),
                    commitSha = StringProp(), changedPaths = new { type = "array", items = StringProp() },
                },
                new[] { "workPackageId", "codebaseTargetId", "providerRef", "commitSha" }),
            Tool("report_workthrough", "Write a workthrough for a work package: kind Succeeded (outcome + whatSucceeded) or Blocked (outcome + blockedReason, optional whatSucceeded for partial progress). Agent claim only — not verification or approval. Allowed even when canProgress is false",
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
            Tool("request_integration", "Queue a change set for integration. The decision re-checks the plan; work the plan has moved past is rejected, not merged",
                new { changeSetId = UuidProp(), idempotencyKey = StringProp() },
                new[] { "changeSetId", "idempotencyKey" }),
            Tool("list_ready_work", "Deprecated alias of create_work_package: despite its name it creates a package. Use create_work_package",
                new
                {
                    campaignId = UuidProp(), baselineId = UuidProp(), objective = StringProp(),
                    implementsRevisionIds = new { type = "array", items = UuidProp() },
                    workItemIds = new { type = "array", items = UuidProp() },
                    specTopics = new { type = "array", items = StringProp() },
                },
                new[] { "campaignId", "baselineId", "objective" }),
            Tool("get_plan_baseline", "Describe a locked plan baseline export manifest",
                new { baselineId = UuidProp() },
                new[] { "baselineId" }),
            Tool("read_plan_outline", "Read the project outline, revision-pinned Work links, and your own proposals within your ReadKnowledge grant and classification ceiling",
                new { }, Array.Empty<string>()),
            Tool("propose_plan_expansion", "Submit a child Knowledge idea for human review; does not create or approve an item. Requires WriteKnowledge grant",
                new { parentItemId = UuidProp(), key = StringProp(), title = StringProp(), typeId = StringProp(), rationale = StringProp() },
                new[] { "parentItemId", "key", "title", "typeId", "rationale" }),
            Tool("resolve_spec", "Resolve the single binding specification decision for a topic in this project, as data. Returns origin, revision ids and any conflicts",
                new { topic = StringProp() }, new[] { "topic" }),
            Tool("get_effective_spec", "Read every binding specification decision for this project, plus undecided topics and incompatibilities",
                new { }, Array.Empty<string>()),
            Tool("list_spec_topics", "List this project's specification topics (built-in packs plus approved custom ones) with legal choices and typed parameters",
                new { }, Array.Empty<string>()),
            Tool("get_spec_catalog", "Read the effective specification catalog: packs, topics, profiles and any overlay problems",
                new { }, Array.Empty<string>()),
            Tool("list_spec_options", "List ranked options for a topic, marking which are recommended and which are compatible with decisions that already bind. Choose from this list",
                new { topic = StringProp() }, new[] { "topic" }),
            Tool("next_open_topics", "List topics that still need a decision, in the order they should be decided",
                new { }, Array.Empty<string>()),
            Tool("get_spec_pack_upgrades", "List available upgrades for built-in packs this project is held back on, with a diff and affected decisions",
                new { }, Array.Empty<string>()),
            Tool("propose_spec_profile", "Propose a whole profile (or a custom list of decisions) as one bundle for a single human approval. Each decision is validated against its topic. Requires WriteKnowledge grant",
                new { profile = StringProp(), level = StringProp(), decisions = new { type = "array", items = new { type = "object" } }, rationale = StringProp() },
                new[] { "level" }),
            Tool("propose_spec_overlay", "Propose a catalog extension (custom topics, options, profiles, hidden or deprecated options). Needs human approval before it applies. Requires WriteKnowledge grant",
                new { overlay = new { type = "object" }, rationale = StringProp() }, new[] { "overlay" }),
            Tool("propose_spec_decision", "Propose a specification decision for a topic that has no binding decision. It is validated against the topic definition and needs human approval before it binds. Requires WriteKnowledge grant",
                new { topic = StringProp(), level = StringProp(), choice = StringProp(), @params = new { type = "object" }, rationale = StringProp() },
                new[] { "topic", "level", "choice" }),
        }
    };

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

        return name switch
        {
            "send_project_message" or "send_message" => await SendFromArgsAsync(scope, runtimeId, args, ct),
            "read_messages_after_cursor" => await ReplayFromArgsAsync(scope, args, ct),
            "acknowledge_messages" => await AckFromArgsAsync(scope, args, ct),
            "open_channel" => await OpenChannelFromArgsAsync(scope, args, ct),
            "register_runtime" => await RegisterRuntimeFromArgsAsync(scope, args, ct),
            "heartbeat_lease" => await RenewLeaseFromArgsAsync(scope, args, ct),
            "request_assignment" => await AcquireLeaseFromArgsAsync(scope, runtimeId, args, ct),
            "list_ready_work" or "create_work_package" => await CreatePackageFromArgsAsync(scope, args, ct),
            "get_plan_status" => await _status.GetPlanStatusAsync(scope, ct),
            "list_work_packages" => await _status.ListWorkPackagesAsync(scope, args.GetProperty("campaignId").GetGuid(), ct),
            "get_work_status" => await _status.GetWorkStatusAsync(scope, args.GetProperty("workPackageId").GetGuid(), ct),
            "release_lease" => await ReleaseLeaseFromArgsAsync(scope, args, ct),
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
            "resolve_spec" => SpecProjection.Resolution(await _specs.ResolveAsync(
                scope.TenantId, scope.ProjectId!, scope.ActorId, args.GetProperty("topic").GetString()!, ct)),
            "get_effective_spec" => SpecProjection.Effective(await _specs.EffectiveAsync(
                scope.TenantId, scope.ProjectId!, scope.ActorId, ct)),
            "list_spec_topics" => (await _specs.CatalogAsync(scope.TenantId, scope.ProjectId!, scope.ActorId, ct))
                .Topics.Select(SpecProjection.Topic).ToArray(),
            "get_spec_catalog" => SpecProjection.Catalog(await _specs.CatalogAsync(
                scope.TenantId, scope.ProjectId!, scope.ActorId, ct)),
            "list_spec_options" => SpecProjection.Options(await _specs.OptionsAsync(
                scope.TenantId, scope.ProjectId!, scope.ActorId, args.GetProperty("topic").GetString()!, ct)),
            "next_open_topics" => SpecProjection.OpenTopics(await _specs.OpenTopicsAsync(
                scope.TenantId, scope.ProjectId!, scope.ActorId, ct)),
            "propose_spec_overlay" => await ProposeOverlayFromArgsAsync(scope, args, ct),
            "propose_spec_profile" => await ProposeBundleFromArgsAsync(scope, args, ct),
            "get_spec_pack_upgrades" => SpecProjection.Upgrades(await _specs.UpgradesAsync(
                scope.TenantId, scope.ProjectId!, scope.ActorId, ct)),
            "propose_spec_decision" => await ProposeSpecFromArgsAsync(scope, args, ct),
            _ => throw new InvalidOperationException($"Unknown tool: {name}")
        };
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
        return await _channels.ReplayAsync(scope, channelId, after, limit, ct);
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
        return await _channels.ReplayAsync(scope, channelId, after, Math.Clamp(limit, 1, 200), ct);
    }

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

    private async Task<object> ReleaseLeaseFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        await _leases.ReleaseAsync(scope, args.GetProperty("leaseId").GetGuid(), args.GetProperty("expectedFence").GetInt64(), ct);
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
        UnauthorizedTenantException => new JsonRpcError(-32003, ex.Message, new { kind = "forbidden" }),
        SpecDecisionRejectedException rejected => new JsonRpcError(-32602, ex.Message,
            new { kind = "invalid", issues = rejected.Issues.Select(i => new { i.Path, i.Message }) }),
        PayloadValidationException => new JsonRpcError(-32602, ex.Message, new { kind = "invalid" }),
        ProhibitedContentException => new JsonRpcError(-32602, ex.Message, new { kind = "prohibited-content" }),
        InvalidOperationException when ex.Message.StartsWith("Unknown tool", StringComparison.Ordinal)
            => new JsonRpcError(-32601, ex.Message, new { kind = "unknown-tool" }),
        ArgumentException or KeyNotFoundException or FormatException => new JsonRpcError(-32602, ex.Message, new { kind = "invalid" }),
        _ => null,
    };

    private async Task<object> RenewLeaseFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        var leaseId = args.GetProperty("leaseId").GetGuid();
        var fence = args.GetProperty("expectedFence").GetInt64();
        var expiresAt = DateTimeOffset.Parse(args.GetProperty("expiresAt").GetString()!);
        return await _leases.RenewAsync(scope, leaseId, fence, expiresAt, ct);
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

    private async Task<object> DescribeBaselineFromArgsAsync(AuthorizedScope scope, JsonElement args, CancellationToken ct)
    {
        return await _baselines.DescribeAsync(scope, args.GetProperty("baselineId").GetGuid(), ct);
    }

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
}
