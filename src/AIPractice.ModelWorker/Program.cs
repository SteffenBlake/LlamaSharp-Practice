using System.Text.Json;
using AIPractice.Domain;
using AIPractice.ModelWorker;
using AIPractice.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var config = builder.Configuration.Get<ModelWorkerConfig>() ?? throw new JsonException(
    "Invalid Configuration Schema"
);
builder.Services.AddSingleton(config);

builder.Services.AddOpenTelemetry().WithTracing(tracing => 
    tracing.AddSource(ModelWorkerBackgroundService.ActivitySource.Name)
);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    _ = options.UseNpgsql(
        builder.Configuration.GetConnectionString(ServiceConstants.POSTGRESDB)
    );
});

builder.AddQdrantClient(ServiceConstants.QDRANT);

builder.AddRabbitMQClient(ServiceConstants.RABBITMQ);

builder.AddKafkaProducer<string, string>(ServiceConstants.KAFKA);

builder.Services.AddHostedService<ModelWorkerBackgroundService>();

var host = builder.Build();
host.Run();
