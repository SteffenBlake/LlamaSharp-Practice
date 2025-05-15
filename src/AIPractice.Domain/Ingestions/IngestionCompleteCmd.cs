namespace AIPractice.Domain.Ingestions;

public readonly record struct IngestionCompleteCmd(List<string> SignatureWatchList);
