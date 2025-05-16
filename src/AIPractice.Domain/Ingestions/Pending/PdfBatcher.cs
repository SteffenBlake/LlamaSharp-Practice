using System.Text;
using UglyToad.PdfPig;

namespace AIPractice.Domain.Ingestions.Pending;

public static class PdfBatcher
{
    private static readonly ParsingOptions _parsingOptions = new ()
    {
        UseLenientParsing = true
    };
    public static IEnumerable<(string Signature, string Text)> BatchPdf(
        Stream data,
        int batchSize,
        int batchOverlap,
        List<EmbeddingSection> sections, 
        StringBuilder builder
    )
    {
        using var document = PdfDocument.Open(data, _parsingOptions);
        foreach (var section in sections)
        {
            var batches = BatchSection(
                batchSize,
                batchOverlap,
                document,
                section,
                builder
            );
            foreach (var batch in batches)
            {
                yield return (section.Signature, batch);
            }
        }
    }

    private static IEnumerable<string> BatchSection(
        int batchSize,
        int batchOverlap,
        PdfDocument document,
        EmbeddingSection section,
        StringBuilder builder
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
