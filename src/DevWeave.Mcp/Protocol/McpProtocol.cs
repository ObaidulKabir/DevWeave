namespace DevWeave.Mcp.Protocol;

public static class McpMethods
{
    public const string Initialize = "initialize";
    public const string Initialized = "notifications/initialized";
    
    public const string ListResources = "resources/list";
    public const string ReadResource = "resources/read";
    public const string SubscribeResource = "resources/subscribe";
    
    public const string ListTools = "tools/list";
    public const string CallTool = "tools/call";
    
    public const string ListPrompts = "prompts/list";
    public const string GetPrompt = "prompts/get";

    public const string SendMessage = "coordination/send";
    public const string ReplayMessages = "coordination/replay";
}
