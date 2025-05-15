using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using AIPractice.Domain.Extensions;

namespace AIPractice.Domain.Ingestions;

public static class PendingIngestionCmdHandler
{
    public static async Task<List<string>> RunAsync(
        AppDbContext db,
        IngestionConfig config,
        IChannel channel,
        PendingIngestionCmd cmd,
        CancellationToken cancellationToken
    )
    {
        _ = await channel.QueueDeclareAsync<IngestionEmbeddingCmd>(
            cancellationToken: cancellationToken
        );

        var existing = await db.Ingestions
            .Select(i => i.Signature)
            .ToHashSetAsync(cancellationToken);

        IEnumerable<string> results = [];
        foreach (var pendingDocument in cmd.Documents)
        {
            var batch = await IngestDocumentAsync(
                config, channel, existing, pendingDocument, cancellationToken
            );

            results = results.Concat(batch);
        }

        return [.. results];
    }

    private static async Task<IEnumerable<string>> IngestDocumentAsync(
        IngestionConfig config,
        IChannel channel,
        HashSet<string> existing,
        PendingDocument pendingDocument,
        CancellationToken cancellationToken
    )
    {
        var docTags = pendingDocument.Tags ?? [];
        var pendingSections = pendingDocument.Pages.SelectMany(page =>
            GetRemainingSections(pendingDocument.Id, docTags, page, existing)
        ).ToList();
        if (pendingSections.Count == 0)
        {
            return [];
        }
        var request = new IngestionEmbeddingCmd(
            pendingDocument.Url, 
            config.BatchSize,
            config.BatchOverlap,
            pendingSections
        );

        await channel.BasicPublishAsync(request, cancellationToken: cancellationToken);

        return pendingSections.Select(s => s.Signature);
    }

    private static IEnumerable<EmbeddingSection> GetRemainingSections(
        string docId,
        Dictionary<string,
        List<string?>> docTags,
        PendingPage page,
        HashSet<string> existing
    )
    {
        var signature = $"{docId}:{page.From}:{page.To}";
        if (existing.Contains(signature))
        {
            yield break;
        }

        var tags = (page.Tags ?? []).Concat(docTags).ToDictionary();
        yield return new(signature, page.From, page.To, tags);
    }
}
