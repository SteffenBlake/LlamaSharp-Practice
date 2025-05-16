using Microsoft.Extensions.Configuration;
using AIPractice.ServiceDefaults;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var useVolumes = builder.Configuration.GetValue<bool>("UseVolumes");
var vectorSize = builder.Configuration.GetValue<int>("VectorSize");

var postgres = builder.AddPostgres(ServiceConstants.POSTGRES);
if (useVolumes)
{
    _ = postgres.WithDataVolume(ServiceConstants.POSTGRES, isReadOnly: false)
        .WithLifetime(ContainerLifetime.Persistent);
}

var postgresDb = postgres.AddDatabase(ServiceConstants.POSTGRESDB);

var qdrant = builder.AddQdrant(ServiceConstants.QDRANT);
if (useVolumes)
{
    _ = qdrant
        .WithDataVolume(ServiceConstants.QDRANT, isReadOnly: false)
        .WithLifetime(ContainerLifetime.Persistent);
}

var rabbitMQ = builder.AddRabbitMQ(ServiceConstants.RABBITMQ);
if (useVolumes)
{
    _ = rabbitMQ
        .WithDataVolume(ServiceConstants.RABBITMQ, isReadOnly: false)
        .WithLifetime(ContainerLifetime.Persistent);
}

var kafka = builder.AddKafka(ServiceConstants.KAFKA);
if (useVolumes)
{
    _ = kafka
        .WithDataVolume(ServiceConstants.KAFKA, isReadOnly: false)
        .WithLifetime(ContainerLifetime.Persistent);
}

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
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
