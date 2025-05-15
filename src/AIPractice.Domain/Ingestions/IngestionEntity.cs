using System.ComponentModel.DataAnnotations;

namespace AIPractice.Domain.Ingestions;

public class IngestionEntity 
{
    [Key]
    public int IngestionId { get; set; }

    public required string Signature { get; set; }
}
