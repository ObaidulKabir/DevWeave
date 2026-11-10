using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class KnowledgeSearchRepository : IKnowledgeSearchRepository
{
    private const int MaximumCursorOffset = 1_000_000;
    private readonly DevWeaveDbContext _db;
    private readonly IContentInspector _inspector;

    public KnowledgeSearchRepository(DevWeaveDbContext db, IContentInspector inspector)
    {
        _db = db;
        _inspector = inspector;
    }

    public async Task<SearchKnowledgeResponse> SearchAsync(
        TenantId tenantId,
        ProjectId projectId,
        Classification classificationCeiling,
        SearchKnowledgeRequest request,
        CancellationToken ct = default)
    {
        var fingerprint = CreateFingerprint(tenantId, projectId, classificationCeiling, request);
        var offset = DecodeCursor(request.Cursor, fingerprint);
        var queryTextInspection = await _inspector.InspectAsync(
            string.Empty,
            "text/plain",
            Encoding.UTF8.GetBytes(request.Text),
            ct);
        if (queryTextInspection.Verdict != ContentVerdict.Allow)
        {
            throw new ProhibitedContentException(
                "The search query was rejected by the content policy.",
                queryTextInspection.Reasons);
        }

        var hits = new List<SearchKnowledgeHit>(request.Limit);
        var rawRowsRead = 0;
        int? nextOffset = null;

        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = _db.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH search_query AS (
                    SELECT websearch_to_tsquery('simple'::regconfig, @query_text) AS terms
                ),
                ranked AS (
                    SELECT
                        i."Id" AS item_id,
                        r."Id" AS revision_id,
                        i."Title" AS title,
                        i."TypeId" AS type_id,
                        i."State" AS state,
                        i."ReviewState" AS review_state,
                        i."VerificationState" AS verification_state,
                        i."CurrencyState" AS currency_state,
                        r."RecordedAt" AS recorded_at,
                        r."Actor" AS actor_id,
                        r."Content"::text AS content_text,
                        ts_rank_cd(
                            to_tsvector('simple'::regconfig, r."Content"::text),
                            search_query.terms
                        ) AS rank
                    FROM knowledge_items AS i
                    INNER JOIN knowledge_revisions AS r
                        ON r."TenantId" = i."TenantId"
                       AND r."KnowledgeItemId" = i."Id"
                       AND r."Id" = i."CurrentRevisionId"
                    CROSS JOIN search_query
                    WHERE i."TenantId" = @tenant_id
                      AND i."ProjectId" = @project_id
                      AND i."Classification" <= @classification_ceiling
                      AND to_tsvector('simple'::regconfig, r."Content"::text) @@ search_query.terms
                      AND (@type_id IS NULL OR i."TypeId" = @type_id)
                      AND (@lifecycle IS NULL OR i."State" = @lifecycle)
                      AND (@review IS NULL OR i."ReviewState" = @review)
                      AND (@verification IS NULL OR i."VerificationState" = @verification)
                      AND (@currency IS NULL OR i."CurrencyState" = @currency)
                      AND (@actor_id IS NULL OR r."Actor" = @actor_id)
                      AND (@since IS NULL OR r."RecordedAt" >= @since)
                )
                SELECT item_id, revision_id, title, type_id, state, review_state,
                       verification_state, currency_state, recorded_at, actor_id, content_text
                FROM ranked
                ORDER BY rank DESC, recorded_at DESC, item_id ASC
                OFFSET @offset
                LIMIT @take
                """;

            command.Parameters.Add(new NpgsqlParameter("query_text", NpgsqlDbType.Text) { Value = request.Text });
            command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId.Value });
            command.Parameters.Add(new NpgsqlParameter("project_id", NpgsqlDbType.Uuid) { Value = projectId.Value });
            command.Parameters.Add(new NpgsqlParameter("classification_ceiling", NpgsqlDbType.Integer)
            {
                Value = (int)classificationCeiling,
            });
            command.Parameters.Add(new NpgsqlParameter("type_id", NpgsqlDbType.Text)
            {
                Value = DbValue(request.TypeId),
            });
            command.Parameters.Add(new NpgsqlParameter("lifecycle", NpgsqlDbType.Integer)
            {
                Value = EnumValue<InfoState>(request.Lifecycle),
            });
            command.Parameters.Add(new NpgsqlParameter("review", NpgsqlDbType.Integer)
            {
                Value = EnumValue<ReviewState>(request.Review),
            });
            command.Parameters.Add(new NpgsqlParameter("verification", NpgsqlDbType.Integer)
            {
                Value = EnumValue<VerificationState>(request.Verification),
            });
            command.Parameters.Add(new NpgsqlParameter("currency", NpgsqlDbType.Integer)
            {
                Value = EnumValue<CurrencyState>(request.Currency),
            });
            command.Parameters.Add(new NpgsqlParameter("actor_id", NpgsqlDbType.Uuid)
            {
                Value = request.ActorId.HasValue ? request.ActorId.Value : DBNull.Value,
            });
            command.Parameters.Add(new NpgsqlParameter("since", NpgsqlDbType.TimestampTz)
            {
                Value = request.Since.HasValue ? request.Since.Value : DBNull.Value,
            });
            command.Parameters.Add(new NpgsqlParameter("offset", NpgsqlDbType.Integer) { Value = offset });
            command.Parameters.Add(new NpgsqlParameter("take", NpgsqlDbType.Integer) { Value = request.Limit + 1 });

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var rowIndex = rawRowsRead++;
                var itemId = reader.GetGuid(0);
                var revisionId = reader.GetGuid(1);
                var title = reader.GetString(2);
                var typeId = reader.GetString(3);
                var content = reader.GetString(10);

                var contentInspection = await InspectSearchableTextAsync(content, ct);
                var titleInspection = await InspectSearchableTextAsync(title, ct);
                var typeInspection = await InspectSearchableTextAsync(typeId, ct);
                if (contentInspection.Verdict != ContentVerdict.Allow ||
                    titleInspection.Verdict != ContentVerdict.Allow ||
                    typeInspection.Verdict != ContentVerdict.Allow)
                {
                    continue;
                }

                if (hits.Count == request.Limit)
                {
                    nextOffset = offset + rowIndex;
                    break;
                }

                hits.Add(new SearchKnowledgeHit(
                    itemId,
                    revisionId,
                    title,
                    typeId,
                    (InfoState)reader.GetInt32(4),
                    (ReviewState)reader.GetInt32(5),
                    (VerificationState)reader.GetInt32(6),
                    (CurrencyState)reader.GetInt32(7),
                    reader.GetFieldValue<DateTimeOffset>(8),
                    reader.GetGuid(9),
                    $"/tenants/{tenantId.Value:D}/revisions/{revisionId:D}"));
            }
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }

        if (nextOffset is null && rawRowsRead > request.Limit)
            nextOffset = offset + rawRowsRead;

        return new SearchKnowledgeResponse(
            hits,
            nextOffset is null ? null : EncodeCursor(nextOffset.Value, fingerprint));
    }

    private async Task<ContentInspectionResult> InspectSearchableTextAsync(string text, CancellationToken ct) =>
        await _inspector.InspectAsync(string.Empty, "application/json", Encoding.UTF8.GetBytes(text), ct);

    private static object DbValue(string? value) => value is null ? DBNull.Value : value;

    private static object EnumValue<TEnum>(string? value)
        where TEnum : struct, Enum =>
        value is null ? DBNull.Value : Convert.ToInt32(Enum.Parse<TEnum>(value, ignoreCase: false));

    private static string CreateFingerprint(
        TenantId tenantId,
        ProjectId projectId,
        Classification classificationCeiling,
        SearchKnowledgeRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            tenantId = tenantId.Value,
            projectId = projectId.Value,
            classificationCeiling,
            request = request with { Cursor = null },
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static int DecodeCursor(string? cursor, string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return 0;

        try
        {
            var encoded = cursor.Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            var payload = JsonSerializer.Deserialize<SearchCursor>(
                Convert.FromBase64String(encoded));

            if (payload is null ||
                payload.Version != 1 ||
                payload.Offset is < 0 or > MaximumCursorOffset ||
                !string.Equals(payload.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                throw new ArgumentException("The search cursor is invalid or belongs to a different query.", nameof(cursor));
            }

            return payload.Offset;
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("The search cursor is malformed.", nameof(cursor), ex);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The search cursor is malformed.", nameof(cursor), ex);
        }
    }

    private static string EncodeCursor(int offset, string fingerprint)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new SearchCursor(1, offset, fingerprint));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed record SearchCursor(int Version, int Offset, string Fingerprint);
}
