using AIPractice.Domain;
using AIPractice.Domain.Ingestions.Pending;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace AIPractice.DocumentIngester;

public class DocumentIngesterBackgroundService(
    ILoggerFactory loggerFactory,
    DocumentIngesterConfig config,
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    IConnection connection
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await RunIngestionsAsync(db, cancellationToken);

        hostApplicationLifetime.StopApplication();
    }

    private async Task RunIngestionsAsync(
        AppDbContext db, CancellationToken cancellationToken
    )
    {
        await ExecuteIngestionsAsync(db, cancellationToken);
    }

    private async Task ExecuteIngestionsAsync(
        AppDbContext db,
        CancellationToken cancellationToken
    )
    {
        using var channel = await connection.CreateChannelAsync(
            default,
            cancellationToken
        );

        await PendingIngestionCmdHandler.HandleAsync(
            loggerFactory, db, config.Ingestion, channel, config.Data, cancellationToken
        );
    }
}
