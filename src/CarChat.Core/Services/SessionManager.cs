using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace CarChat.Core.Services;

public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _appSockets = new();
    private readonly ConcurrentDictionary<string, WebSocket> _agentSockets = new();

    public void RegisterApp(string userId, WebSocket socket) =>
        _appSockets[userId] = socket;

    public void RegisterAgent(string userId, WebSocket socket) =>
        _agentSockets[userId] = socket;

    public void UnregisterApp(string userId) =>
        _appSockets.TryRemove(userId, out _);

    public void UnregisterAgent(string userId) =>
        _agentSockets.TryRemove(userId, out _);

    public WebSocket? GetAppSocket(string userId) =>
        _appSockets.TryGetValue(userId, out var ws) ? ws : null;

    public WebSocket? GetAgentSocket(string userId) =>
        _agentSockets.TryGetValue(userId, out var ws) ? ws : null;

    public SessionStats GetStats()
    {
        var appCount = _appSockets.Count;
        var agentCount = _agentSockets.Count;
        var paired = _appSockets.Keys.Count(k => _agentSockets.ContainsKey(k));
        return new SessionStats(appCount, agentCount, paired);
    }
}
