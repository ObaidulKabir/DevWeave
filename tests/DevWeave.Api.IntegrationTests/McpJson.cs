using System.Text.Json;

namespace DevWeave.Api.IntegrationTests;

internal static class McpJson
{
    public static void AssertNoError(JsonElement envelope)
    {
        if (envelope.TryGetProperty("error", out var error) &&
            error.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            Assert.Fail($"MCP error: {error}");
        }
    }

    public static JsonElement Result(JsonElement envelope)
    {
        AssertNoError(envelope);
        Assert.True(envelope.TryGetProperty("result", out var result), "JSON-RPC success responses must include result.");
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        return result;
    }

    public static JsonElement Payload(JsonElement envelope)
    {
        var result = Result(envelope);
        if (result.TryGetProperty("structuredContent", out var structured) &&
            structured.ValueKind == JsonValueKind.Object)
        {
            return structured;
        }

        return result;
    }

    public static void AssertCallToolResult(JsonElement envelope)
    {
        var result = Result(envelope);
        Assert.True(result.TryGetProperty("content", out var content));
        Assert.Equal(JsonValueKind.Array, content.ValueKind);
        Assert.Equal("text", content[0].GetProperty("type").GetString());
        Assert.False(result.GetProperty("isError").GetBoolean());
        Assert.Equal(JsonValueKind.Object, result.GetProperty("structuredContent").ValueKind);
    }
}
