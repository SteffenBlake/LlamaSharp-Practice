using AIPractice.Domain.Extensions;
using RabbitMQ.Client;

namespace AIPractice.Domain.Ingestions;

public static class IngestionCompleteCmdHandler
{
    public static async Task RunAsync(
        AppDbContext db,
        IChannel channel,
        IngestionCompleteCmd cmd,
        CancellationToken cancellationToken
    )
    {
        _ = await channel.QueueDeclareAsync<IngestionFinishedMsg>(
            cancellationToken: cancellationToken
        );

        while (cmd.SignatureWatchList.Count != 0)
        {
            var next = await channel.GetNextAsync<IngestionFinishedMsg>(cancellationToken);
            if (!cmd.SignatureWatchList.Remove(next.Signature))
            {
                throw new InvalidOperationException(
                    $"Unrecognized file signature '{next.Signature}'"
                );
            }
            await db.Ingestions.AddAsync(new()
            {
                Signature = next.Signature
            }, cancellationToken);
        }

        _ = db.SaveChangesAsync(cancellationToken);
    }
}
