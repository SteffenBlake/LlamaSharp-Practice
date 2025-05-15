
using AIPractice.ServiceDefaults;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;

namespace AIPractice.WebApi;

public class ChatHubBridge(
    IConsumer<string, string> kafka,
    IHubContext<ChatHub> chatHub
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        kafka.Subscribe(ServiceConstants.KAFKA);
        try
        {
            await MonitorKafkaEvents(cancellationToken);
        }
        finally
        {
            kafka.Close();
        }
    }

    private async Task MonitorKafkaEvents(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {

                var result = kafka.Consume(millisecondsTimeout:50);
                if (result != null)
                {
                    await chatHub.Clients.All.SendAsync(
                        "token", result.Message, cancellationToken
                    );
                    continue;
                }
                await Task.Delay(5000, cancellationToken);
            }
            catch (ConsumeException ex) when (ex.Error.IsLocalError && ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                await Task.Delay(5000, cancellationToken);
            }
        }
    }
}
