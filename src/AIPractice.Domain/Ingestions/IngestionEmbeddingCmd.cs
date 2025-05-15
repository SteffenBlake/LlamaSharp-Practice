namespace AIPractice.Domain.Ingestions;

public readonly record struct IngestionEmbeddingCmd(
    string Url,
    int BatchSize,
    int BatchOverlap,
    List<EmbeddingSection> Sections
);

public readonly record struct EmbeddingSection(
    string Signature,
    int From,
    int To,
    Dictionary<string, List<string?>> Tags
);
