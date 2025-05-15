using AIPractice.Domain.Ingestions;
using Microsoft.EntityFrameworkCore;

namespace AIPractice.Domain;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<IngestionEntity> Ingestions { get; set; } = null!;
}
