using CarChat.Core.Models;

namespace CarChat.Api.Services;

public interface IJwtService
{
    string GenerateToken(User user);
    string? ValidateTokenAndGetUserId(string token);
}
