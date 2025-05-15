namespace AIPractice.Domain.Ingestions;

public record IngestionConfig(
    int BatchSize, int BatchOverlap
);
