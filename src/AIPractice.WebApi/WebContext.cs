using AIPractice.Domain;
using RabbitMQ.Client;

namespace AIPractice.WebApi;

public class WebContext<T>(
    ILogger<T> logger, IChannel channel, AppDbContext db
)
{
    public ILogger<T> Logger => logger;
    public AppDbContext DB => db;

    public CommandContext<T> Compile(HttpContext ctx)
    {
        var userId = ctx.User.Identity?.Name;
        return new CommandContext<T>(logger, channel, db, userId);
    }
}
