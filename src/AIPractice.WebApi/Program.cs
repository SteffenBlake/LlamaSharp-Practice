using AIPractice.Domain;
using AIPractice.ServiceDefaults;
using AIPractice.WebApi;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.AddServiceDefaults();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    _ = options.UseNpgsql(
        builder.Configuration.GetConnectionString(ServiceConstants.POSTGRESDB)
    );
});

builder.AddRabbitMQClient(ServiceConstants.RABBITMQ);

builder.AddKafkaConsumer<string, string>(ServiceConstants.KAFKA, config =>
{
    config.Config.GroupId = ServiceConstants.KAFKA;
});

builder.Services.AddSignalR();
builder.Services.AddHostedService<ChatHubBridge>();

var app = builder.Build();

app.AddRoutes();
app.MapHub<ChatHub>("/api/hubs/chat");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
