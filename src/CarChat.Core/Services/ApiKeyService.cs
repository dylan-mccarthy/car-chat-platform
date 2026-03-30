using System.Security.Cryptography;
using System.Text;
using CarChat.Core.Data;
using CarChat.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CarChat.Core.Services;

public class ApiKeyService(AppDbContext db) : IApiKeyService
{
    private const string Prefix = "ccp_";

    public async Task<(string RawKey, ApiKey Entity)> CreateKeyAsync(string userId, string name, CancellationToken ct = default)
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawKey = Prefix + Base64UrlEncode(rawBytes);
        var hash = HashKey(rawKey);

        var entity = new ApiKey
        {
            UserId = userId,
            Name = name,
            KeyHash = hash,
        };

        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync(ct);

        return (rawKey, entity);
    }

    public async Task<IReadOnlyList<ApiKey>> ListKeysAsync(string userId, CancellationToken ct = default) =>
        await db.ApiKeys
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> RevokeKeyAsync(string keyId, string userId, CancellationToken ct = default)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, ct);
        if (key is null) return false;

        key.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<User?> ValidateKeyAsync(string rawKey, CancellationToken ct = default)
    {
        if (!rawKey.StartsWith(Prefix)) return null;

        var hash = HashKey(rawKey);
        var key = await db.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.RevokedAt == null, ct);

        if (key is null) return null;

        key.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return key.User;
    }

    private static string HashKey(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
