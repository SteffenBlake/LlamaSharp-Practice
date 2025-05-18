using AIPractice.Domain;
using AIPractice.Domain.Chat.Prompt;
using Microsoft.AspNetCore.Mvc;

namespace AIPractice.WebApi.Controllers;

public static class ChatController 
{
    public static async Task<IDomainResult<Unit>> PostPrompt(
        HttpContext httpContext,
        [FromServices] WebContext<ChatPromptCmd> webContext,
        [FromBody] ChatPromptCmd cmd
    )
    {
        return await Handlers.HandleCommandAsync(
            httpContext, webContext, cmd, ChatPromptCmdHandler.HandleAsync
        );
    }
}
