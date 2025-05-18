using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace AIPractice.Domain;

public class CommandContext<T>(
    ILogger<T> logger, IChannel channel, AppDbContext db, string? userId
)
{
    public ILogger<T> Logger => logger;
    public IChannel Channel => channel;
    public AppDbContext DB => db;
    public string? UserId => userId;
}
