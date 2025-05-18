namespace AIPractice.ServiceDefaults;

public static class ServiceString 
{
    public static string Build(string serviceName)
    {
        return $"services__{serviceName}__http__0";
    }
}
