using AIPractice.DataWorker;
using AIPractice.Domain;
using AIPractice.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddOpenTelemetry().WithTracing(tracing => 
    tracing.AddSource(DataWorkerBackgroundService.ActivitySource.Name)
);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    _ = options.UseNpgsql(
        builder.Configuration.GetConnectionString(ServiceConstants.POSTGRESDB)
    );
});

builder.Services.AddHostedService<DataWorkerBackgroundService>();

var host = builder.Build();
host.Run();
