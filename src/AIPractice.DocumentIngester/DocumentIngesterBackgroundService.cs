using System.Diagnostics;
using AIPractice.Domain;
using AIPractice.Domain.Ingestions;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace AIPractice.DocumentIngester;

public class DocumentIngesterBackgroundService(
    DocumentIngesterConfig config,
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    IConnection connection
) : BackgroundService
{
    public static readonly ActivitySource ActivitySource 
        = new(nameof(DocumentIngesterBackgroundService));

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
        using var activity = ActivitySource.StartActivity(
            "Data Ingestion", ActivityKind.Client
        );

        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await ExecuteIngestionsAsync(db, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }
    }

    private async Task ExecuteIngestionsAsync(
        AppDbContext db, CancellationToken cancellationToken
    )
    {
        using var channel = await connection.CreateChannelAsync(
            default,
            cancellationToken
        );

        var watchlist = await PendingIngestionCmdHandler.RunAsync(
            db, config.Ingestion, channel, config.Data, cancellationToken
        );

        var completionCommand = new IngestionCompleteCmd(watchlist);

        await IngestionCompleteCmdHandler.RunAsync(
            db, channel, completionCommand, cancellationToken
        );
    }
}
