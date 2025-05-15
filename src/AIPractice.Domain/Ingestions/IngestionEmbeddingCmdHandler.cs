#pragma warning disable SKEXP0001

using System.Text;
using AIPractice.ServiceDefaults;
using Microsoft.CodeAnalysis;
using Microsoft.SemanticKernel.Memory;
using UglyToad.PdfPig;

namespace AIPractice.Domain.Ingestions;

public static class IngestionEmbeddingCmdHandler
{
    public static async Task HandleAsync(
        HttpClient httpClient,
        ISemanticTextMemory memory,
        IngestionEmbeddingCmd cmd,
        CancellationToken cancellationToken
    )
    {
        var builder = new StringBuilder();
        var downloadResult = await httpClient.GetAsync(cmd.Url, cancellationToken);
        downloadResult.EnsureSuccessStatusCode();
        var data = await downloadResult.Content.ReadAsStreamAsync(cancellationToken);

        foreach (var batch in BatchPdf(builder, data, cmd))
        {
            var id = Guid.NewGuid().ToString();
            await memory.SaveReferenceAsync(
                ServiceConstants.QDRANT, batch, id, id, cancellationToken: cancellationToken
            );
        }
    }

    private static readonly ParsingOptions _parsingOptions = new ()
    {
        UseLenientParsing = true
    };
    private static IEnumerable<string> BatchPdf(
        StringBuilder builder, Stream data, IngestionEmbeddingCmd cmd
    )
    {
        using var document = PdfDocument.Open(data, _parsingOptions);
        foreach (var section in cmd.Sections)
        {
            var batches = BatchSection(
                builder, cmd.BatchSize, cmd.BatchOverlap, document, section
            );
            foreach (var batch in batches)
            {
                yield return batch;
            }
        }
    }

    private static IEnumerable<string> BatchSection(
        StringBuilder builder,
        int batchSize,
        int batchOverlap,
        PdfDocument document,
        EmbeddingSection section
    )
    {
        builder.Clear();
        var tagString = BuildTags(builder, section.Tags);

        builder.Clear();
        builder.AppendLine(tagString);
        for (var page = section.From; page <= section.To; page++)
        {
            var pageText = document.GetPage(page).Text;
            builder.AppendLine(pageText);

            while (builder.Length >= batchSize)
            {
                var excess = builder.Length - batchSize;
                builder.Remove(builder.Length - excess, excess);

                var chunk = builder.ToString();
                yield return chunk;

                builder.Clear();
                builder.AppendLine(tagString);
                builder.Append(chunk[^batchOverlap..]);
                builder.AppendLine(pageText[^excess..]);
            }
        }

        // Final flush
        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static string BuildTags(
        StringBuilder builder, Dictionary<string, List<string?>> tags
    )
    {
        foreach (var (key, values) in tags)
        {
            foreach (var value in values)
            {
                builder.AppendLine($"{{{key}:{value}}}");
            }
        }
        return builder.ToString();
    }

}
