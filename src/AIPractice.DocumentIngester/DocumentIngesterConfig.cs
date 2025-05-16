using AIPractice.Domain.Ingestions;
using AIPractice.Domain.Ingestions.Pending;

namespace AIPractice.DocumentIngester;

public record DocumentIngesterConfig(
    PendingIngestionCmd Data,
    IngestionConfig Ingestion
);
