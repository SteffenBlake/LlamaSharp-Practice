using Qwen.MCP.AppHost.Extensions;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();

        var builder = DistributedApplication.CreateBuilder(args);

        var server = builder.AddProject<Projects.Qwen_MCP_Server>("server")
            .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName);

        builder.AddProject<Projects.Qwen_MCP_Agent>("agent")
            .WithReference(server)
            .AutoPrefixEnvironmentVariables()
            .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
            .WaitFor(server);

        var app = builder.Build();

        await app.RunAsync(cts.Token);
    }
}
