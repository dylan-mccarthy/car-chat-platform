using CarChat.Api.Models;
using CarChat.Api.Services;
using CarChat.Core.Data;
using CarChat.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarChat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IOAuthValidationService oauthValidator,
    IJwtService jwtService,
    AppDbContext db,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>Exchanges an OAuth provider token for a platform JWT.</summary>
    [HttpPost("token")]
    public async Task<ActionResult<TokenResponse>> ExchangeToken(
        [FromBody] TokenRequest request,
        CancellationToken ct)
    {
        var userInfo = await oauthValidator.ValidateAsync(request.Provider, request.IdentityToken, ct);
        if (userInfo is null)
        {
            logger.LogWarning("OAuth validation failed for provider {Provider}", request.Provider);
            return Unauthorized(new { error = "Invalid or expired OAuth token" });
        }

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Provider == request.Provider && u.ProviderSubjectId == userInfo.SubjectId, ct);

        if (user is null)
        {
            user = new User
            {
                Email = userInfo.Email,
                DisplayName = userInfo.DisplayName,
                Provider = request.Provider.ToLowerInvariant(),
                ProviderSubjectId = userInfo.SubjectId,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("New user registered: {Email} via {Provider}", user.Email, user.Provider);
        }
        else
        {
            if (user.Email != userInfo.Email || user.DisplayName != userInfo.DisplayName)
            {
                user.Email = userInfo.Email;
                user.DisplayName = userInfo.DisplayName;
                await db.SaveChangesAsync(ct);
            }
        }

        var token = jwtService.GenerateToken(user);
        return Ok(new TokenResponse(token, DateTime.UtcNow.AddHours(24)));
    }
}
