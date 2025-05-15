using AIPractice.Domain.Ingestions;

namespace AIPractice.DocumentIngester;

public record DocumentIngesterConfig(
    PendingIngestionCmd Data,
    IngestionConfig Ingestion
);
