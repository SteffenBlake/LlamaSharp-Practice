using LLama;
using LLama.Common;
using LLama.Native;
using RabbitMQ.Client;
using AIPractice.Domain.Extensions;
using Confluent.Kafka;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;
using Microsoft.Extensions.VectorData;
using AIPractice.Domain.Chat.Prompt;
using AIPractice.Domain.TextRecords;
using Azure.Storage.Blobs;
using AIPractice.ServiceDefaults;

namespace AIPractice.ModelWorker;

public class ModelWorkerBackgroundService(
    ILoggerFactory loggerFactory,
    ILogger<ModelWorkerBackgroundService> logger,
    BlobServiceClient blobServiceClient,
    QdrantClient qdrantClient,
    ModelWorkerConfig config,
    IHostApplicationLifetime hostApplicationLifetime,
    IConnection connection,
    IProducer<string, string> kafka
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await RunAsync(cancellationToken);
        hostApplicationLifetime.StopApplication();
    }

    private record ModelContext(
        IVectorStoreRecordCollection<Guid, TextRecord> Memory,
        LLamaContext Context
    );
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var (memory, context) = await BuildModelAsync(cancellationToken);

        using var channel = await connection.CreateChannelAsync(
            default,
            cancellationToken
        );

        while (!cancellationToken.IsCancellationRequested)
        {
            await IngestQueueAsync(
                memory, context, channel, kafka, cancellationToken
            );
        }
    }

    private async Task<ModelContext> BuildModelAsync(
        CancellationToken cancellationToken
    )
    {
        var modelDir = config.Model.CacheDir ?? ServiceConstants.MODELDIR;
        if (!Directory.Exists(modelDir))
        {
            logger.LogInformation($"Model Directory doesnt exist, creating: '{modelDir}'");
            Directory.CreateDirectory(modelDir);
        }
        var modelPath = Path.Combine(modelDir, "Model.gguf");
        if (!File.Exists(modelPath))
        {
            await DownloadModelAsync(modelPath, cancellationToken);
        }

        var modelParams = new ModelParams(modelPath)
        {
            ContextSize = config.Model.ContextSize,
            GpuLayerCount = config.Model.GPU.Layers,
            Embeddings = false,
            MainGpu = config.Model.GPU.Index,
            SplitMode = GPUSplitMode.None,
            BatchSize = config.Model.MaxTokens,
            UBatchSize = config.Model.MaxTokens,
            PoolingType = LLamaPoolingType.Mean
        };

        using var weights = LLamaWeights.LoadFromFile(modelParams);
        var embedder = new LLamaEmbedder(weights, modelParams);
        var vectorStore = new QdrantVectorStore(qdrantClient, new()
        {
            EmbeddingGenerator = embedder
        });

        var memory = vectorStore.GetCollection<Guid, TextRecord>(
            nameof(TextRecord), TextRecord.BuildDefinition(config.Model.VectorSize)
        );

        if (!await memory.CollectionExistsAsync(cancellationToken))
        {
            await memory.CreateCollectionAsync(cancellationToken);
        }

        using var context = weights.CreateContext(modelParams);

        return new(memory, context);
    }

    private async Task DownloadModelAsync(
        string modelPath, CancellationToken cancellationToken
    )
    {
        logger.LogInformation($"Downloading model to {modelPath} from Azure Storage");

        var containerClient = blobServiceClient
            .GetBlobContainerClient(ServiceConstants.AZUREBLOBS);

        var blobClient = containerClient.GetBlobClient(ServiceConstants.BLOBMODEL);
        await blobClient.DownloadToAsync(modelPath, cancellationToken);

        logger.LogInformation("Model downloaded from blob storage.");
    }

    public async Task IngestQueueAsync(
        IVectorStoreRecordCollection<Guid, TextRecord> memory,
        LLamaContext context,
        IChannel channel,
        IProducer<string, string> kafka,
        CancellationToken cancellationToken
    )
    {
        var pendingRecord = await channel.GetAsync<TextRecord>(
            autoAck:true, cancellationToken
        );
        if (pendingRecord != null)
        {
            await TextRecordHandler.HandleAsync(
                 loggerFactory, channel, memory, pendingRecord, cancellationToken
            );
            return;
        }

        var promptCmd = await channel.GetAsync<ChatPromptCmd>(
            autoAck:true, cancellationToken
        );
        if (promptCmd != null)
        {
            await ChatPromptCmdHandler.HandleAsync(
                loggerFactory, memory, context, kafka, promptCmd, cancellationToken
            );
            return;
        }
    }
}
