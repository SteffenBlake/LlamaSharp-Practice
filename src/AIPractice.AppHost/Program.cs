using Microsoft.Extensions.Configuration;
using AIPractice.ServiceDefaults;
using Microsoft.Extensions.Hosting;
using AIPractice.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

var useVolumes = builder.Configuration.GetValue<bool>("UseVolumes");
var vectorSize = builder.Configuration.GetValue<int>("VectorSize");

var postgres = builder.AddPostgres(ServiceConstants.POSTGRES).WithPgAdmin();

var postgresDb = postgres.AddDatabase(ServiceConstants.POSTGRESDB);

var qdrant = builder.AddQdrant(ServiceConstants.QDRANT)
    .WithEnvironment("__VITE_ADDITIONAL_SERVER_ALLOWED_HOSTS", "true")
    // Auto append the /dashboard path to the qdrant urls
    .WithUrls(ctx => {
        foreach(var annotation in ctx.Urls)
        {
            annotation.Url += "dashboard";
        }
    });

var rabbitMQ = builder.AddRabbitMQ(ServiceConstants.RABBITMQ)
    .WithManagementPlugin();

var kafka = builder.AddKafka(ServiceConstants.KAFKA).WithKafkaUI();

var azureStorage = builder.AddAzureStorage(ServiceConstants.AZURESTORAGE);
if (builder.Environment.IsDevelopment())
{
    azureStorage.RunAsEmulator(azurite => {
        if (useVolumes)
        {
            azurite.WithDataVolume(ServiceConstants.AZURESTORAGE, isReadOnly: false);
        }
    });
}

var blobStorage = azureStorage.AddBlobs(ServiceConstants.AZUREBLOBS);

var bootstrapper = builder.AddProject<Projects.AIPractice_Bootstrapper>(
        ServiceConstants.BOOTSTRAPPER
    )
    .WithEnvironment("Model__VectorSize", vectorSize.ToString())
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithReference(qdrant)
    .WaitFor(qdrant)
    .WithReference(rabbitMQ)
    .WaitFor(rabbitMQ)
    .WithReference(kafka)
    .WaitFor(kafka)
    .WithReference(blobStorage)
    .WaitFor(blobStorage);

if (useVolumes)
{
    _ = rabbitMQ.WithDataVolume(ServiceConstants.RABBITMQ, isReadOnly: false);
    _ = postgres.WithDataVolume(ServiceConstants.POSTGRES, isReadOnly: false);
    _ = qdrant.WithDataVolume(ServiceConstants.QDRANT, isReadOnly: false);
    _ = kafka.WithDataVolume(ServiceConstants.KAFKA, isReadOnly: false);
}

if (builder.Environment.IsDevelopment())
{
    var modelPath = builder.Configuration["ModelPath"]?.Trim() ?? 
        throw new InvalidOperationException(
            "ModelPath config value is required in dev mode, please configure it to point to a .gguf file"
        );
    bootstrapper.WithEnvironment("ModelPath", modelPath);
}

var docIngester = builder.AddProject<Projects.AIPractice_DocumentIngester>(
        ServiceConstants.DOCINGESTER
    )
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithReference(qdrant)
    .WaitFor(qdrant)
    .WithReference(rabbitMQ)
    .WaitFor(rabbitMQ)
    .WaitForCompletion(bootstrapper);

var modelWorker = builder.AddProject<Projects.AIPractice_ModelWorker>(
        ServiceConstants.MODELWORKER
    )
    .WithEnvironment("Model__VectorSize", vectorSize.ToString())
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithReference(qdrant)
    .WaitFor(qdrant)
    .WithReference(rabbitMQ)
    .WaitFor(rabbitMQ)
    .WithReference(kafka)
    .WaitFor(kafka)
    .WithReference(blobStorage)
    .WaitFor(blobStorage)
    .WaitForCompletion(bootstrapper)
    .PublishAsDockerFile(container => {
        if (useVolumes)
        {
            container.WithVolume(ServiceConstants.MODELDIR);
        }
    });

var webApi = builder.AddProject<Projects.AIPractice_WebApi>(
        ServiceConstants.WEBAPI
    )
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WaitFor(modelWorker)
    .WithReference(rabbitMQ)
    .WaitFor(rabbitMQ)
    .WithReference(kafka)
    .WaitFor(kafka)
    .WaitForCompletion(bootstrapper);

var svelteChat = builder.AddNpmApp(ServiceConstants.SVELTECHAT, "../AIPractice.Chat")
    .WithReference(webApi)
    .WaitFor(webApi)
    .WithHttpEndpoint(env: "PORT")
    .PublishAsDockerFile();

var hostOverride = builder.Configuration["YARP:Host"]?.Trim();
if (!string.IsNullOrEmpty(hostOverride))
{
    Console.WriteLine("Dev Mode Host Override Mode Engaged, enabling DevProxy Service");
    Console.WriteLine($"HostOverride: {hostOverride}");

    var devProxy = builder.AddProject<Projects.AIPractice_DevProxy>(
        ServiceConstants.DEVPROXY
    )
    .WithEnvironment("HostOverride", hostOverride)
    .ProxyTo(svelteChat, hostOverride, out _)
    .ProxyTo(webApi, hostOverride, out var webApiProxyUrl)
    .ProxyTo(rabbitMQ, hostOverride, out _, targetEndpointName: "management")
    .ProxyTo(qdrant, hostOverride, out _, $"/dashboard")
    .WithUrlsHost(hostOverride)
    .WithUrlForEndpoint("http", url => url.DisplayOrder = null);

    postgres.WithPgAdmin(pgAdmin => devProxy.ProxyTo(pgAdmin, hostOverride, out _));
    kafka.WithKafkaUI(kafkaUI => devProxy.ProxyTo(kafkaUI, hostOverride, out _));

    svelteChat.WithEnvironment("ProxiedUrl", webApiProxyUrl);
}

builder.Build().Run();
