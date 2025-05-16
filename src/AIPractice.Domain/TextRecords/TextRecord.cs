using Microsoft.Extensions.VectorData;

namespace AIPractice.Domain.TextRecords;

public class TextRecord
{
    public Guid TextRecordId { get; set; }

    public string Value { get; set; } = default!;

    public static VectorStoreRecordDefinition BuildDefinition(int vectorSize) => new ()
    {
        Properties =
        [
            new VectorStoreRecordKeyProperty(
                nameof(TextRecordId), typeof(Guid)
            ),
            new VectorStoreRecordVectorProperty(
                nameof(Value), typeof(string), dimensions: vectorSize
            ) { 
                DistanceFunction = DistanceFunction.CosineSimilarity,
                IndexKind = IndexKind.Hnsw,
            },
        ]
    };
}
