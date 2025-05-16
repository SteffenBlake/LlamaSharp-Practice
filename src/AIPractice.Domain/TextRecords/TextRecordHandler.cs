using AIPractice.Domain.Extensions;
using AIPractice.Domain.Ingestions.Pending;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using RabbitMQ.Client;

namespace AIPractice.Domain.TextRecords;

public static class TextRecordHandler
{
    public static async Task HandleAsync(
        ILoggerFactory loggerFactory,
        IChannel channel,
        IVectorStoreRecordCollection<Guid, TextRecord> memory,
        TextRecord record,
        CancellationToken cancellationToken
    )
    {
        var logger = loggerFactory.CreateLogger<TextRecord>();
        logger.LogObject(new {record});
        await memory.UpsertAsync(record, cancellationToken);

        var callbackMsg = new IngestionFinishedMsg(record.TextRecordId);
        await channel.BasicPublishAsync(
            callbackMsg, cancellationToken:cancellationToken
        );
    }
}
