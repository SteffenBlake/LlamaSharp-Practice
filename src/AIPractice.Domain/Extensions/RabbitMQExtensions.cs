using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace AIPractice.Domain.Extensions;

public static class RabbitMQExtensions
{
    public static Task<QueueDeclareOk> QueueDeclareAsync<T>(
        this IChannel channel,
        bool durable = false, 
        bool exclusive = true, 
        bool autoDelete = true, 
        IDictionary<string, object?>? arguments = null, 
        bool noWait = false,
        CancellationToken cancellationToken = default
    )
        where T: class
    {
        return channel.QueueDeclareAsync(
            typeof(T).Name, durable, exclusive, autoDelete, arguments, noWait, cancellationToken
        );
    }

    public static ValueTask BasicPublishAsync<T>(
        this IChannel channel,
        T data,
        string exchange = "",
        CancellationToken cancellationToken = default
    )
        where T: class
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
        return channel.BasicPublishAsync(
            exchange,
            typeof(T).Name,
            body,
            cancellationToken
        );
    }

    public static async ValueTask<T> GetNextAsync<T>(
        this IChannel channel,
        CancellationToken cancellationToken = default
    )
        where T : class 
    {
        T? result;
        do
        {
            result = await channel.GetAsync<T>(
                autoAck: true, cancellationToken
            );

            if (result != null)
            {
                return result;
            }

            await Task.Delay(1000, cancellationToken);
        } while (result == null);

        throw new InvalidOperationException();
    }

    public static async ValueTask<T?> GetAsync<T>(
        this IChannel channel,
        bool autoAck,
        CancellationToken cancellationToken = default
    )
        where T : class 
    {
        var result = await channel.BasicGetAsync(
            typeof(T).Name, autoAck, cancellationToken
        );
        if (result == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(
            result.Body.Span
        );
    }
}
