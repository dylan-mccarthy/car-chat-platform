using System.Net.WebSockets;
using System.Text;
using CarChat.Api.Services;
using CarChat.Core.Services;

namespace CarChat.Api.WebSockets;

/// <summary>
/// Middleware that handles WebSocket upgrades for /ws/app and /ws/agent paths.
/// </summary>
public class RelayMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ISessionManager sessionManager,
        IJwtService jwtService,
        IApiKeyService apiKeyService,
        ILogger<RelayMiddleware> logger)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value?.TrimEnd('/') ?? string.Empty;

        if (path.Equals("/ws/app", StringComparison.OrdinalIgnoreCase))
        {
            await HandleAppConnection(context, sessionManager, jwtService, logger);
        }
        else if (path.Equals("/ws/agent", StringComparison.OrdinalIgnoreCase))
        {
            await HandleAgentConnection(context, sessionManager, apiKeyService, logger);
        }
        else
        {
            await next(context);
        }
    }

    private static async Task HandleAppConnection(
        HttpContext context,
        ISessionManager sessionManager,
        IJwtService jwtService,
        ILogger logger)
    {
        var token = context.Request.Query["token"].FirstOrDefault()
            ?? context.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

        var userId = jwtService.ValidateTokenAndGetUserId(token);
        if (userId is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        sessionManager.RegisterApp(userId, ws);
        logger.LogInformation("App connected for user {UserId}", userId);

        try
        {
            await RelayLoop(ws, userId, isApp: true, sessionManager, logger);
        }
        finally
        {
            sessionManager.UnregisterApp(userId);
            logger.LogInformation("App disconnected for user {UserId}", userId);

            var agentSocket = sessionManager.GetAgentSocket(userId);
            if (agentSocket?.State == WebSocketState.Open)
                await SendControlMessage(agentSocket, "peer_disconnected");
        }
    }

    private static async Task HandleAgentConnection(
        HttpContext context,
        ISessionManager sessionManager,
        IApiKeyService apiKeyService,
        ILogger logger)
    {
        var rawKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(rawKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var user = await apiKeyService.ValidateKeyAsync(rawKey);
        if (user is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        sessionManager.RegisterAgent(user.Id, ws);
        logger.LogInformation("Agent connected for user {UserId}", user.Id);

        try
        {
            await RelayLoop(ws, user.Id, isApp: false, sessionManager, logger);
        }
        finally
        {
            sessionManager.UnregisterAgent(user.Id);
            logger.LogInformation("Agent disconnected for user {UserId}", user.Id);

            var appSocket = sessionManager.GetAppSocket(user.Id);
            if (appSocket?.State == WebSocketState.Open)
                await SendControlMessage(appSocket, "peer_disconnected");
        }
    }

    private static async Task RelayLoop(
        WebSocket ws,
        string userId,
        bool isApp,
        ISessionManager sessionManager,
        ILogger logger)
    {
        var buffer = new byte[64 * 1024];

        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            using var ms = new MemoryStream();

            do
            {
                result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            var payload = ms.ToArray();
            var peer = isApp
                ? sessionManager.GetAgentSocket(userId)
                : sessionManager.GetAppSocket(userId);

            if (peer?.State == WebSocketState.Open)
            {
                try
                {
                    await peer.SendAsync(payload, result.MessageType, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to relay message for user {UserId}", userId);
                }
            }
        }
    }

    private static async Task SendControlMessage(WebSocket ws, string type)
    {
        var message = Encoding.UTF8.GetBytes($"{{\"type\":\"{type}\"}}");
        try
        {
            await ws.SendAsync(message, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch { /* peer may already be gone */ }
    }
}
