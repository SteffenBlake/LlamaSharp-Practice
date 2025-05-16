using System.Text;
using AIPractice.Domain.Extensions;
using AIPractice.Domain.TextRecords;
using AIPractice.ServiceDefaults;
using Confluent.Kafka;
using LLama;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace AIPractice.Domain.Chat.Prompt;

public static class ChatPromptCmdHandler
{
    public static async Task HandleAsync(
        ILoggerFactory loggerFactory,
        IVectorStoreRecordCollection<Guid, TextRecord> memory,
        LLamaContext context,
        IProducer<string, string> kafka,
        ChatPromptCmd cmd,
        CancellationToken cancellationToken
    )
    {
        var logger = loggerFactory.CreateLogger<ChatPromptCmd>();
        logger.LogObject(new {cmd});

        var ex = new InteractiveExecutor(context);
        var session = new ChatSession(ex);
        session.AddUserMessage(cmd.Prompt);

        var sb = new StringBuilder();
        var memories = memory.SearchAsync(
            cmd.Prompt, 
            top:5,
            new () { IncludeVectors = false },
            cancellationToken
        );

        await foreach (var result in memories)
        {
            sb.Append("<fact relevance:");
            sb.Append((result.Score ?? 0).ToString("P1"));
            sb.AppendLine(">");
            sb.AppendLine(result.Record.Value);
            sb.AppendLine("</fact>");
        }

        var factsResult = sb.ToString();
        sb.Clear();
        session.AddSystemMessage(factsResult);
        logger.LogObject(new { facts = factsResult });

        var tokens = session.ChatAsync(
            session.History, cancellationToken: cancellationToken
        );
        var tokenCount = 0;
        
        await foreach(var token in tokens)
        {
            if (token == null)
            {
                continue;
            }

            tokenCount++;
            sb.Append(token);
            await kafka.ProduceAsync(
                ServiceConstants.KAFKA, 
                new (){ 
                    Value = token
                }, 
                cancellationToken
            );
        }
        var response = sb.ToString();
        logger.LogObject(new {tokenCount, response});
    }
}
