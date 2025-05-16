using AIPractice.DocumentIngester;
using AIPractice.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using AIPractice.Domain;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var config = builder.Configuration.Get<DocumentIngesterConfig>() ??
    throw new InvalidOperationException(
        "Invalid configuration schema"
    );
builder.Services.AddSingleton(config);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    _ = options.UseNpgsql(
        builder.Configuration.GetConnectionString(ServiceConstants.POSTGRESDB)
    );
});

builder.AddQdrantClient(ServiceConstants.QDRANT);

builder.AddRabbitMQClient(ServiceConstants.RABBITMQ);

builder.Services.AddHostedService<DocumentIngesterBackgroundService>();

var host = builder.Build();
host.Run();
