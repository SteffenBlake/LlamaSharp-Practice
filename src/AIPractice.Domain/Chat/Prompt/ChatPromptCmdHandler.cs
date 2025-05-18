using AIPractice.Domain.Extensions;

namespace AIPractice.Domain.Chat.Prompt;

public static class ChatPromptCmdHandler 
{
    public static async Task<IDomainResult<Unit>> HandleAsync(
        CommandContext<ChatPromptCmd> ctx, ChatPromptCmd cmd
    )
    {
        await ctx.Channel.BasicPublishAsync(cmd);
        return Unit.Default;
    }
}
