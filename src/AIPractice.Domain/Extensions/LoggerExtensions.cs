using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIPractice.Domain.Extensions;

public static class LoggerExtensions
{
    private static readonly JsonSerializerOptions _logJsonOptions = new()
    {
        WriteIndented = true,
    };
    public static void LogObject<T>(this ILogger logger, T value)
    {
        logger.LogInformation(JsonSerializer.Serialize(value, _logJsonOptions));
    }
}
