namespace Qwen.MCP.AppHost.Extensions;

public static class ResourceBuilderExtensions
{
    public static IResourceBuilder<ProjectResource> AutoPrefixEnvironmentVariables(
        this IResourceBuilder<ProjectResource> builder
    )
    {
        return builder.WithEnvironment(ctx =>
        {
            var kvps = ctx.EnvironmentVariables.ToArray();
            var name = ctx.Resource.Name;

            foreach (var p in kvps)
            {
                ctx.EnvironmentVariables[$"{name}_{p.Key}"] = p.Value;
                _ = ctx.EnvironmentVariables.Remove(p.Key);
            }
        });
    }
}
