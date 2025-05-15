namespace AIPractice.ModelWorker;

public record ModelWorkerConfig(
    ModelConfig Model,
    string Prompt,
    List<string> AdditionalAntiPrompts
)
{
    public string[] AntiPrompts { get; } = [.. AdditionalAntiPrompts, Prompt];
}


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

