namespace AIPractice.Domain.Ingestions;

public record PendingIngestionCmd(
    PendingDocument[] Documents
);

public record PendingDocument(
    string Url,
    string Id,
    PendingPage[] Pages,
    Dictionary<string, List<string?>>? Tags = null
);

public record PendingPage(
    int From,
    int To,
    Dictionary<string, List<string?>>? Tags = null
);
