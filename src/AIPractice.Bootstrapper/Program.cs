using AIPractice.Bootstrapper;
using AIPractice.Domain;
using AIPractice.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var config = builder.Configuration.Get<BootstrapperConfig>() 
    ?? throw new InvalidOperationException(
        "Invalid configuration schema"
    );
_ = builder.Services.AddSingleton(config);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    _ = options.UseNpgsql(
        builder.Configuration.GetConnectionString(ServiceConstants.POSTGRESDB)
    );
});

builder.AddQdrantClient(ServiceConstants.QDRANT);

builder.AddRabbitMQClient(ServiceConstants.RABBITMQ);

builder.AddKafkaProducer<string, string>(ServiceConstants.KAFKA);

builder.AddAzureBlobClient(ServiceConstants.AZUREBLOBS);

builder.Services.AddHostedService<BootstrapperBackgroundService>();

var host = builder.Build();
host.Run();
