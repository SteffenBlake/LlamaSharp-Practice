using System.Diagnostics;
using AIPractice.Domain;
using Microsoft.EntityFrameworkCore;

namespace AIPractice.DataWorker;

public class DataWorkerBackgroundService(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime
) : BackgroundService
{
    public static readonly ActivitySource ActivitySource 
        = new(nameof(DataWorkerBackgroundService));
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await MigrateDatabaseAsync(db, cancellationToken);

        hostApplicationLifetime.StopApplication();
    }

    private static async Task MigrateDatabaseAsync(
        AppDbContext db, CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity(
            "Data Migration", ActivityKind.Client
        );

        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await db.Database.EnsureCreatedAsync(cancellationToken);
                if (db.Database.HasPendingModelChanges())
                {
                    await db.Database.MigrateAsync(cancellationToken);
                }
            });
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }
    }
}
