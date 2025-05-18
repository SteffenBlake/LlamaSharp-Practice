namespace AIPractice.DevProxy;

public record DevProxyConfig(
    string HostOverride,
    Dictionary<string, Dictionary<string, List<string>>> Services
);
