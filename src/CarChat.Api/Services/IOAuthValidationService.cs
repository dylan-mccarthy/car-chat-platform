namespace CarChat.Api.Services;

public interface IOAuthValidationService
{
    /// <summary>
    /// Validates an OAuth token with the provider and returns user info.
    /// Returns null if validation fails.
    /// </summary>
    Task<OAuthUserInfo?> ValidateAsync(string provider, string identityToken, CancellationToken ct = default);
}

public record OAuthUserInfo(string SubjectId, string Email, string DisplayName);
