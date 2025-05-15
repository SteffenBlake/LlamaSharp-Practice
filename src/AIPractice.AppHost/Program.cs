using Microsoft.Extensions.Configuration;
using AIPractice.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

var useVolumes = builder.Configuration.GetValue<bool>("UseVolumes");


var postgres = builder.AddPostgres(ServiceConstants.POSTGRES);
if (useVolumes)
{
    _ = postgres.WithDataVolume(ServiceConstants.POSTGRES, isReadOnly: false);
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
        .WithDataVolume(ServiceConstants.RABBITMQ, isReadOnly: false);
}

var kafka = builder.AddKafka(ServiceConstants.KAFKA);
if (useVolumes)
{
    _ = kafka
        .WithDataVolume(ServiceConstants.KAFKA, isReadOnly: false);
}

var dataWorker = builder.AddProject<Projects.AIPractice_DataWorker>(
        ServiceConstants.DATAWORKER
    )
    .WithReference(postgresDb)
    .WaitFor(postgresDb);

var docIngester = builder.AddProject<Projects.AIPractice_DocumentIngester>(
        ServiceConstants.DOCINGESTER
    )
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithReference(qdrant)
    .WaitFor(qdrant)
    .WithReference(rabbitMQ)
    .WaitFor(rabbitMQ)
    .WaitForCompletion(dataWorker);

var modelWorker = builder.AddProject<Projects.AIPractice_ModelWorker>(
        ServiceConstants.MODELWORKER
    )
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithReference(qdrant)
    .WaitFor(qdrant)
    .WithReference(rabbitMQ)
    .WaitFor(rabbitMQ)
    .WithReference(kafka)
    .WaitFor(kafka)
    .WaitForCompletion(dataWorker);

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
    .WaitForCompletion(dataWorker);

var svelteChat = builder.AddNpmApp(ServiceConstants.SVELTECHAT, "../AIPractice.Chat")
    .WithReference(webApi)
    .WaitFor(webApi)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
