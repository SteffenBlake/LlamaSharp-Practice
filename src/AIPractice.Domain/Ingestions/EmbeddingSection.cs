namespace AIPractice.Domain.Ingestions;

public readonly record struct EmbeddingSection(
    string Signature,
    int From,
    int To,
    Dictionary<string, List<string?>> Tags
);
