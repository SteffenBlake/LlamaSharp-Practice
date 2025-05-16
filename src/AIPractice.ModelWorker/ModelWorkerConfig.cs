using System.Data.Common;
using AIPractice.ServiceDefaults;

namespace AIPractice.ModelWorker;

public record ModelWorkerConfig(
    ModelConfig Model,
    string Prompt,
    List<string> AdditionalAntiPrompts,
    Dictionary<string, string> ConnectionStrings
)
{
    public string[] AntiPrompts { get; } = [.. AdditionalAntiPrompts, Prompt];

    public DbConnectionStringBuilder QDrantConnection { get; } 
        = new DbConnectionStringBuilder()
        {
            ConnectionString = ConnectionStrings[ServiceConstants.QDRANT]
        };
    public string QDrantEndpoint => QDrantConnection["Endpoint"]?.ToString() ??
        throw new InvalidOperationException(
            "Ddrant connection string missing endpoint key"
        );
}

public record ModelConfig(
    GpuConfig GPU,
    float Temperature,
    float TopK,
    float TopP,
    float MinP,
    float PresencePenalty,
    float FrequencyPenalty,
    int VectorSize,
    uint ContextSize = 8192,
    uint MaxTokens = 1024,
    string? CacheDir = null
);

public record GpuConfig(
    int Index = 0,
    int Layers = 1
);
