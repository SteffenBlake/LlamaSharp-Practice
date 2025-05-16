using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using AIPractice.Domain.Extensions;
using System.Text;
using UglyToad.PdfPig;
using Microsoft.Extensions.Logging;
using AIPractice.Domain.TextRecords;
using System.Runtime.CompilerServices;

namespace AIPractice.Domain.Ingestions.Pending;

public static class PendingIngestionCmdHandler
{
    private record PendingIngestionContext(
        ILogger Logger,
        AppDbContext DB,
        IngestionConfig Config,
        IChannel Channel,
        PendingIngestionCmd Cmd,
        HttpClient HttpClient
    );

    private record PendingIngestionResults(
        Dictionary<string, HashSet<Guid>> SigToIds,
        Dictionary<Guid, string> IdsToSigs
    );

    public static async Task HandleAsync(
        ILoggerFactory loggerFactory,
        AppDbContext db,
        IngestionConfig config,
        IChannel channel,
        PendingIngestionCmd cmd,
        CancellationToken cancellationToken
    )
    {
        var logger = loggerFactory.CreateLogger<PendingIngestionCmd>();
        logger.LogObject(new {cmd});
        using var httpClient = new HttpClient();

        var ctx = new PendingIngestionContext(
            logger, db, config, channel, cmd, httpClient
        ); 
        var existing = await db.Ingestions
            .Select(i => i.Signature)
            .ToHashSetAsync(cancellationToken);
        logger.LogInformation($"Existing ingestions found: {existing.Count}");

        var results = new PendingIngestionResults([], []);
        foreach (var pendingDocument in cmd.Documents)
        {
            var batch = IngestDocumentAsync(
                ctx, existing, pendingDocument, cancellationToken
            );
            await foreach(var result in batch)
            {
                if (existing.Contains(result.Signature))
                {
                    throw new InvalidOperationException(
                        "Attempted to ship a file signature we already ingested, something went really wrong"
                    );
                }
                results.IdsToSigs[result.Id] = result.Signature;
                if (!results.SigToIds.TryGetValue(result.Signature, out var map))
                {
                    map = results.SigToIds[result.Signature] = [];
                }
                if (!map.Add(result.Id))
                {
                    throw new InvalidOperationException(
                        $"Duplicate TextRecordId Detected: '{result.Id}'"
                    );
                }
            }
        }

        await WaitForCallbacksAsync(logger, db, channel, results, cancellationToken);
    }

    private record BatchResult(string Signature, Guid Id);
    private static async IAsyncEnumerable<BatchResult> IngestDocumentAsync(
        PendingIngestionContext ctx,
        HashSet<string> existing,
        PendingDocument pendingDocument,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var docTags = pendingDocument.Tags ?? [];
        var pendingSections = pendingDocument.Pages.SelectMany(page =>
            CalcRemainingSections(pendingDocument.Id, docTags, page, existing)
        ).ToList();
        if (pendingSections.Count == 0)
        {
            yield break;
        }

        var results = ProcessPdfAsync(
            ctx,
            pendingDocument.Url,
            pendingSections,
            cancellationToken
        );
        await foreach(var result in results)
        {
            yield return result;
        }
    }

    private static IEnumerable<EmbeddingSection> CalcRemainingSections(
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

    private static int _totalRuntimeBatches = 0;
    private static async IAsyncEnumerable<BatchResult> ProcessPdfAsync(
        PendingIngestionContext ctx,
        string url,
        List<EmbeddingSection> sections,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        ctx.Logger.LogInformation($"Processing Pdf '{url}'");

        var builder = new StringBuilder();
        var downloadResult = await ctx.HttpClient.GetAsync(url, cancellationToken);
        downloadResult.EnsureSuccessStatusCode();
        var dataStream = await downloadResult.Content.ReadAsStreamAsync(cancellationToken);
    
        var batches = PdfBatcher.BatchPdf(
            dataStream, ctx.Config.BatchSize, ctx.Config.BatchOverlap, sections, builder
        );

        foreach (var (Signature, Text) in batches)
        {
            _totalRuntimeBatches++;
            ctx.Logger.LogInformation($"Sending Batch #{_totalRuntimeBatches}, Length: {Text.Length}");
            var record = new TextRecord()
            {
                TextRecordId = Guid.NewGuid(),
                Value = Text
            };
            await ctx.Channel.BasicPublishAsync(
                record, cancellationToken: cancellationToken
            );

            yield return new(Signature, record.TextRecordId);
        }
    }

    private static async Task WaitForCallbacksAsync(
        ILogger logger,
        AppDbContext db,
        IChannel channel,
        PendingIngestionResults results,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            $"Waiting for completion callbacks, pending signatures: {results.SigToIds.Count}"
        );
        while (results.SigToIds.Count > 0)
        {
            var next = await channel.GetNextAsync<IngestionFinishedMsg>(cancellationToken);
            if (!results.IdsToSigs.TryGetValue(next.TextRecordId, out var signature))
            {
                throw new InvalidOperationException(
                    $"Unrecognized TextRecordId '{next.TextRecordId}'"
                );
            }
            _ = results.IdsToSigs.Remove(next.TextRecordId);

            if (!results.SigToIds.TryGetValue(signature, out var map))
            {
                throw new InvalidOperationException(
                    $"Unrecognized Signature '{signature}'"
                );
            }

            if (!map.Remove(next.TextRecordId))
            {
                throw new InvalidOperationException(
                    $"Unrecognized Mapping '{signature} -> {next.TextRecordId}'"
                );
            }

            if (map.Count > 0)
            {
                // Still remaining records for this signature
                continue;
            }

            await db.Ingestions.AddAsync(new()
            {
                Signature = signature
            }, cancellationToken);
            _ = db.SaveChangesAsync(cancellationToken);

            results.SigToIds.Remove(signature);

            logger.LogInformation(
                $"Signature complete: '{signature}', Remaining: {results.SigToIds.Count}"
            );
        }

    }
}
