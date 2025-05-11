namespace Qwen.MCP.Agent;

public record AppConfig(
    ModelConfig Model,
    string DataPath,
    DocumentConfig[] Documents,
    string Prompt,
    List<string> AdditionalAntiPrompts,
    ServicesConfig? Services = null
)
{
    public string[] AntiPrompts { get; } = [.. AdditionalAntiPrompts, Prompt];
}

public record ServicesConfig(
    ServerConfig Server
);

public record ServerConfig(
    string[] HTTP,
    string[] HTTPS
);

public record ModelConfig(
    GpuConfig GPU,
    string Path,
    float Temperature,
    float TopK,
    float TopP,
    float MinP,
    float PresencePenalty,
    float FrequencyPenalty,
    uint ContextSize = 8192,
    uint MaxTokens = 1024
);

public record GpuConfig(
    int Index = 0,
    int Layers = 1
);

public record DocumentConfig(
    string Path,
    string Id,
    Dictionary<string, List<string?>>? Tags = null
);
