#pragma warning disable SKEXP0001

using System.Text;
using AIPractice.ServiceDefaults;
using Confluent.Kafka;
using LLama;
using Microsoft.SemanticKernel.Memory;

namespace AIPractice.Domain.Chat;

public static class ChatPromptCmdHandler
{
    public static async Task HandleAsync(
        ISemanticTextMemory memory,
        LLamaContext context,
        IProducer<string, string> kafka,
        ChatPromptCmd cmd,
        CancellationToken cancellationToken
    )
    {
        var ex = new InteractiveExecutor(context);
        var session = new ChatSession(ex);
        session.AddUserMessage(cmd.Prompt);

        var facts = new StringBuilder();
        var memories = memory.SearchAsync(
            ServiceConstants.QDRANT,
            cmd.Prompt,
            limit:5,
            withEmbeddings:false,
            cancellationToken:cancellationToken
        );
        await foreach (var result in memories)
        {
            facts.Append("<fact relevance:");
            facts.Append(result.Relevance.ToString("P1"));
            facts.AppendLine(">");
            facts.AppendLine(result.Metadata.Text);
            facts.AppendLine("</fact>");
        }

        session.AddSystemMessage(facts.ToString());

        var tokens = session.ChatAsync(
            session.History, cancellationToken: cancellationToken
        );

        await foreach(var token in tokens)
        {
            await kafka.ProduceAsync(
                ServiceConstants.KAFKA, 
                new (){ 
                    Value = token
                }, 
                cancellationToken
            );
        }
    }
}
