using AIPractice.WebApi.Controllers;

namespace AIPractice.WebApi;

public static class Routing 
{
    public static void AddRoutes(this WebApplication app)
    {
        _ = app.MapGet("/api", () => "Hello world!");

        _ = app.MapPost("/api/prompt", ChatController.PostPrompt);
    }
}
