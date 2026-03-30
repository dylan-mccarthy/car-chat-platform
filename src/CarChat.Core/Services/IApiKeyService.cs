using CarChat.Core.Models;

namespace CarChat.Core.Services;

public interface IApiKeyService
{
    /// <summary>Creates a new API key. Returns the raw key (shown once) and the persisted entity.</summary>
    Task<(string RawKey, ApiKey Entity)> CreateKeyAsync(string userId, string name, CancellationToken ct = default);

    Task<IReadOnlyList<ApiKey>> ListKeysAsync(string userId, CancellationToken ct = default);

    /// <summary>Marks a key as revoked. Returns false if key not found or not owned by user.</summary>
    Task<bool> RevokeKeyAsync(string keyId, string userId, CancellationToken ct = default);

    /// <summary>Validates a raw key. Returns the owning User if valid and active, null otherwise. Updates LastUsedAt.</summary>
    Task<User?> ValidateKeyAsync(string rawKey, CancellationToken ct = default);
}
