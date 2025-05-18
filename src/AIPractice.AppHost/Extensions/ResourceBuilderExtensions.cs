using AIPractice.ServiceDefaults;

namespace AIPractice.AppHost.Extensions;

public static class ResourceBuilderExtensions 
{
    public static IResourceBuilder<T> WithUrlsHost<T>(
        this IResourceBuilder<T> resource, string? host
    )
        where T: IResourceWithEndpoints
    {
        if (string.IsNullOrEmpty(host) || host == "localhost")
        {
            return resource;
        }

        resource.WithUrls(ctx => {
            foreach(var annotation in ctx.Urls)
            {
                var original = new  UriBuilder(new Uri(annotation.Url))
                {
                    Host = host 
                };
                annotation.Url = original.ToString(); 
            }
        });

        return resource;
    }

    private static int _proxyPortTracker = 0;
    public static IResourceBuilder<ProjectResource> ProxyTo<T>(
        this IResourceBuilder<ProjectResource> devProxy,
        IResourceBuilder<T> target,
        string hostOverride,
        out string host,
        string? urlSuffix = null,
        string targetEndpointName = "http"
    )
        where T: IResourceWithEndpoints
    {
        var targetPort = _proxyPortTracker + 50000;
        host = $"http://{hostOverride}:{targetPort}";

        devProxy.WithUrl(
            $"{host}{urlSuffix}",
            target.Resource.Name
        );

        devProxy.WithEnvironment(
            $"services__{ServiceConstants.DEVPROXY}__{target.Resource.Name}__{_proxyPortTracker}",
            host
        );

        devProxy.WithReference(target.GetEndpoint(targetEndpointName));

        _proxyPortTracker++;

        return devProxy;
    }
}
