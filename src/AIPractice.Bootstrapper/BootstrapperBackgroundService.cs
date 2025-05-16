using AIPractice.Domain;
using AIPractice.Domain.Chat.Prompt;
using AIPractice.Domain.Extensions;
using AIPractice.Domain.Ingestions.Pending;
using AIPractice.Domain.TextRecords;
using AIPractice.ServiceDefaults;
using Azure.Storage.Blobs;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.VectorData;
using RabbitMQ.Client;

namespace AIPractice.Bootstrapper;

public class BootstrapperBackgroundService(
    ILogger<BootstrapperBackgroundService> logger,
    IHostApplicationLifetime hostApplicationLifetime,
    IServiceProvider services,
    IConfiguration config,
    IConnection connection,
    IProducer<string, string> kafka,
    BlobServiceClient blobServiceClient
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await MigrateDatabaseAsync(cancellationToken);
        await DeclareRabbitQueuesAsync(cancellationToken);
        await DeclareKafkaChannelsAsync(cancellationToken);
        await EnsureDevModelInBlobStorage(cancellationToken);

        hostApplicationLifetime.StopApplication();
    }

    private async Task MigrateDatabaseAsync(
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Ensuring Database.");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
            if (db.Database.HasPendingModelChanges())
            {
                await db.Database.MigrateAsync(cancellationToken);
            }
        });
        logger.LogInformation("Database ensured.");
    }

    private async Task DeclareRabbitQueuesAsync(
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Ensuring RabbitMQ Queues.");
        using var channel = await connection.CreateChannelAsync(
                default,
                cancellationToken
            );

        logger.LogInformation($"Ensuring {nameof(TextRecord)} Queue.");
        _ = await channel.QueueDeclareAsync<TextRecord>(
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken
        );
        logger.LogInformation($"Ensuring {nameof(ChatPromptCmd)} Queue.");
        _ = await channel.QueueDeclareAsync<ChatPromptCmd>(
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken
        );
        logger.LogInformation($"Ensuring {nameof(IngestionFinishedMsg)} Queue.");
        _ = await channel.QueueDeclareAsync<IngestionFinishedMsg>(
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken
        );
        logger.LogInformation("RabbitMQ Queues Ensured.");
    }

    // Kafka explodes if you try and consume from a channel that
    // hasn't produced at least one message on it, to ensure it exists
    private async Task DeclareKafkaChannelsAsync(
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Ensuring Kafka Channels.");
        await kafka.ProduceAsync(
            ServiceConstants.KAFKA,
            new() { Value = "" },
            cancellationToken
        );
        logger.LogInformation("Kafka Channels Ensured.");
    }

    private async Task EnsureDevModelInBlobStorage(
        CancellationToken cancellationToken
    )
    {
        var modelPath = config["ModelPath"]?.Trim();
        if (string.IsNullOrEmpty(modelPath))
        {
            return;
        }

        logger.LogInformation(
            $"Model path configured, uploading to blob storage from '{modelPath}'"
        );
        using var fileStream = File.OpenRead(modelPath);
        
        var containerClient = blobServiceClient
            .GetBlobContainerClient(ServiceConstants.AZUREBLOBS);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blobClient = containerClient.GetBlobClient(ServiceConstants.BLOBMODEL);
        if (await blobClient.ExistsAsync(cancellationToken))
        {
            logger.LogInformation("Model already uploaded to blob storage, skipping.");
            return;
        }

        await blobClient.UploadAsync(fileStream, cancellationToken);
        logger.LogInformation("Model uploaded to blob storage.");
    }
}
