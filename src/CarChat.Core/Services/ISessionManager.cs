using System.Net.WebSockets;

namespace CarChat.Core.Services;

public interface ISessionManager
{
    void RegisterApp(string userId, WebSocket socket);
    void RegisterAgent(string userId, WebSocket socket);
    void UnregisterApp(string userId);
    void UnregisterAgent(string userId);
    WebSocket? GetAppSocket(string userId);
    WebSocket? GetAgentSocket(string userId);
    SessionStats GetStats();
}

public record SessionStats(int AppConnections, int AgentConnections, int PairedSessions);
