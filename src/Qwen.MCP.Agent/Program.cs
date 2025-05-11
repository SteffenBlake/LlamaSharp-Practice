
using LLama;
using LLama.Common;
using LLama.Native;
using LLamaSharp.KernelMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Qwen.MCP.Agent;
using Qwen.MCP.Agent.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables("agent_");
var app = builder.Build();

using var scope = app.Services.CreateScope();
var rootConfig = scope.ServiceProvider.GetRequiredService<IConfiguration>();

var config = rootConfig.Get<AppConfig>()!;

if (!Directory.Exists(config.DataPath))
{
    _ = Directory.CreateDirectory(config.DataPath);
}

Console.WriteLine($"Loading model '{config.Model.Path}'");

var llamaConfig = new LLamaSharpConfig(config.Model.Path)
{
    DefaultInferenceParams = new InferenceParams()
    {
        AntiPrompts = config.AntiPrompts,
        MaxTokens = (int)config.Model.MaxTokens,
    },
    SplitMode = GPUSplitMode.None,
    GpuLayerCount = config.Model.GPU.Layers,
    MainGpu = config.Model.GPU.Index,
    ContextSize = config.Model.ContextSize,
};

var modelParams = new ModelParams(config.Model.Path)
{
    ContextSize = config.Model.ContextSize,
    GpuLayerCount = config.Model.GPU.Layers,
    Embeddings = false,
    MainGpu = config.Model.GPU.Index,
    SplitMode = GPUSplitMode.None,
    BatchSize = config.Model.MaxTokens,
    UBatchSize = config.Model.MaxTokens
};
var weights = LLamaWeights.LoadFromFile(modelParams);
var context = weights.CreateContext(modelParams);
var executor = new InteractiveExecutor(context);
await executor.PrefillPromptAsync(
@"
Question: What is 1+1
Answer: 2
Question: What is the value of pi
Answer: 3.14
");

var memory = new KernelMemoryBuilder()
    .WithSimpleFileStorage(new SimpleFileStorageConfig()
    {
        Directory = config.DataPath,
        StorageType = FileSystemTypes.Disk
    })
    .WithSimpleVectorDb(new SimpleVectorDbConfig()
    {
        Directory = config.DataPath,
        StorageType = FileSystemTypes.Disk
    })
    .WithLLamaSharpTextEmbeddingGeneration(
        new LLamaSharpTextEmbeddingGenerator(llamaConfig, weights)
    )
    .WithLLamaSharpTextGeneration(
        new LlamaSharpTextGenerator(
            weights, context,
            new StatelessExecutor(weights, modelParams),
            llamaConfig.DefaultInferenceParams
        )
    )
    .WithSearchClientConfig(new SearchClientConfig()
    {
        MaxMatchesCount = 3,
        AnswerTokens = (int)config.Model.MaxTokens,
        TopP = config.Model.TopP,
        Temperature = config.Model.Temperature,
        StopSequences = config.AntiPrompts,
        PresencePenalty = config.Model.PresencePenalty,
        FrequencyPenalty = config.Model.FrequencyPenalty
    })
    .With(new TextPartitioningOptions()
    {
        MaxTokensPerParagraph = (int)config.Model.MaxTokens,
        OverlappingTokens = 30
    })
    .Build();
Console.WriteLine("Model Loaded!");

// Disable further model log spam
NativeLogConfig.llama_log_set((_, __) => { });

var hasExistingMemory = Directory.GetDirectories(config.DataPath).Length > 0;
if (!hasExistingMemory)
{
    Console.WriteLine("No preloaded memory found, ingesting files.");
    foreach (var file in config.Documents)
    {
        Console.WriteLine($"Ingesting: '{file.Path}'");
        await memory.ImportDocumentAsync(
            file.Path,
            documentId: file.Id,
            tags: file.Tags?.ToTagCollection(),
            steps: Constants.PipelineWithSummary
        );
    }
}
Console.WriteLine("RAG Loaded!");

Console.WriteLine("==============================");

Console.WriteLine("Begin of convo. Type 'quit' or press Ctrl-C to end the convo whenever you like.");

var prompt = "";
Console.Write($"{config.Prompt} ");
while (true)
{
    do
    {
        prompt = Console.ReadLine()?.Trim();
    } while (string.IsNullOrEmpty(prompt));

    if (prompt.Equals("quit", StringComparison.InvariantCultureIgnoreCase))
    {
        break;
    }

    Console.WriteLine();

    var tokens = await memory.AskAsync(prompt);
    Console.Write(tokens.Result);
}
