#pragma warning disable SKEXP0001
using System.Diagnostics;
using LLama;
using LLama.Common;
using LLama.Native;
using LLamaSharp.SemanticKernel.TextEmbedding;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using RabbitMQ.Client;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using AIPractice.Domain.Ingestions;
using AIPractice.Domain.Chat;
using AIPractice.Domain.Extensions;
using Confluent.Kafka;

namespace AIPractice.ModelWorker;

public class ModelWorkerBackgroundService(
    ModelWorkerConfig config,
    IHostApplicationLifetime hostApplicationLifetime,
    IConnection connection,
    QdrantClient qdrant,
    IProducer<string, string> kafka
) : BackgroundService
{
    public static readonly ActivitySource ActivitySource
        = new(nameof(ModelWorkerBackgroundService));

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity(
            "Data Ingestion", ActivityKind.Client
        );

        try
        {
            await RunAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var httpClient = new HttpClient();

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

        using var weights = LLamaWeights.LoadFromFile(modelParams);
        var embedder = new LLamaEmbedder(weights, modelParams);
        var embeddingGenerator = new LLamaSharpEmbeddingGeneration(embedder);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(qdrant);
        builder.AddQdrantVectorStore();
        builder.Services.AddSingleton<ITextEmbeddingGenerationService>(embeddingGenerator);

        var kernel = builder.Build();
        var memory = kernel.GetRequiredService<ISemanticTextMemory>();

        using var context = weights.CreateContext(modelParams);

        using var channel = await connection.CreateChannelAsync(
            default,
            cancellationToken
        );

        _ = await channel.QueueDeclareAsync<IngestionEmbeddingCmd>(
            cancellationToken: cancellationToken
        );
        _ = await channel.QueueDeclareAsync<ChatPromptCmd>(
            cancellationToken: cancellationToken
        );

        while (!cancellationToken.IsCancellationRequested)
        {
            await IngestQueueAsync(
                httpClient, memory, context, channel, kafka, cancellationToken
            );
        }
    }

    public static async Task IngestQueueAsync(
        HttpClient httpClient,
        ISemanticTextMemory memory,
        LLamaContext context,
        IChannel channel,
        IProducer<string, string> kafka,
        CancellationToken cancellationToken
    )
    {
        var ingestCmd = await channel.GetAsync<IngestionEmbeddingCmd>(
            autoAck:true, cancellationToken
        );
        if (ingestCmd.HasValue)
        {
            await IngestionEmbeddingCmdHandler.HandleAsync(
                 httpClient, memory, ingestCmd.Value, cancellationToken
            );
            return;
        }

        var promptCmd = await channel.GetAsync<ChatPromptCmd>(
            autoAck:true, cancellationToken
        );
        if (promptCmd.HasValue)
        {
            await ChatPromptCmdHandler.HandleAsync(
                memory, context, kafka, promptCmd.Value, cancellationToken
            );
            return;
        }
    }
}
