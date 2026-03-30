using System.Security.Claims;
using CarChat.Core.Data;
using CarChat.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CarChat.Web.Services;

/// <summary>
/// Resolves the platform User from the current ClaimsPrincipal.
/// </summary>
public class UserSessionService(AppDbContext db)
{
    public async Task<User?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var provider = principal.FindFirstValue("provider");
        var subjectId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (provider is null || subjectId is null) return null;

        return await db.Users.FirstOrDefaultAsync(
            u => u.Provider == provider && u.ProviderSubjectId == subjectId, ct);
    }

    public async Task<User> GetOrCreateUserAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var provider = principal.FindFirstValue("provider")!;
        var subjectId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var name = principal.FindFirstValue(ClaimTypes.Name) ?? email;

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Provider == provider && u.ProviderSubjectId == subjectId, ct);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                DisplayName = name,
                Provider = provider,
                ProviderSubjectId = subjectId,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }
        else if (user.Email != email || user.DisplayName != name)
        {
            user.Email = email;
            user.DisplayName = name;
            await db.SaveChangesAsync(ct);
        }

        return user;
    }
}
